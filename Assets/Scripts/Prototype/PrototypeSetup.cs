using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// M1 prototype only: builds the map from <see cref="LevelData"/>, creates the input adapter, path planner,
    /// landing system and airspace safety, and keeps <see cref="LevelData.aircraftInAir"/> aircraft in the air
    /// (a new one appears after each landing or crash). It will be replaced by LevelDirector, which will spawn
    /// aircraft from a schedule in the level data.
    /// </summary>
    public sealed class PrototypeSetup : MonoBehaviour
    {
        [SerializeField] LevelData level;
        [SerializeField] Material shapeMaterial;
        [SerializeField] AircraftColors aircraftColors = AircraftColors.Default;
        [SerializeField] float aircraftSpeed = 1.2f;          // world units per second
        [SerializeField] float aircraftTurnRate = 200f;       // degrees per second, only for the edge turn-back with no path
        [SerializeField] float edgeMargin = 0.8f;             // turn back this far inside the screen edge
        [SerializeField] float respawnDelay = 1f;             // seconds after a landing or crash
        [SerializeField] float busyRetryDelay = 0.5f;         // seconds to wait when every spawn point is busy

        readonly List<Vector2> spawnPositions = new List<Vector2>();
        readonly List<Vector2> aircraftPositions = new List<Vector2>();
        Camera worldCamera;
        LandingSystem landing;
        int nextSpawnIndex;

        void Awake()
        {
            // Mobile platforms default to 30 FPS. Drawing feels much smoother at 60.
            Application.targetFrameRate = 60;
        }

        void Start()
        {
            worldCamera = Camera.main;

            if (level == null || level.spawnPoints.Count == 0)
            {
                Debug.LogError("PrototypeSetup: no LevelData or no spawn points. Run Tools > Air Traffic Control > Set Up Prototype Scene.");
                return;
            }

            MeshBuilder.CreateObject("Island", MapShapes.CreateIslandMesh(level.islandRadii), shapeMaterial,
                MapShapes.IslandSortingOrder, level.islandCentre, 0f);

            landing = new GameObject("LandingSystem").AddComponent<LandingSystem>();
            landing.Initialise(level.runways, shapeMaterial);

            new GameObject("AirspaceSafety").AddComponent<AirspaceSafety>();

            var input = new GameObject("InputAdapter").AddComponent<InputAdapter>();
            input.Initialise(worldCamera);

            var planner = new GameObject("PathPlanner").AddComponent<PathPlanner>();
            planner.Initialise(input, landing);

            foreach (SpawnPoint point in level.spawnPoints)
                spawnPositions.Add(point.position);

            for (int i = 0; i < level.aircraftInAir; i++)
                StartCoroutine(SpawnWhenClear(0f));
        }

        IEnumerator SpawnWhenClear(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            while (true)
            {
                aircraftPositions.Clear();
                foreach (AircraftController aircraft in AircraftController.Active)
                    aircraftPositions.Add(aircraft.transform.position);

                int index = SpawnRules.ChooseClearPoint(spawnPositions, aircraftPositions, nextSpawnIndex);
                if (index >= 0)
                {
                    nextSpawnIndex = index + 1;
                    Spawn(level.spawnPoints[index]);
                    yield break;
                }
                yield return new WaitForSeconds(busyRetryDelay);
            }
        }

        void Spawn(SpawnPoint point)
        {
            var motion = new AircraftMotion(point.position, point.headingDegrees, aircraftSpeed, aircraftTurnRate);
            AircraftController aircraft = AircraftController.Spawn(motion, worldCamera, landing, shapeMaterial, aircraftColors, edgeMargin);
            aircraft.Landed += OnAircraftGone;
            aircraft.Crashed += OnAircraftGone;
        }

        void OnAircraftGone(AircraftController aircraft)
        {
            aircraft.Landed -= OnAircraftGone;
            aircraft.Crashed -= OnAircraftGone;
            StartCoroutine(SpawnWhenClear(respawnDelay));
        }
    }
}
