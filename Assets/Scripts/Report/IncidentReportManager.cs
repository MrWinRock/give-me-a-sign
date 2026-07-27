using System;
using System.Collections;
using System.Collections.Generic;
using GameLogic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Report
{
    /// <summary>
    /// Drives the Incident Report gameplay loop: opens the report window when an anomaly
    /// is clicked, validates the submitted location and spoken anomaly type, then resolves
    /// or escalates the anomaly based on the result.
    /// </summary>
    public class IncidentReportManager : MonoBehaviour
    {
        public static IncidentReportManager Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private IncidentReportUI reportUI;

        [Header("System References")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private bool autoFindReferences = true;

        [Header("Rooms (data-driven)")]
        [SerializeField] private List<string> roomNames = new List<string>
        {
            "Hallway", "Kitchen", "Bedroom", "Living room", "Basement", "Attic"
        };

        [Header("Matching Settings")]
        [Tooltip("If true, the selected LOCATION must also match the anomaly's actual room for the report to succeed. If false, only the spoken anomaly type is checked.")]
        [SerializeField] private bool requireCorrectLocation;
        [Range(0.5f, 1f)] [SerializeField] private float matchThreshold = 0.65f;

        [Header("Result Feedback")]
        [Tooltip("How long the SENT/ERROR badge is shown before the window closes and the anomaly is resolved/escalated.")]
        [SerializeField] private float resultDisplayDuration = 0.8f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        // Randomized once per session so case numbers don't always start at the same value; increments by 1 per report after that.
        private static int _nextCaseNumber = -1;

        private Anomaly _currentAnomaly;
        private string _recognizedKeyword = "";
        private int _activeAlertCount;

        public bool IsReportOpen { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                if (_nextCaseNumber < 0)
                    _nextCaseNumber = UnityEngine.Random.Range(1000, 9000);
            }
            else
            {
                Debug.LogWarning("Multiple IncidentReportManager instances found! Destroying duplicate.");
                Destroy(gameObject);
            }
        }

        void Start()
        {
            if (autoFindReferences && gameManager == null)
            {
                gameManager = FindObjectOfType<GameManager>();
            }

            if (reportUI == null)
            {
                Debug.LogError("IncidentReportManager: No IncidentReportUI assigned!");
                return;
            }

            reportUI.Initialize(this, roomNames);
            reportUI.Hide();
        }

        void Update()
        {
            if (IsReportOpen) return;

            bool spacePressed;
#if ENABLE_INPUT_SYSTEM
            spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            spacePressed = Input.GetKeyDown(KeyCode.Space);
#endif
            if (spacePressed)
            {
                if (showDebugInfo)
                    Debug.Log($"[IncidentReportManager] Spacebar detected. ActiveAnomalies={Anomaly.ActiveAnomalies.Count}, IsReportOpen={IsReportOpen}");
                TryOpenReportViaSpacebar();
            }
        }

        /// <summary>
        /// Spacebar opens the Incident Report window at any time - it is not gated on an anomaly
        /// being active. If an un-reported anomaly is currently spawned, the report is linked to it
        /// (existing behavior: the anomaly only disappears afterwards if the submitted report is
        /// correct, via ResolveByReport). If nothing is active, a blank report opens instead so the
        /// player can still bring up the terminal; submitting a blank report always comes back
        /// ERROR since there is nothing on record to confirm.
        /// </summary>
        private void TryOpenReportViaSpacebar()
        {
            foreach (var anomaly in Anomaly.ActiveAnomalies)
            {
                if (showDebugInfo)
                    Debug.Log($"[IncidentReportManager] Candidate anomaly '{anomaly?.name}': active={anomaly?.gameObject.activeInHierarchy}, isReported={anomaly?.IsReported}");
                if (anomaly != null && anomaly.gameObject.activeInHierarchy && !anomaly.IsReported)
                {
                    OpenReport(anomaly);
                    return;
                }
            }

            if (showDebugInfo)
                Debug.Log("[IncidentReportManager] Spacebar pressed with no active anomaly - opening blank report.");
            OpenBlankReport();
        }

        /// <summary>
        /// Call from ClickManager when the player clicks an anomaly, instead of calling Anomaly.Respond() directly.
        /// </summary>
        public void OpenReport(Anomaly anomaly)
        {
            if (IsReportOpen || anomaly == null || anomaly.IsReported) return;

            anomaly.MarkReported();
            OpenReportInternal(anomaly);
        }

        /// <summary>
        /// Opens the report window with no anomaly attached, so Spacebar can bring up the terminal
        /// even when nothing has been detected yet.
        /// </summary>
        private void OpenBlankReport()
        {
            if (IsReportOpen) return;

            OpenReportInternal(null);
        }

        private void OpenReportInternal(Anomaly anomaly)
        {
            _currentAnomaly = anomaly;
            _recognizedKeyword = "";
            IsReportOpen = true;

            if (gameManager != null)
                gameManager.inputLocked = true;

            int caseNumber = _nextCaseNumber++;
            reportUI.Show(caseNumber, roomNames);
            reportUI.SetAlertVisual(_activeAlertCount > 0);

            if (showDebugInfo)
            {
                Debug.Log(anomaly != null
                    ? $"IncidentReportManager: Opened report #{caseNumber:D4} for anomaly '{anomaly.name}'."
                    : $"IncidentReportManager: Opened blank report #{caseNumber:D4} (no anomaly attached).");
            }
        }

        /// <summary>
        /// Called by IncidentReportUI when the player clicks Cancel or the titlebar Close button.
        /// The anomaly is left unresolved (and un-flagged) so it can be clicked and reported again later.
        /// </summary>
        public void CancelReport()
        {
            if (!IsReportOpen) return;

            _currentAnomaly?.ClearReportedFlag();
            CloseReport();

            if (showDebugInfo)
                Debug.Log("IncidentReportManager: Report cancelled, anomaly left unresolved.");
        }

        /// <summary>
        /// Called by an Anomaly whenever it enters/exits its active jumpscare state, regardless of
        /// whether that anomaly is the one currently being reported. Multiple anomalies can raise
        /// this concurrently, hence the counter rather than a single bool.
        /// </summary>
        public void SetAlert(bool active)
        {
            _activeAlertCount = Mathf.Max(0, _activeAlertCount + (active ? 1 : -1));

            if (IsReportOpen)
                reportUI.SetAlertVisual(_activeAlertCount > 0);
        }

        /// <summary>
        /// Called by WhisperMicInput while the report's Push-to-Talk button is held down.
        /// </summary>
        public void Route(string recognizedText)
        {
            if (!IsReportOpen || string.IsNullOrWhiteSpace(recognizedText)) return;

            _recognizedKeyword = recognizedText.Trim();
            reportUI.ShowRecognizedKeyword(_recognizedKeyword);
        }

        /// <summary>
        /// Called by IncidentReportUI when the player presses SUBMIT REPORT. Flashes a SENT/ERROR
        /// badge for a beat via reportUI.ShowResult(), then closes the window and resolves/escalates
        /// the anomaly. Runs on a coroutine, not Time.timeScale, so background jumpscare animations
        /// keep playing throughout.
        /// </summary>
        public void SubmitReport(string selectedRoom)
        {
            if (!IsReportOpen) return;

            // A blank report (opened via Spacebar with no anomaly active) has nothing to confirm
            // against, so it always comes back as an error rather than throwing on a null anomaly.
            bool success = false;
            if (_currentAnomaly != null)
            {
                bool typeMatches = IsKeywordMatch(_recognizedKeyword, _currentAnomaly.correctAnomalyType);
                bool locationMatches = !requireCorrectLocation ||
                    string.Equals(selectedRoom, _currentAnomaly.correctLocationName, StringComparison.OrdinalIgnoreCase);
                success = typeMatches && locationMatches;
            }

            if (showDebugInfo)
            {
                string outcome = success ? "SUCCESS" : "FAILED";
                string target = _currentAnomaly != null ? $"'{_currentAnomaly.name}'" : "(no anomaly attached)";
                Debug.Log($"IncidentReportManager: Report {outcome} for {target}. Spoken: '{_recognizedKeyword}', Expected: '{_currentAnomaly?.correctAnomalyType}'.");
            }

            reportUI.ShowResult(success);
            StartCoroutine(FinishReportAfterDelay(success));
        }

        private IEnumerator FinishReportAfterDelay(bool success)
        {
            yield return new WaitForSeconds(resultDisplayDuration);

            var resolvedAnomaly = _currentAnomaly;
            CloseReport();

            if (resolvedAnomaly == null) yield break;

            if (success)
                resolvedAnomaly.ResolveByReport();
            else
                resolvedAnomaly.Respond();
        }

        private void CloseReport()
        {
            IsReportOpen = false;
            reportUI.Hide();
            _currentAnomaly = null;

            if (gameManager != null)
                gameManager.inputLocked = false;
        }

        private bool IsKeywordMatch(string spoken, string correct)
        {
            if (string.IsNullOrWhiteSpace(spoken) || string.IsNullOrWhiteSpace(correct))
                return false;

            var a = spoken.Trim().ToLowerInvariant();
            var b = correct.Trim().ToLowerInvariant();

            if (a.Contains(b) || b.Contains(a))
                return true;

            return Similarity(a, b) >= matchThreshold;
        }

        private static float Similarity(string a, string b)
        {
            if (a.Length == 0 && b.Length == 0) return 1f;
            var dist = Levenshtein(a, b);
            var maxLen = Mathf.Max(a.Length, b.Length);
            return 1f - (float)dist / maxLen;
        }

        // Optimized Levenshtein distance (uses two 1D rows)
        private static int Levenshtein(string s, string t)
        {
            int n = s.Length, m = t.Length;
            if (n == 0) return m; if (m == 0) return n;

            var prev = new int[m + 1];
            var curr = new int[m + 1];
            for (int j = 0; j <= m; j++) prev[j] = j;

            for (int i = 1; i <= n; i++)
            {
                curr[0] = i;
                var si = s[i - 1];
                for (int j = 1; j <= m; j++)
                {
                    var cost = (si == t[j - 1]) ? 0 : 1;
                    var del = prev[j] + 1;
                    var ins = curr[j - 1] + 1;
                    var sub = prev[j - 1] + cost;
                    curr[j] = Mathf.Min(del, Mathf.Min(ins, sub));
                }
                var tmp = prev; prev = curr; curr = tmp;
            }
            return prev[m];
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
