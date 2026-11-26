using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>What clearing a level paid out, for the result screen.</summary>
    public readonly struct LevelRewards
    {
        public LevelRewards(int coinsEarned, int newStars, bool newBestTime, bool unlockedNext, int previousStars, int xpEarned = 0)
        {
            CoinsEarned = coinsEarned;
            NewStars = newStars;
            NewBestTime = newBestTime;
            UnlockedNext = unlockedNext;
            PreviousStars = previousStars;
            XpEarned = xpEarned;
        }

        public int CoinsEarned { get; }

        /// <summary>Stars earned for the first time on this clear.</summary>
        public int NewStars { get; }

        public bool NewBestTime { get; }
        public bool UnlockedNext { get; }
        public int PreviousStars { get; }
        public int XpEarned { get; }
    }

    /// <summary>
    /// The progression rules over a <see cref="SaveData"/> (M3 step 1, blueprint 16.3, C9, C11). Plain C#, no Unity
    /// components, so it can be unit tested.
    /// - Levels: inside a map and mode, the first level is open once the map is open, and a level opens when the one
    ///   before it is cleared, with any number of stars (C11).
    /// - Maps: the first map (Island, both modes) is open from the start. The next map opens when level 5 of the map
    ///   before it is cleared in either mode AND the total XP reaches the map's value. A map whose gameplay is not
    ///   built yet stays locked (Coming soon).
    /// - Coins (C9): every clear pays per landing and per departure; stars pay only the first time each is earned.
    /// - XP: per landing, per departure, per first-time star and for a level with no danger warning (<see cref="XpRules"/>).
    /// </summary>
    public sealed class Progression
    {
        /// <summary>The level whose clear (in either mode) opens the next map.</summary>
        public const int LevelThatOpensNextMap = 5;

        readonly SaveData data;
        readonly List<MapInfo> maps;
        readonly XpRules xpRules;

        public Progression(SaveData data, IEnumerable<MapInfo> maps, XpRules xpRules = null)
        {
            this.data = data ?? new SaveData();
            this.maps = new List<MapInfo>(maps);
            this.xpRules = xpRules ?? new XpRules();
        }

        public SaveData Data => data;
        public int Coins => data.coins;
        public int Xp => data.xp;
        public IReadOnlyList<MapInfo> Maps => maps;

        public int MapIndex(string mapId) => maps.FindIndex(m => m.id == mapId);

        // ---- Maps ----

        public bool IsMapUnlocked(int mapIndex) => MapLockFor(mapIndex).Unlocked;

        public bool IsMapUnlocked(string mapId) => IsMapUnlocked(MapIndex(mapId));

        public MapLock MapLockFor(int mapIndex)
        {
            if (mapIndex < 0 || mapIndex >= maps.Count)
                return new MapLock(false, false, "", false, 0);
            MapInfo map = maps[mapIndex];
            if (!map.gameplayReady)
                return new MapLock(false, true, "", false, 0);
            if (mapIndex == 0)
                return new MapLock(true, false, "", false, 0);

            MapInfo previous = maps[mapIndex - 1];
            bool previousCleared = IsMapUnlocked(mapIndex - 1) && ClearedTheLevelThatOpensNextMap(previous);
            int xpMissing = Mathf.Max(0, map.xpToUnlock - data.xp);
            bool unlocked = previousCleared && xpMissing == 0;
            return new MapLock(unlocked, false, previous.name, !previousCleared, xpMissing);
        }

        bool ClearedTheLevelThatOpensNextMap(MapInfo map)
        {
            foreach (GameMode mode in GameModes.All)
            {
                List<string> ids = map.LevelIds(mode);
                if (ids.Count == 0)
                    continue;
                string id = ids[Mathf.Min(LevelThatOpensNextMap, ids.Count) - 1];
                if (IsCleared(id))
                    return true;
            }
            return false;
        }

        // ---- Levels ----

        public bool IsCleared(string levelId) => data.Find(levelId)?.Cleared ?? false;

        public bool IsLevelUnlocked(int mapIndex, GameMode mode, int levelIndex)
        {
            if (!IsMapUnlocked(mapIndex))
                return false;
            List<string> ids = maps[mapIndex].LevelIds(mode);
            if (levelIndex < 0 || levelIndex >= ids.Count)
                return false;
            if (levelIndex == 0)
                return true;
            // Unlocked by clearing the level before it (keeps saves consistent if the order changes), or recorded.
            return IsCleared(ids[levelIndex - 1]) || data.unlocked.Contains(ids[levelIndex]);
        }

        public bool IsLevelUnlocked(string levelId)
        {
            if (!TryFind(levelId, out int mapIndex, out GameMode mode, out int levelIndex))
                return false;
            return IsLevelUnlocked(mapIndex, mode, levelIndex);
        }

        public bool TryFind(string levelId, out int mapIndex, out GameMode mode, out int levelIndex)
        {
            for (mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                foreach (GameMode m in GameModes.All)
                {
                    levelIndex = maps[mapIndex].LevelIds(m).IndexOf(levelId);
                    if (levelIndex >= 0)
                    {
                        mode = m;
                        return true;
                    }
                }
            }
            mapIndex = -1;
            mode = GameMode.Draw;
            levelIndex = -1;
            return false;
        }

        public int StarsFor(string levelId) => data.Find(levelId)?.stars ?? 0;

        /// <summary>Best time in seconds, or a negative number when never cleared.</summary>
        public float BestSecondsFor(string levelId) => data.Find(levelId)?.bestSeconds ?? -1f;

        public int TotalStars
        {
            get
            {
                int total = 0;
                foreach (LevelProgress level in data.levels)
                    total += level.stars;
                return total;
            }
        }

        /// <summary>Records a cleared level and pays its coins and XP. Returns what it paid, for the result screen.</summary>
        public LevelRewards RecordClear(string levelId, int stars, float seconds, int landings, int departures, int warnings, ResultSettings rules)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            LevelProgress level = data.FindOrAdd(levelId);
            int previousStars = level.stars;

            int newStars = Mathf.Max(0, stars - previousStars);
            int coins = Mathf.Max(0, landings) * rules.coinsPerLanding
                      + Mathf.Max(0, departures) * rules.coinsPerDeparture
                      + newStars * rules.coinsPerStar;
            data.coins += coins;

            int xp = xpRules.ForClear(landings, departures, newStars, warnings);
            data.xp += xp;
            data.totalLandings += Mathf.Max(0, landings);
            data.totalDepartures += Mathf.Max(0, departures);

            level.stars = Mathf.Max(level.stars, stars);
            level.timesCleared++;
            bool newBest = level.bestSeconds < 0f || seconds < level.bestSeconds;
            if (newBest)
                level.bestSeconds = seconds;

            bool unlockedNext = false;
            if (TryFind(levelId, out int mapIndex, out GameMode mode, out int levelIndex))
            {
                List<string> ids = maps[mapIndex].LevelIds(mode);
                if (levelIndex + 1 < ids.Count && !data.unlocked.Contains(ids[levelIndex + 1]))
                {
                    data.unlocked.Add(ids[levelIndex + 1]);
                    unlockedNext = true;
                }
            }

            return new LevelRewards(coins, newStars, newBest, unlockedNext, previousStars, xp);
        }
    }
}
