// Writes the 25 Tower-mode level assets (blueprint 16, phase 3): 5 levels for each of Island, City, Desert, Mountain Valley
// and Arctic, on each map's Tower picture, and links them into Assets/Maps/MapCatalog.asset.
// The level assets under Assets/Levels/Tower are the data; this script only seeds them. Running it again keeps existing
// assets (values tuned in the Inspector survive) unless "Rebuild" is used, which overwrites them from the table below.
// Menu: Tools > Air Traffic Control > Build Tower Levels / Rebuild Tower Levels (overwrites)
// Command line: Unity -batchmode -nographics -quit -projectPath . -executeMethod TowerLevelsSetup.Rebuild -logFile -

using System.Collections.Generic;
using AirTrafficControl;
using UnityEditor;
using UnityEngine;

public static class TowerLevelsSetup
{
    const string Folder = "Assets/Levels/Tower";
    const string CatalogPath = "Assets/Maps/MapCatalog.asset";
    const string AircraftFolder = "Assets/Aircraft";

    // Aircraft kinds by letter: A airliner, J jumbo jet, H helicopter, B hot air balloon, D drone, F fighter jet.
    // A letter repeated makes that kind more common. Kinds arrive a few at a time (blueprint 14.4). Each level counts
    // landings and departures (land N, launch M). The map rule is on from level 2 (Desert: low fuel from 2, sandstorm
    // from 3). Island 1 teaches Go around and Island 2 teaches Hold (first-time help).
    struct Design
    {
        public string map;
        public int number;
        public int land;
        public int launch;
        public int inAir;
        public string kinds;
        public string title;
        public Design(string map, int number, int land, int launch, int inAir, string kinds, string title)
        {
            this.map = map; this.number = number; this.land = land; this.launch = launch; this.inAir = inAir; this.kinds = kinds; this.title = title;
        }
    }

    static readonly Design[] Designs =
    {
        new Design("island", 1, 5, 2, 2, "A", "Cleared to land"),
        new Design("island", 2, 6, 2, 2, "AAJ", "Holding pattern"),
        new Design("island", 3, 6, 3, 3, "AAJH", "Helicopter"),
        new Design("island", 4, 7, 3, 3, "AJHBD", "Balloon and drone"),
        new Design("island", 5, 8, 3, 3, "AJHBDF", "Island rush"),

        new Design("city", 1, 6, 2, 3, "AAJHD", "Three runways"),
        new Design("city", 2, 6, 3, 3, "AAJHD", "Runway closed"),
        new Design("city", 3, 7, 3, 3, "AJHBDF", "Crossings"),
        new Design("city", 4, 8, 3, 4, "AJJHBDF", "Closures and jumbos"),
        new Design("city", 5, 8, 4, 4, "AAJHBDFF", "City finale"),

        new Design("desert", 1, 6, 2, 3, "AAJHDF", "The triangle"),
        new Design("desert", 2, 6, 3, 3, "AAJHDF", "Low fuel"),
        new Design("desert", 3, 7, 3, 3, "AJHBDF", "Sandstorm"),
        new Design("desert", 4, 8, 3, 4, "AAJHBDF", "Storm season"),
        new Design("desert", 5, 8, 4, 4, "AAJHBDFF", "Desert finale"),

        new Design("mountain", 1, 5, 2, 2, "AAJHB", "The valley"),
        new Design("mountain", 2, 6, 2, 3, "AAJHB", "Along the valley"),
        new Design("mountain", 3, 6, 3, 3, "AJHBDF", "Narrow skies"),
        new Design("mountain", 4, 7, 3, 3, "AAJHBDF", "Valley traffic"),
        new Design("mountain", 5, 8, 3, 4, "AAJHBDFF", "Mountain finale"),

        new Design("arctic", 1, 6, 2, 3, "AAJHDF", "Snow field"),
        new Design("arctic", 2, 6, 3, 3, "AAJHDF", "Icy runways"),
        new Design("arctic", 3, 7, 3, 3, "AJHBDF", "Slow down"),
        new Design("arctic", 4, 8, 3, 4, "AAJHBDF", "Black ice"),
        new Design("arctic", 5, 8, 4, 4, "AAJHBDFF", "Arctic finale"),
    };

    [MenuItem("Tools/Air Traffic Control/Build Tower Levels")]
    public static void Run() => Build(false);

    [MenuItem("Tools/Air Traffic Control/Rebuild Tower Levels (overwrites)")]
    public static void Rebuild() => Build(true);

    public static void Build(bool overwrite)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(CatalogPath);
        if (catalog == null)
            throw new System.Exception("No map catalog: run Tools > Air Traffic Control > Set Up Prototype Scene first.");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Levels", "Tower");

        var kinds = new Dictionary<char, AircraftType>
        {
            { 'A', Kind("Airliner") }, { 'J', Kind("JumboJet") }, { 'H', Kind("Helicopter") },
            { 'B', Kind("HotAirBalloon") }, { 'D', Kind("Drone") }, { 'F', Kind("FighterJet") },
        };

        foreach (Design design in Designs)
        {
            MapEntry map = catalog.Find(design.map);
            if (map == null || map.towerLayout == null)
                throw new System.Exception($"Map {design.map} has no Tower layout in the catalog.");
            string id = MapInfo.LevelId(design.map, GameMode.Tower, design.number);
            string path = $"{Folder}/{id}.asset";

            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            bool fresh = level == null;
            if (fresh)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, path);
            }
            if (fresh || overwrite)
                Fill(level, design, map.towerLayout, kinds);
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssetIfDirty(level);

            LevelEntry entry = map.towerLevels.Find(e => e.id == id);
            if (entry == null)
            {
                entry = new LevelEntry { id = id };
                map.towerLevels.Add(entry);
            }
            entry.title = design.title;
            entry.data = level;
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"TowerLevelsSetup: {Designs.Length} Tower levels ready{(overwrite ? " (rebuilt)" : "")}.");
    }

    static void Fill(LevelData level, Design d, PictureLayout layout, Dictionary<char, AircraftType> kinds)
    {
        // Start from the defaults, so a rebuild never keeps a value from the table's earlier version.
        LevelData defaults = ScriptableObject.CreateInstance<LevelData>();
        EditorUtility.CopySerialized(defaults, level);
        Object.DestroyImmediate(defaults);

        bool ruleOn = d.number >= 2;
        level.name = MapInfo.LevelId(d.map, GameMode.Tower, d.number);
        level.mapId = d.map;
        TowerModeSetup.Apply(level, layout, valleyOnly: d.map == "mountain" && ruleOn);

        level.landingsToClear = d.land;
        level.departuresToClear = d.launch;
        level.aircraftInAir = d.inAir;
        level.departures.firstDepartureAfterSeconds = 5f;
        level.departures.secondsBetweenDepartures = 14f;
        level.aircraftTypes = new List<AircraftType>();
        foreach (char c in d.kinds)
            level.aircraftTypes.Add(kinds[c]);

        level.tower.teachGoAround = d.map == "island" && d.number == 1;
        level.tower.teachHold = d.map == "island" && d.number == 2;

        level.wind.hasWind = d.map == "island" && ruleOn;
        level.wind.onAtStart = true;
        level.rules.closures.enabled = d.map == "city" && ruleOn;
        level.rules.fuel.enabled = d.map == "desert" && ruleOn;
        level.rules.sandstorm.enabled = d.map == "desert" && d.number >= 3;
        level.rules.sandstorm.radius = 1.3f; // the close-up airport is bigger on screen than in Draw mode
        level.rules.noFly.enabled = false;  // Mountain Valley's Tower rule is valleyOnly: approaches along the valley
        level.rules.ice.enabled = d.map == "arctic" && ruleOn;

        // Busier levels get a little more time for 2 stars (PROPOSED values; the owner tunes them after playing).
        float estimate = TowerModeSetup.EstimateSeconds(level);
        level.result.twoStarSeconds = Mathf.Clamp(Mathf.Ceil(estimate * 1.25f / 5f) * 5f, 70f, 150f);
    }

    static AircraftType Kind(string file)
    {
        var type = AssetDatabase.LoadAssetAtPath<AircraftType>($"{AircraftFolder}/{file}.asset");
        if (type == null)
            throw new System.Exception($"Missing aircraft kind {file}.asset");
        return type;
    }
}
