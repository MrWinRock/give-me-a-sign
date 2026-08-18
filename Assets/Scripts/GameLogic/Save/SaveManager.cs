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

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

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
        /// Full wipe - day progress, consumed event pools and email flags. Used by New Game and
        /// by the post-ending reset, which are the same operation by design.
        /// </summary>
        public static void ResetAll()
        {
            _current = new SaveData();
            Save();
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
