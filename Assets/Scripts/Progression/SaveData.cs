using System;
using System.Collections.Generic;

namespace AirTrafficControl
{
    /// <summary>
    /// What ProgressionSave keeps on the device (M3 step 1): coins, XP, unlocked levels, and per level the stars and
    /// best time. Version 2 (25 Nov 2026) added XP and level ids per map and mode (island-draw-01). Saved as JSON with a version number, so later updates can read older saves (blueprint 5 and 15.1).
    /// All saving is local and works offline. Plain C#, so it can be unit tested.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>
        /// The save format this build writes. Raise it when the format changes, and add a step to
        /// <see cref="ProgressionSave.Migrate"/> that brings older saves up to date.
        /// </summary>
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public int coins;

        /// <summary>Total XP (version 2, blueprint 16.3). Drives the controller rank (C8) and opens maps.</summary>
        public int xp;

        /// <summary>Safe landings and departures over all cleared levels (version 2).</summary>
        public int totalLandings;
        public int totalDepartures;

        /// <summary>Level ids the player may play. The first level of a map is always playable as well.</summary>
        public List<string> unlocked = new List<string>();

        public List<LevelProgress> levels = new List<LevelProgress>();

        public LevelProgress Find(string levelId)
        {
            foreach (LevelProgress level in levels)
                if (level.id == levelId)
                    return level;
            return null;
        }

        public LevelProgress FindOrAdd(string levelId)
        {
            LevelProgress level = Find(levelId);
            if (level == null)
            {
                level = new LevelProgress { id = levelId };
                levels.Add(level);
            }
            return level;
        }
    }

    [Serializable]
    public sealed class LevelProgress
    {
        public string id;

        /// <summary>Best stars earned (0 to 3). Stars pay coins only the first time they are earned (C9).</summary>
        public int stars;

        /// <summary>Best time to clear, in game seconds; below 0 when never cleared.</summary>
        public float bestSeconds = -1f;

        public int timesCleared;

        public bool Cleared => timesCleared > 0;
    }
}
