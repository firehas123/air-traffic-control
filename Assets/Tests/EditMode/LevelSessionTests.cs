using NUnit.Framework;

namespace AirTrafficControl.Tests
{
    public class LevelSessionTests
    {
        [Test]
        public void NewSession_IsPlayingWithNothingCounted()
        {
            var session = new LevelSession();

            Assert.IsTrue(session.IsPlaying);
            Assert.AreEqual(0, session.LandedCount);
            Assert.AreEqual(0f, session.ElapsedSeconds);
        }

        [Test]
        public void Landings_AreCounted()
        {
            var session = new LevelSession();
            session.RecordLanding();
            session.RecordLanding();

            Assert.AreEqual(2, session.LandedCount);
        }

        [Test]
        public void Time_AddsUpWhilePlaying()
        {
            var session = new LevelSession();
            session.Tick(0.5f);
            session.Tick(0.25f);
            session.Tick(-1f); // ignored

            Assert.AreEqual(0.75f, session.ElapsedSeconds, 1e-5f);
        }

        [Test]
        public void FirstCollision_FailsTheSession_LaterOnesAreIgnored()
        {
            var session = new LevelSession();

            Assert.IsTrue(session.RecordCollision());
            Assert.AreEqual(SessionState.Failed, session.State);
            Assert.IsFalse(session.RecordCollision(), "Handled once");
        }

        [Test]
        public void AfterFailing_LandingsAndTimeStop()
        {
            var session = new LevelSession();
            session.RecordLanding();
            session.Tick(10f);
            session.RecordCollision();

            session.RecordLanding();
            session.Tick(5f);

            Assert.AreEqual(1, session.LandedCount);
            Assert.AreEqual(10f, session.ElapsedSeconds, 1e-5f);
        }

        [Test]
        public void Restart_ClearsEverything()
        {
            var session = new LevelSession();
            session.RecordLanding();
            session.Tick(30f);
            session.RecordCollision();

            session.Restart();

            Assert.IsTrue(session.IsPlaying);
            Assert.AreEqual(0, session.LandedCount);
            Assert.AreEqual(0f, session.ElapsedSeconds);
        }

        [Test]
        public void ReachingTheLandingTarget_ClearsTheLevel()
        {
            var session = new LevelSession(3);

            Assert.IsFalse(session.RecordLanding());
            Assert.IsFalse(session.RecordLanding());
            Assert.IsTrue(session.RecordLanding(), "The third landing clears it");
            Assert.AreEqual(SessionState.Cleared, session.State);
            Assert.IsFalse(session.IsPlaying);
        }

        [Test]
        public void AfterClearing_NothingMoreCounts_AndACollisionDoesNotFailIt()
        {
            var session = new LevelSession(1);
            session.RecordLanding();
            session.Tick(4f);

            Assert.IsFalse(session.RecordLanding());
            Assert.IsFalse(session.RecordCollision());
            Assert.AreEqual(SessionState.Cleared, session.State);
            Assert.AreEqual(1, session.LandedCount);
            Assert.AreEqual(0f, session.ElapsedSeconds, "Time stopped when it was cleared");
        }

        [Test]
        public void NoTarget_NeverClears()
        {
            var session = new LevelSession(0);
            for (int i = 0; i < 50; i++)
                Assert.IsFalse(session.RecordLanding());
            Assert.IsTrue(session.IsPlaying);
        }

        [Test]
        public void Restart_KeepsTheTarget()
        {
            var session = new LevelSession(8);
            session.RecordCollision();
            session.Restart();

            Assert.AreEqual(8, session.LandingsToClear);
            Assert.IsTrue(session.IsPlaying);
        }

        [TestCase(0f, "0:00")]
        [TestCase(9.9f, "0:09")]
        [TestCase(72.4f, "1:12")]
        [TestCase(600f, "10:00")]
        [TestCase(-3f, "0:00")]
        public void FormatTime_ShowsMinutesAndSeconds(float seconds, string expected)
        {
            Assert.AreEqual(expected, LevelSession.FormatTime(seconds));
        }
    }
}
