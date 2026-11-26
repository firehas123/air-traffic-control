using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    /// <summary>
    /// Phase 4 final check (blueprint 16, 26 Nov 2026): all 50 levels of maps 1 to 5 in both modes, read through the map
    /// catalog the game uses, and the unlock rules end to end on the real catalog with a fresh save and an upgraded
    /// version 1 save.
    /// </summary>
    public class FinalCheckLevelTests
    {
        static readonly string[] ReadyMaps = { "island", "city", "desert", "mountain", "arctic" };

        static MapCatalog Catalog() => AssetDatabase.LoadAssetAtPath<MapCatalog>("Assets/Maps/MapCatalog.asset");

        static IEnumerable<(MapEntry map, GameMode mode, int number, LevelEntry entry)> AllLevels()
        {
            MapCatalog catalog = Catalog();
            foreach (string id in ReadyMaps)
            {
                MapEntry map = catalog.Find(id);
                foreach (GameMode mode in GameModes.All)
                    for (int i = 0; i < map.Levels(mode).Count; i++)
                        yield return (map, mode, i + 1, map.Levels(mode)[i]);
            }
        }

        static float Estimate(LevelData level) =>
            level.mode == GameMode.Tower ? TowerModeSetup.EstimateSeconds(level) : SessionLengthEstimate.Seconds(level, 0.6f);

        [Test]
        public void Catalog_HasFiftyPlayableLevels_AndMaps6And7AreComingSoon()
        {
            MapCatalog catalog = Catalog();
            Assert.AreEqual(7, catalog.maps.Count);
            int count = 0;
            foreach (var (map, mode, number, entry) in AllLevels())
            {
                string name = $"{map.id} {mode} {number}";
                Assert.IsTrue(map.gameplayReady, name);
                Assert.AreEqual(MapInfo.LevelId(map.id, mode, number), entry.id, name);
                Assert.IsNotNull(entry.data, $"{name}: the level asset loads");
                Assert.AreEqual(entry.id, entry.data.name, name);
                Assert.AreEqual(mode, entry.data.mode, name);
                Assert.AreEqual(map.id, entry.data.mapId, name);
                Assert.IsNotNull(entry.data.mapArt.sprite, $"{name}: has its painted picture");
                Assert.AreSame(mode == GameMode.Tower ? map.towerPicture : map.drawPicture, entry.data.mapArt.sprite, name);
                Assert.AreSame(mode == GameMode.Tower ? map.towerLayout : map.drawLayout, entry.data.layout, name);
                Assert.IsFalse(string.IsNullOrEmpty(entry.title), name);
                count++;
            }
            Assert.AreEqual(50, count, "5 maps x 2 modes x 5 levels");

            foreach (string id in new[] { "military", "spaceport" })
            {
                MapEntry map = catalog.Find(id);
                Assert.IsFalse(map.gameplayReady, $"{id}: Coming soon");
                Assert.IsTrue(map.drawLevels.TrueForAll(l => l.data == null) && map.towerLevels.TrueForAll(l => l.data == null), id);
            }
        }

        [Test]
        public void EveryLevel_HasAReachableGoal()
        {
            var problems = new List<string>();
            foreach (var (map, mode, number, entry) in AllLevels())
            {
                LevelData level = entry.data;
                string name = entry.id;
                void Check(bool ok, string what) { if (!ok) problems.Add($"{name}: {what}"); }

                Check(level.landingsToClear > 0, "has a landing goal");
                Check(level.aircraftTypes.Count > 0 && !level.aircraftTypes.Contains(null), "has aircraft kinds");
                Check(level.spawnPoints.Count > 0, "has spawn points");
                Check(level.aircraftInAir >= 1 && level.aircraftInAir <= level.landingsToClear, "aircraft in the air within the goal");

                foreach (AircraftType kind in level.aircraftTypes)
                {
                    if (kind == null)
                        continue;
                    if (kind.landsOn == LandingZoneKind.Runway)
                    {
                        bool usable = false;
                        foreach (RunwayDefinition r in level.runways)
                        {
                            Runway runway = r.ToRunway();
                            usable |= mode == GameMode.Draw || level.tower.AllowsRunway(runway) || level.tower.AllowsRunway(runway.Reversed());
                        }
                        Check(usable, $"{kind.displayName} has a runway it may land on");
                    }
                    else
                    {
                        Check(level.landingPads.Exists(p => p.kind == kind.landsOn), $"{kind.displayName} has a {kind.landsOn}");
                    }
                }

                if (mode == GameMode.Draw)
                {
                    Check(level.departuresToClear == 0, "Draw mode has no departures");
                    Check(level.carrier.hasCarrier == (map.id == "island" && number == 5), "carrier only on Island Draw 5");
                }
                else
                {
                    Check(level.departuresToClear > 0, "Tower mode counts departures");
                    Check(level.aircraftTypes.Exists(k => k != null && k.landsOn == LandingZoneKind.Runway), "a runway kind for departures");
                    Check(level.tower.approaches.Count > 0, "has approach routes");
                    Check(!level.carrier.hasCarrier, "no carrier in Tower mode");
                    foreach (TowerApproach approach in level.tower.approaches)
                    {
                        Check(approach.runway >= 0 && approach.runway < level.runways.Count, "approach to an existing runway");
                        if (approach.runway < 0 || approach.runway >= level.runways.Count)
                            continue;
                        Runway runway = level.runways[approach.runway].ToRunway();
                        Check(level.tower.AllowsRunway(approach.endB ? runway.Reversed() : runway), "approach to an allowed runway end");
                    }

                    // Departures: every stand reaches the hold line of at least one allowed runway end.
                    TaxiNetwork network = TaxiNetwork.Build(level.layout);
                    Check(network.StandCount > 0, "has stands");
                    var holds = new List<int>();
                    for (int r = 0; r < level.runways.Count; r++)
                        foreach (bool endB in new[] { false, true })
                        {
                            Runway runway = level.runways[r].ToRunway();
                            if (level.tower.AllowsRunway(endB ? runway.Reversed() : runway))
                                holds.AddRange(network.EndHoldNodes(r, endB));
                        }
                    for (int s = 0; s < network.StandCount; s++)
                        Check(network.FindRoute(network.StandNode(s), holds) != null, $"stand {s + 1} can taxi to a runway");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EveryLevel_IsEstimatedAt60To120Seconds_WithTwoStarsReachable()
        {
            var lines = new List<string>();
            var wrong = new List<string>();
            foreach (var (map, mode, number, entry) in AllLevels())
            {
                LevelData level = entry.data;
                float seconds = Estimate(level);
                string line = $"{entry.id}: land {level.landingsToClear}, launch {level.departuresToClear}, {seconds:F0} s, 2 stars within {level.result.twoStarSeconds:F0} s";
                lines.Add(line);
                if (seconds < 60f || seconds > 120f || level.result.twoStarSeconds < seconds)
                    wrong.Add(line);
            }
            TestContext.WriteLine(string.Join("\n", lines));
            Assert.IsEmpty(wrong, "C3: 60 to 120 s, with 2 stars reachable at the estimate:\n" + string.Join("\n", wrong));
        }

        [Test]
        public void EachMapsRule_IsOffInLevel1_AndOnFromLevel2_InBothModes()
        {
            var wrong = new List<string>();
            foreach (var (map, mode, number, entry) in AllLevels())
            {
                LevelData level = entry.data;
                bool on = number >= 2;
                bool tower = mode == GameMode.Tower;
                var flags = new Dictionary<string, (bool actual, bool expected)>
                {
                    ["wind"] = (level.wind.hasWind, map.id == "island" && on),
                    ["closures"] = (level.rules.closures.enabled, map.id == "city" && on),
                    ["low fuel"] = (level.rules.fuel.enabled, map.id == "desert" && on),
                    ["sandstorm"] = (level.rules.sandstorm.enabled, map.id == "desert" && number >= 3),
                    ["mountains (Draw no-fly)"] = (level.rules.noFly.enabled, !tower && map.id == "mountain" && on),
                    ["valley only (Tower)"] = (level.tower.valleyOnly, tower && map.id == "mountain" && on),
                    ["ice"] = (level.rules.ice.enabled, map.id == "arctic" && on),
                };
                foreach (var pair in flags)
                    if (pair.Value.actual != pair.Value.expected)
                        wrong.Add($"{entry.id}: {pair.Key} is {(pair.Value.actual ? "on" : "off")}");
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }
    }

    /// <summary>Unlocks end to end on the real map catalog (blueprint 16.3), with the save written and read back each time.</summary>
    public class FinalCheckUnlockTests
    {
        string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "atc-final-check-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        string SavePath => Path.Combine(folder, ProgressionSave.FileName);

        static MapCatalog Catalog() => AssetDatabase.LoadAssetAtPath<MapCatalog>("Assets/Maps/MapCatalog.asset");

        /// <summary>Loads the save from disk the way the game does on start.</summary>
        Progression Reload(MapCatalog catalog, out ProgressionSave save)
        {
            save = ProgressionSave.Load(SavePath);
            return new Progression(save.Data, catalog.ToInfo(), catalog.xp);
        }

        /// <summary>Clears a level with its own goal, as the result screen records it, then writes the save.</summary>
        static void Clear(Progression p, ProgressionSave save, LevelEntry entry, int stars, int warnings)
        {
            Assert.IsTrue(p.IsLevelUnlocked(entry.id), $"{entry.id} is open before it is played");
            p.RecordClear(entry.id, stars, 80f, entry.data.landingsToClear, entry.data.departuresToClear, warnings, entry.data.result);
            Assert.IsTrue(save.Write(), "Save written");
        }

        static List<string> OpenLevels(Progression p, MapCatalog catalog)
        {
            var open = new List<string>();
            for (int m = 0; m < catalog.maps.Count; m++)
                foreach (GameMode mode in GameModes.All)
                    for (int i = 0; i < catalog.maps[m].Levels(mode).Count; i++)
                        if (p.IsLevelUnlocked(m, mode, i))
                            open.Add(catalog.maps[m].Levels(mode)[i].id);
            return open;
        }

        [Test]
        public void FreshSave_OpensEveryMapInOrder_ByPlayingBothModes()
        {
            MapCatalog catalog = Catalog();
            Progression p = Reload(catalog, out ProgressionSave save);
            CollectionAssert.AreEquivalent(new[] { "island-draw-01", "island-tower-01" }, OpenLevels(p, catalog),
                "A fresh save opens only the first level of each Island mode");
            Assert.AreEqual("Trainee", ControllerRank.Name(p.Xp));

            var report = new List<string>();
            for (int m = 0; m < 5; m++)
            {
                MapEntry map = catalog.maps[m];
                Assert.IsTrue(p.IsMapUnlocked(m), $"{map.displayName} is open when its turn comes");
                int xpBefore = p.Xp;
                foreach (GameMode mode in GameModes.All)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        // Level i+1 is open only after level i is cleared, in the same mode.
                        if (i + 1 < 5)
                            Assert.IsFalse(p.IsLevelUnlocked(m, mode, i + 1), $"{map.Levels(mode)[i + 1].id} waits for {map.Levels(mode)[i].id}");
                        Clear(p, save, map.Levels(mode)[i], stars: 2, warnings: 1);
                        p = Reload(catalog, out save); // every clear survives a restart

                        // The next map waits for level 5 (either mode) AND its XP.
                        if (m + 1 < 5 && i < 4 && mode == GameMode.Draw)
                            Assert.IsFalse(p.IsMapUnlocked(m + 1), $"{catalog.maps[m + 1].displayName} waits for {map.displayName} level 5");
                    }
                }
                if (m + 1 < 5)
                    TestContext.WriteLine($"{catalog.maps[m + 1].displayName} open: {p.IsMapUnlocked(m + 1)}");
                report.Add($"{map.displayName}: both modes with 2 stars and a warning each = {p.Xp - xpBefore} XP (total {p.Xp}, {ControllerRank.Name(p.Xp)})");
            }
            TestContext.WriteLine(string.Join("\n", report));

            Assert.IsFalse(p.IsMapUnlocked(5), "Military Base: Coming soon");
            Assert.IsFalse(p.IsMapUnlocked(6), "Spaceport: Coming soon");
            Assert.AreEqual("Coming soon", p.MapLockFor(5).Describe(Progression.LevelThatOpensNextMap));
            Assert.AreEqual(50, OpenLevels(p, catalog).Count, "All 50 built levels are open at the end");
        }

        [Test]
        public void XpPerMap_IsReported_ForThePlanningDoc()
        {
            // Blueprint 16.3 says a first play of one map gives about 1,500 XP. This prints what the level data gives, per
            // mode, for the best case (3 stars, no warning) and an average case (2 stars, one warning): not an assert,
            // because XP values and unlock XP are PROPOSED and the owner decides.
            MapCatalog catalog = Catalog();
            var lines = new List<string>();
            for (int m = 0; m < 5; m++)
            {
                MapEntry map = catalog.maps[m];
                foreach (GameMode mode in GameModes.All)
                {
                    int best = 0, average = 0;
                    foreach (LevelEntry entry in map.Levels(mode))
                    {
                        best += catalog.xp.ForClear(entry.data.landingsToClear, entry.data.departuresToClear, 3, 0);
                        average += catalog.xp.ForClear(entry.data.landingsToClear, entry.data.departuresToClear, 2, 1);
                    }
                    lines.Add($"{map.displayName} {mode}: {average} XP (2 stars, warnings) to {best} XP (3 stars, no warnings)");
                }
                lines.Add($"   next map needs {(m + 1 < catalog.maps.Count ? catalog.maps[m + 1].xpToUnlock : 0)} XP in total");
            }
            TestContext.WriteLine(string.Join("\n", lines));
            Assert.Pass();
        }

        [Test]
        public void OneModeOnly_WithThreeStars_StillOpensCity()
        {
            // A player who never tries Tower mode: Island Draw with 3 stars and no warnings opens City.
            MapCatalog catalog = Catalog();
            Progression p = Reload(catalog, out ProgressionSave save);
            foreach (LevelEntry entry in catalog.maps[0].drawLevels)
                Clear(p, save, entry, stars: 3, warnings: 0);
            p = Reload(catalog, out save);
            TestContext.WriteLine($"Island Draw only, 3 stars: {p.Xp} XP");
            Assert.IsTrue(p.IsMapUnlocked(1), $"City opens with {p.Xp} XP");
            Assert.IsTrue(p.IsLevelUnlocked("city-draw-01") && p.IsLevelUnlocked("city-tower-01"), "Both City modes open together");
        }

        [Test]
        public void UpgradedVersionOneSave_KeepsIslandProgress_AndCarriesOnIntoCity()
        {
            // A save from the M3 build on the iPad (version 1, ten Island levels).
            File.WriteAllText(SavePath,
                "{\"version\": 1, \"coins\": 410, \"unlocked\": [\"island-02\", \"island-03\", \"island-04\", \"island-05\", \"island-06\"], \"levels\": [" +
                "{\"id\": \"island-01\", \"stars\": 3, \"bestSeconds\": 70, \"timesCleared\": 3}," +
                "{\"id\": \"island-02\", \"stars\": 2, \"bestSeconds\": 95, \"timesCleared\": 1}," +
                "{\"id\": \"island-03\", \"stars\": 2, \"bestSeconds\": 99, \"timesCleared\": 1}," +
                "{\"id\": \"island-04\", \"stars\": 1, \"bestSeconds\": 118, \"timesCleared\": 1}," +
                "{\"id\": \"island-05\", \"stars\": 3, \"bestSeconds\": 81, \"timesCleared\": 2}]}");

            MapCatalog catalog = Catalog();
            Progression p = Reload(catalog, out ProgressionSave save);
            Assert.IsFalse(save.IsReadOnly);
            Assert.AreEqual(SaveData.CurrentVersion, p.Data.version);
            Assert.AreEqual(410, p.Coins, "Coins kept");
            Assert.AreEqual((3 + 2 + 2 + 1 + 3) * 25, p.Xp, "25 XP per kept star");
            for (int n = 1; n <= 5; n++)
                Assert.IsTrue(p.IsCleared(MapInfo.LevelId("island", GameMode.Draw, n)), $"Island Draw {n} stays cleared");
            Assert.IsTrue(p.IsLevelUnlocked("island-tower-01"), "Tower mode is open from the start");
            Assert.IsFalse(p.IsLevelUnlocked("island-tower-02"));

            // Island Draw 5 is cleared, so City only waits for XP: 1,000 - 275.
            MapLock city = p.MapLockFor(1);
            Assert.IsFalse(city.Unlocked);
            Assert.IsFalse(city.NeedsPreviousLevel, "Island level 5 counts as cleared after the upgrade");
            Assert.AreEqual(1000 - 275, city.XpMissing);
            StringAssert.StartsWith("Earn 725 more XP", city.Describe(Progression.LevelThatOpensNextMap));

            // Replaying an upgraded level pays no stars again; Tower levels earn the rest.
            int before = p.Xp;
            LevelEntry draw1 = catalog.maps[0].drawLevels[0];
            Clear(p, save, draw1, stars: 3, warnings: 1);
            Assert.AreEqual(draw1.data.landingsToClear * catalog.xp.perLanding, p.Xp - before, "Only landings pay on a replay");

            int played = 0;
            foreach (LevelEntry entry in catalog.maps[0].towerLevels)
            {
                if (p.IsMapUnlocked(1))
                    break;
                Clear(p, save, entry, stars: 2, warnings: 1);
                p = Reload(catalog, out save);
                played++;
            }
            TestContext.WriteLine($"Upgraded save opened City after {played} Tower levels, at {p.Xp} XP");
            Assert.IsTrue(p.IsMapUnlocked(1), "The upgraded player reaches City");
            Assert.IsFalse(p.IsMapUnlocked(2), "Desert still waits");
        }
    }
}
