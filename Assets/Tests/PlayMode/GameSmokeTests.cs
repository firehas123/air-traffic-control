using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AirTrafficControl.Tests
{
    /// <summary>
    /// Smoke test of the real scene: the game starts on the home screen, Island Draw level 1 builds and runs for a while, and
    /// leaving it works, all without an error in the log (Unity fails the test on any logged error). This catches
    /// runtime problems that the EditMode unit tests cannot, before the build goes to the iPad.
    /// </summary>
    public class GameSmokeTests
    {
        static T Load<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
            return null;
#endif
        }

        static LevelData DrawLevel(string map, int number) => Load<LevelData>($"Assets/Levels/Draw/{map}-draw-0{number}.asset");

        [UnityTest]
        public IEnumerator DrawLevelsWithTheirMapRules_RunOnAllFiveMaps_WithoutErrors()
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;
            GameFlow flow = Object.FindAnyObjectByType<GameFlow>();

            // Level 2 of each map has its rule; Desert 3 adds the sandstorm and Island 5 the carrier.
            foreach (var (map, number) in new[] { ("island", 2), ("island", 5), ("city", 2), ("desert", 3), ("mountain", 2), ("arctic", 2) })
            {
                LevelData level = DrawLevel(map, number);
                Assert.IsNotNull(level, $"{map} {number}");
                flow.PlayLevelData(level);
                yield return WaitRealSeconds(2.5f);
                Assert.Greater(AircraftController.Active.Count, 0, $"{map} {number}: aircraft are in the air");
                Assert.IsNotNull(Object.FindAnyObjectByType<MapRuleSystem>(), $"{map} {number}: map rules run");
                foreach (AircraftController aircraft in AircraftController.Active)
                    Assert.AreEqual(level.rules.noFly.enabled, aircraft.Motion.NoFly != null, $"{map}: no-fly given to aircraft");
            }
            flow.ShowLevels();
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, 1e-5f);
        }

        [UnityTest]
        public IEnumerator Desert_RunningOutOfFuel_EndsTheLevel()
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;
            GameFlow flow = Object.FindAnyObjectByType<GameFlow>();
            flow.PlayLevelData(DrawLevel("desert", 2));
            yield return null;
            LevelDirector director = Object.FindAnyObjectByType<LevelDirector>();
            string failTitle = null;
            director.Failed += outcome => failTitle = outcome.FailTitle;

            // The second aircraft is the first low-fuel one; keep only it, so nothing collides, and drain its tank.
            AircraftController lowFuel = null;
            float end = Time.realtimeSinceStartup + 10f;
            while (lowFuel == null && Time.realtimeSinceStartup < end)
            {
                foreach (AircraftController aircraft in AircraftController.Active)
                    if (aircraft.FuelSecondsLeft.HasValue)
                        lowFuel = aircraft;
                yield return null;
            }
            Assert.IsNotNull(lowFuel, "A low-fuel aircraft with a timer appears");
            Assert.IsNotNull(lowFuel.GetComponentInChildren<FuelGaugeView>(), "Its fuel timer is visible");
            foreach (AircraftController aircraft in new System.Collections.Generic.List<AircraftController>(AircraftController.Active))
                if (aircraft != lowFuel)
                {
                    aircraft.gameObject.SetActive(false);
                    Object.Destroy(aircraft.gameObject);
                }
            lowFuel.SetLowFuel(0.3f, null);
            yield return WaitRealSeconds(1.5f);

            Assert.IsFalse(director.Session.IsPlaying, "Running out of fuel ends the attempt");
            Assert.AreEqual("Out of fuel", failTitle);
            flow.ShowLevels();
            yield return null;
        }

        static IEnumerator WaitRealSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
                yield return null;
        }

        [UnityTest]
        public IEnumerator Level1_BuildsAndRuns_WithoutErrors()
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            GameFlow flow = Object.FindAnyObjectByType<GameFlow>();
            Assert.IsNotNull(flow, "The scene has the game flow");
            Assert.IsNotNull(Object.FindAnyObjectByType<HomeView>(), "Starts on the home screen");
            Assert.IsTrue(Object.FindAnyObjectByType<HomeView>().IsShown);

            flow.ChooseMode(0, GameMode.Tower); // no Tower levels yet: a note, no level
            yield return null;
            Assert.IsNull(Object.FindAnyObjectByType<LevelDirector>());

            flow.ChooseMode(0, GameMode.Draw); // Island Draw
            flow.Play(0);
            yield return WaitRealSeconds(1f);
            Assert.Greater(AircraftController.Active.Count, 0, "Aircraft are in the air");
            Assert.IsNotNull(Object.FindAnyObjectByType<IslandScenery>(), "The island scenery is built");

            yield return WaitRealSeconds(4f);
            Assert.IsNotNull(Object.FindAnyObjectByType<LevelDirector>());

            flow.ShowLevels();
            yield return null;
            yield return null;
            Assert.AreEqual(0, AircraftController.Active.Count, "Leaving the level removes its aircraft");
            Assert.AreEqual(1f, Time.timeScale, 1e-5f);

            flow.Play(0);
            yield return WaitRealSeconds(2f);
            Assert.Greater(AircraftController.Active.Count, 0, "The level can be played again");
            flow.ShowLevels();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Departure_TaxisTakesOffAndIsCounted_WithoutErrors()
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;
            GameFlow flow = Object.FindAnyObjectByType<GameFlow>();
            // Draw mode has no ground control (phase 2); the ground code is kept for Tower mode (phase 3) and tried on
            // the old test level, IslandPrototype.asset.
            flow.PlayLevelData(Load<LevelData>("Assets/Levels/IslandPrototype.asset"));
            yield return null;
            LevelDirector director = Object.FindAnyObjectByType<LevelDirector>();
            Assert.AreEqual(3, director.Session.DeparturesToClear, "The test level asks for 3 departures");

            // Keep the sky clear of arrivals so nothing collides, and act as the player for the departures.
            float end = Time.realtimeSinceStartup + 60f;
            bool sawGate = false, sawCrossing = false, sawHoldLine = false;
            while (Time.realtimeSinceStartup < end && director.Session.DepartureCount < 1)
            {
                foreach (AircraftController aircraft in new System.Collections.Generic.List<AircraftController>(AircraftController.Active))
                {
                    if (aircraft.IsControllableInAir)
                    {
                        aircraft.gameObject.SetActive(false);
                        Object.Destroy(aircraft.gameObject);
                        continue;
                    }
                    if (aircraft.Ground == null)
                        continue;
                    switch (aircraft.Ground.Phase)
                    {
                        case GroundPhase.AtGate: sawGate = true; aircraft.GiveGroundCommand(AircraftCommand.Taxi); break;
                        case GroundPhase.HoldingAtCrossing: sawCrossing = true; aircraft.GiveGroundCommand(AircraftCommand.Cross); break;
                        case GroundPhase.HoldingShort: sawHoldLine = true; aircraft.GiveGroundCommand(AircraftCommand.TakeOff); break;
                    }
                }
                yield return null;
            }

            Assert.IsTrue(sawGate, "A departure appeared at a gate");
            Assert.IsTrue(sawHoldLine, "It taxied to the hold line");
            Assert.GreaterOrEqual(director.Session.DepartureCount, 1, "It took off, left the screen and was counted");
            TestContext.WriteLine($"Crossing point used: {sawCrossing}");
            flow.ShowLevels();
            yield return null;
        }
    }
}
