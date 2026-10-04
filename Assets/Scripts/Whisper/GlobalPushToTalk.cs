using System.Text;
using Audio;
using GameLogic;
using GameLogic.Story;
using Report;
using UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Whisper
{
    /// <summary>
    /// The guard's walkie-talkie. Hold V to transmit from anywhere: a click marks the mic open, a
    /// hiss loops while held, and on release whatever was said is filed as a radio report with
    /// IncidentReportManager. Everything said still flows through NoiseMeter and voice prompts.
    /// </summary>
    public class GlobalPushToTalk : MonoBehaviour
    {
        public const string OpenCloseSound = "MicOpenAndClose";
        public const string HoldSound = "MicHold";

        [Tooltip("Auto-found in the scene if left empty.")]
        [SerializeField] private WhisperMicInput mic;

        [Tooltip("Seconds after releasing V to wait for Whisper's last segment before filing the report.")]
        [Min(0f)] [SerializeField] private float finalizeGraceSeconds = 1f;

        [Tooltip("While the game holds the mic open, seconds of silence after speech before it counts as one report.")]
        [Min(0.3f)] [SerializeField] private float forcedFileGapSeconds = 1.2f;

        public static GlobalPushToTalk Instance { get; private set; }

        public bool IsMicOpen => _talking;

        private float _lastSpeechAt;

        private PushToTalkHud _hud;
        private bool _talking;
        private bool _forcedOpen;
        private float _fileAt = -1f;
        private readonly StringBuilder _transmission = new StringBuilder();
        private float _rmsSum;
        private int _rmsCount;

        void Awake()
        {
            if (mic == null) mic = FindFirstObjectByType<WhisperMicInput>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            Instance = this;
            _hud = PushToTalkHud.Create();
            WhisperMicInput.OnSpeechChunk += HandleSpeechChunk;
        }

        void OnDisable()
        {
            WhisperMicInput.OnSpeechChunk -= HandleSpeechChunk;
            StopTalking(playClick: false);
            _hud?.Destroy();
            _hud = null;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (mic == null) return;

            // Game frozen (pause menu): the mic must not keep listening, even a forced-open one.
            if (Time.timeScale <= 0f)
            {
                StopTalking(playClick: false);
                return;
            }

            if (_forcedOpen)
            {
                if (!_talking) StartTalking();
                else if (IsKeyDown()) AudioManager.Instance?.Play(OpenCloseSound); // V "keys the mic" - it's already live

                // The mic never releases, so a pause in speech ends the transmission instead.
                if (_transmission.Length > 0 && Time.unscaledTime - _lastSpeechAt >= forcedFileGapSeconds)
                    FileTransmission();
                return;
            }

            if (_talking && (!IsKeyHeld() || IsBlocked()))
            {
                StopTalking(playClick: true);
            }
            else if (!_talking && IsKeyDown() && !IsBlocked())
            {
                if (mic.IsModelLoading)
                    _hud?.ShowStatus(PlayerMessages.Text(MessageId.ModelLoading), new Color(0.8f, 0.8f, 0.8f), PlayerMessages.Blink(MessageId.ModelLoading));
                else
                    StartTalking();
            }

            if (_fileAt >= 0f && Time.unscaledTime >= _fileAt)
                FileTransmission();
        }

        public void ShowHint(string text, bool blink = false) => _hud?.SetHint(text, blink);

        // The game holds the mic live (V can't close it). Speech is still filed as reports, one per pause.
        public void SetForcedOpen(bool forced)
        {
            if (_forcedOpen == forced) return;
            _forcedOpen = forced;

            if (forced) return; // Update() opens the mic on the next frame

            StopTalking(playClick: true);
            _fileAt = -1f;
            _transmission.Clear();
        }

        // Average of the chunks that are actually speech, so pauses between words don't drag a shout down.
        private void HandleSpeechChunk(float rms, float seconds)
        {
            if (!_talking) return;

            var meter = NoiseMeter.Instance;
            var level = meter != null ? meter.Classify(rms) : VoiceLevel.Normal;
            if (level == VoiceLevel.Silent) return;

            _rmsSum += rms;
            _rmsCount++;

        }

        private VoiceLevel TransmissionLevel()
        {
            if (_rmsCount == 0) return VoiceLevel.Normal;
            var meter = NoiseMeter.Instance;
            return meter != null ? meter.Classify(_rmsSum / _rmsCount) : VoiceLevel.Normal;
        }

        // Called by WhisperMicInput for every recognized chunk.
        public void OnSpeech(string text)
        {
            if (!_talking && _fileAt < 0f) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            _lastSpeechAt = Time.unscaledTime;
            if (_transmission.Length > 0) _transmission.Append(' ');
            _transmission.Append(text.Trim());
        }

        private void StartTalking()
        {
            _talking = true;
            _fileAt = -1f;
            _transmission.Clear();
            _rmsSum = 0f;
            _rmsCount = 0;

            mic.BeginPushToTalk();
            var audio = AudioManager.Instance;
            audio?.Play(OpenCloseSound);
            audio?.PlayLoop(HoldSound);
            _hud?.SetTalking(true);
        }

        private void StopTalking(bool playClick)
        {
            if (!_talking) return;

            _talking = false;
            if (mic != null) mic.EndPushToTalk();

            var audio = AudioManager.Instance;
            audio?.StopLoop(HoldSound);
            if (playClick) audio?.Play(OpenCloseSound);
            _hud?.SetTalking(false);

            // Whisper's final segment lands shortly after release, so file after a grace period.
            if (playClick) _fileAt = Time.unscaledTime + finalizeGraceSeconds;
        }

        private void FileTransmission()
        {
            _fileAt = -1f;
            string spoken = _transmission.ToString();
            _transmission.Clear();
            var level = TransmissionLevel();
            _rmsSum = 0f;
            _rmsCount = 0;

            var reports = IncidentReportManager.Instance;
            if (reports == null) return;

            var outcome = reports.FileRadioReport(spoken, level);
            Debug.Log($"[Walkie] heard '{spoken}' ({level}) -> {outcome} (active anomalies: {DescribeActiveAnomalies()})", this);

            string heard = string.IsNullOrWhiteSpace(spoken) ? PlayerMessages.Text(MessageId.NothingHeard) : $"heard: \"{Trim(spoken, 55)}\"";

            switch (outcome)
            {
                case IncidentReportManager.RadioReportOutcome.NotAReport:
                    _hud?.ShowStatus(heard, new Color(1f, 1f, 1f, 0.8f));
                    break;
                case IncidentReportManager.RadioReportOutcome.Confirmed:
                    _hud?.ShowStatus(PlayerMessages.Text(MessageId.ReportConfirmed), new Color(0.4f, 0.9f, 0.4f), PlayerMessages.Blink(MessageId.ReportConfirmed));
                    break;
                case IncidentReportManager.RadioReportOutcome.NeedRoom:
                    _hud?.ShowStatus($"{PlayerMessages.Text(MessageId.ReportNeedRoom)}  {heard}", new Color(0.95f, 0.85f, 0.4f), PlayerMessages.Blink(MessageId.ReportNeedRoom));
                    break;
                case IncidentReportManager.RadioReportOutcome.NeedWhat:
                    _hud?.ShowStatus($"{PlayerMessages.Text(MessageId.ReportNeedWhat)}  {heard}", new Color(0.95f, 0.85f, 0.4f), PlayerMessages.Blink(MessageId.ReportNeedWhat));
                    break;
                // Wrong volume for the threat: no words - the volume slider is how the player learns it.
                case IncidentReportManager.RadioReportOutcome.TooLoud:
                case IncidentReportManager.RadioReportOutcome.TooQuiet:
                    break;
                case IncidentReportManager.RadioReportOutcome.Negative:
                    _hud?.ShowStatus($"{PlayerMessages.Text(MessageId.ReportNegative)}  {heard}", new Color(0.95f, 0.6f, 0.2f), PlayerMessages.Blink(MessageId.ReportNegative));
                    break;
            }
        }

        private static string Trim(string text, int max) =>
            text.Length <= max ? text : text.Substring(0, max) + "...";

        private static string DescribeActiveAnomalies()
        {
            var sb = new StringBuilder();
            foreach (var a in Anomaly.ActiveAnomalies)
            {
                if (a == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(a.name).Append(a.IsReported ? " [reported]" : "").Append(" ").Append(a.State);
            }
            return sb.Length > 0 ? sb.ToString() : "none";
        }

        private static bool IsKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.V);
#endif
        }

        private static bool IsKeyHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.vKey.isPressed;
#else
            return Input.GetKey(KeyCode.V);
#endif
        }

        // The report form (if enabled) has its own Hold-to-Speak button; never fight it for the mic.
        private static bool IsBlocked()
        {
            if (Time.timeScale <= 0f) return true;
            if (CinematicPlayer.IsAnyPlaying) return true;
            var report = IncidentReportManager.Instance;
            return report != null && report.IsReportOpen;
        }
    }
}
