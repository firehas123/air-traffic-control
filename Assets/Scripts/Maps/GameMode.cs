namespace AirTrafficControl
{
    /// <summary>
    /// The two game modes (blueprint 16.1, AGREED 25 Nov 2026). Draw: zoomed-out sky view, the player draws lines.
    /// Tower: zoomed-in airport close-up, the player taps aircraft for commands. Each map has one picture per mode.
    /// </summary>
    public enum GameMode
    {
        Draw = 0,
        Tower = 1,
    }

    public static class GameModes
    {
        public static readonly GameMode[] All = { GameMode.Draw, GameMode.Tower };

        /// <summary>"draw" or "tower", used in level ids such as island-draw-01.</summary>
        public static string Key(this GameMode mode) => mode == GameMode.Tower ? "tower" : "draw";
    }
}
