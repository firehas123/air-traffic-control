using System;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// The game's flow (M3 step 1, blueprint 16): loads the save, shows the home screen (seven maps, Draw and Tower),
    /// then the chosen map and mode's level select, builds the chosen level, records the result (stars, best time,
    /// coins, XP, unlocks) and saves it on the device. Replaces the M1/M2 PrototypeSetup.
    ///
    /// Each level is built under one "Level" object (map, landing zones, carrier, input, path planner, safety, HUD,
    /// sound and the <see cref="LevelDirector"/>), and the whole object is removed when the level is left.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField] MapCatalog maps;
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
        HomeView home;
        LevelSelectView levelSelect;
        GameObject levelRoot;
        LevelDirector director;
        HudView hud;
        int currentMap = -1;
        GameMode currentMode = GameMode.Draw;
        int currentIndex = -1;

        public Progression Progression => progression;

        void Awake()
        {
            // Mobile platforms default to 30 FPS; the game aims for 60 (blueprint 5).
            Application.targetFrameRate = 60;
        }

        void Start()
        {
            if (maps == null || maps.maps.Count == 0)
            {
                Debug.LogError("GameFlow: no map catalog. Run Tools > Air Traffic Control > Set Up Prototype Scene.");
                return;
            }

            Animations.Use(animationSettings);
            AircraftController.SpriteMaterial = spriteMaterial;
            save = ProgressionSave.Load(ProgressionSave.DefaultPath);
            progression = new Progression(save.Data, maps.ToInfo(), maps.xp);

            home = new GameObject("HomeView").AddComponent<HomeView>();
            home.transform.SetParent(transform, false);
            home.Initialise();
            home.ModeChosen += ChooseMode;

            levelSelect = new GameObject("LevelSelectView").AddComponent<LevelSelectView>();
            levelSelect.transform.SetParent(transform, false);
            levelSelect.Initialise();
            levelSelect.LevelChosen += Play;
            levelSelect.HomePressed += ShowHome;

            ShowHome();
        }

        public void ShowHome()
        {
            LeaveLevel();
            levelSelect.Hide();
            home.Show(progression, maps.maps);
        }

        /// <summary>Opens a map and mode: its level select, or a "coming in the next update" note when it has no levels yet.</summary>
        public void ChooseMode(int mapIndex, GameMode mode)
        {
            if (!progression.IsMapUnlocked(mapIndex))
                return;
            MapEntry map = maps.maps[mapIndex];
            if (!HasPlayableLevels(map, mode))
            {
                // Until the Draw and Tower tracks are built (phases 2 and 3), only Island Draw has levels.
                home.ShowNotice($"{map.displayName} {mode} mode is coming in the next update");
                return;
            }
            currentMap = mapIndex;
            currentMode = mode;
            ShowLevels();
        }

        static bool HasPlayableLevels(MapEntry map, GameMode mode)
        {
            List<LevelEntry> levels = map.Levels(mode);
            return levels.Count > 0 && levels.TrueForAll(l => l.data != null);
        }

        public void ShowLevels()
        {
            LeaveLevel();
            if (currentMap < 0)
            {
                ShowHome();
                return;
            }
            home.Hide();
            MapEntry map = maps.maps[currentMap];
            levelSelect.Show(progression, currentMap, map.displayName, currentMode, map.Levels(currentMode));
        }

        List<LevelEntry> CurrentLevels => maps.maps[currentMap].Levels(currentMode);

        public void Play(int index)
        {
            if (currentMap < 0 || index < 0 || index >= CurrentLevels.Count || !progression.IsLevelUnlocked(currentMap, currentMode, index))
                return;
            LeaveLevel();
            home.Hide();
            levelSelect.Hide();
            currentIndex = index;
            BuildLevel(CurrentLevels[index].data);
        }

        /// <summary>Plays a level directly, outside the level select (for the PlayMode tests). Results are not saved.</summary>
        public void PlayLevelData(LevelData level)
        {
            LeaveLevel();
            home.Hide();
            levelSelect.Hide();
            currentIndex = -1;
            BuildLevel(level);
            director.Cleared -= OnCleared; // not a catalog level: nothing to record
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

            // Draw mode draws lines; Tower mode has no drawing (blueprint 16.1), so its planner is never connected to input.
            bool tower = level.mode == GameMode.Tower;
            var planner = Add<PathPlanner>("PathPlanner");
            if (!tower)
                planner.Initialise(input, landing, carrier);

            hud = Add<HudView>("HudView");
            hud.Initialise();
            hud.NextLevelPressed += OnNextLevel;
            hud.LevelsPressed += ShowLevels;
            hud.ZonesPressed += zones.Toggle;

            var feedback = Add<FeedbackSystem>("FeedbackSystem");
            feedback.Initialise();

            // Option buttons beside a tapped aircraft: Slow and Fast in Draw mode (blueprint 15.4); every Tower command in
            // Tower mode (blueprint 16.1), with the runway letters on the map.
            var menu = Add<CommandMenu>("CommandMenu");
            menu.Initialise(worldCamera);
            GroundControl groundControl = null;
            TowerControl towerControl = null;
            if (tower)
            {
                towerControl = Add<TowerControl>("TowerControl");
                towerControl.Initialise(level, input, menu, landing, worldCamera);
                var labels = Add<RunwayLabels>("RunwayLabels");
                labels.Initialise(landing, level.layout, worldCamera);
            }
            else
            {
                groundControl = Add<GroundControl>("GroundControl");
                groundControl.Initialise(input, menu);
            }

            // Map rules of Draw mode (blueprint 16.2): City closures, Desert fuel and sandstorm, Mountain no-fly, Arctic ice.
            var mapRules = Add<MapRuleSystem>("MapRuleSystem");
            mapRules.Initialise(level, landing, planner, worldCamera, shapeMaterial);

            director = Add<LevelDirector>("LevelDirector");
            director.Initialise(level, worldCamera, landing, safety, input, planner, carrier, hud, shapeMaterial, aircraft, feedback, effects,
                                groundControl, mapRules, towerControl);
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
            LevelEntry entry = CurrentLevels[currentIndex];
            LevelRewards rewards = progression.RecordClear(entry.id, outcome.Stars, outcome.Seconds,
                                                           outcome.Landed, outcome.Departed, outcome.Warnings, entry.data.result);
            Persist();
            bool hasNext = currentIndex + 1 < CurrentLevels.Count && progression.IsLevelUnlocked(currentMap, currentMode, currentIndex + 1);
            hud.ShowCleared(outcome, rewards, progression.Coins, hasNext);
        }

        void OnFailed(LevelOutcome outcome)
        {
            if (outcome.FailTitle != null)
                hud.ShowFailed(outcome.FailTitle, outcome);
            else
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
