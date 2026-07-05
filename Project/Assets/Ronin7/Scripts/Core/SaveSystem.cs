using System;
using System.IO;
using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Persists and loads campaign save files to three numbered slots.
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 3;

        /// <summary>
        /// Null = default Path.Combine(Application.persistentDataPath, "saves").
        /// Tests point this at a temp dir (EditMode tests can't touch persistentDataPath safely).
        /// </summary>
        public static string DirectoryOverride;

        private static string Directory
        {
            get
            {
                if (DirectoryOverride != null)
                    return DirectoryOverride;
                return Path.Combine(Application.persistentDataPath, "saves");
            }
        }

        private static string SlotPath(int slot)
        {
            return Path.Combine(Directory, $"slot{slot}.json");
        }

        private static string TempPath(int slot)
        {
            return SlotPath(slot) + ".tmp";
        }

        /// <summary>
        /// Saves data to the specified slot (1-based, 1..SlotCount).
        /// Sets savedAtUtcTicks, creates directory if needed, writes JSON.
        /// Returns silently on invalid slot or null data.
        /// </summary>
        public static void Save(int slot, SaveData data)
        {
            if (slot < 1 || slot > SlotCount)
            {
                Debug.LogError($"SaveSystem.Save: slot {slot} out of range [1..{SlotCount}]");
                return;
            }

            if (data == null)
            {
                Debug.LogError("SaveSystem.Save: data is null");
                return;
            }

            try
            {
                data.savedAtUtcTicks = DateTime.UtcNow.Ticks;
                string dir = Directory;
                System.IO.Directory.CreateDirectory(dir);
                string path = SlotPath(slot);
                string tempPath = TempPath(slot);
                string json = JsonUtility.ToJson(data, true);

                // Atomic write: stage to a temp file in the same directory and flush it to
                // disk, then swap it into place. A crash/power-loss mid-write leaves the
                // previous save intact instead of truncating the only copy.
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(path))
                    File.Replace(tempPath, path, null);
                else
                    File.Move(tempPath, path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveSystem.Save: IO error: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads data from the specified slot (1-based, 1..SlotCount).
        /// Returns null if slot out of range, file missing, JSON parse fails, result is null,
        /// or the file was saved by a newer build (version > CurrentVersion).
        /// </summary>
        public static SaveData Load(int slot)
        {
            if (slot < 1 || slot > SlotCount)
            {
                return null;
            }

            CleanupStrayTemp(slot);

            string path = SlotPath(slot);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                string json = File.ReadAllText(path);
                SaveData result = JsonUtility.FromJson<SaveData>(json);
                if (result == null)
                {
                    return null;
                }

                if (result.version > SaveData.CurrentVersion)
                {
                    Debug.LogWarning($"SaveSystem.Load: slot {slot} is version {result.version}, newer than CurrentVersion {SaveData.CurrentVersion}");
                    return null;
                }

                if (result.version < SaveData.CurrentVersion)
                {
                    result = Migrate(result);
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveSystem.Load: failed to load slot {slot}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Upgrades an older-versioned SaveData to the current schema, one ascending step at a time.
        /// v1 is the baseline, so anything at or below it is an identity upgrade today; future schema
        /// changes slot in here as ordered steps (e.g. a v1-to-v2 step that ends with data.version = 2)
        /// before the final stamp to CurrentVersion.
        /// </summary>
        private static SaveData Migrate(SaveData data)
        {
            if (data.version < 2)
            {
                // v2 (chapter restructure): campaign missions now enter through the ship-prologue
                // scenes, and completion tracking follows the CampaignDirector's entry names. A v1
                // save that completed "Ch02_Auction" must read as having completed "Ch02_Prologue"
                // or the launcher would replay finished chapters.
                for (int i = 0; i < data.completedPlanetScenes.Count; i++)
                    data.completedPlanetScenes[i] = PrologueSceneNames.Rename(data.completedPlanetScenes[i]);
                data.lastPlanetScene = PrologueSceneNames.Rename(data.lastPlanetScene);
                data.version = 2;
            }

            data.version = SaveData.CurrentVersion;
            return data;
        }

        /// <summary>
        /// Best-effort removal of a temp file left behind by an interrupted atomic write.
        /// The real save (slot{n}.json) is never touched, so a half-written temp can't shadow it.
        /// </summary>
        private static void CleanupStrayTemp(int slot)
        {
            string tempPath = TempPath(slot);
            if (!File.Exists(tempPath))
            {
                return;
            }

            try
            {
                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"SaveSystem: failed to remove stray temp file for slot {slot}: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns true if the given slot (1-based) exists and contains a valid save file.
        /// </summary>
        public static bool SlotExists(int slot)
        {
            if (slot < 1 || slot > SlotCount)
            {
                return false;
            }

            return File.Exists(SlotPath(slot));
        }

        /// <summary>
        /// Returns a human-readable summary of the slot's contents.
        /// Empty slot → "Empty". Populated → "X planet(s) cleared" (singular/plural).
        /// </summary>
        public static string SlotSummary(int slot)
        {
            SaveData data = Load(slot);
            if (data == null)
            {
                return "Empty";
            }

            int count = data.completedPlanetScenes.Count;
            string plural = count == 1 ? "planet" : "planets";
            return $"{count} {plural} cleared";
        }

        /// <summary>
        /// Gets/sets the most recently used save slot (1-based, clamped to 1..SlotCount).
        /// PlayerPrefs key "ss.lastSaveSlot", default 1.
        /// </summary>
        public static int MostRecentSlot
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("ss.lastSaveSlot", 1), 1, SlotCount);
            set
            {
                int clamped = Mathf.Clamp(value, 1, SlotCount);
                PlayerPrefs.SetInt("ss.lastSaveSlot", clamped);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Convenience: saves data to MostRecentSlot.
        /// </summary>
        public static void Autosave(SaveData data)
        {
            Save(MostRecentSlot, data);
        }
    }
}
