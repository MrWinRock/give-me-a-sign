using GameLogic;
using GameLogic.Data;
using GameLogic.Flow;
using GameLogic.Story;
using Report;
using UnityEngine;

namespace Whisper
{
    public enum VoiceLevel { Silent, Whisper, Normal, Shout }

    /// <summary>
    /// How loud the guard has been lately (0-100). Speaking into the mic fills it - whispering
    /// barely, shouting fast - and silence drains it. When it fills, the Listener (Silence
    /// Protocol) comes. Makes every word the player says cost something.
    /// </summary>
    public class NoiseMeter : MonoBehaviour
    {
        public const float Max = 100f;

        [Header("Filling")]
        [Tooltip("Noise added per second of normal-volume speech.")]
        [Min(0f)] [SerializeField] private float speechNoisePerSecond = 12f;
        [Tooltip("Speech at or below this multiple of the calibrated noise floor counts as a whisper.")]
        [Min(1f)] [SerializeField] private float whisperBandMultiplier = 3f;
        [Tooltip("Speech at or above this multiple of the calibrated noise floor counts as shouting.")]
        [Min(1f)] [SerializeField] private float shoutBandMultiplier = 8f;
        [Tooltip("Cost multiplier while whispering.")]
        [Min(0f)] [SerializeField] private float whisperCostMultiplier = 0.3f;
        [Tooltip("Cost multiplier while shouting.")]
        [Min(0f)] [SerializeField] private float shoutCostMultiplier = 2.5f;

        [Header("Draining")]
        [Tooltip("Noise removed per second once the player has been quiet for the delay below.")]
        [Min(0f)] [SerializeField] private float drainPerSecond = 4f;
        [Tooltip("Seconds of quiet before the meter starts draining.")]
        [Min(0f)] [SerializeField] private float drainDelaySeconds = 2.5f;

        [Header("Listener")]
        [Tooltip("Meter level that summons the Listener (Silence Protocol).")]
        [Min(1f)] [SerializeField] private float triggerThreshold = Max;
        [Tooltip("Where the meter drops to after it fills.")]
        [Min(0f)] [SerializeField] private float levelAfterTrigger = 35f;
        [Tooltip("Seconds after filling before it can summon the Listener again.")]
        [Min(0f)] [SerializeField] private float triggerCooldownSeconds = 45f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        public static NoiseMeter Instance { get; private set; }

        public float Level { get; private set; }
        public float Normalized => Level / Max;

        private NoiseMeterHud _hud;
        private float _lastNoiseTime = float.NegativeInfinity;
        private float _cooldownUntil;
        private bool _pendingTrigger;

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
            _hud.SetLevel(0f);
        }

        void OnDestroy()
        {
            _hud?.Destroy();
            _hud = null;
        }

        void Update()
        {
            if (_pendingTrigger && CanSummonNow())
            {
                _pendingTrigger = false;
                Summon();
            }

            if (!ListenerActive && Level > 0f && Time.unscaledTime - _lastNoiseTime >= drainDelaySeconds)
                Level = Mathf.Max(0f, Level - drainPerSecond * Time.deltaTime);

            _hud?.SetLevel(Normalized);
        }

        // Also the hook for non-speech noise (e.g. a future "Give me a sign" costing noise).
        public void AddNoise(float amount)
        {
            if (amount <= 0f || ListenerActive || _pendingTrigger) return;

            Level = Mathf.Min(Max, Level + amount);
            _lastNoiseTime = Time.unscaledTime;

            if (Level >= triggerThreshold && Time.unscaledTime >= _cooldownUntil)
                _pendingTrigger = true;
        }

        private void HandleSpeechChunk(float rms, float seconds)
        {
            float floor = Mathf.Max(0.001f, MicCalibration.NoiseFloor);
            if (rms <= floor * 1.1f) return; // room tone, not speech

            float cost = rms >= floor * shoutBandMultiplier ? shoutCostMultiplier
                : rms <= floor * whisperBandMultiplier ? whisperCostMultiplier
                : 1f;

            AddNoise(speechNoisePerSecond * cost * seconds);
        }

        public VoiceLevel Classify(float rms)
        {
            float floor = Mathf.Max(0.001f, MicCalibration.NoiseFloor);
            if (rms <= floor * 1.1f) return VoiceLevel.Silent;
            if (rms >= floor * shoutBandMultiplier) return VoiceLevel.Shout;
            if (rms <= floor * whisperBandMultiplier) return VoiceLevel.Whisper;
            return VoiceLevel.Normal;
        }

        private static bool ListenerActive =>
            HauntDirector.ExistingInstance != null && HauntDirector.ExistingInstance.IsLoopActive(HauntLoopId.SilenceProtocol);

        // Held until the report window closes: the Listener measures the mic itself, and fighting
        // the report's live recording for the device isn't worth it. Also waits out cutscenes/the demon.
        private static bool CanSummonNow()
        {
            if (GameFlowManager.State != GameFlowState.DayGameplay) return false;
            if (IncidentReportManager.Instance != null && IncidentReportManager.Instance.IsReportOpen) return false;
            if (CinematicPlayer.IsAnyPlaying || DemonAnomaly.AnyRevealed) return false;
            return true;
        }

        private void Summon()
        {
            bool came = HauntDirector.Instance != null && HauntDirector.Instance.TriggerNow(HauntLoopId.SilenceProtocol);

            Level = levelAfterTrigger;
            _cooldownUntil = Time.unscaledTime + triggerCooldownSeconds;

            // Not summoned = tutorial night or another haunt already running: warn instead of punish.
            _hud?.ShowWarning(came ? "Too loud. Something heard you..." : "Too loud. Something almost heard you.");

            if (showDebugInfo)
                Debug.Log($"NoiseMeter: filled - Listener {(came ? "summoned" : "blocked (tutorial or another haunt)")}.", this);
        }

        [ContextMenu("Debug/Fill Meter")]
        private void DebugFill() => AddNoise(Max);
    }
}
