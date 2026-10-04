using System;
using System.Collections;
using System.Collections.Generic;
using GameLogic;
using GameLogic.Data;
using Whisper;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Report
{
    /// <summary>
    /// Drives the Incident Report gameplay loop: opens the report window, matches what the player
    /// said they saw (and optionally the room) against every active anomaly, then resolves or
    /// escalates based on the result.
    /// </summary>
    public class IncidentReportManager : MonoBehaviour
    {
        public static IncidentReportManager Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private IncidentReportUI reportUI;

        [Header("System References")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private bool autoFindReferences = true;

        [Tooltip("Off = Spacebar no longer opens the report form; reports are called in over the walkie-talkie (hold V).")]
        [SerializeField] private bool reportFormEnabled;

        [Header("Matching Settings")]
        [Tooltip("If true, the selected LOCATION must also match the anomaly's actual room for the report to succeed. If false, only what the player said they saw is checked.")]
        [SerializeField] private bool requireCorrectLocation;

        [Header("Result Feedback")]
        [Tooltip("How long the SENT/ERROR badge is shown before the window closes and the anomaly is resolved/escalated.")]
        [SerializeField] private float resultDisplayDuration = 0.8f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        // Starts at #0001 and only advances once the player actually submits a report (see
        // SubmitReport) - opening/cancelling a report never consumes a case number.
        private static int _nextCaseNumber = 1;

        public static int NextCaseNumber => _nextCaseNumber;

        private Anomaly _currentAnomaly;
        private string _recognizedKeyword = "";
        private int _activeAlertCount;
        private GlitchStateSource _glitchStateSource;

        public bool IsReportOpen { get; private set; }

        public int ReportsFiled { get; private set; }

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

            // Must be GlitchStateSource, not GlitchDirector: the director reads the state source's
            // ConsecutiveFailures instead of any value pushed into it.
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

        public enum RadioReportOutcome { NotAReport, Confirmed, Negative, TooLoud, TooQuiet, NeedRoom, NeedWhat }

        // Walkie-talkie path (hold V): no form. Same observation matching and bookkeeping as SubmitReport.
        // A volume mismatch is "static on the line" - the report is not filed and nothing is penalised.
        public RadioReportOutcome FileRadioReport(string spoken, VoiceLevel level = VoiceLevel.Normal)
        {
            if (IsReportOpen || string.IsNullOrWhiteSpace(spoken)) return RadioReportOutcome.NotAReport;

            var vocabulary = ObservationVocabulary.Load();
            string room = FindSpokenRoom(spoken);

            // A report is "what + where" ("shadow in bedroom"); half of one is bounced back, not filed.
            if (!MentionsAnyObservation(vocabulary, spoken))
                return room != null ? RadioReportOutcome.NeedWhat : RadioReportOutcome.NotAReport;
            if (room == null) return RadioReportOutcome.NeedRoom;

            _recognizedKeyword = spoken.Trim();

            Anomaly matched = null;
            RadioReportOutcome volumeMiss = RadioReportOutcome.NotAReport;
            foreach (var anomaly in Anomaly.ActiveAnomalies)
            {
                if (anomaly == null || anomaly.IsReported || !IsReportable(anomaly, room)
                    || (room != null && !MatchesLocationStrict(anomaly, room)))
                    continue;

                var required = anomaly.Definition != null ? anomaly.Definition.voiceResponse : VoiceResponse.None;
                if (required == VoiceResponse.Silence) required = VoiceResponse.Whisper; // stealth: must be whispered

                if (required == VoiceResponse.Whisper && level != VoiceLevel.Whisper)
                {
                    if (volumeMiss == RadioReportOutcome.NotAReport) volumeMiss = RadioReportOutcome.TooLoud;
                    continue;
                }
                if (required == VoiceResponse.Shout && level != VoiceLevel.Shout)
                {
                    if (volumeMiss == RadioReportOutcome.NotAReport) volumeMiss = RadioReportOutcome.TooQuiet;
                    continue;
                }

                matched = anomaly;
                break;
            }

            if (matched == null && volumeMiss != RadioReportOutcome.NotAReport)
                return volumeMiss;

            bool success = matched != null;
            ReportsFiled++;
            if (!success) ReportsFailed++;
            _glitchStateSource?.RegisterReportResult(success);

            if (success)
            {
                matched.MarkReported();
                matched.ResolveByReport();
            }
            else
            {
                // A wrong call-in lets the nearest unreported threat advance.
                foreach (var anomaly in Anomaly.ActiveAnomalies)
                {
                    if (anomaly != null && anomaly.isActiveAndEnabled && !anomaly.IsReported
                        && anomaly.State != AnomalyState.Resolved)
                    {
                        anomaly.Respond();
                        break;
                    }
                }
            }

            if (showDebugInfo)
                Debug.Log($"IncidentReportManager: Radio report {(success ? "CONFIRMED" : "NEGATIVE")}. Spoken: '{spoken}', Room: '{room ?? "(none)"}'.");

            return success ? RadioReportOutcome.Confirmed : RadioReportOutcome.Negative;
        }

        private static bool MentionsAnyObservation(ObservationVocabulary vocabulary, string spoken)
        {
            foreach (ObservationType type in Enum.GetValues(typeof(ObservationType)))
            {
                if (vocabulary.Mentions(spoken, type)) return true;
            }
            return false;
        }

        // Spaces/punctuation ignored so "bed room" or "Bedroom." both hear as Bedroom.
        private static string FindSpokenRoom(string spoken)
        {
            string squashed = Squash(spoken);
            var names = RoomRegistry.DisplayNames();

            foreach (var name in names)
            {
                if (squashed.Contains(Squash(name))) return name;
            }

            // The tiny model mishears ("kitchin", "kitten"): accept a close-enough single word.
            string best = null;
            float bestScore = 0.7f;
            foreach (var word in spoken.ToLowerInvariant().Split(new[] { ' ', ',', '.', '!', '?', '-' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length < 5) continue;

                foreach (var name in names)
                {
                    string target = Squash(name);
                    if (target.Length < 5) continue;

                    float score = PhraseMatcher.Similarity(word, target);
                    if (score >= bestScore) { bestScore = score; best = name; }
                }
            }
            return best;
        }

        private static string Squash(string text)
        {
            var sb = new System.Text.StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private static bool MatchesLocationStrict(Anomaly anomaly, string room)
        {
            var assigned = anomaly.AssignedRoom;
            return assigned == null || string.Equals(room, assigned.Label, StringComparison.OrdinalIgnoreCase);
        }

        void Update()
        {
            if (!reportFormEnabled) return;

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

        private void CloseReportViaSpacebar()
        {
            if (reportUI != null && reportUI.IsLocked) return;
            CancelReport();
        }

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

        public void OpenReport(Anomaly anomaly)
        {
            if (IsReportOpen || anomaly == null || anomaly.IsReported) return;

            anomaly.MarkReported();
            OpenReportInternal(anomaly);
        }

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

        public void CancelReport()
        {
            if (!IsReportOpen) return;

            _currentAnomaly?.ClearReportedFlag();
            CloseReport();

            if (showDebugInfo)
                Debug.Log("IncidentReportManager: Report cancelled, anomaly left unresolved.");
        }

        public void SetAlert(bool active)
        {
            _activeAlertCount = Mathf.Max(0, _activeAlertCount + (active ? 1 : -1));

            if (IsReportOpen)
                reportUI.SetAlertVisual(_activeAlertCount > 0);
        }

        public void Route(string recognizedText)
        {
            if (!IsReportOpen || string.IsNullOrWhiteSpace(recognizedText)) return;

            _recognizedKeyword = recognizedText.Trim();
            reportUI.ShowRecognizedKeyword(_recognizedKeyword);
        }

        public void SubmitReport(string selectedRoom)
        {
            if (!IsReportOpen) return;

            // The report describes what the player saw, so it can match ANY active anomaly -
            // not just the one the form happened to open on.
            var matched = FindReportedAnomaly(selectedRoom);
            bool success = matched != null;

            if (matched != null && matched != _currentAnomaly)
            {
                _currentAnomaly?.ClearReportedFlag();
                matched.MarkReported();
                _currentAnomaly = matched;
            }

            if (showDebugInfo)
            {
                string outcome = success ? "SUCCESS" : "FAILED";
                string target = _currentAnomaly != null ? $"'{_currentAnomaly.name}'" : "(no anomaly attached)";
                Debug.Log($"IncidentReportManager: Report {outcome} for {target}. Spoken: '{_recognizedKeyword}', Room: '{selectedRoom}', Expected: [{ExpectedKeywordsLabel(_currentAnomaly)}].");
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

        // The anomaly the form opened on wins if it fits; otherwise the first active one that does.
        private Anomaly FindReportedAnomaly(string selectedRoom)
        {
            if (string.IsNullOrWhiteSpace(_recognizedKeyword)) return null;

            if (IsReportable(_currentAnomaly, selectedRoom)) return _currentAnomaly;

            foreach (var anomaly in Anomaly.ActiveAnomalies)
            {
                if (anomaly != _currentAnomaly && !anomaly.IsReported && IsReportable(anomaly, selectedRoom))
                    return anomaly;
            }
            return null;
        }

        private bool IsReportable(Anomaly anomaly, string selectedRoom)
        {
            if (anomaly == null || !anomaly.isActiveAndEnabled || anomaly.State == AnomalyState.Resolved) return false;
            return MatchesObservation(anomaly) && MatchesLocation(anomaly, selectedRoom);
        }

        private bool MatchesObservation(Anomaly anomaly)
        {
            var definition = anomaly.Definition;

            if (definition == null)
            {
                // Every shipped prefab carries a Definition now (see 'Tools/Give Me A Sign/Validate
                // Data'). One with none assigned is an authoring mistake, not a state to recover from.
                Debug.LogError($"Anomaly '{anomaly.name}' has no AnomalyDefinition assigned - it can never be reported correctly.", anomaly);
                return false;
            }

            var vocabulary = ObservationVocabulary.Load();
            if (vocabulary.Mentions(_recognizedKeyword, definition.observation)) return true;

            if (definition.correctKeywords == null) return false;

            foreach (var keyword in definition.correctKeywords)
            {
                if (vocabulary.PhraseHeard(_recognizedKeyword, keyword)) return true;
            }
            return false;
        }

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
            var definition = anomaly != null ? anomaly.Definition : null;
            if (definition == null) return anomaly == null ? "no anomaly" : "(no definition)";

            string label = ObservationVocabulary.Load().LabelFor(definition.observation);
            return definition.correctKeywords != null && definition.correctKeywords.Length > 0
                ? $"{label} | {string.Join(" | ", definition.correctKeywords)}"
                : label;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
