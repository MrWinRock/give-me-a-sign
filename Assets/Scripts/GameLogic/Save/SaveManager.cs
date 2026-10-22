using System.IO;
using UnityEngine;

namespace GameLogic.Save
{
    /// <summary>
    /// Reads and writes the single save file (JSON, in Application.persistentDataPath).
    ///
    /// Static because everything that saves - the flow manager, the event director, the mail
    /// window - needs the same one instance of the data, and none of them should have to be
    /// wired to a scene object to get it.
    /// </summary>
    public static class SaveManager
    {
        public const string FileName = "givemeasign.save.json";

        private static SaveData _current;

        public const int SlotCount = 3;
        private const string ActiveSlotKey = "Save_ActiveSlot";

        /// <summary>Which of the 3 slots the game is playing on (1..3). Remembered between launches.</summary>
        public static int ActiveSlot => Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotKey, 1), 1, SlotCount);

        public static string SlotPath(int slot) => slot == 1
            ? Path.Combine(Application.persistentDataPath, FileName)   // slot 1 keeps the original file name: existing saves carry over
            : Path.Combine(Application.persistentDataPath, $"givemeasign.save.slot{slot}.json");

        public static string SavePath => SlotPath(ActiveSlot);

        /// <summary>Switches the live save to another slot (loaded fresh from disk on next access).</summary>
        public static void SelectSlot(int slot)
        {
            PlayerPrefs.SetInt(ActiveSlotKey, Mathf.Clamp(slot, 1, SlotCount));
            PlayerPrefs.Save();
            _current = null;
        }

        public static bool SlotExists(int slot) => File.Exists(SlotPath(slot));

        public static void DeleteSlot(int slot)
        {
            try { if (SlotExists(slot)) File.Delete(SlotPath(slot)); }
            catch (System.Exception e) { Debug.LogError($"SaveManager: could not delete slot {slot} ({e.Message})."); }
            if (slot == ActiveSlot) _current = null;
        }

        /// <summary>Fires after any successful write, so UI can refresh without polling.</summary>
        public static event System.Action<SaveData> OnSaved;

        /// <summary>The live save, loaded from disk on first access.</summary>
        public static SaveData Current
        {
            get
            {
                if (_current == null) Load();
                return _current;
            }
        }

        public static bool SaveFileExists => File.Exists(SavePath);

        public static SaveData Load()
        {
            if (!SaveFileExists)
            {
                _current = new SaveData();
                return _current;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                _current = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();

                // JsonUtility leaves lists null when the field was absent in an older file.
                                if (_current.consumedEventIds == null) _current.consumedEventIds = new System.Collections.Generic.List<string>();
                                if (_current.readEmailIds == null) _current.readEmailIds = new System.Collections.Generic.List<string>();
                                if (_current.foundMailIds == null) _current.foundMailIds = new System.Collections.Generic.List<string>();
                                if (_current.playedCinematics == null) _current.playedCinematics = new System.Collections.Generic.List<string>();
            }
            catch (System.Exception e)
            {
                // A corrupt save must not brick the game - start fresh and keep the bad file
                // alongside it so it can still be inspected.
                Debug.LogError($"SaveManager: could not read '{SavePath}' ({e.Message}). Starting a new save.");
                TryBackupCorruptFile();
                _current = new SaveData();
            }

            return _current;
        }

        public static void Save()
        {
            if (_current == null) _current = new SaveData();

            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(_current, prettyPrint: true));
                OnSaved?.Invoke(_current);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SaveManager: could not write '{SavePath}' ({e.Message}).");
            }
        }

        /// <summary>
        /// Full wipe - day progress, consumed event pools, and email/document flags. Used by New
        /// Game and by the post-ending reset, which are the same operation by design.
        /// </summary>
        public static void ResetAll()
        {
            _current = new SaveData();
            Save();
        }

        /// <summary>
        /// Reads the save file fresh from disk without touching the live copy. Returns false when there is no file;
        /// <paramref name="corrupted"/> is true when a file exists but cannot be read.
        /// </summary>
        public static bool Peek(int slot, out SaveData data, out bool corrupted)
        {
            data = null; corrupted = false;
            if (!SlotExists(slot)) return false;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SlotPath(slot)));
                if (data == null) corrupted = true;
            }
            catch { corrupted = true; }
            return true;
        }

        /// <summary>Drops the in-memory copy so the next access re-reads from disk. For tests/tools.</summary>
        public static void Unload() => _current = null;

        private static void TryBackupCorruptFile()
        {
            try
            {
                File.Copy(SavePath, SavePath + ".corrupt", overwrite: true);
            }
            catch
            {
                // Best effort only - never let the backup attempt itself throw.
            }
        }
    }
}
