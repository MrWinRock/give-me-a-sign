using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.SpawnAndTime
{
    /// <summary>
    /// One timeline entry: which anomaly appears, at which REAL minute of the night,
    /// and where. Leave Spawn Point empty to use the position saved inside the prefab.
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

        public bool IsValid()
        {
            return anomalyPrefab != null;
        }

        /// <summary>Where this entry will actually spawn (spawn point override, else the prefab's own position).</summary>
        public Vector3 ResolvePosition()
        {
            if (spawnPoint != null) return spawnPoint.position;
            return anomalyPrefab != null ? anomalyPrefab.transform.position : Vector3.zero;
        }
    }

    /// <summary>
    /// Spawns anomalies on a simple minute-based timeline.
    ///
    /// How to use:
    ///   1. Add entries to the Schedule list: pick a prefab and type the minute it should appear.
    ///   2. (Optional) Drag a scene Transform into Spawn Point to override the position.
    ///   3. Press Play - entries fire in time order. Gizmos in the Scene view show
    ///      each spawn position labelled with its scheduled time.
    ///
    /// Entries are pre-sorted once at startup and consumed with a single index cursor,
    /// so the per-frame cost is one float comparison - no list scans or allocations.
    /// </summary>
    public class AnomalyScheduler : MonoBehaviour
    {
        [Header("Schedule (real minutes into the night)")]
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

        // Sorted copy of the valid schedule entries; _nextIndex walks it forward as time passes.
        private readonly List<AnomalyScheduleEntry> _sorted = new List<AnomalyScheduleEntry>();
        private int _nextIndex;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private bool _allSpawnedNotified;

        /// <summary>Fired right after an anomaly is instantiated.</summary>
        public System.Action<GameObject, AnomalyScheduleEntry> OnAnomalySpawned;
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
            _allSpawnedNotified = false;

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

                _sorted.Add(entry);
            }

            _sorted.Sort((a, b) => a.spawnAtMinute.CompareTo(b.spawnAtMinute));
        }

        private void OnTimeChanged(float normalizedTime)
        {
            float elapsedMinutes = normalizedTime * nightTimer.NightDurationMinutes;

            while (_nextIndex < _sorted.Count && _sorted[_nextIndex].spawnAtMinute <= elapsedMinutes)
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

        private void Spawn(AnomalyScheduleEntry entry)
        {
            GameObject instance = entry.spawnPoint != null
                ? Instantiate(entry.anomalyPrefab, entry.spawnPoint.position, entry.spawnPoint.rotation)
                : Instantiate(entry.anomalyPrefab); // keeps the position saved inside the prefab

            if (anomalyParent != null)
                instance.transform.SetParent(anomalyParent, worldPositionStays: true);

            // Runtime-spawned objects are invisible to AudioManager's scene-load sweep,
            // so hand their AudioSources (jumpscare/fight sounds) over explicitly - this
            // keeps the player's volume sliders in control of them too.
            Audio.AudioManager.RegisterHierarchy(instance);

            _spawned.Add(instance);
            OnAnomalySpawned?.Invoke(instance, entry);

            if (showDebugInfo)
                Debug.Log($"AnomalyScheduler: spawned '{Label(entry)}' at {instance.transform.position}.", this);
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
            var sb = new System.Text.StringBuilder("=== Anomaly Schedule ===\n");
            foreach (var entry in _sorted)
                sb.AppendLine($"  minute {entry.spawnAtMinute,5:0.##} ({GameClockLabelFor(entry.spawnAtMinute)})  {Label(entry)}");
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
            if (!showGizmos || schedule == null) return;

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
