using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The Unity side of one aircraft. Each frame it steps its <see cref="AircraftMotion"/>,
    /// places the plane shape, and draws the part of the path still ahead of it. When its path ends,
    /// it asks the <see cref="LandingSystem"/> whether it lands, and if so rolls out along the runway
    /// and is removed.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AircraftController : MonoBehaviour
    {
        const int PlaneSortingOrder = 10;
        const int PathSortingOrder = 0;

        const float RollDuration = 1f;      // seconds from touchdown to gone
        const float RollEndScale = 0.4f;

        static readonly List<AircraftController> active = new List<AircraftController>();

        Camera worldCamera;
        LandingSystem landing;
        Mesh normalMesh;
        Mesh clearedMesh;
        bool isLanding;
        float rollTime;
        Vector2 touchdownPoint;
        Vector2 rollDirection;
        float edgeMargin;
        LineRenderer pathLine;
        Vector3[] linePoints = new Vector3[64];

        public AircraftMotion Motion { get; private set; }

        /// <summary>True from touchdown until the aircraft is removed. A landing aircraft cannot be selected.</summary>
        public bool IsLanding => isLanding;

        /// <summary>Raised once the landing roll has finished, just before the aircraft is removed.</summary>
        public event Action<AircraftController> Landed;

        /// <summary>True when the current line connects to a runway, so the aircraft will land at its end.</summary>
        public bool IsClearedToLand { get; private set; }

        public static AircraftController Spawn(AircraftMotion motion, Camera camera, LandingSystem landing,
                                               Material material, Color color, Color clearedColor, float edgeMargin)
        {
            var go = new GameObject("Aircraft");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            var aircraft = go.AddComponent<AircraftController>();
            aircraft.landing = landing;
            aircraft.clearedMesh = AircraftShape.CreateMesh(clearedColor);
            aircraft.Initialise(motion, camera, material, color, edgeMargin);
            return aircraft;
        }

        /// <summary>Returns the aircraft closest to <paramref name="point"/> within <paramref name="maxDistance"/>, or null.</summary>
        public static AircraftController FindNearest(Vector2 point, float maxDistance)
        {
            AircraftController best = null;
            float bestDistance = maxDistance;
            foreach (AircraftController aircraft in active)
            {
                if (aircraft.Motion == null || aircraft.isLanding)
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

        void Initialise(AircraftMotion motion, Camera camera, Material material, Color color, float margin)
        {
            Motion = motion;
            worldCamera = camera;
            edgeMargin = margin;

            normalMesh = AircraftShape.CreateMesh(color);
            GetComponent<MeshFilter>().sharedMesh = normalMesh;
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = PlaneSortingOrder;

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
            IsClearedToLand = cleared;
            GetComponent<MeshFilter>().sharedMesh = cleared && clearedMesh != null ? clearedMesh : normalMesh;
        }

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void Update()
        {
            if (Motion == null)
                return;

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
        }

        void StartLandingRoll(Runway runway)
        {
            isLanding = true;
            rollTime = 0f;
            touchdownPoint = Motion.Path[Motion.Path.Count - 1];
            rollDirection = runway.Direction;
            pathLine.positionCount = 0;
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
