using System.Collections;
using System.Collections.Generic;
using GameLogic.SpawnAndTime;
using TMPro;
using UIHelpers;
using UnityEngine;
using UnityEngine.UI;
using Whisper;

namespace Report
{
    /// <summary>
    /// Windows-XP-styled Incident Report window. Owns all presentation state: titlebar
    /// min/max/close, live clock, the location dropdown, the Push-to-Talk recording flow,
    /// and the status bar (STANDBY / REC / READY / SENT / ERROR / ALERT).
    ///
    /// IncidentReportManager drives this via Show()/Hide()/ShowRecognizedKeyword()/
    /// ShowResult()/SetAlertVisual(); this script never decides pass/fail itself.
    /// </summary>
    public class IncidentReportUI : MonoBehaviour
    {
        private enum FormStatus { Standby, Recording, Ready }

        [Header("Window Root")]
        [SerializeField] private GameObject windowRoot;
        [SerializeField] private RectTransform windowRect;
        [SerializeField] private GameObject contentRoot; // MenuBar + Body + StatusBar (hidden when minimized)
        [SerializeField] private Vector2 defaultSize = new Vector2(380f, 440f);
        [SerializeField] private Vector2 maximizedSize = new Vector2(480f, 560f);
        [SerializeField] private float minimizedHeight = 26f;

        [Header("Titlebar")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button minimizeButton;
        [SerializeField] private Button maximizeButton;
        [SerializeField] private Button closeButton;

        [Header("Info Row")]
        [SerializeField] private TextMeshProUGUI caseValueText;
        [SerializeField] private TextMeshProUGUI timeValueText;
        [Tooltip("Drives the clock display so it always matches the main HUD's night timer instead of the real-world wall clock. Auto-found in Awake() if left empty.")]
        [SerializeField] private NightTimer nightTimer;
        [SerializeField] private TextMeshProUGUI officerValueText;
        [SerializeField] private string officerId = "SEC-04";

        [Header("Location Field")]
        [SerializeField] private TMP_Dropdown locationDropdown;

        [Header("Push-to-Talk")]
        [SerializeField] private PointerHoldButton pttHoldButton;
        [SerializeField] private Image pttButtonImage;
        [SerializeField] private TextMeshProUGUI pttButtonLabel;
        [SerializeField] private GameObject recStatusRow;
        [SerializeField] private Image recDotImage;
        [SerializeField] private Color pttIdleColor = new Color(0.94f, 0.94f, 0.91f);   // ~#F0F0E8
        [SerializeField] private Color pttPressedColor = new Color(0.78f, 0.78f, 0.74f); // ~#C8C8BC, inverted/depressed

        [Header("Recognized Keyword")]
        [SerializeField] private TMP_InputField recognizedField; // set readOnly = true, keeps XP styling without the disabled-gray look

        [Header("Buttons")]
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button submitButton;

        [Header("Status Bar")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image statusBadgeImage;
        [SerializeField] private TextMeshProUGUI statusBadgeText;

        [Header("Status Colors")]
        [SerializeField] private Color standbyColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color recordingColor = new Color(0.8f, 0f, 0f);
        [SerializeField] private Color readyColor = new Color(0.18f, 0.49f, 0.2f);
        [SerializeField] private Color sentColor = new Color(0.04f, 0.14f, 0.42f);   // #0A246A
        [SerializeField] private Color errorColor = new Color(0.8f, 0.4f, 0f);
        [SerializeField] private Color alertColor = new Color(0.8f, 0f, 0f);          // #CC0000

        [Header("Result Feedback")]
        [SerializeField] private float recordingDotPulseSpeed = 6f;

        [Header("Voice Hookup")]
        [SerializeField] private WhisperMicInput whisperMicInput;

        private IncidentReportManager _manager;
        private FormStatus _formStatus = FormStatus.Standby;
        private bool _isRecording;
        private bool _hasKeyword;
        private bool _isAlertActive;
        private bool _locked; // true while showing the SENT/ERROR result flash - ignores status/alert refresh
        private bool _isMinimized;
        private bool _isMaximized;
        private Coroutine _pulseRoutine;

        /// <summary>True while the SENT/ERROR result badge is showing (interactions ignored until Hide()).</summary>
        public bool IsLocked => _locked;

        void Awake()
        {
            if (nightTimer == null)
                nightTimer = FindObjectOfType<NightTimer>();

            InvokeRepeating(nameof(UpdateClock), 0f, 1f);
        }

        public void Initialize(IncidentReportManager manager, List<string> roomNames)
        {
            _manager = manager;

            if (locationDropdown != null)
            {
                locationDropdown.ClearOptions();
                locationDropdown.AddOptions(roomNames);
            }

            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(OnSubmitClicked);
                submitButton.onClick.AddListener(OnSubmitClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnCancelClicked);
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCancelClicked);
                closeButton.onClick.AddListener(OnCancelClicked);
            }

            if (minimizeButton != null)
            {
                minimizeButton.onClick.RemoveListener(OnMinimizeClicked);
                minimizeButton.onClick.AddListener(OnMinimizeClicked);
            }

            if (maximizeButton != null)
            {
                maximizeButton.onClick.RemoveListener(OnMaximizeClicked);
                maximizeButton.onClick.AddListener(OnMaximizeClicked);
            }

            if (pttHoldButton != null)
            {
                // Press to Speak: each click toggles recording on/off, rather than requiring the
                // button to be held down. Only the press event is used - release is ignored so a
                // normal click doesn't immediately stop the recording it just started.
                pttHoldButton.onPress.RemoveListener(OnPttButtonPressed);
                pttHoldButton.onPress.AddListener(OnPttButtonPressed);
            }

            if (recognizedField != null)
                recognizedField.readOnly = true;
        }

        public void Show(int caseNumber, List<string> roomNames)
        {
            _locked = false;
            _isMinimized = false;
            _isMaximized = false;
            ApplyWindowSize(defaultSize);

            if (windowRoot != null) windowRoot.SetActive(true);
            if (contentRoot != null) contentRoot.SetActive(true);

            if (caseValueText != null) caseValueText.text = $"#{caseNumber:D4}";
            if (officerValueText != null) officerValueText.text = officerId;
            UpdateClock();

            if (locationDropdown != null)
            {
                locationDropdown.ClearOptions();
                locationDropdown.AddOptions(roomNames);
                locationDropdown.value = 0;
                locationDropdown.RefreshShownValue();
                locationDropdown.interactable = true;
            }

            if (recognizedField != null)
                recognizedField.text = "";
            _hasKeyword = false;
            _isRecording = false;
            ApplyPttVisual(false);
            SetInteractionLocked(false);

            _formStatus = FormStatus.Standby;
            RefreshStatusVisual();
            UpdateSubmitInteractable();
        }

        public void Hide()
        {
            if (_isRecording)
                EndPushToTalk();

            _locked = false;
            if (windowRoot != null) windowRoot.SetActive(false);
        }

        /// <summary>Called by IncidentReportManager as speech is recognized while the PTT button is held.</summary>
        public void ShowRecognizedKeyword(string keyword)
        {
            if (recognizedField != null)
                recognizedField.text = keyword;

            _hasKeyword = !string.IsNullOrWhiteSpace(keyword);
            UpdateSubmitInteractable();
        }

        /// <summary>
        /// Called by IncidentReportManager right after Submit is evaluated, to flash a SENT/ERROR
        /// badge before the window actually closes.
        /// </summary>
        public void ShowResult(bool success)
        {
            _locked = true;
            SetInteractionLocked(true);

            if (statusBadgeText != null)
                statusBadgeText.text = success ? "SENT" : "ERROR";
            if (statusBadgeImage != null)
                statusBadgeImage.color = success ? sentColor : errorColor;
            if (statusText != null)
                statusText.text = success
                    ? "Report filed successfully."
                    : "Report rejected — details do not match.";
        }

        /// <summary>Called externally (Anomaly) whenever an active jumpscare starts/ends.</summary>
        public void SetAlertVisual(bool active)
        {
            _isAlertActive = active;
            RefreshStatusVisual();
        }

        private void OnPttButtonPressed()
        {
            if (_locked) return;

            if (_isRecording)
                EndPushToTalk();
            else
                BeginPushToTalk();
        }

        public void BeginPushToTalk()
        {
            if (_isRecording || _locked) return;

            _isRecording = true;
            whisperMicInput?.BeginPushToTalk();
            ApplyPttVisual(true);

            _formStatus = FormStatus.Recording;
            RefreshStatusVisual();
        }

        public void EndPushToTalk()
        {
            if (!_isRecording) return;

            _isRecording = false;
            whisperMicInput?.EndPushToTalk();
            ApplyPttVisual(false);

            _formStatus = _hasKeyword ? FormStatus.Ready : FormStatus.Standby;
            RefreshStatusVisual();
            UpdateSubmitInteractable();
        }

        private void OnSubmitClicked()
        {
            if (_manager == null || locationDropdown == null || _locked) return;

            if (_isRecording)
                EndPushToTalk();

            string selectedRoom = locationDropdown.options[locationDropdown.value].text;
            _manager.SubmitReport(selectedRoom);
        }

        private void OnCancelClicked()
        {
            if (_locked) return;
            _manager?.CancelReport();
        }

        private void OnMinimizeClicked()
        {
            _isMinimized = !_isMinimized;

            if (contentRoot != null)
                contentRoot.SetActive(!_isMinimized);

            ApplyWindowSize(_isMinimized ? new Vector2(defaultSize.x, minimizedHeight) : (_isMaximized ? maximizedSize : defaultSize));
        }

        private void OnMaximizeClicked()
        {
            _isMaximized = !_isMaximized;

            if (_isMinimized)
            {
                _isMinimized = false;
                if (contentRoot != null) contentRoot.SetActive(true);
            }

            ApplyWindowSize(_isMaximized ? maximizedSize : defaultSize);
        }

        private void ApplyWindowSize(Vector2 size)
        {
            if (windowRect != null)
                windowRect.sizeDelta = size;
        }

        private void SetInteractionLocked(bool locked)
        {
            if (submitButton != null) submitButton.interactable = !locked && _hasKeyword;
            if (cancelButton != null) cancelButton.interactable = !locked;
            if (locationDropdown != null) locationDropdown.interactable = !locked;
            if (pttHoldButton != null) pttHoldButton.interactable = !locked;
        }

        private void ApplyPttVisual(bool recording)
        {
            if (pttButtonImage != null)
                pttButtonImage.color = recording ? pttPressedColor : pttIdleColor;

            if (pttButtonLabel != null)
                pttButtonLabel.text = recording ? "● Recording..." : "Press to Speak";

            if (recStatusRow != null)
                recStatusRow.SetActive(recording);

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }

            if (recording && recDotImage != null)
                _pulseRoutine = StartCoroutine(PulseRecordingDot());
        }

        private IEnumerator PulseRecordingDot()
        {
            while (true)
            {
                float t = (Mathf.Sin(Time.unscaledTime * recordingDotPulseSpeed) + 1f) * 0.5f;
                var c = recDotImage.color;
                c.a = Mathf.Lerp(0.3f, 1f, t);
                recDotImage.color = c;
                yield return null;
            }
        }

        private void UpdateClock()
        {
            if (timeValueText == null) return;

            timeValueText.text = nightTimer != null
                ? NightTimer.FormatGameTime(nightTimer.GetGameTimeHours())
                : "--:--:--";
        }

        private void RefreshStatusVisual()
        {
            if (_locked) return; // SENT/ERROR flash is showing - leave it alone until Hide()

            if (_isAlertActive)
            {
                ApplyBadge("! ALERT", alertColor);
                if (statusText != null)
                    statusText.text = "ANOMALY ACTIVE — SUBMIT REPORT IMMEDIATELY";
                return;
            }

            switch (_formStatus)
            {
                case FormStatus.Standby:
                    ApplyBadge("STANDBY", standbyColor);
                    if (statusText != null) statusText.text = "Awaiting report details.";
                    break;
                case FormStatus.Recording:
                    ApplyBadge("REC", recordingColor);
                    if (statusText != null) statusText.text = "Listening... speak the anomaly type.";
                    break;
                case FormStatus.Ready:
                    ApplyBadge("READY", readyColor);
                    if (statusText != null) statusText.text = "Keyword captured. Review and submit.";
                    break;
            }
        }

        private void ApplyBadge(string label, Color color)
        {
            if (statusBadgeText != null) statusBadgeText.text = label;
            if (statusBadgeImage != null) statusBadgeImage.color = color;
        }

        private void UpdateSubmitInteractable()
        {
            if (submitButton != null)
                submitButton.interactable = _hasKeyword && !_locked;
        }
    }
}
