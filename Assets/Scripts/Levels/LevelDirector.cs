using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AirTrafficControl
{
    /// <summary>How aircraft fly and look. Set on PrototypeSetup in the scene.</summary>
    [Serializable]
    public sealed class AircraftSettings
    {
        public AircraftColors colors = AircraftColors.Default;
        public float speed = 1.2f;          // world units per second
        public float turnRate = 200f;       // degrees per second, only for the edge turn-back with no path
        public float edgeMargin = 0.8f;     // turn back this far inside the screen edge
    }

    /// <summary>
    /// Runs one level from <see cref="LevelData"/>: keeps the level's number of aircraft in the air, counts
    /// landings, and ends the attempt on the first collision (blueprint: collisions restart the level).
    /// Retry restarts at once (C3: under two seconds, no lives, no waiting).
    /// Spawn schedules and objectives will be added here once the level data schema is settled.
    /// </summary>
    public sealed class LevelDirector : MonoBehaviour
    {
        /// <summary>The crash stays on screen this long, frozen, before the collision panel appears.</summary>
        const float CrashPauseSeconds = 0.5f;
        const float RespawnDelay = 1f;      // seconds after a landing
        const float BusyRetryDelay = 0.5f;  // seconds to wait when every spawn point is busy

        readonly LevelSession session = new LevelSession();
        readonly List<Vector2> spawnPositions = new List<Vector2>();
        readonly List<Vector2> aircraftPositions = new List<Vector2>();
        readonly List<AircraftController> toRemove = new List<AircraftController>();

        LevelData level;
        Camera worldCamera;
        LandingSystem landing;
        AirspaceSafety safety;
        InputAdapter input;
        HudView hud;
        Material material;
        AircraftSettings aircraftSettings;
        int nextSpawnIndex;

        public LevelSession Session => session;

        public void Initialise(LevelData levelData, Camera camera, LandingSystem landingSystem, AirspaceSafety airspaceSafety,
                               InputAdapter inputAdapter, HudView hudView, Material shapeMaterial, AircraftSettings settings)
        {
            level = levelData;
            worldCamera = camera;
            landing = landingSystem;
            safety = airspaceSafety;
            input = inputAdapter;
            hud = hudView;
            material = shapeMaterial;
            aircraftSettings = settings;

            spawnPositions.Clear();
            foreach (SpawnPoint point in level.spawnPoints)
                spawnPositions.Add(point.position);

            safety.Collided += OnCollided;
            hud.RetryPressed += OnRetryPressed;
        }

        void OnDestroy()
        {
            if (safety != null) safety.Collided -= OnCollided;
            if (hud != null) hud.RetryPressed -= OnRetryPressed;
            Time.timeScale = 1f;
        }

        /// <summary>Starts a fresh attempt: counter at 0, the level's aircraft spawned, input on.</summary>
        public void StartLevel()
        {
            StopAllCoroutines();
            RemoveAllAircraft();

            session.Restart();
            hud.SetLanded(0);
            hud.HideCollision();
            Time.timeScale = 1f;
            input.enabled = true;

            nextSpawnIndex = 0;
            for (int i = 0; i < level.aircraftInAir; i++)
            {
                if (!TrySpawnNow())
                    StartCoroutine(SpawnWhenClear(BusyRetryDelay)); // more aircraft than clear spawn points
            }
        }

        void Update()
        {
            session.Tick(Time.deltaTime);
        }

        void OnCollided(AircraftController a, AircraftController b)
        {
            if (!session.RecordCollision())
                return;

            // Freeze everything so the crash can be seen, and stop drawing while the panel is up.
            StopAllCoroutines();
            Time.timeScale = 0f;
            input.enabled = false;
            StartCoroutine(ShowCollisionPanel());
        }

        IEnumerator ShowCollisionPanel()
        {
            yield return new WaitForSecondsRealtime(CrashPauseSeconds);
            hud.ShowCollision(session.ElapsedSeconds, session.LandedCount);
        }

        void OnRetryPressed()
        {
            var timer = Stopwatch.StartNew();
            StartLevel();
            // C3: restart after a collision takes under two seconds. Shown in the Xcode console.
            Debug.Log($"LevelDirector: restart took {timer.Elapsed.TotalMilliseconds:F1} ms");
        }

        void OnAircraftLanded(AircraftController aircraft)
        {
            aircraft.Landed -= OnAircraftLanded;
            session.RecordLanding();
            hud.SetLanded(session.LandedCount);
            StartCoroutine(SpawnWhenClear(RespawnDelay));
        }

        void RemoveAllAircraft()
        {
            toRemove.Clear();
            toRemove.AddRange(AircraftController.Active);
            foreach (AircraftController aircraft in toRemove)
            {
                // Deactivate first: it leaves the active list at once, so spawning straight after
                // does not see it. Destroy happens at the end of the frame.
                aircraft.gameObject.SetActive(false);
                Destroy(aircraft.gameObject);
            }
        }

        IEnumerator SpawnWhenClear(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            while (!TrySpawnNow())
                yield return new WaitForSeconds(BusyRetryDelay);
        }

        bool TrySpawnNow()
        {
            aircraftPositions.Clear();
            foreach (AircraftController other in AircraftController.Active)
                aircraftPositions.Add(other.transform.position);

            int index = SpawnRules.ChooseClearPoint(spawnPositions, aircraftPositions, nextSpawnIndex);
            if (index < 0)
                return false;

            nextSpawnIndex = index + 1;
            SpawnPoint point = level.spawnPoints[index];
            var motion = new AircraftMotion(point.position, point.headingDegrees, aircraftSettings.speed, aircraftSettings.turnRate);
            AircraftController aircraft = AircraftController.Spawn(motion, worldCamera, landing, material,
                                                                   aircraftSettings.colors, aircraftSettings.edgeMargin);
            aircraft.Landed += OnAircraftLanded;
            return true;
        }
    }
}
