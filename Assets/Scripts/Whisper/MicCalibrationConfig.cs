using UnityEngine;
using UnityEngine.InputSystem;

namespace Whisper
{
    /// <summary>
    /// Every number the microphone setup wizard, the calibration store and the Noise Meter mapping use
    /// (Resources/MicCalibrationConfig). Defaults reproduce NoiseMeter's built-in bands (floor 0.02 RMS, whisper x3, shout x8, max x16).
    /// </summary>
    [CreateAssetMenu(fileName = "MicCalibrationConfig", menuName = "Give Me A Sign/Mic Calibration Config")]
    public class MicCalibrationConfig : ScriptableObject
    {
        private const string ResourcePath = "MicCalibrationConfig";

        [Header("Storage (outside the game save: survives the Day 7 wipe and New Game)")]
        public string prefsKey = "Opt_MicCalibration";
        [Tooltip("Bump this to make every player run the wizard again.")]
        [Min(1)] public int currentCalibrationVersion = 1;

        [Header("Startup routing")]
        public string mainMenuSceneName = "MainMenu";
        public string micSetupSceneName = "MicSetup";

        [Header("Defaults (used on Skip / failed measurement)")]
        public float defaultNoiseFloorDb = -34f;
        public float defaultNormalVoiceDb = -22f;
        public float defaultLoudVoiceDb = -14f;
        public float defaultMinDb = -34f;
        public float defaultMaxDb = -10f;
        [Range(0f, 1f)] public float defaultLowToNormal = 0.40f;
        [Range(0f, 1f)] public float defaultNormalToLoud = 0.75f;

        [Header("Wizard input")]
        public Key pushToTalkKey = Key.Space;
        [Tooltip("Spoken for the normal and loud captures.")]
        public string phrase = "Give me a sign";

        [Header("Measurement")]
        [Min(1f)] public float quietSeconds = 3f;
        [Range(0.5f, 1f)] public float noiseFloorPercentile = 0.9f;
        [Tooltip("dB reported for digital silence (rms 0).")]
        public float silenceDb = -80f;
        [Tooltip("Above this floor the room is considered noisy (warning only).")]
        public float noisyFloorWarningDb = -38f;
        [Min(0.2f)] public float minUtteranceSeconds = 0.8f;
        [Tooltip("Only frames this far above the noise floor count as speech.")]
        [Min(0f)] public float speechGateAboveFloorDb = 6f;
        [Tooltip("The utterance level is the average of this loudest fraction of its speech frames.")]
        [Range(0.2f, 1f)] public float louderPortionFraction = 0.5f;
        [Min(0f)] public float minNormalOverFloorDb = 6f;
        [Min(0f)] public float minLoudOverNormalDb = 6f;
        [Tooltip("Loudest level the microphone can deliver (digital clipping is 0 dB). A normal voice within minLoudOverNormalDb of this leaves no room for a louder one.")]
        public float maxMeasurableDb = -3f;
        [Min(1)] public int failedAttemptsBeforeDefaultsOffered = 3;

        [Header("Derivation")]
        [Tooltip("minDb = noise floor + this, so room noise reads as zero.")]
        public float minDbOffset = 2f;
        [Tooltip("maxDb = loud voice + this.")]
        public float maxDbHeadroom = 3f;
        [Min(6f)] public float minRangeDb = 12f;
        [Tooltip("Where the LOW/NORMAL boundary sits between the noise floor (0) and normal voice (1), in dB.")]
        [Range(0.1f, 0.9f)] public float lowToNormalWeight = 0.5f;
        [Tooltip("Where the NORMAL/LOUD boundary sits between normal voice (0) and loud voice (1), in dB.")]
        [Range(0.1f, 0.9f)] public float normalToLoudWeight = 0.5f;
        public Vector2 lowToNormalClamp = new Vector2(0.15f, 0.45f);
        public Vector2 normalToLoudClamp = new Vector2(0.55f, 0.90f);
        [Range(0.05f, 0.5f)] public float minThresholdGap = 0.15f;

        private static MicCalibrationConfig _cached;
        private static MicCalibrationConfig _fallback;

        public static MicCalibrationConfig Load()
        {
            if (_cached != null) return _cached;

            _cached = Resources.Load<MicCalibrationConfig>(ResourcePath);
            if (_cached != null) return _cached;

            if (_fallback == null)
            {
                Debug.LogWarning($"MicCalibrationConfig: no Resources/{ResourcePath} asset - using built-in defaults.");
                _fallback = CreateInstance<MicCalibrationConfig>();
            }
            return _fallback;
        }
    }
}
