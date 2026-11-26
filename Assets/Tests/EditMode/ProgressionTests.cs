using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    /// <summary>ProgressionSave and the progression rules (M3 step 1, blueprint 16.3, C8, C9, C11).</summary>
    public class ProgressionTests
    {
        // Seven maps as in the catalog: five levels per mode, XP to open (16.3), the last two not built yet.
        static List<MapInfo> Maps()
        {
            var list = new List<MapInfo>();
            (string id, string name, int xp, bool ready)[] maps =
            {
                ("island", "Island", 0, true), ("city", "City", 1000, true), ("desert", "Desert", 2500, true),
                ("mountain", "Mountain Valley", 4000, true), ("arctic", "Arctic", 5500, true),
                ("military", "Military Base", 7000, false), ("spaceport", "Spaceport", 8500, false),
            };
            foreach (var m in maps)
            {
                var info = new MapInfo { id = m.id, name = m.name, xpToUnlock = m.xp, gameplayReady = m.ready };
                for (int n = 1; n <= 5; n++)
                {
                    info.drawLevelIds.Add(MapInfo.LevelId(m.id, GameMode.Draw, n));
                    info.towerLevelIds.Add(MapInfo.LevelId(m.id, GameMode.Tower, n));
                }
                list.Add(info);
            }
            return list;
        }

        static Progression New(SaveData data = null) => new Progression(data ?? new SaveData(), Maps());

        const string Draw1 = "island-draw-01";
        const string Draw2 = "island-draw-02";
        const string Draw3 = "island-draw-03";
        static readonly ResultSettings Rules = new ResultSettings(); // 5 per landing, 5 per departure, 10 per new star

        string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "atc-save-tests-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        string SavePath => Path.Combine(folder, ProgressionSave.FileName);

        // ---- Levels ----

        [Test]
        public void LevelIds_SayMapModeAndNumber()
        {
            Assert.AreEqual("island-draw-01", MapInfo.LevelId("island", GameMode.Draw, 1));
            Assert.AreEqual("city-tower-05", MapInfo.LevelId("city", GameMode.Tower, 5));
        }

        [Test]
        public void FreshSave_OnlyTheFirstLevelOfEachIslandModeIsOpen()
        {
            Progression p = New();
            Assert.IsTrue(p.IsLevelUnlocked(0, GameMode.Draw, 0));
            Assert.IsTrue(p.IsLevelUnlocked(0, GameMode.Tower, 0), "Both Island modes are open from the start");
            Assert.IsFalse(p.IsLevelUnlocked(0, GameMode.Draw, 1));
            Assert.IsFalse(p.IsLevelUnlocked(0, GameMode.Tower, 1));
            Assert.IsFalse(p.IsLevelUnlocked(0, GameMode.Draw, 5), "Outside the list");
            Assert.IsFalse(p.IsLevelUnlocked(1, GameMode.Draw, 0), "City is locked");
        }

        [Test]
        public void ClearingALevel_OpensTheNextInTheSameModeOnly_WithAnyNumberOfStars()
        {
            Progression p = New();
            LevelRewards rewards = p.RecordClear(Draw1, 1, 110f, 8, 0, 1, Rules);
            Assert.IsTrue(rewards.UnlockedNext);
            Assert.IsTrue(p.IsLevelUnlocked(0, GameMode.Draw, 1), "C11: stars never block the campaign");
            Assert.IsFalse(p.IsLevelUnlocked(0, GameMode.Draw, 2));
            Assert.IsFalse(p.IsLevelUnlocked(0, GameMode.Tower, 1), "Tower has its own order");
            Assert.IsTrue(p.IsLevelUnlocked(Draw2));
        }

        [Test]
        public void FirstTimeStars_PayCoinsOnlyOnce()
        {
            Progression p = New();
            LevelRewards first = p.RecordClear(Draw1, 2, 90f, 8, 0, 1, Rules);
            Assert.AreEqual(2, first.NewStars);
            Assert.AreEqual(8 * 5 + 2 * 10, first.CoinsEarned);
            LevelRewards same = p.RecordClear(Draw1, 2, 95f, 8, 0, 1, Rules);
            Assert.AreEqual(0, same.NewStars);
            Assert.AreEqual(8 * 5, same.CoinsEarned);
            LevelRewards better = p.RecordClear(Draw1, 3, 80f, 8, 0, 1, Rules);
            Assert.AreEqual(1, better.NewStars);
            LevelRewards worse = p.RecordClear(Draw1, 1, 130f, 8, 0, 1, Rules);
            Assert.AreEqual(0, worse.NewStars);
            Assert.AreEqual(3, p.StarsFor(Draw1), "Best stars are kept");
        }

        [Test]
        public void BestTime_IsTheFastestClear()
        {
            Progression p = New();
            Assert.IsTrue(p.RecordClear(Draw1, 1, 100f, 8, 0, 1, Rules).NewBestTime);
            Assert.IsFalse(p.RecordClear(Draw1, 1, 120f, 8, 0, 1, Rules).NewBestTime);
            Assert.IsTrue(p.RecordClear(Draw1, 1, 75f, 8, 0, 1, Rules).NewBestTime);
            Assert.AreEqual(75f, p.BestSecondsFor(Draw1), 1e-4f);
        }

        // ---- XP (16.3) ----

        [Test]
        public void Xp_TenPerLandingAndDeparture_25PerNewStar_50WithoutAWarning()
        {
            Progression p = New();
            LevelRewards first = p.RecordClear(Draw1, 3, 70f, 6, 3, 0, Rules);
            Assert.AreEqual(6 * 10 + 3 * 10 + 3 * 25 + 50, first.XpEarned);
            Assert.AreEqual(first.XpEarned, p.Xp);
            Assert.AreEqual(6, p.Data.totalLandings);
            Assert.AreEqual(3, p.Data.totalDepartures);
        }

        [Test]
        public void Xp_StarsNeverPayTwice_AndWarningsLoseTheBonus()
        {
            Progression p = New();
            p.RecordClear(Draw1, 3, 70f, 8, 0, 0, Rules);
            LevelRewards replay = p.RecordClear(Draw1, 3, 70f, 8, 0, 2, Rules);
            Assert.AreEqual(8 * 10, replay.XpEarned, "No star XP the second time, no bonus with warnings");
            LevelRewards departures = p.RecordClear(Draw1, 1, 70f, 0, 2, 0, Rules);
            Assert.AreEqual(2 * 10 + 50, departures.XpEarned);
        }

        [Test]
        public void ControllerRank_FollowsXp()
        {
            Assert.AreEqual("Trainee", ControllerRank.Name(0));
            Assert.AreEqual("Trainee", ControllerRank.Name(999));
            Assert.AreEqual("Controller", ControllerRank.Name(1000));
            Assert.AreEqual("Senior Controller", ControllerRank.Name(4000));
            Assert.AreEqual("Tower Chief", ControllerRank.Name(20000));
            Assert.AreEqual(0.5f, ControllerRank.ProgressToNext(500), 1e-5f);
            Assert.AreEqual(1f, ControllerRank.ProgressToNext(9000));
            Assert.AreEqual(250, ControllerRank.XpToNext(750));
            Assert.AreEqual(0, ControllerRank.XpToNext(9000));
        }

        // ---- Maps (16.3) ----

        static void ClearLevel5(Progression p, string mapId, GameMode mode)
        {
            for (int n = 1; n <= 5; n++)
                p.RecordClear(MapInfo.LevelId(mapId, mode, n), 1, 100f, 0, 0, 1, Rules);
        }

        [Test]
        public void City_NeedsIslandLevel5_AndAThousandXp()
        {
            Progression p = New();
            Assert.IsTrue(p.IsMapUnlocked(0), "Island is open from the start");
            MapLock fresh = p.MapLockFor(1);
            Assert.IsFalse(fresh.Unlocked);
            Assert.IsTrue(fresh.NeedsPreviousLevel);
            Assert.AreEqual(1000, fresh.XpMissing);
            StringAssert.StartsWith("Clear Island level 5 (Draw or Tower) and earn ", fresh.Describe(5));
            StringAssert.EndsWith(" more XP", fresh.Describe(5));

            ClearLevel5(p, "island", GameMode.Draw); // landings 0, so little XP
            MapLock cleared = p.MapLockFor(1);
            Assert.IsFalse(cleared.Unlocked, "Level 5 alone is not enough");
            Assert.IsFalse(cleared.NeedsPreviousLevel);
            Assert.Greater(cleared.XpMissing, 0);
            StringAssert.StartsWith("Earn ", cleared.Describe(5));

            p.Data.xp = 1000;
            Assert.IsTrue(p.IsMapUnlocked(1), "Level 5 and 1,000 XP open City");
            Assert.IsTrue(p.IsLevelUnlocked(1, GameMode.Draw, 0));
            Assert.IsTrue(p.IsLevelUnlocked(1, GameMode.Tower, 0), "Both modes of the new map");
            Assert.IsFalse(p.IsMapUnlocked(2), "Desert still needs City level 5");
        }

        [Test]
        public void XpAlone_NeverOpensAMap()
        {
            Progression p = New(new SaveData { xp = 100000 });
            Assert.IsFalse(p.IsMapUnlocked(1));
            Assert.IsTrue(p.MapLockFor(1).NeedsPreviousLevel);
            Assert.AreEqual(0, p.MapLockFor(1).XpMissing);
        }

        [Test]
        public void TowerLevel5_OpensTheNextMapToo()
        {
            Progression p = New(new SaveData { xp = 1000 });
            ClearLevel5(p, "island", GameMode.Tower);
            Assert.IsTrue(p.IsMapUnlocked(1), "Either mode counts");
        }

        [Test]
        public void EachMapNeedsItsOwnXp_InOrder()
        {
            int[] xp = { 0, 1000, 2500, 4000, 5500 };
            Progression p = New();
            for (int i = 1; i < xp.Length; i++)
            {
                ClearLevel5(p, Maps()[i - 1].id, GameMode.Draw);
                p.Data.xp = xp[i] - 1;
                Assert.IsFalse(p.IsMapUnlocked(i), $"{Maps()[i].name} at {xp[i] - 1} XP");
                Assert.AreEqual(1, p.MapLockFor(i).XpMissing);
                p.Data.xp = xp[i];
                Assert.IsTrue(p.IsMapUnlocked(i), $"{Maps()[i].name} at {xp[i]} XP");
            }
        }

        [Test]
        public void MilitaryBaseAndSpaceport_StayLocked_ComingSoon()
        {
            Progression p = New(new SaveData { xp = 100000 });
            foreach (MapInfo map in Maps())
                ClearLevel5(p, map.id, GameMode.Draw);
            Assert.IsTrue(p.IsMapUnlocked(4), "Arctic");
            Assert.IsFalse(p.IsMapUnlocked(5));
            Assert.IsTrue(p.MapLockFor(5).ComingSoon);
            Assert.AreEqual("Coming soon", p.MapLockFor(5).Describe(5));
            Assert.IsFalse(p.IsMapUnlocked(6));
            Assert.IsFalse(p.IsLevelUnlocked(5, GameMode.Draw, 0), "No levels on a locked map");
        }

        // ---- Saving and loading ----

        [Test]
        public void SaveAndLoad_KeepsEverything()
        {
            var saved = ProgressionSave.Load(SavePath);
            var progression = new Progression(saved.Data, Maps());
            progression.RecordClear(Draw1, 3, 70f, 8, 0, 0, Rules);
            progression.RecordClear(Draw2, 1, 115f, 8, 0, 1, Rules);
            Assert.IsTrue(saved.Write());

            var loaded = ProgressionSave.Load(SavePath);
            var again = new Progression(loaded.Data, Maps());
            Assert.AreEqual(progression.Coins, again.Coins);
            Assert.AreEqual(progression.Xp, again.Xp);
            Assert.Greater(again.Xp, 0);
            Assert.AreEqual(3, again.StarsFor(Draw1));
            Assert.AreEqual(1, again.StarsFor(Draw2));
            Assert.AreEqual(70f, again.BestSecondsFor(Draw1), 1e-4f);
            Assert.IsTrue(again.IsLevelUnlocked(Draw3), "Clearing level 2 unlocked level 3");
            Assert.AreEqual(SaveData.CurrentVersion, loaded.Data.version);
            Assert.IsFalse(loaded.IsReadOnly);
        }

        [Test]
        public void NoFile_StartsFresh()
        {
            var loaded = ProgressionSave.Load(SavePath);
            Assert.AreEqual(0, loaded.Data.coins);
            Assert.IsEmpty(loaded.Data.levels);
            Assert.IsFalse(File.Exists(SavePath), "Nothing is written until there is progress");
        }

        [Test]
        public void Write_LeavesNoTemporaryFile()
        {
            var save = ProgressionSave.Load(SavePath);
            save.Data.coins = 12;
            save.Write();
            save.Data.coins = 20;
            save.Write();

            Assert.IsTrue(File.Exists(SavePath));
            Assert.IsFalse(File.Exists(SavePath + ".tmp"));
            Assert.AreEqual(20, ProgressionSave.Load(SavePath).Data.coins);
        }

        // ---- Old, newer and damaged saves ----

        [Test]
        public void SaveWithoutAVersion_IsUpgraded()
        {
            File.WriteAllText(SavePath, "{\"coins\": 45, \"levels\": [{\"id\": \"island-01\", \"stars\": 2, \"bestSeconds\": 99, \"timesCleared\": 1}]}");

            var loaded = ProgressionSave.Load(SavePath);
            Assert.AreEqual(SaveData.CurrentVersion, loaded.Data.version);
            Assert.AreEqual(45, loaded.Data.coins);
            Assert.AreEqual(2, New(loaded.Data).StarsFor(Draw1), "Old island-01 is Island Draw level 1");
            Assert.IsTrue(New(loaded.Data).IsLevelUnlocked(Draw2), "Cleared level 1 unlocks level 2 even without the unlocked list");
            Assert.IsFalse(loaded.IsReadOnly);
        }

        [Test]
        public void SaveFromANewerVersion_IsReadButNeverWrittenOver()
        {
            string newer = "{\"version\": 99, \"coins\": 300, \"futureField\": \"kept\", \"levels\": []}";
            File.WriteAllText(SavePath, newer);

            var loaded = ProgressionSave.Load(SavePath);
            Assert.IsTrue(loaded.IsReadOnly);
            Assert.AreEqual(300, loaded.Data.coins);

            loaded.Data.coins = 1;
            Assert.IsFalse(loaded.Write());
            Assert.AreEqual(newer, File.ReadAllText(SavePath), "Untouched, so nothing a later build stored is lost");
        }

        [Test]
        public void DamagedSave_StartsFresh_AndKeepsACopy()
        {
            File.WriteAllText(SavePath, "{ this is not json");

            var loaded = ProgressionSave.Load(SavePath);
            Assert.AreEqual(0, loaded.Data.coins);
            Assert.IsTrue(File.Exists(SavePath + ".broken"));
            Assert.IsFalse(loaded.IsReadOnly);
        }

        [Test]
        public void EmptyOrPartialSave_IsTidied()
        {
            SaveData data = ProgressionSave.FromJson("{\"version\": 1, \"coins\": -5, \"levels\": [{\"stars\": 3}]}", out bool newer, out bool broken);
            Assert.IsFalse(newer);
            Assert.IsFalse(broken);
            Assert.AreEqual(0, data.coins, "No negative coins");
            Assert.IsEmpty(data.levels, "A level without an id is dropped");
            Assert.IsNotNull(data.unlocked);

            ProgressionSave.FromJson("   ", out _, out bool emptyBroken);
            Assert.IsTrue(emptyBroken);
        }

        [Test]
        public void VersionOneSave_IsUpgraded_KeepingCoinsStarsAndIslandProgress()
        {
            File.WriteAllText(SavePath,
                "{\"version\": 1, \"coins\": 230, \"unlocked\": [\"island-02\", \"island-03\", \"island-07\"], \"levels\": [" +
                "{\"id\": \"island-01\", \"stars\": 3, \"bestSeconds\": 70, \"timesCleared\": 2}," +
                "{\"id\": \"island-02\", \"stars\": 1, \"bestSeconds\": 110, \"timesCleared\": 1}," +
                "{\"id\": \"island-06\", \"stars\": 2, \"bestSeconds\": 90, \"timesCleared\": 1}," +
                "{\"id\": \"island-07\", \"stars\": 1, \"bestSeconds\": 85, \"timesCleared\": 1}]}");

            var loaded = ProgressionSave.Load(SavePath);
            SaveData data = loaded.Data;
            Assert.AreEqual(2, data.version);
            Assert.AreEqual(230, data.coins, "Coins kept");
            Progression p = New(data);
            Assert.AreEqual(3, p.StarsFor(Draw1));
            Assert.AreEqual(70f, p.BestSecondsFor(Draw1), 1e-4f);
            Assert.AreEqual(1, p.StarsFor(Draw2));
            Assert.AreEqual(2, p.StarsFor("island-draw-05"), "Old levels 5 to 10 merge into Draw level 5, best stars");
            Assert.AreEqual(85f, p.BestSecondsFor("island-draw-05"), 1e-4f, "and best time");
            Assert.AreEqual(2, data.Find("island-draw-05").timesCleared);
            Assert.IsNull(data.Find("island-01"), "No old ids left");
            Assert.IsTrue(p.IsLevelUnlocked(Draw3), "Unlocked list carried over");
            Assert.IsTrue(p.IsLevelUnlocked("island-draw-05"));
            Assert.AreEqual((3 + 1 + 2) * 25, data.xp, "25 XP per kept star");
            Assert.AreEqual("city-draw-01", ProgressionSave.VersionTwoId("city-draw-01"), "New ids stay");

            // Stars kept by the upgrade never pay again.
            LevelRewards replay = p.RecordClear(Draw1, 3, 60f, 8, 0, 1, Rules);
            Assert.AreEqual(0, replay.NewStars);
            Assert.IsTrue(loaded.Write());
            Assert.AreEqual(2, ProgressionSave.Load(SavePath).Data.version);
        }

        [Test]
        public void Migrate_BringsVersionZeroUpToDate()
        {
            var data = new SaveData { version = 0 };
            ProgressionSave.Migrate(data);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
        }
    }
}
