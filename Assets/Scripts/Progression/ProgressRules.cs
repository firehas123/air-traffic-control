using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>A map as the progression rules see it: its id, unlock XP and level ids per mode. Plain C#.</summary>
    public sealed class MapInfo
    {
        public string id;
        public string name;
        public int xpToUnlock;
        public bool gameplayReady = true;
        public List<string> drawLevelIds = new List<string>();
        public List<string> towerLevelIds = new List<string>();

        public List<string> LevelIds(GameMode mode) => mode == GameMode.Tower ? towerLevelIds : drawLevelIds;

        /// <summary>Level ids follow one pattern: island-draw-01, island-tower-05.</summary>
        public static string LevelId(string mapId, GameMode mode, int number) => $"{mapId}-{mode.Key()}-{number:00}";
    }

    /// <summary>XP per action (blueprint 16.3, PROPOSED by Claude; tunable in the Inspector on the map catalog).</summary>
    [Serializable]
    public sealed class XpRules
    {
        [Min(0)] public int perLanding = 10;
        [Min(0)] public int perDeparture = 10;

        [Tooltip("Paid only the first time each star is earned: stars never pay twice (C9).")]
        [Min(0)] public int perNewStar = 25;

        [Tooltip("For clearing a level without a single danger warning.")]
        [Min(0)] public int noWarningBonus = 50;

        /// <summary>XP for one cleared level. Nothing for a level that was not cleared.</summary>
        public int ForClear(int landings, int departures, int newStars, int warnings) =>
            Mathf.Max(0, landings) * perLanding + Mathf.Max(0, departures) * perDeparture
            + Mathf.Max(0, newStars) * perNewStar + (warnings == 0 ? noWarningBonus : 0);
    }

    /// <summary>
    /// The controller rank on the home screen (C8: Trainee, Controller, Senior Controller, Tower Chief). Since
    /// 25 Nov 2026 XP drives it (blueprint 16.3). The XP each rank needs is PROPOSED by Claude: Controller after about
    /// the first map, Tower Chief around the last map's unlock.
    /// </summary>
    public static class ControllerRank
    {
        public static readonly string[] Names = { "Trainee", "Controller", "Senior Controller", "Tower Chief" };
        public static readonly int[] XpNeeded = { 0, 1000, 4000, 8500 };

        public static int Index(int xp)
        {
            int index = 0;
            for (int i = 0; i < XpNeeded.Length; i++)
                if (xp >= XpNeeded[i])
                    index = i;
            return index;
        }

        public static string Name(int xp) => Names[Index(xp)];

        /// <summary>Progress from this rank to the next, 0 to 1 (1 at the top rank).</summary>
        public static float ProgressToNext(int xp)
        {
            int index = Index(xp);
            if (index >= XpNeeded.Length - 1)
                return 1f;
            return Mathf.Clamp01((xp - XpNeeded[index]) / (float)(XpNeeded[index + 1] - XpNeeded[index]));
        }

        /// <summary>XP still needed for the next rank, or 0 at the top rank.</summary>
        public static int XpToNext(int xp)
        {
            int index = Index(xp);
            return index >= XpNeeded.Length - 1 ? 0 : XpNeeded[index + 1] - xp;
        }
    }

    /// <summary>Why a map is still locked, for the home screen.</summary>
    public readonly struct MapLock
    {
        public MapLock(bool unlocked, bool comingSoon, string previousMap, bool needsPreviousLevel, int xpMissing)
        {
            Unlocked = unlocked;
            ComingSoon = comingSoon;
            PreviousMap = previousMap;
            NeedsPreviousLevel = needsPreviousLevel;
            XpMissing = xpMissing;
        }

        public bool Unlocked { get; }

        /// <summary>The map's gameplay is not built yet (Military Base, Spaceport).</summary>
        public bool ComingSoon { get; }

        public string PreviousMap { get; }

        /// <summary>Level 5 of the map before it is not cleared yet, in either mode.</summary>
        public bool NeedsPreviousLevel { get; }

        public int XpMissing { get; }

        /// <summary>One line such as "Clear Island level 5 (Draw or Tower) and earn 350 more XP".</summary>
        public string Describe(int levelNumber)
        {
            if (Unlocked)
                return "";
            if (ComingSoon)
                return "Coming soon";
            var parts = new List<string>();
            if (NeedsPreviousLevel)
                parts.Add($"Clear {PreviousMap} level {levelNumber} (Draw or Tower)");
            if (XpMissing > 0)
                parts.Add(parts.Count == 0 ? $"Earn {XpMissing:N0} more XP" : $"earn {XpMissing:N0} more XP");
            if (parts.Count == 0)
                return $"Open {PreviousMap} first";
            return string.Join(" and ", parts);
        }
    }
}
