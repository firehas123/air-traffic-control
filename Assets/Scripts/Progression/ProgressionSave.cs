using System;
using System.IO;
using UnityEngine;

namespace AirTrafficControl
{
    /// <summary>
    /// ProgressionSave (blueprint 5): reads and writes <see cref="SaveData"/> as a JSON file on the device. Offline only.
    ///
    /// - Writes go to a temporary file that then replaces the save, so a crash mid-write cannot leave half a file.
    /// - A save from an older version is brought up to date by <see cref="Migrate"/>.
    /// - A save from a newer version (a later build) is read, but never written over, so nothing in it is lost.
    /// - A damaged file is kept aside as ".broken" and the player starts fresh, rather than the game failing to start.
    /// </summary>
    public sealed class ProgressionSave
    {
        public const string FileName = "progress.json";

        public ProgressionSave(string path, SaveData data, bool readOnly)
        {
            Path = path;
            Data = data;
            IsReadOnly = readOnly;
        }

        public string Path { get; }
        public SaveData Data { get; }

        /// <summary>True when the file came from a newer version of the game: it is not written over.</summary>
        public bool IsReadOnly { get; }

        /// <summary>The save in the game's data folder on this device.</summary>
        public static string DefaultPath => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public static ProgressionSave Load(string path)
        {
            if (!File.Exists(path))
                return new ProgressionSave(path, new SaveData(), false);

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ProgressionSave: could not read {path}: {e.Message}. Starting fresh.");
                return new ProgressionSave(path, new SaveData(), false);
            }

            SaveData data = FromJson(json, out bool newer, out bool broken);
            if (broken)
            {
                KeepBrokenCopy(path);
                return new ProgressionSave(path, new SaveData(), false);
            }
            return new ProgressionSave(path, data, newer);
        }

        /// <summary>
        /// Parses a save. <paramref name="fromNewerVersion"/> is true when it was written by a later build;
        /// <paramref name="broken"/> when it could not be read at all.
        /// </summary>
        public static SaveData FromJson(string json, out bool fromNewerVersion, out bool broken)
        {
            fromNewerVersion = false;
            broken = false;
            if (string.IsNullOrWhiteSpace(json))
            {
                broken = true;
                return new SaveData();
            }

            SaveData data;
            try
            {
                data = new SaveData { version = 0 }; // a file without a version field is from before versioning
                JsonUtility.FromJsonOverwrite(json, data);
            }
            catch (Exception)
            {
                broken = true;
                return new SaveData();
            }

            fromNewerVersion = data.version > SaveData.CurrentVersion;
            if (!fromNewerVersion)
                Migrate(data);
            Tidy(data);
            return data;
        }

        public static string ToJson(SaveData data) => JsonUtility.ToJson(data, true);

        /// <summary>
        /// Brings an older save up to the current version, one version at a time. Version 0 means a file written
        /// before saves had a version number (only test builds); it has the same fields, so it only needs the number.
        /// </summary>
        public static void Migrate(SaveData data)
        {
            if (data.version < 1)
                data.version = 1;
            // When CurrentVersion becomes 2, add: if (data.version < 2) { ...convert...; data.version = 2; }
        }

        // Missing lists (from hand-edited or partial files) become empty instead of null.
        static void Tidy(SaveData data)
        {
            if (data.unlocked == null) data.unlocked = new System.Collections.Generic.List<string>();
            if (data.levels == null) data.levels = new System.Collections.Generic.List<LevelProgress>();
            data.levels.RemoveAll(level => level == null || string.IsNullOrEmpty(level.id));
            if (data.coins < 0) data.coins = 0;
        }

        /// <summary>Writes the save. Returns false (and writes nothing) for a save from a newer version.</summary>
        public bool Write()
        {
            if (IsReadOnly)
            {
                Debug.LogWarning("ProgressionSave: the save is from a newer version of the game, so it is not written over.");
                return false;
            }

            Data.version = SaveData.CurrentVersion;
            string directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporary = Path + ".tmp";
            File.WriteAllText(temporary, ToJson(Data));
            if (File.Exists(Path))
                File.Replace(temporary, Path, null);
            else
                File.Move(temporary, Path);
            return true;
        }

        static void KeepBrokenCopy(string path)
        {
            try
            {
                File.Copy(path, path + ".broken", true);
                Debug.LogWarning($"ProgressionSave: {path} could not be read; kept a copy as .broken and started fresh.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ProgressionSave: could not keep a copy of the damaged save: {e.Message}");
            }
        }
    }
}
