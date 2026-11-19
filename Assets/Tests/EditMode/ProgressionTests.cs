using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace AirTrafficControl.Tests
{
    /// <summary>ProgressionSave and the progression rules (M3 step 1, C9, C11).</summary>
    public class ProgressionTests
    {
        static readonly string[] Ids = { "island-01", "island-02", "island-03" };
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

        // ---- Unlocking ----

        [Test]
        public void FreshSave_OnlyTheFirstLevelIsUnlocked()
        {
            var progression = new Progression(new SaveData(), Ids);
            Assert.IsTrue(progression.IsUnlocked(0));
            Assert.IsFalse(progression.IsUnlocked(1));
            Assert.IsFalse(progression.IsUnlocked(2));
            Assert.IsFalse(progression.IsUnlocked(5), "Outside the list");
        }

        [Test]
        public void ClearingALevel_UnlocksTheNext_WithAnyNumberOfStars()
        {
            var progression = new Progression(new SaveData(), Ids);
            LevelRewards rewards = progression.RecordClear("island-01", 1, 110f, 8, 0, Rules);

            Assert.IsTrue(rewards.UnlockedNext);
            Assert.IsTrue(progression.IsUnlocked(1), "C11: stars never block the campaign");
            Assert.IsFalse(progression.IsUnlocked(2));
        }

        // ---- Coins and stars (C9) ----

        [Test]
        public void FirstTimeStars_PayOnlyOnce()
        {
            var progression = new Progression(new SaveData(), Ids);

            LevelRewards first = progression.RecordClear("island-01", 2, 90f, 8, 0, Rules);
            Assert.AreEqual(8 * 5 + 2 * 10, first.CoinsEarned);
            Assert.AreEqual(2, first.NewStars);

            LevelRewards same = progression.RecordClear("island-01", 2, 95f, 8, 0, Rules);
            Assert.AreEqual(8 * 5, same.CoinsEarned, "Landings pay again, the two stars do not");
            Assert.AreEqual(0, same.NewStars);

            LevelRewards better = progression.RecordClear("island-01", 3, 80f, 8, 0, Rules);
            Assert.AreEqual(8 * 5 + 1 * 10, better.CoinsEarned, "Only the third star is new");

            LevelRewards worse = progression.RecordClear("island-01", 1, 130f, 8, 0, Rules);
            Assert.AreEqual(8 * 5, worse.CoinsEarned);
            Assert.AreEqual(3, progression.StarsFor("island-01"), "Best stars are kept");

            Assert.AreEqual(first.CoinsEarned + same.CoinsEarned + better.CoinsEarned + worse.CoinsEarned, progression.Coins);
        }

        [Test]
        public void Departures_PayCoinsToo()
        {
            var progression = new Progression(new SaveData(), Ids);
            LevelRewards rewards = progression.RecordClear("island-01", 0, 100f, 6, 3, Rules);
            Assert.AreEqual(6 * 5 + 3 * 5, rewards.CoinsEarned);
        }

        [Test]
        public void BestTime_IsTheFastestClear()
        {
            var progression = new Progression(new SaveData(), Ids);
            Assert.Less(progression.BestSecondsFor("island-01"), 0f, "Never cleared");

            Assert.IsTrue(progression.RecordClear("island-01", 1, 100f, 8, 0, Rules).NewBestTime);
            Assert.IsFalse(progression.RecordClear("island-01", 1, 120f, 8, 0, Rules).NewBestTime);
            Assert.IsTrue(progression.RecordClear("island-01", 1, 75f, 8, 0, Rules).NewBestTime);
            Assert.AreEqual(75f, progression.BestSecondsFor("island-01"), 1e-4f);
        }

        // ---- Saving and loading ----

        [Test]
        public void SaveAndLoad_KeepsEverything()
        {
            var saved = ProgressionSave.Load(SavePath);
            var progression = new Progression(saved.Data, Ids);
            progression.RecordClear("island-01", 3, 70f, 8, 0, Rules);
            progression.RecordClear("island-02", 1, 115f, 8, 0, Rules);
            Assert.IsTrue(saved.Write());

            var loaded = ProgressionSave.Load(SavePath);
            var again = new Progression(loaded.Data, Ids);
            Assert.AreEqual(progression.Coins, again.Coins);
            Assert.AreEqual(3, again.StarsFor("island-01"));
            Assert.AreEqual(1, again.StarsFor("island-02"));
            Assert.AreEqual(70f, again.BestSecondsFor("island-01"), 1e-4f);
            Assert.IsTrue(again.IsUnlocked(2), "Clearing level 2 unlocked level 3");
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
            Assert.AreEqual(2, new Progression(loaded.Data, Ids).StarsFor("island-01"));
            Assert.IsTrue(new Progression(loaded.Data, Ids).IsUnlocked(1), "Cleared level 1 unlocks level 2 even without the unlocked list");
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
        public void Migrate_BringsVersionZeroUpToDate()
        {
            var data = new SaveData { version = 0 };
            ProgressionSave.Migrate(data);
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
        }
    }
}
