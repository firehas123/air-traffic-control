using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    public class FirstTimeHelpTests
    {
        [Test]
        public void NewPlayer_IsShownHowToDraw_UntilTheyDoIt()
        {
            var help = new FirstTimeHelp();
            help.Tick(0.1f, aircraftInTheAir: true);
            Assert.AreEqual(HelpHint.DrawToLandingZone, help.Current);

            help.Tick(30f, true);
            Assert.AreEqual(HelpHint.DrawToLandingZone, help.Current, "Stays until the player has done it");

            help.LineConnected();
            Assert.AreEqual(HelpHint.ClearedToLand, help.Current, "Then explains the green ring");
            help.Tick(FirstTimeHelp.ShortHintSeconds + 0.1f, true);
            Assert.AreEqual(HelpHint.None, help.Current);
        }

        [Test]
        public void LaterHints_WaitUntilTheDrawingLessonIsDone()
        {
            var help = new FirstTimeHelp();
            help.Tick(0.1f, true);
            help.WarningStarted();
            Assert.AreEqual(HelpHint.DrawToLandingZone, help.Current);
            Assert.IsFalse(help.HasSeen(HelpHint.DangerRing), "Kept for later");
        }

        [Test]
        public void EachLaterHint_ShowsOnce()
        {
            var help = new FirstTimeHelp();
            help.LineConnected();
            help.Tick(5f, true);

            help.WarningStarted();
            Assert.AreEqual(HelpHint.DangerRing, help.Current);
            help.Tick(FirstTimeHelp.LongHintSeconds + 0.1f, true);
            Assert.AreEqual(HelpHint.None, help.Current);

            help.WarningStarted();
            Assert.AreEqual(HelpHint.None, help.Current, "Not again");
        }

        [Test]
        public void AHintIsNotCutShortByAnother()
        {
            var help = new FirstTimeHelp();
            help.LineConnected();
            help.Tick(5f, true);
            help.WindChangeComing();
            help.CarrierReachable();

            Assert.AreEqual(HelpHint.WindChanging, help.Current);
            Assert.IsFalse(help.HasSeen(HelpHint.CarrierDeck), "Shown another time instead");
        }

        [Test]
        public void SavedProgress_IsRemembered_AndResetPlaysItAgain()
        {
            var first = new FirstTimeHelp();
            first.LineConnected();
            first.Tick(5f, true);
            first.WarningStarted();

            var later = new FirstTimeHelp(first.Seen);
            later.Tick(0.1f, true);
            Assert.AreEqual(HelpHint.None, later.Current, "A returning player is not taught to draw again");
            later.WarningStarted();
            Assert.AreEqual(HelpHint.None, later.Current);

            later.Reset();
            later.Tick(0.1f, true);
            Assert.AreEqual(HelpHint.DrawToLandingZone, later.Current);
        }

        [Test]
        public void EveryHint_IsOneShortLine()
        {
            foreach (HelpHint hint in System.Enum.GetValues(typeof(HelpHint)))
            {
                if (hint == HelpHint.None) continue;
                string text = FirstTimeHelp.Text(hint);
                Assert.IsNotNull(text, hint.ToString());
                Assert.LessOrEqual(text.Length, 55, $"'{text}' should be short");
                Assert.IsFalse(text.Contains("\n"));
            }
        }
    }

    public class SessionLengthTests
    {
        [Test]
        public void MoreAircraftInTheAir_ClearsFaster()
        {
            LevelData level = ScriptableObject.CreateInstance<LevelData>();
            var airliner = ScriptableObject.CreateInstance<AircraftType>();
            airliner.SetDefaults(AircraftSilhouette.Airliner);
            level.aircraftTypes.Add(airliner);

            level.aircraftInAir = 1;
            float one = SessionLengthEstimate.Seconds(level, 0.6f);
            level.aircraftInAir = 2;
            float two = SessionLengthEstimate.Seconds(level, 0.6f);

            Assert.AreEqual(one / 2f, two, 1e-3f);
            Object.DestroyImmediate(airliner);
            Object.DestroyImmediate(level);
        }
    }
}
