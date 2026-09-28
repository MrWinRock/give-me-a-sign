using Audio;
using TMPro;
using UI;
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
    /// Resume and Quit to Menu. XP-chrome + CCTV tint via XPWindowBuilder, same as every other
    /// window in the gameplay scene.
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

        private XPTheme _theme;
        private XPWindowBuilder.Window _window;
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
            // Story cinematics also bind Escape (skip) - don't open the pause menu under one,
            // and don't let a pause opened before the cinematic corrupt its timeScale restore.
            if (GameLogic.Story.CinematicPlayer.IsAnyPlaying) return;

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
            _theme = XPTheme.Load();
            _window = XPWindowBuilder.Build("PauseMenuCanvas", "System Paused.exe", new Vector2(480f, 360f), 900, _theme, Resume, cctv: true);
            _root = _window.Root;

            var panel = _window.Panel;

            BuildVolumeRow(panel, "Master", 70f, NudgeMaster, out _masterValueText);
            BuildVolumeRow(panel, "Music", 116f, NudgeMusic, out _musicValueText);

            XPWindowBuilder.BuildButton(panel, _theme, "Resume", TopCenter(200f, 220f, 40f), Resume);
            XPWindowBuilder.BuildButton(panel, _theme, "Quit to Menu", TopCenter(230f, 268f, 40f), QuitToMenu);
        }

        private void BuildVolumeRow(RectTransform panel, string label, float top, System.Action<float> nudge, out TextMeshProUGUI valueText)
        {
            var labelText = XPWindowBuilder.CreateText(panel, $"{label}Label", 20f, _theme, TopLeft(60f, top, 140f, 30f));
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.text = label;

            XPWindowBuilder.BuildButton(panel, _theme, "-", TopLeft(210f, top - 4f, 38f, 38f), () => nudge(-VolumeStep));

            valueText = XPWindowBuilder.CreateText(panel, $"{label}Value", 20f, _theme, TopLeft(258f, top, 70f, 30f));

            XPWindowBuilder.BuildButton(panel, _theme, "+", TopLeft(338f, top - 4f, 38f, 38f), () => nudge(VolumeStep));
        }

        private static System.Action<RectTransform> TopLeft(float x, float yFromTop, float width, float height) => rect =>
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -yFromTop);
        };

        private static System.Action<RectTransform> TopCenter(float width, float yFromTop, float height) => rect =>
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, -yFromTop);
        };
    }
}
