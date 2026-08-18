using System.Collections.Generic;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// Every route out of the desktop. Desktop icons and Start menu items both resolve to one
    /// of these, so there is exactly one switch (<see cref="DesktopManager.Execute"/>) that maps
    /// "what the player clicked" to "what the game does".
    /// </summary>
    public enum DesktopAction
    {
        None,
        StartShift,
        OpenMyReports,
        OpenControlPanel,
        OpenHelp,
        OpenNotepad,
        OpenRecycleBin,
        LogOff,
        TurnOffComputer,

        // Appended, never inserted: these values are serialized as ints in MainMenu.unity, so
        // inserting anywhere above would silently repoint every existing menu item.
        NewGame
    }

    /// <summary>Named audio cues; each maps to a serialized AudioClip on the DesktopManager.</summary>
    public enum MenuCue
    {
        StartMenuOpen,
        WindowOpen,
        WindowClose,
        IconClick,
        Error
    }

    /// <summary>
    /// Owner of the fake Windows XP desktop that stands in for the main menu.
    /// </summary>
    public class DesktopManager : MonoBehaviour
    {
        [Header("Desktop Background")]
        [SerializeField] private Image desktopBackground;
        [SerializeField] private UIGradient desktopGradient;
        [SerializeField] private Color desktopTopColor = XPPalette.Hex("#1A2838");
        [SerializeField] private Color desktopMidColor = XPPalette.Hex("#24384C");
        [SerializeField] private Color desktopBottomColor = XPPalette.Hex("#1E3326");
        [Range(0f, 1f)] [SerializeField] private float desktopMidPosition = 0.45f;
        [Tooltip("Optional wallpaper (e.g. a darkened photo of the house). When assigned it replaces the gradient.")]
        [SerializeField] private Sprite wallpaper;
        [Tooltip("Tint applied on top of the wallpaper - darken the house photo from here without re-exporting it.")]
        [SerializeField] private Color wallpaperTint = Color.white;
        [Tooltip("Full-screen click catcher behind the icons. Clicking it deselects icons and closes the Start menu.")]
        [SerializeField] private Button desktopClickCatcher;

        [Header("Icons")]
        [SerializeField] private List<DesktopIcon> icons = new List<DesktopIcon>();
        [Tooltip("Two clicks on the SAME icon within this many seconds count as a double-click.")]
        [SerializeField] private float doubleClickThreshold = 0.4f;

        [Header("Taskbar / Start Menu")]
        [SerializeField] private StartMenuController startMenu;

        [Header("Window Prefabs")]
        [Tooltip("Parent every spawned window goes under. Keep it BELOW the taskbar in sibling order so windows never cover the taskbar.")]
        [SerializeField] private RectTransform windowLayer;
        [SerializeField] private XPWindowController turnOffWindowPrefab;
        [SerializeField] private XPWindowController controlPanelWindowPrefab;
        [SerializeField] private XPWindowController myReportsWindowPrefab;
        [SerializeField] private XPWindowController notepadWindowPrefab;
        [SerializeField] private XPWindowController recycleBinWindowPrefab;
        [SerializeField] private XPWindowController logOffWindowPrefab;
        [SerializeField] private XPWindowController helpWindowPrefab;

        [Header("Sequences")]
        [SerializeField] private ShutdownSequence shutdownSequence;

        [Header("Audio (all optional - unassigned clips are skipped)")]
        [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private AudioClip startMenuOpenClip;
        [SerializeField] private AudioClip windowOpenClip;
        [SerializeField] private AudioClip windowCloseClip;
        [SerializeField] private AudioClip iconClickClip;
        [SerializeField] private AudioClip errorClip;

        private readonly Dictionary<XPWindowController, XPWindowController> _windowPool =
            new Dictionary<XPWindowController, XPWindowController>();

        private XPWindowController _openWindow;
        private DesktopIcon _selectedIcon;
        private float _lastIconClickTime = float.NegativeInfinity;

        public XPWindowController OpenWindow => _openWindow;

        void Awake()
        {
            ApplyBackground();

            // Saved Control Panel settings take effect even if the player never opens it.
            ControlPanelWindow.ApplySavedSettings();

            for (int i = 0; i < icons.Count; i++)
                icons[i]?.Bind(this);

            startMenu?.Bind(this);

            if (desktopClickCatcher != null)
            {
                desktopClickCatcher.onClick.RemoveListener(OnDesktopClicked);
                desktopClickCatcher.onClick.AddListener(OnDesktopClicked);
            }
        }

        // =======================================================================================
        // Background
        // =======================================================================================

        public void ApplyBackground()
        {
            if (desktopBackground != null)
            {
                desktopBackground.sprite = wallpaper;
                desktopBackground.type = Image.Type.Simple;
                desktopBackground.preserveAspect = false;
                desktopBackground.color = wallpaper != null ? wallpaperTint : Color.white;
            }

            if (desktopGradient != null)
            {
                // A wallpaper replaces the gradient rather than tinting it.
                desktopGradient.enabled = wallpaper == null;
                desktopGradient.UseMidColor = true;
                desktopGradient.MidColor = desktopMidColor;
                desktopGradient.MidPosition = desktopMidPosition;
                desktopGradient.SetColors(desktopTopColor, desktopBottomColor);
            }
        }

        // =======================================================================================
        // Icons
        // =======================================================================================

        public void OnIconClicked(DesktopIcon icon)
        {
            if (icon == null) return;

            startMenu?.Close();
            PlayCue(MenuCue.IconClick);

            bool isDoubleClick = icon == _selectedIcon &&
                                 Time.unscaledTime - _lastIconClickTime <= doubleClickThreshold;

            // Reset the stamp on activation so a third click doesn't immediately re-activate.
            _lastIconClickTime = isDoubleClick ? float.NegativeInfinity : Time.unscaledTime;

            SelectIcon(icon);

            if (isDoubleClick)
                Execute(icon.Action);
        }

        public void SelectIcon(DesktopIcon icon)
        {
            if (_selectedIcon == icon) return;

            for (int i = 0; i < icons.Count; i++)
                icons[i]?.SetSelected(icons[i] == icon);

            _selectedIcon = icon;
        }

        public void DeselectAllIcons()
        {
            for (int i = 0; i < icons.Count; i++)
                icons[i]?.SetSelected(false);

            _selectedIcon = null;
            _lastIconClickTime = float.NegativeInfinity;
        }

        private void OnDesktopClicked()
        {
            DeselectAllIcons();
            startMenu?.Close();
        }

        // =======================================================================================
        // Action routing
        // =======================================================================================

        public void Execute(DesktopAction action)
        {
            switch (action)
            {
                case DesktopAction.StartShift: StartShift(); break;
                case DesktopAction.NewGame: NewGame(); break;
                case DesktopAction.OpenMyReports: OpenWindowPrefab(myReportsWindowPrefab); break;
                case DesktopAction.OpenControlPanel: OpenWindowPrefab(controlPanelWindowPrefab); break;
                case DesktopAction.OpenHelp: OpenWindowPrefab(helpWindowPrefab); break;
                case DesktopAction.OpenNotepad: OpenWindowPrefab(notepadWindowPrefab); break;
                case DesktopAction.OpenRecycleBin: OpenWindowPrefab(recycleBinWindowPrefab); break;
                case DesktopAction.LogOff: OpenWindowPrefab(logOffWindowPrefab); break;
                case DesktopAction.TurnOffComputer: OpenWindowPrefab(turnOffWindowPrefab); break;
                case DesktopAction.None: break;
            }
        }

        /// <summary>
        /// Continues the saved run - the boot sequence loads the gameplay scene, which generates
        /// whatever day the save is on. With no save file that is day 1, so this doubles as
        /// "start playing" on a fresh install.
        /// </summary>
        public void StartShift()
        {
            CloseOpenWindow();
            startMenu?.Close();

            if (shutdownSequence != null)
                shutdownSequence.PlayBootSequence();
            else
                Debug.LogError("DesktopManager: no ShutdownSequence assigned - cannot start the shift.", this);
        }

        /// <summary>
        /// Wipes the save and starts over at day 1. Overwriting is the whole point, so this must
        /// only be reachable behind a confirmation in the UI.
        /// </summary>
        public void NewGame()
        {
            GameLogic.Flow.GameFlowManager.ResetAllSaveData();
            StartShift();
        }

        public void QuitGame()
        {
            CloseOpenWindow();
            startMenu?.Close();

            if (shutdownSequence != null)
                shutdownSequence.PlayShutdownSequence();
            else
                Debug.LogError("DesktopManager: no ShutdownSequence assigned - cannot shut down.", this);
        }

        public void RestartMenu()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // =======================================================================================
        // Windows
        // =======================================================================================

        public XPWindowController OpenWindowPrefab(XPWindowController prefab)
        {
            if (prefab == null)
            {
                Debug.LogWarning("DesktopManager: window prefab not assigned.", this);
                PlayCue(MenuCue.Error);
                return null;
            }

            if (!_windowPool.TryGetValue(prefab, out var instance) || instance == null)
            {
                if (windowLayer == null)
                {
                    Debug.LogError("DesktopManager: windowLayer is not assigned - windows have nowhere to spawn.", this);
                    PlayCue(MenuCue.Error);
                    return null;
                }

                instance = Instantiate(prefab, windowLayer);
                instance.name = prefab.name;
                instance.Bind(this);
                instance.InitializeHidden();
                _windowPool[prefab] = instance;
            }

            instance.Show();   // Show() -> NotifyWindowShown() closes whatever was open
            PlayCue(MenuCue.WindowOpen);
            return instance;
        }

        public void CloseOpenWindow()
        {
            _openWindow?.Hide();
        }

        public void NotifyWindowShown(XPWindowController window)
        {
            if (window == null) return;

            if (_openWindow != null && _openWindow != window)
            {
                var previous = _openWindow;
                _openWindow = null;           // cleared first so the Hide() callback can't clobber us
                previous.Hide();
            }

            _openWindow = window;
            BringToFront(window);
        }

        public void NotifyWindowClosed(XPWindowController window)
        {
            if (_openWindow == window)
                _openWindow = null;

            PlayCue(MenuCue.WindowClose);
        }

        public void BringToFront(XPWindowController window)
        {
            if (window != null)
                window.transform.SetAsLastSibling();
        }

        // =======================================================================================
        // Audio
        // =======================================================================================

        public void PlayCue(MenuCue cue)
        {
            if (uiAudioSource == null) return;

            AudioClip clip = null;
            switch (cue)
            {
                case MenuCue.StartMenuOpen: clip = startMenuOpenClip; break;
                case MenuCue.WindowOpen: clip = windowOpenClip; break;
                case MenuCue.WindowClose: clip = windowCloseClip; break;
                case MenuCue.IconClick: clip = iconClickClip; break;
                case MenuCue.Error: clip = errorClip; break;
            }

            if (clip != null)
                uiAudioSource.PlayOneShot(clip);
        }

        // =======================================================================================
        // Debug
        // =======================================================================================

        [ContextMenu("Debug/Open Turn Off Computer")]
        private void DebugOpenTurnOff() => OpenWindowPrefab(turnOffWindowPrefab);

        [ContextMenu("Debug/Open Control Panel")]
        private void DebugOpenControlPanel() => OpenWindowPrefab(controlPanelWindowPrefab);

        [ContextMenu("Debug/Open My Reports")]
        private void DebugOpenMyReports() => OpenWindowPrefab(myReportsWindowPrefab);

        [ContextMenu("Debug/Open Notepad")]
        private void DebugOpenNotepad() => OpenWindowPrefab(notepadWindowPrefab);

        [ContextMenu("Debug/Open Recycle Bin")]
        private void DebugOpenRecycleBin() => OpenWindowPrefab(recycleBinWindowPrefab);

        [ContextMenu("Debug/Open Log Off")]
        private void DebugOpenLogOff() => OpenWindowPrefab(logOffWindowPrefab);

        [ContextMenu("Debug/Open Help and Support")]
        private void DebugOpenHelp() => OpenWindowPrefab(helpWindowPrefab);

        [ContextMenu("Debug/Start Shift")]
        private void DebugStartShift() => StartShift();

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying)
                ApplyBackground();
        }
#endif
    }
}
