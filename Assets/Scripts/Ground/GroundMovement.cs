using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    public enum GroundPhase
    {
        /// <summary>A departure parked at its stand, waiting for Taxi.</summary>
        AtStand,
        /// <summary>Moving along a taxiway (it stops behind other aircraft).</summary>
        Taxiing,
        /// <summary>Stopped at a hold line before a runway it must cross, waiting for Cross.</summary>
        HoldingAtCrossing,
        /// <summary>Crossing a runway.</summary>
        Crossing,
        /// <summary>At the hold line of a runway end, waiting for Take off.</summary>
        HoldingShort,
        /// <summary>Moving from the hold line onto the runway.</summary>
        LiningUp,
        /// <summary>Accelerating along the runway.</summary>
        TakeOffRoll,
        /// <summary>Lifted off; it now flies out of the screen in the runway direction.</summary>
        Airborne,
        /// <summary>An arrival slowing down on the runway after touchdown.</summary>
        Rollout,
        /// <summary>An arrival leaving the runway at the next exit, at taxi speed.</summary>
        Vacating,
        /// <summary>An arrival clear of the runway at the exit's hold line, waiting for Taxi to stand.</summary>
        WaitingForStand,
        /// <summary>An arrival parked at its stand: done.</summary>
        Parked,
    }

    /// <summary>
    /// One aircraft on the ground in Tower mode (blueprint 16.1, phase 3). Departures: from the stand along a taxi route
    /// to the hold line of a runway end, stopping before each runway the route crosses until Cross; on Take off it lines
    /// up, rolls and lifts off in the runway direction. Arrivals: after touchdown it slows down along the runway, leaves it
    /// at the next exit and waits clear of the runway; on Taxi to stand it follows a taxi route to a free stand and parks.
    /// Routes come from the <see cref="TaxiNetwork"/>. Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public sealed class GroundMovement
    {
        /// <summary>Lift-off happens this far along the runway from the line-up point, as a fraction of the runway's length.</summary>
        public const float LiftOffFraction = 0.42f;

        readonly float taxiSpeed;
        readonly float airSpeed;

        readonly List<Vector2> leg = new List<Vector2>();
        readonly List<int> legNodes = new List<int>();
        readonly List<TaxiRoute.Stop> stops = new List<TaxiRoute.Stop>();
        int legNext;
        int stopIndex;
        GroundPhase phaseAtEnd;
        int crossingEndPoint = -1;

        Runway runway;
        Vector2 lineUpPoint;
        float rollDistance;
        float rollSpeed;
        float liftOffDistance;
        float acceleration;

        // Landing roll: from touchdown speed to exit speed over the roll distance, an optional stop (a slide on ice), then
        // along the runway to the exit and off it at exit speed.
        Vector2 touchdownPoint;
        float rollLength;
        float touchdownSpeed;
        float exitSpeed;
        float pauseLeft;

        GroundMovement(Vector2 position, float headingDegrees, float taxiSpeed, float airSpeed, GroundPhase phase)
        {
            Position = position;
            HeadingDegrees = headingDegrees;
            this.taxiSpeed = Mathf.Max(0.01f, taxiSpeed);
            this.airSpeed = Mathf.Max(0.01f, airSpeed);
            Phase = phase;
        }

        /// <summary>A departure parked at stand <paramref name="stand"/>, waiting for Taxi.</summary>
        public static GroundMovement AtStand(Vector2 position, float headingDegrees, int stand, int standNode, float taxiSpeed, float airSpeed) =>
            new GroundMovement(position, headingDegrees, taxiSpeed, airSpeed, GroundPhase.AtStand) { Stand = stand, CurrentNode = standNode };

        /// <summary>
        /// An arrival that has just touched down on <paramref name="landingRunway"/> (its landing direction) at
        /// <paramref name="touchdown"/>, at <paramref name="speed"/>. It slows to <paramref name="exitSpeed"/> over
        /// <paramref name="rollDistance"/>, stands still for <paramref name="stopSeconds"/> (an aircraft that slid on ice),
        /// then follows <paramref name="exit"/> at exit speed (from the end of the roll along the runway and off it to the
        /// exit's hold line, node <paramref name="exitNode"/>). The runway is busy until it is clear at the hold line.
        /// </summary>
        public static GroundMovement Landed(Vector2 touchdown, Runway landingRunway, int runwayIndex, float speed, float rollDistance,
                                            IReadOnlyList<Vector2> exit, int exitNode, float exitSpeed, float taxiSpeed, float stopSeconds = 0f)
        {
            var g = new GroundMovement(touchdown, landingRunway.LandingHeadingDegrees, taxiSpeed, speed, GroundPhase.Rollout)
            {
                runway = landingRunway,
                RunwayIndex = runwayIndex,
                touchdownPoint = touchdown,
                rollLength = Mathf.Max(0.05f, rollDistance),
                exitSpeed = Mathf.Clamp(exitSpeed, 0.05f, Mathf.Max(0.05f, speed)),
                touchdownSpeed = Mathf.Max(speed, 0.05f),
                pauseLeft = Mathf.Max(0f, stopSeconds),
                CurrentSpeed = speed,
            };
            g.leg.AddRange(exit);
            g.legNodes.Add(-1);
            for (int i = 1; i < exit.Count; i++)
                g.legNodes.Add(i == exit.Count - 1 ? exitNode : -1);
            return g;
        }

        /// <summary>Where the landing roll ends: <paramref name="rollDistance"/> along the runway from the touchdown point.</summary>
        public static Vector2 RollEnd(Vector2 touchdown, Runway landingRunway, float rollDistance) =>
            touchdown + landingRunway.Direction * rollDistance;

        public GroundPhase Phase { get; private set; }
        public Vector2 Position { get; private set; }
        public float HeadingDegrees { get; private set; }

        /// <summary>Current speed along the ground (0 when stopped).</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>The stand it is parked at, or heading for; -1 when none.</summary>
        public int Stand { get; set; } = -1;

        /// <summary>The network node it is stopped at, or -1 while moving between nodes.</summary>
        public int CurrentNode { get; private set; } = -1;

        /// <summary>The runway (index in the level) it lands on, takes off from, or is crossing; -1 when none.</summary>
        public int RunwayIndex { get; private set; } = -1;

        /// <summary>For a departure: the runway it was sent to with Taxi, and which end (true = end B). -1 when none.</summary>
        public int TargetRunway { get; set; } = -1;
        public bool TargetEndB { get; set; }

        /// <summary>The last node of the route it is following (its goal), or -1.</summary>
        public int GoalNode => legNodes.Count > 0 ? legNodes[legNodes.Count - 1] : -1;

        /// <summary>True when the aircraft waits for a command: Taxi, Cross, Take off or Taxi to stand.</summary>
        public bool IsWaitingForCommand =>
            Phase == GroundPhase.AtStand || Phase == GroundPhase.HoldingAtCrossing || Phase == GroundPhase.HoldingShort
            || Phase == GroundPhase.WaitingForStand;

        /// <summary>True while it is on a runway: rolling out, leaving it, crossing, lining up or taking off.</summary>
        public bool UsesRunway =>
            Phase == GroundPhase.Crossing || Phase == GroundPhase.LiningUp || Phase == GroundPhase.TakeOffRoll
            || Phase == GroundPhase.Rollout || Phase == GroundPhase.Vacating;

        /// <summary>True while it moves along a taxiway and should stop behind aircraft in front.</summary>
        public bool IsTaxiing => Phase == GroundPhase.Taxiing;

        public bool IsArrival => Phase == GroundPhase.Rollout || Phase == GroundPhase.Vacating || Phase == GroundPhase.WaitingForStand
                                 || Phase == GroundPhase.Parked || (Phase == GroundPhase.Taxiing && phaseAtEnd == GroundPhase.Parked);

        public Vector2 HeadingVector
        {
            get
            {
                float r = HeadingDegrees * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            }
        }

        /// <summary>
        /// Follows <paramref name="route"/> (it must start where the aircraft is) and ends in <paramref name="endPhase"/>:
        /// HoldingShort for a departure at a runway end, Parked for an arrival at its stand. Allowed when it waits at a
        /// stand, at a hold line or for a stand, or while taxiing (a new route from the node it is heading for).
        /// </summary>
        public bool FollowRoute(TaxiRoute route, GroundPhase endPhase)
        {
            bool canStart = Phase == GroundPhase.AtStand || Phase == GroundPhase.HoldingShort || Phase == GroundPhase.WaitingForStand
                            || Phase == GroundPhase.Taxiing || Phase == GroundPhase.HoldingAtCrossing;
            if (route == null || route.points.Count == 0 || !canStart)
                return false;
            leg.Clear();
            legNodes.Clear();
            stops.Clear();
            // While moving, the new route starts at the node ahead: keep going there first.
            if (Phase == GroundPhase.Taxiing && Vector2.Distance(Position, route.points[0]) > 1e-4f)
            {
                leg.Add(Position);
                legNodes.Add(-1);
                foreach (TaxiRoute.Stop stop in route.crossings)
                    stops.Add(stop.Shifted(1));
            }
            else
            {
                stops.AddRange(route.crossings);
            }
            leg.AddRange(route.points);
            legNodes.AddRange(route.nodes);
            legNext = 1;
            stopIndex = 0;
            phaseAtEnd = endPhase;
            CurrentNode = -1;
            RunwayIndex = -1;
            Phase = GroundPhase.Taxiing;
            if (leg.Count < 2 || ReachedStop())
            {
                ArriveIfDone();
                return true;
            }
            FaceNext();
            return true;
        }

        /// <summary>The node the aircraft is moving towards now (or stopped at), for a new route; -1 if unknown.</summary>
        public int NodeAhead
        {
            get
            {
                if (CurrentNode >= 0)
                    return CurrentNode;
                for (int i = legNext; i < legNodes.Count; i++)
                    if (legNodes[i] >= 0)
                        return legNodes[i];
                return -1;
            }
        }

        /// <summary>Cross: crosses the runway ahead from the hold line.</summary>
        public bool Cross()
        {
            if (Phase != GroundPhase.HoldingAtCrossing)
                return false;
            TaxiRoute.Stop stop = stops[stopIndex];
            stopIndex++;
            RunwayIndex = stop.Runway;
            // It is on the runway until the first point after the run of runway edges.
            crossingEndPoint = stop.ClearPointIndex;
            Phase = GroundPhase.Crossing;
            CurrentNode = -1;
            FaceNext();
            return true;
        }

        /// <summary>
        /// Take off from the hold line along <paramref name="takeOffRunway"/> (its landing direction is the take-off direction):
        /// it lines up at <paramref name="lineUp"/> on the centreline, rolls and lifts off after <paramref name="liftOff"/>.
        /// </summary>
        public bool TakeOff(Runway takeOffRunway, int runwayIndex, Vector2 lineUp, float liftOff)
        {
            if (Phase != GroundPhase.HoldingShort)
                return false;
            runway = takeOffRunway;
            RunwayIndex = runwayIndex;
            lineUpPoint = lineUp;
            liftOffDistance = Mathf.Max(0.3f, liftOff);
            // A rolling take-off from taxi speed, reaching flying speed exactly at the lift-off point.
            float startSpeed = Mathf.Min(taxiSpeed, airSpeed * 0.9f);
            acceleration = (airSpeed * airSpeed - startSpeed * startSpeed) / (2f * liftOffDistance);
            leg.Clear();
            legNodes.Clear();
            stops.Clear();
            leg.Add(Position);
            leg.Add(lineUpPoint);
            legNodes.Add(CurrentNode);
            legNodes.Add(-1);
            legNext = 1;
            CurrentNode = -1;
            Phase = GroundPhase.LiningUp;
            FaceNext();
            return true;
        }

        /// <summary>Where a departure lines up for <paramref name="takeOffRunway"/>: on the centreline level with its hold line, at least a little past the threshold.</summary>
        public static Vector2 LineUpPoint(Runway takeOffRunway, Vector2 holdLine)
        {
            float along = Vector2.Dot(holdLine - takeOffRunway.Threshold, takeOffRunway.Direction);
            along = Mathf.Clamp(along, 0.15f, takeOffRunway.Length - 0.5f);
            return takeOffRunway.Threshold + takeOffRunway.Direction * along;
        }

        /// <summary>Distance from the line-up point to lift-off: <see cref="LiftOffFraction"/> of the runway, within what is left.</summary>
        public static float LiftOffDistance(Runway takeOffRunway, Vector2 lineUp)
        {
            float remaining = takeOffRunway.Length - Vector2.Dot(lineUp - takeOffRunway.Threshold, takeOffRunway.Direction);
            return Mathf.Clamp(takeOffRunway.Length * LiftOffFraction, 0.3f, Mathf.Max(0.3f, remaining * 0.85f));
        }

        /// <summary>Advances by <paramref name="deltaTime"/> seconds. A taxiing aircraft does not move while <paramref name="blockedAhead"/>.</summary>
        public void Step(float deltaTime, bool blockedAhead)
        {
            if (deltaTime <= 0f)
                return;

            switch (Phase)
            {
                case GroundPhase.Taxiing:
                    CurrentSpeed = blockedAhead ? 0f : taxiSpeed;
                    if (!blockedAhead)
                        MoveAlongLeg(taxiSpeed * deltaTime);
                    break;
                case GroundPhase.Crossing:
                case GroundPhase.LiningUp:
                    CurrentSpeed = taxiSpeed;
                    MoveAlongLeg(taxiSpeed * deltaTime); // committed: it does not stop for queues on the runway
                    break;
                case GroundPhase.Vacating:
                    CurrentSpeed = exitSpeed;
                    MoveAlongLeg(exitSpeed * deltaTime);
                    break;
                case GroundPhase.Rollout:
                    StepRollout(deltaTime);
                    break;
                case GroundPhase.TakeOffRoll:
                    rollSpeed += acceleration * deltaTime;
                    rollDistance += rollSpeed * deltaTime;
                    CurrentSpeed = rollSpeed;
                    Position = lineUpPoint + runway.Direction * rollDistance;
                    if (rollDistance >= liftOffDistance)
                    {
                        Phase = GroundPhase.Airborne;
                        CurrentSpeed = airSpeed;
                    }
                    else if (rollSpeed > airSpeed)
                    {
                        rollSpeed = airSpeed;
                    }
                    break;
                default:
                    CurrentSpeed = 0f;
                    break;
            }
        }

        // Even deceleration from touchdown speed to exit speed over the roll length; then a stop if it slid on ice.
        void StepRollout(float deltaTime)
        {
            if (rollDistance >= rollLength)
            {
                // Stopped after a slide: it gathers itself before it can leave the runway.
                CurrentSpeed = 0f;
                pauseLeft -= deltaTime;
                if (pauseLeft <= 0f)
                    StartVacating(0f);
                return;
            }
            float decel = (touchdownSpeed * touchdownSpeed - exitSpeed * exitSpeed) / (2f * rollLength);
            CurrentSpeed = Mathf.Max(exitSpeed, CurrentSpeed - decel * deltaTime);
            rollDistance += CurrentSpeed * deltaTime;
            if (rollDistance >= rollLength)
            {
                float beyond = rollDistance - rollLength;
                rollDistance = rollLength;
                Position = touchdownPoint + runway.Direction * rollLength;
                if (pauseLeft > 0f)
                    CurrentSpeed = 0f;
                else
                    StartVacating(beyond);
                return;
            }
            Position = touchdownPoint + runway.Direction * rollDistance;
            HeadingDegrees = runway.LandingHeadingDegrees;
        }

        void StartVacating(float distance)
        {
            Phase = GroundPhase.Vacating;
            legNext = 1;
            if (leg.Count < 2)
            {
                Phase = GroundPhase.WaitingForStand;
                return;
            }
            FaceNext();
            CurrentSpeed = exitSpeed;
            MoveAlongLeg(distance);
        }

        /// <summary>How far the landing roll has gone, for tests.</summary>
        public float RolledDistance => rollDistance;

        bool ReachedStop() => stopIndex < stops.Count && stops[stopIndex].PointIndex == legNext - 1;

        void MoveAlongLeg(float distance)
        {
            while (distance > 0f && legNext < leg.Count)
            {
                Vector2 target = leg[legNext];
                float toTarget = Vector2.Distance(Position, target);
                if (toTarget > distance)
                {
                    Position = Vector2.MoveTowards(Position, target, distance);
                    return;
                }
                Position = target;
                distance -= toTarget;
                legNext++;
                if (Phase == GroundPhase.Crossing && legNext - 1 >= crossingEndPoint)
                {
                    // Off the runway on the far side: carry on taxiing (or stop at another runway's hold line).
                    Phase = GroundPhase.Taxiing;
                    RunwayIndex = -1;
                }
                if (Phase == GroundPhase.Taxiing && ReachedStop())
                {
                    Phase = GroundPhase.HoldingAtCrossing;
                    CurrentNode = legNodes[legNext - 1];
                    CurrentSpeed = 0f;
                    return;
                }
                FaceNext();
            }
            if (legNext >= leg.Count)
                ArriveIfDone();
        }

        void ArriveIfDone()
        {
            if (legNext < leg.Count)
                return;
            CurrentSpeed = 0f;
            CurrentNode = legNodes.Count > 0 ? legNodes[legNodes.Count - 1] : -1;
            switch (Phase)
            {
                case GroundPhase.LiningUp:
                    HeadingDegrees = runway.LandingHeadingDegrees;
                    rollDistance = 0f;
                    rollSpeed = Mathf.Min(taxiSpeed, airSpeed * 0.9f); // keeps rolling from taxi speed, no stop
                    Phase = GroundPhase.TakeOffRoll;
                    break;
                case GroundPhase.Vacating:
                    Phase = GroundPhase.WaitingForStand;
                    RunwayIndex = -1;
                    break;
                case GroundPhase.Taxiing:
                case GroundPhase.Crossing:
                    Phase = phaseAtEnd;
                    RunwayIndex = -1;
                    break;
            }
        }

        void FaceNext()
        {
            if (legNext < leg.Count)
            {
                Vector2 d = leg[legNext] - Position;
                if (d.sqrMagnitude > 1e-8f)
                    HeadingDegrees = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            }
        }
    }

    /// <summary>Queueing on taxiways (blueprint 15.4): taxiing aircraft never collide, they stop behind each other.</summary>
    public static class TaxiQueue
    {
        /// <summary>
        /// Tower mode's queue rule: a taxiing aircraft waits for <paramref name="other"/> when it is close in front of it,
        /// on the ground and not taking off or rolling out on a runway. Head-on traffic that is moving passes (taxiing
        /// aircraft never collide, and two that wait for each other would never move again); parked arrivals are ignored.
        /// </summary>
        public static bool Blocks(GroundMovement self, GroundMovement other, float spacing)
        {
            if (other == null || other == self)
                return false;
            switch (other.Phase)
            {
                case GroundPhase.Rollout:
                case GroundPhase.TakeOffRoll:
                case GroundPhase.Airborne:
                case GroundPhase.Parked:
                    return false;
            }
            if (other.CurrentSpeed > 0f && Vector2.Dot(other.HeadingVector, self.HeadingVector) < -0.5f)
                return false;
            return IsBlocked(self.Position, self.HeadingVector, new[] { other.Position }, spacing);
        }

        /// <summary>
        /// True if any other ground aircraft is within <paramref name="spacing"/> in front of this one (in a cone ahead),
        /// so it should wait.
        /// </summary>
        public static bool IsBlocked(Vector2 position, Vector2 heading, IReadOnlyList<Vector2> others, float spacing)
        {
            foreach (Vector2 other in others)
            {
                Vector2 offset = other - position;
                float distance = offset.magnitude;
                if (distance < 1e-4f || distance > spacing)
                    continue;
                if (Vector2.Dot(offset / distance, heading) > 0.5f)
                    return true;
            }
            return false;
        }
    }

    public enum RunwayUse
    {
        None,
        Landing,
        TakeOff,
        LineUp,
        Crossing,
    }

    public readonly struct RunwayOccupant
    {
        public RunwayOccupant(Vector2 position, float radius, RunwayUse use)
        {
            Position = position;
            Radius = radius;
            Use = use;
        }

        public Vector2 Position { get; }
        public float Radius { get; }
        public RunwayUse Use { get; }
    }

    /// <summary>
    /// The busy-runway rule (blueprint 15.4, 16.1): a runway is busy while an aircraft lands (touchdown, roll and leaving
    /// it), lines up, takes off or crosses it; where two runways cross, an aircraft in the intersection makes both busy.
    /// A landing or a take-off that meets another aircraft on a runway or at an intersection is a collision. Plain C#, so it
    /// can be unit tested.
    /// </summary>
    public static class RunwayRules
    {
        public static bool IsBusy(IReadOnlyList<RunwayOccupant> occupants)
        {
            foreach (RunwayOccupant o in occupants)
                if (o.Use != RunwayUse.None)
                    return true;
            return false;
        }

        /// <summary>
        /// Which runways are busy: those an occupant is using (<paramref name="usedRunway"/>, -1 for none) or standing on
        /// (its position on the runway's surface, so an intersection counts for both runways).
        /// </summary>
        public static void BusyRunways(IReadOnlyList<Runway> runways, IReadOnlyList<RunwayOccupant> occupants,
                                       IReadOnlyList<int> usedRunway, bool[] busy)
        {
            for (int r = 0; r < runways.Count && r < busy.Length; r++)
            {
                busy[r] = false;
                for (int i = 0; i < occupants.Count; i++)
                {
                    if (occupants[i].Use == RunwayUse.None)
                        continue;
                    if ((usedRunway != null && i < usedRunway.Count && usedRunway[i] == r) || runways[r].Contains(occupants[i].Position, 0.02f))
                    {
                        busy[r] = true;
                        break;
                    }
                }
            }
        }

        /// <summary>Pairs (by index) that meet on the runway where at least one is landing or taking off.</summary>
        public static void FindCollisions(IReadOnlyList<RunwayOccupant> occupants, List<Vector2Int> collisions)
        {
            collisions.Clear();
            for (int i = 0; i < occupants.Count; i++)
            {
                for (int j = i + 1; j < occupants.Count; j++)
                {
                    RunwayOccupant a = occupants[i];
                    RunwayOccupant b = occupants[j];
                    if (a.Use == RunwayUse.None || b.Use == RunwayUse.None)
                        continue;
                    bool fast = a.Use == RunwayUse.Landing || a.Use == RunwayUse.TakeOff
                             || b.Use == RunwayUse.Landing || b.Use == RunwayUse.TakeOff;
                    if (fast && Vector2.Distance(a.Position, b.Position) < a.Radius + b.Radius)
                        collisions.Add(new Vector2Int(i, j));
                }
            }
        }
    }
}
