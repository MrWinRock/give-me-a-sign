using System;
using System.Collections;
using System.Collections.Generic;
using GameLogic;
using GameLogic.Data;
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

        [Header("Matching Settings")]
        [Tooltip("If true, the selected LOCATION must also match the anomaly's actual room for the report to succeed. If false, only the spoken anomaly type is checked.")]
        [SerializeField] private bool requireCorrectLocation;
        [Range(0.5f, 1f)] [SerializeField] private float matchThreshold = 0.65f;

        [Header("Result Feedback")]
        [Tooltip("How long the SENT/ERROR badge is shown before the window closes and the anomaly is resolved/escalated.")]
        [SerializeField] private float resultDisplayDuration = 0.8f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        // Starts at #0001 and only advances once the player actually submits a report (see
        // SubmitReport) - opening/cancelling a report never consumes a case number.
        private static int _nextCaseNumber = 1;

        /// <summary>The case number that will be assigned the next time a report opens - a
        /// read-only peek for haunt loops like ImpostorCaseHaunt that need to fake a case number
        /// ahead of the real one without actually consuming it.</summary>
        public static int NextCaseNumber => _nextCaseNumber;

        private Anomaly _currentAnomaly;
        private string _recognizedKeyword = "";
        private int _activeAlertCount;
        private GlitchStateSource _glitchStateSource;

        public bool IsReportOpen { get; private set; }

        /// <summary>Reports actually submitted this night (cancelling doesn't count).</summary>
        public int ReportsFiled { get; private set; }

        /// <summary>Of those, how many came back ERROR. Recorded on the night's result.</summary>
        public int ReportsFailed { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
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

            // S-106 fix: this used to be found nowhere in the codebase - GlitchStateSource's
            // ConsecutiveFailures (and the failure-streak escalation/scripted beats keyed off it,
            // read by GlitchDirector via the wired stateSourceBehaviour) was permanently stuck at
            // 0 because nothing ever reported a result back to it. Must target GlitchStateSource
            // directly, not GlitchDirector - GlitchDirector.ReadConsecutiveFailures() ignores its
            // own pushed value and reads _stateSource.ConsecutiveFailures whenever a state source
            // is assigned (which it is, in this scene).
            _glitchStateSource = FindObjectOfType<GlitchStateSource>();

            if (reportUI == null)
            {
                Debug.LogError("IncidentReportManager: No IncidentReportUI assigned!");
                return;
            }

            // Room list comes from the RoomAnchors in the scene, so the dropdown always offers
            // exactly the rooms the camera can actually reach.
            if (RoomRegistry.Count == 0)
            {
                Debug.LogError(
                    "IncidentReportManager: no RoomAnchors in the scene - the LOCATION dropdown will be empty. " +
                    "Run 'Tools/Give Me A Sign/Setup/1. Create Rooms And Anchors'.", this);
            }

            reportUI.Initialize(this, RoomRegistry.DisplayNames());
            reportUI.Hide();
        }

        void Update()
        {
            bool spacePressed;
#if ENABLE_INPUT_SYSTEM
            spacePressed = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            spacePressed = Input.GetKeyDown(KeyCode.Space);
#endif
            if (!spacePressed) return;

            if (showDebugInfo)
                Debug.Log($"[IncidentReportManager] Spacebar detected. ActiveAnomalies={Anomaly.ActiveAnomalies.Count}, IsReportOpen={IsReportOpen}");

            if (IsReportOpen)
            {
                CloseReportViaSpacebar();
            }
            else
            {
                TryOpenReportViaSpacebar();
            }
        }

        /// <summary>
        /// Pressing Spacebar again while the report window is open closes it (same as Cancel),
        /// unless the SENT/ERROR result flash is currently showing - that shouldn't be interrupted.
        /// </summary>
        private void CloseReportViaSpacebar()
        {
            if (reportUI != null && reportUI.IsLocked) return;
            CancelReport();
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

            // Not incremented here - the same case number is shown again if this report is
            // cancelled and reopened. It only advances once the player actually submits.
            int caseNumber = _nextCaseNumber;
            reportUI.Show(caseNumber, RoomRegistry.DisplayNames());
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
            bool success = _currentAnomaly != null
                && MatchesSpokenType(_currentAnomaly)
                && MatchesLocation(_currentAnomaly, selectedRoom);

            if (showDebugInfo)
            {
                string outcome = success ? "SUCCESS" : "FAILED";
                string target = _currentAnomaly != null ? $"'{_currentAnomaly.name}'" : "(no anomaly attached)";
                Debug.Log($"IncidentReportManager: Report {outcome} for {target}. Spoken: '{_recognizedKeyword}', Expected: [{ExpectedKeywordsLabel(_currentAnomaly)}].");
            }

            // The case number only advances once a report has actually been filed - cancelling
            // never consumes one.
            _nextCaseNumber++;
            ReportsFiled++;
            if (!success) ReportsFailed++;

            // S-106 fix: bumps ConsecutiveFailures on GlitchStateSource so the failure-streak
            // escalation and OnConsecutiveFailures scripted beats in GlitchDirector actually fire.
            _glitchStateSource?.RegisterReportResult(success);

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

        /// <summary>
        /// An anomaly kind can accept several spoken words - the proper name plus whatever the
        /// speech-to-text is likely to hear instead - so any one of its keywords matching is a
        /// correct report.
        /// </summary>
        private bool MatchesSpokenType(Anomaly anomaly)
        {
            var definition = anomaly.Definition;

            if (definition == null)
            {
                // Every shipped prefab carries a Definition now (see 'Tools/Give Me A Sign/Validate
                // Data'). One with none assigned is an authoring mistake, not a state to recover from.
                Debug.LogError($"Anomaly '{anomaly.name}' has no AnomalyDefinition assigned - it can never be reported correctly.", anomaly);
                return false;
            }

            var keywords = definition.correctKeywords;
            if (keywords == null || keywords.Length == 0)
            {
                Debug.LogWarning($"AnomalyDefinition '{definition.name}' has no correctKeywords - it can never be reported correctly.", definition);
                return false;
            }

            foreach (var keyword in keywords)
            {
                if (IsKeywordMatch(_recognizedKeyword, keyword)) return true;
            }
            return false;
        }

        /// <summary>
        /// The room is compared against the one assigned when the anomaly spawned, not a string
        /// baked into its prefab. If nothing assigned a room we can't verify the answer, so the
        /// check passes rather than failing the player over missing authoring data.
        /// </summary>
        private bool MatchesLocation(Anomaly anomaly, string selectedRoom)
        {
            if (!requireCorrectLocation) return true;

            var assigned = anomaly.AssignedRoom;
            if (assigned == null)
            {
                Debug.LogWarning($"Anomaly '{anomaly.name}' has no room assigned, so the LOCATION answer cannot be checked. Treating it as correct.", anomaly);
                return true;
            }

            return string.Equals(selectedRoom, assigned.Label, StringComparison.OrdinalIgnoreCase);
        }

        private static string ExpectedKeywordsLabel(Anomaly anomaly)
        {
            if (anomaly == null) return "no anomaly";

            var definition = anomaly.Definition;
            if (definition?.correctKeywords != null && definition.correctKeywords.Length > 0)
                return string.Join(" | ", definition.correctKeywords);

            return "(no definition)";
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
