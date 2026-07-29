using System.Collections;
using System.Collections.Generic;
using GameLogic.SpawnAndTime;
using UnityEngine;

namespace Report
{
    /// <summary>
    /// One glitch pinned to a specific real minute of the night - independent of report count,
    /// game hour, or anomaly state. E.g. "2.5 -> ClockDesync", "2.7 -> PhantomDropdown".
    /// </summary>
    [System.Serializable]
    public class GlitchScheduleEntry
    {
        [Tooltip("Label shown in the Inspector list. Auto-filled from the time + glitch type if left empty.")]
        public string entryName = "";

        [Tooltip("REAL minutes after the night starts. Compare against the Night Timer's duration, not the in-game clock.")]
        [Min(0f)]
        public float atMinute = 1f;

        public GlitchType glitchType = GlitchType.StatusIntrusion;

        [Tooltip("Optional. If set, this exact string is used instead of a random pick from the controller's word list.")]
        public string overrideText = "";

        [Tooltip("Extra delay after the moment this glitch actually fires (either immediately, if the report form is already open at Spawn At Minute, or right when the form next opens). Lets several glitches scheduled close together stagger instead of all flashing on the same frame.")]
        [Min(0f)]
        public float fireDelay = 0f;
    }

    /// <summary>
    /// Fires Form Glitches on a simple minute-based timeline, completely independent of
    /// GlitchDirector's report-count/game-hour scripted beats and ambient random system -
    /// same idea as AnomalyScheduler, but for glitches instead of anomalies.
    ///
    /// How to use:
    ///   1. Add entries to the Schedule list: pick a glitch type and type the minute it should fire.
    ///   2. Press Play - each entry fires once its minute passes, bypassing cooldowns/blackouts
    ///      (this is an authored beat, not a random roll).
    ///
    /// Form Glitches only make sense while the Incident Report window is open (they glitch that
    /// window's own widgets), so a scheduled entry whose minute arrives while the form is CLOSED
    /// is queued and fires the moment the form next opens - it never gets silently skipped.
    /// </summary>
    public class GlitchScheduler : MonoBehaviour
    {
        [Header("Schedule (real minutes into the night)")]
        [SerializeField] private List<GlitchScheduleEntry> schedule = new List<GlitchScheduleEntry>();

        [Header("References")]
        [Tooltip("Auto-found in the scene if left empty.")]
        [SerializeField] private NightTimer nightTimer;
        [Tooltip("Auto-found in the scene if left empty. Executes the actual glitches.")]
        [SerializeField] private GlitchDirector glitchDirector;
        [Tooltip("Auto-found (IncidentReportManager.Instance) if left empty. Used to check whether the report form is currently open.")]
        [SerializeField] private IncidentReportManager reportManager;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        // Sorted copy of the schedule; _nextIndex walks it forward as time passes.
        private readonly List<GlitchScheduleEntry> _sorted = new List<GlitchScheduleEntry>();
        private int _nextIndex;

        // Entries whose minute has passed but the form was closed at the time - fired as soon
        // as the form opens, so nothing scheduled is ever silently dropped.
        private readonly Queue<GlitchScheduleEntry> _pending = new Queue<GlitchScheduleEntry>();

        void Start()
        {
            if (nightTimer == null)
                nightTimer = FindFirstObjectByType<NightTimer>();
            if (glitchDirector == null)
                glitchDirector = FindFirstObjectByType<GlitchDirector>();
            if (reportManager == null)
                reportManager = IncidentReportManager.Instance != null ? IncidentReportManager.Instance : FindFirstObjectByType<IncidentReportManager>();

            if (nightTimer == null || glitchDirector == null)
            {
                Debug.LogError("GlitchScheduler: missing NightTimer or GlitchDirector - nothing will fire.", this);
                enabled = false;
                return;
            }

            BuildSortedSchedule();
            nightTimer.OnTimeChanged += OnTimeChanged;

            if (showDebugInfo)
                LogSchedule();
        }

        void OnDestroy()
        {
            if (nightTimer != null)
                nightTimer.OnTimeChanged -= OnTimeChanged;
        }

        private void BuildSortedSchedule()
        {
            _sorted.Clear();
            _nextIndex = 0;
            _pending.Clear();

            foreach (var entry in schedule)
            {
                if (entry == null) continue;

                if (entry.atMinute > nightTimer.NightDurationMinutes)
                {
                    Debug.LogWarning(
                        $"GlitchScheduler: entry '{Label(entry)}' is scheduled at minute {entry.atMinute:F2} " +
                        $"but the night only lasts {nightTimer.NightDurationMinutes:F2} minutes - it will never fire.", this);
                }

                _sorted.Add(entry);
            }

            _sorted.Sort((a, b) => a.atMinute.CompareTo(b.atMinute));
        }

        private void OnTimeChanged(float normalizedTime)
        {
            float elapsedMinutes = normalizedTime * nightTimer.NightDurationMinutes;

            while (_nextIndex < _sorted.Count && _sorted[_nextIndex].atMinute <= elapsedMinutes)
            {
                _pending.Enqueue(_sorted[_nextIndex]);
                _nextIndex++;
            }

            if (_pending.Count == 0) return;

            bool reportOpen = reportManager != null && reportManager.IsReportOpen;
            if (!reportOpen) return;

            while (_pending.Count > 0)
                Fire(_pending.Dequeue());
        }

        private void Fire(GlitchScheduleEntry entry)
        {
            if (entry.fireDelay > 0f)
            {
                StartCoroutine(FireAfterDelay(entry));
            }
            else
            {
                glitchDirector.FireGlitchNow(entry.glitchType, NullIfEmpty(entry.overrideText));

                if (showDebugInfo)
                    Debug.Log($"GlitchScheduler: fired '{Label(entry)}'.", this);
            }
        }

        private IEnumerator FireAfterDelay(GlitchScheduleEntry entry)
        {
            yield return new WaitForSecondsRealtime(entry.fireDelay);

            glitchDirector.FireGlitchNow(entry.glitchType, NullIfEmpty(entry.overrideText));

            if (showDebugInfo)
                Debug.Log($"GlitchScheduler: fired '{Label(entry)}' (after {entry.fireDelay:F1}s delay).", this);
        }

        private static string NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

        /// <summary>In-game clock label (e.g. "2:24 AM") for a schedule minute, given the current night duration.</summary>
        public string GameClockLabelFor(float minute)
        {
            float duration = nightTimer != null ? nightTimer.NightDurationMinutes : 5f;
            if (duration <= 0f) duration = 5f;
            float gameHours = Mathf.Clamp01(minute / duration) * NightTimer.GameHoursPerNight;
            return NightTimer.FormatGameTime(gameHours, includeSeconds: false);
        }

        private static string Label(GlitchScheduleEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.entryName)) return entry.entryName;
            return $"{entry.atMinute:0.##}m {entry.glitchType}";
        }

        private void LogSchedule()
        {
            var sb = new System.Text.StringBuilder("=== Glitch Schedule ===\n");
            foreach (var entry in _sorted)
                sb.AppendLine($"  minute {entry.atMinute,5:0.##} ({GameClockLabelFor(entry.atMinute)})  {Label(entry)}");
            Debug.Log(sb.ToString(), this);
        }

        void OnValidate()
        {
            foreach (var entry in schedule)
            {
                if (entry != null && string.IsNullOrEmpty(entry.entryName))
                    entry.entryName = $"{entry.atMinute:0.##}m - {entry.glitchType}";
            }
        }

        [ContextMenu("Sort Entries By Time")]
        private void SortEntriesByTime()
        {
            schedule.Sort((a, b) => a.atMinute.CompareTo(b.atMinute));
        }

        [ContextMenu("Force Fire Next")]
        public void ForceFireNext()
        {
            if (_nextIndex < _sorted.Count)
            {
                var entry = _sorted[_nextIndex];
                _nextIndex++;
                Fire(entry);
            }
            else
            {
                Debug.Log("GlitchScheduler: no more entries to fire.", this);
            }
        }
    }
}
