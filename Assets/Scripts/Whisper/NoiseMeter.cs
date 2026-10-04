using GameLogic;
using GameLogic.Data;
using UnityEngine;

namespace Whisper
{
    public enum VoiceLevel { Silent, Whisper, Normal, Shout }

    /// <summary>
    /// Live volume slider: shows the player how loud they are right now (left = silent, right = shouting)
    /// with the whisper / shout lines marked, so a Hooded Figure ("stay quiet") or a Demon ("shout")
    /// can be answered by watching the bar instead of reading a label. Also classifies chunk loudness
    /// for the rest of the voice systems.
    /// </summary>
    public class NoiseMeter : MonoBehaviour
    {
        public enum Requirement { None, StayQuiet, BeLoud }

        [Header("Loudness bands (x the calibrated noise floor)")]
        [Tooltip("Speech at or below this multiple of the noise floor counts as a whisper.")]
        [Min(1f)] [SerializeField] private float whisperBandMultiplier = 3f;
        [Tooltip("Speech at or above this multiple of the noise floor counts as shouting.")]
        [Min(1f)] [SerializeField] private float shoutBandMultiplier = 8f;
        [Tooltip("Level at the far right of the slider (log scale).")]
        [Min(2f)] [SerializeField] private float sliderMaxMultiplier = 16f;

        [Header("Slider feel")]
        [Tooltip("Slider units per second when the level rises.")]
        [Min(0.1f)] [SerializeField] private float riseSpeed = 8f;
        [Tooltip("Slider units per second when the level falls.")]
        [Min(0.1f)] [SerializeField] private float fallSpeed = 2.5f;
        [Tooltip("Seconds after the last mic chunk before the slider starts falling back to zero.")]
        [Min(0f)] [SerializeField] private float holdSeconds = 0.25f;

        public static NoiseMeter Instance { get; private set; }

        // 0 = silent, 1 = far right of the slider.
        public float Position { get; private set; }
        public float WhisperEdge => ToPosition(whisperBandMultiplier);
        public float ShoutEdge => ToPosition(shoutBandMultiplier);

        private NoiseMeterHud _hud;
        private float _target;
        private float _lastChunkTime = float.NegativeInfinity;
        private float _nextRequirementCheck;
        private Requirement _requirement;

        void OnEnable()
        {
            Instance = this;
            WhisperMicInput.OnSpeechChunk += HandleSpeechChunk;
        }

        void OnDisable()
        {
            WhisperMicInput.OnSpeechChunk -= HandleSpeechChunk;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _hud = NoiseMeterHud.Create();
            _hud.SetZones(WhisperEdge, ShoutEdge);
            _hud.SetLevel(0f);
        }

        void OnDestroy()
        {
            _hud?.Destroy();
            _hud = null;
        }

        void Update()
        {
            if (Time.unscaledTime - _lastChunkTime > holdSeconds)
                _target = 0f;

            float speed = _target > Position ? riseSpeed : fallSpeed;
            Position = Mathf.MoveTowards(Position, _target, speed * Time.unscaledDeltaTime);

            if (Time.unscaledTime >= _nextRequirementCheck)
            {
                _nextRequirementCheck = Time.unscaledTime + 0.25f;
                _requirement = CurrentRequirement();
                _hud?.SetRequirement(_requirement);
            }

            _hud?.SetMicOpen(GlobalPushToTalk.Instance != null && GlobalPushToTalk.Instance.IsMicOpen);
            _hud?.SetLevel(Position);
        }

        private void HandleSpeechChunk(float rms, float seconds)
        {
            _lastChunkTime = Time.unscaledTime;
            _target = ToPosition(rms / Mathf.Max(0.001f, MicCalibration.NoiseFloor));
        }

        // Log scale: loudness is perceived in ratios, and this keeps both a whisper and a shout on the bar.
        private float ToPosition(float ratio)
        {
            if (ratio <= 1f) return 0f;
            return Mathf.Clamp01(Mathf.Log(ratio, 2f) / Mathf.Log(sliderMaxMultiplier, 2f));
        }

        public VoiceLevel Classify(float rms)
        {
            float floor = Mathf.Max(0.001f, MicCalibration.NoiseFloor);
            if (rms <= floor * 1.1f) return VoiceLevel.Silent;
            if (rms >= floor * shoutBandMultiplier) return VoiceLevel.Shout;
            if (rms <= floor * whisperBandMultiplier) return VoiceLevel.Whisper;
            return VoiceLevel.Normal;
        }

        // What the active threat asks of the player right now: shown on the slider as the zone to aim for.
        private static Requirement CurrentRequirement()
        {
            if (DemonAnomaly.AnyRevealed) return Requirement.BeLoud;

            foreach (var anomaly in Anomaly.ActiveAnomalies)
            {
                if (anomaly != null && anomaly.State != AnomalyState.Resolved && !anomaly.IsReported
                    && anomaly.Definition != null && anomaly.Definition.voiceResponse == VoiceResponse.Silence)
                    return Requirement.StayQuiet;
            }
            return Requirement.None;
        }
    }
}
