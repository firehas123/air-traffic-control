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
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PrototypeSceneSetup: SampleScene is ready.");
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
