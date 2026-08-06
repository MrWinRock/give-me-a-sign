using Audio;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameLogic.Flow
{
    /// <summary>
    /// Sprint 6, S-608 (MVP). Escape toggles a simple in-game pause: freezes Time.timeScale, which
    /// also freezes every Update-driven system in the game for free - NightTimer, haunt loop
    /// countdowns, glitch timers - since they all read Time.deltaTime/Time.time rather than the
    /// unscaled variants. Shows Master/Music volume controls bound straight to AudioManager, plus
    /// Resume and Quit to Menu.
    ///
    /// Deliberately does NOT include microphone device reselection mid-game: WhisperMicInput's
    /// push-to-talk pipeline may be mid-stream, and tearing down/restarting Microphone.Start on a
    /// live recording is exactly the class of hang GameFlowManager.EndNight's own comments already
    /// warn about for scene loads. Mic selection stays in the Control Panel's pre-game Options
    /// screen, where it's safe to change.
    ///
    /// Runtime-built UI, same disposable-builder style as every HUD since Sprint 4 - no scene
    /// wiring needed for the UI itself, only for this component being present at all. Unlike the
    /// display-only HUDs, this one needs real clicks, so its GraphicRaycaster stays enabled and it
    /// uses actual UnityEngine.UI.Button components (the scene already has an EventSystem).
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("Loaded by Quit to Menu.")]
        [SerializeField] private string mainMenuSceneName = "StartScene";

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private const float VolumeStep = 0.1f;

        public bool IsPaused { get; private set; }

        private GameObject _root;
        private TextMeshProUGUI _masterValueText;
        private TextMeshProUGUI _musicValueText;

        void Update()
        {
            bool escPressed;
#if ENABLE_INPUT_SYSTEM
            escPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (escPressed) Toggle();
        }

        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            Time.timeScale = 0f;

            if (_root == null) BuildUi();
            _root.SetActive(true);
            RefreshVolumeLabels();

            if (showDebugInfo) Debug.Log("PauseMenuController: paused.", this);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;

            if (_root != null) _root.SetActive(false);

            if (showDebugInfo) Debug.Log("PauseMenuController: resumed.", this);
        }

        private void QuitToMenu()
        {
            Time.timeScale = 1f; // never leave the next scene frozen
            SceneManager.LoadScene(mainMenuSceneName);
        }

        void OnDestroy()
        {
            // Safety: never leave the game frozen if this object is torn down while paused (e.g.
            // scene unload mid-pause, such as a night ending via GameFlowManager before Resume).
            if (IsPaused) Time.timeScale = 1f;
        }

        // ── volume nudge ─────────────────────────────────────────────────────────────────

        private void NudgeMaster(float delta)
        {
            if (AudioManager.Instance == null) return;
            AudioManager.Instance.MasterVolume = Mathf.Clamp01(AudioManager.Instance.MasterVolume + delta);
            RefreshVolumeLabels();
        }

        private void NudgeMusic(float delta)
        {
            if (AudioManager.Instance == null) return;
            AudioManager.Instance.MusicVolume = Mathf.Clamp01(AudioManager.Instance.MusicVolume + delta);
            RefreshVolumeLabels();
        }

        private void RefreshVolumeLabels()
        {
            var audio = AudioManager.Instance;
            if (_masterValueText != null) _masterValueText.text = audio != null ? $"{audio.MasterVolume * 100f:0}%" : "-";
            if (_musicValueText != null) _musicValueText.text = audio != null ? $"{audio.MusicVolume * 100f:0}%" : "-";
        }

        // ── UI build ─────────────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            _root = new GameObject("PauseMenuCanvas", typeof(RectTransform));
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // above every gameplay HUD, below DeathSequenceHud (1000)

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _root.AddComponent<GraphicRaycaster>(); // enabled - this one needs to actually catch clicks

            CreateImage(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.6f), Stretch());

            var panel = CreateImage(_root.transform, "Panel", new Color(0.08f, 0.08f, 0.1f, 0.92f),
                Anchored(new Vector2(0.5f, 0.5f), new Vector2(480f, 340f)));

            CreateText(panel.transform, "Title", 36f, Anchored(new Vector2(0.5f, 0.88f), new Vector2(400f, 50f)))
                .text = "PAUSED";

            BuildVolumeRow(panel.transform, "Master", 0.62f, NudgeMaster, out _masterValueText);
            BuildVolumeRow(panel.transform, "Music", 0.46f, NudgeMusic, out _musicValueText);

            BuildButton(panel.transform, "ResumeButton", "Resume", new Vector2(0.5f, 0.24f), Resume);
            BuildButton(panel.transform, "QuitButton", "Quit to Menu", new Vector2(0.5f, 0.09f), QuitToMenu);
        }

        private void BuildVolumeRow(Transform parent, string label, float yAnchor, System.Action<float> nudge, out TextMeshProUGUI valueText)
        {
            CreateText(parent, $"{label}Label", 22f, Anchored(new Vector2(0.28f, yAnchor), new Vector2(140f, 34f)))
                .text = label;

            BuildButton(parent, $"{label}Minus", "-", new Vector2(0.62f, yAnchor), () => nudge(-VolumeStep), 40f);

            var value = CreateText(parent, $"{label}Value", 22f, Anchored(new Vector2(0.74f, yAnchor), new Vector2(70f, 34f)));
            valueText = value;

            BuildButton(parent, $"{label}Plus", "+", new Vector2(0.86f, yAnchor), () => nudge(VolumeStep), 40f);
        }

        private Button BuildButton(Transform parent, string name, string label, Vector2 anchor, System.Action onClick, float width = 220f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.12f);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 44f);
            rect.anchoredPosition = Vector2.zero;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());

            var text = CreateText(go.transform, "Label", 22f, Stretch());
            text.text = label;
            text.raycastTarget = false;

            return button;
        }

        private static Image CreateImage(Transform parent, string name, Color color, System.Action<RectTransform> layout)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true; // Dim/Panel deliberately eat clicks - this is a real pause, not passthrough

            layout(go.GetComponent<RectTransform>());
            return image;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, System.Action<RectTransform> layout)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            layout(go.GetComponent<RectTransform>());
            return text;
        }

        private static System.Action<RectTransform> Stretch() => rect =>
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        };

        private static System.Action<RectTransform> Anchored(Vector2 anchor, Vector2 size) => rect =>
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        };
    }
}
