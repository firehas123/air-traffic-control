namespace AirTrafficControl
{
    /// <summary>
    /// Decides how fast game time runs (Time.timeScale). Plain C#, no Unity components, so it can be unit tested.
    ///
    /// - Speed: 1x is normal. 2x is fast-forward, the speed the prototype ran at before 15 Nov 2026
    ///   (owner decision: the old speed became 2x and half of it became normal).
    /// - Paused: the game is frozen, for example after a collision or when the level is cleared.
    /// - Slow draw (C4): while the player is drawing an aircraft's line, time runs at
    ///   <see cref="DrawingSlowFactor"/> of the chosen speed. A switch turns it off so both versions can be compared.
    /// </summary>
    public sealed class GameClock
    {
        public const float NormalSpeed = 1f;
        public const float FastSpeed = 2f;

        /// <summary>Time runs at this fraction of the chosen speed while the player draws (C4).</summary>
        public const float DrawingSlowFactor = 0.4f;

        /// <summary>True for 2x.</summary>
        public bool Fast { get; set; }

        public bool Paused { get; set; }

        /// <summary>The C4 switch: whether drawing slows time. On at start.</summary>
        public bool SlowWhileDrawing { get; set; } = true;

        /// <summary>True while the player is drawing an aircraft's line.</summary>
        public bool Drawing { get; set; }

        public float SpeedMultiplier => Fast ? FastSpeed : NormalSpeed;

        public bool IsSlowedForDrawing => SlowWhileDrawing && Drawing;

        /// <summary>The value for Time.timeScale.</summary>
        public float TimeScale => Paused ? 0f : SpeedMultiplier * (IsSlowedForDrawing ? DrawingSlowFactor : 1f);

        /// <summary>Label for the slow-draw button.</summary>
        public string SlowDrawLabel => SlowWhileDrawing ? "Slow draw on" : "Slow draw off";

        /// <summary>Tower mode's label for the same switch: there is no drawing, time slows while the buttons are open.</summary>
        public string SlowTapsLabel => SlowWhileDrawing ? "Slow time on" : "Slow time off";

        /// <summary>Label for the speed button, for example "Speed 1x".</summary>
        public string SpeedLabel => Fast ? "Speed 2x" : "Speed 1x";
    }
}
