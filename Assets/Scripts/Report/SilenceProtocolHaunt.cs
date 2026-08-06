using System.Collections;
using Audio;
using GameLogic.Data;
using GameLogic.Flow;
using GameLogic.Night;
using UnityEngine;
using Whisper;

namespace Report
{
    /// <summary>
    /// HL-3 Silence Protocol. Something finds the player by sound, not sight: stay under the
    /// mic's danger threshold and it eventually leaves; whispering (a soft but non-zero level)
    /// burns the same progress down faster, which is the "lean in and whisper" moment the
    /// mechanic is built around. Three sustained loud strikes end the night.
    ///
    /// Deliberately NOT tied to any anomaly prefab yet - Sprint 3's "Listener" content (a proper
    /// sprite/animation) can attach to the same HauntBeat.room later without this class changing;
    /// for now the encounter is a full-screen event, the same way DemonAnomaly's jumpscare is a
    /// full-screen overlay rather than something the player has to be looking at a sprite for.
    ///
    /// No STT/keyword requirement in this version: reliably transcribing an actual whisper with a
    /// small Whisper model is unproven, so the escape hatch is amplitude-only (see the whisper
    /// band below) - fully robust, testable with pure math, no dependency on speech recognition
    /// working at whisper volume. VoicePromptSystem is wired in for a later enhancement (grant an
    /// instant pass if Whisper happens to catch the anomaly's keyword) once that is confirmed
    /// reliable in playtesting; it is not required for the mechanic to function today.
    /// </summary>
    public class SilenceProtocolHaunt : MonoBehaviour, IHauntLoop
    {
        [Header("Timing")]
        [Tooltip("Seconds of accumulated quiet needed to make it leave.")]
        [SerializeField] private float requiredQuietSeconds = 18f;
        [Tooltip("Progress accumulates this many times faster while whispering (audible but under the whisper ceiling).")]
        [SerializeField] private float whisperProgressMultiplier = 2.5f;
        [Tooltip("The danger threshold has to be crossed continuously for this long before it counts as a strike - a single spike (a cough) isn't punished.")]
        [SerializeField] private float dangerSustainSeconds = 2f;
        [SerializeField] private int strikesToLose = 3;
        [Tooltip("Hard cap so a stuck encounter (e.g. no working mic input at all) can never soft-lock the night.")]
        [SerializeField] private float safetyMaxDurationSeconds = 90f;

        [Header("Thresholds (multiples of the calibrated noise floor)")]
        [Tooltip("Above this multiple of the floor, the player is still audible but counts as 'whispering' rather than silent.")]
        [SerializeField] private float whisperBandMultiplier = 3f;
        [Tooltip("Above this multiple of the floor, it can hear the player.")]
        [SerializeField] private float dangerMultiplier = 8f;

        [Header("Audio (best-effort - a missing library entry just stays silent)")]
        [SerializeField] private string revealSoundName = "ListenerReveal";
        [SerializeField] private string strikeSoundName = "ListenerStrike";
        [SerializeField] private string escapeSoundName = "ListenerEscape";

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        public HauntLoopId LoopId => HauntLoopId.SilenceProtocol;
        public bool IsActive { get; private set; }

        // Stays exclusive: two full-screen "stay quiet or die" encounters at once would just be
        // confusing, not scarier. Radio Check (HL-4) is the one loop that is allowed to interrupt
        // this - see IHauntLoop.IsExclusive.
        public bool IsExclusive => true;

        private MicAmplitudeMonitor _monitor;
        private MicCalibrationRunner _calibrator;
        private SilenceProtocolHud _hud;
        private RoomDefinition _room;
        private Coroutine _encounter;

        private float _progressSeconds;
        private float _dangerSustain;
        private int _strikes;
        private float _elapsedTotal;

        void Awake()
        {
            _monitor = gameObject.AddComponent<MicAmplitudeMonitor>();
            _calibrator = gameObject.AddComponent<MicCalibrationRunner>();
        }

        void OnEnable() => HauntDirector.Instance?.Register(this);

        void OnDisable()
        {
            // ExistingInstance, not Instance: this runs during scene teardown, where
            // HauntDirector's own OnDestroy may already have run - going through the
            // auto-creating Instance property here would spawn an orphan GameObject mid-teardown.
            HauntDirector.ExistingInstance?.Unregister(this);

            if (IsActive)
                EndEncounter(caught: false, silent: true);
        }

        public void Trigger(HauntBeat beat)
        {
            if (IsActive) return; // HauntDirector already guards this - belt and braces

            _room = beat.room;
            _encounter = StartCoroutine(BeginEncounter());
        }

        private IEnumerator BeginEncounter()
        {
            IsActive = true;
            _progressSeconds = 0f;
            _dangerSustain = 0f;
            _strikes = 0;
            _elapsedTotal = 0f;

            _hud = SilenceProtocolHud.Create();
            _hud.SetStrikes(0, strikesToLose);
            _hud.SetInstruction("Something is listening. Stay quiet.");

            if (!MicCalibration.HasCalibrated)
            {
                _hud.SetInstruction("Calibrating microphone... stay quiet for a moment.");

                bool calibrationDone = false;
                _calibrator.OnCompleted += _ => calibrationDone = true;
                _calibrator.Run(_monitor);

                while (!calibrationDone)
                    yield return null;

                _hud.SetInstruction("Something is listening. Stay quiet.");
            }

            _monitor.StartMonitoring();
            AudioManager.Instance?.Play(revealSoundName);

            while (IsActive)
            {
                _elapsedTotal += Time.deltaTime;
                Tick(Time.deltaTime);

                if (IsActive && _elapsedTotal >= safetyMaxDurationSeconds)
                {
                    if (showDebugInfo)
                        Debug.LogWarning("SilenceProtocolHaunt: safety timeout reached - releasing the player.", this);

                    EndEncounter(caught: false);
                    yield break;
                }

                yield return null;
            }
        }

        private void Tick(float dt)
        {
            float level = _monitor.IsAvailable ? _monitor.CurrentLevel : 0f;
            float floor = Mathf.Max(0.001f, MicCalibration.NoiseFloor);
            float whisperCeiling = floor * whisperBandMultiplier;
            float dangerFloor = floor * dangerMultiplier;

            _hud.SetLevel(level, whisperCeiling, dangerFloor);

            // No microphone at all: nothing to measure, so the encounter can't fairly punish the
            // player - let it resolve on the clock alone rather than trapping them.
            bool noMic = !_monitor.IsAvailable;

            if (!noMic && level >= dangerFloor)
            {
                _dangerSustain += dt;
                _hud.SetInstruction("IT HEARD THAT. Go quiet!");

                if (_dangerSustain >= dangerSustainSeconds)
                {
                    RegisterStrike();
                    _dangerSustain = 0f;
                }
                return;
            }

            _dangerSustain = Mathf.Max(0f, _dangerSustain - dt * 2f); // recovers faster than it builds

            bool whispering = !noMic && level > floor * 1.1f && level < whisperCeiling;
            _hud.SetInstruction(whispering ? "Whispering... hold still." : "Stay quiet.");

            float rate = whispering ? whisperProgressMultiplier : 1f;
            _progressSeconds += dt * rate;

            if (_progressSeconds >= requiredQuietSeconds)
                EndEncounter(caught: false);
        }

        private void RegisterStrike()
        {
            _strikes++;
            _hud.SetStrikes(_strikes, strikesToLose);
            _hud.FlashCaught();
            AudioManager.Instance?.Play(strikeSoundName);

            // A close call costs progress but doesn't zero it - three strikes should feel like
            // three real mistakes, not "start entirely over" every time.
            _progressSeconds = Mathf.Max(0f, _progressSeconds - requiredQuietSeconds * 0.34f);

            if (showDebugInfo)
                Debug.Log($"SilenceProtocolHaunt: strike {_strikes}/{strikesToLose}.", this);

            if (_strikes < strikesToLose) return;

            EndEncounter(caught: true);
        }

        /// <summary>
        /// Ends the current encounter. <paramref name="silent"/> is for teardown paths (object
        /// disabled mid-encounter, e.g. scene unload) where nothing should fire - the outcome is
        /// simply abandoned, not scored either way.
        /// </summary>
        private void EndEncounter(bool caught, bool silent = false)
        {
            IsActive = false;

            if (_encounter != null)
            {
                StopCoroutine(_encounter);
                _encounter = null;
            }

            _monitor.StopMonitoring();

            if (_hud != null)
            {
                _hud.Destroy();
                _hud = null;
            }

            if (silent) return;

            if (caught)
            {
                GameFlowManager.Instance?.EndNight(
                    NightOutcome.Negligence,
                    "silence_protocol",
                    _room != null ? _room.roomId : null);
                return;
            }

            AudioManager.Instance?.Play(escapeSoundName);

            if (showDebugInfo)
                Debug.Log($"SilenceProtocolHaunt: escaped after {_elapsedTotal:0.0}s.", this);
        }
    }
}
