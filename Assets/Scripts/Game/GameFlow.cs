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

            GameObject island = MeshBuilder.CreateObject("Island", MapShapes.CreateIslandMesh(level.islandRadii), shapeMaterial,
                MapShapes.IslandSortingOrder, level.islandCentre, 0f);
            island.transform.SetParent(levelRoot.transform, true);

            var landing = Add<LandingSystem>("LandingSystem");
            landing.Initialise(level.runways, level.landingPads, shapeMaterial);

            var safety = Add<AirspaceSafety>("AirspaceSafety");

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

            var feedback = Add<FeedbackSystem>("FeedbackSystem");
            feedback.Initialise();

            director = Add<LevelDirector>("LevelDirector");
            director.Initialise(level, worldCamera, landing, safety, input, planner, carrier, hud, shapeMaterial, aircraft, feedback);
            director.Cleared += OnCleared;
            director.Failed += OnFailed;
            director.StartLevel();
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
