using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The seven maps in unlock order (blueprint 16.2): Island, City, Desert, Mountain Valley, Arctic, Military Base,
    /// Spaceport. Each map is one airport shown at two zoom levels, with a Draw picture and a Tower picture and the
    /// measured layout of each. Levels are listed per mode. One asset: Assets/Maps/MapCatalog.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Air Traffic Control/Map Catalog", fileName = "MapCatalog")]
    public sealed class MapCatalog : ScriptableObject
    {
        public List<MapEntry> maps = new List<MapEntry>();

        [Tooltip("XP rules (blueprint 16.3, PROPOSED values).")]
        public XpRules xp = new XpRules();

        public MapEntry Find(string id) => maps.Find(m => m.id == id);

        /// <summary>The plain description the progression rules work on.</summary>
        public List<MapInfo> ToInfo()
        {
            var list = new List<MapInfo>();
            foreach (MapEntry map in maps)
                list.Add(map.ToInfo());
            return list;
        }
    }

    [Serializable]
    public sealed class MapEntry
    {
        [Tooltip("Stable id used in level ids and the save, for example island. Never change it once released.")]
        public string id;
        public string displayName;

        [Tooltip("Total XP needed to open this map (with level 5 of the map before it cleared). 0 for the first map.")]
        [Min(0)] public int xpToUnlock;

        [Tooltip("Off until the map's gameplay is built: the home screen shows Coming soon and the map stays locked.")]
        public bool gameplayReady = true;

        [Tooltip("The map's rule in one line, for the home screen.")]
        public string ruleText;

        public Sprite drawPicture;
        public Sprite towerPicture;
        public PictureLayout drawLayout;
        public PictureLayout towerLayout;

        public List<LevelEntry> drawLevels = new List<LevelEntry>();
        public List<LevelEntry> towerLevels = new List<LevelEntry>();

        public List<LevelEntry> Levels(GameMode mode) => mode == GameMode.Tower ? towerLevels : drawLevels;

        public MapInfo ToInfo()
        {
            var info = new MapInfo { id = id, name = displayName, xpToUnlock = xpToUnlock, gameplayReady = gameplayReady };
            foreach (LevelEntry level in drawLevels)
                info.drawLevelIds.Add(level.id);
            foreach (LevelEntry level in towerLevels)
                info.towerLevelIds.Add(level.id);
            return info;
        }
    }
}
