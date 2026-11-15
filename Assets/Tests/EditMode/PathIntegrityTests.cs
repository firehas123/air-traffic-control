using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    public class PathIntegrityTests
    {
        static List<Vector2> Line() => new List<Vector2> { Vector2.zero, new Vector2(1f, 0f), new Vector2(1f, 1f) };

        [Test]
        public void SameLine_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => PathIntegrity.CheckSameLine(Line(), Line()));
        }

        [Test]
        public void MovedPoint_Throws()
        {
            var altered = Line();
            altered[1] = new Vector2(1f, 0.0001f);

            var error = Assert.Throws<PathAlteredException>(() => PathIntegrity.CheckSameLine(Line(), altered));
            StringAssert.Contains("Point 1", error.Message);
        }

        [Test]
        public void RemovedPoint_Throws()
        {
            var altered = Line();
            altered.RemoveAt(1);

            Assert.Throws<PathAlteredException>(() => PathIntegrity.CheckSameLine(Line(), altered));
        }

        [Test]
        public void ExtraPoint_Throws()
        {
            var altered = Line();
            altered.Add(new Vector2(2f, 2f));

            Assert.Throws<PathAlteredException>(() => PathIntegrity.CheckSameLine(Line(), altered));
        }

        [Test]
        public void FollowedPath_MatchesWhatTheRecorderDrew()
        {
            // The same flow as PathPlanner: start at the aircraft, add finger samples.
            var motion = new AircraftMotion(new Vector2(0.3f, -0.2f), 0f, 1.2f, 200f);
            var recorder = new PathRecorder(0.01f);
            motion.BeginPath();
            recorder.Begin(motion.Position);
            var added = new List<Vector2>();
            foreach (Vector2 sample in new[] { new Vector2(0.9f, 0.1f), new Vector2(0.905f, 0.1f), new Vector2(1.4f, 0.8f), new Vector2(0.2f, 0.8f) })
            {
                added.Clear();
                recorder.AddSample(sample, added);
                foreach (Vector2 p in added)
                    motion.AppendWaypoint(p);
                motion.Step(1f / 60f, new Rect(-10f, -10f, 20f, 20f));
                PathIntegrity.CheckSameLine(recorder.Points, motion.Path);
            }
        }
    }
}
