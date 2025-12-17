using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.SpawnAndTime
{
    [System.Serializable]
    public class AnomalySpawnEntry
    {
        [Header("Anomaly Spawn Data")]
        public GameObject anomalyPrefab;
        public Transform spawnPoint;
    
        [Header("Target Spawn Data")]
        public GameObject targetPrefab;
        public Transform targetSpawnPoint;
    
        [Header("Timing")]
        [Range(0f, 6f)]
        public float spawnGameTime = 1f; // Game time (0:00 to 6:00) when to spawn
    
        [Header("Additional Settings")]
        public bool hasSpawned = false; // Track if this entry has already spawned
        public string entryName = "Anomaly Entry"; // For organization in Inspector
    
        // Validation method
        public bool IsValid()
        {
            return anomalyPrefab != null && spawnPoint != null;
        }
    
        // Get normalized spawn time (0-1)
        public float GetNormalizedSpawnTime()
        {
            return spawnGameTime / 6f;
        }
    }

    public class AnomalySpawnList : MonoBehaviour
    {
        [Header("Anomaly Spawn Configuration")]
        [SerializeField] private List<AnomalySpawnEntry> spawnEntries = new List<AnomalySpawnEntry>();
    
        [Header("Debug Settings")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color gizmoColor = Color.red;
        [SerializeField] private float gizmoSize = 0.5f;
    
        void Start()
        {
            ValidateEntries();
        
            if (showDebugInfo)
            {
                LogSpawnSchedule();
            }
        }
    
        private void ValidateEntries()
        {
            int invalidCount = 0;
        
            for (int i = 0; i < spawnEntries.Count; i++)
            {
                if (!spawnEntries[i].IsValid())
                {
                    Debug.LogWarning($"AnomalySpawnList: Entry {i} ({spawnEntries[i].entryName}) is invalid! Missing prefab or spawn point.");
                    invalidCount++;
                }
            }
        
            if (invalidCount > 0)
            {
                Debug.LogWarning($"AnomalySpawnList: {invalidCount} invalid entries found. Please check the Inspector.");
            }
        }
    
        private void LogSpawnSchedule()
        {
            Debug.Log("=== Anomaly Spawn Schedule ===");
            var sortedEntries = GetSortedEntries();
        
            foreach (var entry in sortedEntries)
            {
                if (entry.IsValid())
                {
                    int hours = Mathf.FloorToInt(entry.spawnGameTime);
                    int minutes = Mathf.FloorToInt((entry.spawnGameTime - hours) * 60f);
                    Debug.Log($"  {hours}:{minutes:00} AM - {entry.entryName} at {entry.spawnPoint.name}");
                }
            }
        }
    
        // Public methods for AnomalySpawnManager
        public List<AnomalySpawnEntry> GetSpawnEntries()
        {
            return new List<AnomalySpawnEntry>(spawnEntries);
        }
    
        public List<AnomalySpawnEntry> GetSortedEntries()
        {
            var sorted = new List<AnomalySpawnEntry>(spawnEntries);
            sorted.Sort((a, b) => a.spawnGameTime.CompareTo(b.spawnGameTime));
            return sorted;
        }
    
        public void ResetAllEntries()
        {
            foreach (var entry in spawnEntries)
            {
                entry.hasSpawned = false;
            }
        
            if (showDebugInfo)
            {
                Debug.Log("AnomalySpawnList: All entries reset.");
            }
        }
    
        public int GetValidEntryCount()
        {
            int count = 0;
            foreach (var entry in spawnEntries)
            {
                if (entry.IsValid()) count++;
            }
            return count;
        }
    
        // Inspector helper methods
        [ContextMenu("Sort Entries by Time")]
        public void SortEntriesByTime()
        {
            spawnEntries.Sort((a, b) => a.spawnGameTime.CompareTo(b.spawnGameTime));
            Debug.Log("Spawn entries sorted by time.");
        }
    
        [ContextMenu("Add Sample Entry")]
        public void AddSampleEntry()
        {
            var newEntry = new AnomalySpawnEntry
            {
                entryName = $"Sample Entry {spawnEntries.Count + 1}",
                spawnGameTime = 1f + spawnEntries.Count * 0.5f
            };
            spawnEntries.Add(newEntry);
        }
    
        [ContextMenu("Clear All Entries")]
        public void ClearAllEntries()
        {
            spawnEntries.Clear();
            Debug.Log("All spawn entries cleared.");
        }
    
        // Gizmo visualization
        void OnDrawGizmos()
        {
            if (!showGizmos || spawnEntries == null) return;
        
            Gizmos.color = gizmoColor;
        
            foreach (var entry in spawnEntries)
            {
                if (entry.spawnPoint != null)
                {
                    // Draw sphere at spawn point
                    Gizmos.DrawWireSphere(entry.spawnPoint.position, gizmoSize);
                
                    // Draw time label
                    if (entry.IsValid())
                    {
                        int hours = Mathf.FloorToInt(entry.spawnGameTime);
                        int minutes = Mathf.FloorToInt((entry.spawnGameTime - hours) * 60f);
                    
#if UNITY_EDITOR
                        UnityEditor.Handles.Label(
                            entry.spawnPoint.position + Vector3.up * (gizmoSize + 0.2f),
                            $"{hours}:{minutes:00}"
                        );
#endif
                    }
                }
            }
        }
    }
}