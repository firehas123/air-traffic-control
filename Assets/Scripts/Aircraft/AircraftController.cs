using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Status colours, shared by every kind of aircraft (blueprint 13.1). The body keeps its kind's colour; status
    /// is shown as a ring and icon around it, so it reads by shape as well as colour.
    /// </summary>
    [Serializable]
    public struct AircraftColors
    {
        [Tooltip("Ring with a tick: the line lands, the aircraft is cleared.")]
        public Color clearedToLand;

        [Tooltip("Plain ring: the aircraft needs a line that lands.")]
        public Color needsLine;

        [Tooltip("Pulsing ring: danger of a collision.")]
        public Color warning;

        [Tooltip("Body and X after a collision.")]
        public Color crashed;

        public static AircraftColors Default => new AircraftColors
        {
            clearedToLand = new Color(0.3f, 0.9f, 0.4f),
            needsLine = new Color(1f, 0.85f, 0.2f),
            warning = new Color(1f, 0.2f, 0.2f, 0.9f),
            crashed = new Color(0.9f, 0.15f, 0.15f),
        };
    }

    /// <summary>
    /// The Unity side of one aircraft. Each frame it steps its <see cref="AircraftMotion"/>, places the shape of its
    /// <see cref="AircraftType"/>, and draws the part of the path still ahead of it. When its path ends it asks the
    /// <see cref="LandingSystem"/> whether it lands, and if so rolls out along the runway and is removed.
    /// <see cref="AirspaceSafety"/> turns its danger ring on and off and crashes it.
    ///
    /// Status rings (blueprint 13.1): yellow ring = needs a line that lands; green ring with a tick = cleared;
    /// red pulsing ring = danger. The danger ring's radius comes from the kind's collision radius, so two rings touch
    /// when the proximity warning starts.
    /// </summary>
    public sealed class AircraftController : MonoBehaviour
    {
        const int PathSortingOrder = 0;
        const int StatusSortingOrder = 8;
        const int WarningSortingOrder = 9;
        const int PlaneSortingOrder = 10;
        const int CrashMarkSortingOrder = 11;

        const float RollDuration = 1f;          // seconds from touchdown to gone
        const float RollEndScale = 0.4f;
        const float CrashDisplaySeconds = 1f;   // how long a crashed aircraft stays on screen
        const float StatusRingFactor = 0.45f;   // status ring radius = this x the kind's size
        const float RingThickness = 0.05f;

        static readonly List<AircraftController> active = new List<AircraftController>();

        Camera worldCamera;
        LandingSystem landing;
        Mesh bodyMesh;
        Mesh crashedMesh;
        MeshFilter bodyFilter;
        MeshRenderer bodyRenderer;
        AircraftVisuals visuals;
        Transform statusRoot;
        GameObject needsLineRing;
        GameObject clearedRing;
        GameObject warningRing;
        GameObject crashMark;
        LineRenderer pathLine;
        Vector3[] linePoints = new Vector3[64];
        float edgeMargin;

        bool isLanding;
        float rollTime;
        Vector2 touchdownPoint;
        Vector2 rollDirection;
        Touchdown? graceTouchdown;
        bool settlesOntoPad;
        Vector2 padCentre;

        bool isCrashed;
        float crashTime;

        // Carrier (blueprint 13.2, 14.2): the connection, and the dashed tether from the end of the line to the deck.
        CarrierApproach carrierApproach;
        bool landsOnCarrierDeck;
        GameObject tetherObject;
        Mesh tetherMesh;
        static readonly Color TetherColor = new Color(1f, 1f, 1f, 0.9f);

        public AircraftMotion Motion { get; private set; }
        public AircraftType Type { get; private set; }

        public float CollisionRadius => Type != null ? Type.collisionRadius : AircraftType.AirlinerCollisionRadius;

        /// <summary>All aircraft currently in the scene, including landing and crashed ones.</summary>
        public static IReadOnlyList<AircraftController> Active => active;

        /// <summary>True from touchdown until the aircraft is removed.</summary>
        public bool IsLanding => isLanding;

        public bool IsCrashed => isCrashed;

        /// <summary>Flying: not rolling out on a runway and not crashed. Only airborne aircraft can be selected or collide.</summary>
        public bool IsAirborne => Motion != null && !isLanding && !isCrashed;

        /// <summary>True when the current line lands, so the aircraft will touch down at its end.</summary>
        public bool IsClearedToLand { get; private set; }

        /// <summary>True when the aircraft keeps a landing at the old end of a runway after the wind changed (14.3).</summary>
        public bool IsInWindGrace => graceTouchdown.HasValue;

        public bool IsWarningShown => warningRing != null && warningRing.activeSelf;

        /// <summary>True while a fighter's line is connected to the carrier deck.</summary>
        public bool IsConnectedToCarrier => carrierApproach != null && carrierApproach.IsConnected;

        /// <summary>Raised once the landing roll has finished, just before the aircraft is removed.</summary>
        public event Action<AircraftController> Landed;

        /// <summary>Raised at the moment of touchdown (runway, pad or carrier deck), for sound and vibration.</summary>
        public event Action<AircraftController> TouchedDown;

        /// <summary>Raised once the crash has been shown, just before the aircraft is removed.</summary>
        public event Action<AircraftController> Crashed;

        public static AircraftController Spawn(AircraftType type, AircraftMotion motion, Camera camera, LandingSystem landing,
                                               Material material, AircraftColors colors, float edgeMargin)
        {
            var go = new GameObject(type != null ? type.displayName : "Aircraft");
            var aircraft = go.AddComponent<AircraftController>();
            aircraft.landing = landing;
            aircraft.Initialise(type != null ? type : AircraftType.CreateDefaultAirliner(), motion, camera, material, colors, edgeMargin);
            return aircraft;
        }

        /// <summary>Returns the airborne aircraft closest to <paramref name="point"/> within <paramref name="maxDistance"/>, or null.</summary>
        public static AircraftController FindNearest(Vector2 point, float maxDistance)
        {
            AircraftController best = null;
            float bestDistance = maxDistance;
            foreach (AircraftController aircraft in active)
            {
                if (!aircraft.IsAirborne)
                    continue;
                float distance = Vector2.Distance(aircraft.Motion.Position, point);
                if (distance <= bestDistance)
                {
                    best = aircraft;
                    bestDistance = distance;
                }
            }
            return best;
        }

        void Initialise(AircraftType type, AircraftMotion motion, Camera camera, Material material, AircraftColors colors, float margin)
        {
            Type = type;
            Motion = motion;
            worldCamera = camera;
            edgeMargin = margin;

            bodyMesh = AircraftShape.CreateMesh(type.silhouette, type.color, type.accentColor, type.size);
            crashedMesh = AircraftShape.CreateMesh(type.silhouette, colors.crashed, colors.crashed, type.size);
            // The shape lives on a child, so animations can lean or bob it without stretching the status rings.
            GameObject body = MeshBuilder.CreateObject("Body", bodyMesh, material, PlaneSortingOrder, Vector2.zero, 0f);
            body.transform.SetParent(transform, false);
            bodyFilter = body.GetComponent<MeshFilter>();
            bodyRenderer = body.GetComponent<MeshRenderer>();

            visuals = gameObject.AddComponent<AircraftVisuals>();
            visuals.Initialise(this, type, body.transform,
                AircraftShape.CreateMesh(type.silhouette, AircraftVisuals.ShadowColor, AircraftVisuals.ShadowColor, type.size), material);

            // Status rings and the crash mark sit under a child that does not turn with the aircraft,
            // so the tick and the X stay upright.
            statusRoot = new GameObject("Status").transform;
            statusRoot.SetParent(transform, false);

            float statusRadius = StatusRingFactor * type.size;
            needsLineRing = CreateChild("NeedsLineRing",
                new MeshBuilder().Ring(Vector2.zero, statusRadius - RingThickness, statusRadius, 40, colors.needsLine).Build("NeedsLineRing"),
                material, StatusSortingOrder);

            // Cleared: the ring plus a tick at its upper right.
            Vector2 tick = new Vector2(statusRadius * 0.75f, statusRadius * 0.75f);
            clearedRing = CreateChild("ClearedRing", new MeshBuilder()
                .Ring(Vector2.zero, statusRadius - RingThickness, statusRadius, 40, colors.clearedToLand)
                .Ellipse(tick, new Vector2(0.13f, 0.13f), 20, colors.clearedToLand)
                .Bar(tick + new Vector2(-0.07f, 0f), tick + new Vector2(-0.02f, -0.05f), 0.035f, Color.white)
                .Bar(tick + new Vector2(-0.02f, -0.05f), tick + new Vector2(0.07f, 0.06f), 0.035f, Color.white)
                .Build("ClearedRing"), material, StatusSortingOrder);

            // Danger: a ring sized from the collision radius, so two rings touch when the warning starts.
            float warningRadius = SeparationRules.WarningRingRadius(type.collisionRadius);
            warningRing = CreateChild("WarningRing",
                new MeshBuilder().Ring(Vector2.zero, warningRadius - 0.06f, warningRadius, 48, colors.warning).Build("WarningRing"),
                material, WarningSortingOrder);

            float cross = 0.3f * type.size;
            crashMark = CreateChild("CrashMark", new MeshBuilder()
                .Bar(new Vector2(-cross, -cross), new Vector2(cross, cross), 0.09f, colors.crashed)
                .Bar(new Vector2(-cross, cross), new Vector2(cross, -cross), 0.09f, colors.crashed)
                .Build("CrashMark"), material, CrashMarkSortingOrder);

            warningRing.SetActive(false);
            crashMark.SetActive(false);
            ShowStatus();

            var lineObject = new GameObject("Path");
            lineObject.transform.SetParent(transform, false);
            pathLine = lineObject.AddComponent<LineRenderer>();
            pathLine.sharedMaterial = material;
            pathLine.useWorldSpace = true;
            pathLine.widthMultiplier = 0.08f;
            pathLine.numCapVertices = 4;
            pathLine.numCornerVertices = 2;
            pathLine.startColor = pathLine.endColor = new Color(1f, 1f, 1f, 0.85f);
            pathLine.sortingOrder = PathSortingOrder;
            pathLine.positionCount = 0;

            ApplyTransform();
        }

        GameObject CreateChild(string name, Mesh mesh, Material material, int sortingOrder)
        {
            GameObject child = MeshBuilder.CreateObject(name, mesh, material, sortingOrder, Vector2.zero, 0f);
            child.transform.SetParent(statusRoot, false);
            return child;
        }

        /// <summary>Shows whether the aircraft's line lands (green ring and tick) or not (yellow ring).</summary>
        public void SetClearedToLand(bool cleared)
        {
            if (isCrashed)
                return;
            graceTouchdown = null;
            if (!cleared)
                DropCarrierConnection();
            IsClearedToLand = cleared;
            ShowStatus();
        }

        /// <summary>
        /// The fighter's line has connected to the carrier deck (blueprint 13.2). The line stays as drawn; from its end
        /// a dashed tether to the deck moves with the ship (14.2).
        /// </summary>
        public void ConnectToCarrier(CarrierPass pass)
        {
            if (isCrashed || isLanding || Motion.Path.Count == 0)
                return;
            graceTouchdown = null;
            carrierApproach = new CarrierApproach(pass, Motion.Path[Motion.Path.Count - 1]);
            IsClearedToLand = true;
            ShowStatus();
            EnsureTetherObject();
        }

        void DropCarrierConnection()
        {
            carrierApproach = null;
            if (tetherObject != null)
                tetherObject.SetActive(false);
        }

        void EnsureTetherObject()
        {
            if (tetherObject == null)
            {
                // Lives in world space (not a child), so the dashes are not turned or scaled with the aircraft.
                tetherMesh = new Mesh { name = "Tether" };
                tetherObject = MeshBuilder.CreateObject("Tether", tetherMesh, bodyRenderer.sharedMaterial,
                    PathSortingOrder, Vector2.zero, 0f);
            }
            tetherObject.SetActive(true);
        }

        void UpdateTetherView()
        {
            if (carrierApproach == null || !carrierApproach.IsConnected || tetherObject == null)
                return;
            Vector2 from = carrierApproach.State == CarrierApproachState.Tethered ? Motion.Position : carrierApproach.LineEnd;
            var builder = new MeshBuilder();
            MapShapes.BuildDashes(builder, from, carrierApproach.DeckPoint, TetherColor);
            builder.Fill(tetherMesh);
        }

        // The ship began to leave the screen before touchdown (blueprint 13.2): the tether goes, the green ring goes,
        // and a fighter already on the tether flies straight on from where it is (C5).
        void LoseCarrierConnection(bool wasTethered)
        {
            if (wasTethered)
                Motion.Place(Motion.Position, Motion.HeadingDegrees);
            DropCarrierConnection();
            IsClearedToLand = false;
            ShowStatus();
        }

        void UpdateTether(float deltaTime)
        {
            bool touchedDown = carrierApproach.StepTether(Motion.Speed * deltaTime);
            Motion.Place(carrierApproach.TetherPosition, carrierApproach.TetherHeadingDegrees);
            if (touchedDown)
            {
                StartCarrierLanding();
                return;
            }
            ApplyTransform();
            pathLine.positionCount = 0;
            UpdateTetherView();
        }

        void StartCarrierLanding()
        {
            isLanding = true;
            landsOnCarrierDeck = true;
            TouchedDown?.Invoke(this);
            rollTime = 0f;
            pathLine.positionCount = 0;
            warningRing.SetActive(false);
            if (tetherObject != null)
                tetherObject.SetActive(false);
            ShowStatus();
            transform.rotation = Quaternion.Euler(0f, 0f, carrierApproach.Pass.HeadingDegrees);
            statusRoot.rotation = Quaternion.identity;
        }

        /// <summary>
        /// Wind grace period (blueprint 14.3): the wind has moved the touchdown zone, but this aircraft is close
        /// enough to touchdown to keep landing where its line ends, on the runway as it was.
        /// </summary>
        public void KeepLandingDuringWindGrace(Touchdown touchdown)
        {
            if (isCrashed || isLanding)
                return;
            graceTouchdown = touchdown;
            IsClearedToLand = true;
            ShowStatus();
        }

        /// <summary>Shows or hides the danger ring.</summary>
        public void SetWarning(bool on)
        {
            bool show = on && IsAirborne;
            if (warningRing != null && warningRing.activeSelf != show)
                warningRing.SetActive(show);
        }

        /// <summary>Stops the aircraft where it is and shows the crash. It is removed after a moment.</summary>
        public void Crash()
        {
            if (isCrashed || isLanding)
                return;
            isCrashed = true;
            crashTime = 0f;
            graceTouchdown = null;
            DropCarrierConnection();
            Motion.ClearPath(); // also ends any stroke being drawn for this aircraft
            pathLine.positionCount = 0;
            warningRing.SetActive(false);
            crashMark.SetActive(true);
            bodyFilter.sharedMesh = crashedMesh;
            visuals.HideLights();
            ShowStatus();
        }

        void ShowStatus()
        {
            bool flying = !isCrashed && !isLanding;
            needsLineRing.SetActive(flying && !IsClearedToLand);
            clearedRing.SetActive(flying && IsClearedToLand);
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void OnDestroy()
        {
            if (tetherObject != null)
                Destroy(tetherObject);
            if (tetherMesh != null)
                Destroy(tetherMesh);
        }

        void Update()
        {
            if (Motion == null)
                return;

            if (isCrashed)
            {
                crashTime += Time.deltaTime;
                if (crashTime >= CrashDisplaySeconds)
                {
                    Crashed?.Invoke(this);
                    Destroy(gameObject);
                }
                return;
            }

            if (isLanding)
            {
                UpdateLandingRoll();
                return;
            }

            if (carrierApproach != null)
            {
                bool wasTethered = carrierApproach.State == CarrierApproachState.Tethered;
                if (carrierApproach.CheckShip())
                    LoseCarrierConnection(wasTethered);
                else if (wasTethered)
                {
                    UpdateTether(Time.deltaTime);
                    return;
                }
            }

            bool wasFollowing = Motion.HasPath;
            Motion.Step(Time.deltaTime, TurnBackBounds());

            if (wasFollowing && !Motion.HasPath)
            {
                // Connected to the carrier: at the end of the drawn line, carry on along the tether to the deck.
                if (carrierApproach != null && carrierApproach.State == CarrierApproachState.FlyingLine)
                {
                    carrierApproach.StartTether();
                    UpdateTether(0f);
                    return;
                }
                // A landing kept through a wind change lands on the runway as it was; otherwise ask the landing system.
                if (graceTouchdown.HasValue)
                {
                    StartLanding(graceTouchdown.Value);
                    return;
                }
                if (landing != null && landing.TryLand(Motion.Path, Type.landsOn, out Touchdown touchdown))
                {
                    StartLanding(touchdown);
                    return;
                }
                if (IsClearedToLand)
                    SetClearedToLand(false);
            }

            ApplyTransform();
            UpdatePathLine();
            UpdateTetherView();

            if (warningRing.activeSelf)
            {
                float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 12f);
                warningRing.transform.localScale = Vector3.one * pulse;
            }
        }

        void StartLanding(Touchdown touchdown)
        {
            isLanding = true;
            rollTime = 0f;
            graceTouchdown = null;
            touchdownPoint = Motion.Path[Motion.Path.Count - 1];
            pathLine.positionCount = 0;
            warningRing.SetActive(false);
            ShowStatus();

            TouchedDown?.Invoke(this);
            settlesOntoPad = !touchdown.IsRunway;
            visuals.Settling = settlesOntoPad;
            if (settlesOntoPad)
            {
                // Helipad or balloon field: settle onto the centre of the pad.
                padCentre = touchdown.Pad.Centre;
                transform.position = touchdownPoint;
            }
            else
            {
                // Runway: line up with it at touchdown and roll out.
                rollDirection = touchdown.Runway.Direction;
                transform.SetPositionAndRotation(touchdownPoint, Quaternion.Euler(0f, 0f, touchdown.Runway.LandingHeadingDegrees));
            }
            statusRoot.rotation = Quaternion.identity;
        }

        // Runway: slow evenly to a stop along the runway. Pad: drift to its centre. Either way, shrink away.
        void UpdateLandingRoll()
        {
            rollTime += Time.deltaTime;
            float t = Mathf.Clamp01(rollTime / RollDuration);
            if (landsOnCarrierDeck)
            {
                // Roll along the deck, which sails on with the ship.
                float rolledOnDeck = Motion.Speed * RollDuration * (t - t * t / 2f) * 0.5f;
                transform.position = carrierApproach.Pass.ToWorld(carrierApproach.DeckLocalPoint + new Vector2(rolledOnDeck, 0f));
            }
            else if (settlesOntoPad)
            {
                transform.position = Vector2.Lerp(touchdownPoint, padCentre, Mathf.SmoothStep(0f, 1f, t));
            }
            else
            {
                float rolled = Motion.Speed * RollDuration * (t - t * t / 2f); // even deceleration to zero
                transform.position = touchdownPoint + rollDirection * rolled;
            }
            transform.localScale = Vector3.one * Mathf.Lerp(1f, RollEndScale, t);
            visuals.Altitude = 1f - t; // the shadow closes in under the aircraft as it lands

            if (t >= 1f)
            {
                Landed?.Invoke(this);
                Destroy(gameObject);
            }
        }

        void ApplyTransform()
        {
            float heading = Type != null && Type.keepsUpright ? 0f : Motion.HeadingDegrees;
            transform.SetPositionAndRotation(Motion.Position, Quaternion.Euler(0f, 0f, heading));
            statusRoot.rotation = Quaternion.identity;
        }

        /// <summary>The visible area of the camera, shrunk by the edge margin.</summary>
        Rect TurnBackBounds()
        {
            float height = worldCamera.orthographicSize * 2f;
            float width = height * worldCamera.aspect;
            Vector2 centre = worldCamera.transform.position;
            return new Rect(
                centre.x - width / 2f + edgeMargin,
                centre.y - height / 2f + edgeMargin,
                width - 2f * edgeMargin,
                height - 2f * edgeMargin);
        }

        void UpdatePathLine()
        {
            int remaining = Motion.RemainingWaypointCount;
            if (remaining == 0)
            {
                pathLine.positionCount = 0;
                return;
            }

            int count = remaining + 1;
            if (linePoints.Length < count)
                linePoints = new Vector3[Mathf.NextPowerOfTwo(count)];

            // The aircraft is always exactly on the line, so the line ahead starts at the aircraft
            // and then runs through the drawn points unchanged.
            linePoints[0] = Motion.Position;
            for (int i = 0; i < remaining; i++)
                linePoints[i + 1] = Motion.GetRemainingWaypoint(i);

            pathLine.positionCount = count;
            pathLine.SetPositions(linePoints);
        }
    }
}
