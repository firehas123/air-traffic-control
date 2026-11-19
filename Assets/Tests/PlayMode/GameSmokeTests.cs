using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AirTrafficControl.Tests
{
    /// <summary>
    /// Smoke test of the real scene: the game starts on level select, level 1 builds and runs for a while, and
    /// leaving it works, all without an error in the log (Unity fails the test on any logged error). This catches
    /// runtime problems that the EditMode unit tests cannot, before the build goes to the iPad.
    /// </summary>
    public class GameSmokeTests
    {
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
            Assert.IsNotNull(Object.FindAnyObjectByType<LevelSelectView>(), "Starts on level select");

            flow.Play(0);
            yield return WaitRealSeconds(1f);
            Assert.Greater(AircraftController.Active.Count, 0, "Aircraft are in the air");
            Assert.IsNotNull(Object.FindAnyObjectByType<IslandScenery>(), "The island scenery is built");

            // Long enough for the first carrier (10 s in the test level) and the scenery to animate.
            yield return WaitRealSeconds(12f);
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
    }
}
