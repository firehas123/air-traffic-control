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
            // A departure waiting at the hold line beside the runway's start, then Take off.
            var departure = GroundMovement.AtStand(new Vector2(-0.6f, 0.3f), 270f, 0, 0, 0.35f, 0.6f);
            var route = new TaxiRoute();
            route.points.Add(new Vector2(-0.6f, 0.3f));
            route.points.Add(new Vector2(-0.45f, -0.15f));
            route.nodes.Add(0);
            route.nodes.Add(1);
            departure.FollowRoute(route, GroundPhase.HoldingShort);
            for (float t = 0f; t < 5f; t += 1f / 60f) departure.Step(1f / 60f, false);
            Vector2 lineUp = GroundMovement.LineUpPoint(runway, departure.Position);
            departure.TakeOff(runway, 0, lineUp, GroundMovement.LiftOffDistance(runway, lineUp));
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
            CollectionAssert.AreEqual(new[] { AircraftCommand.Slow, AircraftCommand.Fast }, AircraftCommands.For(true));
            Assert.IsEmpty(AircraftCommands.For(false), "No buttons while landing or departing");
            var runways = new List<RunwayChoice> { new RunwayChoice(0, true, true) };
            Assert.AreEqual(AircraftCommand.Taxi, AircraftCommands.TowerGround(GroundPhase.AtStand, runways, -1, false, true)[0].Command);
            Assert.AreEqual(AircraftCommand.Cross, AircraftCommands.TowerGround(GroundPhase.HoldingAtCrossing, runways, -1, false, true)[0].Command);
            Assert.AreEqual(AircraftCommand.TakeOff, AircraftCommands.TowerGround(GroundPhase.HoldingShort, runways, 0, true, true)[0].Command);
            Assert.IsEmpty(AircraftCommands.TowerGround(GroundPhase.Taxiing, runways, -1, false, true), "No buttons while moving");
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
