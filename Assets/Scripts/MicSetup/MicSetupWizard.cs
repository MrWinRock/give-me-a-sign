using System;
using System.Collections.Generic;
using MainMenu;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Whisper;

namespace MicSetup
{
    /// <summary>
    /// "Microphone Setup.exe": measures the room noise, a normal voice and a loud voice, derives the Noise Meter thresholds
    /// and stores them in MicCalibrationStore. A window prefab hosted by the MicSetup scene (first launch) or opened as a
    /// modal overlay from the Control Panel. Uses unscaled time only and a single microphone session (MicAmplitudeMonitor).
    /// </summary>
    public class MicSetupWizard : MonoBehaviour
    {
        public enum WizardMode { Scene, Overlay }
        public enum Result { Finished, Skipped, Cancelled, UsedDefaults }

        private enum Page { Quiet, Normal, Loud, Test }
        private enum Problem { None, NoDevice, Unavailable, Disconnected }

        [Header("Layout")]
        [SerializeField] private RectTransform windowFrame;
        [SerializeField] private Image dim;
        [Tooltip("Window scale when it is the whole screen (MicSetup scene, 1920x1080 canvas).")]
        [SerializeField] private float sceneScale = 2f;
        [Tooltip("Window scale when opened over the main menu (1024x768 canvas).")]
        [SerializeField] private float overlayScale = 1.25f;

        [Header("Window")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Wizard body")]
        [SerializeField] private GameObject stepRoot;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI instructionText;
        [SerializeField] private TMP_Dropdown deviceDropdown;
        [SerializeField] private Slider gainSlider;
        [SerializeField] private TextMeshProUGUI gainValueText;
        [SerializeField] private NoiseMeter meter;
        [SerializeField] private GameObject keyHintRow;
        [SerializeField] private Image keyCapFill;
        [SerializeField] private TextMeshProUGUI keyCapText;
        [SerializeField] private TextMeshProUGUI keyHintText;
        [SerializeField] private GameObject progressRoot;
        [SerializeField] private Image[] progressBlocks = new Image[0];
        [SerializeField] private TextMeshProUGUI feedbackText;
        [SerializeField] private Button skipButton;
        [SerializeField] private TextMeshProUGUI skipLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI nextLabel;
        [SerializeField] private Button useDefaultsButton;

        [Header("No microphone / unavailable")]
        [SerializeField] private GameObject problemRoot;
        [SerializeField] private TextMeshProUGUI problemText;
        [SerializeField] private Button continueTypedButton;
        [SerializeField] private Button rescanButton;

        [Header("Colours")]
        [SerializeField] private Color progressOn = new Color32(0x31, 0x6A, 0xC5, 0xFF);
        [SerializeField] private Color progressOff = new Color32(0xD6, 0xD2, 0xC2, 0xFF);
        [SerializeField] private Color keyCapIdle = new Color32(0xF7, 0xF5, 0xEA, 0xFF);
        [SerializeField] private Color keyCapHeld = new Color32(0xB9, 0xD1, 0xEA, 0xFF);
        [SerializeField] private Color feedbackNormal = new Color32(0x00, 0x00, 0x00, 0xFF);
        [SerializeField] private Color feedbackGood = new Color32(0x1E, 0x6B, 0x1E, 0xFF);
        [SerializeField] private Color feedbackWarn = new Color32(0xA0, 0x30, 0x00, 0xFF);

        [Header("Text")]
        [SerializeField] private string windowTitle = "Microphone Setup.exe";
        [SerializeField] private string[] pageHeaders = { "Step 1 of 3: Stay quiet", "Step 2 of 3: Speak normally", "Step 3 of 3: Speak loudly", "Finished: Try it out" };
        [SerializeField] private string quietInstruction = "Stay quiet for a few seconds while we listen to your room.";
        [SerializeField] private string normalInstruction = "Say the phrase in your normal speaking voice.";
        [SerializeField] private string loudInstruction = "Now say it LOUDLY, like you really want to be heard.";
        [SerializeField] private string testInstruction = "Whisper, speak normally and shout. The meter should read LOW, NORMAL and LOUD. Go back if it looks wrong.";
        [SerializeField] private string keyHintFormat = "Hold the key and say: \"{0}\"";
        [SerializeField] private string listeningFeedback = "Listening...";
        [SerializeField] private string quietDoneFeedback = "Captured. Click Next to continue.";
        [SerializeField] private string capturedFeedback = "Captured.";
        [SerializeField] private string noisyFeedback = "It's quite noisy here. Results may be less accurate.";
        [SerializeField] private string tooShortFeedback = "That was too short. Hold the key a little longer while you speak.";
        [SerializeField] private string noSpeechFeedback = "We couldn't hear you. Try again closer to the microphone.";
        [SerializeField] private string noDifferenceFeedback = "We couldn't hear a difference. Try again closer to the microphone.";
        [SerializeField] private string quietMicFeedback = "We can barely hear you. Raise Mic volume or move closer to the microphone, then try again.";
        [SerializeField] private string loudNotLouderFeedback = "That wasn't louder than your normal voice. Speak louder, or go Back and redo the normal step more quietly.";
        [SerializeField] private string normalTooLoudFeedback = "Your normal voice is already too loud for this microphone to hear anything louder. Speak more quietly or lower Mic volume, then try again.";
        [SerializeField] private string holdToCaptureFormat = "Hold {0}, say the phrase, then release. Next unlocks once it is captured.";
        [SerializeField] private string gainChangedFeedback = "Mic volume changed - that recording was dropped. Try again.";
        [SerializeField] private string gainValueFormat = "{0}%";
        [SerializeField] private string testFeedback = "Looks good? Click Finish to save.";
        [SerializeField] private string disconnectedFeedback = "Microphone disconnected. Choose a microphone to continue.";
        [SerializeField] private string noDeviceMessage = "No microphone detected.";
        [SerializeField] private string unavailableMessage = "The microphone is unavailable. Check that Windows allows this app to use it.";
        [SerializeField] private string skipLabelScene = "Skip setup";
        [SerializeField] private string skipLabelOverlay = "Cancel";
        [SerializeField] private string nextText = "Next >";
        [SerializeField] private string finishText = "Finish";
        [SerializeField] private string systemDefaultDeviceLabel = "System default";
        [SerializeField] private string statusMessage = "This setup appears only once. You can change it later in Control Panel.";

        public event Action<Result> Closed;

        public bool IsOpen { get; private set; }

        private MicCalibrationConfig _config;
        private MicCalibrationMeasurer _measurer;
        private MicAmplitudeMonitor _monitor;
        private WizardMode _mode;
        private Page _page;
        private Problem _problem;

        private readonly List<string> _deviceNames = new List<string>();
        private string _selectedDevice; // null = system default
        private string _deviceSignature = string.Empty;
        private float _nextDeviceCheck;
        private float _monitorStartedAt;

        private bool _quietRunning, _capturing;
        private bool _hasFloor, _hasNormal, _hasLoud;
        private float _floorDb, _normalDb, _loudDb;
        private int _failures;
        private MicCalibrationData _derived;
        private bool _micWasAvailable;
        private float _openGain, _gain;

        // ───────────────────────────── lifecycle ─────────────────────────────

        void Awake()
        {
            if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);
            if (closeButton != null) closeButton.onClick.AddListener(OnSkipClicked);
            if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
            if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
            if (useDefaultsButton != null) useDefaultsButton.onClick.AddListener(OnUseDefaultsClicked);
            if (continueTypedButton != null) continueTypedButton.onClick.AddListener(OnContinueTypedClicked);
            if (rescanButton != null) rescanButton.onClick.AddListener(() => RefreshDevices(force: true));
            if (deviceDropdown != null) deviceDropdown.onValueChanged.AddListener(OnDeviceChosen);
            if (gainSlider != null) gainSlider.onValueChanged.AddListener(OnGainChanged);
        }

        void OnDestroy()
        {
            ShutDownCapture();
        }

        public void Open(WizardMode mode)
        {
            _mode = mode;
            _config = MicCalibrationStore.Config;
            _measurer = new MicCalibrationMeasurer(_config);
            IsOpen = true;

            if (windowFrame != null) windowFrame.localScale = Vector3.one * (mode == WizardMode.Scene ? sceneScale : overlayScale);
            if (dim != null) dim.enabled = mode == WizardMode.Overlay;
            if (titleText != null) titleText.text = windowTitle;
            if (statusText != null) statusText.text = statusMessage;
            if (skipLabel != null) skipLabel.text = mode == WizardMode.Scene ? skipLabelScene : skipLabelOverlay;
            if (keyCapText != null) keyCapText.text = _config.pushToTalkKey.ToString().ToUpperInvariant();

            _selectedDevice = MicCalibrationStore.ResolveDevice();
            _failures = 0;
            _hasFloor = _hasNormal = _hasLoud = false;
            _derived = null;

            _openGain = _gain = ControlPanelWindow.MicGain;
            if (gainSlider != null)
            {
                ControlPanelWindow.ApplyMicGainRange(gainSlider);
                gainSlider.SetValueWithoutNotify(_gain);
            }
            ShowGain();

            gameObject.SetActive(true);
            RefreshDevices(force: true);
            StartCapture();
            ShowPage(Page.Quiet);
        }

        // ───────────────────────────── per frame ─────────────────────────────

        void Update()
        {
            if (!IsOpen) return;

            if (Time.unscaledTime >= _nextDeviceCheck)
            {
                _nextDeviceCheck = Time.unscaledTime + 0.5f;
                RefreshDevices(force: false);
                CheckUnavailable();
            }

            HandleKeys();

            if (_problem != Problem.None || _monitor == null) return;

            float rms = _monitor.RawLevel;
            float db = _measurer.ToDb(rms);
            bool warm = _monitor.FramesRead > 0;

            if (meter != null)
            {
                meter.ExternalActive = true;
                if (warm) meter.FeedExternal(rms);
            }

            if (!warm) return;
            float dt = Time.unscaledDeltaTime;

            if (_page == Page.Quiet && _quietRunning) UpdateQuiet(db, dt);
            else if (_page == Page.Normal || _page == Page.Loud) UpdateVoiceCapture(db, dt);
        }

        private void HandleKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (_mode == WizardMode.Overlay && keyboard.escapeKey.wasPressedThisFrame)
            {
                OnSkipClicked();
                return;
            }

            // Enter presses the default button, unless a button already has focus (Tab navigation), which submits itself.
            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (enter && EventSystem.current != null && (selected == null || selected.GetComponent<Slider>() != null))
            {
                if (_problem == Problem.None && nextButton != null && nextButton.gameObject.activeInHierarchy && nextButton.interactable) OnNextClicked();
                else if (_problem != Problem.None && continueTypedButton != null && continueTypedButton.gameObject.activeInHierarchy) OnContinueTypedClicked();
            }
        }

        // ───────────────────────────── pages ─────────────────────────────

        private void ShowPage(Page page)
        {
            _page = page;
            _capturing = false;
            _quietRunning = false;

            if (stepRoot != null) stepRoot.SetActive(_problem == Problem.None);
            if (problemRoot != null) problemRoot.SetActive(_problem == Problem.NoDevice || _problem == Problem.Unavailable);

            if (headerText != null) headerText.text = page < (Page)pageHeaders.Length ? pageHeaders[(int)page] : string.Empty;

            string key = _config.pushToTalkKey.ToString().ToUpperInvariant();
            if (instructionText != null)
            {
                instructionText.text = page == Page.Quiet ? quietInstruction
                                     : page == Page.Normal ? normalInstruction
                                     : page == Page.Loud ? loudInstruction
                                     : testInstruction;
            }

            bool voicePage = page == Page.Normal || page == Page.Loud;
            if (keyHintRow != null) keyHintRow.SetActive(voicePage);
            if (keyHintText != null) keyHintText.text = string.Format(keyHintFormat, _config.phrase);
            if (keyCapText != null) keyCapText.text = key;
            if (progressRoot != null) progressRoot.SetActive(page == Page.Quiet);
            SetProgress(0f);

            if (backButton != null) backButton.interactable = page != Page.Quiet;
            if (nextLabel != null) nextLabel.text = page == Page.Test ? finishText : nextText;
            if (useDefaultsButton != null) useDefaultsButton.gameObject.SetActive(_failures >= _config.failedAttemptsBeforeDefaultsOffered && voicePage);

            switch (page)
            {
                case Page.Quiet:
                    _hasFloor = false;
                    _measurer.BeginQuiet();
                    _quietRunning = _problem == Problem.None;
                    SetFeedback(listeningFeedback, feedbackNormal);
                    if (meter != null) meter.ClearPreview();
                    break;

                case Page.Normal:
                    if (_hasNormal) SetFeedback(capturedFeedback, feedbackGood);
                    else SetFeedback(string.Format(holdToCaptureFormat, key), feedbackNormal);
                    if (meter != null) meter.ClearPreview();
                    break;

                case Page.Loud:
                    if (_hasLoud) SetFeedback(capturedFeedback, feedbackGood);
                    else SetFeedback(string.Format(holdToCaptureFormat, key), feedbackNormal);
                    if (meter != null) meter.ClearPreview();
                    break;

                case Page.Test:
                    DerivePreview();
                    SetFeedback(testFeedback, feedbackNormal);
                    break;
            }

            RefreshNextButton();
        }

        private void RefreshNextButton()
        {
            if (nextButton == null) return;

            bool ready = _problem == Problem.None && (_page == Page.Quiet ? _hasFloor
                                                     : _page == Page.Normal ? _hasNormal
                                                     : _page == Page.Loud ? _hasLoud
                                                     : _derived != null);
            nextButton.interactable = ready;
        }

        private void UpdateQuiet(float db, float dt)
        {
            _measurer.AddQuiet(db, dt);
            SetProgress(_measurer.QuietProgress01);
            if (!_measurer.QuietDone) return;

            _quietRunning = false;
            _floorDb = _measurer.QuietFloorDb();
            _hasFloor = true;
            // A new floor invalidates voices measured against the old one.
            _hasNormal = _hasLoud = false;

            if (_floorDb > _config.noisyFloorWarningDb) SetFeedback(noisyFeedback, feedbackWarn);
            else SetFeedback(quietDoneFeedback, feedbackGood);
            RefreshNextButton();
        }

        private void UpdateVoiceCapture(float db, float dt)
        {
            var keyboard = Keyboard.current;
            bool held = keyboard != null && keyboard[_config.pushToTalkKey].isPressed;
            if (keyCapFill != null) keyCapFill.color = held ? keyCapHeld : keyCapIdle;

            if (held && !_capturing)
            {
                _capturing = true;
                _measurer.BeginUtterance();
                SetFeedback(listeningFeedback, feedbackNormal);
            }

            if (held && _capturing)
            {
                _measurer.AddUtterance(db, dt, _floorDb);
            }
            else if (!held && _capturing)
            {
                _capturing = false;
                EvaluateUtterance(_measurer.EndUtterance());
            }
        }

        private void EvaluateUtterance(MicCalibrationMeasurer.Utterance u)
        {
            if (!u.longEnough)
            {
                SetFeedback(tooShortFeedback, feedbackWarn);
                return;
            }

            if (!u.hasSpeech)
            {
                Fail(noSpeechFeedback);
                return;
            }

            if (_page == Page.Normal)
            {
                if (!_measurer.NormalIsClearlyAboveFloor(u.levelDb, _floorDb))
                {
                    Fail(_gain < ControlPanelWindow.MaxMicGain ? quietMicFeedback : noDifferenceFeedback);
                    return;
                }
                // A normal voice this close to clipping would make the Loud step impossible, so reject it here.
                if (!_measurer.LoudHasRoomAbove(u.levelDb, GainDb(_gain))) { Fail(normalTooLoudFeedback); return; }
                _normalDb = u.levelDb;
                _hasNormal = true;
                _hasLoud = false; // the loud voice must be re-checked against the new normal
            }
            else
            {
                if (!_measurer.LoudIsClearlyAboveNormal(u.levelDb, _normalDb)) { Fail(loudNotLouderFeedback); return; }
                _loudDb = u.levelDb;
                _hasLoud = true;
            }

            SetFeedback(capturedFeedback, feedbackGood);
            RefreshNextButton();
        }

        private void Fail(string message)
        {
            _failures++;
            SetFeedback(message, feedbackWarn);
            if (useDefaultsButton != null) useDefaultsButton.gameObject.SetActive(_failures >= _config.failedAttemptsBeforeDefaultsOffered);
        }

        // ───────────────────────────── buttons ─────────────────────────────

        private void OnNextClicked()
        {
            if (nextButton != null && !nextButton.interactable) return;

            if (_page == Page.Test)
            {
                Finish();
                return;
            }

            if (_page == Page.Quiet) ShowPage(Page.Normal);
            else if (_page == Page.Normal) ShowPage(Page.Loud);
            else ShowPage(Page.Test);
        }

        private void OnBackClicked()
        {
            if (_page == Page.Test) ShowPage(Page.Loud);
            else if (_page == Page.Loud) ShowPage(Page.Normal);
            else if (_page == Page.Normal) ShowPage(Page.Quiet);
        }

        // Scene: skipping counts as done (defaults). Overlay: Cancel keeps the previous calibration untouched.
        private void OnSkipClicked()
        {
            if (!IsOpen) return;

            if (_mode == WizardMode.Scene)
            {
                MicCalibrationStore.SaveDefaults();
                Close(Result.Skipped);
            }
            else
            {
                Close(Result.Cancelled);
            }
        }

        private void OnUseDefaultsClicked()
        {
            MicCalibrationStore.SaveDefaults();
            Close(Result.UsedDefaults);
        }

        private void OnContinueTypedClicked()
        {
            // Re-running with no microphone must not wipe a good earlier calibration.
            if (_mode == WizardMode.Overlay && MicCalibrationStore.IsSetupCompleted)
            {
                Close(Result.Cancelled);
                return;
            }

            MicCalibrationStore.SaveDefaults();
            Close(Result.UsedDefaults);
        }

        private void Finish()
        {
            if (_derived == null) return;

            // The Control Panel's microphone choice follows the wizard's, so gameplay captures from the calibrated device.
            PlayerPrefs.SetString(ControlPanelWindow.MicDeviceKey, _selectedDevice ?? string.Empty);
            _derived.deviceName = _selectedDevice ?? string.Empty;
            MicCalibrationStore.Save(_derived);
            Close(Result.Finished);
        }

        private void Close(Result result)
        {
            if (!IsOpen) return;
            IsOpen = false;

            ShutDownCapture();

            // The slider writes the gain live; Cancel puts back the one the player had when the wizard opened.
            if (result == Result.Cancelled) PlayerPrefs.SetFloat(ControlPanelWindow.MicGainKey, _openGain);
            PlayerPrefs.Save();

            gameObject.SetActive(false);
            Closed?.Invoke(result);
        }

        // ───────────────────────────── mic volume ─────────────────────────────

        // Captured dB values are rescaled by the gain ratio so finished steps survive. An in-progress quiet
        // measurement restarts and an in-progress utterance is dropped, since either would mix two gains.
        private void OnGainChanged(float value)
        {
            float shiftDb = GainDb(value) - GainDb(_gain);
            _gain = value;

            PlayerPrefs.SetFloat(ControlPanelWindow.MicGainKey, value);
            if (_monitor != null) _monitor.SetGain(value);
            ShowGain();

            if (Mathf.Abs(shiftDb) < 0.001f) return;

            if (_quietRunning)
            {
                _measurer.BeginQuiet();
                SetProgress(0f);
                return;
            }

            if (_capturing)
            {
                _capturing = false;
                SetFeedback(gainChangedFeedback, feedbackWarn);
            }

            if (_hasFloor) _floorDb = ShiftDb(_floorDb, shiftDb);
            if (_hasNormal) _normalDb = ShiftDb(_normalDb, shiftDb);
            if (_hasLoud) _loudDb = ShiftDb(_loudDb, shiftDb);

            // Turning the volume up can push a normal voice too close to clipping for a louder one to exist.
            if (_hasNormal && !_measurer.LoudHasRoomAbove(_normalDb, GainDb(_gain)))
            {
                _hasNormal = _hasLoud = false;
                _derived = null;
                if (_page != Page.Quiet)
                {
                    ShowPage(Page.Normal);
                    SetFeedback(normalTooLoudFeedback, feedbackWarn);
                }
                RefreshNextButton();
                return;
            }

            if (_page == Page.Test && _derived != null) DerivePreview();
        }

        // Same 0.01 floor the mic consumers apply to the gain.
        private static float GainDb(float gain) => 20f * Mathf.Log10(Mathf.Max(0.01f, gain));

        // Boosted readings may exceed 0 dB, up to the loudest boost the slider allows.
        private float ShiftDb(float db, float shiftDb) => Mathf.Clamp(db + shiftDb, _config.silenceDb, GainDb(ControlPanelWindow.MaxMicGain));

        private void DerivePreview()
        {
            _derived = _measurer.Derive(_floorDb, _normalDb, _loudDb, _selectedDevice);
            if (meter != null) meter.ApplyPreview(_derived);
        }

        private void ShowGain()
        {
            if (gainValueText != null) gainValueText.text = string.Format(gainValueFormat, Mathf.RoundToInt(_gain * 100f));
        }

        // ───────────────────────────── devices / microphone ─────────────────────────────

        private void OnDeviceChosen(int index)
        {
            _selectedDevice = index <= 0 || index - 1 >= _deviceNames.Count ? null : _deviceNames[index - 1];

            // Changing the device restarts the current step.
            _hasFloor = _hasNormal = _hasLoud = false;
            _derived = null;
            _problem = Problem.None;
            StartCapture();
            ShowPage(Page.Quiet);
        }

        private void RefreshDevices(bool force)
        {
            var devices = Microphone.devices;
            string signature = string.Join("|", devices);
            if (!force && signature == _deviceSignature) return;
            _deviceSignature = signature;

            _deviceNames.Clear();
            _deviceNames.AddRange(devices);

            if (deviceDropdown != null)
            {
                var options = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData(systemDefaultDeviceLabel) };
                foreach (var name in _deviceNames) options.Add(new TMP_Dropdown.OptionData(name));

                deviceDropdown.ClearOptions();
                deviceDropdown.AddOptions(options);

                int index = _selectedDevice == null ? 0 : _deviceNames.IndexOf(_selectedDevice) + 1;
                deviceDropdown.SetValueWithoutNotify(Mathf.Max(0, index));
                deviceDropdown.RefreshShownValue();
            }

            if (!IsOpen) return;

            if (_deviceNames.Count == 0)
            {
                EnterProblem(Problem.NoDevice);
                return;
            }

            // The chosen microphone vanished mid-setup: pause and let the player choose again.
            if (_selectedDevice != null && !_deviceNames.Contains(_selectedDevice))
            {
                _selectedDevice = null;
                EnterProblem(Problem.Disconnected);
                return;
            }

            // A microphone appeared (or was re-plugged) while the "none found" message was up.
            if (_problem == Problem.NoDevice || _problem == Problem.Unavailable)
            {
                _problem = Problem.None;
                StartCapture();
                ShowPage(Page.Quiet);
            }
            else if (_problem == Problem.Disconnected)
            {
                if (deviceDropdown != null) deviceDropdown.SetValueWithoutNotify(0);
                _problem = Problem.None;
                StartCapture();
                ShowPage(Page.Quiet);
            }
        }

        // Devices are listed but the OS delivers no audio (permission denied / device held by another app).
        private void CheckUnavailable()
        {
            if (_problem != Problem.None || _monitor == null) return;
            if (_monitor.FramesRead > 0 || _monitor.IsReceivingAudio) { _micWasAvailable = true; return; }
            if (_micWasAvailable) return;

            if (Time.unscaledTime - _monitorStartedAt > 3f) EnterProblem(Problem.Unavailable);
        }

        private void EnterProblem(Problem problem)
        {
            if (_problem == problem) return;
            _problem = problem;

            _quietRunning = false;
            _capturing = false;
            ShutDownCapture();

            bool message = problem == Problem.NoDevice || problem == Problem.Unavailable;
            if (stepRoot != null) stepRoot.SetActive(!message);
            if (problemRoot != null) problemRoot.SetActive(message);
            if (problemText != null) problemText.text = problem == Problem.NoDevice ? noDeviceMessage : unavailableMessage;

            if (problem == Problem.Disconnected)
            {
                SetFeedback(disconnectedFeedback, feedbackWarn);
                if (nextButton != null) nextButton.interactable = false;
            }
        }

        private void StartCapture()
        {
            if (_monitor == null) _monitor = gameObject.AddComponent<MicAmplitudeMonitor>();

            _monitor.StopMonitoring();
            _monitor.SetDevice(_selectedDevice);
            _monitor.StartMonitoring();
            _monitorStartedAt = Time.unscaledTime;
            _micWasAvailable = false;

            if (!_monitor.IsAvailable) EnterProblem(Problem.NoDevice);
        }

        private void ShutDownCapture()
        {
            if (_monitor != null) _monitor.StopMonitoring();

            if (meter != null)
            {
                meter.ExternalActive = false;
                meter.ClearPreview();
            }
        }

        // ───────────────────────────── small helpers ─────────────────────────────

        private void SetProgress(float t01)
        {
            if (progressBlocks == null) return;

            int lit = Mathf.RoundToInt(t01 * progressBlocks.Length);
            for (int i = 0; i < progressBlocks.Length; i++)
                if (progressBlocks[i] != null) progressBlocks[i].color = i < lit ? progressOn : progressOff;
        }

        private void SetFeedback(string text, Color color)
        {
            if (feedbackText == null) return;
            feedbackText.text = text;
            feedbackText.color = color;
        }
    }
}
