using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// The two full-screen black overlays that bookend the desktop:
    /// </summary>
    public class ShutdownSequence : MonoBehaviour
    {
        [Header("Overlay")]
        [Tooltip("Full-screen black panel with a centered monospace label. Inactive by default.")]
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private Image overlayBackground;
        [SerializeField] private TextMeshProUGUI overlayText;
        [SerializeField] private Color overlayBackgroundColor = Color.black;

        [Header("Boot Sequence")]
        [SerializeField] private bool skipBootSequence;
        [SerializeField] private string gameSceneName = "GameManager";
        [SerializeField] private float lineInterval = 0.8f;
        [SerializeField] private Color bootTextColor = XPPalette.Hex("#4A9A4A");
        [SerializeField]
        private List<string> bootLines = new List<string>
        {
            "CONNECTING TO CCTV NETWORK",
            "AUTHENTICATING SEC-04",
            "LOADING CAMERA FEEDS",
            "SHIFT BEGINS",
        };

        [Header("Shutdown Sequence")]
        [SerializeField] private string shutdownMessage = "It is now safe to turn off your computer.";
        [SerializeField] private float shutdownHoldSeconds = 2f;
        [SerializeField] private Color shutdownTextColor = XPPalette.Hex("#E8A33D");

        private Coroutine _routine;

        public string GameSceneName => gameSceneName;

        public bool IsPlaying => _routine != null;

        void Awake()
        {
            if (overlayRoot != null)
                overlayRoot.SetActive(false);
        }

        // =======================================================================================
        // Boot
        // =======================================================================================

        [ContextMenu("Play Boot Sequence")]
        public void PlayBootSequence()
        {
            if (IsPlaying) return;

            if (skipBootSequence)
            {
                LoadGameSceneImmediately();
                return;
            }

            _routine = StartCoroutine(BootRoutine());
        }

        private void LoadGameSceneImmediately()
        {
            Debug.Log($"ShutdownSequence: boot sequence skipped, loading '{gameSceneName}' directly.", this);
            SceneManager.LoadScene(gameSceneName);
        }

        private IEnumerator BootRoutine()
        {
            ShowOverlay(bootTextColor);
            if (overlayText != null)
                overlayText.text = string.Empty;

            // Start streaming the scene in behind the text, but hold activation until the last
            // line has been on screen for its full interval.
            var load = SceneManager.LoadSceneAsync(gameSceneName);
            if (load == null)
            {
                Debug.LogError($"ShutdownSequence: scene '{gameSceneName}' could not be loaded. " +
                               "Is it added to File > Build Settings?", this);
                HideOverlay();
                _routine = null;
                yield break;
            }

            load.allowSceneActivation = false;

            var builder = new StringBuilder();
            for (int i = 0; i < bootLines.Count; i++)
            {
                builder.AppendLine(bootLines[i]);
                if (overlayText != null)
                    overlayText.text = builder.ToString();

                yield return new WaitForSecondsRealtime(lineInterval);
            }

            load.allowSceneActivation = true;
            _routine = null;
        }

        // =======================================================================================
        // Shutdown
        // =======================================================================================

        [ContextMenu("Play Shutdown Sequence")]
        public void PlayShutdownSequence()
        {
            if (IsPlaying) return;
            _routine = StartCoroutine(ShutdownRoutine());
        }

        private IEnumerator ShutdownRoutine()
        {
            ShowOverlay(shutdownTextColor);
            if (overlayText != null)
                overlayText.text = shutdownMessage;

            yield return new WaitForSecondsRealtime(shutdownHoldSeconds);

            _routine = null;

#if UNITY_EDITOR
            Debug.Log("ShutdownSequence: Application.Quit() suppressed in the Editor - " +
                      "the build would have exited here.", this);
#else
            Application.Quit();
#endif
        }

        // =======================================================================================

        private void ShowOverlay(Color textColor)
        {
            if (overlayRoot != null)
            {
                overlayRoot.SetActive(true);
                overlayRoot.transform.SetAsLastSibling(); // above the desktop, taskbar and windows
            }

            if (overlayBackground != null)
                overlayBackground.color = overlayBackgroundColor;

            if (overlayText != null)
                overlayText.color = textColor;
        }

        private void HideOverlay()
        {
            if (overlayRoot != null)
                overlayRoot.SetActive(false);
        }
    }
}
