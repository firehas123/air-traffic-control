using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    /// <summary>Carrier ship (blueprint 13.2) and its tether (14.2).</summary>
    public class CarrierTests
    {
        // A 12.9" iPad in landscape: about 13.3 units wide, centred on 0.
        const float ScreenMin = -6.67f;
        const float ScreenMax = 6.67f;
        const float Lane = -4.1f;

        static CarrierPass NewPass(bool fromLeft = true, float seconds = 20f) =>
            new CarrierPass(ScreenMin, ScreenMax, Lane, fromLeft, seconds, 2.6f, 0.7f, 2.1f, 0.5f, 0.1f, 0.7f);

        static List<Vector2> Line(params Vector2[] points) => new List<Vector2>(points);

        // A point in the middle of the deck's touchdown zone, where it is now.
        static Vector2 InDeckZone(CarrierPass pass)
        {
            Runway deck = pass.Deck;
            float intoZone = deck.TouchdownZoneStart + deck.TouchdownZoneLength / 2f;
            return deck.Threshold + deck.Direction * intoZone;
        }

        static void Sail(CarrierPass pass, float seconds)
        {
            for (float t = 0f; t < seconds; t += 1f / 60f)
                pass.Tick(1f / 60f);
        }

        // ---- The crossing ----

        [Test]
        public void Pass_StartsJustOffScreen_AndCrossesInTheSetTime()
        {
            CarrierPass pass = NewPass();
            Assert.AreEqual(ScreenMin - 1.3f, pass.Centre.x, 1e-4f);
            Assert.AreEqual(Lane, pass.Centre.y);
            Assert.IsFalse(pass.IsFullyOnScreen);

            pass.Tick(20f);
            Assert.IsTrue(pass.IsFinished);
            Assert.AreEqual(ScreenMax + 1.3f, pass.Centre.x, 1e-3f);
        }

        [Test]
        public void Pass_IsFullyOnScreenOnlyInTheMiddleOfTheCrossing()
        {
            CarrierPass pass = NewPass();
            pass.Tick(2f);
            Assert.IsFalse(pass.IsFullyOnScreen, "Still coming on");
            pass.Tick(8f); // 10 s: half way
            Assert.IsTrue(pass.IsFullyOnScreen);
            pass.Tick(9f); // 19 s
            Assert.IsFalse(pass.IsFullyOnScreen, "Already leaving");
        }

        [Test]
        public void Countdown_RunsDownToTheMomentItStartsLeaving()
        {
            CarrierPass pass = NewPass();
            pass.Tick(10f);
            float left = pass.SecondsUntilLeavesScreen;
            Assert.Greater(left, 0f);

            pass.Tick(left - 0.01f);
            Assert.IsTrue(pass.IsFullyOnScreen);
            pass.Tick(0.02f);
            Assert.IsFalse(pass.IsFullyOnScreen);
            Assert.AreEqual(0f, pass.SecondsUntilLeavesScreen, 1e-4f);
        }

        [Test]
        public void Pass_FromTheRight_SailsLeft_AndItsDeckFacesThatWay()
        {
            CarrierPass pass = NewPass(fromLeft: false);
            float startX = pass.Centre.x;
            pass.Tick(5f);
            Assert.Less(pass.Centre.x, startX);
            Assert.AreEqual(180f, pass.Deck.LandingHeadingDegrees, 1e-4f);
        }

        [Test]
        public void ShipLocalPoints_MoveWithTheShip()
        {
            CarrierPass pass = NewPass(fromLeft: false);
            pass.Tick(10f);
            Vector2 world = pass.Centre + new Vector2(0.3f, 0.1f);
            Vector2 local = pass.ToShipLocal(world);

            Assert.AreEqual(world, pass.ToWorld(local));
            pass.Tick(1f);
            Assert.AreEqual(world.x - pass.Speed * 1f, pass.ToWorld(local).x, 1e-3f, "Moved with the ship");
        }

        // ---- Connecting ----

        [Test]
        public void Connect_OnlyWhenTheShipIsFullyOnScreen()
        {
            CarrierPass entering = NewPass();
            entering.Tick(1.5f);
            Assert.IsFalse(entering.IsFullyOnScreen);
            Vector2 zone = InDeckZone(entering);
            Assert.IsFalse(CarrierRules.CanConnect(entering, Line(zone + new Vector2(-2f, 2f), zone)), "Not yet fully on screen");

            CarrierPass onScreen = NewPass();
            onScreen.Tick(10f);
            zone = InDeckZone(onScreen);
            Assert.IsTrue(CarrierRules.CanConnect(onScreen, Line(zone + new Vector2(-2f, 2f), zone)));
        }

        [Test]
        public void Connect_NeedsTheDeckTouchdownZone_FromAnyDirection()
        {
            CarrierPass pass = NewPass();
            pass.Tick(10f);
            Vector2 zone = InDeckZone(pass);

            Assert.IsTrue(CarrierRules.CanConnect(pass, Line(zone + new Vector2(0f, 3f), zone)), "From above");
            Assert.IsTrue(CarrierRules.CanConnect(pass, Line(zone + new Vector2(4f, 0f), zone)), "From ahead of the ship");
            Assert.IsFalse(CarrierRules.CanConnect(pass, Line(zone + new Vector2(0f, 3f), pass.Centre + new Vector2(0.9f, 0f))), "Bow end, not the zone");
            Assert.IsFalse(CarrierRules.CanConnect(pass, Line(zone + new Vector2(0f, 3f), zone + new Vector2(0f, 1f))), "Off the deck");
            Assert.IsFalse(CarrierRules.CanConnect(null, Line(zone, zone + Vector2.one)), "No ship");
        }

        // ---- The tether ----

        [Test]
        public void Tether_StartsAtTheEndOfTheLine_AndReachesTheMovingDeck()
        {
            CarrierPass pass = NewPass();
            pass.Tick(8f);
            Vector2 lineEnd = InDeckZone(pass);
            var approach = new CarrierApproach(pass, lineEnd);
            Assert.AreEqual(CarrierApproachState.FlyingLine, approach.State);

            // The fighter takes 1 s to fly the rest of its line; the ship sails on meanwhile.
            Sail(pass, 1f);
            Assert.AreEqual(lineEnd, approach.LineEnd, "The end of the drawn line never moves");
            Assert.Greater(Vector2.Distance(approach.DeckPoint, lineEnd), 0.5f, "The deck point has moved with the ship");

            approach.StartTether();
            Assert.AreEqual(lineEnd, approach.TetherPosition, "No jump: the tether starts where the line ends");

            const float fighterSpeed = 0.96f; // 1.6 x 0.6
            Vector2 previous = approach.TetherPosition;
            float seconds = 0f;
            while (approach.State == CarrierApproachState.Tethered && seconds < 10f)
            {
                pass.Tick(1f / 60f);
                approach.CheckShip();
                approach.StepTether(fighterSpeed / 60f);
                Vector2 now = approach.TetherPosition;
                Assert.Less(Vector2.Distance(previous, now), 0.1f, "Moves smoothly, no teleporting");
                previous = now;
                seconds += 1f / 60f;
            }

            Assert.AreEqual(CarrierApproachState.TouchedDown, approach.State, "Catches the deck even though it moves away");
            Assert.AreEqual(approach.DeckPoint.x, approach.TetherPosition.x, 1e-3f);
        }

        [Test]
        public void ShipLeavingAtTheLastMoment_LosesTheConnection()
        {
            CarrierPass pass = NewPass();
            pass.Tick(10f);
            var approach = new CarrierApproach(pass, InDeckZone(pass));
            approach.StartTether();

            // Sail until just before the ship starts to leave, then just after, without reaching the deck.
            pass.Tick(pass.SecondsUntilLeavesScreen - 0.01f);
            Assert.IsFalse(approach.CheckShip(), "Still fully on screen");
            Assert.IsTrue(approach.IsConnected);

            pass.Tick(0.02f);
            Assert.IsTrue(approach.CheckShip(), "Lost the moment it starts to leave");
            Assert.AreEqual(CarrierApproachState.Lost, approach.State);
            Assert.IsFalse(approach.StepTether(10f), "No landing once lost");
        }

        [Test]
        public void ConnectionWhileStillFlyingTheLine_IsLostToo()
        {
            CarrierPass pass = NewPass();
            pass.Tick(10f);
            var approach = new CarrierApproach(pass, InDeckZone(pass));
            pass.Tick(10f);
            Assert.IsTrue(approach.CheckShip());
            Assert.IsFalse(approach.IsConnected);
        }

        [Test]
        public void FighterFliesItsDrawnLineExactly_BeforeTheTether()
        {
            // The tether is the only part that moves (14.2): the followed path stays the drawn line.
            CarrierPass pass = NewPass();
            pass.Tick(10f);
            Vector2 end = InDeckZone(pass);
            var motion = new AircraftMotion(end + new Vector2(-2f, 2f), 0f, 0.96f, 200f);
            var recorder = new PathRecorder(0.01f);
            motion.BeginPath();
            recorder.Begin(motion.Position);
            for (int i = 1; i <= 40; i++)
            {
                Vector2 p = Vector2.Lerp(motion.Path[0], end, i / 40f);
                recorder.AddSample(p);
                motion.AppendWaypoint(p);
            }
            Assert.IsTrue(CarrierRules.CanConnect(pass, recorder.Points));
            var approach = new CarrierApproach(pass, motion.Path[motion.Path.Count - 1]);

            var bounds = new Rect(-100f, -100f, 200f, 200f);
            while (motion.HasPath)
            {
                pass.Tick(1f / 60f);
                motion.Step(1f / 60f, bounds);
                PathIntegrity.CheckSameLine(recorder.Points, motion.Path);
            }
            Assert.IsTrue(approach.IsConnected, "About 3 s of flying; the ship is still fully on screen");
        }

        // ---- The schedule ----

        [Test]
        public void Schedule_FirstShipAfterTheSetTime_ThenEveryIntervalAlternatingSides()
        {
            var schedule = new CarrierSchedule(10f, 40f, 60f, 3);
            Assert.IsTrue(schedule.NextFromLeft);
            Assert.IsFalse(schedule.Tick(9.9f, false));
            Assert.IsTrue(schedule.Tick(0.2f, false), "First ship at 10 s");
            schedule.ShipStarted();
            Assert.IsFalse(schedule.NextFromLeft, "The next comes from the other side");
            Assert.That(schedule.SecondsUntilNextShip, Is.InRange(40f, 60f));
        }

        [Test]
        public void Schedule_WaitsWhileAShipIsStillCrossing()
        {
            var schedule = new CarrierSchedule(5f, 10f, 10f, 1);
            Assert.IsFalse(schedule.Tick(6f, true), "Due, but a ship is still crossing");
            Assert.IsTrue(schedule.Tick(0.1f, false), "Starts once it has gone");
        }
    }
}
