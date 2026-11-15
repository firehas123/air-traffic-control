using NUnit.Framework;

namespace AirTrafficControl.Tests
{
    public class GameClockTests
    {
        [Test]
        public void StartsAtNormalSpeed()
        {
            var clock = new GameClock();

            Assert.AreEqual(1f, clock.TimeScale);
            Assert.AreEqual("Speed 1x", clock.SpeedLabel);
        }

        [Test]
        public void Fast_IsTwiceNormal()
        {
            var clock = new GameClock { Fast = true };

            Assert.AreEqual(2f, clock.TimeScale);
            Assert.AreEqual("Speed 2x", clock.SpeedLabel);
        }

        [Test]
        public void Paused_StopsTime_AndKeepsTheSpeedSetting()
        {
            var clock = new GameClock { Fast = true, Paused = true };
            Assert.AreEqual(0f, clock.TimeScale);

            clock.Paused = false;
            Assert.AreEqual(2f, clock.TimeScale);
        }
    }

    public class SlowDrawTests
    {
        [Test]
        public void Drawing_SlowsTimeToTheSlowFactor()
        {
            var clock = new GameClock { Drawing = true };
            Assert.AreEqual(GameClock.DrawingSlowFactor, clock.TimeScale, 1e-6f);
            Assert.IsTrue(clock.IsSlowedForDrawing);
        }

        [Test]
        public void Drawing_AtFastSpeed_SlowsFromFast()
        {
            var clock = new GameClock { Fast = true, Drawing = true };
            Assert.AreEqual(GameClock.FastSpeed * GameClock.DrawingSlowFactor, clock.TimeScale, 1e-6f);
        }

        [Test]
        public void SwitchOff_DrawingDoesNotSlowTime()
        {
            var clock = new GameClock { SlowWhileDrawing = false, Drawing = true };
            Assert.AreEqual(1f, clock.TimeScale);
            Assert.AreEqual("Slow draw off", clock.SlowDrawLabel);
        }

        [Test]
        public void SwitchIsOnAtStart()
        {
            Assert.IsTrue(new GameClock().SlowWhileDrawing);
            Assert.AreEqual("Slow draw on", new GameClock().SlowDrawLabel);
        }

        [Test]
        public void Paused_BeatsDrawing()
        {
            var clock = new GameClock { Drawing = true, Paused = true };
            Assert.AreEqual(0f, clock.TimeScale);
        }
    }

    public class WindScheduleTests
    {
        [Test]
        public void StartsInTheNormalDirection_WithAnIntervalInRange()
        {
            var wind = new WindSchedule(25f, 40f, 5f, 1);

            Assert.IsFalse(wind.Reversed);
            Assert.That(wind.SecondsUntilChange, Is.InRange(25f, 40f));
            Assert.IsFalse(wind.IsWarning);
        }

        [Test]
        public void Reverses_WhenTheIntervalRunsOut_ThenSchedulesTheNext()
        {
            var wind = new WindSchedule(10f, 10f, 3f, 1);

            Assert.IsFalse(wind.Tick(9.9f));
            Assert.IsTrue(wind.Tick(0.2f), "Changes at 10 s");
            Assert.IsTrue(wind.Reversed);
            Assert.AreEqual(9.9f, wind.SecondsUntilChange, 1e-3f, "Next change 10 s after the last");

            Assert.IsTrue(wind.Tick(10f));
            Assert.IsFalse(wind.Reversed, "And back again");
        }

        [Test]
        public void Warning_RunsForTheLastSecondsBeforeAChange()
        {
            var wind = new WindSchedule(10f, 10f, 3f, 1);

            wind.Tick(6.5f);
            Assert.IsFalse(wind.IsWarning);

            wind.Tick(1f); // 2.5 s left
            Assert.IsTrue(wind.IsWarning);
            Assert.AreEqual(3, wind.WholeSecondsUntilChange);
        }

        [Test]
        public void Reset_GoesBackToNormal()
        {
            var wind = new WindSchedule(10f, 10f, 3f, 1);
            wind.Tick(10.5f);
            Assert.IsTrue(wind.Reversed);

            wind.Reset();

            Assert.IsFalse(wind.Reversed);
            Assert.AreEqual(10f, wind.SecondsUntilChange, 1e-3f);
        }

        [Test]
        public void SameSeed_GivesTheSameChanges()
        {
            var a = new WindSchedule(25f, 40f, 5f, 42);
            var b = new WindSchedule(25f, 40f, 5f, 42);

            Assert.AreEqual(a.SecondsUntilChange, b.SecondsUntilChange);
            a.Tick(100f);
            b.Tick(100f);
            Assert.AreEqual(a.SecondsUntilChange, b.SecondsUntilChange);
        }

        [Test]
        public void IgnoresZeroOrNegativeTime()
        {
            var wind = new WindSchedule(10f, 10f, 3f, 1);
            Assert.IsFalse(wind.Tick(0f));
            Assert.IsFalse(wind.Tick(-5f));
            Assert.AreEqual(10f, wind.SecondsUntilChange, 1e-3f);
        }
    }
}
