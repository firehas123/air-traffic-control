using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The levels of a map, in order (M3 step 1). Each entry has a stable id used by the save, so levels can be
    /// retuned or reordered later without losing the player's stars. Until M3 step 4 the ten Island entries reuse
    /// the test level's data.
    /// </summary>
    [CreateAssetMenu(menuName = "Air Traffic Control/Level Catalog", fileName = "LevelCatalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        public string mapName = "Island Airport";
        public List<LevelEntry> levels = new List<LevelEntry>();

        public IEnumerable<string> Ids
        {
            get
            {
                foreach (LevelEntry entry in levels)
                    yield return entry.id;
            }
        }
    }

    [Serializable]
    public sealed class LevelEntry
    {
        [Tooltip("Stable id used by the save, for example island-01. Never change it once released.")]
        public string id;

        public string title;
        public LevelData data;
    }
}
