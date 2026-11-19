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
        public LevelSession(int landingsToClear = 0, int departuresToClear = 0)
        {
            LandingsToClear = Mathf.Max(0, landingsToClear);
            DeparturesToClear = Mathf.Max(0, departuresToClear);
        }

        /// <summary>Landings needed to clear the level. 0 means no landing target.</summary>
        public int LandingsToClear { get; }

        /// <summary>Departures needed to clear the level (M3, blueprint 15.4). 0 means none are needed.</summary>
        public int DeparturesToClear { get; }

        public int DepartureCount { get; private set; }

        bool HasTarget => LandingsToClear > 0 || DeparturesToClear > 0;

        bool TargetReached => LandedCount >= LandingsToClear && DepartureCount >= DeparturesToClear;

        public SessionState State { get; private set; } = SessionState.Playing;
        public int LandedCount { get; private set; }
        public float ElapsedSeconds { get; private set; }

        /// <summary>Danger warnings started during this attempt (for the 3-star rule).</summary>
        public int WarningCount { get; private set; }

        /// <summary>The smallest edge-to-edge gap between two aircraft during this attempt (infinity if none).</summary>
        public float ClosestGap { get; private set; } = float.PositiveInfinity;

        public void RecordWarning()
        {
            if (IsPlaying)
                WarningCount++;
        }

        public void RecordSeparation(float gap)
        {
            if (IsPlaying && gap < ClosestGap)
                ClosestGap = gap;
        }

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
            return CheckCleared();
        }

        /// <summary>
        /// Counts a departure (an aircraft that took off and left the screen). Returns true for the one that completes
        /// the level's goal and clears it.
        /// </summary>
        public bool RecordDeparture()
        {
            if (!IsPlaying)
                return false;
            DepartureCount++;
            return CheckCleared();
        }

        bool CheckCleared()
        {
            if (HasTarget && TargetReached)
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
            DepartureCount = 0;
            ElapsedSeconds = 0f;
            WarningCount = 0;
            ClosestGap = float.PositiveInfinity;
        }

        /// <summary>Formats seconds as m:ss, for example 72.4 -> "1:12".</summary>
        public static string FormatTime(float seconds)
        {
            int whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{whole / 60}:{whole % 60:00}";
        }
    }
}
