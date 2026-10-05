using System;
using GameLogic;
using GameLogic.Data;
using UnityEngine;

namespace Whisper
{
    public enum VoiceLevel { Silent, Whisper, Normal, Shout }

    /// <summary>The three zones the Noise Meter HUD shows. Same thresholds the voice rules judge with.</summary>
    public enum NoiseLevel { Low, Normal, Loud }

    /// <summary>Loudness of one push-to-talk press, in the meter's 0..1 units.</summary>
    public struct UtteranceStats
    {
        public float peak;
        public float average;
        public float duration;
    }

    /// <summary>
    /// The one place loudness is measured (from WhisperMicInput's existing mic chunks - no second mic session).
    /// The Noise Meter HUD reads everything from here, and the voice rules (GlobalPushToTalk -> IncidentReportManager)
    /// classify with the same whisper / shout thresholds, so the meter never shows a different verdict than the one the player is judged by.
    /// </summary>
    public class NoiseMeter : MonoBehaviour
    {
        public enum Requirement { None, StayQuiet, BeLoud }

        [Header("Loudness bands (x the calibrated noise floor)")]
        [Tooltip("Speech at or below this multiple of the noise floor counts as a whisper (the LOW zone).")]
        [Min(1f)] [SerializeField] private float whisperBandMultiplier = 3f;
        [Tooltip("Speech at or above this multiple of the noise floor counts as shouting (the LOUD zone).")]
        [Min(1f)] [SerializeField] private float shoutBandMultiplier = 8f;
        [Tooltip("Level at the far right of the meter (log scale).")]
        [Min(2f)] [SerializeField] private float sliderMaxMultiplier = 16f;

        [Header("Meter feel")]
        [Tooltip("Meter units per second when the level rises (fast attack).")]
        [Min(0.1f)] [SerializeField] private float riseSpeed = 8f;
        [Tooltip("Meter units per second when the level falls (slow release).")]
        [Min(0.1f)] [SerializeField] private float fallSpeed = 2.5f;
        [Tooltip("Seconds after the last mic chunk before the meter starts falling back to zero.")]
        [Min(0f)] [SerializeField] private float holdSeconds = 0.25f;
        [Tooltip("The displayed zone only changes once the level is this far past a boundary, so it does not flicker on the line.")]
        [Range(0f, 0.15f)] [SerializeField] private float zoneHysteresis = 0.03f;

        public static NoiseMeter Instance { get; private set; }

        // 0 = silent, 1 = far right of the meter.
        public float Position { get; private set; }
        public float Normalized => Position;
        public float WhisperEdge => ToPosition(whisperBandMultiplier);
        public float ShoutEdge => ToPosition(shoutBandMultiplier);

        public NoiseLevel Level { get; private set; }

        // True while V is held (or the game holds the mic open), or while a Debug feed runs.
        public bool IsActive => IsDebugFeeding || (GlobalPushToTalk.Instance != null && GlobalPushToTalk.Instance.IsMicOpen);
        public bool IsDebugFeeding => Time.unscaledTime < _debugUntil;

        // False on a machine with no microphone: the HUD hides itself and typed input is not judged on volume.
        public bool HasMicrophone { get; private set; } = true;

        public float PeakNormalized { get; private set; }
        public UtteranceStats LastUtterance { get; private set; }
        public Requirement CurrentRequirementNow => _requirement;

        public event Action<NoiseLevel> OnLevelChanged;
        public event Action<UtteranceStats> OnUtteranceEnded;
        public event Action<bool> VolumeMissed; // true = too loud, false = too quiet

        private float _target;
        private float _lastChunkTime = float.NegativeInfinity;
        private float _nextRequirementCheck;
        private float _nextMicCheck;
        private Requirement _requirement;

        private bool _wasActive;
        private float _utteranceStart;
        private float _utterancePeak;
        private float _utteranceSum;
        private int _utteranceSamples;

        private float _debugRms;
        private float _debugUntil = float.NegativeInfinity;

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

        void Update()
        {
            if (Time.unscaledTime >= _nextMicCheck)
            {
                _nextMicCheck = Time.unscaledTime + 1f;
                HasMicrophone = Microphone.devices.Length > 0;
            }

            if (IsDebugFeeding)
            {
                _lastChunkTime = Time.unscaledTime;
                _target = ToPosition(_debugRms / Mathf.Max(0.001f, MicCalibration.NoiseFloor));
            }
            else if (Time.unscaledTime - _lastChunkTime > holdSeconds)
            {
                _target = 0f;
            }

            float speed = _target > Position ? riseSpeed : fallSpeed;
            Position = Mathf.MoveTowards(Position, _target, speed * Time.unscaledDeltaTime);

            UpdateLevel();
            TrackUtterance();

            if (Time.unscaledTime >= _nextRequirementCheck)
            {
                _nextRequirementCheck = Time.unscaledTime + 0.25f;
                _requirement = CurrentRequirement();
            }
        }

        private void UpdateLevel()
        {
            float low = WhisperEdge, high = ShoutEdge, h = zoneHysteresis;
            var next = Level;

            switch (Level)
            {
                case NoiseLevel.Low:
                    if (Position > high + h) next = NoiseLevel.Loud;
                    else if (Position > low + h) next = NoiseLevel.Normal;
                    break;
                case NoiseLevel.Normal:
                    if (Position > high + h) next = NoiseLevel.Loud;
                    else if (Position < low - h) next = NoiseLevel.Low;
                    break;
                default:
                    if (Position < low - h) next = NoiseLevel.Low;
                    else if (Position < high - h) next = NoiseLevel.Normal;
                    break;
            }

            if (next == Level) return;
            Level = next;
            OnLevelChanged?.Invoke(Level);
        }

        // Peak / average / duration of each V press, for tuning and the debug overlay.
        private void TrackUtterance()
        {
            bool active = IsActive;

            if (active && !_wasActive)
            {
                _utteranceStart = Time.unscaledTime;
                _utterancePeak = 0f;
                _utteranceSum = 0f;
                _utteranceSamples = 0;
            }

            if (active)
            {
                _utterancePeak = Mathf.Max(_utterancePeak, Position);
                _utteranceSum += Position;
                _utteranceSamples++;
                PeakNormalized = _utterancePeak;
            }
            else if (_wasActive)
            {
                LastUtterance = new UtteranceStats
                {
                    peak = _utterancePeak,
                    average = _utteranceSamples > 0 ? _utteranceSum / _utteranceSamples : 0f,
                    duration = Time.unscaledTime - _utteranceStart,
                };
                OnUtteranceEnded?.Invoke(LastUtterance);
            }

            _wasActive = active;
        }

        private void HandleSpeechChunk(float rms, float seconds)
        {
            if (IsDebugFeeding) return;

            _lastChunkTime = Time.unscaledTime;
            _target = ToPosition(rms / Mathf.Max(0.001f, MicCalibration.NoiseFloor));
        }

        // Log scale: loudness is perceived in ratios, and this keeps both a whisper and a shout on the meter.
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

        // A correct report whose volume was wrong: nothing is filed or penalised, the HUD just says why.
        public void NotifyVolumeMiss(bool tooLoud) => VolumeMissed?.Invoke(tooLoud);

        // Debug panel: holds the meter at a loudness for a few seconds, as if the player were speaking.
        public void DebugFeed(VoiceLevel level, float seconds)
        {
            float multiplier = level == VoiceLevel.Shout ? shoutBandMultiplier * 1.5f
                             : level == VoiceLevel.Normal ? (whisperBandMultiplier + shoutBandMultiplier) * 0.5f
                             : whisperBandMultiplier * 0.7f;
            _debugRms = MicCalibration.NoiseFloor * multiplier;
            _debugUntil = Time.unscaledTime + seconds;
        }

        // What the active threat asks of the player right now: the HUD lights that zone.
        private static Requirement CurrentRequirement()
        {
            if (DemonAnomaly.AnyRevealed) return Requirement.BeLoud;

            // Only once the player has found the stealth anomaly and the countdown is running.
            return QuietResponse.Engaged ? Requirement.StayQuiet : Requirement.None;
        }
    }
}
