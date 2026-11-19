namespace AirTrafficControl
{
    /// <summary>What happens to one aircraft's landing when the wind reverses the runway.</summary>
    public enum ClearanceAfterWindChange
    {
        /// <summary>Its line does not land: yellow ring, it needs a new line.</summary>
        NotCleared,

        /// <summary>It was cleared and is close enough to touchdown to keep landing at the old end (grace period).</summary>
        KeepsLandingOnOldZone,

        /// <summary>Its line ends in the new touchdown zone, so it is cleared for that.</summary>
        ClearedForNewZone,
    }

    /// <summary>
    /// The wind grace period (blueprint 14.3, AGREED 19 Nov 2026): an aircraft that would reach touchdown within
    /// the grace time when the wind changes still lands where its line ends. Time is game time, so at 2x speed
    /// the grace lasts half as long in real seconds. Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public static class WindGrace
    {
        public const float DefaultGraceSeconds = 1.5f;

        /// <param name="wasCleared">The aircraft was cleared to land just before the change.</param>
        /// <param name="secondsToTouchdown">Game seconds until it reaches the end of its line.</param>
        /// <param name="landsAfterChange">Its line ends in a touchdown zone that is active after the change.</param>
        /// <param name="graceSeconds">The level's grace period.</param>
        public static ClearanceAfterWindChange Decide(bool wasCleared, float secondsToTouchdown, bool landsAfterChange, float graceSeconds)
        {
            if (landsAfterChange)
                return ClearanceAfterWindChange.ClearedForNewZone;
            if (wasCleared && secondsToTouchdown <= graceSeconds)
                return ClearanceAfterWindChange.KeepsLandingOnOldZone;
            return ClearanceAfterWindChange.NotCleared;
        }
    }
}
