using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// Result-screen numbers (C10): stars, coins and the closest pass. Plain C#, no Unity components, so it can be
    /// unit tested.
    ///
    /// Stars (blueprint 3, PROPOSED, thresholds to be tuned by playing; they must never reward reckless flying):
    /// 1 star for clearing the level safely, 2 for clearing it within the level's two-star time, 3 for that and no
    /// danger warning during the whole level.
    /// Coins (C9): per landing plus per star. Shown now; saved from M3 (ProgressionSave). Until saving exists every
    /// star counts as earned for the first time.
    /// </summary>
    public static class LevelResult
    {
        /// <summary>World units to metres for the "closest pass" readout: the 0.7-unit airliner is about 70 m long.</summary>
        public const float MetresPerUnit = 100f;

        public static int Stars(bool cleared, float elapsedSeconds, int warningCount, ResultSettings settings)
        {
            if (!cleared)
                return 0;
            int stars = 1;
            if (elapsedSeconds <= settings.twoStarSeconds)
            {
                stars = 2;
                if (warningCount == 0 || !settings.threeStarsNeedNoWarnings)
                    stars = 3;
            }
            return stars;
        }

        public static int Coins(int landed, int stars, ResultSettings settings) =>
            Mathf.Max(0, landed) * settings.coinsPerLanding + Mathf.Clamp(stars, 0, 3) * settings.coinsPerStar;

        /// <summary>
        /// The smallest gap between two aircraft right now, edge to edge: centre distance minus both collision radii.
        /// Infinity with fewer than two aircraft.
        /// </summary>
        public static float ClosestGap(IReadOnlyList<Vector2> positions, IReadOnlyList<float> radii)
        {
            float closest = float.PositiveInfinity;
            for (int i = 0; i < positions.Count; i++)
                for (int j = i + 1; j < positions.Count; j++)
                    closest = Mathf.Min(closest, Vector2.Distance(positions[i], positions[j]) - radii[i] - radii[j]);
            return closest;
        }

        /// <summary>"Closest pass 42 m", or "No close passes" when two aircraft never shared the sky.</summary>
        public static string ClosestPassText(float closestGapUnits)
        {
            if (float.IsInfinity(closestGapUnits))
                return "No close passes";
            int metres = Mathf.Max(0, Mathf.RoundToInt(closestGapUnits * MetresPerUnit));
            return $"Closest pass {metres} m";
        }
    }
}

namespace AirTrafficControl
{
    /// <summary>How an attempt at a level ended, for saving and the result screen.</summary>
    public readonly struct LevelOutcome
    {
        public LevelOutcome(bool cleared, int stars, float seconds, int landed, int landingTarget,
                            int departed, int departureTarget, float closestGap)
        {
            Cleared = cleared;
            Stars = stars;
            Seconds = seconds;
            Landed = landed;
            LandingTarget = landingTarget;
            Departed = departed;
            DepartureTarget = departureTarget;
            ClosestGap = closestGap;
        }

        public bool Cleared { get; }
        public int Stars { get; }
        public float Seconds { get; }
        public int Landed { get; }
        public int LandingTarget { get; }
        public int Departed { get; }
        public int DepartureTarget { get; }
        public float ClosestGap { get; }
    }
}
