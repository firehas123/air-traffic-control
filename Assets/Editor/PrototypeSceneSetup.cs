// Sets up SampleScene for the M1 prototype. Safe to run again: it replaces its own objects
// and keeps an existing level asset (Assets/Levels/IslandPrototype.asset).
// Menu: Tools > Air Traffic Control > Set Up Prototype Scene
// Command line (Unity closed):
//   Unity -batchmode -quit -projectPath . -executeMethod PrototypeSceneSetup.Run -logFile -

using AirTrafficControl;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PrototypeSceneSetup
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";
    const string ShaderPath = "Assets/Shaders/UnlitVertexColor.shader";
    const string MaterialPath = "Assets/Materials/UnlitVertexColor.mat";
    const string LevelPath = "Assets/Levels/IslandPrototype.asset";
    const string AircraftFolder = "Assets/Aircraft";
    const string RootName = "Game";
    const string OldRootName = "Prototype"; // the M1/M2 scene object

    [MenuItem("Tools/Air Traffic Control/Set Up Prototype Scene")]
    public static void Run()
    {
        Material material = LoadOrCreateMaterial();
        SetLandscapeOnly();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        foreach (string name in new[] { RootName, OldRootName })
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
                Object.DestroyImmediate(existing);
        }

        LevelData level = LoadOrCreateLevel();

        MapCatalog maps = LoadOrCreateMapCatalog(level);

        // Phase 2 (Draw mode): the 25 Draw levels of maps 1 to 5. Existing level assets are kept.
        DrawLevelsSetup.Run();

        var root = new GameObject(RootName);
        var flow = root.AddComponent<GameFlow>();
        var serialized = new SerializedObject(flow);
        serialized.FindProperty("shapeMaterial").objectReferenceValue = material;
        serialized.FindProperty("maps").objectReferenceValue = maps;
        serialized.FindProperty("animationSettings").objectReferenceValue = LoadOrCreateAnimationSettings();
        serialized.FindProperty("spriteMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>(SpriteUnlitMaterialPath);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PrototypeSceneSetup: SampleScene is ready.");
    }

    const string SpriteUnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    const string MapsFolder = "Assets/Art/Maps";
    const string LayoutsFolder = "Assets/Maps/Layouts";
    const string MapCatalogPath = "Assets/Maps/MapCatalog.asset";
    const string AircraftArtFolder = "Assets/Art/Aircraft";
    const float MapPixelsPerUnit = 102.4f; // 1024-pixel-high pictures are 10 world units high

    // The seven maps in unlock order (blueprint 16.2): id, name, picture number of the Draw picture (the Tower picture
    // is the one before it), XP to open (16.3, PROPOSED), gameplay built, and the map rule in one line.
    static readonly (string id, string name, int draw, int xp, bool ready, string rule)[] Maps =
    {
        ("island", "Island", 2, 0, true, "Wind changes switch the active runway end"),
        ("city", "City", 4, 1000, true, "Runways close for short periods"),
        ("desert", "Desert", 6, 2500, true, "Sandstorms and low-fuel aircraft"),
        ("mountain", "Mountain Valley", 8, 4000, true, "No line may pass through the mountains"),
        ("arctic", "Arctic", 10, 5500, true, "Icy runways: land on Slow"),
        ("military", "Military Base", 12, 7000, false, "Fighter scrambles with a time limit"),
        ("spaceport", "Spaceport", 14, 8500, false, "Rocket launches close a no-fly column"),
    };

    // Imports the painted images with good settings for the iPad, links each map picture to its measured layout, gives
    // each aircraft kind its image, and puts the Island test level on the Island Draw picture (02_island_draw.png).
    static void ApplyPaintedArt(LevelData level)
    {
        // Maps: full resolution (1536 x 1024, never scaled down), smooth filtering, ASTC 4x4 on iOS (the highest-quality
        // ASTC block), 102.4 pixels per unit so a picture is 10 units high, the game's world scale.
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { MapsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileName(path);
            if (path.Contains("/Variants/") || file.Length < 3 || !char.IsDigit(file[0]) || !char.IsDigit(file[1]))
                continue; // only the numbered pictures (01_ to 15_); Variants is not used
            ConfigureTexture(path, MapPixelsPerUnit, mipmaps: false, maxSize: 2048);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:PictureLayout", new[] { LayoutsFolder }))
        {
            var layout = AssetDatabase.LoadAssetAtPath<PictureLayout>(AssetDatabase.GUIDToAssetPath(guid));
            var picture = AssetDatabase.LoadAssetAtPath<Sprite>($"{MapsFolder}/{layout.pictureFile}");
            if (picture != null && layout.picture != picture)
            {
                layout.picture = picture;
                EditorUtility.SetDirty(layout);
                AssetDatabase.SaveAssetIfDirty(layout);
            }
        }

        // The test level moves to the Island Draw picture once; later runs keep values tuned in the Inspector.
        var islandDraw = AssetDatabase.LoadAssetAtPath<PictureLayout>($"{LayoutsFolder}/02_island_draw.asset");
        if (islandDraw != null && level.layout != islandDraw)
        {
            level.mapId = "island";
            islandDraw.ApplyTo(level);
        }

        // Aircraft: 512-pixel images, mipmaps and trilinear filtering so they stay crisp when drawn small.
        SetAircraftArt("Airliner", "airliner.png", -90f,
            lights: new[] { new Vector2(-0.108f, 0.325f), new Vector2(-0.108f, -0.323f), new Vector2(-0.355f, 0f) },
            engines: new[] { new Vector2(0.05f, 0.12f), new Vector2(0.05f, -0.12f) });
        SetAircraftArt("JumboJet", "jumbo_jet.png", -90f,
            lights: new[] { new Vector2(-0.095f, 0.359f), new Vector2(-0.095f, -0.359f), new Vector2(-0.352f, 0f) },
            engines: new[] { new Vector2(0.02f, 0.2f), new Vector2(0.02f, -0.2f) });
        SetAircraftArt("FighterJet", "fighter_jet.png", -90f,
            engines: new[] { new Vector2(-0.36f, 0f) });
        SetAircraftArt("Helicopter", "helicopter.png", -90f,
            rotors: new[] { new Vector2(0.134f, 0f), new Vector2(-0.327f, 0f) });
        SetAircraftArt("Drone", "drone.png", -90f,
            rotors: new[] { new Vector2(0.272f, 0.3f), new Vector2(0.272f, -0.3f), new Vector2(-0.256f, 0.294f), new Vector2(-0.256f, -0.288f) });
        SetAircraftArt("HotAirBalloon", "hot_air_balloon.png", 0f); // stays upright
    }

    static void SetAircraftArt(string kindFile, string imageFile, float rotation,
                               Vector2[] lights = null, Vector2[] engines = null, Vector2[] rotors = null)
    {
        string imagePath = $"{AircraftArtFolder}/{imageFile}";
        ConfigureTexture(imagePath, 640f, mipmaps: true, maxSize: 512);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
        var type = AssetDatabase.LoadAssetAtPath<AircraftType>($"{AircraftFolder}/{kindFile}.asset");
        if (sprite == null || type == null || type.sprite != null)
            return; // missing, or already set (tuned values are kept)
        type.sprite = sprite;
        type.spriteRotation = rotation;
        type.spriteWorldSize = 0.8f;
        type.lightPoints = lights ?? new Vector2[0];
        type.enginePoints = engines ?? new Vector2[0];
        type.rotorPoints = rotors ?? new Vector2[0];
        EditorUtility.SetDirty(type);
        AssetDatabase.SaveAssetIfDirty(type);
    }

    static void ConfigureTexture(string path, float pixelsPerUnit, bool mipmaps, int maxSize)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.mipmapEnabled = mipmaps;
        importer.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "iPhone",
            overridden = true,
            maxTextureSize = maxSize,
            format = TextureImporterFormat.ASTC_4x4,
            compressionQuality = 100,
        });
        importer.SaveAndReimport();
    }

    // M3 step 2: every animation value in one asset (blueprint 15.3). An existing asset is kept.
    static AnimationSettings LoadOrCreateAnimationSettings()
    {
        const string path = "Assets/Config/AnimationSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<AnimationSettings>(path);
        if (settings != null)
            return settings;
        if (!AssetDatabase.IsValidFolder("Assets/Config"))
            AssetDatabase.CreateFolder("Assets", "Config");
        settings = ScriptableObject.CreateInstance<AnimationSettings>();
        AssetDatabase.CreateAsset(settings, path);
        return settings;
    }

    // Blueprint 16: the seven maps with both pictures and layouts, and five level ids per mode. The Draw levels of maps
    // 1 to 5 come from DrawLevelsSetup (phase 2); entries without data (Tower until phase 3, maps 6 and 7) make their
    // buttons say "coming in the next update". IslandPrototype.asset stays as the ground-control test level. An existing catalog keeps its levels and values; pictures
    // and layouts are filled in when missing.
    static MapCatalog LoadOrCreateMapCatalog(LevelData islandTest)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MapCatalog>(MapCatalogPath);
        if (catalog == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Maps"))
                AssetDatabase.CreateFolder("Assets", "Maps");
            catalog = ScriptableObject.CreateInstance<MapCatalog>();
            AssetDatabase.CreateAsset(catalog, MapCatalogPath);
        }

        foreach (var m in Maps)
        {
            MapEntry entry = catalog.Find(m.id);
            if (entry == null)
            {
                entry = new MapEntry { id = m.id, displayName = m.name, xpToUnlock = m.xp, gameplayReady = m.ready, ruleText = m.rule };
                foreach (GameMode mode in GameModes.All)
                    for (int n = 1; n <= Progression.LevelThatOpensNextMap; n++)
                        entry.Levels(mode).Add(new LevelEntry
                        {
                            id = MapInfo.LevelId(m.id, mode, n),
                            title = $"{m.name} {mode} {n}",
                            data = null, // Draw levels: DrawLevelsSetup; Tower levels: phase 3
                        });
                catalog.maps.Add(entry);
            }
            string drawFile = $"{m.draw:00}_{FileKey(m.id)}_draw";
            string towerFile = $"{m.draw - 1:00}_{FileKey(m.id)}_tower";
            if (entry.drawPicture == null) entry.drawPicture = AssetDatabase.LoadAssetAtPath<Sprite>($"{MapsFolder}/{drawFile}.png");
            if (entry.towerPicture == null) entry.towerPicture = AssetDatabase.LoadAssetAtPath<Sprite>($"{MapsFolder}/{towerFile}.png");
            if (entry.drawLayout == null) entry.drawLayout = AssetDatabase.LoadAssetAtPath<PictureLayout>($"{LayoutsFolder}/{drawFile}.asset");
            if (entry.towerLayout == null) entry.towerLayout = AssetDatabase.LoadAssetAtPath<PictureLayout>($"{LayoutsFolder}/{towerFile}.asset");
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        return catalog;
    }

    // Picture file names use short words: 07_mountain_tower.png for Mountain Valley.
    static string FileKey(string mapId) => mapId;

    // Creates the Island prototype level with LevelData's default values, if it does not exist yet.
    // An existing asset is kept, so edits made in the Inspector survive re-running this setup. It is
    // saved again so that fields added to LevelData since it was created appear in the file.
    static LevelData LoadOrCreateLevel()
    {
        var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelPath);
        if (level == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Levels"))
                AssetDatabase.CreateFolder("Assets", "Levels");
            level = ScriptableObject.CreateInstance<LevelData>();
            AssetDatabase.CreateAsset(level, LevelPath);
        }

        // During M2 the test level spawns every kind so the owner can try them (blueprint 14.4). A kind is added to
        // the level when its asset is first created; kinds already in the asset (or removed from it) are left alone.
        AddKind(level, "Airliner", AircraftSilhouette.Airliner);
        AddKind(level, "JumboJet", AircraftSilhouette.JumboJet);
        AddKind(level, "FighterJet", AircraftSilhouette.FighterJet);
        AddKind(level, "Helicopter", AircraftSilhouette.Helicopter);
        AddKind(level, "Drone", AircraftSilhouette.Drone);
        AddKind(level, "HotAirBalloon", AircraftSilhouette.HotAirBalloon);

        // Painted art (art rule of 20 Nov 2026): the Island map and the aircraft images.
        ApplyPaintedArt(level);

        // M3: the test level gets two gates and one runway crossing, so the owner can try ground control
        // (blueprint 15.4). Gate A is on the apron north of the runway; gate B is a remote stand to the south,
        // whose taxiway crosses the runway and stops at a crossing point first.
        if (level.ground.gates.Count == 0)
        {
            level.ground.gates.Add(new GateDefinition
            {
                position = new Vector2(0.4f, 0.45f), headingDegrees = 270f,
                connector = new System.Collections.Generic.List<Vector2> { new Vector2(0.4f, 0.45f), new Vector2(0.4f, -0.15f) },
                crossingIndex = -1,
            });
            level.ground.gates.Add(new GateDefinition
            {
                position = new Vector2(1.6f, -1.75f), headingDegrees = 90f,
                connector = new System.Collections.Generic.List<Vector2>
                    { new Vector2(1.6f, -1.75f), new Vector2(1.6f, -1.32f), new Vector2(1.6f, -0.15f) },
                crossingIndex = 1,
            });
        }

        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssetIfDirty(level);
        return level;
    }

    static void AddKind(LevelData level, string fileName, AircraftSilhouette kind)
    {
        AircraftType type = LoadOrCreateAircraftType(fileName, kind, out bool created);
        if ((created || level.aircraftTypes.Count == 0) && !level.aircraftTypes.Contains(type))
            level.aircraftTypes.Add(type);
    }

    // Creates an aircraft kind with its agreed starting values (blueprint 13.1, 14.1), if it does not exist yet.
    // An existing asset is kept, so values tuned in the Inspector survive re-running this setup.
    static AircraftType LoadOrCreateAircraftType(string fileName, AircraftSilhouette kind, out bool created)
    {
        string path = $"{AircraftFolder}/{fileName}.asset";
        var type = AssetDatabase.LoadAssetAtPath<AircraftType>(path);
        created = type == null;
        if (type != null)
            return type;

        if (!AssetDatabase.IsValidFolder(AircraftFolder))
            AssetDatabase.CreateFolder("Assets", "Aircraft");
        type = ScriptableObject.CreateInstance<AircraftType>();
        type.SetDefaults(kind);
        AssetDatabase.CreateAsset(type, path);
        return type;
    }

    static Material LoadOrCreateMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
            return material;

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
            throw new System.Exception("Shader not found at " + ShaderPath);

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        material = new Material(shader) { name = "UnlitVertexColor" };
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    // Blueprint C19: landscape for now. Auto-rotation is kept between the two landscape directions.
    static void SetLandscapeOnly()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        // In batch mode Unity does not write project settings on exit, so mark them changed and save now.
        Object playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
        EditorUtility.SetDirty(playerSettings);
        AssetDatabase.SaveAssetIfDirty(playerSettings);
    }
}
