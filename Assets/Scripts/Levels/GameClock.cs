namespace AirTrafficControl
{
    /// <summary>
    /// Decides how fast game time runs (Time.timeScale). Plain C#, no Unity components, so it can be unit tested.
    ///
    /// - Speed: 1x is normal. 2x is fast-forward, the speed the prototype ran at before 15 Nov 2026
    ///   (owner decision: the old speed became 2x and half of it became normal).
    /// - Paused: the game is frozen, for example after a collision.
    /// - Slow time while drawing (C4, M1 step 6) will be added here as one more factor.
    /// </summary>
    public sealed class GameClock
    {
        public const float NormalSpeed = 1f;
        public const float FastSpeed = 2f;

        /// <summary>True for 2x.</summary>
        public bool Fast { get; set; }

        public bool Paused { get; set; }

        public float SpeedMultiplier => Fast ? FastSpeed : NormalSpeed;

        /// <summary>The value for Time.timeScale.</summary>
        public float TimeScale => Paused ? 0f : SpeedMultiplier;

        /// <summary>Label for the speed button, for example "Speed 1x".</summary>
        public string SpeedLabel => Fast ? "Speed 2x" : "Speed 1x";
    }
}
