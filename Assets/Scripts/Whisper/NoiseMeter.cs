using GameLogic;
using GameLogic.Data;
using GameLogic.Flow;
using GameLogic.Night;
using GameLogic.SpawnAndTime;
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

            float before = Normalized;
            Level = Mathf.Min(Max, Level + amount);
            _lastNoiseTime = Time.unscaledTime;
            WarnOnThresholds(before, Normalized);

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

        // The bar alone never says what it is for - tell the player when it crosses each step.
        private void WarnOnThresholds(float before, float after)
        {
            if (after >= 0.8f && before < 0.8f)
                _hud?.ShowWarning("They can hear you. Stop talking.");
            else if (after >= 0.5f && before < 0.5f)
                _hud?.ShowWarning("You're getting loud...");
        }

        public VoiceLevel Classify(float rms)
        {
            float floor = Mathf.Max(0.001f, MicCalibration.NoiseFloor);
            if (rms <= floor * 1.1f) return VoiceLevel.Silent;
            if (rms >= floor * shoutBandMultiplier) return VoiceLevel.Shout;
            if (rms <= floor * whisperBandMultiplier) return VoiceLevel.Whisper;
            return VoiceLevel.Normal;
        }

        // The "Listener" is the stealth anomaly (a VoiceResponse.Silence kind, e.g. Hooded Figure).
        private static bool ListenerActive
        {
            get
            {
                foreach (var anomaly in Anomaly.ActiveAnomalies)
                {
                    if (anomaly != null && anomaly.State != AnomalyState.Resolved
                        && anomaly.Definition != null && anomaly.Definition.voiceResponse == VoiceResponse.Silence)
                        return true;
                }
                return false;
            }
        }

        // Also waits out cutscenes and the Demon.
        private static bool CanSummonNow()
        {
            if (GameFlowManager.State != GameFlowState.DayGameplay) return false;
            if (CinematicPlayer.IsAnyPlaying || DemonAnomaly.AnyRevealed) return false;
            return true;
        }

        private void Summon()
        {
            bool came = TrySpawnListener();

            Level = levelAfterTrigger;
            _cooldownUntil = Time.unscaledTime + triggerCooldownSeconds;

            // Not summoned = tutorial night or one already out: warn instead of punish.
            _hud?.ShowWarning(came ? "Too loud. Something heard you..." : "Too loud. Something almost heard you.");

            if (showDebugInfo)
                Debug.Log($"NoiseMeter: filled - Listener {(came ? "summoned" : "blocked (tutorial or already out)")}.", this);
        }

        private static bool TrySpawnListener()
        {
            var glitch = FindFirstObjectByType<GlitchDirector>();
            if (glitch != null && glitch.GetFlag("tutorial")) return false; // night 1 teaches, it doesn't punish
            if (ListenerActive) return false;

            var scheduler = AnomalyScheduler.Instance;
            var library = NightContentLibrary.Load();
            if (scheduler == null || library == null) return false;

            foreach (var def in library.anomalies)
            {
                if (def == null || def.prefab == null || def.voiceResponse != VoiceResponse.Silence) continue;
                return scheduler.SpawnNow(def.prefab) != null;
            }
            return false;
        }

        [ContextMenu("Debug/Summon Listener Now")]
        public void DebugSummonListener() => Summon();

        [ContextMenu("Debug/Fill Meter")]
        private void DebugFill() => AddNoise(Max);
    }
}
