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

        /// <summary>Material for painted aircraft images (unlit sprites). Set once by the game flow.</summary>
        public static Material SpriteMaterial { get; set; }

        SpriteRenderer bodySprite;
        Color crashedColor;

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

        // Speed in the air (blueprint 15.4): Slow / Fast change only the speed, never the line.
        float normalSpeed;
        GameObject slowIcon;
        GameObject fastIcon;

        // Ground control (blueprint 15.4): a departure from a gate, then its climb out of the screen.
        GroundMovement ground;
        GameObject waitingRing;
        bool departing;
        float climbTime;
        float rollDuration = RollDuration;

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

        /// <summary>
        /// In the air: not on the ground, not rolling out after landing, not crashed. Airborne aircraft are checked for
        /// collisions (including departures climbing out).
        /// </summary>
        public bool IsAirborne => Motion != null && !isLanding && !isCrashed && ground == null;

        /// <summary>In the air and under the player's control: a line can be drawn for it, and it has Slow / Fast.</summary>
        public bool IsControllableInAir => IsAirborne && !departing;

        /// <summary>A departure climbing out after take-off: it leaves the screen by itself.</summary>
        public bool IsDeparting => departing;

        /// <summary>Its ground movement while it is a departure on the ground, otherwise null.</summary>
        public GroundMovement Ground => ground;

        /// <summary>Set each frame by the ground control: a taxiing aircraft waits while this is true (queueing).</summary>
        public bool GroundBlocked { get; set; }

        public AircraftSpeed SpeedSetting { get; private set; } = AircraftSpeed.Normal;

        /// <summary>How this aircraft uses the runway now, for the busy-runway rule (blueprint 15.4).</summary>
        public RunwayUse RunwayUse
        {
            get
            {
                if (isCrashed)
                    return RunwayUse.None;
                if (isLanding)
                    return !settlesOntoPad && !landsOnCarrierDeck ? RunwayUse.Landing : RunwayUse.None;
                if (ground == null)
                    return RunwayUse.None;
                switch (ground.Phase)
                {
                    case GroundPhase.Crossing: return RunwayUse.Crossing;
                    case GroundPhase.LiningUp: return RunwayUse.LineUp;
                    case GroundPhase.TakeOffRoll: return RunwayUse.TakeOff;
                    default: return RunwayUse.None;
                }
            }
        }

        /// <summary>Raised when a departure has climbed out of the screen, just before it is removed.</summary>
        public event Action<AircraftController> Departed;

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

        /// <summary>
        /// A departure parked at a gate (blueprint 15.4). It waits for Taxi; it is on the ground, so it cannot be drawn
        /// for and does not collide until it lifts off.
        /// </summary>
        public static AircraftController SpawnAtGate(AircraftType type, AircraftMotion motion, GroundMovement groundMovement,
                                                     Camera camera, LandingSystem landing, Material material, AircraftColors colors,
                                                     float edgeMargin)
        {
            AircraftController aircraft = Spawn(type, motion, camera, landing, material, colors, edgeMargin);
            aircraft.ground = groundMovement;
            aircraft.visuals.Altitude = 0f;
            aircraft.ShowStatus();
            aircraft.ApplyGroundTransform();
            return aircraft;
        }

        /// <summary>
        /// The aircraft for a tap at <paramref name="point"/>: any aircraft in the air under control or on the ground,
        /// closest first, within <paramref name="maxDistance"/>. Null if none.
        /// </summary>
        public static AircraftController FindNearestForTap(Vector2 point, float maxDistance)
        {
            AircraftController best = null;
            float bestDistance = maxDistance;
            foreach (AircraftController aircraft in active)
            {
                if (!aircraft.IsControllableInAir && aircraft.ground == null)
                    continue;
                float distance = Vector2.Distance(aircraft.transform.position, point);
                if (distance <= bestDistance)
                {
                    best = aircraft;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>Returns the aircraft in the air under control closest to <paramref name="point"/> within <paramref name="maxDistance"/>, or null.</summary>
        public static AircraftController FindNearest(Vector2 point, float maxDistance)
        {
            AircraftController best = null;
            float bestDistance = maxDistance;
            foreach (AircraftController aircraft in active)
            {
                if (!aircraft.IsControllableInAir)
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
            normalSpeed = motion.Speed;
            worldCamera = camera;
            edgeMargin = margin;

            bodyMesh = AircraftShape.CreateMesh(type.silhouette, type.color, type.accentColor, type.size);
            crashedMesh = AircraftShape.CreateMesh(type.silhouette, colors.crashed, colors.crashed, type.size);
            // The shape lives on a child, so animations can lean or bob it without stretching the status rings.
            // With a painted image (art rule of 20 Nov 2026) the image is drawn instead of the code shape.
            crashedColor = colors.crashed;
            bool painted = type.sprite != null && SpriteMaterial != null;
            GameObject body = MeshBuilder.CreateObject("Body", painted ? new Mesh { name = "PaintedBody" } : bodyMesh, material,
                PlaneSortingOrder, Vector2.zero, 0f);
            body.transform.SetParent(transform, false);
            bodyFilter = body.GetComponent<MeshFilter>();
            bodyRenderer = body.GetComponent<MeshRenderer>();
            if (painted)
                bodySprite = CreateSpriteChild(body.transform, type, SpriteMaterial, PlaneSortingOrder);

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

            // Waiting for a command on the ground: a white ring, "tap me" (white is not a status colour).
            waitingRing = CreateChild("WaitingRing",
                new MeshBuilder().Ring(Vector2.zero, statusRadius - RingThickness, statusRadius, 40, new Color(1f, 1f, 1f, 0.9f)).Build("WaitingRing"),
                material, StatusSortingOrder);

            // Speed icon beside the ring (blueprint 15.4): one chevron for slow, two for fast, on a dark disc.
            Vector2 iconAt = new Vector2(statusRadius * 0.8f, -statusRadius * 0.8f);
            slowIcon = CreateChild("SlowIcon", SpeedIcon(iconAt, 1), material, StatusSortingOrder);
            fastIcon = CreateChild("FastIcon", SpeedIcon(iconAt, 2), material, StatusSortingOrder);

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

        /// <summary>
        /// Draws a kind's painted image under <paramref name="parent"/>: turned so the nose points along local +x and
        /// scaled so the image's square is the kind's world size.
        /// </summary>
        public static SpriteRenderer CreateSpriteChild(Transform parent, AircraftType type, Material material, int sortingOrder)
        {
            var go = new GameObject("Image");
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, type.spriteRotation);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = type.sprite;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            float imageSize = Mathf.Max(type.sprite.bounds.size.x, type.sprite.bounds.size.y);
            float scale = imageSize > 0f ? type.spriteWorldSize * type.size / imageSize : 1f;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            return renderer;
        }

        static Mesh SpeedIcon(Vector2 at, int chevrons)
        {
            var b = new MeshBuilder().Ellipse(at, new Vector2(0.13f, 0.13f), 20, new Color(0.08f, 0.14f, 0.2f, 0.9f));
            if (chevrons == 1)
            {
                // Slow: a single chevron with a bar behind it.
                b.Bar(at + new Vector2(-0.06f, -0.06f), at + new Vector2(-0.06f, 0.06f), 0.03f, Color.white);
                b.Bar(at + new Vector2(0.0f, -0.06f), at + new Vector2(0.05f, 0f), 0.03f, Color.white);
                b.Bar(at + new Vector2(0.0f, 0.06f), at + new Vector2(0.05f, 0f), 0.03f, Color.white);
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    float x = -0.05f + i * 0.06f;
                    b.Bar(at + new Vector2(x, -0.06f), at + new Vector2(x + 0.05f, 0f), 0.03f, Color.white);
                    b.Bar(at + new Vector2(x, 0.06f), at + new Vector2(x + 0.05f, 0f), 0.03f, Color.white);
                }
            }
            return b.Build(chevrons == 1 ? "SlowIcon" : "FastIcon");
        }

        /// <summary>
        /// Slow or Fast (blueprint 15.4): the kind's speed times its slow or fast factor. Choosing the active setting
        /// again returns to normal. Only the speed changes; the drawn line and the aircraft's place on it stay exact.
        /// </summary>
        public void ToggleSpeed(AircraftSpeed requested)
        {
            if (!IsControllableInAir)
                return;
            SpeedSetting = SpeedRules.Toggle(SpeedSetting, requested);
            Motion.SetSpeed(normalSpeed * SpeedRules.Factor(Type, SpeedSetting));
            ShowStatus();
        }

        /// <summary>Taxi, Cross or Take off for a departure on the ground. Returns false if it does not apply now.</summary>
        public bool GiveGroundCommand(AircraftCommand command)
        {
            if (ground == null || isCrashed)
                return false;
            bool done;
            switch (command)
            {
                case AircraftCommand.Taxi: done = ground.Taxi(); break;
                case AircraftCommand.Cross: done = ground.Cross(); break;
                case AircraftCommand.TakeOff:
                    done = landing != null && landing.Runways.Count > 0 && ground.TakeOff(landing.Runways[0]);
                    break;
                default: done = false; break;
            }
            ShowStatus();
            return done;
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
            // A landing roll can crash too: the busy-runway rule (blueprint 15.4) can hit an aircraft on the runway.
            if (isCrashed)
                return;
            isCrashed = true;
            crashTime = 0f;
            graceTouchdown = null;
            DropCarrierConnection();
            Motion.ClearPath(); // also ends any stroke being drawn for this aircraft
            pathLine.positionCount = 0;
            warningRing.SetActive(false);
            crashMark.SetActive(true);
            if (bodySprite != null)
                bodySprite.color = crashedColor;
            else
                bodyFilter.sharedMesh = crashedMesh;
            ShowStatus();
        }

        void ShowStatus()
        {
            bool flying = !isCrashed && !isLanding && ground == null && !departing;
            needsLineRing.SetActive(flying && !IsClearedToLand);
            clearedRing.SetActive(flying && IsClearedToLand);
            waitingRing.SetActive(!isCrashed && ground != null && ground.IsWaitingForCommand);
            slowIcon.SetActive(flying && SpeedSetting == AircraftSpeed.Slow);
            fastIcon.SetActive(flying && SpeedSetting == AircraftSpeed.Fast);
        }

        void ApplyGroundTransform()
        {
            Motion.Place(ground.Position, ground.HeadingDegrees);
            transform.SetPositionAndRotation(ground.Position, Quaternion.Euler(0f, 0f, ground.HeadingDegrees));
            statusRoot.rotation = Quaternion.identity;
        }

        // On the ground: taxi (waiting behind others), cross, line up and roll. At lift-off it becomes a departure in
        // the air, flying straight out of the screen in the runway direction.
        void UpdateGround(float deltaTime)
        {
            if (landing != null)
                ground.SetActiveEnd(landing.IsReversed);
            GroundPhase before = ground.Phase;
            ground.Step(deltaTime, GroundBlocked);
            ApplyGroundTransform();
            if (ground.Phase != before)
                ShowStatus();

            if (ground.Phase == GroundPhase.TakeOffRoll)
                visuals.Altitude = 0f;

            if (ground.Phase == GroundPhase.Airborne)
            {
                float heading = ground.HeadingDegrees;
                Vector2 position = ground.Position;
                ground = null;
                departing = true;
                climbTime = 0f;
                Motion.Place(position, heading);
                ShowStatus();
            }

            if (waitingRing.activeSelf)
            {
                float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 5f);
                waitingRing.transform.localScale = Vector3.one * pulse;
            }
        }

        void UpdateDeparting(float deltaTime)
        {
            // Straight out in the runway direction; no turning back at the edge.
            Motion.Step(deltaTime, new Rect(-1000f, -1000f, 2000f, 2000f));
            climbTime += deltaTime;
            visuals.Altitude = Mathf.Clamp01(climbTime / 1.5f);
            ApplyTransform();

            float height = worldCamera.orthographicSize;
            float width = height * worldCamera.aspect;
            Vector2 p = Motion.Position - (Vector2)worldCamera.transform.position;
            if (Mathf.Abs(p.x) > width + 1f || Mathf.Abs(p.y) > height + 1f)
            {
                Departed?.Invoke(this);
                Destroy(gameObject);
            }
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

            if (ground != null)
            {
                UpdateGround(Time.deltaTime);
                return;
            }

            if (departing)
            {
                UpdateDeparting(Time.deltaTime);
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
            // Fast lands sooner but rolls further, so the runway stays busy for longer (blueprint 15.4).
            rollDuration = RollDuration * (normalSpeed > 0f ? Motion.Speed / normalSpeed : 1f);
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
            float t = Mathf.Clamp01(rollTime / rollDuration);
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
                float rolled = Motion.Speed * rollDuration * (t - t * t / 2f); // even deceleration to zero
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
