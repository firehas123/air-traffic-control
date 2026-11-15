using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    public class PathRecorderTests
    {
        const float MinDistance = 0.01f; // same as PathPlanner

        [Test]
        public void Begin_StoresStartPointAndStartsRecording()
        {
            var recorder = new PathRecorder(MinDistance);
            recorder.Begin(new Vector2(1f, 2f));

            Assert.IsTrue(recorder.IsRecording);
            Assert.AreEqual(1, recorder.Points.Count);
            Assert.AreEqual(new Vector2(1f, 2f), recorder.Points[0]);
        }

        [Test]
        public void AddSample_KeepsEachSampleExactly()
        {
            var recorder = new PathRecorder(MinDistance);
            recorder.Begin(Vector2.zero);
            var samples = new[] { new Vector2(0.3f, 0.01f), new Vector2(1.7f, -0.4f), new Vector2(1.70002f, 2.5f) };
            var added = new List<Vector2>();

            foreach (Vector2 s in samples)
                recorder.AddSample(s, added);

            CollectionAssert.AreEqual(samples, added);
            for (int i = 0; i < samples.Length; i++)
                Assert.AreEqual(samples[i], recorder.Points[i + 1], "No resampling or smoothing");
        }

        [Test]
        public void AddSample_LeavesOutRepeatsOfThePreviousPoint()
        {
            var recorder = new PathRecorder(MinDistance);
            recorder.Begin(Vector2.zero);

            Assert.AreEqual(0, recorder.AddSample(Vector2.zero));
            Assert.AreEqual(0, recorder.AddSample(new Vector2(0.005f, 0f)));
            Assert.AreEqual(1, recorder.AddSample(new Vector2(0.02f, 0f)));
            Assert.AreEqual(2, recorder.Points.Count);
        }

        [Test]
        public void SmallZigzag_IsKeptPointForPoint()
        {
            var recorder = new PathRecorder(MinDistance);
            recorder.Begin(Vector2.zero);
            var samples = new List<Vector2>();
            for (int i = 1; i <= 20; i++)
                samples.Add(new Vector2(i * 0.05f, i % 2 == 0 ? -0.03f : 0.03f)); // under 1 mm either side

            foreach (Vector2 s in samples)
                recorder.AddSample(s);

            Assert.AreEqual(samples.Count + 1, recorder.Points.Count);
            for (int i = 0; i < samples.Count; i++)
                Assert.AreEqual(samples[i], recorder.Points[i + 1]);
        }

        [Test]
        public void AddSample_DoesNothingWhenNotRecording()
        {
            var recorder = new PathRecorder(MinDistance);
            Assert.AreEqual(0, recorder.AddSample(new Vector2(5f, 0f)));

            recorder.Begin(Vector2.zero);
            recorder.End();
            Assert.AreEqual(0, recorder.AddSample(new Vector2(5f, 0f)));
        }

        [Test]
        public void Begin_DiscardsThePreviousLine()
        {
            var recorder = new PathRecorder(MinDistance);
            recorder.Begin(Vector2.zero);
            recorder.AddSample(new Vector2(1f, 0f));

            recorder.Begin(new Vector2(3f, 3f));

            Assert.AreEqual(1, recorder.Points.Count);
            Assert.AreEqual(new Vector2(3f, 3f), recorder.Points[0]);
        }
    }
}
