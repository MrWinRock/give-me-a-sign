using GameLogic.Story;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameLogic
{
    /// <summary>
    /// Sprint 3, S-306. TAB opens/closes the Field Manual during the shift - the gameplay-scene
    /// entry point <see cref="FieldManualUI"/> itself doesn't know about (it just exposes
    /// Open/Close/ToggleOpen, the same as any other <see cref="MainMenu.XPWindowController"/>).
    /// Deliberately never touches Time.timeScale: unlike the pause menu, threats keep coming
    /// while you read.
    /// </summary>
    public class FieldManualController : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Auto-found in the scene if left empty.")]
        [SerializeField] private FieldManualUI fieldManual;

        [Header("Toggle")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        void Awake()
        {
            if (fieldManual == null)
                fieldManual = FindFirstObjectByType<FieldManualUI>(FindObjectsInactive.Include);

            // Unlike MainMenu windows, nothing in this scene owns a DesktopManager to call
            // InitializeHidden() on our behalf - left active in the scene, the window would sit on
            // screen fully unpopulated (Show()/OnShown() never fired) the moment gameplay starts.
            if (fieldManual != null) fieldManual.InitializeHidden();
        }

        void Update()
        {
            if (fieldManual == null) return;

            bool togglePressed;
#if ENABLE_INPUT_SYSTEM
            togglePressed = Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
#else
            togglePressed = Input.GetKeyDown(toggleKey);
#endif
            if (togglePressed) Toggle();

            // Something else claimed the screen (cutscene started, demon revealed, paused) -
            // close rather than fight it for input/visibility.
            if (fieldManual.IsOpen && IsBlocked()) fieldManual.Close();
        }

        public void Toggle()
        {
            if (fieldManual == null) return;
            if (!fieldManual.IsOpen && (!fieldManual.AllowOpenDuringShift || IsBlocked())) return;

            fieldManual.ToggleOpen();
        }

        private static bool IsBlocked()
        {
            if (Time.timeScale <= 0f) return true; // paused, or a cutscene is mid-playback
            if (CinematicPlayer.IsAnyPlaying) return true;
            if (DemonAnomaly.AnyRevealed) return true;
            // Report form deliberately NOT blocking: reading the manual while filling a report is its purpose.
            return false;
        }
    }
}
