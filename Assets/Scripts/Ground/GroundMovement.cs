using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    public enum GroundPhase
    {
        /// <summary>Parked at the gate, waiting for Taxi.</summary>
        AtGate,
        /// <summary>Moving along a taxiway (it stops behind other aircraft).</summary>
        Taxiing,
        /// <summary>Stopped at a crossing point before the runway, waiting for Cross.</summary>
        HoldingAtCrossing,
        /// <summary>Crossing the runway.</summary>
        Crossing,
        /// <summary>At the hold line of the active runway end, waiting for Take off.</summary>
        HoldingShort,
        /// <summary>Moving from the hold line onto the runway.</summary>
        LiningUp,
        /// <summary>Accelerating along the runway.</summary>
        TakeOffRoll,
        /// <summary>Lifted off; it now flies out of the screen in the runway direction.</summary>
        Airborne,
    }

    /// <summary>
    /// A departing aircraft on the ground (blueprint 15.4): from its gate along a fixed taxi route to the hold line of
    /// the runway end that is active for the current wind, stopping at a crossing point if its route crosses the runway;
    /// then, on Take off, it lines up, rolls and lifts off in the runway direction. If the wind reverses while it is on
    /// the main taxiway or waiting at the hold line, it taxis to the other end by itself.
    /// Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public sealed class GroundMovement
    {
        /// <summary>Lift-off happens this far along the runway from the line-up point (a fraction of what is left).</summary>
        public const float LiftOffFraction = 0.7f;
        /// <summary>The line-up point is this far past the threshold, on the centreline.</summary>
        public const float LineUpInset = 0.15f;

        readonly List<Vector2> connector;
        readonly int crossingIndex;
        readonly List<Vector2> main;
        readonly float taxiSpeed;
        readonly float airSpeed;

        readonly List<Vector2> leg = new List<Vector2>();
        int legNext;
        LegKind legKind;
        bool towardReversedEnd;

        Runway takeOffRunway;
        Vector2 lineUpPoint;
        float rollDistance;
        float rollSpeed;
        float liftOffDistance;
        float acceleration;

        enum LegKind { Connector, Crossing, AfterCrossing, Main, LineUp }

        public GroundMovement(IReadOnlyList<Vector2> gateConnector, int gateCrossingIndex, IReadOnlyList<Vector2> mainTaxiway,
                              float taxiSpeed, float airSpeed, float gateHeadingDegrees, bool runwayReversed)
        {
            connector = new List<Vector2>(gateConnector);
            crossingIndex = gateCrossingIndex >= 0 && gateCrossingIndex < connector.Count - 1 ? gateCrossingIndex : -1;
            main = new List<Vector2>(mainTaxiway);
            this.taxiSpeed = Mathf.Max(0.01f, taxiSpeed);
            this.airSpeed = Mathf.Max(0.01f, airSpeed);
            towardReversedEnd = runwayReversed;
            Position = connector.Count > 0 ? connector[0] : Vector2.zero;
            HeadingDegrees = gateHeadingDegrees;
            Phase = GroundPhase.AtGate;
        }

        public GroundPhase Phase { get; private set; }
        public Vector2 Position { get; private set; }
        public float HeadingDegrees { get; private set; }

        /// <summary>Current speed along the ground (0 when stopped).</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>True when the aircraft waits for a command: Taxi, Cross or Take off.</summary>
        public bool IsWaitingForCommand =>
            Phase == GroundPhase.AtGate || Phase == GroundPhase.HoldingAtCrossing || Phase == GroundPhase.HoldingShort;

        /// <summary>True while it is on (or entering) the runway: crossing, lining up or taking off.</summary>
        public bool UsesRunway => Phase == GroundPhase.Crossing || Phase == GroundPhase.LiningUp || Phase == GroundPhase.TakeOffRoll;

        /// <summary>True while it moves along a taxiway and should stop behind aircraft in front.</summary>
        public bool IsTaxiing => Phase == GroundPhase.Taxiing;

        /// <summary>Which runway end it is heading for: false for the normal landing direction, true for the reversed one.</summary>
        public bool TowardReversedEnd => towardReversedEnd;

        public Vector2 HeadingVector
        {
            get
            {
                float r = HeadingDegrees * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            }
        }

        /// <summary>Taxi: leave the gate.</summary>
        public bool Taxi()
        {
            if (Phase != GroundPhase.AtGate)
                return false;
            int end = crossingIndex >= 0 ? crossingIndex : connector.Count - 1;
            StartLeg(LegKind.Connector, connector.GetRange(0, end + 1));
            return true;
        }

        /// <summary>Cross: cross the runway from the crossing point.</summary>
        public bool Cross()
        {
            if (Phase != GroundPhase.HoldingAtCrossing)
                return false;
            StartLeg(LegKind.Crossing, connector.GetRange(crossingIndex, 2));
            Phase = GroundPhase.Crossing;
            return true;
        }

        /// <summary>Take off from the hold line onto <paramref name="runway"/> (the active runway, landing direction = take-off direction).</summary>
        public bool TakeOff(Runway runway)
        {
            if (Phase != GroundPhase.HoldingShort)
                return false;
            takeOffRunway = runway;
            lineUpPoint = runway.Threshold + runway.Direction * LineUpInset;
            liftOffDistance = Mathf.Max(0.3f, (runway.Length - LineUpInset) * LiftOffFraction);
            // A rolling take-off from taxi speed, reaching flying speed exactly at the lift-off point.
            float startSpeed = Mathf.Min(taxiSpeed, airSpeed * 0.9f);
            acceleration = (airSpeed * airSpeed - startSpeed * startSpeed) / (2f * liftOffDistance);
            StartLeg(LegKind.LineUp, new List<Vector2> { Position, lineUpPoint });
            Phase = GroundPhase.LiningUp;
            return true;
        }

        /// <summary>
        /// The wind sets which runway end is active. An aircraft on the main taxiway, or waiting at a hold line, that is
        /// heading for the other end turns round and taxis there by itself (blueprint 15.4).
        /// </summary>
        public void SetActiveEnd(bool runwayReversed)
        {
            if (runwayReversed == towardReversedEnd)
                return;
            towardReversedEnd = runwayReversed;
            bool onMain = (Phase == GroundPhase.Taxiing && legKind == LegKind.Main) || Phase == GroundPhase.HoldingShort;
            if (onMain)
                StartMainLeg();
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
                case GroundPhase.TakeOffRoll:
                    rollSpeed += acceleration * deltaTime;
                    rollDistance += rollSpeed * deltaTime;
                    CurrentSpeed = rollSpeed;
                    Position = lineUpPoint + takeOffRunway.Direction * rollDistance;
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

        void StartLeg(LegKind kind, List<Vector2> points)
        {
            legKind = kind;
            leg.Clear();
            leg.AddRange(points);
            legNext = 1;
            if (Phase != GroundPhase.Crossing && Phase != GroundPhase.LiningUp)
                Phase = GroundPhase.Taxiing;
            FaceNext();
        }

        // From where it is now along the main taxiway to the end it is heading for.
        void StartMainLeg()
        {
            float here = ParamOnMain(Position);
            var points = new List<Vector2> { Position };
            float total = MainLength();
            if (towardReversedEnd)
            {
                float travelled = 0f;
                for (int i = 1; i < main.Count; i++)
                {
                    travelled += Vector2.Distance(main[i - 1], main[i]);
                    if (travelled > here + 1e-4f)
                        points.Add(main[i]);
                }
            }
            else
            {
                float travelled = total;
                for (int i = main.Count - 2; i >= 0; i--)
                {
                    travelled -= Vector2.Distance(main[i], main[i + 1]);
                    if (travelled < here - 1e-4f)
                        points.Add(main[i]);
                }
            }
            if (points.Count == 1)
                points.Add(Position); // already at the end
            Phase = GroundPhase.Taxiing;
            StartLeg(LegKind.Main, points);
        }

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
                FaceNext();
            }
            if (legNext >= leg.Count)
                FinishLeg();
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

        void FinishLeg()
        {
            switch (legKind)
            {
                case LegKind.Connector:
                    if (crossingIndex >= 0)
                        Phase = GroundPhase.HoldingAtCrossing;
                    else
                        StartMainLeg();
                    break;
                case LegKind.Crossing:
                    Phase = GroundPhase.Taxiing;
                    if (crossingIndex + 1 < connector.Count - 1)
                        StartLeg(LegKind.AfterCrossing, connector.GetRange(crossingIndex + 1, connector.Count - crossingIndex - 1));
                    else
                        StartMainLeg();
                    break;
                case LegKind.AfterCrossing:
                    StartMainLeg();
                    break;
                case LegKind.Main:
                    Phase = GroundPhase.HoldingShort;
                    break;
                case LegKind.LineUp:
                    HeadingDegrees = takeOffRunway.LandingHeadingDegrees;
                    rollDistance = 0f;
                    rollSpeed = Mathf.Min(taxiSpeed, airSpeed * 0.9f); // keeps rolling from taxi speed, no stop
                    Phase = GroundPhase.TakeOffRoll;
                    break;
            }
        }

        float MainLength()
        {
            float total = 0f;
            for (int i = 1; i < main.Count; i++)
                total += Vector2.Distance(main[i - 1], main[i]);
            return total;
        }

        /// <summary>Distance along the main taxiway, from its first point, of the closest point to <paramref name="p"/>.</summary>
        float ParamOnMain(Vector2 p)
        {
            float best = float.MaxValue;
            float bestParam = 0f;
            float travelled = 0f;
            for (int i = 1; i < main.Count; i++)
            {
                Vector2 a = main[i - 1];
                Vector2 b = main[i];
                Vector2 ab = b - a;
                float length = ab.magnitude;
                float t = length > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / (length * length)) : 0f;
                float d = Vector2.Distance(p, a + ab * t);
                if (d < best)
                {
                    best = d;
                    bestParam = travelled + t * length;
                }
                travelled += length;
            }
            return bestParam;
        }
    }

    /// <summary>Queueing on taxiways (blueprint 15.4): taxiing aircraft never collide, they stop behind each other.</summary>
    public static class TaxiQueue
    {
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
    /// The busy-runway rule (blueprint 15.4): the runway is busy while an aircraft lands (its roll), lines up, takes
    /// off or crosses. A landing or a take-off that meets another aircraft on the runway is a collision. Plain C#, so
    /// it can be unit tested.
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

    public enum AircraftCommand
    {
        Slow,
        Fast,
        Taxi,
        Cross,
        TakeOff,
    }

    /// <summary>Which option buttons a tapped aircraft shows (blueprint 15.4). Plain C#, so it can be unit tested.</summary>
    public static class AircraftCommands
    {
        static readonly AircraftCommand[] Air = { AircraftCommand.Slow, AircraftCommand.Fast };
        static readonly AircraftCommand[] Gate = { AircraftCommand.Taxi };
        static readonly AircraftCommand[] Crossing = { AircraftCommand.Cross };
        static readonly AircraftCommand[] HoldLine = { AircraftCommand.TakeOff };
        static readonly AircraftCommand[] None = new AircraftCommand[0];

        /// <param name="flying">In the air and under the player's control (not landing, not departing).</param>
        /// <param name="ground">Its ground phase, or null when it is not on the ground.</param>
        public static AircraftCommand[] For(bool flying, GroundPhase? ground)
        {
            if (ground.HasValue)
            {
                switch (ground.Value)
                {
                    case GroundPhase.AtGate: return Gate;
                    case GroundPhase.HoldingAtCrossing: return Crossing;
                    case GroundPhase.HoldingShort: return HoldLine;
                    default: return None;
                }
            }
            return flying ? Air : None;
        }

        public static string Label(AircraftCommand command)
        {
            switch (command)
            {
                case AircraftCommand.Slow: return "Slow";
                case AircraftCommand.Fast: return "Fast";
                case AircraftCommand.Taxi: return "Taxi";
                case AircraftCommand.Cross: return "Cross";
                case AircraftCommand.TakeOff: return "Take off";
                default: return command.ToString();
            }
        }
    }
}
