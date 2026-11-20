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
    const string CatalogPath = "Assets/Levels/IslandCatalog.asset";
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

        LevelCatalog catalog = LoadOrCreateCatalog(level);

        var root = new GameObject(RootName);
        var flow = root.AddComponent<GameFlow>();
        var serialized = new SerializedObject(flow);
        serialized.FindProperty("shapeMaterial").objectReferenceValue = material;
        serialized.FindProperty("catalog").objectReferenceValue = catalog;
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
    const string MapPath = "Assets/Art/Maps/island_day.png";
    const string AircraftArtFolder = "Assets/Art/Aircraft";

    // Imports the painted images with good settings for the iPad, gives each aircraft kind its image, and the first
    // time the Island picture is used, fits the level data to it (positions measured from pixels in the picture).
    static void ApplyPaintedArt(LevelData level)
    {
        // Map: full resolution (1449 x 1086, never scaled down), smooth filtering, ASTC 4x4 on iOS (the highest-quality
        // ASTC block), 108.6 pixels per unit so the picture is 10 units high, the game's world scale.
        ConfigureTexture(MapPath, 108.6f, mipmaps: false, maxSize: 2048);
        var map = AssetDatabase.LoadAssetAtPath<Sprite>(MapPath);
        if (map != null && level.mapArt.sprite == null)
        {
            level.mapArt.sprite = map;
            level.mapArt.pixelsPerUnit = 108.6f;
            IslandPictureLayout.Apply(level);
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

    // M3 step 1: the Island levels 1 to 10. Until step 4 every entry reuses the test level's data.
    // An existing catalog is kept, so ids and edits survive re-running this setup.
    static LevelCatalog LoadOrCreateCatalog(LevelData level)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
        if (catalog != null)
            return catalog;

        catalog = ScriptableObject.CreateInstance<LevelCatalog>();
        catalog.mapName = "Island Airport";
        for (int i = 1; i <= 10; i++)
            catalog.levels.Add(new LevelEntry { id = $"island-{i:00}", title = $"Island {i}", data = level });
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

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
