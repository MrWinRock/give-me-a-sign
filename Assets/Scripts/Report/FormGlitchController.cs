using System;
using System.Collections;
using System.Collections.Generic;
using GameLogic.SpawnAndTime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Report
{
    /// <summary>
    /// The five ways the Incident Report form can betray the player. Shared by
    /// <see cref="FormGlitchController"/> (which knows HOW to run each one) and
    /// <see cref="GlitchDirector"/> (which decides WHEN).
    /// </summary>
    public enum GlitchType
    {
        PhantomDropdown,
        FalseRecognition,
        CaseCorruption,
        StatusIntrusion,
        ClockDesync
    }

    /// <summary>
    /// "Form Betrayal" executor. Temporarily overrides the Incident Report window's *visuals*
    /// so the form itself feels untrustworthy, then always reverts.
    ///
    /// This is a pure executor - it never decides when to glitch (that is
    /// <see cref="GlitchDirector"/>'s job) and it deliberately touches nothing that
    /// IncidentReportUI / IncidentReportManager use for submission or validation:
    ///
    ///  - The dropdown's real option list and selected room are snapshotted and restored by TEXT,
    ///    so a phantom entry can never change or clear what the player picked.
    ///  - The recognized keyword the manager validates against lives in the manager's own private
    ///    field. This script only ever writes to the input field's displayed text, so a false
    ///    recognition is cosmetic by construction and cannot fail a valid submission.
    ///  - Every glitch registers a revert action up-front; CancelAllGlitches() (called when the form
    ///    closes) runs them all, so the UI can never be left permanently corrupted.
    /// </summary>
    public class FormGlitchController : MonoBehaviour
    {
        /// <summary>Optional per-glitch audio. Leave the clip empty to stay silent.</summary>
        [Serializable]
        public class GlitchAudio
        {
            public AudioSource source;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        public enum ClockDesyncMode
        {
            JumpBackwards,
            Freeze,
            ImpossibleTime
        }

        /// <summary>Inspector-friendly "random, or pin to one mode" selector for Glitch E.</summary>
        public enum ClockDesyncModeChoice
        {
            Random,
            JumpBackwards,
            Freeze,
            ImpossibleTime
        }

        // ---------------------------------------------------------------------------------------
        // Wiring
        // ---------------------------------------------------------------------------------------

        [Header("Form Widgets (same objects IncidentReportUI drives)")]
        [Tooltip("The report window's room dropdown. Used by the Phantom Dropdown glitch.")]
        [SerializeField] private TMP_Dropdown locationDropdown;
        [Tooltip("The read-only Recognized field. Used by the False Recognition glitch.")]
        [SerializeField] private TMP_InputField recognizedField;
        [Tooltip("The '#0000' case number label. Used by the Case Corruption glitch.")]
        [SerializeField] private TextMeshProUGUI caseValueText;
        [Tooltip("The live shift clock. Used by the Clock Desync glitch.")]
        [SerializeField] private TextMeshProUGUI timeValueText;
        [Tooltip("Same NightTimer IncidentReportUI reads from, so Clock Desync rewinds/reverts against game time instead of the real-world clock. Auto-found in Awake() if left empty.")]
        [SerializeField] private NightTimer nightTimer;
        [Tooltip("Status bar sentence. Used by the Status Intrusion glitch.")]
        [SerializeField] private TextMeshProUGUI statusText;
        [Tooltip("Status bar badge background. Used by the Status Intrusion glitch.")]
        [SerializeField] private Image statusBadgeImage;
        [Tooltip("Status bar badge label (STANDBY / REC / READY / ...).")]
        [SerializeField] private TextMeshProUGUI statusBadgeText;

        // ---------------------------------------------------------------------------------------
        // Glitch A - Phantom dropdown entry
        // ---------------------------------------------------------------------------------------

        [Header("A - Phantom Dropdown")]
        [Tooltip("Fake room names. One is picked at random unless a scripted beat supplies an override.")]
        [SerializeField] private List<string> phantomOptionLabels = new List<string>
        {
            "Room 0", "———", "Basement (2)", "Your room", "[REDACTED]"
        };
        [Tooltip("Where to insert the phantom entry. -1 = append to the end of the list.")]
        [SerializeField] private int phantomInsertIndex = -1;
        [Tooltip("If the player never opens the dropdown, give up and remove the phantom after this many seconds.")]
        [SerializeField] private float phantomTimeoutSeconds = 14f;
        [Tooltip("Colour of the phantom entry's label, so it reads as subtly wrong.")]
        [SerializeField] private Color phantomOptionColor = new Color(0.45f, 0.45f, 0.45f);
        [SerializeField] private GlitchAudio phantomAudio;

        // ---------------------------------------------------------------------------------------
        // Glitch B - False recognition
        // ---------------------------------------------------------------------------------------

        [Header("B - False Recognition")]
        [Tooltip("Words the player never said. Display-only - never passed to validation.")]
        [SerializeField] private List<string> falseRecognitionWords = new List<string>
        {
            "behind you", "don't look", "it's here", "help me", "look up"
        };
        [Tooltip("x = min seconds, y = max seconds the false word stays on screen before self-correcting.")]
        [SerializeField] private Vector2 falseRecognitionDurationRange = new Vector2(0.6f, 1.2f);
        [Tooltip("Tint applied to the Recognized field while the false word is shown.")]
        [SerializeField] private Color falseRecognitionColor = new Color(0.62f, 0.16f, 0.16f);
        [Tooltip("This glitch arms itself and waits for the player to stop speaking. Once the recognized text has been unchanged for this long, the false word appears.")]
        [SerializeField] private float falseRecognitionSettleSeconds = 0.5f;
        [Tooltip("If the player never speaks, drop the armed glitch after this many seconds.")]
        [SerializeField] private float falseRecognitionArmTimeout = 25f;
        [SerializeField] private GlitchAudio falseRecognitionAudio;

        // ---------------------------------------------------------------------------------------
        // Glitch C - Case number corruption
        // ---------------------------------------------------------------------------------------

        [Header("C - Case Corruption")]
        [Tooltip("x = min seconds, y = max seconds the wrong case number is shown.")]
        [SerializeField] private Vector2 caseCorruptionDurationRange = new Vector2(0.5f, 1f);
        [Tooltip("Literal fallbacks used when there is no case history to repeat from yet.")]
        [SerializeField] private List<string> caseCorruptionLiterals = new List<string>
        {
            "#0000", "#6666", "#8888", "#----"
        };
        [Tooltip("Chance (0-1) of repeating a previously used case number instead of using a literal.")]
        [Range(0f, 1f)] [SerializeField] private float caseRepeatChance = 0.5f;
        [Tooltip("How many past case numbers to remember for repeat-corruption.")]
        [SerializeField] private int caseHistoryLimit = 24;
        [Tooltip("Tint applied to the case number while corrupted.")]
        [SerializeField] private Color caseCorruptionColor = new Color(0.62f, 0.16f, 0.16f);
        [SerializeField] private GlitchAudio caseCorruptionAudio;

        // ---------------------------------------------------------------------------------------
        // Glitch D - Status bar intrusion
        // ---------------------------------------------------------------------------------------

        [Header("D - Status Intrusion")]
        [Tooltip("Messages that briefly hijack the status bar.")]
        [SerializeField] private List<string> statusIntrusionMessages = new List<string>
        {
            "SEC-04 IS NOT ON SHIFT",
            "SIGNAL ORIGIN: INSIDE",
            "PREVIOUS OFFICER DID NOT REPORT",
            "WHY ARE YOU STILL HERE"
        };
        [Tooltip("x = min seconds, y = max seconds the intrusion is shown.")]
        [SerializeField] private Vector2 statusIntrusionDurationRange = new Vector2(0.3f, 0.8f);
        [Tooltip("Badge label shown during the intrusion. Leave empty to keep the current label.")]
        [SerializeField] private string statusIntrusionBadgeLabel = "??????";
        [Tooltip("Badge colour during the intrusion.")]
        [SerializeField] private Color statusIntrusionBadgeColor = new Color(0.35f, 0f, 0f);
        [Tooltip("Status sentence colour during the intrusion.")]
        [SerializeField] private Color statusIntrusionTextColor = new Color(0.62f, 0.16f, 0.16f);
        [SerializeField] private GlitchAudio statusIntrusionAudio;

        // ---------------------------------------------------------------------------------------
        // Glitch E - Clock desync
        // ---------------------------------------------------------------------------------------

        [Header("E - Clock Desync")]
        [Tooltip("x = min seconds, y = max seconds the clock stays desynced.")]
        [SerializeField] private Vector2 clockDesyncDurationRange = new Vector2(1f, 2f);
        [Tooltip("Impossible readouts used by the ImpossibleTime mode.")]
        [SerializeField] private List<string> impossibleTimeStrings = new List<string>
        {
            "25:61:99", "00:00:--", "88:88:88", "--:--:--", "13:60:61"
        };
        [Tooltip("x = min seconds, y = max seconds of IN-GAME time to rewind by in JumpBackwards mode (game time, not real-world time).")]
        [SerializeField] private Vector2 clockRewindSecondsRange = new Vector2(90f, 3600f);
        [Tooltip("Random picks one of the three each time. Pin to a specific mode when testing - Freeze in particular looks like nothing happened, because it holds the clock at its current reading.")]
        [SerializeField] private ClockDesyncModeChoice clockDesyncMode = ClockDesyncModeChoice.Random;
        [Tooltip("Tint applied to the clock while desynced.")]
        [SerializeField] private Color clockDesyncColor = new Color(0.62f, 0.16f, 0.16f);
        [SerializeField] private GlitchAudio clockDesyncAudio;

        // ---------------------------------------------------------------------------------------
        // Debug
        // ---------------------------------------------------------------------------------------

        [Header("Debug")]
        [Tooltip("Log every glitch fire and revert to the console, including the exact before -> after text.")]
        [SerializeField] private bool verboseLogging;
        [Tooltip("Hotkeys while playing: F1=Phantom F2=FalseRecognition F3=CaseCorruption F4=StatusIntrusion F5=ClockDesync F6=CancelAll. Lets you fire a glitch while your eyes are on the Game view instead of the Inspector.")]
        [SerializeField] private bool debugHotkeys = true;
        [Tooltip("When > 0, EVERY timed glitch uses this duration (seconds) instead of its own range. Set to ~5 while testing - the shipping durations (0.3-2s) are deliberately too fast to study.")]
        [SerializeField] private float debugDurationOverride;

        // ---------------------------------------------------------------------------------------
        // Runtime state
        // ---------------------------------------------------------------------------------------

        private readonly Dictionary<GlitchType, Coroutine> _running = new Dictionary<GlitchType, Coroutine>();
        // Revert actions are registered the moment a glitch mutates anything, so even a hard
        // StopCoroutine (form closed mid-glitch) can still put the UI back exactly as it was.
        private readonly Dictionary<GlitchType, Action> _reverts = new Dictionary<GlitchType, Action>();
        private readonly List<string> _caseHistory = new List<string>();

        private bool _formOpen;

        /// <summary>True between NotifyFormOpened() and NotifyFormClosed().</summary>
        public bool IsFormOpen => _formOpen;

        /// <summary>True while at least one glitch is mid-flight.</summary>
        public bool IsGlitchActive => _running.Count > 0;

        /// <summary>Case numbers seen so far this playthrough, oldest first. Used by Glitch C.</summary>
        public IReadOnlyList<string> CaseHistory => _caseHistory;

        void Awake()
        {
            if (nightTimer == null)
                nightTimer = FindObjectOfType<NightTimer>();
        }

        void Update()
        {
            if (!debugHotkeys) return;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame) PlayGlitch(GlitchType.PhantomDropdown);
            else if (kb.f2Key.wasPressedThisFrame) PlayGlitch(GlitchType.FalseRecognition);
            else if (kb.f3Key.wasPressedThisFrame) PlayGlitch(GlitchType.CaseCorruption);
            else if (kb.f4Key.wasPressedThisFrame) PlayGlitch(GlitchType.StatusIntrusion);
            else if (kb.f5Key.wasPressedThisFrame) PlayGlitch(GlitchType.ClockDesync);
            else if (kb.f6Key.wasPressedThisFrame) CancelAllGlitches();
#else
            if (Input.GetKeyDown(KeyCode.F1)) PlayGlitch(GlitchType.PhantomDropdown);
            else if (Input.GetKeyDown(KeyCode.F2)) PlayGlitch(GlitchType.FalseRecognition);
            else if (Input.GetKeyDown(KeyCode.F3)) PlayGlitch(GlitchType.CaseCorruption);
            else if (Input.GetKeyDown(KeyCode.F4)) PlayGlitch(GlitchType.StatusIntrusion);
            else if (Input.GetKeyDown(KeyCode.F5)) PlayGlitch(GlitchType.ClockDesync);
            else if (Input.GetKeyDown(KeyCode.F6)) CancelAllGlitches();
#endif
        }

        // ---------------------------------------------------------------------------------------
        // Lifecycle - called by GlitchDirector
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Call when the report window opens. Snapshots the session's real case number into history
        /// so Glitch C can repeat it later, and clears any stale glitch state.
        /// </summary>
        public void NotifyFormOpened()
        {
            CancelAllGlitches();
            _formOpen = true;

            if (caseValueText != null)
                RegisterCaseNumber(caseValueText.text);
        }

        /// <summary>
        /// Call when the report window closes (submit, cancel, or Spacebar). Immediately kills every
        /// running glitch coroutine and restores all cached values.
        /// </summary>
        public void NotifyFormClosed()
        {
            _formOpen = false;
            CancelAllGlitches();
        }

        /// <summary>
        /// Optional: call the moment push-to-talk stops, to fire an armed False Recognition without
        /// waiting for the settle timer. Safe to never call.
        /// </summary>
        public void NotifyRecordingStopped()
        {
            _recordingStoppedSignal = true;
        }

        private bool _recordingStoppedSignal;

        /// <summary>Adds a case number to the repeat-corruption history.</summary>
        public void RegisterCaseNumber(string caseNumber)
        {
            if (string.IsNullOrWhiteSpace(caseNumber)) return;
            if (_caseHistory.Contains(caseNumber)) return;

            _caseHistory.Add(caseNumber);
            while (_caseHistory.Count > Mathf.Max(1, caseHistoryLimit))
                _caseHistory.RemoveAt(0);
        }

        // ---------------------------------------------------------------------------------------
        // Public entry point
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Runs one glitch. <paramref name="overrideText"/> lets a scripted beat pin the exact
        /// string used (phantom label / false word / corrupted case / status message / clock readout);
        /// pass null or empty to pull randomly from the Inspector lists.
        /// Returns false if the glitch could not start (missing widget reference, or that glitch
        /// type is already running).
        /// </summary>
        public bool PlayGlitch(GlitchType type, string overrideText = null)
        {
            if (_running.ContainsKey(type))
            {
                if (verboseLogging)
                    Debug.Log($"[FormGlitch] {type} skipped - already running.", this);
                return false;
            }

            switch (type)
            {
                case GlitchType.PhantomDropdown:  return TriggerPhantomDropdown(overrideText);
                case GlitchType.FalseRecognition: return TriggerFalseRecognition(overrideText);
                case GlitchType.CaseCorruption:   return TriggerCaseCorruption(overrideText);
                case GlitchType.StatusIntrusion:  return TriggerStatusIntrusion(overrideText);
                case GlitchType.ClockDesync:      return TriggerClockDesync(overrideText);
                default: return false;
            }
        }

        /// <summary>
        /// Starts a glitch coroutine and records it as running.
        ///
        /// The slot is claimed BEFORE StartCoroutine, because StartCoroutine executes the routine
        /// body synchronously up to its first yield. If that body ever ran to completion in one go,
        /// its own "_running.Remove(type)" would fire before the handle could be stored - leaving a
        /// stale entry that makes PlayGlitch() reject that glitch type for the rest of the session.
        /// </summary>
        private void BeginTracked(GlitchType type, IEnumerator routine)
        {
            _running[type] = null;
            var handle = StartCoroutine(routine);

            // Still present means the routine yielded rather than finishing outright.
            if (_running.ContainsKey(type))
                _running[type] = handle;
        }

        /// <summary>Stops one glitch early and restores its cached values.</summary>
        public void CancelGlitch(GlitchType type)
        {
            if (_running.TryGetValue(type, out var routine))
            {
                if (routine != null) StopCoroutine(routine);
                _running.Remove(type);
            }

            if (_reverts.TryGetValue(type, out var revert))
            {
                _reverts.Remove(type);
                SafeInvoke(revert, type);
            }
        }

        /// <summary>Stops every glitch and restores every cached value. Safe to call at any time.</summary>
        public void CancelAllGlitches()
        {
            // Copy the keys first - the revert actions mutate the dictionaries.
            var types = new List<GlitchType>(_running.Keys);
            foreach (var t in types)
            {
                if (_running.TryGetValue(t, out var routine) && routine != null)
                    StopCoroutine(routine);
            }
            _running.Clear();

            var revertTypes = new List<GlitchType>(_reverts.Keys);
            foreach (var t in revertTypes)
            {
                if (_reverts.TryGetValue(t, out var revert))
                {
                    _reverts.Remove(t);
                    SafeInvoke(revert, t);
                }
            }

            _recordingStoppedSignal = false;
        }

        private void SafeInvoke(Action revert, GlitchType type)
        {
            if (revert == null) return;
            try
            {
                revert();
                if (verboseLogging)
                    Debug.Log($"[FormGlitch] {type} reverted.", this);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        void OnDisable()
        {
            CancelAllGlitches();
        }

        // =======================================================================================
        // Glitch A - Phantom dropdown entry
        // =======================================================================================

        /// <summary>
        /// Injects one fake room into the dropdown. Selecting it does nothing: the selection snaps
        /// back to whatever was picked before and the entry is removed once the list closes.
        /// </summary>
        public bool TriggerPhantomDropdown(string overrideText = null)
        {
            if (locationDropdown == null)
            {
                WarnMissing(GlitchType.PhantomDropdown, nameof(locationDropdown));
                return false;
            }

            string label = ResolveText(overrideText, phantomOptionLabels);
            if (string.IsNullOrEmpty(label)) return false;

            BeginTracked(GlitchType.PhantomDropdown, PhantomDropdownRoutine(label));
            LogFire(GlitchType.PhantomDropdown, label);
            PlayAudio(phantomAudio);
            return true;
        }

        private IEnumerator PhantomDropdownRoutine(string label)
        {
            var dd = locationDropdown;

            // Wait for any already-open list to close, so the injected entry appears on a fresh open.
            float waitStart = Time.unscaledTime;
            while (dd.IsExpanded && Time.unscaledTime - waitStart < phantomTimeoutSeconds)
                yield return null;

            // --- Snapshot the real state by TEXT, so restoring can never mangle the selection.
            var realOptions = new List<string>(dd.options.Count);
            foreach (var o in dd.options) realOptions.Add(o.text);
            string realSelectedText = (dd.value >= 0 && dd.value < dd.options.Count)
                ? dd.options[dd.value].text
                : null;

            int insertAt = phantomInsertIndex < 0 || phantomInsertIndex > dd.options.Count
                ? dd.options.Count
                : phantomInsertIndex;

            bool removed = false;
            Action revert = () =>
            {
                if (removed) return;
                removed = true;
                dd.onValueChanged.RemoveListener(OnPhantomDropdownValueChanged);
                RestoreDropdown(dd, realOptions, realSelectedText);
            };
            _reverts[GlitchType.PhantomDropdown] = revert;

            // --- Inject. Mutating dd.options directly fires no callbacks.
            var phantom = new TMP_Dropdown.OptionData(label);
            dd.options.Insert(insertAt, phantom);
            _phantomIndex = insertAt;
            // Inserting at or before the selection would silently shift what dd.value points at.
            if (insertAt <= dd.value)
                dd.SetValueWithoutNotify(dd.value + 1);
            _phantomPreviousValue = dd.value;
            dd.RefreshShownValue();

            _phantomWasSelected = false;
            dd.onValueChanged.AddListener(OnPhantomDropdownValueChanged);

            // --- Wait for the player to open the list at least once, then close it again.
            float deadline = Time.unscaledTime + phantomTimeoutSeconds;
            bool everOpened = false;

            while (Time.unscaledTime < deadline && !_phantomWasSelected)
            {
                if (dd.IsExpanded)
                {
                    everOpened = true;
                    TintPhantomLabel(dd, label);
                }
                else if (everOpened)
                {
                    break; // opened and closed - clean up
                }
                yield return null;
            }

            _running.Remove(GlitchType.PhantomDropdown);
            _reverts.Remove(GlitchType.PhantomDropdown);
            SafeInvoke(revert, GlitchType.PhantomDropdown);
        }

        private int _phantomIndex = -1;
        private int _phantomPreviousValue;
        private bool _phantomWasSelected;

        private void OnPhantomDropdownValueChanged(int newValue)
        {
            if (newValue != _phantomIndex) return;

            // The phantom was clicked: refuse the selection. TMP_Dropdown has already closed the
            // list by this point, so all that is left is snapping the value back.
            _phantomWasSelected = true;
            locationDropdown.SetValueWithoutNotify(_phantomPreviousValue);
            locationDropdown.RefreshShownValue();

            if (verboseLogging)
                Debug.Log("[FormGlitch] Phantom option clicked - selection reverted.", this);
        }

        private static void RestoreDropdown(TMP_Dropdown dd, List<string> realOptions, string realSelectedText)
        {
            if (dd == null) return;

            dd.options.Clear();
            foreach (var text in realOptions)
                dd.options.Add(new TMP_Dropdown.OptionData(text));

            int restoredIndex = 0;
            if (!string.IsNullOrEmpty(realSelectedText))
            {
                for (int i = 0; i < realOptions.Count; i++)
                {
                    if (realOptions[i] == realSelectedText) { restoredIndex = i; break; }
                }
            }

            dd.SetValueWithoutNotify(restoredIndex);
            dd.RefreshShownValue();
        }

        /// <summary>Recolours the phantom row inside the open dropdown list, if it can be found.</summary>
        private void TintPhantomLabel(TMP_Dropdown dd, string label)
        {
            var list = dd.transform.Find("Dropdown List");
            if (list == null) return;

            var labels = list.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in labels)
            {
                if (t != null && t.text == label)
                    t.color = phantomOptionColor;
            }
        }

        // =======================================================================================
        // Glitch B - False recognition
        // =======================================================================================

        /// <summary>
        /// Arms a display-only lie. Once the player stops speaking, the Recognized field briefly
        /// shows a word they never said, then corrects itself. The manager's stored keyword - the
        /// one actually validated - is never touched.
        /// </summary>
        public bool TriggerFalseRecognition(string overrideText = null)
        {
            if (recognizedField == null)
            {
                WarnMissing(GlitchType.FalseRecognition, nameof(recognizedField));
                return false;
            }

            string word = ResolveText(overrideText, falseRecognitionWords);
            if (string.IsNullOrEmpty(word)) return false;

            BeginTracked(GlitchType.FalseRecognition, FalseRecognitionRoutine(word));
            LogFire(GlitchType.FalseRecognition, word + " (armed)");
            return true;
        }

        private IEnumerator FalseRecognitionRoutine(string falseWord)
        {
            var field = recognizedField;
            _recordingStoppedSignal = false;

            // --- Arm: wait until the player has actually said something and stopped.
            float armDeadline = Time.unscaledTime + falseRecognitionArmTimeout;
            string lastSeen = field.text;
            float stableSince = Time.unscaledTime;

            while (Time.unscaledTime < armDeadline)
            {
                if (field.text != lastSeen)
                {
                    lastSeen = field.text;
                    stableSince = Time.unscaledTime;
                }

                bool hasSpeech = !string.IsNullOrWhiteSpace(lastSeen);
                bool settled = Time.unscaledTime - stableSince >= falseRecognitionSettleSeconds;

                if (hasSpeech && (settled || _recordingStoppedSignal))
                    break;

                yield return null;
            }

            _recordingStoppedSignal = false;

            if (string.IsNullOrWhiteSpace(field.text))
            {
                // Player never spoke - drop the glitch rather than lying about silence.
                if (verboseLogging)
                    Debug.Log("[FormGlitch] FalseRecognition disarmed - no speech captured.", this);
                _running.Remove(GlitchType.FalseRecognition);
                yield break;
            }

            // --- Fire.
            var textComponent = field.textComponent;
            Color originalColor = textComponent != null ? textComponent.color : Color.black;
            string cachedReal = field.text;

            bool restored = false;
            Action revert = () =>
            {
                if (restored) return;
                restored = true;
                if (field != null) field.text = cachedReal;
                if (textComponent != null) textComponent.color = originalColor;
            };
            _reverts[GlitchType.FalseRecognition] = revert;

            field.text = falseWord;
            if (textComponent != null) textComponent.color = falseRecognitionColor;
            PlayAudio(falseRecognitionAudio);

            float duration = ResolveDuration(falseRecognitionDurationRange);
            float end = Time.unscaledTime + duration;
            while (Time.unscaledTime < end)
            {
                // If real recognition lands mid-lie, keep the lie on screen but remember the newer
                // real value so the correction snaps to the truth, not to a stale snapshot.
                if (field.text != falseWord)
                {
                    cachedReal = field.text;
                    field.text = falseWord;
                }
                yield return null;
            }

            _running.Remove(GlitchType.FalseRecognition);
            _reverts.Remove(GlitchType.FalseRecognition);
            SafeInvoke(revert, GlitchType.FalseRecognition);
        }

        // =======================================================================================
        // Glitch C - Case number corruption
        // =======================================================================================

        /// <summary>Briefly shows a wrong case number - all zeroes, a repeated digit, or a past case.</summary>
        public bool TriggerCaseCorruption(string overrideText = null)
        {
            if (caseValueText == null)
            {
                WarnMissing(GlitchType.CaseCorruption, nameof(caseValueText));
                return false;
            }

            string corrupted = overrideText;
            if (string.IsNullOrWhiteSpace(corrupted))
            {
                // Repeating a case number the player has already filed is the nastiest variant, so
                // prefer it when there is history to draw from.
                var repeatable = new List<string>();
                foreach (var c in _caseHistory)
                {
                    if (c != caseValueText.text) repeatable.Add(c);
                }

                if (repeatable.Count > 0 && UnityEngine.Random.value < caseRepeatChance)
                    corrupted = repeatable[UnityEngine.Random.Range(0, repeatable.Count)];
                else
                    corrupted = PickRandom(caseCorruptionLiterals);
            }

            if (string.IsNullOrEmpty(corrupted)) return false;

            BeginTracked(GlitchType.CaseCorruption, CaseCorruptionRoutine(corrupted));
            LogFire(GlitchType.CaseCorruption, corrupted);
            PlayAudio(caseCorruptionAudio);
            return true;
        }

        private IEnumerator CaseCorruptionRoutine(string corrupted)
        {
            var label = caseValueText;
            string cachedReal = label.text;
            Color originalColor = label.color;

            bool restored = false;
            Action revert = () =>
            {
                if (restored) return;
                restored = true;
                if (label != null)
                {
                    label.text = cachedReal;
                    label.color = originalColor;
                }
            };
            _reverts[GlitchType.CaseCorruption] = revert;

            label.text = corrupted;
            label.color = caseCorruptionColor;

            float duration = ResolveDuration(caseCorruptionDurationRange);
            LogApplied(GlitchType.CaseCorruption, nameof(caseValueText), cachedReal, corrupted, duration);

            float end = Time.unscaledTime + duration;
            while (Time.unscaledTime < end)
            {
                if (label.text != corrupted)
                {
                    cachedReal = label.text;
                    label.text = corrupted;
                }
                yield return null;
            }

            _running.Remove(GlitchType.CaseCorruption);
            _reverts.Remove(GlitchType.CaseCorruption);
            SafeInvoke(revert, GlitchType.CaseCorruption);
        }

        // =======================================================================================
        // Glitch D - Status bar intrusion
        // =======================================================================================

        /// <summary>
        /// Flashes an unsettling line over the status bar, then restores whatever status and badge
        /// state was live before - including an ALERT raised while the intrusion was on screen.
        /// </summary>
        public bool TriggerStatusIntrusion(string overrideText = null)
        {
            if (statusText == null)
            {
                WarnMissing(GlitchType.StatusIntrusion, nameof(statusText));
                return false;
            }

            string message = ResolveText(overrideText, statusIntrusionMessages);
            if (string.IsNullOrEmpty(message)) return false;

            BeginTracked(GlitchType.StatusIntrusion, StatusIntrusionRoutine(message));
            LogFire(GlitchType.StatusIntrusion, message);
            PlayAudio(statusIntrusionAudio);
            return true;
        }

        private IEnumerator StatusIntrusionRoutine(string message)
        {
            string cachedStatus = statusText.text;
            Color cachedStatusColor = statusText.color;
            string cachedBadgeLabel = statusBadgeText != null ? statusBadgeText.text : null;
            Color cachedBadgeColor = statusBadgeImage != null ? statusBadgeImage.color : Color.white;

            bool restored = false;
            Action revert = () =>
            {
                if (restored) return;
                restored = true;
                if (statusText != null)
                {
                    statusText.text = cachedStatus;
                    statusText.color = cachedStatusColor;
                }
                if (statusBadgeText != null && cachedBadgeLabel != null)
                    statusBadgeText.text = cachedBadgeLabel;
                if (statusBadgeImage != null)
                    statusBadgeImage.color = cachedBadgeColor;
            };
            _reverts[GlitchType.StatusIntrusion] = revert;

            string badgeShown = cachedBadgeLabel;
            statusText.text = message;
            statusText.color = statusIntrusionTextColor;
            if (statusBadgeText != null && !string.IsNullOrEmpty(statusIntrusionBadgeLabel))
            {
                statusBadgeText.text = statusIntrusionBadgeLabel;
                badgeShown = statusIntrusionBadgeLabel;
            }
            if (statusBadgeImage != null)
                statusBadgeImage.color = statusIntrusionBadgeColor;

            float duration = ResolveDuration(statusIntrusionDurationRange);
            LogApplied(GlitchType.StatusIntrusion, nameof(statusText), cachedStatus, message, duration);

            float end = Time.unscaledTime + duration;
            while (Time.unscaledTime < end)
            {
                // IncidentReportUI may legitimately refresh the status mid-flash (ALERT raised, PTT
                // toggled). Track those writes so the restore lands on the newest real state.
                if (statusText.text != message)
                {
                    cachedStatus = statusText.text;
                    statusText.text = message;
                }
                if (statusBadgeText != null && badgeShown != null && statusBadgeText.text != badgeShown)
                {
                    cachedBadgeLabel = statusBadgeText.text;
                    statusBadgeText.text = badgeShown;
                }
                yield return null;
            }

            _running.Remove(GlitchType.StatusIntrusion);
            _reverts.Remove(GlitchType.StatusIntrusion);
            SafeInvoke(revert, GlitchType.StatusIntrusion);
        }

        // =======================================================================================
        // Glitch E - Clock desync
        // =======================================================================================

        /// <summary>
        /// Rewinds, freezes, or breaks the shift clock for a beat. IncidentReportUI's own
        /// InvokeRepeating clock tick is never stopped, so the clock always resyncs on its own.
        /// </summary>
        public bool TriggerClockDesync(string overrideText = null)
        {
            if (timeValueText == null)
            {
                WarnMissing(GlitchType.ClockDesync, nameof(timeValueText));
                return false;
            }

            string forced = overrideText;

            ClockDesyncMode mode = clockDesyncMode == ClockDesyncModeChoice.Random
                ? (ClockDesyncMode)UnityEngine.Random.Range(0, 3)
                : (ClockDesyncMode)(clockDesyncMode - 1); // Random occupies slot 0 in the choice enum
            if (!string.IsNullOrWhiteSpace(forced))
                mode = ClockDesyncMode.ImpossibleTime;

            BeginTracked(GlitchType.ClockDesync, ClockDesyncRoutine(mode, forced));
            LogFire(GlitchType.ClockDesync, mode.ToString());
            PlayAudio(clockDesyncAudio);
            return true;
        }

        private IEnumerator ClockDesyncRoutine(ClockDesyncMode mode, string forcedText)
        {
            var label = timeValueText;
            Color originalColor = label.color;

            bool restored = false;
            Action revert = () =>
            {
                if (restored) return;
                restored = true;
                if (label != null)
                {
                    label.color = originalColor;
                    // Snap straight back to the truth instead of waiting up to a second for the
                    // window's own clock tick.
                    label.text = FormatCurrentGameTime();
                }
            };
            _reverts[GlitchType.ClockDesync] = revert;

            string desyncedText;
            switch (mode)
            {
                case ClockDesyncMode.Freeze:
                    desyncedText = label.text;
                    break;
                case ClockDesyncMode.JumpBackwards:
                    desyncedText = FormatRewoundGameTime(RandomInRange(clockRewindSecondsRange));
                    break;
                default:
                    desyncedText = !string.IsNullOrWhiteSpace(forcedText)
                        ? forcedText
                        : PickRandom(impossibleTimeStrings);
                    break;
            }

            if (string.IsNullOrEmpty(desyncedText))
                desyncedText = "--:--:--";

            string beforeText = label.text;
            label.color = clockDesyncColor;

            // The window ticks its clock once a second, so hold the desynced value every frame.
            float duration = ResolveDuration(clockDesyncDurationRange);
            LogApplied(GlitchType.ClockDesync, $"{nameof(timeValueText)} [{mode}]", beforeText, desyncedText, duration);

            if (mode == ClockDesyncMode.Freeze && beforeText == desyncedText && verboseLogging)
            {
                Debug.LogWarning("[FormGlitch] ClockDesync Freeze is holding the clock at its CURRENT reading - " +
                                 "on screen this looks identical to no glitch at all. Pin 'Clock Desync Mode' to " +
                                 "JumpBackwards or ImpossibleTime to see an obvious change while testing.", this);
            }

            float end = Time.unscaledTime + duration;
            while (Time.unscaledTime < end)
            {
                if (label.text != desyncedText)
                    label.text = desyncedText;
                yield return null;
            }

            _running.Remove(GlitchType.ClockDesync);
            _reverts.Remove(GlitchType.ClockDesync);
            SafeInvoke(revert, GlitchType.ClockDesync);
        }

        /// <summary>The true, un-glitched clock text right now - same source and format IncidentReportUI uses.</summary>
        private string FormatCurrentGameTime()
        {
            return nightTimer != null
                ? NightTimer.FormatGameTime(nightTimer.GetGameTimeHours())
                : "--:--:--";
        }

        /// <summary>The clock rewound by <paramref name="rewindSeconds"/> of IN-GAME time, clamped to 0:00 AM.</summary>
        private string FormatRewoundGameTime(float rewindSeconds)
        {
            if (nightTimer == null) return "--:--:--";

            float currentSeconds = nightTimer.GetGameTimeHours() * 3600f;
            float rewoundSeconds = Mathf.Max(0f, currentSeconds - rewindSeconds);
            return NightTimer.FormatGameTime(rewoundSeconds / 3600f);
        }

        // =======================================================================================
        // Helpers
        // =======================================================================================

        private string ResolveText(string overrideText, List<string> pool)
        {
            return !string.IsNullOrWhiteSpace(overrideText) ? overrideText : PickRandom(pool);
        }

        private static string PickRandom(List<string> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        private static float RandomInRange(Vector2 range)
        {
            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);
            return UnityEngine.Random.Range(min, max);
        }

        /// <summary>
        /// How long a timed glitch should hold. Honours debugDurationOverride so testing can stretch
        /// every glitch to a length you can actually study, without editing five separate ranges.
        /// </summary>
        private float ResolveDuration(Vector2 range)
        {
            return debugDurationOverride > 0f ? debugDurationOverride : RandomInRange(range);
        }

        private static void PlayAudio(GlitchAudio audio)
        {
            if (audio == null || audio.source == null || audio.clip == null) return;
            audio.source.PlayOneShot(audio.clip, audio.volume);
        }

        private void LogFire(GlitchType type, string detail)
        {
            if (verboseLogging)
                Debug.Log($"[FormGlitch] FIRE {type} :: \"{detail}\"", this);
        }

        /// <summary>
        /// Proof-of-work log: shows the exact text that was on screen before the glitch, what it was
        /// replaced with, and for how long. If this line appears but you saw nothing, the glitch DID
        /// run and the problem is that it was too brief - raise debugDurationOverride.
        /// </summary>
        private void LogApplied(GlitchType type, string widget, string before, string after, float seconds)
        {
            if (!verboseLogging) return;
            Debug.Log($"[FormGlitch] {type} applied to {widget}: \"{before}\" -> \"{after}\" for {seconds:F2}s", this);
        }

        private void WarnMissing(GlitchType type, string fieldName)
        {
            Debug.LogWarning($"[FormGlitch] {type} skipped - '{fieldName}' is not assigned in the Inspector.", this);
        }

        // =======================================================================================
        // Inspector debug triggers
        // =======================================================================================

        [ContextMenu("Glitch/A - Phantom Dropdown")]
        private void DebugPhantomDropdown() => PlayGlitch(GlitchType.PhantomDropdown);

        [ContextMenu("Glitch/B - False Recognition")]
        private void DebugFalseRecognition() => PlayGlitch(GlitchType.FalseRecognition);

        [ContextMenu("Glitch/C - Case Corruption")]
        private void DebugCaseCorruption() => PlayGlitch(GlitchType.CaseCorruption);

        [ContextMenu("Glitch/D - Status Intrusion")]
        private void DebugStatusIntrusion() => PlayGlitch(GlitchType.StatusIntrusion);

        [ContextMenu("Glitch/E - Clock Desync")]
        private void DebugClockDesync() => PlayGlitch(GlitchType.ClockDesync);

        [ContextMenu("Glitch/Cancel All")]
        private void DebugCancelAll() => CancelAllGlitches();

        /// <summary>
        /// Fires C -> D -> E one at a time, each held for 4 seconds, so the three "invisible" glitches
        /// can be watched back to back. Play Mode only (it needs coroutines).
        /// </summary>
        [ContextMenu("Glitch/RUN DEMO (C, D, E - 4s each)")]
        private void DebugRunDemo()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[FormGlitch] Demo needs Play Mode - coroutines don't run in edit mode.", this);
                return;
            }

            StopCoroutine(nameof(DemoRoutine));
            StartCoroutine(DemoRoutine());
        }

        private IEnumerator DemoRoutine()
        {
            float savedOverride = debugDurationOverride;
            bool savedVerbose = verboseLogging;
            debugDurationOverride = 4f;
            verboseLogging = true;

            Debug.Log("[FormGlitch] === DEMO START - watch the Game view, NOT the Inspector ===", this);

            var order = new[] { GlitchType.CaseCorruption, GlitchType.StatusIntrusion, GlitchType.ClockDesync };
            foreach (var type in order)
            {
                Debug.Log($"[FormGlitch] DEMO -> {type} (4s)", this);
                PlayGlitch(type);
                yield return new WaitForSecondsRealtime(5f);
            }

            Debug.Log("[FormGlitch] === DEMO END ===", this);

            debugDurationOverride = savedOverride;
            verboseLogging = savedVerbose;
        }
    }
}
