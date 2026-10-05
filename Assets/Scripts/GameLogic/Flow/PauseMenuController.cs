using System.Collections.Generic;
using Audio;
using DG.Tweening;
using GameLogic.Story;
using Report;
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
    /// Escape opens "System Menu.exe" (the PauseMenu prefab, placed in the gameplay Canvas): Master/Music/SFX
    /// trackbars, Resume and Return to main menu. Opening freezes Time.timeScale (and audio, DOTween, videos);
    /// closing restores whatever timeScale was before. Escape first closes an open Field Manual / Incident Report,
    /// and never opens during cutscenes or while a night is ending.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Behaviour")]
        [Tooltip("ESC closes an open Incident Report / Field Manual first; the next ESC opens the menu.")]
        [SerializeField] private bool escClosesOpenWindowsFirst = true;
        [Tooltip("The menu cannot open while the game flow is in one of these states. A night that is already ending is always blocked.")]
        [SerializeField] private GameFlowState[] blockedStates = { GameFlowState.DayEndEvent, GameFlowState.Ending };

        [Header("Pause window (children of this prefab)")]
        [Tooltip("Dim overlay + the window. Hidden until paused.")]
        [SerializeField] private GameObject windowRoot;
        [SerializeField] private AudioSettingsPanel audioPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button returnButton;
        [Tooltip("The titlebar X - behaves exactly like Resume.")]
        [SerializeField] private Button closeButton;

        [Header("Return-to-menu confirm dialog")]
        [SerializeField] private GameObject confirmRoot;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;
        [Tooltip("The dialog's titlebar X - behaves exactly like No.")]
        [SerializeField] private Button confirmCloseButton;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        public bool IsOpen { get; private set; }

        private FieldManualUI _fieldManual;
        private float _previousTimeScale = 1f;
        private float _previousDoTweenScale = 1f;
        private bool _previousAudioPause;
        private readonly List<VideoPlayer> _pausedVideos = new List<VideoPlayer>();

        void Awake()
        {
            if (resumeButton != null) resumeButton.onClick.AddListener(Close);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (returnButton != null) returnButton.onClick.AddListener(ShowConfirm);
            if (yesButton != null) yesButton.onClick.AddListener(ConfirmReturn);
            if (noButton != null) noButton.onClick.AddListener(HideConfirm);
            if (confirmCloseButton != null) confirmCloseButton.onClick.AddListener(HideConfirm);

            if (windowRoot != null) windowRoot.SetActive(false);
            if (confirmRoot != null) confirmRoot.SetActive(false);
        }

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
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen || windowRoot == null || !CanPause()) return;

            IsOpen = true;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            FreezeEverythingElse(true);

            transform.SetAsLastSibling(); // draw above every other window in the Canvas
            windowRoot.SetActive(true);   // the audio panel refreshes itself on enable

            if (EventSystem.current != null && resumeButton != null)
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);

            if (showDebugInfo) Debug.Log("PauseMenuController: paused.", this);
        }

        public void Close()
        {
            if (!IsOpen) return;

            HideConfirm();
            IsOpen = false;
            Time.timeScale = _previousTimeScale;
            FreezeEverythingElse(false);

            windowRoot.SetActive(false);

            if (showDebugInfo) Debug.Log("PauseMenuController: resumed.", this);
        }

        private bool ConfirmOpen => confirmRoot != null && confirmRoot.activeSelf;

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
            if (confirmRoot == null) return;

            confirmRoot.SetActive(true);
            if (EventSystem.current != null && noButton != null)
                EventSystem.current.SetSelectedGameObject(noButton.gameObject);
        }

        private void HideConfirm()
        {
            if (!ConfirmOpen) return;

            confirmRoot.SetActive(false);
            if (IsOpen && EventSystem.current != null && resumeButton != null)
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }

        private void ConfirmReturn()
        {
            confirmRoot.SetActive(false);
            IsOpen = false;
            windowRoot.SetActive(false);

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
    }
}
