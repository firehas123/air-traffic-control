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
        /// Keeps a spawn point just inside the screen, so aircraft still enter from the screen edges on any screen
        /// shape (the camera crops the picture differently on wider screens).
        /// </summary>
        public static Vector2 ClampToView(Vector2 point, Rect view, float inset)
        {
            return new Vector2(Mathf.Clamp(point.x, view.xMin + inset, view.xMax - inset),
                               Mathf.Clamp(point.y, view.yMin + inset, view.yMax - inset));
        }

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
