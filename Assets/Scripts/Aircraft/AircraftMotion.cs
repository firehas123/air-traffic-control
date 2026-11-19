using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Movement model for one aircraft. Plain C#, no Unity components, so it can be unit tested.
    ///
    /// Following a path: the aircraft moves along the drawn line at constant speed and is always
    /// exactly on it. Its position is a distance along the line, and its heading is the direction of the
    /// segment it is on, so it turns instantly at every corner, including a full reversal. The path
    /// points are never changed. Leaving the line throws <see cref="PathAlteredException"/>.
    ///
    /// Rules (blueprint C5):
    /// - With no path, the aircraft flies straight and turns back, at its limited turn rate, when it
    ///   reaches the turn-back bounds.
    /// - When a path ends, the aircraft carries on along the direction of the last segment.
    ///
    /// Headings are in degrees: 0 points along +x and angles increase counter-clockwise.
    /// </summary>
    public sealed class AircraftMotion
    {
        /// <summary>How far the aircraft may be from its segment before it counts as off the line (float rounding only).</summary>
        const float OnLineTolerance = 1e-3f;

        readonly List<Vector2> path = new List<Vector2>();
        readonly List<float> distanceAtPoint = new List<float>(); // distance along the line at each point

        int segment;          // the aircraft is on path[segment] -> path[segment + 1]
        float distanceAlong;  // how far along the line the aircraft is
        bool following;
        float? turnBackHeading;

        public AircraftMotion(Vector2 position, float headingDegrees, float speed, float turnRateDegrees)
        {
            Position = position;
            HeadingDegrees = Mathf.Repeat(headingDegrees, 360f);
            Speed = Mathf.Max(0f, speed);
            TurnRateDegrees = Mathf.Max(1f, turnRateDegrees);
        }

        public Vector2 Position { get; private set; }
        public float HeadingDegrees { get; private set; }
        public float Speed { get; }

        /// <summary>Turn rate used only when flying without a path (the edge turn-back).</summary>
        public float TurnRateDegrees { get; }

        public Vector2 HeadingVector
        {
            get
            {
                float radians = HeadingDegrees * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            }
        }

        /// <summary>Current velocity: heading times speed.</summary>
        public Vector2 Velocity => HeadingVector * Speed;

        /// <summary>Radius of the tightest circle the aircraft flies when turning back at an edge.</summary>
        public float TurnRadius => Speed / (TurnRateDegrees * Mathf.Deg2Rad);

        /// <summary>True while the aircraft is following a drawn path.</summary>
        public bool HasPath => following;

        /// <summary>The full path being followed, exactly as drawn. Read-only.</summary>
        public IReadOnlyList<Vector2> Path => path;

        public float PathLength => distanceAtPoint.Count == 0 ? 0f : distanceAtPoint[distanceAtPoint.Count - 1];
        public float DistanceAlongPath => distanceAlong;

        /// <summary>How much of the line is still ahead (0 when not following a path).</summary>
        public float RemainingPathLength => following ? Mathf.Max(0f, PathLength - distanceAlong) : 0f;

        /// <summary>Game seconds until the aircraft reaches the end of its line; infinity when not following one.</summary>
        public float SecondsToEndOfPath => following && Speed > 0f ? RemainingPathLength / Speed : float.PositiveInfinity;

        /// <summary>Points of the line still ahead of the aircraft, in drawing order.</summary>
        public int RemainingWaypointCount => following ? path.Count - segment - 1 : 0;
        public Vector2 GetRemainingWaypoint(int index) => path[segment + 1 + index];

        public void ClearPath()
        {
            path.Clear();
            distanceAtPoint.Clear();
            segment = 0;
            distanceAlong = 0f;
            following = false;
        }

        /// <summary>
        /// Puts the aircraft at a position and heading with no path. Used only when something other than a drawn line
        /// moves it: the carrier tether (blueprint 14.2). It then flies straight on and turns back at the edges (C5).
        /// </summary>
        public void Place(Vector2 position, float headingDegrees)
        {
            ClearPath();
            Position = position;
            HeadingDegrees = Mathf.Repeat(headingDegrees, 360f);
        }

        /// <summary>Starts a new path at the aircraft's current position, replacing any old one.</summary>
        public void BeginPath()
        {
            ClearPath();
            path.Add(Position);
            distanceAtPoint.Add(0f);
            following = true;
        }

        /// <summary>
        /// Adds the next point of the line. Returns false, and adds nothing, if there is no path to add
        /// to, for example because the aircraft has already flown off the end of it.
        /// </summary>
        public bool AppendWaypoint(Vector2 point)
        {
            if (!following)
                return false;

            Vector2 last = path[path.Count - 1];
            path.Add(point);
            distanceAtPoint.Add(distanceAtPoint[distanceAtPoint.Count - 1] + Vector2.Distance(last, point));
            return true;
        }

        /// <summary>Advances the aircraft by <paramref name="deltaTime"/> seconds.</summary>
        public void Step(float deltaTime, Rect turnBackBounds)
        {
            if (deltaTime <= 0f)
                return;

            if (following)
            {
                StepAlongPath(Speed * deltaTime);
                return;
            }

            float desiredHeading = HeadingDegrees;
            UpdateTurnBack(turnBackBounds);
            if (turnBackHeading.HasValue)
                desiredHeading = turnBackHeading.Value;

            float maxTurn = TurnRateDegrees * deltaTime;
            HeadingDegrees = Mathf.Repeat(Mathf.MoveTowardsAngle(HeadingDegrees, desiredHeading, maxTurn), 360f);
            Position += HeadingVector * (Speed * deltaTime);
        }

        void StepAlongPath(float distance)
        {
            distanceAlong += distance;
            turnBackHeading = null;

            float length = PathLength;
            if (distanceAlong >= length)
            {
                // End of the line: finish exactly on the last point, then carry straight on (C5).
                float beyond = distanceAlong - length;
                segment = Mathf.Max(0, path.Count - 2);
                SetHeadingFromSegmentsUpTo(path.Count - 1);
                Position = path[path.Count - 1] + HeadingVector * beyond;
                following = false;
                segment = path.Count - 1;
                return;
            }

            while (segment < path.Count - 2 && distanceAtPoint[segment + 1] <= distanceAlong)
                segment++;

            Vector2 a = path[segment];
            Vector2 b = path[segment + 1];
            float segmentLength = distanceAtPoint[segment + 1] - distanceAtPoint[segment];
            float t = segmentLength > 0f ? (distanceAlong - distanceAtPoint[segment]) / segmentLength : 0f;
            Position = Vector2.Lerp(a, b, t);
            SetHeadingFromSegmentsUpTo(segment + 1);

            CheckOnLine();
        }

        /// <summary>Heading = direction of the last non-zero-length segment ending at or before point <paramref name="endIndex"/>.</summary>
        void SetHeadingFromSegmentsUpTo(int endIndex)
        {
            for (int i = endIndex; i >= 1; i--)
            {
                Vector2 delta = path[i] - path[i - 1];
                if (delta.sqrMagnitude > 0f)
                {
                    HeadingDegrees = Mathf.Repeat(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, 360f);
                    return;
                }
            }
        }

        void CheckOnLine()
        {
            Vector2 a = path[segment];
            Vector2 b = path[segment + 1];
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(Position - a, ab) / ab.sqrMagnitude) : 0f;
            float offLine = Vector2.Distance(Position, a + ab * t);
            if (offLine > OnLineTolerance)
                throw new PathAlteredException($"Aircraft is {offLine} units off the drawn line on segment {segment}.");
        }

        /// <summary>
        /// When the aircraft crosses an edge while heading out (or along it), pick the mirrored
        /// heading, like a ball bouncing off a wall, and keep turning towards it until the aircraft
        /// is back inside. The target is kept from frame to frame instead of being re-mirrored from
        /// the current heading, which would let the aircraft settle flying along the edge just outside it.
        /// </summary>
        void UpdateTurnBack(Rect bounds)
        {
            if (bounds.Contains(Position))
            {
                turnBackHeading = null;
                return;
            }
            // Start from the target already chosen, if any, and correct it for every edge the aircraft
            // is now outside. Near a corner, a target mirrored off one edge can still point out
            // through the other.
            float baseHeading = turnBackHeading ?? HeadingDegrees;
            float radians = baseHeading * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            bool turn = false;
            turn |= PointInwards(ref direction.x, Position.x, bounds.xMin, bounds.xMax);
            turn |= PointInwards(ref direction.y, Position.y, bounds.yMin, bounds.yMax);
            if (turn)
                turnBackHeading = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// If the position is outside [min, max] on this axis and the heading does not point back in,
        /// flip that heading component so it points inwards. Returns true if it changed.
        /// </summary>
        static bool PointInwards(ref float component, float position, float min, float max)
        {
            const float MinInward = 0.3f; // so a plane sliding along the edge still turns back in
            float inward;
            if (position < min) inward = 1f;
            else if (position > max) inward = -1f;
            else return false;

            if (component * inward > 0f)
                return false; // already heading back in

            component = inward * Mathf.Max(Mathf.Abs(component), MinInward);
            return true;
        }
    }
}
