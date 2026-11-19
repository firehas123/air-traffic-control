using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    /// <summary>Wind grace period, blueprint 14.3: within 1.5 s of touchdown an aircraft keeps its landing.</summary>
    public class WindGraceTests
    {
        const float Grace = WindGrace.DefaultGraceSeconds;

        // Threshold at x = -0.5; touchdown zone (the arrows) from x = -0.4 to 0.3. Reversed, it is at the other end.
        static readonly Runway Eastbound = new Runway(new Vector2(1f, -1f), 0f, 3f, 0.6f, 0.1f, 0.7f);

        [Test]
        public void JustInsideTheGracePeriod_KeepsItsLanding()
        {
            Assert.AreEqual(ClearanceAfterWindChange.KeepsLandingOnOldZone, WindGrace.Decide(true, Grace - 0.05f, false, Grace));
            Assert.AreEqual(ClearanceAfterWindChange.KeepsLandingOnOldZone, WindGrace.Decide(true, Grace, false, Grace), "Exactly 1.5 s counts");
        }

        [Test]
        public void JustOutsideTheGracePeriod_LosesItsClearance()
        {
            Assert.AreEqual(ClearanceAfterWindChange.NotCleared, WindGrace.Decide(true, Grace + 0.05f, false, Grace));
        }

        [Test]
        public void NotClearedBefore_GetsNoGrace()
        {
            Assert.AreEqual(ClearanceAfterWindChange.NotCleared, WindGrace.Decide(false, 0.2f, false, Grace));
        }

        [Test]
        public void LineEndingInTheNewZone_IsCleared_WhateverTheTime()
        {
            Assert.AreEqual(ClearanceAfterWindChange.ClearedForNewZone, WindGrace.Decide(false, 30f, true, Grace));
            Assert.AreEqual(ClearanceAfterWindChange.ClearedForNewZone, WindGrace.Decide(true, 0.5f, true, Grace));
        }

        /// <summary>
        /// An airliner flying a line that ends in the eastbound touchdown zone, stopped when it is
        /// <paramref name="secondsLeft"/> game seconds from touchdown. Returns its motion.
        /// </summary>
        static AircraftMotion AirlinerOnFinalApproach(float secondsLeft)
        {
            const float speed = 0.6f;
            var motion = new AircraftMotion(new Vector2(-4f, -1f), 0f, speed, 200f);
            motion.BeginPath();
            for (int i = 1; i <= 80; i++)
                motion.AppendWaypoint(new Vector2(-4f + i * 0.05f, -1f)); // ends at x = 0, in the zone

            var bounds = new Rect(-100f, -100f, 200f, 200f);
            while (motion.SecondsToEndOfPath > secondsLeft)
                motion.Step(Mathf.Min(1f / 60f, motion.SecondsToEndOfPath - secondsLeft), bounds);
            return motion;
        }

        [Test]
        public void AircraftJustInside_KeepsItsLanding_AndTouchesDownOnTheOldZone()
        {
            AircraftMotion motion = AirlinerOnFinalApproach(1.4f);
            Runway reversed = Eastbound.Reversed();

            Assert.IsTrue(LandingRules.CanLand(Eastbound, motion.Path), "Cleared before the change");
            bool landsAfter = LandingRules.CanLand(reversed, motion.Path);
            Assert.IsFalse(landsAfter, "Its line ends at the old end");

            Assert.AreEqual(ClearanceAfterWindChange.KeepsLandingOnOldZone,
                WindGrace.Decide(true, motion.SecondsToEndOfPath, landsAfter, Grace));

            // It flies on to the end of its line, which is still in the old zone where it lands.
            var bounds = new Rect(-100f, -100f, 200f, 200f);
            for (int i = 0; i < 200 && motion.HasPath; i++)
                motion.Step(1f / 60f, bounds);
            Assert.IsFalse(motion.HasPath);
            Assert.IsTrue(Eastbound.InTouchdownZone(motion.Path[motion.Path.Count - 1], LandingRules.ConnectInset));
        }

        [Test]
        public void AircraftJustOutside_LosesItsClearance()
        {
            AircraftMotion motion = AirlinerOnFinalApproach(1.6f);
            bool landsAfter = LandingRules.CanLand(Eastbound.Reversed(), motion.Path);

            Assert.AreEqual(ClearanceAfterWindChange.NotCleared,
                WindGrace.Decide(true, motion.SecondsToEndOfPath, landsAfter, Grace));
        }

        [Test]
        public void SecondsToEndOfPath_IsRemainingLengthOverSpeed()
        {
            var motion = new AircraftMotion(Vector2.zero, 0f, 0.5f, 200f);
            Assert.AreEqual(float.PositiveInfinity, motion.SecondsToEndOfPath, "No path");

            motion.BeginPath();
            motion.AppendWaypoint(new Vector2(2f, 0f));
            Assert.AreEqual(4f, motion.SecondsToEndOfPath, 1e-4f);

            motion.Step(1f, new Rect(-10f, -10f, 20f, 20f));
            Assert.AreEqual(3f, motion.SecondsToEndOfPath, 1e-4f);
        }
    }
}
