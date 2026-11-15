using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// M1 prototype scene setup: builds the map from <see cref="LevelData"/>, creates the systems
    /// (landing, airspace safety, input, path planner, HUD), and hands the level to <see cref="LevelDirector"/>.
    /// A level-select flow will replace this once there is more than one level.
    /// </summary>
    public sealed class PrototypeSetup : MonoBehaviour
    {
        [SerializeField] LevelData level;
        [SerializeField] Material shapeMaterial;
        [SerializeField] AircraftSettings aircraft = new AircraftSettings();

        void Awake()
        {
            // Mobile platforms default to 30 FPS. Drawing feels much smoother at 60.
            Application.targetFrameRate = 60;
        }

        void Start()
        {
            Camera worldCamera = Camera.main;

            if (level == null || level.spawnPoints.Count == 0)
            {
                Debug.LogError("PrototypeSetup: no LevelData or no spawn points. Run Tools > Air Traffic Control > Set Up Prototype Scene.");
                return;
            }

            MeshBuilder.CreateObject("Island", MapShapes.CreateIslandMesh(level.islandRadii), shapeMaterial,
                MapShapes.IslandSortingOrder, level.islandCentre, 0f);

            var landing = new GameObject("LandingSystem").AddComponent<LandingSystem>();
            landing.Initialise(level.runways, shapeMaterial);

            var safety = new GameObject("AirspaceSafety").AddComponent<AirspaceSafety>();

            var input = new GameObject("InputAdapter").AddComponent<InputAdapter>();
            input.Initialise(worldCamera);

            var planner = new GameObject("PathPlanner").AddComponent<PathPlanner>();
            planner.Initialise(input, landing);

            var hud = new GameObject("HudView").AddComponent<HudView>();
            hud.Initialise();

            var director = new GameObject("LevelDirector").AddComponent<LevelDirector>();
            director.Initialise(level, worldCamera, landing, safety, input, planner, hud, shapeMaterial, aircraft);
            director.StartLevel();
        }
    }
}
