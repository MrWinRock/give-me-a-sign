using System;
using MainMenu;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// The player's microphone calibration, kept in PlayerPrefs - deliberately NOT in the game save
    /// (givemeasign.save.json), so neither the Day 7 wipe nor New Game ever clears it.
    /// </summary>
    public static class MicCalibrationStore
    {
        private static MicCalibrationData _cached;

        public static event Action OnChanged;

        public static MicCalibrationConfig Config => MicCalibrationConfig.Load();

        public static MicCalibrationData Current
        {
            get
            {
                if (_cached == null) Load();
                return _cached;
            }
        }

        // False on first launch, after Reset, or when the stored version is older than the config's.
        public static bool IsSetupCompleted => Current.setupCompleted && Current.calibrationVersion >= Config.currentCalibrationVersion;

        public static bool UsesDefaultSettings => !IsSetupCompleted || Current.usedDefaults;

        // A specific microphone was chosen and it is no longer connected.
        public static bool DeviceMissing
        {
            get
            {
                string saved = ControlPanelWindow.MicrophoneDevice;
                if (string.IsNullOrEmpty(saved)) return false;
                return Array.IndexOf(Microphone.devices, saved) < 0;
            }
        }

        public static void Load()
        {
            string json = PlayerPrefs.GetString(Config.prefsKey, string.Empty);
            MicCalibrationData data = null;

            if (!string.IsNullOrEmpty(json))
            {
                try { data = JsonUtility.FromJson<MicCalibrationData>(json); }
                catch (Exception e) { Debug.LogWarning($"MicCalibrationStore: stored calibration unreadable ({e.Message}) - using defaults."); }
            }

            _cached = data ?? CreateDefaults(completed: false);
        }

        public static void Save(MicCalibrationData data)
        {
            data.setupCompleted = true;
            data.calibrationVersion = Config.currentCalibrationVersion;
            _cached = data.Clone();

            PlayerPrefs.SetString(Config.prefsKey, JsonUtility.ToJson(_cached));
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        // Skip / no microphone: the wizard counts as done and the config's defaults apply.
        public static void SaveDefaults()
        {
            var data = CreateDefaults(completed: true);
            data.deviceName = ControlPanelWindow.MicrophoneDevice;
            Save(data);
        }

        // Back to "never ran" - the wizard shows again on the next launch.
        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(Config.prefsKey);
            PlayerPrefs.Save();
            _cached = CreateDefaults(completed: false);
            OnChanged?.Invoke();
        }

        public static MicCalibrationData CreateDefaults(bool completed)
        {
            var c = Config;
            return new MicCalibrationData
            {
                setupCompleted = completed,
                usedDefaults = true,
                deviceName = string.Empty,
                noiseFloorDb = c.defaultNoiseFloorDb,
                normalVoiceDb = c.defaultNormalVoiceDb,
                loudVoiceDb = c.defaultLoudVoiceDb,
                minDb = c.defaultMinDb,
                maxDb = c.defaultMaxDb,
                lowToNormal = c.defaultLowToNormal,
                normalToLoud = c.defaultNormalToLoud,
                calibrationVersion = c.currentCalibrationVersion,
            };
        }

        // The device gameplay and the wizard capture from: the Control Panel's choice, or null (system default) when it is gone.
        public static string ResolveDevice()
        {
            string saved = ControlPanelWindow.MicrophoneDevice;
            if (string.IsNullOrEmpty(saved)) return null;
            return Array.IndexOf(Microphone.devices, saved) >= 0 ? saved : null;
        }
    }
}
