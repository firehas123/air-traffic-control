using System.Collections;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// M1 prototype only: builds the map from <see cref="LevelData"/>, creates the input adapter, path planner
    /// and landing system, and keeps one aircraft in the air (a new one appears after each landing).
    /// It will be replaced by LevelDirector, which will spawn aircraft from a schedule in the level data.
    /// </summary>
    public sealed class PrototypeSetup : MonoBehaviour
    {
        [SerializeField] LevelData level;
        [SerializeField] Material shapeMaterial;
        [SerializeField] Color aircraftColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] Color clearedToLandColor = new Color(0.3f, 0.9f, 0.4f);
        [SerializeField] float aircraftSpeed = 1.2f;          // world units per second
        [SerializeField] float aircraftTurnRate = 200f;       // degrees per second, only for the edge turn-back with no path
        [SerializeField] float edgeMargin = 0.8f;             // turn back this far inside the screen edge
        [SerializeField] float respawnDelay = 1f;             // seconds after a landing

        Camera worldCamera;
        LandingSystem landing;

        void Awake()
        {
            // Mobile platforms default to 30 FPS. Drawing feels much smoother at 60.
            Application.targetFrameRate = 60;
        }

        void Start()
        {
            worldCamera = Camera.main;

            if (level == null)
            {
                Debug.LogError("PrototypeSetup: no LevelData assigned. Run Tools > Air Traffic Control > Set Up Prototype Scene.");
                return;
            }

            MeshBuilder.CreateObject("Island", MapShapes.CreateIslandMesh(level.islandRadii), shapeMaterial,
                MapShapes.IslandSortingOrder, level.islandCentre, 0f);

            landing = new GameObject("LandingSystem").AddComponent<LandingSystem>();
            landing.Initialise(level.runways, shapeMaterial);

            var input = new GameObject("InputAdapter").AddComponent<InputAdapter>();
            input.Initialise(worldCamera);

            var planner = new GameObject("PathPlanner").AddComponent<PathPlanner>();
            planner.Initialise(input, landing);

            SpawnAircraft();
        }

        void SpawnAircraft()
        {
            var motion = new AircraftMotion(level.spawnPosition, level.spawnHeadingDegrees, aircraftSpeed, aircraftTurnRate);
            AircraftController aircraft = AircraftController.Spawn(motion, worldCamera, landing, shapeMaterial, aircraftColor, clearedToLandColor, edgeMargin);
            aircraft.Landed += OnAircraftLanded;
        }

        void OnAircraftLanded(AircraftController aircraft)
        {
            aircraft.Landed -= OnAircraftLanded;
            StartCoroutine(SpawnAfterDelay());
        }

        IEnumerator SpawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            SpawnAircraft();
        }
    }
}
