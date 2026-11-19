using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>What clearing a level paid out, for the result screen.</summary>
    public readonly struct LevelRewards
    {
        public LevelRewards(int coinsEarned, int newStars, bool newBestTime, bool unlockedNext, int previousStars)
        {
            CoinsEarned = coinsEarned;
            NewStars = newStars;
            NewBestTime = newBestTime;
            UnlockedNext = unlockedNext;
            PreviousStars = previousStars;
        }

        public int CoinsEarned { get; }

        /// <summary>Stars earned for the first time on this clear.</summary>
        public int NewStars { get; }

        public bool NewBestTime { get; }
        public bool UnlockedNext { get; }
        public int PreviousStars { get; }
    }

    /// <summary>
    /// The progression rules over a <see cref="SaveData"/> (M3 step 1, C9, C11):
    /// - The first level is always unlocked; clearing a level unlocks the next one.
    /// - Coins: every clear pays per landing and per departure; stars pay only the first time each star is earned.
    /// - Best stars and best time are kept.
    /// Stars never block the campaign: a level is unlocked by clearing the one before it, with any number of stars
    /// (C11). Plain C#, no Unity components, so it can be unit tested.
    /// </summary>
    public sealed class Progression
    {
        readonly SaveData data;
        readonly List<string> levelOrder;

        public Progression(SaveData data, IEnumerable<string> levelIds)
        {
            this.data = data ?? new SaveData();
            levelOrder = new List<string>(levelIds);
        }

        public SaveData Data => data;
        public int Coins => data.coins;
        public IReadOnlyList<string> LevelOrder => levelOrder;

        public int IndexOf(string levelId) => levelOrder.IndexOf(levelId);

        public bool IsUnlocked(int index)
        {
            if (index < 0 || index >= levelOrder.Count)
                return false;
            if (index == 0)
                return true;
            string id = levelOrder[index];
            if (data.unlocked.Contains(id))
                return true;
            // Also unlocked if the level before it has been cleared (keeps saves consistent if the order changes).
            LevelProgress previous = data.Find(levelOrder[index - 1]);
            return previous != null && previous.Cleared;
        }

        public bool IsUnlocked(string levelId) => IsUnlocked(IndexOf(levelId));

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

        /// <summary>Records a cleared level and pays its coins. Returns what it paid, for the result screen.</summary>
        public LevelRewards RecordClear(string levelId, int stars, float seconds, int landings, int departures, ResultSettings rules)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            LevelProgress level = data.FindOrAdd(levelId);
            int previousStars = level.stars;

            int newStars = Mathf.Max(0, stars - previousStars);
            int coins = Mathf.Max(0, landings) * rules.coinsPerLanding
                      + Mathf.Max(0, departures) * rules.coinsPerDeparture
                      + newStars * rules.coinsPerStar;
            data.coins += coins;

            level.stars = Mathf.Max(level.stars, stars);
            level.timesCleared++;
            bool newBest = level.bestSeconds < 0f || seconds < level.bestSeconds;
            if (newBest)
                level.bestSeconds = seconds;

            bool unlockedNext = false;
            int index = IndexOf(levelId);
            if (index >= 0 && index + 1 < levelOrder.Count)
            {
                string next = levelOrder[index + 1];
                if (!data.unlocked.Contains(next))
                {
                    data.unlocked.Add(next);
                    unlockedNext = true;
                }
            }

            return new LevelRewards(coins, newStars, newBest, unlockedNext, previousStars);
        }
    }
}
