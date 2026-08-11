using System.Collections.Generic;
using GameLogic.Data;
using GameLogic.Night;
using UnityEngine;

// Aliased, not imported: AnomalyScheduleEntry below uses UnityEngine's [Min], and a plain
// `using Gaskellgames;` would make the simple name ambiguous (CS0104).
// See CLAUDE.md - "Gaskellgames" for the project-wide rule.
using GG = Gaskellgames;

namespace GameLogic.SpawnAndTime
{
    /// <summary>Where an AnomalyScheduler gets its timeline from.</summary>
    public enum ScheduleSource
    {
        NightPlan,
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

        public Vector3 ResolvePosition()
        {
            if (spawnPoint != null) return spawnPoint.position;
            return anomalyPrefab != null ? anomalyPrefab.transform.position : Vector3.zero;
        }
    }

    /// <summary>
    /// Spawns anomalies on a minute-based timeline, normally the one in the night's
    /// <see cref="NightPlan"/>.
    /// </summary>
    public class AnomalyScheduler : MonoBehaviour
    {
        // AnomalySchedulerEditor overrides Gaskellgames' global editor, so [Button] won't draw here
        // - the debug actions below stay on [ContextMenu].
        [Header("Source")]
        [GG.InfoBox("Schedule below is IGNORED in NightPlan mode - the timeline comes from the generated night.",
                    GG.InfoMessageType.Info, nameof(source), (int)ScheduleSource.NightPlan)]
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

        // Distinct prefabs drawn from this night's own timeline, used by SpawnPenaltyAnomaly -
        // built lazily the first time a penalty spawn is needed.
        private readonly List<GameObject> _penaltyPool = new List<GameObject>();
        private bool _penaltyPoolBuilt;

        // Seeded from the plan so which spawn point a room picks is part of the night's seed too -
        // otherwise replaying a seed would put anomalies in subtly different places.
        private System.Random _rng = new System.Random(0);

        public System.Action<GameObject> OnAnomalySpawned;
        public System.Action<int> OnAllAnomaliesSpawned;

        public static AnomalyScheduler Instance { get; private set; }

        public int RemainingCount => _sorted.Count - _nextIndex;

        public int TotalSpawned => _spawned.Count;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Debug.LogWarning("Multiple AnomalyScheduler instances found! Destroying duplicate.", this);
                Destroy(gameObject);
            }
        }

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

            if (Instance == this)
                Instance = null;
        }

        // ── Building the timeline ────────────────────────────────────────────────────────

        private void BuildTimeline()
        {
            // Owned here rather than by the caller, so a penalty spawn arriving before the first
            // timer tick can safely build the timeline without OnTimeChanged then rebuilding it -
            // a rebuild resets the cursor and would re-spawn everything already on screen.
            _built = true;

            _sorted.Clear();
            _nextIndex = 0;
            _allSpawnedNotified = false;

            // The pool is a view onto _sorted, so it has to be invalidated with it.
            _penaltyPoolBuilt = false;

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
                BuildTimeline();

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

        // ── Penalty spawns ───────────────────────────────────────────────────────────────

        public int SpawnPenaltyAnomalies()
        {
            int count = NightPlanProvider.HasPlan
                ? Mathf.Max(0, NightPlanProvider.Current.penaltyAnomaliesPerWrongReport)
                : 1;

            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                if (SpawnPenaltyAnomaly() != null) spawned++;
            }

            if (showDebugInfo && count > 0)
                Debug.Log($"AnomalyScheduler: wrong report cost {spawned}/{count} penalty anomalies.", this);

            return spawned;
        }

        public GameObject SpawnPenaltyAnomaly()
        {
            if (!_built)
                BuildTimeline();

            if (!_penaltyPoolBuilt)
                BuildPenaltyPool();

            if (_penaltyPool.Count == 0)
            {
                if (showDebugInfo)
                    Debug.LogWarning("AnomalyScheduler: no anomaly prefab available to spawn as a penalty.", this);
                return null;
            }

            var prefab = _penaltyPool[_rng.Next(_penaltyPool.Count)];
            var anchor = RoomRegistry.Count > 0 ? RoomRegistry.All[_rng.Next(RoomRegistry.Count)] : null;

            var spawn = new ScheduledSpawn
            {
                atMinute = -1f,
                prefab = prefab,
                point = anchor != null ? anchor.GetSpawnPoint(_rng) : null,
                room = anchor != null ? anchor.Room : null,
                label = $"PENALTY: {prefab.name} in {(anchor != null ? anchor.Room.Label : "(no room)")}",
            };

            Spawn(spawn);

            if (showDebugInfo)
                Debug.Log($"AnomalyScheduler: spawned penalty anomaly '{spawn.label}'.", this);

            return _spawned[_spawned.Count - 1];
        }

        private void BuildPenaltyPool()
        {
            _penaltyPoolBuilt = true;
            _penaltyPool.Clear();

            foreach (var item in _sorted)
            {
                if (item.prefab == null || _penaltyPool.Contains(item.prefab)) continue;
                if (item.prefab.GetComponentInChildren<DemonAnomaly>(true) != null) continue;

                _penaltyPool.Add(item.prefab);
            }
        }

        public List<GameObject> GetSpawnedAnomalies()
        {
            _spawned.RemoveAll(go => go == null);
            return new List<GameObject>(_spawned);
        }

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
