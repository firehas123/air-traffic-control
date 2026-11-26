using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>What a runway looks like to the player under the map rules: open, closing soon (the X blinks) or closed.</summary>
    public enum RunwayState
    {
        Open = 0,
        Closing = 1,
        Closed = 2,
    }

    /// <summary>
    /// City rule (blueprint 16.2): one runway at a time closes for 10 to 15 s, after a 5 s warning. Runs on game time.
    /// Runways take turns in a shuffled order, so each one closes before any closes twice. Plain C#, so it can be unit tested.
    /// </summary>
    public sealed class RunwayClosureSchedule
    {
        public enum Phase { Open, Warning, Closed }

        readonly RunwayClosureSettings settings;
        readonly int runwayCount;
        readonly System.Random random;
        readonly List<int> order = new List<int>();

        public RunwayClosureSchedule(RunwayClosureSettings settings, int runwayCount, int seed)
        {
            this.settings = settings;
            this.runwayCount = Mathf.Max(0, runwayCount);
            random = new System.Random(seed);
            CurrentPhase = Phase.Open;
            SecondsLeft = Mathf.Max(0f, settings.firstWarningAfterSeconds);
            Runway = -1;
        }

        public Phase CurrentPhase { get; private set; }

        /// <summary>The runway that is closing or closed, or -1 while all are open.</summary>
        public int Runway { get; private set; }

        /// <summary>Game seconds until the current phase ends.</summary>
        public float SecondsLeft { get; private set; }

        public int WholeSecondsLeft => Mathf.Max(1, Mathf.CeilToInt(SecondsLeft));

        public RunwayState StateOf(int runway)
        {
            if (runway != Runway)
                return RunwayState.Open;
            return CurrentPhase == Phase.Closed ? RunwayState.Closed : CurrentPhase == Phase.Warning ? RunwayState.Closing : RunwayState.Open;
        }

        /// <summary>Advances the schedule. Returns true when a runway closed or opened (landing zones changed).</summary>
        public bool Tick(float deltaTime)
        {
            if (runwayCount == 0 || deltaTime <= 0f)
                return false;
            // A level with a single runway never closes it: there would be nowhere to land.
            if (runwayCount < 2)
                return false;

            bool changed = false;
            SecondsLeft -= deltaTime;
            while (SecondsLeft <= 0f)
            {
                switch (CurrentPhase)
                {
                    case Phase.Open:
                        CurrentPhase = Phase.Warning;
                        Runway = NextRunway();
                        SecondsLeft += settings.warningSeconds;
                        break;
                    case Phase.Warning:
                        CurrentPhase = Phase.Closed;
                        SecondsLeft += Range(settings.minClosedSeconds, settings.maxClosedSeconds);
                        changed = true;
                        break;
                    default:
                        CurrentPhase = Phase.Open;
                        Runway = -1;
                        SecondsLeft += Mathf.Max(0.5f, Range(settings.minOpenSeconds, settings.maxOpenSeconds));
                        changed = true;
                        break;
                }
            }
            return changed;
        }

        int NextRunway()
        {
            if (order.Count == 0)
            {
                for (int i = 0; i < runwayCount; i++)
                    order.Add(i);
                for (int i = order.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    (order[i], order[j]) = (order[j], order[i]);
                }
            }
            int next = order[order.Count - 1];
            order.RemoveAt(order.Count - 1);
            return next;
        }

        float Range(float min, float max) => min + (float)random.NextDouble() * Mathf.Max(0f, max - min);
    }

    /// <summary>
    /// Desert rule (blueprint 16.2): a sandstorm, a circle that drifts slowly in a straight line across the screen through
    /// the airport. When it has left the screen, the next one comes from another side after a pause. A runway is closed while
    /// the storm covers any part of it. Plain C#, so it can be unit tested.
    /// </summary>
    public sealed class Sandstorm
    {
        readonly SandstormSettings settings;
        readonly Rect view;
        readonly Rect airport;
        readonly System.Random random;
        float waitLeft;
        int side;

        public Sandstorm(SandstormSettings settings, Rect view, Rect airport, int seed)
        {
            this.settings = settings;
            this.view = view;
            this.airport = airport;
            random = new System.Random(seed);
            side = random.Next(4);
            waitLeft = settings.firstStormAfterSeconds;
        }

        public bool IsActive { get; private set; }
        public Vector2 Centre { get; private set; }
        public Vector2 Velocity { get; private set; }
        public float Radius => settings.radius;

        /// <summary>Starts a storm now from <paramref name="from"/>, drifting towards <paramref name="through"/>. For tests and the first storm.</summary>
        public void Start(Vector2 from, Vector2 through)
        {
            IsActive = true;
            Centre = from;
            Vector2 direction = through - from;
            Velocity = (direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.right) * settings.speed;
        }

        /// <summary>Advances the storm (game time).</summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;
            if (!IsActive)
            {
                waitLeft -= deltaTime;
                if (waitLeft <= 0f)
                    StartNext();
                return;
            }

            Centre += Velocity * deltaTime;
            // Gone once it is fully off screen and moving away.
            Rect outer = new Rect(view.xMin - Radius, view.yMin - Radius, view.width + 2f * Radius, view.height + 2f * Radius);
            if (!outer.Contains(Centre) && Vector2.Dot(Velocity, Centre - view.center) > 0f)
            {
                IsActive = false;
                waitLeft = Range(settings.minSecondsBetweenStorms, settings.maxSecondsBetweenStorms);
            }
        }

        void StartNext()
        {
            // Enter just off screen on the next side (clockwise), aimed at a random point of the airport.
            side = (side + 1 + random.Next(3)) % 4;
            float r = Radius + 0.05f;
            float u = (float)random.NextDouble();
            Vector2 from;
            switch (side)
            {
                case 0: from = new Vector2(view.xMin - r, Mathf.Lerp(view.yMin, view.yMax, u)); break;
                case 1: from = new Vector2(Mathf.Lerp(view.xMin, view.xMax, u), view.yMax + r); break;
                case 2: from = new Vector2(view.xMax + r, Mathf.Lerp(view.yMin, view.yMax, u)); break;
                default: from = new Vector2(Mathf.Lerp(view.xMin, view.xMax, u), view.yMin - r); break;
            }
            Vector2 through = new Vector2(Mathf.Lerp(airport.xMin, airport.xMax, (float)random.NextDouble()),
                                          Mathf.Lerp(airport.yMin, airport.yMax, (float)random.NextDouble()));
            Start(from, through);
        }

        /// <summary>True if the storm covers any part of the runway now.</summary>
        public bool Covers(Runway runway) => IsActive && CircleTouchesRunway(Centre, Radius, runway);

        /// <summary>True if the storm will cover the runway within <paramref name="seconds"/> (and does not yet).</summary>
        public bool WillCoverWithin(Runway runway, float seconds)
        {
            if (!IsActive || Covers(runway))
                return false;
            const int steps = 10;
            for (int i = 1; i <= steps; i++)
                if (CircleTouchesRunway(Centre + Velocity * (seconds * i / steps), Radius, runway))
                    return true;
            return false;
        }

        public RunwayState StateOf(Runway runway) =>
            Covers(runway) ? RunwayState.Closed : WillCoverWithin(runway, settings.warningSeconds) ? RunwayState.Closing : RunwayState.Open;

        /// <summary>True if a circle overlaps the runway's rectangle.</summary>
        public static bool CircleTouchesRunway(Vector2 centre, float radius, Runway runway)
        {
            Vector2 local = runway.ToLocal(centre);
            float dx = Mathf.Max(0f, Mathf.Abs(local.x) - runway.Length / 2f);
            float dy = Mathf.Max(0f, Mathf.Abs(local.y) - runway.Width / 2f);
            return dx * dx + dy * dy < radius * radius;
        }

        float Range(float min, float max) => min + (float)random.NextDouble() * Mathf.Max(0f, max - min);
    }

    /// <summary>Desert rule (blueprint 16.2): which new aircraft arrive low on fuel, and with how much. Plain C#, so it can be unit tested.</summary>
    public sealed class FuelRules
    {
        readonly FuelSettings settings;
        readonly System.Random random;
        int spawned;

        public FuelRules(FuelSettings settings, int seed)
        {
            this.settings = settings;
            random = new System.Random(seed);
        }

        /// <summary>
        /// Called for every new aircraft in order. Returns its fuel in game seconds, or null when it has plenty (no timer).
        /// The second aircraft is the first low-fuel one, then every <see cref="FuelSettings.lowFuelEvery"/>th.
        /// </summary>
        public float? NextAircraft(AircraftType kind)
        {
            spawned++;
            if (!settings.enabled || kind == null || kind.speedMultiplier < settings.slowestKind)
                return null;
            int every = Mathf.Max(1, settings.lowFuelEvery);
            if ((spawned - 2) % every != 0 || spawned < 2)
                return null;
            float seconds = settings.minFuelSeconds + (float)random.NextDouble() * Mathf.Max(0f, settings.maxFuelSeconds - settings.minFuelSeconds);
            return seconds / Mathf.Max(0.05f, kind.speedMultiplier);
        }
    }

    /// <summary>
    /// Mountain Valley rule (blueprint 16.2): no-fly areas as polygons in world units. A drawn line cannot enter them;
    /// an aircraft flying straight turns back at them as it does at the screen edge (C5). Plain C#, so it can be unit tested.
    /// </summary>
    public sealed class NoFlyZones
    {
        readonly List<List<Vector2>> areas = new List<List<Vector2>>();

        public NoFlyZones(IEnumerable<NoFlyArea> source)
        {
            if (source == null)
                return;
            foreach (NoFlyArea area in source)
                if (area != null && area.points != null && area.points.Count >= 3)
                    areas.Add(new List<Vector2>(area.points));
        }

        public NoFlyZones(IEnumerable<List<Vector2>> polygons)
        {
            foreach (List<Vector2> polygon in polygons)
                if (polygon != null && polygon.Count >= 3)
                    areas.Add(new List<Vector2>(polygon));
        }

        public int Count => areas.Count;
        public IReadOnlyList<Vector2> Area(int index) => areas[index];

        public bool Contains(Vector2 point) => IndexAt(point) >= 0;

        /// <summary>Index of the area that contains the point, or -1.</summary>
        public int IndexAt(Vector2 point)
        {
            for (int i = 0; i < areas.Count; i++)
                if (PolygonContains(areas[i], point))
                    return i;
            return -1;
        }

        /// <summary>
        /// True if a line segment from <paramref name="from"/> to <paramref name="to"/> goes into a no-fly area. A segment
        /// that starts inside an area (an aircraft that drifted into a corner) may leave it, but not enter another one.
        /// </summary>
        public bool SegmentEnters(Vector2 from, Vector2 to)
        {
            int startArea = IndexAt(from);
            for (int i = 0; i < areas.Count; i++)
            {
                if (i == startArea)
                    continue;
                if (PolygonContains(areas[i], to) || SegmentCrossesPolygon(areas[i], from, to))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// For an aircraft at <paramref name="position"/> heading <paramref name="headingDegrees"/>: if a point
        /// <paramref name="lookAhead"/> ahead is in an area (and the aircraft itself is not), the heading to turn to, mirrored
        /// off the mountain's edge like a ball off a wall. Null when nothing is ahead.
        /// </summary>
        public float? TurnBackHeading(Vector2 position, float headingDegrees, float lookAhead) =>
            TurnBackHeading(position, headingDegrees, lookAhead, out _);

        /// <summary>As above, also giving the edge's normal, pointing out of the mountain towards the aircraft.</summary>
        public float? TurnBackHeading(Vector2 position, float headingDegrees, float lookAhead, out Vector2 outward)
        {
            outward = Vector2.zero;
            if (Contains(position))
                return null;
            float radians = headingDegrees * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            Vector2 ahead = position + direction * lookAhead;
            int area = IndexAt(ahead);
            if (area < 0)
            {
                // A thin spike between here and the look-ahead point counts too.
                for (int i = 0; i < areas.Count && area < 0; i++)
                    if (SegmentCrossesPolygon(areas[i], position, ahead))
                        area = i;
                if (area < 0)
                    return null;
            }

            Vector2 normal = EdgeNormalFacing(areas[area], position, ahead);
            outward = normal;
            Vector2 mirrored = direction - 2f * Vector2.Dot(direction, normal) * normal;
            // Always point clearly away from the edge, so an aircraft sliding along it still turns back out.
            const float MinOutward = 0.3f;
            float away = Vector2.Dot(mirrored, normal);
            if (away < MinOutward)
                mirrored = (mirrored + normal * (MinOutward - away)).normalized;
            return Mathf.Repeat(Mathf.Atan2(mirrored.y, mirrored.x) * Mathf.Rad2Deg, 360f);
        }

        // Unit normal of the area's edge that the path from position to ahead crosses first (or the nearest edge),
        // pointing out of the area towards the aircraft.
        static Vector2 EdgeNormalFacing(List<Vector2> polygon, Vector2 position, Vector2 ahead)
        {
            int best = -1;
            float bestT = float.MaxValue;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                if (SegmentIntersection(position, ahead, polygon[j], polygon[i], out float t) && t < bestT)
                {
                    bestT = t;
                    best = i;
                }
            }
            if (best < 0)
            {
                float bestDistance = float.MaxValue;
                for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
                {
                    float d = DistanceToSegment(ahead, polygon[j], polygon[i]);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = i;
                    }
                }
            }
            Vector2 a = polygon[(best - 1 + polygon.Count) % polygon.Count];
            Vector2 b = polygon[best];
            Vector2 edge = b - a;
            Vector2 normal = new Vector2(-edge.y, edge.x).normalized;
            if (Vector2.Dot(position - a, normal) < 0f)
                normal = -normal;
            return normal;
        }

        public static bool PolygonContains(IReadOnlyList<Vector2> polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        static bool SegmentCrossesPolygon(List<Vector2> polygon, Vector2 from, Vector2 to)
        {
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
                if (SegmentIntersection(from, to, polygon[j], polygon[i], out _))
                    return true;
            return false;
        }

        // True if segment p→p2 crosses segment q→q2; t is the fraction along p→p2.
        static bool SegmentIntersection(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2, out float t)
        {
            t = 0f;
            Vector2 r = p2 - p, s = q2 - q;
            float denominator = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(denominator) < 1e-9f)
                return false;
            Vector2 qp = q - p;
            t = (qp.x * s.y - qp.y * s.x) / denominator;
            float u = (qp.x * r.y - qp.y * r.x) / denominator;
            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }

        static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(point, a + ab * t);
        }
    }

    /// <summary>Arctic rule (blueprint 16.2): icy runways. Plain C#, so it can be unit tested.</summary>
    public static class IceRules
    {
        /// <summary>How many times longer the landing roll is: the slide factor when landing on Fast, otherwise 1.</summary>
        public static float RollFactor(IceSettings ice, AircraftSpeed speed) =>
            ice != null && ice.enabled && speed == AircraftSpeed.Fast ? Mathf.Max(1f, ice.fastSlideFactor) : 1f;

        /// <summary>
        /// On an icy runway the whole runway is busy while an aircraft rolls or slides on it: another aircraft touching
        /// down on the same runway then is a collision. Returns the index (in <paramref name="rollingRunways"/>) of the
        /// aircraft it meets, or -1.
        /// </summary>
        public static int TouchdownConflict(int runway, IReadOnlyList<int> rollingRunways)
        {
            if (runway < 0)
                return -1;
            for (int i = 0; i < rollingRunways.Count; i++)
                if (rollingRunways[i] == runway)
                    return i;
            return -1;
        }
    }
}
