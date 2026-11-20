using System;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The game's flow (M3 step 1): loads the save, shows level select, builds the chosen level, records the result
    /// (stars, best time, coins, unlocks) and saves it on the device. Replaces the M1/M2 PrototypeSetup.
    ///
    /// Each level is built under one "Level" object (map, landing zones, carrier, input, path planner, safety, HUD,
    /// sound and the <see cref="LevelDirector"/>), and the whole object is removed when the level is left.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField] LevelCatalog catalog;
        [SerializeField] Material shapeMaterial;
        [SerializeField] AircraftSettings aircraft = new AircraftSettings();
        [SerializeField] AnimationSettings animationSettings;

        [Tooltip("Unlit sprite material for painted images (map and aircraft).")]
        [SerializeField] Material spriteMaterial;

        Vector3 defaultCameraPosition;
        float defaultCameraSize;
        bool cameraDefaultsKnown;

        ProgressionSave save;
        Progression progression;
        LevelSelectView levelSelect;
        GameObject levelRoot;
        LevelDirector director;
        HudView hud;
        int currentIndex = -1;

        public Progression Progression => progression;

        void Awake()
        {
            // Mobile platforms default to 30 FPS; the game aims for 60 (blueprint 5).
            Application.targetFrameRate = 60;
        }

        void Start()
        {
            if (catalog == null || catalog.levels.Count == 0)
            {
                Debug.LogError("GameFlow: no level catalog. Run Tools > Air Traffic Control > Set Up Prototype Scene.");
                return;
            }

            Animations.Use(animationSettings);
            AircraftController.SpriteMaterial = spriteMaterial;
            save = ProgressionSave.Load(ProgressionSave.DefaultPath);
            progression = new Progression(save.Data, catalog.Ids);

            levelSelect = new GameObject("LevelSelectView").AddComponent<LevelSelectView>();
            levelSelect.transform.SetParent(transform, false);
            levelSelect.Initialise(catalog.mapName);
            levelSelect.LevelChosen += Play;

            ShowLevels();
        }

        public void ShowLevels()
        {
            LeaveLevel();
            levelSelect.Show(progression, catalog.levels);
        }

        public void Play(int index)
        {
            if (index < 0 || index >= catalog.levels.Count || !progression.IsUnlocked(index))
                return;
            LeaveLevel();
            levelSelect.Hide();
            currentIndex = index;
            BuildLevel(catalog.levels[index].data);
        }

        void BuildLevel(LevelData level)
        {
            Camera worldCamera = Camera.main;
            levelRoot = new GameObject("Level");
            bool painted = level.mapArt.HasPicture && spriteMaterial != null;

            FrameCamera(worldCamera, level, painted);

            if (painted)
            {
                // The painted map (art rule of 20 Nov 2026), below everything else.
                var map = new GameObject("Map");
                map.transform.SetParent(levelRoot.transform, false);
                var renderer = map.AddComponent<SpriteRenderer>();
                renderer.sprite = level.mapArt.sprite;
                renderer.sharedMaterial = spriteMaterial;
                renderer.sortingOrder = MapSortingOrder;
                float scale = level.mapArt.WorldRect.height / Mathf.Max(0.001f, level.mapArt.sprite.bounds.size.y);
                map.transform.localScale = new Vector3(scale, scale, 1f);
            }
            else
            {
                GameObject island = MeshBuilder.CreateObject("Island", MapShapes.CreateIslandMesh(level.islandRadii), shapeMaterial,
                    MapShapes.IslandSortingOrder, level.islandCentre, 0f);
                island.transform.SetParent(levelRoot.transform, true);
            }

            var landing = Add<LandingSystem>("LandingSystem");
            landing.Initialise(level.runways, level.landingPads, shapeMaterial, level.scenery, painted);

            var scenery = Add<IslandScenery>("IslandScenery");
            scenery.Initialise(level, worldCamera, landing, shapeMaterial, painted);

            var zones = Add<ZoneDebugView>("ZoneDebugView");
            zones.Initialise(level, landing, worldCamera, shapeMaterial);

            var effects = Add<EffectsSystem>("EffectsSystem");
            effects.Initialise(worldCamera, shapeMaterial);

            var safety = Add<AirspaceSafety>("AirspaceSafety");
            safety.Initialise(landing);

            var input = Add<InputAdapter>("InputAdapter");
            input.Initialise(worldCamera);

            var carrier = Add<CarrierSystem>("CarrierSystem");
            carrier.Initialise(level.carrier, worldCamera, shapeMaterial);

            var planner = Add<PathPlanner>("PathPlanner");
            planner.Initialise(input, landing, carrier);

            hud = Add<HudView>("HudView");
            hud.Initialise();
            hud.NextLevelPressed += OnNextLevel;
            hud.LevelsPressed += ShowLevels;
            hud.ZonesPressed += zones.Toggle;

            var feedback = Add<FeedbackSystem>("FeedbackSystem");
            feedback.Initialise();

            // Hybrid controls (blueprint 15.4): option buttons beside a tapped aircraft, and ground control.
            var menu = Add<CommandMenu>("CommandMenu");
            menu.Initialise(worldCamera);
            var groundControl = Add<GroundControl>("GroundControl");
            groundControl.Initialise(input, menu, level.ground);

            director = Add<LevelDirector>("LevelDirector");
            director.Initialise(level, worldCamera, landing, safety, input, planner, carrier, hud, shapeMaterial, aircraft, feedback, effects, groundControl);
            // Taps on the HUD or on the option buttons never reach the map.
            input.SetScreenBlocker(point => hud.IsOverInteractive(point) || menu.IsOverButton(point));
            director.Cleared += OnCleared;
            director.Failed += OnFailed;
            director.StartLevel();
        }

        const int MapSortingOrder = -40;

        // Painted map: on the iPad (4:3) the whole island fills the screen; on wider screens the picture is cropped at
        // its top and bottom (sea), never the airport. Levels without a picture keep the default camera.
        void FrameCamera(Camera camera, LevelData level, bool painted)
        {
            if (!cameraDefaultsKnown)
            {
                defaultCameraPosition = camera.transform.position;
                defaultCameraSize = camera.orthographicSize;
                cameraDefaultsKnown = true;
            }
            if (!painted)
            {
                camera.transform.position = defaultCameraPosition;
                camera.orthographicSize = defaultCameraSize;
                return;
            }
            CameraFraming.Apply(camera, CameraFraming.View(level.mapArt.WorldRect, level.mapArt.keepVisible, camera.aspect));
        }

        T Add<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(levelRoot.transform, false);
            return go.AddComponent<T>();
        }

        void LeaveLevel()
        {
            if (director != null)
            {
                director.Cleared -= OnCleared;
                director.Failed -= OnFailed;
                director.Shutdown();
            }
            if (hud != null)
            {
                hud.NextLevelPressed -= OnNextLevel;
                hud.LevelsPressed -= ShowLevels;
            }
            if (levelRoot != null)
                Destroy(levelRoot);
            levelRoot = null;
            director = null;
            hud = null;
            Time.timeScale = 1f;
        }

        void OnNextLevel() => Play(currentIndex + 1);

        void OnCleared(LevelOutcome outcome)
        {
            LevelEntry entry = catalog.levels[currentIndex];
            LevelRewards rewards = progression.RecordClear(entry.id, outcome.Stars, outcome.Seconds,
                                                           outcome.Landed, outcome.Departed, entry.data.result);
            Persist();
            bool hasNext = currentIndex + 1 < catalog.levels.Count && progression.IsUnlocked(currentIndex + 1);
            hud.ShowCleared(outcome, rewards, progression.Coins, hasNext);
        }

        void OnFailed(LevelOutcome outcome)
        {
            hud.ShowCollision(outcome);
        }

        void Persist()
        {
            try
            {
                save.Write();
            }
            catch (Exception e)
            {
                Debug.LogError($"GameFlow: could not save progress: {e.Message}");
            }
        }

        // iOS may close a backgrounded app without warning, so save whenever the app is paused or closed.
        void OnApplicationPause(bool paused)
        {
            if (paused && save != null)
                Persist();
        }

        void OnApplicationQuit()
        {
            if (save != null)
                Persist();
        }
    }
}
