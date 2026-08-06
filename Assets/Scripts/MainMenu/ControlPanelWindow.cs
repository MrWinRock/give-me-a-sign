using System.Collections.Generic;
using Audio;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// Start &gt; Control Panel, i.e. the settings screen. Three tabs (Audio / Display / Input),
    /// everything persisted to PlayerPrefs.
    ///
    /// Volume lives in <see cref="AudioManager"/> (which already owns Vol_Master / Vol_Music /
    /// Vol_Sfx and saves them itself) so the sliders here bind straight to it - "Ambience" is the
    /// Music channel. Only the settings AudioManager doesn't own get their own keys below.
    ///
    /// OK writes and applies. Cancel restores the values that were saved when the window opened.
    /// <see cref="ApplySavedSettings"/> is called once on scene start by DesktopManager so the
    /// saved resolution / fullscreen state apply even if the player never opens this window.
    /// </summary>
    public class ControlPanelWindow : XPWindowController
    {
        // ---- PlayerPrefs keys (volumes are AudioManager's own Vol_* keys) ----
        public const string MicGainKey = "Opt_MicGain";
        public const string SubtitlesKey = "Opt_Subtitles";
        public const string ReduceFlashingKey = "Opt_ReduceFlashing";
        public const string ResWidthKey = "Opt_ResWidth";
        public const string ResHeightKey = "Opt_ResHeight";
        public const string FullscreenKey = "Opt_Fullscreen";
        public const string PushToTalkKeyKey = "Opt_PTTKey";
        public const string MicDeviceKey = "Opt_MicDevice";

        /// <summary>Placeholder shown when no capture device exists. Never saved as a device name.</summary>
        private const string NoMicrophoneLabel = "No microphone detected";

        [Header("Tabs")]
        [SerializeField] private Button[] tabButtons = new Button[0];
        [SerializeField] private GameObject[] tabPanels = new GameObject[0];
        [SerializeField] private Image[] tabBackgrounds = new Image[0];
        [SerializeField] private Color activeTabColor = XPPalette.Hex("#ECE9D8");
        [SerializeField] private Color inactiveTabColor = XPPalette.Hex("#D6D2C2");

        [Header("Audio Tab")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider ambienceVolumeSlider;
        [SerializeField] private Slider micGainSlider;
        [SerializeField] private Toggle subtitlesToggle;
        [SerializeField] private Toggle reduceFlashingToggle;

        [Header("Display Tab")]
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private Toggle fullscreenToggle;

        [Header("Input Tab")]
        [SerializeField] private Button pushToTalkRebindButton;
        [SerializeField] private TextMeshProUGUI pushToTalkKeyLabel;
        [SerializeField] private TMP_Dropdown micDeviceDropdown;
        [SerializeField] private string rebindPromptText = "Press any key...";

        [Header("Buttons")]
        [SerializeField] private Button okButton;
        [SerializeField] private Button cancelButton;

        private readonly List<Resolution> _resolutions = new List<Resolution>();
        private bool _rebinding;
        private Snapshot _openSnapshot;

        /// <summary>The values that were saved when the window opened, so Cancel can restore them.</summary>
        private struct Snapshot
        {
            public float master, ambience, micGain;
            public bool subtitles, reduceFlashing, fullscreen;
            public int resWidth, resHeight;
            public string pttKey, micDevice;
        }

        // =======================================================================================
        // Static read API - other systems read settings from here, never from PlayerPrefs directly
        // =======================================================================================

        public static float MicGain => PlayerPrefs.GetFloat(MicGainKey, 1f);
        public static bool SubtitlesEnabled => PlayerPrefs.GetInt(SubtitlesKey, 1) == 1;
        public static bool ReduceFlashingEffects => PlayerPrefs.GetInt(ReduceFlashingKey, 0) == 1;
        public static string MicrophoneDevice => PlayerPrefs.GetString(MicDeviceKey, string.Empty);

        /// <summary>The saved Push-to-Talk key, as an Input System <see cref="Key"/>. Defaults to Space.</summary>
        public static Key PushToTalkKey
        {
            get
            {
                var saved = PlayerPrefs.GetString(PushToTalkKeyKey, Key.Space.ToString());
                return System.Enum.TryParse(saved, out Key key) ? key : Key.Space;
            }
        }

        /// <summary>
        /// Applies the saved display settings. Called once on scene start by DesktopManager.
        /// Skipped in the Editor - forcing a resolution on the Game view every play is nothing
        /// but a nuisance while iterating.
        /// </summary>
        public static void ApplySavedSettings()
        {
#if !UNITY_EDITOR
            bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            var mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

            int width = PlayerPrefs.GetInt(ResWidthKey, Screen.width);
            int height = PlayerPrefs.GetInt(ResHeightKey, Screen.height);

            if (width > 0 && height > 0)
                Screen.SetResolution(width, height, mode);
            else
                Screen.fullScreenMode = mode;
#endif
        }

        // =======================================================================================
        // Lifecycle
        // =======================================================================================

        protected override void Awake()
        {
            base.Awake();

            BuildResolutionOptions();
            BuildMicDeviceOptions();

            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                int index = i;
                tabButtons[i].onClick.AddListener(() => ShowTab(index));
            }

            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterChanged);
            if (ambienceVolumeSlider != null) ambienceVolumeSlider.onValueChanged.AddListener(OnAmbienceChanged);
            if (pushToTalkRebindButton != null) pushToTalkRebindButton.onClick.AddListener(BeginRebind);

            if (okButton != null) okButton.onClick.AddListener(OnOkClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        }

        protected override void OnShown()
        {
            _openSnapshot = CaptureSaved();
            LoadIntoUI();
            ShowTab(0);
        }

        protected override void OnHiding()
        {
            CancelRebind();
        }

        void Update()
        {
            if (!_rebinding) return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                CancelRebind();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelRebind();
                return;
            }

            foreach (var key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;
                CompleteRebind(key.keyCode);
                return;
            }
        }

        // =======================================================================================
        // Tabs
        // =======================================================================================

        public void ShowTab(int index)
        {
            for (int i = 0; i < tabPanels.Length; i++)
                if (tabPanels[i] != null) tabPanels[i].SetActive(i == index);

            for (int i = 0; i < tabBackgrounds.Length; i++)
                if (tabBackgrounds[i] != null) tabBackgrounds[i].color = i == index ? activeTabColor : inactiveTabColor;
        }

        // =======================================================================================
        // Load / save
        // =======================================================================================

        private Snapshot CaptureSaved()
        {
            var audio = AudioManager.Instance;
            return new Snapshot
            {
                master = audio != null ? audio.MasterVolume : PlayerPrefs.GetFloat("Vol_Master", 1f),
                ambience = audio != null ? audio.MusicVolume : PlayerPrefs.GetFloat("Vol_Music", 1f),
                micGain = MicGain,
                subtitles = SubtitlesEnabled,
                reduceFlashing = ReduceFlashingEffects,
                fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1,
                resWidth = PlayerPrefs.GetInt(ResWidthKey, Screen.width),
                resHeight = PlayerPrefs.GetInt(ResHeightKey, Screen.height),
                pttKey = PlayerPrefs.GetString(PushToTalkKeyKey, Key.Space.ToString()),
                micDevice = MicrophoneDevice,
            };
        }

        private void LoadIntoUI()
        {
            ApplySnapshotToUI(_openSnapshot);
        }

        private void ApplySnapshotToUI(Snapshot snap)
        {
            if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(snap.master);
            if (ambienceVolumeSlider != null) ambienceVolumeSlider.SetValueWithoutNotify(snap.ambience);
            if (micGainSlider != null) micGainSlider.SetValueWithoutNotify(snap.micGain);
            if (subtitlesToggle != null) subtitlesToggle.SetIsOnWithoutNotify(snap.subtitles);
            if (reduceFlashingToggle != null) reduceFlashingToggle.SetIsOnWithoutNotify(snap.reduceFlashing);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(snap.fullscreen);

            if (resolutionDropdown != null)
            {
                int index = IndexOfResolution(snap.resWidth, snap.resHeight);
                if (index >= 0)
                {
                    resolutionDropdown.SetValueWithoutNotify(index);
                    resolutionDropdown.RefreshShownValue();
                }
            }

            if (micDeviceDropdown != null)
            {
                int index = IndexOfMicDevice(snap.micDevice);
                if (index >= 0)
                {
                    micDeviceDropdown.SetValueWithoutNotify(index);
                    micDeviceDropdown.RefreshShownValue();
                }
            }

            if (pushToTalkKeyLabel != null)
                pushToTalkKeyLabel.text = snap.pttKey;
        }

        private void OnOkClicked()
        {
            CancelRebind();
            SaveFromUI();
            Hide();
        }

        /// <summary>The titlebar X discards changes, exactly like Cancel - volume applies live,
        /// so treating X as "apply" would silently keep a slider the player was only auditioning.</summary>
        protected override void OnCloseButtonClicked() => OnCancelClicked();

        private void OnCancelClicked()
        {
            CancelRebind();

            // Sliders apply live, so Cancel has to push the opening values back through.
            var audio = AudioManager.Instance;
            if (audio != null)
            {
                audio.MasterVolume = _openSnapshot.master;
                audio.MusicVolume = _openSnapshot.ambience;
            }

            ApplySnapshotToUI(_openSnapshot);
            WriteSnapshot(_openSnapshot);
            Hide();
        }

        private void SaveFromUI()
        {
            var snap = new Snapshot
            {
                master = masterVolumeSlider != null ? masterVolumeSlider.value : _openSnapshot.master,
                ambience = ambienceVolumeSlider != null ? ambienceVolumeSlider.value : _openSnapshot.ambience,
                micGain = micGainSlider != null ? micGainSlider.value : _openSnapshot.micGain,
                subtitles = subtitlesToggle != null ? subtitlesToggle.isOn : _openSnapshot.subtitles,
                reduceFlashing = reduceFlashingToggle != null ? reduceFlashingToggle.isOn : _openSnapshot.reduceFlashing,
                fullscreen = fullscreenToggle != null ? fullscreenToggle.isOn : _openSnapshot.fullscreen,
                resWidth = _openSnapshot.resWidth,
                resHeight = _openSnapshot.resHeight,
                pttKey = pushToTalkKeyLabel != null ? pushToTalkKeyLabel.text : _openSnapshot.pttKey,
                micDevice = _openSnapshot.micDevice,
            };

            if (resolutionDropdown != null && resolutionDropdown.value < _resolutions.Count)
            {
                snap.resWidth = _resolutions[resolutionDropdown.value].width;
                snap.resHeight = _resolutions[resolutionDropdown.value].height;
            }

            if (micDeviceDropdown != null && micDeviceDropdown.options.Count > 0)
            {
                string selected = micDeviceDropdown.options[micDeviceDropdown.value].text;
                // "" means "system default", which is what WhisperMicInput.deviceName expects.
                snap.micDevice = selected == NoMicrophoneLabel ? string.Empty : selected;
            }

            var audio = AudioManager.Instance;
            if (audio != null)
            {
                audio.MasterVolume = snap.master;   // AudioManager persists these itself
                audio.MusicVolume = snap.ambience;
            }

            WriteSnapshot(snap);
            ApplySavedSettings();
        }

        private void WriteSnapshot(Snapshot snap)
        {
            PlayerPrefs.SetFloat(MicGainKey, snap.micGain);
            PlayerPrefs.SetInt(SubtitlesKey, snap.subtitles ? 1 : 0);
            PlayerPrefs.SetInt(ReduceFlashingKey, snap.reduceFlashing ? 1 : 0);
            PlayerPrefs.SetInt(FullscreenKey, snap.fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(ResWidthKey, snap.resWidth);
            PlayerPrefs.SetInt(ResHeightKey, snap.resHeight);
            PlayerPrefs.SetString(PushToTalkKeyKey, snap.pttKey);
            PlayerPrefs.SetString(MicDeviceKey, snap.micDevice ?? string.Empty);
            PlayerPrefs.Save();
        }

        // Volume sliders apply live so the player can hear what they're setting.
        private void OnMasterChanged(float value)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.MasterVolume = value;
        }

        private void OnAmbienceChanged(float value)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.MusicVolume = value;
        }

        // =======================================================================================
        // Dropdown sources
        // =======================================================================================

        private void BuildResolutionOptions()
        {
            _resolutions.Clear();
            if (resolutionDropdown == null) return;

            var seen = new HashSet<long>();
            foreach (var res in Screen.resolutions)
            {
                long id = ((long)res.width << 32) | (uint)res.height;
                if (seen.Add(id))
                    _resolutions.Add(res);
            }

            if (_resolutions.Count == 0)
            {
                // Editor / headless: Screen.resolutions can come back empty.
                _resolutions.Add(new Resolution { width = 1280, height = 720 });
                _resolutions.Add(new Resolution { width = 1600, height = 900 });
                _resolutions.Add(new Resolution { width = 1920, height = 1080 });
            }

            var options = new List<string>(_resolutions.Count);
            foreach (var res in _resolutions)
                options.Add($"{res.width} x {res.height}");

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
        }

        private void BuildMicDeviceOptions()
        {
            if (micDeviceDropdown == null) return;

            var devices = new List<string>();
            foreach (var device in Microphone.devices)
                devices.Add(device);

            if (devices.Count == 0)
                devices.Add(NoMicrophoneLabel);

            micDeviceDropdown.ClearOptions();
            micDeviceDropdown.AddOptions(devices);
        }

        private int IndexOfResolution(int width, int height)
        {
            for (int i = 0; i < _resolutions.Count; i++)
                if (_resolutions[i].width == width && _resolutions[i].height == height)
                    return i;

            return _resolutions.Count > 0 ? _resolutions.Count - 1 : -1;
        }

        private int IndexOfMicDevice(string device)
        {
            if (micDeviceDropdown == null) return -1;

            for (int i = 0; i < micDeviceDropdown.options.Count; i++)
                if (micDeviceDropdown.options[i].text == device)
                    return i;

            return micDeviceDropdown.options.Count > 0 ? 0 : -1;
        }

        // =======================================================================================
        // Push-to-Talk rebind
        // =======================================================================================

        private void BeginRebind()
        {
            if (_rebinding) return;

            // Drop UI focus first. The rebind button stays "selected" after being clicked, so the
            // very key we are about to capture (Space / Enter) would also fire the EventSystem's
            // Submit on it and immediately restart the rebind.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            _rebinding = true;
            if (pushToTalkKeyLabel != null)
                pushToTalkKeyLabel.text = rebindPromptText;
        }

        private void CompleteRebind(Key key)
        {
            _rebinding = false;
            if (pushToTalkKeyLabel != null)
                pushToTalkKeyLabel.text = key.ToString();
        }

        private void CancelRebind()
        {
            if (!_rebinding) return;

            _rebinding = false;
            if (pushToTalkKeyLabel != null)
                pushToTalkKeyLabel.text = _openSnapshot.pttKey;
        }
    }
}
