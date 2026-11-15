using UnityEngine;

namespace AirTrafficControl
{
    public enum SessionState
    {
        Playing,
        Failed,
        Cleared,
    }

    /// <summary>
    /// The score and state of one attempt at a level: landings so far, time played, and whether it has ended,
    /// failed by a collision or cleared by reaching the landing target (objective type "land a set number of
    /// aircraft", C2). Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public sealed class LevelSession
    {
        public LevelSession(int landingsToClear = 0)
        {
            LandingsToClear = Mathf.Max(0, landingsToClear);
        }

        /// <summary>Landings needed to clear the level. 0 means no target: the level only ends on a collision.</summary>
        public int LandingsToClear { get; }

        public SessionState State { get; private set; } = SessionState.Playing;
        public int LandedCount { get; private set; }
        public float ElapsedSeconds { get; private set; }

        public bool IsPlaying => State == SessionState.Playing;

        public void Tick(float deltaTime)
        {
            if (IsPlaying && deltaTime > 0f)
                ElapsedSeconds += deltaTime;
        }

        /// <summary>
        /// Counts a landing. Ignored once the session has ended. Returns true for the landing that reaches the
        /// target and clears the level.
        /// </summary>
        public bool RecordLanding()
        {
            if (!IsPlaying)
                return false;
            LandedCount++;
            if (LandingsToClear > 0 && LandedCount >= LandingsToClear)
            {
                State = SessionState.Cleared;
                return true;
            }
            return false;
        }

        /// <summary>Ends the session. Returns true only for the first collision, so the failure is handled once.</summary>
        public bool RecordCollision()
        {
            if (!IsPlaying)
                return false;
            State = SessionState.Failed;
            return true;
        }

        /// <summary>Starts a fresh attempt: no landings, no time, playing. The target stays the same.</summary>
        public void Restart()
        {
            State = SessionState.Playing;
            LandedCount = 0;
            ElapsedSeconds = 0f;
        }

        /// <summary>Formats seconds as m:ss, for example 72.4 -> "1:12".</summary>
        public static string FormatTime(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{whole / 60}:{whole % 60:00}";
        }
    }
}
