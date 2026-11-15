using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The Unity side of one aircraft. Each frame it steps its <see cref="AircraftMotion"/>,
    /// places the plane shape, and draws the part of the path still ahead of it.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AircraftController : MonoBehaviour
    {
        const int PlaneSortingOrder = 10;
        const int PathSortingOrder = 0;

        static readonly List<AircraftController> active = new List<AircraftController>();

        Camera worldCamera;
        float edgeMargin;
        LineRenderer pathLine;
        Vector3[] linePoints = new Vector3[64];

        public AircraftMotion Motion { get; private set; }

        public static AircraftController Spawn(AircraftMotion motion, Camera camera, Material material, Color color, float edgeMargin)
        {
            var go = new GameObject("Aircraft");
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            var aircraft = go.AddComponent<AircraftController>();
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
                if (aircraft.Motion == null)
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

            GetComponent<MeshFilter>().sharedMesh = AircraftShape.CreateMesh(color);
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

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void Update()
        {
            if (Motion == null)
                return;

            Motion.Step(Time.deltaTime, TurnBackBounds());
            ApplyTransform();
            UpdatePathLine();
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
