using System.Collections.Generic;
using Audio;
using DG.Tweening;
using GameLogic.Story;
using Report;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameLogic.Flow
{
    /// <summary>
    /// Escape opens "System Menu.exe": an XP window with Master/Music/SFX trackbars, Resume and Return to main menu.
    /// Opening freezes Time.timeScale (and audio, DOTween, videos); closing restores whatever timeScale was before.
    /// Escape first closes an open Field Manual / Incident Report, and never opens during cutscenes or while a night is ending.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Behaviour")]
        [Tooltip("ESC closes an open Incident Report / Field Manual first; the next ESC opens the menu.")]
        [SerializeField] private bool escClosesOpenWindowsFirst = true;
        [Tooltip("The menu cannot open while the game flow is in one of these states. A night that is already ending is always blocked.")]
        [SerializeField] private GameFlowState[] blockedStates = { GameFlowState.DayEndEvent, GameFlowState.Ending };
        [SerializeField] private int sortingOrder = 900;
        [SerializeField] private bool showStatusBar = true;
        [Tooltip("Adds the green CCTV tint and REC dot used by the other gameplay windows. Off keeps the exact XP colours.")]
        [SerializeField] private bool cctvStyle;

        [Header("Text")]
        [SerializeField] private string windowTitle = "System Menu.exe";
        [SerializeField] private string headerText = "Shift paused";
        [SerializeField] private string audioLegend = "Audio";
        [SerializeField] private string resumeLabel = "Resume";
        [SerializeField] private string returnLabel = "Return to main menu";
        [SerializeField] private string statusHint = "Press Esc to resume";
        [SerializeField] private string pausedBadge = "PAUSED";
        [SerializeField] private string confirmTitle = "System Menu.exe";
        [TextArea(1, 3)]
        [SerializeField] private string confirmMessage = "Return to the main menu? Today's shift progress will be lost.";
        [SerializeField] private string yesLabel = "Yes";
        [SerializeField] private string noLabel = "No";

        [Header("Layout (reference px at scale 1)")]
        [SerializeField] private float windowWidth = 360f;
        [SerializeField] private float windowHeight = 262f;
        [Tooltip("Scales the whole window on the 1920x1080 canvas. 1 = literal 360px wide. Whole numbers keep the 1px borders crisp.")]
        [SerializeField] private float uiScale = 2f;
        [SerializeField] private float dimAlpha = 0.6f;
        [SerializeField] private float titlebarHeight = 26f;
        [SerializeField] private float statusBarHeight = 22f;
        [SerializeField] private float bodyPadding = 14f;
        [SerializeField] private float sectionSpacing = 10f;
        [SerializeField] private float groupPadding = 12f;
        [SerializeField] private float buttonRowExtraTop = 4f;
        [SerializeField] private float buttonSpacing = 8f;
        [SerializeField] private float buttonHeight = 26f;
        [SerializeField] private float returnButtonWidth = 150f;
        [SerializeField] private float resumeButtonWidth = 86f;
        [SerializeField] private float titleSize = 12f;
        [SerializeField] private float titleIconSize = 16f;
        [SerializeField] private float captionButtonSize = 21f;
        [SerializeField] private Vector2 confirmSize = new Vector2(320f, 140f);
        [SerializeField] private float confirmButtonWidth = 76f;

        [Header("Look")]
        [SerializeField] private XPControlStyle style = new XPControlStyle();
        [Tooltip("Optional 16px titlebar icon. Empty draws a small monitor glyph.")]
        [SerializeField] private Sprite titleIcon;

        [Header("Audio rows")]
        [SerializeField] private AudioSettingsPanel.Config audioRows = new AudioSettingsPanel.Config();

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        public bool IsOpen { get; private set; }
        public bool IsPaused => IsOpen;

        private XPTheme _theme;
        private XPWindowBuilder.Window _window;
        private XPWindowBuilder.Window _confirm;
        private GameObject _root;
        private AudioSettingsPanel _audioPanel;
        private Button _resumeButton;
        private Button _noButton;
        private FieldManualUI _fieldManual;
        private float _previousTimeScale = 1f;
        private float _previousDoTweenScale = 1f;
        private bool _previousAudioPause;
        private readonly List<VideoPlayer> _pausedVideos = new List<VideoPlayer>();

        void Update()
        {
            if (!EscapePressed()) return;

            if (ConfirmOpen) { HideConfirm(); return; } // ESC = No
            if (IsOpen) { Close(); return; }
            if (escClosesOpenWindowsFirst && CloseOpenWindow()) return;

            Open();
        }

        void OnDestroy()
        {
            // Never leave the game frozen if this object goes away while paused (scene unload mid-pause).
            if (IsOpen)
            {
                Time.timeScale = _previousTimeScale;
                FreezeEverythingElse(false);
            }

            if (_theme != null) Destroy(_theme);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen || !CanPause()) return;

            IsOpen = true;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            FreezeEverythingElse(true);

            if (_root == null) BuildUi();
            _root.SetActive(true);
            _audioPanel.Refresh();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_resumeButton.gameObject);

            if (showDebugInfo) Debug.Log("PauseMenuController: paused.", this);
        }

        public void Close()
        {
            if (!IsOpen) return;

            HideConfirm();
            IsOpen = false;
            Time.timeScale = _previousTimeScale;
            FreezeEverythingElse(false);

            if (_root != null) _root.SetActive(false);

            if (showDebugInfo) Debug.Log("PauseMenuController: resumed.", this);
        }

        private bool ConfirmOpen => _confirm != null && _confirm.Root.activeSelf;

        private bool CanPause()
        {
            if (CinematicPlayer.IsAnyPlaying) return false;
            if (System.Array.IndexOf(blockedStates, GameFlowManager.State) >= 0) return false;

            var flow = GameFlowManager.Instance;
            return flow == null || !flow.IsNightEnding;
        }

        // True if a window was closed (so this ESC press is spent).
        private bool CloseOpenWindow()
        {
            var report = IncidentReportManager.Instance;
            if (report != null && report.IsReportOpen)
            {
                report.CancelReport();
                return true;
            }

            if (_fieldManual == null) _fieldManual = FindFirstObjectByType<FieldManualUI>(FindObjectsInactive.Include);
            if (_fieldManual != null && _fieldManual.IsOpen)
            {
                _fieldManual.Close();
                return true;
            }
            return false;
        }

        private static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        // ── return to main menu ──────────────────────────────────────────────────────────

        private void ShowConfirm()
        {
            if (_confirm == null) BuildConfirm();
            _confirm.Root.SetActive(true);

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_noButton.gameObject);
        }

        private void HideConfirm()
        {
            if (_confirm == null || !_confirm.Root.activeSelf) return;

            _confirm.Root.SetActive(false);
            if (IsOpen && EventSystem.current != null && _resumeButton != null)
                EventSystem.current.SetSelectedGameObject(_resumeButton.gameObject);
        }

        private void ConfirmReturn()
        {
            _confirm.Root.SetActive(false);
            IsOpen = false;
            if (_root != null) _root.SetActive(false);

            Time.timeScale = 1f; // never carry a frozen clock into the menu
            FreezeEverythingElse(false);

            GameFlowManager.Instance.AbandonNight();
        }

        // ── freeze ───────────────────────────────────────────────────────────────────────

        // timeScale alone leaves audio, videos and DOTween running.
        private void FreezeEverythingElse(bool freeze)
        {
            if (freeze)
            {
                _previousAudioPause = AudioListener.pause;
                _previousDoTweenScale = DOTween.timeScale;
                AudioListener.pause = true;
                DOTween.timeScale = 0f;

                _pausedVideos.Clear();
                foreach (var video in FindObjectsByType<VideoPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!video.isPlaying) continue;
                    video.Pause();
                    _pausedVideos.Add(video);
                }
                return;
            }

            AudioListener.pause = _previousAudioPause;
            DOTween.timeScale = _previousDoTweenScale;
            foreach (var video in _pausedVideos)
            {
                if (video != null) video.Play();
            }
            _pausedVideos.Clear();
        }

        // ── UI build ─────────────────────────────────────────────────────────────────────

        private XPTheme BuildTheme()
        {
            var theme = Instantiate(XPTheme.Load());
            theme.titlebarTop = style.titlebarTop;
            theme.titlebarBottom = style.titlebarBottom;
            theme.titlebarHeight = titlebarHeight;
            theme.closeTop = Color.Lerp(style.closeButton, Color.white, 0.25f);
            theme.closeBottom = style.closeButton;
            theme.border = style.windowBorder;
            theme.body = style.windowBody;
            theme.bodyText = style.label;
            theme.dim = new Color(0f, 0f, 0f, dimAlpha);
            return theme;
        }

        private void BuildUi()
        {
            _theme = BuildTheme();
            var font = _theme.ResolvedFont;

            _window = XPWindowBuilder.Build("PauseMenuCanvas", windowTitle, new Vector2(windowWidth, windowHeight), sortingOrder, _theme, Close, cctvStyle);
            _root = _window.Root;
            DecorateWindow(_window, font, captionButtons: true);

            float statusHeight = showStatusBar ? statusBarHeight : 0f;

            var body = new GameObject("Body", typeof(RectTransform), typeof(VerticalLayoutGroup));
            body.transform.SetParent(_window.Panel, false);
            var bodyRect = body.GetComponent<RectTransform>();
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = new Vector2(0f, statusHeight);
            bodyRect.offsetMax = new Vector2(0f, -titlebarHeight);

            var layout = body.GetComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(bodyPadding);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = sectionSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = XPControls.Text(body.transform, "HeaderText", font, headerText, style.headerSize, style.value, true, TextAlignmentOptions.MidlineLeft);
            XPControls.SetPreferred(header.gameObject, height: style.headerSize + 6f);

            var group = XPControls.CreateGroupBox(body.transform, style, font, audioLegend, groupPadding, audioRows.rowSpacing);
            _audioPanel = AudioSettingsPanel.Build(group, audioRows, style, font);

            BuildButtonRow(body.transform, font);

            if (showStatusBar) BuildStatusBar(font);

            _root.SetActive(false);
        }

        private void BuildButtonRow(Transform parent, TMP_FontAsset font)
        {
            var row = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            XPControls.SetPreferred(row, height: buttonHeight + buttonRowExtraTop);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, Mathf.RoundToInt(buttonRowExtraTop), 0);
            layout.spacing = buttonSpacing;
            layout.childAlignment = TextAnchor.LowerRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            XPControls.CreateButton(row.transform, style, font, returnLabel, returnButtonWidth, buttonHeight, false, ShowConfirm);
            _resumeButton = XPControls.CreateButton(row.transform, style, font, resumeLabel, resumeButtonWidth, buttonHeight, true, Close);
        }

        private void BuildStatusBar(TMP_FontAsset font)
        {
            var bar = XPControls.Img(_window.Panel, "StatusBar", style.windowBody, rect =>
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(0f, statusBarHeight);
                rect.anchoredPosition = Vector2.zero;
            });

            XPControls.Img(bar.transform, "TopBorder", style.groupBorder, rect =>
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, 1f);
            });

            var hint = XPControls.Text(bar.transform, "HintText", font, statusHint, style.smallSize, style.label, false, TextAlignmentOptions.MidlineLeft);
            XPControls.Stretch(hint.rectTransform);
            hint.rectTransform.offsetMin = new Vector2(bodyPadding, 0f);

            var badge = XPControls.Img(bar.transform, "Badge", style.accent, rect =>
            {
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(-bodyPadding, 0f);
            });
            var badgeText = XPControls.Text(badge.transform, "BadgeText", font, pausedBadge, style.smallSize, style.badgeText, true, TextAlignmentOptions.Center);
            XPControls.Stretch(badgeText.rectTransform);
            badgeText.ForceMeshUpdate();
            badge.rectTransform.sizeDelta = new Vector2(badgeText.preferredWidth + 14f, statusBarHeight - 6f);
        }

        private void BuildConfirm()
        {
            var font = _theme.ResolvedFont;

            _confirm = XPWindowBuilder.Build("PauseConfirmCanvas", confirmTitle, confirmSize, sortingOrder + 1, _theme, HideConfirm, cctvStyle);
            DecorateWindow(_confirm, font, captionButtons: false);

            var body = new GameObject("Body", typeof(RectTransform), typeof(VerticalLayoutGroup));
            body.transform.SetParent(_confirm.Panel, false);
            var bodyRect = body.GetComponent<RectTransform>();
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = new Vector2(0f, -titlebarHeight);

            var layout = body.GetComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(bodyPadding);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = sectionSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var message = XPControls.Text(body.transform, "Message", font, confirmMessage, style.labelSize, style.label, false, TextAlignmentOptions.TopLeft);
            message.enableWordWrapping = true;
            XPControls.SetPreferred(message.gameObject, height: confirmSize.y - titlebarHeight - buttonHeight - bodyPadding * 2f - sectionSpacing);

            var row = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(body.transform, false);
            XPControls.SetPreferred(row, height: buttonHeight);
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = buttonSpacing;
            rowLayout.childAlignment = TextAnchor.LowerRight;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;

            XPControls.CreateButton(row.transform, style, font, yesLabel, confirmButtonWidth, buttonHeight, false, ConfirmReturn);
            _noButton = XPControls.CreateButton(row.transform, style, font, noLabel, confirmButtonWidth, buttonHeight, true, HideConfirm);

            _confirm.Root.SetActive(false);
        }

        // Spec-sized titlebar: 16px icon, 12px title, 21px caption buttons (Min/Max are decorative).
        private void DecorateWindow(XPWindowBuilder.Window window, TMP_FontAsset font, bool captionButtons)
        {
            var border = window.Root.transform.Find("Border");
            if (border != null) border.localScale = Vector3.one * uiScale;

            var titlebar = (RectTransform)window.Panel.Find("Titlebar");
            var title = titlebar.Find("Title").GetComponent<TextMeshProUGUI>();
            title.fontSize = titleSize;

            int captionCount = captionButtons ? 3 : 1;
            title.rectTransform.offsetMin = new Vector2(titleIconSize + 12f, 0f);
            title.rectTransform.offsetMax = new Vector2(-(captionCount * (captionButtonSize + 2f) + 4f), 0f);

            var icon = XPControls.Img(titlebar, "Icon", titleIcon != null ? Color.white : new Color(1f, 1f, 1f, 0.9f), rect =>
            {
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(titleIconSize, titleIconSize);
                rect.anchoredPosition = new Vector2(6f, 0f);
            });
            if (titleIcon != null) icon.sprite = titleIcon;
            else XPControls.Img(icon.transform, "Screen", style.accent, rect => XPControls.Stretch(rect, 2f));

            var close = window.CloseButton.GetComponent<RectTransform>();
            close.sizeDelta = new Vector2(captionButtonSize, captionButtonSize);
            close.anchoredPosition = new Vector2(-4f, 0f);
            var closeLabel = close.GetComponentInChildren<TextMeshProUGUI>();
            if (closeLabel != null) closeLabel.fontSize = titleSize;

            if (!captionButtons) return;

            float step = captionButtonSize + 2f;
            BuildCaptionButton(titlebar, "Maximize", -4f - step, maximize: true);
            BuildCaptionButton(titlebar, "Minimize", -4f - 2f * step, maximize: false);
        }

        private void BuildCaptionButton(RectTransform titlebar, string name, float x, bool maximize)
        {
            Color face = Color.Lerp(style.titlebarTop, Color.white, 0.3f);
            var button = XPControls.Img(titlebar, name, new Color(1f, 1f, 1f, 0.8f), rect =>
            {
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.sizeDelta = new Vector2(captionButtonSize, captionButtonSize);
                rect.anchoredPosition = new Vector2(x, 0f);
            });
            XPControls.Img(button.transform, "Face", face, rect => XPControls.Stretch(rect, 1f));

            if (maximize)
            {
                var frame = XPControls.Img(button.transform, "Frame", Color.white, rect =>
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(9f, 9f);
                });
                XPControls.Img(frame.transform, "Hole", face, rect =>
                {
                    XPControls.Stretch(rect, 1f);
                    rect.offsetMax = new Vector2(-1f, -3f);
                });
            }
            else
            {
                XPControls.Img(button.transform, "Bar", Color.white, rect =>
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(8f, 3f);
                    rect.anchoredPosition = new Vector2(0f, -3f);
                });
            }
        }
    }
}
