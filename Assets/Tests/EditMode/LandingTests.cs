using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    public class RunwayTests
    {
        [Test]
        public void Contains_PointsOnAndOffAnEastboundRunway()
        {
            var runway = new Runway(new Vector2(1f, -1f), 0f, 3f, 0.6f);

            Assert.IsTrue(runway.Contains(new Vector2(1f, -1f)), "centre");
            Assert.IsTrue(runway.Contains(new Vector2(2.4f, -0.75f)), "near a corner");
            Assert.IsFalse(runway.Contains(new Vector2(2.6f, -1f)), "past the far end");
            Assert.IsFalse(runway.Contains(new Vector2(1f, -0.6f)), "beside it");
        }

        [Test]
        public void Contains_FollowsTheRunwayRotation()
        {
            var runway = new Runway(Vector2.zero, 90f, 3f, 0.6f); // runs up the screen

            Assert.IsTrue(runway.Contains(new Vector2(0f, 1.4f)));
            Assert.IsFalse(runway.Contains(new Vector2(1.4f, 0f)));
        }

        [Test]
        public void Contains_MarginExtendsEverySide()
        {
            var runway = new Runway(Vector2.zero, 0f, 2f, 0.4f);

            Assert.IsFalse(runway.Contains(new Vector2(1.05f, 0f)));
            Assert.IsTrue(runway.Contains(new Vector2(1.05f, 0f), 0.1f));
            Assert.IsTrue(runway.Contains(new Vector2(0f, 0.28f), 0.1f));
        }

        [Test]
        public void Threshold_IsAtTheStartOfTheLandingDirection()
        {
            var runway = new Runway(new Vector2(1f, 0f), 0f, 3f, 0.6f);

            Assert.AreEqual(-0.5f, runway.Threshold.x, 1e-5f);
            Assert.AreEqual(2.5f, runway.Reversed().Threshold.x, 1e-5f);
            Assert.AreEqual(180f, runway.Reversed().LandingHeadingDegrees, 1e-5f);
        }

        [Test]
        public void TouchdownZone_CoversOnlyTheArrowArea()
        {
            // Threshold at x = -0.5. Zone from 0.1 to 0.8 past it: x = -0.4 to 0.3.
            var runway = new Runway(new Vector2(1f, 0f), 0f, 3f, 0.6f, 0.1f, 0.7f);

            Assert.IsFalse(runway.InTouchdownZone(new Vector2(-0.6f, 0f)), "before the runway");
            Assert.IsFalse(runway.InTouchdownZone(new Vector2(-0.45f, 0f)), "on the runway, before the arrows");
            Assert.IsTrue(runway.InTouchdownZone(new Vector2(-0.4f, 0f)), "start of the zone");
            Assert.IsTrue(runway.InTouchdownZone(new Vector2(0f, 0.29f)), "inside, near the side");
            Assert.IsTrue(runway.InTouchdownZone(new Vector2(0.3f, 0f)), "end of the zone");
            Assert.IsFalse(runway.InTouchdownZone(new Vector2(0.4f, 0f)), "past the zone");
            Assert.IsFalse(runway.InTouchdownZone(new Vector2(0f, 0.34f)), "beside the zone");
            Assert.IsTrue(runway.InTouchdownZone(new Vector2(0f, 0.34f), 0.05f), "beside, within the side margin");
        }

        [Test]
        public void TouchdownZone_MovesToTheOtherEndWhenReversed()
        {
            var runway = new Runway(new Vector2(1f, 0f), 0f, 3f, 0.6f, 0.1f, 0.7f).Reversed();

            Assert.IsTrue(runway.InTouchdownZone(new Vector2(2.2f, 0f)));
            Assert.IsFalse(runway.InTouchdownZone(new Vector2(0f, 0f)));
        }
    }

    public class LandingRulesTests
    {
        // Threshold at x = -0.5. Touchdown zone (the arrows) from x = -0.4 to 0.3.
        static readonly Runway Eastbound = new Runway(new Vector2(1f, -1f), 0f, 3f, 0.6f, 0.1f, 0.7f);
        const float InZoneX = 0f;

        static List<Vector2> Line(params Vector2[] points) => new List<Vector2>(points);

        [Test]
        public void EndsInTheTouchdownZone_HeadingTheLandingWay_Lands()
        {
            var line = Line(new Vector2(-3f, -1f), new Vector2(-0.5f, -1f), new Vector2(InZoneX, -1f));
            Assert.IsTrue(LandingRules.CanLand(Eastbound, line));
        }

        [Test]
        public void EndsBeforeTheRunway_DoesNotLand()
        {
            var line = Line(new Vector2(-3f, -1f), new Vector2(-0.55f, -1f));
            Assert.IsFalse(LandingRules.CanLand(Eastbound, line));
        }

        [Test]
        public void EndsOnTheRunwayButPastTheTouchdownZone_DoesNotLand()
        {
            var line = Line(new Vector2(-3f, -1f), new Vector2(1f, -1f));
            Assert.IsFalse(LandingRules.CanLand(Eastbound, line));
        }

        [Test]
        public void EndsInTheZone_HeadingTheWrongWay_DoesNotLand()
        {
            var line = Line(new Vector2(3f, -1f), new Vector2(InZoneX, -1f));
            Assert.IsFalse(LandingRules.CanLand(Eastbound, line));
        }

        [Test]
        public void ReversedRunway_LandsInTheZoneAtTheOtherEnd()
        {
            var line = Line(new Vector2(5f, -1f), new Vector2(2f, -1f)); // westbound, in the reversed zone
            Assert.IsFalse(LandingRules.CanLand(Eastbound, line));
            Assert.IsTrue(LandingRules.CanLand(Eastbound.Reversed(), line), "Lands once the direction is reversed (wind, C1)");
        }

        [Test]
        public void EndsBesideTheRunway_DoesNotLand()
        {
            var line = Line(new Vector2(-3f, 0f), new Vector2(InZoneX, 0f)); // 1 unit to the left of the centreline
            Assert.IsFalse(LandingRules.CanLand(Eastbound, line));
        }

        [Test]
        public void EndsJustBesideTheZoneEdge_WithinTheSideMargin_Lands()
        {
            float edge = -1f + 0.3f;
            var line = Line(new Vector2(-3f, edge + 0.03f), new Vector2(InZoneX, edge + 0.03f));
            Assert.IsTrue(LandingRules.CanLand(Eastbound, line));
        }

        [TestCase(25f, true)]
        [TestCase(-25f, true)]
        [TestCase(35f, false)]
        [TestCase(90f, false)]
        public void ApproachAngle_MustBeWithinTheLimit(float angleOffRunway, bool expectLanding)
        {
            float radians = angleOffRunway * Mathf.Deg2Rad;
            var end = new Vector2(InZoneX, -1f);
            var start = end - new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 1f;

            Assert.AreEqual(expectLanding, LandingRules.CanLand(Eastbound, Line(start, end)));
        }

        [Test]
        public void FingerTwitchAtTheEnd_DoesNotDecideTheApproach()
        {
            // A good eastbound approach, then a tiny hook upwards as the finger lifts.
            var line = Line(new Vector2(-3f, -1f), new Vector2(InZoneX, -1f), new Vector2(0.02f, -0.98f), new Vector2(0.02f, -0.96f));

            Assert.IsTrue(LandingRules.TryGetApproachHeading(line, out float heading));
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(heading, 0f)), 10f);
            Assert.IsTrue(LandingRules.CanLand(Eastbound, line));
        }

        [Test]
        public void LineWithNoLength_DoesNotLand()
        {
            Assert.IsFalse(LandingRules.CanLand(Eastbound, Line(new Vector2(InZoneX, -1f))));
            Assert.IsFalse(LandingRules.CanLand(Eastbound, Line(new Vector2(InZoneX, -1f), new Vector2(InZoneX, -1f))));
        }

        [Test]
        public void ShortLine_UsesItsWholeLengthForTheApproach()
        {
            var line = Line(new Vector2(-0.1f, -1f), new Vector2(InZoneX, -1f)); // 0.1 long, eastbound
            Assert.IsTrue(LandingRules.TryGetApproachHeading(line, out float heading));
            Assert.AreEqual(0f, Mathf.DeltaAngle(heading, 0f), 1e-3f);
        }

        [Test]
        public void FindLandingRunway_PicksTheRunwayTheLineEndsOn()
        {
            // Second runway runs up the screen: threshold y = 2, zone y = 2.1 to 2.8.
            var runways = new List<Runway> { Eastbound, new Runway(new Vector2(0f, 3f), 90f, 2f, 0.5f, 0.1f, 0.7f) };
            var line = Line(new Vector2(0f, 0f), new Vector2(0f, 2.4f));

            Assert.AreEqual(1, LandingRules.FindLandingRunway(runways, line));
            Assert.AreEqual(-1, LandingRules.FindLandingRunway(runways, Line(new Vector2(0f, 0f), new Vector2(5f, 5f))));
        }

        /// <summary>
        /// Feeds finger samples one at a time, as PathPlanner does, and returns the point where the line
        /// first connects to the runway, or null if it never does.
        /// </summary>
        static Vector2? ConnectionPoint(Vector2 start, IEnumerable<Vector2> samples)
        {
            var recorder = new PathRecorder(0.01f);
            recorder.Begin(start);
            foreach (Vector2 sample in samples)
            {
                recorder.AddSample(sample);
                if (LandingRules.CanLand(Eastbound, recorder.Points))
                    return recorder.Points[recorder.Points.Count - 1];
            }
            return null;
        }

        static IEnumerable<Vector2> Stroke(Vector2 from, Vector2 to, float step = 0.05f)
        {
            int count = Mathf.CeilToInt(Vector2.Distance(from, to) / step);
            for (int i = 1; i <= count; i++)
                yield return Vector2.Lerp(from, to, (float)i / count);
        }

        [Test]
        public void StrokeIntoTheRunway_ConnectsOnTheArrowsNotBefore()
        {
            // Eastbound along the centreline, carrying on well past the runway's far end.
            var start = new Vector2(-3f, -1f);
            Vector2? connected = ConnectionPoint(start, Stroke(start, new Vector2(4f, -1f)));

            Assert.IsTrue(connected.HasValue, "Should connect");
            Assert.IsTrue(Eastbound.InTouchdownZone(connected.Value), "Ends in the touchdown zone");
            Assert.GreaterOrEqual(connected.Value.x, -0.4f, "Not before the arrows");
            Assert.Less(connected.Value.x, -0.4f + 0.06f, "Connects on reaching the zone, not later");
        }

        [Test]
        public void StrokeIntoTheRunwayTheWrongWay_NeverConnects()
        {
            var start = new Vector2(5f, -1f);
            Assert.IsNull(ConnectionPoint(start, Stroke(start, new Vector2(-3f, -1f))));
        }

        [Test]
        public void StrokeAcrossTheZoneTooSteeply_DoesNotConnect()
        {
            // Straight down across the touchdown zone.
            var start = new Vector2(InZoneX, 1f);
            Assert.IsNull(ConnectionPoint(start, Stroke(start, new Vector2(InZoneX, -3f))));
        }

        [Test]
        public void StrokeOntoTheRunwayPastTheZone_DoesNotConnect()
        {
            // A shallow approach that reaches the runway beyond the arrows, then runs along it.
            var start = new Vector2(-1f, 0f);
            var samples = new List<Vector2>(Stroke(start, new Vector2(1.5f, -1f)));
            samples.AddRange(Stroke(new Vector2(1.5f, -1f), new Vector2(2.4f, -1f)));
            Assert.IsNull(ConnectionPoint(start, samples));
        }

        [Test]
        public void FollowedLine_EndingInTheZone_LandsWhenTheAircraftFinishesIt()
        {
            // The same flow as AircraftController: follow the line, then check it when the path ends.
            var motion = new AircraftMotion(new Vector2(-3f, -1f), 0f, 1.2f, 200f);
            motion.BeginPath();
            for (int i = 1; i <= 60; i++)
                motion.AppendWaypoint(new Vector2(-3f + i * 0.05f, -1f)); // ends at x = 0, in the zone

            bool finished = false;
            for (int i = 0; i < 60 * 10 && !finished; i++)
            {
                motion.Step(1f / 60f, new Rect(-100f, -100f, 200f, 200f));
                finished = !motion.HasPath;
            }

            Assert.IsTrue(finished);
            Assert.IsTrue(LandingRules.CanLand(Eastbound, motion.Path));
        }
    }
}
