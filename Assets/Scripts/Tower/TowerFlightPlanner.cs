using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Where an arrival is going in Tower mode: a final approach fix (a pose it reaches lined up) and the points after it.
    /// For a runway end the fix is <see cref="TowerSettings.finalLength"/> before the threshold, heading along the runway;
    /// for a pad it is just short of the pad, heading at it.
    /// </summary>
    public readonly struct ApproachTarget
    {
        public ApproachTarget(Vector2 fix, float headingDegrees, Vector2 decisionPoint, Vector2 touchdown, int runway, LandingZoneKind kind)
        {
            Fix = fix;
            HeadingDegrees = headingDegrees;
            DecisionPoint = decisionPoint;
            Touchdown = touchdown;
            Runway = runway;
            Kind = kind;
        }

        public Vector2 Fix { get; }
        public float HeadingDegrees { get; }

        /// <summary>Where an aircraft that has no landing clearance goes around by itself: the runway's threshold, or just short of the pad.</summary>
        public Vector2 DecisionPoint { get; }

        /// <summary>Where a cleared aircraft's path ends: in the middle of the touchdown zone, or the pad's centre.</summary>
        public Vector2 Touchdown { get; }

        /// <summary>Runway index, or -1 for a pad.</summary>
        public int Runway { get; }

        public LandingZoneKind Kind { get; }
        public bool IsRunway => Runway >= 0;
    }

    /// <summary>
    /// The paths aircraft fly in Tower mode (blueprint 16.1): fixed approach routes from the screen edge onto a runway's
    /// final, a landing, a go-around that rejoins the approach about 10 s later, and a hold that circles near the edge.
    /// Every path is a list of points the aircraft follows exactly (as with a drawn line), built from Dubins curves so it
    /// never turns tighter than its turn radius. Plain C#, so it can be unit tested.
    /// </summary>
    public static class TowerFlightPlanner
    {
        /// <summary>Spacing of the points along a path, in world units.</summary>
        public const float Step = 0.06f;

        /// <summary>How far short of a pad's edge the approach to it is lined up.</summary>
        public const float PadFixDistance = 0.9f;

        public static Vector2 TouchdownPoint(Runway runway) =>
            runway.Threshold + runway.Direction * (runway.TouchdownZoneStart + runway.TouchdownZoneLength / 2f);

        public static ApproachTarget ForRunway(Runway runway, int index, float finalLength) =>
            new ApproachTarget(runway.Threshold - runway.Direction * finalLength, runway.LandingHeadingDegrees, runway.Threshold,
                               TouchdownPoint(runway), index, LandingZoneKind.Runway);

        /// <summary>The approach to a pad from <paramref name="from"/>: lined up straight at the pad's centre.</summary>
        public static ApproachTarget ForPad(LandingPad pad, Vector2 from)
        {
            Vector2 toPad = pad.Centre - from;
            Vector2 u = toPad.sqrMagnitude > 1e-6f ? toPad.normalized : Vector2.right;
            float heading = Mathf.Atan2(u.y, u.x) * Mathf.Rad2Deg;
            Vector2 decision = pad.Centre - u * (pad.Radius + 0.15f);
            return new ApproachTarget(pad.Centre - u * (pad.Radius + PadFixDistance), heading, decision, pad.Centre, -1, pad.Kind);
        }

        /// <summary>
        /// From the current pose to the target: onto its final, then to the decision point (no clearance) or on to the
        /// touchdown point (cleared). An aircraft already lined up on that final just flies straight on.
        /// </summary>
        public static List<Vector2> Approach(Vector2 position, float headingDegrees, ApproachTarget target, float turnRadius, bool cleared)
        {
            var points = new List<Vector2> { position };
            if (!IsEstablished(position, headingDegrees, target))
                Dubins.AppendPath(points, position, headingDegrees, target.Fix, target.HeadingDegrees, turnRadius, Step);
            AppendTail(points, target, cleared);
            return points;
        }

        /// <summary>True when the aircraft is on the target's final: on its line, heading along it, before the decision point.</summary>
        public static bool IsEstablished(Vector2 position, float headingDegrees, ApproachTarget target)
        {
            float h = target.HeadingDegrees * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(h), Mathf.Sin(h));
            Vector2 offset = position - target.DecisionPoint;
            float before = -Vector2.Dot(offset, along);
            float sideways = Mathf.Abs(offset.x * along.y - offset.y * along.x);
            float fixDistance = Vector2.Distance(target.Fix, target.DecisionPoint);
            return before > 0.02f && before <= fixDistance + 0.05f && sideways < 0.04f
                   && Mathf.Abs(Mathf.DeltaAngle(headingDegrees, target.HeadingDegrees)) < 3f;
        }

        static void AppendTail(List<Vector2> points, ApproachTarget target, bool cleared)
        {
            AddPoint(points, target.DecisionPoint);
            if (cleared)
                AddPoint(points, target.Touchdown);
        }

        static void AddPoint(List<Vector2> points, Vector2 p)
        {
            if (points.Count == 0 || Vector2.Distance(points[points.Count - 1], p) > 1e-4f)
                points.Add(p);
        }

        /// <summary>
        /// Go around: climbs straight ahead, then turns back and rejoins the target's final about <paramref name="seconds"/>
        /// later at <paramref name="speed"/>. The climb is kept inside <paramref name="view"/> (shrunk by a margin); if the
        /// way back is still too short a full circle is added at the top of the climb. Ends at the decision point, with no
        /// clearance (the player gives Land again).
        /// </summary>
        public static List<Vector2> GoAround(Vector2 position, float headingDegrees, float speed, ApproachTarget target, float turnRadius,
                                             Rect view, float seconds)
        {
            float wanted = Mathf.Max(0.5f, seconds * Mathf.Max(0.05f, speed));
            float h = headingDegrees * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(h), Mathf.Sin(h));
            Rect inner = Shrink(view, Mathf.Min(0.6f, Mathf.Min(view.width, view.height) / 4f));
            float maxClimb = Mathf.Max(0.3f, DistanceInside(position, dir, inner));

            float bestClimb = 0.3f, bestError = float.MaxValue;
            bool bestLoop = false;
            foreach (bool loop in new[] { false, true })
            {
                for (float climb = 0.3f; climb <= maxClimb + 1e-3f; climb += 0.1f)
                {
                    Vector2 top = position + dir * climb;
                    float total = climb + (loop ? 2f * Mathf.PI * turnRadius : 0f)
                                  + Dubins.Length(top, headingDegrees, target.Fix, target.HeadingDegrees, turnRadius);
                    float error = Mathf.Abs(total - wanted);
                    if (error < bestError - 1e-3f)
                    {
                        bestError = error;
                        bestClimb = climb;
                        bestLoop = loop;
                    }
                }
            }

            var points = new List<Vector2> { position };
            Vector2 climbTop = position + dir * bestClimb;
            for (float s = Step; s < bestClimb; s += Step)
                points.Add(position + dir * s);
            AddPoint(points, climbTop);
            if (bestLoop)
                AppendCircle(points, climbTop, headingDegrees, turnRadius, InwardTurn(climbTop, dir, view));
            Dubins.AppendPath(points, climbTop, headingDegrees, target.Fix, target.HeadingDegrees, turnRadius, Step);
            AppendTail(points, target, false);
            return points;
        }

        /// <summary>
        /// Hold: flies to the hold circle around <paramref name="fix"/> and circles it once (counter-clockwise). The caller
        /// adds another circle with <see cref="AppendHoldCircle"/> before this one runs out, so it holds until a new order.
        /// </summary>
        public static List<Vector2> Hold(Vector2 position, float headingDegrees, Vector2 fix, float radius, float turnRadius)
        {
            radius = Mathf.Max(radius, turnRadius);
            Vector2 away = position - fix;
            float angle = away.sqrMagnitude > 1e-6f ? Mathf.Atan2(away.y, away.x) : 0f;
            Vector2 entry = fix + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            float entryHeading = angle * Mathf.Rad2Deg + 90f; // counter-clockwise round the fix
            var points = new List<Vector2> { position };
            if (Vector2.Distance(position, fix) > radius + 1e-3f || Vector2.Distance(position, entry) > 0.05f)
                Dubins.AppendPath(points, position, headingDegrees, entry, entryHeading, turnRadius, Step);
            AppendHoldCircle(points, fix, radius);
            return points;
        }

        /// <summary>Adds one full counter-clockwise circle round <paramref name="fix"/>, from the last point of the list.</summary>
        public static void AppendHoldCircle(List<Vector2> points, Vector2 fix, float radius)
        {
            Vector2 last = points[points.Count - 1];
            Vector2 away = last - fix;
            float start = Mathf.Atan2(away.y, away.x);
            int samples = Mathf.Max(12, Mathf.CeilToInt(2f * Mathf.PI * radius / Step));
            for (int k = 1; k <= samples; k++)
            {
                float a = start + 2f * Mathf.PI * k / samples;
                points.Add(fix + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        public static float HoldCircleLength(float radius) => 2f * Mathf.PI * radius;

        // One full circle that starts and ends at `at` with `headingDegrees`, turning left (+1) or right (-1).
        static void AppendCircle(List<Vector2> points, Vector2 at, float headingDegrees, float radius, float turn)
        {
            float h = headingDegrees * Mathf.Deg2Rad;
            Vector2 centre = at + new Vector2(-Mathf.Sin(h), Mathf.Cos(h)) * radius * turn;
            Vector2 away = at - centre;
            float start = Mathf.Atan2(away.y, away.x);
            int samples = Mathf.Max(12, Mathf.CeilToInt(2f * Mathf.PI * radius / Step));
            for (int k = 1; k <= samples; k++)
            {
                float a = start + turn * 2f * Mathf.PI * k / samples;
                points.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            points[points.Count - 1] = at;
        }

        // Turn towards the middle of the view, so the loop stays on screen.
        static float InwardTurn(Vector2 at, Vector2 dir, Rect view)
        {
            Vector2 toCentre = view.center - at;
            return dir.x * toCentre.y - dir.y * toCentre.x >= 0f ? 1f : -1f;
        }

        static Rect Shrink(Rect r, float by) => new Rect(r.xMin + by, r.yMin + by, Mathf.Max(0.01f, r.width - 2f * by), Mathf.Max(0.01f, r.height - 2f * by));

        // How far from `p` along `dir` before leaving `rect` (0 if already outside).
        static float DistanceInside(Vector2 p, Vector2 dir, Rect rect)
        {
            if (!rect.Contains(p))
                return 0f;
            float t = float.MaxValue;
            if (dir.x > 1e-6f) t = Mathf.Min(t, (rect.xMax - p.x) / dir.x);
            if (dir.x < -1e-6f) t = Mathf.Min(t, (rect.xMin - p.x) / dir.x);
            if (dir.y > 1e-6f) t = Mathf.Min(t, (rect.yMax - p.y) / dir.y);
            if (dir.y < -1e-6f) t = Mathf.Min(t, (rect.yMin - p.y) / dir.y);
            return t == float.MaxValue ? 0f : t;
        }

        /// <summary>Length of a path (sum of its segments).</summary>
        public static float Length(IReadOnlyList<Vector2> points)
        {
            float total = 0f;
            for (int i = 1; i < points.Count; i++)
                total += Vector2.Distance(points[i - 1], points[i]);
            return total;
        }

        /// <summary>
        /// Length from the start of <paramref name="points"/> to the point closest to <paramref name="fix"/> (the time to
        /// rejoin the final, for tests).
        /// </summary>
        public static float LengthTo(IReadOnlyList<Vector2> points, Vector2 fix)
        {
            float total = 0f, best = float.MaxValue, at = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0)
                    total += Vector2.Distance(points[i - 1], points[i]);
                float d = Vector2.Distance(points[i], fix);
                if (d < best - 1e-5f)
                {
                    best = d;
                    at = total;
                }
            }
            return at;
        }
    }
}
