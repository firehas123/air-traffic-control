using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>Chooses where a new aircraft appears. Plain C#, no Unity components, so it can be unit tested.</summary>
    public static class SpawnRules
    {
        /// <summary>A spawn point counts as clear when no aircraft is closer than this.</summary>
        public const float MinClearDistance = 2.5f;

        /// <summary>
        /// Starting at <paramref name="firstIndex"/> and wrapping round, returns the first spawn point with no
        /// aircraft within <see cref="MinClearDistance"/>, or -1 if every point is busy.
        /// </summary>
        public static int ChooseClearPoint(IReadOnlyList<Vector2> spawnPositions, IReadOnlyList<Vector2> aircraftPositions, int firstIndex)
        {
            int count = spawnPositions.Count;
            for (int k = 0; k < count; k++)
            {
                int index = ((firstIndex % count) + count + k) % count;
                if (IsClear(spawnPositions[index], aircraftPositions))
                    return index;
            }
            return -1;
        }

        static bool IsClear(Vector2 point, IReadOnlyList<Vector2> aircraftPositions)
        {
            foreach (Vector2 aircraft in aircraftPositions)
            {
                if (Vector2.Distance(point, aircraft) < MinClearDistance)
                    return false;
            }
            return true;
        }
    }
}
