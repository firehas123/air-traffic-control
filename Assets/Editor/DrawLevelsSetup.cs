// Writes the 25 Draw-mode level assets (blueprint 16, phase 2): 5 levels for each of Island, City, Desert, Mountain Valley
// and Arctic, on each map's Draw picture, and links them into Assets/Maps/MapCatalog.asset.
// The level assets under Assets/Levels/Draw are the data; this script only seeds them. Running it again keeps existing
// assets (values tuned in the Inspector survive) unless "Rebuild" is used, which overwrites them from the table below.
// Menu: Tools > Air Traffic Control > Build Draw Levels / Rebuild Draw Levels (overwrites)
// Command line: Unity -batchmode -nographics -quit -projectPath . -executeMethod DrawLevelsSetup.Rebuild -logFile -

using System.Collections.Generic;
using AirTrafficControl;
using UnityEditor;
using UnityEngine;

public static class DrawLevelsSetup
{
    const string Folder = "Assets/Levels/Draw";
    const string CatalogPath = "Assets/Maps/MapCatalog.asset";
    const string AircraftFolder = "Assets/Aircraft";
    const string CarrierPicture = "Assets/Art/Maps/15_carrier.png";

    // Aircraft kinds by letter: A airliner, J jumbo jet, H helicopter, B hot air balloon, D drone, F fighter jet.
    // A letter repeated makes that kind more common. New kinds arrive a few at a time (blueprint 14.4): Island brings in
    // the airliner, then the jumbo jet, helicopter, balloon and drone, and the fighter with the carrier last.
    // The map rule is on from level 2 (Desert: low fuel from level 2, the sandstorm from level 3).
    struct Design
    {
        public string map;
        public int number;
        public int land;
        public int inAir;
        public string kinds;
        public string title;
        public Design(string map, int number, int land, int inAir, string kinds, string title)
        {
            this.map = map; this.number = number; this.land = land; this.inAir = inAir; this.kinds = kinds; this.title = title;
        }
    }

    static readonly Design[] Designs =
    {
        new Design("island", 1, 9, 2, "A", "First landings"),
        new Design("island", 2, 9, 2, "AAJ", "Jumbo and the wind"),
        new Design("island", 3, 11, 3, "AAJH", "Helicopter"),
        new Design("island", 4, 11, 4, "AJHBD", "Balloon and drone"),
        new Design("island", 5, 12, 4, "AJHBDFF", "Fighters and the carrier"),

        new Design("city", 1, 11, 3, "AAJHD", "Three runways"),
        new Design("city", 2, 11, 3, "AAJHD", "Runway closed"),
        new Design("city", 3, 12, 4, "AJHBDF", "Rush hour"),
        new Design("city", 4, 12, 4, "AJJHBDF", "Closures and jumbos"),
        new Design("city", 5, 13, 4, "AAJHBDFF", "City finale"),

        new Design("desert", 1, 12, 3, "AAJHDF", "The triangle"),
        new Design("desert", 2, 12, 3, "AAJHDF", "Low fuel"),
        new Design("desert", 3, 12, 4, "AJHBDF", "Sandstorm"),
        new Design("desert", 4, 12, 4, "AAJHBDF", "Storm season"),
        new Design("desert", 5, 13, 4, "AAJHBDFF", "Desert finale"),

        new Design("mountain", 1, 9, 3, "AAJHB", "The valley"),
        new Design("mountain", 2, 9, 3, "AAJHB", "No-fly mountains"),
        new Design("mountain", 3, 10, 3, "AJHBDF", "Narrow skies"),
        new Design("mountain", 4, 12, 4, "AAJHBDF", "Valley traffic"),
        new Design("mountain", 5, 13, 4, "AAJHBDFF", "Mountain finale"),

        new Design("arctic", 1, 12, 3, "AAJHDF", "Snow field"),
        new Design("arctic", 2, 12, 3, "AAJHDF", "Icy runways"),
        new Design("arctic", 3, 12, 4, "AJHBDF", "Slow down"),
        new Design("arctic", 4, 12, 4, "AAJHBDF", "Black ice"),
        new Design("arctic", 5, 13, 4, "AAJHBDFF", "Arctic finale"),
    };

    [MenuItem("Tools/Air Traffic Control/Build Draw Levels")]
    public static void Run() => Build(false);

    [MenuItem("Tools/Air Traffic Control/Rebuild Draw Levels (overwrites)")]
    public static void Rebuild() => Build(true);

    public static void Build(bool overwrite)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(CatalogPath);
        if (catalog == null)
            throw new System.Exception("No map catalog: run Tools > Air Traffic Control > Set Up Prototype Scene first.");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Levels", "Draw");

        var kinds = new Dictionary<char, AircraftType>
        {
            { 'A', Kind("Airliner") }, { 'J', Kind("JumboJet") }, { 'H', Kind("Helicopter") },
            { 'B', Kind("HotAirBalloon") }, { 'D', Kind("Drone") }, { 'F', Kind("FighterJet") },
        };
        var carrierPicture = AssetDatabase.LoadAssetAtPath<Sprite>(CarrierPicture);

        foreach (Design design in Designs)
        {
            MapEntry map = catalog.Find(design.map);
            if (map == null || map.drawLayout == null)
                throw new System.Exception($"Map {design.map} has no Draw layout in the catalog.");
            string id = MapInfo.LevelId(design.map, GameMode.Draw, design.number);
            string path = $"{Folder}/{id}.asset";

            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            bool fresh = level == null;
            if (fresh)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, path);
            }
            if (fresh || overwrite)
                Fill(level, design, map.drawLayout, kinds, carrierPicture);
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssetIfDirty(level);

            LevelEntry entry = map.drawLevels.Find(e => e.id == id);
            if (entry == null)
            {
                entry = new LevelEntry { id = id };
                map.drawLevels.Add(entry);
            }
            entry.title = design.title;
            entry.data = level;
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"DrawLevelsSetup: {Designs.Length} Draw levels ready{(overwrite ? " (rebuilt)" : "")}.");
    }

    static void Fill(LevelData level, Design d, PictureLayout layout, Dictionary<char, AircraftType> kinds, Sprite carrierPicture)
    {
        // Start from the defaults, so a rebuild never keeps a value from the table's earlier version.
        LevelData defaults = ScriptableObject.CreateInstance<LevelData>();
        EditorUtility.CopySerialized(defaults, level);
        Object.DestroyImmediate(defaults);

        level.name = MapInfo.LevelId(d.map, GameMode.Draw, d.number);
        level.mapId = d.map;
        DrawModeSetup.Apply(level, layout);

        level.landingsToClear = d.land;
        level.aircraftInAir = d.inAir;
        level.aircraftTypes = new List<AircraftType>();
        foreach (char c in d.kinds)
            level.aircraftTypes.Add(kinds[c]);

        bool ruleOn = d.number >= 2;
        level.wind.hasWind = d.map == "island" && ruleOn;
        level.wind.onAtStart = true;
        level.rules.closures.enabled = d.map == "city" && ruleOn;
        level.rules.fuel.enabled = d.map == "desert" && ruleOn;
        level.rules.sandstorm.enabled = d.map == "desert" && d.number >= 3;
        level.rules.noFly.enabled = d.map == "mountain" && ruleOn;
        level.rules.ice.enabled = d.map == "arctic" && ruleOn;

        // The carrier: Island level 5 only, with the painted ship (blueprint 13.2, 14.4: last).
        level.carrier.hasCarrier = d.map == "island" && d.number == 5;
        level.carrier.picture = carrierPicture;
        level.carrier.shipLength = 2.6f;
        level.carrier.shipWidth = 0.61f; // 15_carrier.png: the ship is 342 by 1459 pixels
        level.carrier.firstShipAfterSeconds = 15f;

        // Busier levels get a little more time for 2 stars (PROPOSED values; the owner tunes them after playing).
        float estimate = SessionLengthEstimate.Seconds(level, 0.6f);
        level.result.twoStarSeconds = Mathf.Clamp(Mathf.Ceil(estimate * 1.25f / 5f) * 5f, 70f, 140f);
    }

    static AircraftType Kind(string file)
    {
        var type = AssetDatabase.LoadAssetAtPath<AircraftType>($"{AircraftFolder}/{file}.asset");
        if (type == null)
            throw new System.Exception($"Missing aircraft kind {file}.asset");
        return type;
    }
}
