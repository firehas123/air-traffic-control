using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>Colours for the placeholder aircraft. Each state also has a shape cue (ring, X) as well as colour.</summary>
    [Serializable]
    public struct AircraftColors
    {
        public Color normal;
        public Color clearedToLand;
        public Color crashed;
        public Color warning;

        public static AircraftColors Default => new AircraftColors
        {
            normal = new Color(1f, 0.85f, 0.2f),
            clearedToLand = new Color(0.3f, 0.9f, 0.4f),
            crashed = new Color(0.9f, 0.15f, 0.15f),
            warning = new Color(1f, 0.2f, 0.2f, 0.9f),
        };
    }

    /// <summary>
    /// The Unity side of one aircraft. Each frame it steps its <see cref="AircraftMotion"/>,
    /// places the plane shape, and draws the part of the path still ahead of it. When its path ends,
    /// it asks the <see cref="LandingSystem"/> whether it lands, and if so rolls out along the runway
    /// and is removed. <see cref="AirspaceSafety"/> turns its warning ring on and off and crashes it.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AircraftController : MonoBehaviour
    {
        const int PathSortingOrder = 0;
        const int WarningSortingOrder = 9;
        const int PlaneSortingOrder = 10;
        const int CrashMarkSortingOrder = 11;

        const float RollDuration = 1f;          // seconds from touchdown to gone
        const float RollEndScale = 0.4f;
        const float CrashDisplaySeconds = 1f;   // how long a crashed aircraft stays on screen

        static readonly List<AircraftController> active = new List<AircraftController>();

        Camera worldCamera;
        LandingSystem landing;
        Mesh normalMesh;
        Mesh clearedMesh;
        Mesh crashedMesh;
        GameObject warningRing;
        GameObject crashMark;
        LineRenderer pathLine;
        Vector3[] linePoints = new Vector3[64];
        float edgeMargin;

        bool isLanding;
        float rollTime;
        Vector2 touchdownPoint;
        Vector2 rollDirection;

        bool isCrashed;
        float crashTime;

        public AircraftMotion Motion { get; private set; }

        /// <summary>All aircraft currently in the scene, including landing and crashed ones.</summary>
        public static IReadOnlyList<AircraftController> Active => active;

        /// <summary>True from touchdown until the aircraft is removed.</summary>
        public bool IsLanding => isLanding;

        public bool IsCrashed => isCrashed;

        /// <summary>Flying: not rolling out on a runway and not crashed. Only airborne aircraft can be selected or collide.</summary>
        public bool IsAirborne => Motion != null && !isLanding && !isCrashed;

        /// <summary>True when the current line connects to a runway, so the aircraft will land at its end.</summary>
        public bool IsClearedToLand { get; private set; }

        public bool IsWarningShown => warningRing != null && warningRing.activeSelf;

        /// <summary>Raised once the landing roll has finished, just before the aircraft is removed.</summary>
        public event Action<AircraftController> Landed;

        /// <summary>Raised once the crash has been shown, just before the aircraft is removed.</summary>
        public event Action<AircraftController> Crashed;

        public static AircraftController Spawn(AircraftMotion motion, Camera camera, LandingSystem landing,
                                               Material material, AircraftColors colors, float edgeMargin)
        {
            var go = new GameObject("Aircraft");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            var aircraft = go.AddComponent<AircraftController>();
            aircraft.landing = landing;
            aircraft.Initialise(motion, camera, material, colors, edgeMargin);
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

        void Initialise(AircraftMotion motion, Camera camera, Material material, AircraftColors colors, float margin)
        {
            Motion = motion;
            worldCamera = camera;
            edgeMargin = margin;

            normalMesh = AircraftShape.CreateMesh(colors.normal);
            clearedMesh = AircraftShape.CreateMesh(colors.clearedToLand);
            crashedMesh = AircraftShape.CreateMesh(colors.crashed);
            GetComponent<MeshFilter>().sharedMesh = normalMesh;
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = PlaneSortingOrder;

            // Danger warning: a ring the size of the proximity warning distance, so two rings touch
            // when the warning starts.
            float ringRadius = SeparationRules.ProximityWarningDistance / 2f;
            Mesh ringMesh = new MeshBuilder().Ring(Vector2.zero, ringRadius - 0.06f, ringRadius, 48, colors.warning).Build("WarningRing");
            warningRing = MeshBuilder.CreateObject("WarningRing", ringMesh, material, WarningSortingOrder, Vector2.zero, 0f);
            warningRing.transform.SetParent(transform, false);
            warningRing.SetActive(false);

            // Crash: a red X over the plane.
            Mesh crossMesh = new MeshBuilder()
                .Bar(new Vector2(-0.3f, -0.3f), new Vector2(0.3f, 0.3f), 0.09f, colors.crashed)
                .Bar(new Vector2(-0.3f, 0.3f), new Vector2(0.3f, -0.3f), 0.09f, colors.crashed)
                .Build("CrashMark");
            crashMark = MeshBuilder.CreateObject("CrashMark", crossMesh, material, CrashMarkSortingOrder, Vector2.zero, 0f);
            crashMark.transform.SetParent(transform, false);
            crashMark.SetActive(false);

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

        /// <summary>Shows whether the aircraft's line connects to a runway (green) or not (normal colour).</summary>
        public void SetClearedToLand(bool cleared)
        {
            if (isCrashed)
                return;
            IsClearedToLand = cleared;
            GetComponent<MeshFilter>().sharedMesh = cleared ? clearedMesh : normalMesh;
        }

        /// <summary>Shows or hides the danger warning ring.</summary>
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
            Motion.ClearPath(); // also ends any stroke being drawn for this aircraft
            pathLine.positionCount = 0;
            warningRing.SetActive(false);
            crashMark.SetActive(true);
            crashMark.transform.rotation = Quaternion.identity;
            GetComponent<MeshFilter>().sharedMesh = crashedMesh;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

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

            bool wasFollowing = Motion.HasPath;
            Motion.Step(Time.deltaTime, TurnBackBounds());

            if (wasFollowing && !Motion.HasPath && landing != null && landing.TryLand(Motion.Path, out Runway runway))
            {
                StartLandingRoll(runway);
                return;
            }

            ApplyTransform();
            UpdatePathLine();

            if (warningRing.activeSelf)
            {
                float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 12f);
                warningRing.transform.localScale = Vector3.one * pulse;
            }
        }

        void StartLandingRoll(Runway runway)
        {
            isLanding = true;
            rollTime = 0f;
            touchdownPoint = Motion.Path[Motion.Path.Count - 1];
            rollDirection = runway.Direction;
            pathLine.positionCount = 0;
            warningRing.SetActive(false);
            transform.SetPositionAndRotation(touchdownPoint, Quaternion.Euler(0f, 0f, runway.LandingHeadingDegrees));
        }

        // Line up with the runway at touchdown, slow evenly to a stop, and shrink away.
        void UpdateLandingRoll()
        {
            rollTime += Time.deltaTime;
            float t = Mathf.Clamp01(rollTime / RollDuration);
            float rolled = Motion.Speed * RollDuration * (t - t * t / 2f); // even deceleration to zero
            transform.position = touchdownPoint + rollDirection * rolled;
            transform.localScale = Vector3.one * Mathf.Lerp(1f, RollEndScale, t);

            if (t >= 1f)
            {
                Landed?.Invoke(this);
                Destroy(gameObject);
            }
        }

        void ApplyTransform()
        {
            transform.SetPositionAndRotation(Motion.Position, Quaternion.Euler(0f, 0f, Motion.HeadingDegrees));
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
