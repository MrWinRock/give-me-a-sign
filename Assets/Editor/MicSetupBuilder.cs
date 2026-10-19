using UnityEditor;
using UnityEngine;
using Whisper;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// Mic Setup menu commands: create the calibration config asset and reset the stored calibration. The wizard prefab and
    /// MicSetup scene are hand-edited assets, not generated.
    /// </summary>
    public static class MicSetupBuilder
    {
        private const string ConfigPath = "Assets/Resources/MicCalibrationConfig.asset";

        [MenuItem("Tools/Give Me A Sign/Mic Setup/Create Config Asset")]
        public static MicCalibrationConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<MicCalibrationConfig>(ConfigPath);
            if (config != null) return config;

            config = ScriptableObject.CreateInstance<MicCalibrationConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"MicSetupBuilder: created {ConfigPath}.", config);
            return config;
        }

        [MenuItem("Tools/Give Me A Sign/Mic Setup/Reset Microphone Calibration")]
        public static void ResetCalibration()
        {
            MicCalibrationStore.ResetToDefaults();
            Debug.Log("MicSetupBuilder: microphone calibration cleared - the setup wizard shows on the next launch.");
        }
    }
}
