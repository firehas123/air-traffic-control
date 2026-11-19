using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    /// <summary>Hybrid controls (M3 step 3, blueprint 15.4).</summary>
    public class GestureTests
    {
        [Test]
        public void QuickStillPress_IsATap()
        {
            var g = new GestureClassifier();
            g.Press(Vector2.zero, 0f);
            Assert.IsFalse(g.Hold(new Vector2(0.05f, 0f), 0.1f));
            Assert.AreEqual(GestureEnd.Tap, g.Release(new Vector2(0.1f, 0f), 0.2f));
        }

        [Test]
        public void JustInsideTheLimits_IsStillATap()
        {
            var g = new GestureClassifier();
            g.Press(Vector2.zero, 0f);
            Assert.AreEqual(GestureEnd.Tap, g.Release(new Vector2(0.14f, 0f), 0.24f));
        }

        [Test]
        public void MovingFar_BecomesADragAtThatMoment()
        {
            var g = new GestureClassifier();
            g.Press(Vector2.zero, 0f);
            Assert.IsFalse(g.Hold(new Vector2(0.1f, 0f), 0.05f));
            Assert.IsTrue(g.Hold(new Vector2(0.16f, 0f), 0.08f), "Became a drag");
            Assert.IsFalse(g.Hold(new Vector2(0.5f, 0f), 0.1f), "Only once");
            Assert.AreEqual(GestureEnd.DragEnded, g.Release(new Vector2(0.5f, 0f), 0.12f));
        }

        [Test]
        public void HoldingStill_BecomesADragAfterAQuarterSecond()
        {
            var g = new GestureClassifier();
            g.Press(Vector2.zero, 0f);
            Assert.IsFalse(g.Hold(Vector2.zero, 0.2f));
            Assert.IsTrue(g.Hold(Vector2.zero, 0.26f));
        }

        [Test]
        public void SlowSmallPress_IsNotATap()
        {
            var g = new GestureClassifier();
            g.Press(Vector2.zero, 0f);
            Assert.AreEqual(GestureEnd.DragEnded, g.Release(new Vector2(0.05f, 0f), 0.4f));
        }

        [Test]
        public void ReleaseWithoutPress_IsNothing()
        {
            Assert.AreEqual(GestureEnd.None, new GestureClassifier().Release(Vector2.zero, 1f));
        }
    }

    public class SpeedControlTests
    {
        static AircraftType Kind(AircraftSilhouette silhouette)
        {
            var type = ScriptableObject.CreateInstance<AircraftType>();
            type.SetDefaults(silhouette);
            return type;
        }

        [Test]
        public void SlowAndFast_AreTheAgreedFactors_PerKind()
        {
            AircraftType airliner = Kind(AircraftSilhouette.Airliner);
            Assert.AreEqual(0.6f, SpeedRules.Factor(airliner, AircraftSpeed.Slow), 1e-5f);
            Assert.AreEqual(1.4f, SpeedRules.Factor(airliner, AircraftSpeed.Fast), 1e-5f);
            Assert.AreEqual(1f, SpeedRules.Factor(airliner, AircraftSpeed.Normal), 1e-5f);

            airliner.fastFactor = 1.25f; // data per kind
            Assert.AreEqual(1.25f, SpeedRules.Factor(airliner, AircraftSpeed.Fast), 1e-5f);
            Object.DestroyImmediate(airliner);
        }

        [Test]
        public void TappingTheActiveButtonAgain_ReturnsToNormal()
        {
            Assert.AreEqual(AircraftSpeed.Slow, SpeedRules.Toggle(AircraftSpeed.Normal, AircraftSpeed.Slow));
            Assert.AreEqual(AircraftSpeed.Normal, SpeedRules.Toggle(AircraftSpeed.Slow, AircraftSpeed.Slow));
            Assert.AreEqual(AircraftSpeed.Fast, SpeedRules.Toggle(AircraftSpeed.Slow, AircraftSpeed.Fast));
            Assert.AreEqual(AircraftSpeed.Normal, SpeedRules.Toggle(AircraftSpeed.Fast, AircraftSpeed.Fast));
        }

        [Test]
        public void ChangingSpeed_KeepsTheDrawnLineExact()
        {
            var motion = new AircraftMotion(Vector2.zero, 0f, 0.6f, 200f);
            var drawn = new List<Vector2> { Vector2.zero };
            motion.BeginPath();
            for (int i = 1; i <= 60; i++)
            {
                var p = new Vector2(i * 0.05f, Mathf.Sin(i * 0.2f) * 0.4f);
                drawn.Add(p);
                motion.AppendWaypoint(p);
            }

            var bounds = new Rect(-100f, -100f, 200f, 200f);
            float[] speeds = { 0.6f, 0.36f, 0.84f, 0.6f };
            int step = 0;
            while (motion.HasPath && step < 2000)
            {
                motion.SetSpeed(speeds[(step / 40) % speeds.Length]);
                float before = motion.DistanceAlongPath;
                motion.Step(1f / 60f, bounds);
                if (motion.HasPath)
                    Assert.AreEqual(motion.Speed / 60f, motion.DistanceAlongPath - before, 1e-4f, "Moves at the current speed");
                PathIntegrity.CheckSameLine(drawn, motion.Path); // the line never changes
                step++;
            }
            Assert.IsFalse(motion.HasPath, "Reaches the end of the line");
        }

        [Test]
        public void TimeToTouchdown_FollowsTheCurrentSpeed()
        {
            var motion = new AircraftMotion(Vector2.zero, 0f, 0.6f, 200f);
            motion.BeginPath();
            motion.AppendWaypoint(new Vector2(3f, 0f));
            Assert.AreEqual(5f, motion.SecondsToEndOfPath, 1e-4f);
            motion.SetSpeed(0.6f * 1.4f);
            Assert.AreEqual(3f / 0.84f, motion.SecondsToEndOfPath, 1e-3f, "Fast lands sooner");
        }

        [Test]
        public void Warnings_UseTheCurrentSpeed()
        {
            // Head-on, 3 units apart: at slow speed they would not meet within the 2 s look-ahead; at fast speed they would.
            var a = new AircraftMotion(Vector2.zero, 0f, 0.6f, 200f);
            var b = new AircraftMotion(new Vector2(3.6f, 0f), 180f, 0.6f, 200f);

            a.SetSpeed(0.36f);
            b.SetSpeed(0.36f);
            Assert.AreEqual(SeparationState.Clear, SeparationRules.Classify(a.Position, a.Velocity, 0.25f, b.Position, b.Velocity, 0.25f));

            a.SetSpeed(0.84f);
            b.SetSpeed(0.84f);
            Assert.AreEqual(SeparationState.Warning, SeparationRules.Classify(a.Position, a.Velocity, 0.25f, b.Position, b.Velocity, 0.25f));
        }
    }

    public class GroundMovementTests
    {
        // The test level layout: runway centre (1, -0.8), length 3.2, width 0.6; main taxiway along y = -0.15.
        static readonly Runway Normal = new Runway(new Vector2(1f, -0.8f), 0f, 3.2f, 0.6f, 0.1f, 0.7f);
        static readonly List<Vector2> Main = new List<Vector2> { new Vector2(-0.6f, -0.15f), new Vector2(2.6f, -0.15f) };
        static readonly List<Vector2> GateA = new List<Vector2> { new Vector2(0.4f, 0.45f), new Vector2(0.4f, -0.15f) };
        static readonly List<Vector2> GateB = new List<Vector2> { new Vector2(1.6f, -1.75f), new Vector2(1.6f, -1.32f), new Vector2(1.6f, -0.15f) };
        const float Taxi = 0.35f;
        const float Air = 0.6f;
        const float Dt = 1f / 60f;

        static GroundMovement FromGateA(bool reversed = false) => new GroundMovement(GateA, -1, Main, Taxi, Air, 270f, reversed);
        static GroundMovement FromGateB(bool reversed = false) => new GroundMovement(GateB, 1, Main, Taxi, Air, 90f, reversed);

        static void Run(GroundMovement g, float seconds, bool blocked = false)
        {
            for (float t = 0f; t < seconds; t += Dt)
                g.Step(Dt, blocked);
        }

        [Test]
        public void WaitsAtTheGate_UntilTaxi()
        {
            GroundMovement g = FromGateA();
            Run(g, 5f);
            Assert.AreEqual(GroundPhase.AtGate, g.Phase);
            Assert.IsTrue(g.IsWaitingForCommand);
            Assert.IsFalse(g.Cross());
            Assert.IsFalse(g.TakeOff(Normal), "Commands that do not apply are refused");
        }

        [Test]
        public void Taxi_GoesToTheHoldLineOfTheActiveEnd()
        {
            GroundMovement g = FromGateA();
            Assert.IsTrue(g.Taxi());
            Run(g, 20f);
            Assert.AreEqual(GroundPhase.HoldingShort, g.Phase);
            Assert.AreEqual(Main[0].x, g.Position.x, 1e-3f, "Normal landing direction: the first end of the main taxiway");
            Assert.AreEqual(Main[0].y, g.Position.y, 1e-3f);
        }

        [Test]
        public void Taxi_FollowsTheFixedRoute()
        {
            GroundMovement g = FromGateA();
            g.Taxi();
            for (float t = 0f; t < 20f; t += Dt)
            {
                g.Step(Dt, false);
                Vector2 p = g.Position;
                bool onConnector = Mathf.Abs(p.x - 0.4f) < 1e-3f && p.y <= 0.45f + 1e-3f && p.y >= -0.15f - 1e-3f;
                bool onMain = Mathf.Abs(p.y + 0.15f) < 1e-3f;
                Assert.IsTrue(onConnector || onMain, $"Off the taxi route at {p}");
            }
        }

        [Test]
        public void ReversedWind_TaxisToTheOtherEnd()
        {
            GroundMovement g = FromGateA(reversed: true);
            g.Taxi();
            Run(g, 20f);
            Assert.AreEqual(GroundPhase.HoldingShort, g.Phase);
            Assert.AreEqual(Main[1].x, g.Position.x, 1e-3f);
        }

        [Test]
        public void WindReversesWhileWaitingAtTheHoldLine_ItReRoutesByItself()
        {
            GroundMovement g = FromGateA();
            g.Taxi();
            Run(g, 20f);
            Assert.AreEqual(Main[0].x, g.Position.x, 1e-3f);

            g.SetActiveEnd(true);
            Assert.AreEqual(GroundPhase.Taxiing, g.Phase, "Sets off for the other end");
            Run(g, 20f);
            Assert.AreEqual(GroundPhase.HoldingShort, g.Phase);
            Assert.AreEqual(Main[1].x, g.Position.x, 1e-3f);
        }

        [Test]
        public void WindReversesWhileTaxiingOnTheMainTaxiway_ItTurnsRound()
        {
            GroundMovement g = FromGateA();
            g.Taxi();
            Run(g, 2.5f); // down the connector and onto the main taxiway, heading west
            Assert.AreEqual(GroundPhase.Taxiing, g.Phase);
            float x = g.Position.x;
            g.SetActiveEnd(true);
            Run(g, 1f);
            Assert.Greater(g.Position.x, x, "Now heading east");
        }

        [Test]
        public void RouteThatCrossesTheRunway_StopsAtTheCrossingPoint_UntilCross()
        {
            GroundMovement g = FromGateB();
            g.Taxi();
            Run(g, 10f);
            Assert.AreEqual(GroundPhase.HoldingAtCrossing, g.Phase);
            Assert.AreEqual(GateB[1].y, g.Position.y, 1e-3f);
            Assert.IsFalse(Normal.Contains(g.Position), "Waits off the runway");

            Assert.IsTrue(g.Cross());
            g.Step(Dt, false);
            Assert.AreEqual(GroundPhase.Crossing, g.Phase);
            Assert.IsTrue(g.UsesRunway);

            Run(g, 4f, blocked: true); // crossing is committed: it does not stop for queues on the runway
            Assert.AreNotEqual(GroundPhase.Crossing, g.Phase, "Off the runway again");
            Run(g, 20f);
            Assert.AreEqual(GroundPhase.HoldingShort, g.Phase);
        }

        [Test]
        public void TakeOff_LinesUpRollsAndLiftsOffInTheRunwayDirection()
        {
            GroundMovement g = FromGateA();
            g.Taxi();
            Run(g, 20f);
            Assert.IsTrue(g.TakeOff(Normal));
            Assert.AreEqual(GroundPhase.LiningUp, g.Phase);
            Assert.IsTrue(g.UsesRunway);

            float seconds = 0f;
            float lastSpeed = 0f;
            while (g.Phase != GroundPhase.Airborne && seconds < 30f)
            {
                g.Step(Dt, false);
                if (g.Phase == GroundPhase.TakeOffRoll)
                {
                    Assert.AreEqual(0f, g.HeadingDegrees, 1e-3f, "Rolls along the runway direction");
                    Assert.GreaterOrEqual(g.CurrentSpeed, lastSpeed, "Accelerates");
                    Assert.IsTrue(Normal.Contains(g.Position, 0.01f), "Stays on the runway");
                    lastSpeed = g.CurrentSpeed;
                }
                seconds += Dt;
            }
            Assert.AreEqual(GroundPhase.Airborne, g.Phase);
            Assert.AreEqual(Air, g.CurrentSpeed, 1e-4f, "Flying speed at lift-off");
            Assert.Greater(lastSpeed, 0.5f, "Speeding up towards flying speed on the roll");
            Assert.Greater(g.Position.x, Normal.Centre.x, "Lifted off well down the runway");
        }

        [Test]
        public void Queue_StopsBehindTheAircraftInFront()
        {
            var me = new Vector2(0f, 0f);
            var east = new Vector2(1f, 0f);
            Assert.IsTrue(TaxiQueue.IsBlocked(me, east, new List<Vector2> { new Vector2(0.6f, 0.05f) }, 0.75f));
            Assert.IsFalse(TaxiQueue.IsBlocked(me, east, new List<Vector2> { new Vector2(-0.6f, 0f) }, 0.75f), "Behind does not block");
            Assert.IsFalse(TaxiQueue.IsBlocked(me, east, new List<Vector2> { new Vector2(0.9f, 0f) }, 0.75f), "Far enough");
            Assert.IsFalse(TaxiQueue.IsBlocked(me, east, new List<Vector2> { new Vector2(0f, 0.5f) }, 0.75f), "Beside does not block");
        }

        [Test]
        public void TwoDepartures_QueueAtTheHoldLine_WithoutTouching()
        {
            GroundMovement first = FromGateA();
            GroundMovement second = FromGateA();
            first.Taxi();
            Run(first, 2.6f); // the first is a safe distance ahead before the second leaves
            second.Taxi();

            float closest = float.MaxValue;
            for (float t = 0f; t < 30f; t += Dt)
            {
                first.Step(Dt, TaxiQueue.IsBlocked(first.Position, first.HeadingVector, new List<Vector2> { second.Position }, 0.75f));
                second.Step(Dt, TaxiQueue.IsBlocked(second.Position, second.HeadingVector, new List<Vector2> { first.Position }, 0.75f));
                closest = Mathf.Min(closest, Vector2.Distance(first.Position, second.Position));
            }

            Assert.AreEqual(GroundPhase.HoldingShort, first.Phase);
            Assert.AreEqual(GroundPhase.Taxiing, second.Phase, "Waits behind, still taxiing");
            Assert.AreEqual(0f, second.CurrentSpeed, "Stopped in the queue");
            Assert.GreaterOrEqual(closest, 0.5f, "Taxiing aircraft never touch (two airliners touch at 0.5)");
        }
    }

    public class BusyRunwayTests
    {
        const float R = 0.25f;

        static List<RunwayOccupant> On(params RunwayOccupant[] occupants) => new List<RunwayOccupant>(occupants);

        [Test]
        public void Busy_WhileAnythingUsesTheRunway()
        {
            Assert.IsFalse(RunwayRules.IsBusy(On()));
            Assert.IsTrue(RunwayRules.IsBusy(On(new RunwayOccupant(Vector2.zero, R, RunwayUse.Crossing))));
            Assert.IsTrue(RunwayRules.IsBusy(On(new RunwayOccupant(Vector2.zero, R, RunwayUse.Landing))));
            Assert.IsTrue(RunwayRules.IsBusy(On(new RunwayOccupant(Vector2.zero, R, RunwayUse.TakeOff))));
        }

        [Test]
        public void LandingOntoACrossingAircraft_IsACollision()
        {
            var collisions = new List<Vector2Int>();
            RunwayRules.FindCollisions(On(new RunwayOccupant(new Vector2(0f, 0f), R, RunwayUse.Landing),
                                          new RunwayOccupant(new Vector2(0.3f, 0.05f), R, RunwayUse.Crossing)), collisions);
            CollectionAssert.AreEqual(new[] { new Vector2Int(0, 1) }, collisions);
        }

        [Test]
        public void TakeOffIntoALandingRoll_IsACollision()
        {
            var collisions = new List<Vector2Int>();
            RunwayRules.FindCollisions(On(new RunwayOccupant(new Vector2(1f, 0f), R, RunwayUse.Landing),
                                          new RunwayOccupant(new Vector2(0.6f, 0f), R, RunwayUse.TakeOff)), collisions);
            Assert.AreEqual(1, collisions.Count);
        }

        [Test]
        public void OnTheRunwayButApart_IsNotACollision()
        {
            var collisions = new List<Vector2Int>();
            RunwayRules.FindCollisions(On(new RunwayOccupant(new Vector2(-1f, 0f), R, RunwayUse.Landing),
                                          new RunwayOccupant(new Vector2(1f, 0f), R, RunwayUse.Crossing)), collisions);
            Assert.IsEmpty(collisions, "Busy, but they have not met");
        }

        [Test]
        public void SlowRunwayUsersTouching_AreNotACollision()
        {
            // Crossing and lining up are taxi speeds: they do not count as a landing or take-off meeting someone.
            var collisions = new List<Vector2Int>();
            RunwayRules.FindCollisions(On(new RunwayOccupant(Vector2.zero, R, RunwayUse.Crossing),
                                          new RunwayOccupant(new Vector2(0.2f, 0f), R, RunwayUse.LineUp)), collisions);
            Assert.IsEmpty(collisions);
        }

        /// <summary>
        /// Runs a real take-off from the hold line until it rolls past <paramref name="rollPastX"/>, then a fast landing
        /// touches down <paramref name="landingAhead"/> in front of it. Returns whether they collide.
        /// </summary>
        static bool LandingNearARollingTakeOff(float rollPastX, float landingAhead)
        {
            var runway = new Runway(new Vector2(1f, -0.8f), 0f, 3.2f, 0.6f, 0.1f, 0.7f);
            var main = new List<Vector2> { new Vector2(-0.6f, -0.15f), new Vector2(2.6f, -0.15f) };
            var gate = new List<Vector2> { new Vector2(-0.6f, 0.3f), new Vector2(-0.6f, -0.15f) };
            var departure = new GroundMovement(gate, -1, main, 0.35f, 0.6f, 270f, false);
            departure.Taxi();
            for (float t = 0f; t < 5f; t += 1f / 60f) departure.Step(1f / 60f, false);
            departure.TakeOff(runway);
            while (departure.Phase != GroundPhase.TakeOffRoll || departure.Position.x < rollPastX)
                departure.Step(1f / 60f, false);

            Vector2 touchdown = departure.Position + Vector2.right * landingAhead;
            const float landingSpeed = 0.84f; // a fast landing
            const float rollSeconds = 1.4f;
            var collisions = new List<Vector2Int>();
            for (float t = 0f; t < rollSeconds; t += 1f / 60f)
            {
                departure.Step(1f / 60f, false);
                float u = t / rollSeconds;
                var occupants = new List<RunwayOccupant>
                {
                    new RunwayOccupant(departure.Position, R, RunwayUse.TakeOff),
                    new RunwayOccupant(touchdown + Vector2.right * landingSpeed * rollSeconds * (u - u * u / 2f), R, RunwayUse.Landing),
                };
                RunwayRules.FindCollisions(occupants, collisions);
                if (collisions.Count > 0)
                    return true;
            }
            return false;
        }

        [Test]
        public void LandingOntoARollingTakeOff_IsACollision()
        {
            Assert.IsTrue(LandingNearARollingTakeOff(-0.3f, 0.4f), "Touches down just in front of the take-off roll");
        }

        [Test]
        public void LandingWellAheadOfARollingTakeOff_IsBusyButSafe()
        {
            Assert.IsFalse(LandingNearARollingTakeOff(-0.3f, 0.9f), "The landing rolls away faster than the take-off catches up");
        }

        [Test]
        public void TakingOffAfterTheLandingHasGone_IsSafe()
        {
            var collisions = new List<Vector2Int>();
            RunwayRules.FindCollisions(On(new RunwayOccupant(new Vector2(-0.45f, -0.8f), R, RunwayUse.TakeOff)), collisions);
            Assert.IsEmpty(collisions);
        }
    }

    public class DepartureGoalTests
    {
        [Test]
        public void LevelNeedsBothLandingsAndDepartures()
        {
            var session = new LevelSession(6, 3);
            for (int i = 0; i < 6; i++)
                Assert.IsFalse(session.RecordLanding());
            Assert.IsFalse(session.RecordDeparture());
            Assert.IsFalse(session.RecordDeparture());
            Assert.IsTrue(session.RecordDeparture(), "The third departure completes land 6, launch 3");
            Assert.AreEqual(SessionState.Cleared, session.State);
            Assert.AreEqual(3, session.DepartureCount);
        }

        [Test]
        public void DeparturesFirst_ThenLandings_AlsoClears()
        {
            var session = new LevelSession(2, 1);
            Assert.IsFalse(session.RecordDeparture());
            Assert.IsFalse(session.RecordLanding());
            Assert.IsTrue(session.RecordLanding());
        }

        [Test]
        public void LevelWithoutDepartures_ClearsOnLandingsAlone()
        {
            var session = new LevelSession(2, 0);
            session.RecordLanding();
            Assert.IsTrue(session.RecordLanding());
        }

        [Test]
        public void Restart_ResetsDepartures()
        {
            var session = new LevelSession(6, 3);
            session.RecordDeparture();
            session.Restart();
            Assert.AreEqual(0, session.DepartureCount);
            Assert.AreEqual(3, session.DeparturesToClear);
        }

        [Test]
        public void Commands_DependOnWhereTheAircraftIs()
        {
            CollectionAssert.AreEqual(new[] { AircraftCommand.Slow, AircraftCommand.Fast }, AircraftCommands.For(true, null));
            CollectionAssert.AreEqual(new[] { AircraftCommand.Taxi }, AircraftCommands.For(false, GroundPhase.AtGate));
            CollectionAssert.AreEqual(new[] { AircraftCommand.Cross }, AircraftCommands.For(false, GroundPhase.HoldingAtCrossing));
            CollectionAssert.AreEqual(new[] { AircraftCommand.TakeOff }, AircraftCommands.For(false, GroundPhase.HoldingShort));
            Assert.IsEmpty(AircraftCommands.For(false, GroundPhase.Taxiing), "No buttons while moving");
            Assert.IsEmpty(AircraftCommands.For(false, null), "No buttons while landing or departing");
        }

        [Test]
        public void NewHelpLines_ShowOnceEach_AfterTheDrawingLesson()
        {
            var help = new FirstTimeHelp();
            help.LineConnected();
            help.Tick(5f, true);

            help.DepartureAtGate();
            Assert.AreEqual(HelpHint.Taxi, help.Current);
            help.Tick(FirstTimeHelp.LongHintSeconds + 0.1f, true);
            help.DepartureAtGate();
            Assert.AreEqual(HelpHint.None, help.Current, "Only the first time");

            help.SpeedButtonsShown();
            Assert.AreEqual(HelpHint.SlowFast, help.Current);
            help.Tick(FirstTimeHelp.LongHintSeconds + 0.1f, true);
            help.DepartureAtCrossing();
            Assert.AreEqual(HelpHint.Cross, help.Current);
            help.Tick(FirstTimeHelp.LongHintSeconds + 0.1f, true);
            help.DepartureAtHoldLine();
            Assert.AreEqual(HelpHint.TakeOff, help.Current);
        }
    }
}
