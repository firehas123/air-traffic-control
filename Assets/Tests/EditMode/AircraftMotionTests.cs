using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    public class AircraftMotionTests
    {
        const float Dt = 1f / 60f;
        const float Speed = 1.2f;
        const float TurnRate = 200f; // same as PrototypeSetup; used only for the edge turn-back
        const float OnLine = 1e-4f;

        // Big enough that the turn-back rule never applies.
        static readonly Rect Open = new Rect(-1000f, -1000f, 2000f, 2000f);

        static AircraftMotion NewMotion(Vector2 position, float heading = 0f) =>
            new AircraftMotion(position, heading, Speed, TurnRate);

        /// <summary>Starts a path at the aircraft (as PathPlanner does) and adds the given points.</summary>
        static AircraftMotion FollowFromOrigin(IEnumerable<Vector2> points, float heading = 0f)
        {
            var motion = NewMotion(Vector2.zero, heading);
            motion.BeginPath();
            foreach (Vector2 point in points)
                Assert.IsTrue(motion.AppendWaypoint(point));
            return motion;
        }

        static IEnumerable<Vector2> Polyline(params Vector2[] corners)
        {
            // Points every 0.05 along straight lines between the corners (the first corner is the origin).
            Vector2 last = Vector2.zero;
            foreach (Vector2 corner in corners)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(last, corner) / 0.05f));
                for (int i = 1; i <= steps; i++)
                    yield return Vector2.Lerp(last, corner, (float)i / steps);
                last = corner;
            }
        }

        static IEnumerable<Vector2> Zigzag()
        {
            for (int i = 1; i <= 40; i++)
                yield return new Vector2(i * 0.1f, i % 2 == 0 ? -0.1f : 0.1f);
        }

        static IEnumerable<Vector2> TinyCircle()
        {
            const float radius = 0.1f; // about 2 mm on a 12.9" iPad
            var centre = new Vector2(0f, radius);
            for (int i = 1; i <= 24; i++)
            {
                float angle = -Mathf.PI / 2f + 2f * Mathf.PI * i / 24;
                yield return centre + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        static IEnumerable<TestCaseData> AwkwardLines()
        {
            yield return new TestCaseData(new List<Vector2>(Polyline(new Vector2(3f, 0f), new Vector2(3f, 3f)))).SetName("LShape");
            yield return new TestCaseData(new List<Vector2>(Polyline(new Vector2(2f, 0f), new Vector2(0f, 0f)))).SetName("HairpinStraightBack");
            yield return new TestCaseData(new List<Vector2>(Polyline(new Vector2(2f, 0f), new Vector2(2f, 0.05f), new Vector2(0f, 0.05f)))).SetName("HairpinNarrow");
            yield return new TestCaseData(new List<Vector2>(Polyline(new Vector2(-2f, 0f)))).SetName("ImmediatelyBackwards");
            yield return new TestCaseData(new List<Vector2>(Polyline(new Vector2(0f, -2f)))).SetName("ImmediateRightAngle");
            yield return new TestCaseData(new List<Vector2>(Zigzag())).SetName("FineZigzag");
            yield return new TestCaseData(new List<Vector2>(TinyCircle())).SetName("TinyCircle");
        }

        [TestCaseSource(nameof(AwkwardLines))]
        public void FollowsTheDrawnLineExactly(List<Vector2> points)
        {
            var motion = FollowFromOrigin(points);
            var drawn = new List<Vector2> { Vector2.zero };
            drawn.AddRange(points);
            float length = motion.PathLength;

            int steps = 0;
            while (motion.HasPath)
            {
                Vector2 before = motion.Position;
                float alongBefore = motion.DistanceAlongPath;
                motion.Step(Dt, Open);
                steps++;
                Assert.Less(steps, 60 * 60, "Aircraft should reach the end of the line");
                if (!motion.HasPath)
                    break;

                // 1. On the line: on the segment between the last passed point and the next one.
                int next = drawn.Count - motion.RemainingWaypointCount;
                Assert.LessOrEqual(DistanceToSegment(motion.Position, drawn[next - 1], drawn[next]), OnLine,
                    $"Off the line at step {steps}");

                // 2. Heading is the direction of that segment (instant turns at corners).
                Vector2 dir = drawn[next] - drawn[next - 1];
                float segmentHeading = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                Assert.AreEqual(0f, Mathf.DeltaAngle(segmentHeading, motion.HeadingDegrees), 0.01f,
                    $"Heading does not match the segment at step {steps}");

                // 3. Constant speed along the line.
                Assert.AreEqual(Speed * Dt, motion.DistanceAlongPath - alongBefore, 1e-4f);
                Assert.LessOrEqual(Vector2.Distance(before, motion.Position), Speed * Dt + 1e-4f);

                // 4. The line ahead is exactly the drawn points.
                for (int i = 0; i < motion.RemainingWaypointCount; i++)
                    Assert.AreEqual(drawn[next + i], motion.GetRemainingWaypoint(i));

                // 5. The followed path is the drawn line, point for point.
                PathIntegrity.CheckSameLine(drawn, motion.Path);
            }

            Assert.AreEqual(Mathf.CeilToInt(length / (Speed * Dt)), steps, 1, "Time to fly the line = length / speed");
        }

        [Test]
        public void HairpinReversal_TurnsRoundInstantlyAtTheCorner()
        {
            var motion = FollowFromOrigin(new[] { new Vector2(2f, 0f), new Vector2(0f, 0f) });

            float closestToCorner = float.MaxValue;
            float previousHeading = motion.HeadingDegrees;
            float largestSingleTurn = 0f;
            while (motion.HasPath)
            {
                motion.Step(Dt, Open);
                closestToCorner = Mathf.Min(closestToCorner, Vector2.Distance(motion.Position, new Vector2(2f, 0f)));
                largestSingleTurn = Mathf.Max(largestSingleTurn, Mathf.Abs(Mathf.DeltaAngle(previousHeading, motion.HeadingDegrees)));
                previousHeading = motion.HeadingDegrees;
            }

            Assert.LessOrEqual(closestToCorner, Speed * Dt, "Aircraft should fly right up to the corner");
            Assert.AreEqual(180f, largestSingleTurn, 0.01f, "Aircraft should reverse in a single step");
        }

        [Test]
        public void BeginPath_StartsTheLineAtTheAircraft()
        {
            var motion = NewMotion(new Vector2(1f, 2f));
            motion.BeginPath();

            Assert.AreEqual(1, motion.Path.Count);
            Assert.AreEqual(new Vector2(1f, 2f), motion.Path[0]);
        }

        [Test]
        public void AppendWaypoint_StoresThePointUnchanged()
        {
            var motion = NewMotion(Vector2.zero);
            motion.BeginPath();
            var point = new Vector2(0.123456f, -0.987654f);
            motion.AppendWaypoint(point);

            Assert.AreEqual(point, motion.Path[1]);
            Assert.AreEqual(point, motion.GetRemainingWaypoint(0));
        }

        [Test]
        public void AfterPathEnds_CarriesOnAlongTheLastSegment()
        {
            var motion = FollowFromOrigin(new[] { new Vector2(1f, 1f) }); // 45 degrees
            while (motion.HasPath)
                motion.Step(Dt, Open);

            Vector2 atEnd = motion.Position;
            for (int i = 0; i < 60; i++)
                motion.Step(Dt, Open);

            Assert.AreEqual(45f, motion.HeadingDegrees, 0.01f);
            Vector2 moved = motion.Position - atEnd;
            Assert.AreEqual(moved.x, moved.y, 1e-3f, "Straight on at 45 degrees");
            Assert.AreEqual(Speed, moved.magnitude, 1e-2f);
        }

        [Test]
        public void AppendWaypoint_AfterTheAircraftFlewOffTheEnd_IsRefused()
        {
            var motion = FollowFromOrigin(new[] { new Vector2(0.1f, 0f) });
            while (motion.HasPath)
                motion.Step(Dt, Open);

            Assert.IsFalse(motion.AppendWaypoint(new Vector2(1f, 1f)));
            Assert.AreEqual(2, motion.Path.Count, "The line must not change");
        }

        [Test]
        public void BeginPath_ReplacesTheOldLine()
        {
            var motion = FollowFromOrigin(new[] { new Vector2(2f, 0f) });
            motion.Step(Dt, Open);
            Vector2 here = motion.Position;

            motion.BeginPath();

            Assert.AreEqual(1, motion.Path.Count);
            Assert.AreEqual(here, motion.Path[0]);
        }

        [Test]
        public void NoPath_FliesStraightAtConstantSpeed()
        {
            var motion = NewMotion(Vector2.zero);

            for (int i = 0; i < 60; i++)
                motion.Step(Dt, Open);

            Assert.AreEqual(Speed, motion.Position.x, 1e-3f);
            Assert.AreEqual(0f, motion.Position.y, 1e-3f);
            Assert.AreEqual(0f, motion.HeadingDegrees, 1e-3f);
        }

        [Test]
        public void NoPath_EdgeTurnBack_RespectsTheTurnRate()
        {
            var bounds = new Rect(-5f, -4f, 10f, 8f);
            var motion = NewMotion(new Vector2(4.9f, 0f));

            for (int i = 0; i < 180; i++)
            {
                float before = motion.HeadingDegrees;
                motion.Step(Dt, bounds);
                Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(before, motion.HeadingDegrees)), TurnRate * Dt + 1e-3f);
            }
        }

        [TestCase(0f)]
        [TestCase(2f)]
        [TestCase(20f)]
        [TestCase(45f)]
        [TestCase(88f)]
        [TestCase(137f)]
        public void NoPath_TurnsBackAtTheEdgeAndComesBackInside(float startHeading)
        {
            var bounds = new Rect(-5f, -4f, 10f, 8f);
            var motion = NewMotion(Vector2.zero, startHeading);
            // A mirrored turn off one edge goes about one turning radius past it. Near a corner the
            // target can be mirrored twice, which measured up to about 1.5 radii. Allow two. In the game
            // the bounds sit 0.8 units inside the screen edge, so the aircraft stays visible.
            float allowedOvershoot = 2f * motion.TurnRadius;
            int framesOutside = 0;
            int mostFramesOutside = 0;
            float worstOvershoot = 0f;

            for (int i = 0; i < 60 * 120; i++)
            {
                motion.Step(Dt, bounds);
                float overshoot = Mathf.Max(Mathf.Abs(motion.Position.x) - 5f, Mathf.Abs(motion.Position.y) - 4f);
                worstOvershoot = Mathf.Max(worstOvershoot, overshoot);

                framesOutside = bounds.Contains(motion.Position) ? 0 : framesOutside + 1;
                mostFramesOutside = Mathf.Max(mostFramesOutside, framesOutside);
            }

            Assert.LessOrEqual(worstOvershoot, allowedOvershoot,
                $"Worst overshoot {worstOvershoot:F3}, turn radius {motion.TurnRadius:F3}");
            Assert.Less(mostFramesOutside, 60 * 3, "Aircraft should come back inside within 3 seconds");
        }

        [Test]
        public void ClearPath_RemovesThePath()
        {
            var motion = FollowFromOrigin(new[] { new Vector2(5f, 0f) });
            motion.ClearPath();

            Assert.IsFalse(motion.HasPath);
            Assert.AreEqual(0, motion.RemainingWaypointCount);
            Assert.AreEqual(0, motion.Path.Count);
        }
    }
}
