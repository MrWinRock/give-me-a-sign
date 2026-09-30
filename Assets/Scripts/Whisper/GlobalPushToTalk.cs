using System.Text;
using Audio;
using GameLogic.Story;
using Report;
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

        public static GlobalPushToTalk Instance { get; private set; }

        private PushToTalkHud _hud;
        private bool _talking;
        private float _fileAt = -1f;
        private readonly StringBuilder _transmission = new StringBuilder();

        void Awake()
        {
            if (mic == null) mic = FindFirstObjectByType<WhisperMicInput>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            Instance = this;
            _hud = PushToTalkHud.Create();
        }

        void OnDisable()
        {
            StopTalking(playClick: false);
            _hud?.Destroy();
            _hud = null;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (mic == null) return;

            if (_talking && (!IsKeyHeld() || IsBlocked()))
            {
                StopTalking(playClick: true);
            }
            else if (!_talking && IsKeyDown() && !IsBlocked())
            {
                StartTalking();
            }

            if (_fileAt >= 0f && Time.unscaledTime >= _fileAt)
                FileTransmission();
        }

        // Called by WhisperMicInput for every recognized chunk.
        public void OnSpeech(string text)
        {
            if (!_talking && _fileAt < 0f) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            if (_transmission.Length > 0) _transmission.Append(' ');
            _transmission.Append(text.Trim());
        }

        private void StartTalking()
        {
            _talking = true;
            _fileAt = -1f;
            _transmission.Clear();

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

            var reports = IncidentReportManager.Instance;
            if (reports == null) return;

            switch (reports.FileRadioReport(spoken))
            {
                case IncidentReportManager.RadioReportOutcome.Confirmed:
                    _hud?.ShowStatus("COPY THAT", new Color(0.4f, 0.9f, 0.4f));
                    break;
                case IncidentReportManager.RadioReportOutcome.Negative:
                    _hud?.ShowStatus("NEGATIVE", new Color(0.95f, 0.6f, 0.2f));
                    break;
            }
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
