using System.Collections.Generic;
using GameLogic.Data;
using GameLogic.Night;
using UnityEngine;

namespace GameLogic.SpawnAndTime
{
    /// <summary>Where an AnomalyScheduler gets its timeline from.</summary>
    public enum ScheduleSource
    {
        /// <summary>The procedurally generated NightPlan. The normal mode.</summary>
        NightPlan,
        /// <summary>The hand-authored Schedule list below. Kept for testing a specific sequence.</summary>
        ManualList,
    }

    /// <summary>
    /// One hand-authored timeline entry: which anomaly appears, at which REAL minute of the night,
    /// and where. Only used when Source is ManualList.
    /// </summary>
    [System.Serializable]
    public class AnomalyScheduleEntry
    {
        [Tooltip("Label shown in the Inspector list. Auto-filled from the time + prefab name if left empty.")]
        public string entryName = "";

        [Tooltip("REAL minutes after the night starts (e.g. 2.5 = two and a half minutes in). Compare against the Night Timer's duration, not the in-game clock.")]
        [Min(0f)]
        public float spawnAtMinute = 1f;

        [Tooltip("Anomaly prefab to spawn. Must contain an Anomaly component (root or child).")]
        public GameObject anomalyPrefab;

        [Tooltip("OPTIONAL. Drag any scene Transform here to spawn at that position instead of the position saved inside the prefab.")]
        public Transform spawnPoint;

        public bool IsValid() => anomalyPrefab != null;

        /// <summary>Where this entry will actually spawn (spawn point override, else the prefab's own position).</summary>
        public Vector3 ResolvePosition()
        {
            if (spawnPoint != null) return spawnPoint.position;
            return anomalyPrefab != null ? anomalyPrefab.transform.position : Vector3.zero;
        }
    }

    /// <summary>
    /// Spawns anomalies on a minute-based timeline, normally the one in the night's
    /// <see cref="NightPlan"/>.
    ///
    /// The timeline is built on the night timer's FIRST TICK rather than in Start, because that is
    /// guaranteed to happen after every Start in the scene - so NightPlanRunner is certain to have
    /// published its plan by then, with no script execution order to get right.
    ///
    /// Entries are pre-sorted once and consumed with a single index cursor, so the per-frame cost
    /// is one float comparison - no list scans or allocations.
    /// </summary>
    public class AnomalyScheduler : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("NightPlan = procedurally generated (normal). ManualList = the hand-authored Schedule below, for testing a fixed sequence.")]
        [SerializeField] private ScheduleSource source = ScheduleSource.NightPlan;

        [Header("Schedule (ManualList mode only, real minutes into the night)")]
        [SerializeField] private List<AnomalyScheduleEntry> schedule = new List<AnomalyScheduleEntry>();

        [Header("References")]
        [Tooltip("Auto-found in the scene if left empty.")]
        [SerializeField] private NightTimer nightTimer;
        [Tooltip("Optional parent for spawned anomalies (keeps the Hierarchy tidy).")]
        [SerializeField] private Transform anomalyParent;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color gizmoColor = Color.red;
        [SerializeField] private float gizmoSize = 0.5f;

        /// <summary>One resolved spawn, whichever source it came from.</summary>
        private struct ScheduledSpawn
        {
            public float atMinute;
            public GameObject prefab;
            public Transform point;      // may be null - then the prefab's own position is used
            public RoomDefinition room;  // null for manual entries, which have no room concept
            public string label;
        }

        private readonly List<ScheduledSpawn> _sorted = new List<ScheduledSpawn>();
        private int _nextIndex;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private bool _built;
        private bool _allSpawnedNotified;

        // Seeded from the plan so which spawn point a room picks is part of the night's seed too -
        // otherwise replaying a seed would put anomalies in subtly different places.
        private System.Random _rng = new System.Random(0);

        /// <summary>Fired right after an anomaly is instantiated.</summary>
        public System.Action<GameObject> OnAnomalySpawned;
        /// <summary>Fired once when the last scheduled entry has spawned. Payload = total spawned.</summary>
        public System.Action<int> OnAllAnomaliesSpawned;

        public int RemainingCount => _sorted.Count - _nextIndex;

        /// <summary>How many anomalies have been spawned so far this night (destroyed ones still count).</summary>
        public int TotalSpawned => _spawned.Count;

        void Start()
        {
            if (nightTimer == null)
                nightTimer = FindFirstObjectByType<NightTimer>();

            if (nightTimer == null)
            {
                Debug.LogError("AnomalyScheduler: No NightTimer found in the scene - nothing will spawn.", this);
                enabled = false;
                return;
            }

            nightTimer.OnTimeChanged += OnTimeChanged;
        }

        void OnDestroy()
        {
            if (nightTimer != null)
                nightTimer.OnTimeChanged -= OnTimeChanged;
        }

        // ── Building the timeline ────────────────────────────────────────────────────────

        private void BuildTimeline()
        {
            _sorted.Clear();
            _nextIndex = 0;
            _allSpawnedNotified = false;

            if (source == ScheduleSource.NightPlan)
                BuildFromPlan();
            else
                BuildFromManualList();

            _sorted.Sort((a, b) => a.atMinute.CompareTo(b.atMinute));

            if (showDebugInfo)
                LogSchedule();
        }

        private void BuildFromPlan()
        {
            var plan = NightPlanProvider.Current;
            _rng = new System.Random(plan.seed);

            foreach (var placement in plan.anomalies)
            {
                if (placement.definition == null || placement.definition.prefab == null)
                {
                    Debug.LogWarning($"AnomalyScheduler: plan placement at {placement.atMinute:0.##}m has no prefab - skipped.", this);
                    continue;
                }

                // A room the loaded scene has no anchor for can't provide a position; the prefab's
                // own position is used instead so the anomaly still appears somewhere.
                var anchor = RoomRegistry.Get(placement.room);
                if (anchor == null && placement.room != null)
                {
                    Debug.LogWarning(
                        $"AnomalyScheduler: room '{placement.room.Label}' has no RoomAnchor in this scene - " +
                        $"'{placement.definition.Label}' will spawn at its prefab position.", this);
                }

                _sorted.Add(new ScheduledSpawn
                {
                    atMinute = placement.atMinute,
                    prefab = placement.definition.prefab,
                    point = anchor != null ? anchor.GetSpawnPoint(_rng) : null,
                    room = placement.room,
                    label = $"{placement.definition.Label} in {placement.room?.Label ?? "(no room)"}",
                });
            }
        }

        private void BuildFromManualList()
        {
            foreach (var entry in schedule)
            {
                if (entry == null) continue;

                if (!entry.IsValid())
                {
                    Debug.LogWarning($"AnomalyScheduler: entry '{entry.entryName}' has no prefab assigned - skipped.", this);
                    continue;
                }

                if (entry.spawnAtMinute > nightTimer.NightDurationMinutes)
                {
                    Debug.LogWarning(
                        $"AnomalyScheduler: entry '{entry.entryName}' is scheduled at minute {entry.spawnAtMinute:F2} " +
                        $"but the night only lasts {nightTimer.NightDurationMinutes:F2} minutes - it will never spawn.", this);
                }

                _sorted.Add(new ScheduledSpawn
                {
                    atMinute = entry.spawnAtMinute,
                    prefab = entry.anomalyPrefab,
                    point = entry.spawnPoint,
                    room = null,
                    label = Label(entry),
                });
            }
        }

        // ── Running the timeline ─────────────────────────────────────────────────────────

        private void OnTimeChanged(float normalizedTime)
        {
            // Deferred to the first tick on purpose - see the class comment.
            if (!_built)
            {
                _built = true;
                BuildTimeline();
            }

            float elapsedMinutes = normalizedTime * nightTimer.NightDurationMinutes;

            while (_nextIndex < _sorted.Count && _sorted[_nextIndex].atMinute <= elapsedMinutes)
            {
                Spawn(_sorted[_nextIndex]);
                _nextIndex++;
            }

            if (!_allSpawnedNotified && _nextIndex >= _sorted.Count && _spawned.Count > 0)
            {
                _allSpawnedNotified = true;
                OnAllAnomaliesSpawned?.Invoke(_spawned.Count);

                if (showDebugInfo)
                    Debug.Log("AnomalyScheduler: all scheduled anomalies have spawned.", this);
            }
        }

        private void Spawn(ScheduledSpawn item)
        {
            GameObject instance = item.point != null
                ? Instantiate(item.prefab, item.point.position, item.point.rotation)
                : Instantiate(item.prefab); // keeps the position saved inside the prefab

            if (anomalyParent != null)
                instance.transform.SetParent(anomalyParent, worldPositionStays: true);

            // The room is handed over HERE, at spawn time - it is not baked into the prefab. That
            // is what lets the same anomaly kind turn up in a different room every night.
            if (item.room != null)
            {
                var anomaly = instance.GetComponentInChildren<Anomaly>(true);
                if (anomaly != null)
                    anomaly.AssignRoom(item.room);
            }

            // Runtime-spawned objects are invisible to AudioManager's scene-load sweep,
            // so hand their AudioSources (jumpscare/fight sounds) over explicitly - this
            // keeps the player's volume sliders in control of them too.
            Audio.AudioManager.RegisterHierarchy(instance);

            _spawned.Add(instance);
            OnAnomalySpawned?.Invoke(instance);

            if (showDebugInfo)
                Debug.Log($"AnomalyScheduler: spawned '{item.label}' at {instance.transform.position}.", this);
        }

        /// <summary>Spawned anomalies that still exist (nulls from destroyed ones are pruned).</summary>
        public List<GameObject> GetSpawnedAnomalies()
        {
            _spawned.RemoveAll(go => go == null);
            return new List<GameObject>(_spawned);
        }

        /// <summary>In-game clock label (e.g. "2:24 AM") for a schedule minute, given the current night duration.</summary>
        public string GameClockLabelFor(float minute)
        {
            float duration = nightTimer != null ? nightTimer.NightDurationMinutes : 5f;
            if (duration <= 0f) duration = 5f;
            float gameHours = Mathf.Clamp01(minute / duration) * NightTimer.GameHoursPerNight;
            return NightTimer.FormatGameTime(gameHours, includeSeconds: false);
        }

        private static string Label(AnomalyScheduleEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.entryName)) return entry.entryName;
            string prefabName = entry.anomalyPrefab != null ? entry.anomalyPrefab.name : "(no prefab)";
            return $"{entry.spawnAtMinute:0.##}m {prefabName}";
        }

        private void LogSchedule()
        {
            var sb = new System.Text.StringBuilder($"=== Anomaly Schedule ({source}) ===\n");
            foreach (var item in _sorted)
                sb.AppendLine($"  minute {item.atMinute,5:0.##} ({GameClockLabelFor(item.atMinute)})  {item.label}");
            Debug.Log(sb.ToString(), this);
        }

        void OnValidate()
        {
            // Auto-label entries so the Inspector list reads like a timeline.
            foreach (var entry in schedule)
            {
                if (entry != null && string.IsNullOrEmpty(entry.entryName) && entry.anomalyPrefab != null)
                    entry.entryName = $"{entry.spawnAtMinute:0.##}m - {entry.anomalyPrefab.name}";
            }
        }

        [ContextMenu("Sort Entries By Time")]
        private void SortEntriesByTime()
        {
            schedule.Sort((a, b) => a.spawnAtMinute.CompareTo(b.spawnAtMinute));
        }

        [ContextMenu("Force Spawn Next")]
        public void ForceSpawnNext()
        {
            if (_nextIndex < _sorted.Count)
            {
                Spawn(_sorted[_nextIndex]);
                _nextIndex++;
            }
            else
            {
                Debug.Log("AnomalyScheduler: no more anomalies to spawn.", this);
            }
        }

        [ContextMenu("Force Spawn All Remaining")]
        public void ForceSpawnAll()
        {
            while (_nextIndex < _sorted.Count)
            {
                Spawn(_sorted[_nextIndex]);
                _nextIndex++;
            }
        }

        void OnDrawGizmos()
        {
            // Only the manual list has positions known before Play - a generated plan picks its
            // rooms at runtime, so there is nothing to draw for it here.
            if (!showGizmos || schedule == null || source != ScheduleSource.ManualList) return;

            Gizmos.color = gizmoColor;

            foreach (var entry in schedule)
            {
                if (entry == null || !entry.IsValid()) continue;

                Vector3 pos = entry.ResolvePosition();
                Gizmos.DrawWireSphere(pos, gizmoSize);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    pos + Vector3.up * (gizmoSize + 0.2f),
                    $"m{entry.spawnAtMinute:0.##}  {Label(entry)}");
#endif
            }
        }
    }
}
