using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AirTrafficControl.Tests
{
    /// <summary>
    /// Phase 4 final check (26 Nov 2026): every one of the 50 levels of maps 1 to 5 builds in the real scene, runs with
    /// aircraft, shows its painted picture, and draws no code-drawn scenery or removed animation over it. Unity fails the
    /// test on any error in the log.
    /// </summary>
    public class FinalCheckPlayTests
    {
        // Objects and meshes that only exist for the old code-drawn island or for animations the owner removed
        // (20 and 25 Nov 2026): sea marks, foam, rocks, palms, terminal, taxiways, parked planes, clouds, birds, boats,
        // the full code-drawn runway and pads. Checked by mesh: on a picture the Runway, Helipad and BalloonField objects
        // carry only the overlay (RunwayOverlay, PaintedPad or the H letter).
        static readonly HashSet<string> Forbidden = new HashSet<string>
        {
            "Island", "SeaFar", "SeaNear", "SeaWaves", "Foam", "Rocks", "Apron", "Terminal", "ControlTower", "ParkedPlane",
            "Taxiways", "PalmTrunk", "PalmFronds", "Cloud", "CloudShadow", "Bird", "Boat", "CarrierHull", "Runway",
            "Helipad", "BalloonField", "Contrail", "Strobe", "Afterburner", "SpeedTrail",
        };

        [UnityTest]
        public IEnumerator AllFiftyLevels_Build_Run_AndDrawNoSceneryOverThePicture()
        {
            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;
            GameFlow flow = Object.FindAnyObjectByType<GameFlow>();
#if UNITY_EDITOR
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<MapCatalog>("Assets/Maps/MapCatalog.asset");
#else
            MapCatalog catalog = null;
#endif
            Assert.IsNotNull(catalog);
            var problems = new List<string>();
            int played = 0;
            for (int m = 0; m < 5; m++)
                foreach (GameMode mode in GameModes.All)
                    foreach (LevelEntry entry in catalog.maps[m].Levels(mode))
                    {
                        flow.PlayLevelData(entry.data);
                        Time.timeScale = 2f;
                        float end = Time.realtimeSinceStartup + 1.2f;
                        while (Time.realtimeSinceStartup < end)
                            yield return null;
                        played++;

                        if (AircraftController.Active.Count == 0)
                            problems.Add($"{entry.id}: no aircraft after 1.2 s");
                        LevelDirector director = Object.FindAnyObjectByType<LevelDirector>();
                        if (director == null || !director.Session.IsPlaying)
                            problems.Add($"{entry.id}: the level is not running");

                        bool picture = false;
                        foreach (SpriteRenderer sprite in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                            picture |= sprite.sprite == entry.data.mapArt.sprite;
                        if (!picture)
                            problems.Add($"{entry.id}: its painted picture is not shown");

                        foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                        {
                            string mesh = filter.sharedMesh != null ? filter.sharedMesh.name : "";
                            if (Forbidden.Contains(mesh) || (filter.gameObject.name.StartsWith("Sea") || filter.gameObject.name.StartsWith("Palm")))
                                problems.Add($"{entry.id}: code-drawn '{filter.gameObject.name}' ({mesh}) over the picture");
                        }
                        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                            if (t.name == "Windsock" && !entry.data.wind.hasWind)
                                problems.Add($"{entry.id}: windsock without the wind rule");
                    }
            Time.timeScale = 1f;
            flow.ShowLevels();
            yield return null;
            Assert.AreEqual(50, played);
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
