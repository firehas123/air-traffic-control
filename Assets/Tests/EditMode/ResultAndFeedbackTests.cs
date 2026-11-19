using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    /// <summary>Result screen numbers (C10) and the sound synthesis (M2 step 4).</summary>
    public class LevelResultTests
    {
        static readonly ResultSettings Rules = new ResultSettings(); // 2 stars within 100 s; 3 also need no warnings

        [Test]
        public void NotCleared_NoStars()
        {
            Assert.AreEqual(0, LevelResult.Stars(false, 50f, 0, Rules));
        }

        [Test]
        public void ClearedSlowly_OneStar()
        {
            Assert.AreEqual(1, LevelResult.Stars(true, 130f, 0, Rules));
        }

        [Test]
        public void ClearedInTime_ButWithWarnings_TwoStars()
        {
            Assert.AreEqual(2, LevelResult.Stars(true, 90f, 3, Rules));
        }

        [Test]
        public void ClearedInTime_WithNoWarnings_ThreeStars()
        {
            Assert.AreEqual(3, LevelResult.Stars(true, 90f, 0, Rules));
            Assert.AreEqual(3, LevelResult.Stars(true, 100f, 0, Rules), "Exactly on the time counts");
        }

        [Test]
        public void ReckelessFlying_NeverGetsThreeStars()
        {
            // Fast but with near misses: the third star is only for safe flying.
            Assert.Less(LevelResult.Stars(true, 40f, 1, Rules), 3);
        }

        [Test]
        public void Coins_ArePerLandingPlusPerStar()
        {
            Assert.AreEqual(8 * 5 + 3 * 10, LevelResult.Coins(8, 3, Rules));
            Assert.AreEqual(0, LevelResult.Coins(0, 0, Rules));
        }

        [Test]
        public void ClosestGap_IsEdgeToEdge()
        {
            var positions = new List<Vector2> { Vector2.zero, new Vector2(1f, 0f), new Vector2(5f, 0f) };
            var radii = new List<float> { 0.25f, 0.35f, 0.25f };
            Assert.AreEqual(1f - 0.25f - 0.35f, LevelResult.ClosestGap(positions, radii), 1e-5f);
            Assert.AreEqual(float.PositiveInfinity, LevelResult.ClosestGap(new List<Vector2> { Vector2.zero }, new List<float> { 0.25f }));
        }

        [Test]
        public void ClosestPassText_InMetres()
        {
            Assert.AreEqual("Closest pass 42 m", LevelResult.ClosestPassText(0.42f));
            Assert.AreEqual("Closest pass 0 m", LevelResult.ClosestPassText(-0.1f));
            Assert.AreEqual("No close passes", LevelResult.ClosestPassText(float.PositiveInfinity));
        }

        [Test]
        public void Session_TracksWarningsAndClosestGap_AndRestartClearsThem()
        {
            var session = new LevelSession(8);
            session.RecordWarning();
            session.RecordSeparation(2f);
            session.RecordSeparation(0.6f);
            session.RecordSeparation(1f);

            Assert.AreEqual(1, session.WarningCount);
            Assert.AreEqual(0.6f, session.ClosestGap, 1e-6f);

            session.Restart();
            Assert.AreEqual(0, session.WarningCount);
            Assert.AreEqual(float.PositiveInfinity, session.ClosestGap);
        }

        [Test]
        public void Session_StopsRecordingOnceEnded()
        {
            var session = new LevelSession(1);
            session.RecordLanding(); // cleared
            session.RecordWarning();
            session.RecordSeparation(0.1f);

            Assert.AreEqual(0, session.WarningCount);
            Assert.AreEqual(float.PositiveInfinity, session.ClosestGap);
        }
    }

    public class ToneSynthTests
    {
        [Test]
        public void Tone_HasTheRightLength_AndStaysInRange()
        {
            float[] samples = ToneSynth.Tone(880f, 0.1f, 0.5f);
            Assert.AreEqual(Mathf.RoundToInt(0.1f * ToneSynth.SampleRate), samples.Length);
            foreach (float s in samples)
                Assert.LessOrEqual(Mathf.Abs(s), 0.5f + 1e-5f);
        }

        [Test]
        public void Tone_StartsSilently_SoItDoesNotClick()
        {
            float[] samples = ToneSynth.Tone(1000f, 0.1f, 1f);
            Assert.AreEqual(0f, samples[0], 1e-6f);
            Assert.Less(Mathf.Abs(samples[10]), 0.1f);
        }

        [Test]
        public void Sequence_PlaysTheSecondAfterTheGap_WithoutClipping()
        {
            float[] a = ToneSynth.Tone(1000f, 0.2f, 0.8f, 1f);
            float[] b = ToneSynth.Tone(1500f, 0.2f, 0.8f, 1f);
            float[] both = ToneSynth.Sequence(a, b, 0.1f);

            Assert.AreEqual(Mathf.RoundToInt(0.1f * ToneSynth.SampleRate) + b.Length, both.Length);
            foreach (float s in both)
                Assert.LessOrEqual(Mathf.Abs(s), 1f);
        }

        [Test]
        public void Crash_IsTheSameEveryTime_AndFadesOut()
        {
            float[] first = ToneSynth.Crash(0.5f, 0.8f);
            float[] second = ToneSynth.Crash(0.5f, 0.8f);
            CollectionAssert.AreEqual(first, second);

            float early = 0f, late = 0f;
            for (int i = 0; i < 2000; i++) early = Mathf.Max(early, Mathf.Abs(first[i]));
            for (int i = first.Length - 2000; i < first.Length; i++) late = Mathf.Max(late, Mathf.Abs(first[i]));
            Assert.Less(late, early);
        }
    }
}
