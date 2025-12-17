using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.SpawnAndTime
{
    public class AnomalySpawnManager : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private NightTimer nightTimer;
        [SerializeField] private AnomalySpawnList spawnList;
    
        [Header("Spawn Management")]
        [SerializeField] private bool autoFindReferences = true;
        [SerializeField] private Transform anomalyParent; // Optional parent for spawned anomalies
        [SerializeField] private bool autoTriggerAnomalyResponse = false; // Whether to automatically call Respond() on spawned anomalies
    
        [Header("Debug Settings")]
        [SerializeField] private bool showDebugInfo = false;
        [SerializeField] private bool showSpawnEffects = false;
    
        private List<GameObject> _spawnedAnomalies = new List<GameObject>();
        private List<AnomalySpawnEntry> _scheduledEntries = new List<AnomalySpawnEntry>();
        private bool _isInitialized = false;
    
        // Events
        public System.Action<GameObject, AnomalySpawnEntry> OnAnomalySpawned;
        public System.Action<int> OnAllAnomaliesSpawned; // Sends total count
    
        void Start()
        {
            InitializeSystem();
        }
    
        private void InitializeSystem()
        {
            // Auto-find references if enabled
            if (autoFindReferences)
            {
                if (nightTimer == null)
                    nightTimer = FindObjectOfType<NightTimer>();
            
                if (spawnList == null)
                    spawnList = FindObjectOfType<AnomalySpawnList>();
            }
        
            // Validate system
            if (!ValidateSystem())
            {
                Debug.LogError("AnomalySpawnManager: System validation failed! Cannot initialize.");
                return;
            }
        
            // Setup spawn schedule
            SetupSpawnSchedule();
        
            // Subscribe to night timer events
            if (nightTimer != null)
            {
                nightTimer.OnTimeChanged += CheckForSpawns;
                nightTimer.OnNightEnded += OnNightEnded;
            }
        
            _isInitialized = true;
        
            if (showDebugInfo)
            {
                Debug.Log($"AnomalySpawnManager: Initialized with {_scheduledEntries.Count} scheduled spawns.");
            }
        }
    
        private bool ValidateSystem()
        {
            bool isValid = true;
        
            if (nightTimer == null)
            {
                Debug.LogError("AnomalySpawnManager: No NightTimer reference found!");
                isValid = false;
            }
        
            if (spawnList == null)
            {
                Debug.LogError("AnomalySpawnManager: No AnomalySpawnList reference found!");
                isValid = false;
            }
        
            return isValid;
        }
    
        private void SetupSpawnSchedule()
        {
            if (spawnList == null) return;
        
            // Get all valid entries and reset their spawn status
            _scheduledEntries = spawnList.GetSortedEntries();
            spawnList.ResetAllEntries();
        
            // Remove invalid entries
            for (int i = _scheduledEntries.Count - 1; i >= 0; i--)
            {
                if (!_scheduledEntries[i].IsValid())
                {
                    if (showDebugInfo)
                    {
                        Debug.LogWarning($"Removing invalid entry: {_scheduledEntries[i].entryName}");
                    }
                    _scheduledEntries.RemoveAt(i);
                }
            }
        
            if (showDebugInfo)
            {
                Debug.Log($"Spawn schedule setup complete. {_scheduledEntries.Count} valid entries scheduled.");
            }
        }
    
        private void CheckForSpawns(float normalizedTime)
        {
            if (!_isInitialized || _scheduledEntries == null) return;
        
            // Check each scheduled entry
            for (int i = _scheduledEntries.Count - 1; i >= 0; i--)
            {
                var entry = _scheduledEntries[i];
            
                if (!entry.hasSpawned && normalizedTime >= entry.GetNormalizedSpawnTime())
                {
                    SpawnAnomaly(entry);
                    _scheduledEntries.RemoveAt(i); // Remove from schedule after spawning
                }
            }
        
            // Check if all anomalies have been spawned
            if (_scheduledEntries.Count == 0 && _spawnedAnomalies.Count > 0)
            {
                OnAllAnomaliesSpawned?.Invoke(_spawnedAnomalies.Count);
            
                if (showDebugInfo)
                {
                    Debug.Log("All scheduled anomalies have been spawned!");
                }
            }
        }
    
        private void SpawnAnomaly(AnomalySpawnEntry entry)
        {
            if (entry.anomalyPrefab == null || entry.spawnPoint == null)
            {
                Debug.LogError($"Cannot spawn anomaly: Invalid entry {entry.entryName}");
                return;
            }
        
            // Determine spawn position and rotation
            Vector3 spawnPos = entry.spawnPoint.position;
            Quaternion spawnRot = entry.spawnPoint.rotation;
        
            // Spawn the anomaly
            GameObject spawnedAnomaly = Instantiate(entry.anomalyPrefab, spawnPos, spawnRot);
        
            // Set parent if specified
            if (anomalyParent != null)
            {
                spawnedAnomaly.transform.SetParent(anomalyParent);
            }
        
            // Add to tracking list
            _spawnedAnomalies.Add(spawnedAnomaly);
        
            // Mark as spawned
            entry.hasSpawned = true;
        
            // Optionally trigger anomaly response if enabled
            if (autoTriggerAnomalyResponse)
            {
                Anomaly anomalyComponent = spawnedAnomaly.GetComponent<Anomaly>();
                if (anomalyComponent != null)
                {
                    anomalyComponent.Respond();
                
                    if (showDebugInfo)
                    {
                        Debug.Log($"Auto-triggered response for {entry.entryName}");
                    }
                }
            }
        
            // Show spawn effects if enabled
            if (showSpawnEffects)
            {
                StartCoroutine(ShowSpawnEffect(spawnPos));
            }
        
            // Fire event
            OnAnomalySpawned?.Invoke(spawnedAnomaly, entry);
        
            if (showDebugInfo)
            {
                int hours = Mathf.FloorToInt(entry.spawnGameTime);
                int minutes = Mathf.FloorToInt((entry.spawnGameTime - hours) * 60f);
                Debug.Log($"Spawned {entry.entryName} at {hours}:{minutes:00} AM (Position: {spawnPos})");
            }
        }
    
        private IEnumerator ShowSpawnEffect(Vector3 position)
        {
            // Simple visual effect - you can replace this with particle effects
            GameObject effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effect.transform.position = position;
            effect.transform.localScale = Vector3.one * 0.2f;
        
            Renderer renderer = effect.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = Color.red;
            }
        
            // Animate the effect
            float duration = 1f;
            float elapsed = 0f;
            Vector3 startScale = effect.transform.localScale;
        
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
            
                effect.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);
            
                if (renderer != null)
                {
                    Color color = renderer.material.color;
                    color.a = 1f - progress;
                    renderer.material.color = color;
                }
            
                yield return null;
            }
        
            Destroy(effect);
        }
    
        private void OnNightEnded()
        {
            if (showDebugInfo)
            {
                Debug.Log($"Night ended. Spawned {_spawnedAnomalies.Count} anomalies total.");
            }
        
            // Clean up spawned anomalies if needed
            CleanupSpawnedAnomalies();
        }
    
        private void CleanupSpawnedAnomalies()
        {
            // Remove null references (in case anomalies were destroyed)
            for (int i = _spawnedAnomalies.Count - 1; i >= 0; i--)
            {
                if (_spawnedAnomalies[i] == null)
                {
                    _spawnedAnomalies.RemoveAt(i);
                }
            }
        }
    
        // Public methods
        public int GetSpawnedAnomalyCount()
        {
            CleanupSpawnedAnomalies();
            return _spawnedAnomalies.Count;
        }
    
        public List<GameObject> GetSpawnedAnomalies()
        {
            CleanupSpawnedAnomalies();
            return new List<GameObject>(_spawnedAnomalies);
        }
    
        public int GetRemainingSpawns()
        {
            return _scheduledEntries != null ? _scheduledEntries.Count : 0;
        }
    
        public bool AreAllAnomaliesSpawned()
        {
            return GetRemainingSpawns() == 0;
        }
    
        // Manual anomaly response triggering
        public void TriggerResponseForAnomaly(GameObject anomaly)
        {
            if (anomaly != null)
            {
                Anomaly anomalyComponent = anomaly.GetComponent<Anomaly>();
                if (anomalyComponent != null)
                {
                    anomalyComponent.Respond();
                
                    if (showDebugInfo)
                    {
                        Debug.Log($"Manually triggered response for {anomaly.name}");
                    }
                }
            }
        }
    
        public void TriggerResponseForAllSpawnedAnomalies()
        {
            CleanupSpawnedAnomalies();
        
            foreach (GameObject anomaly in _spawnedAnomalies)
            {
                TriggerResponseForAnomaly(anomaly);
            }
        
            if (showDebugInfo)
            {
                Debug.Log($"Triggered response for all {_spawnedAnomalies.Count} spawned anomalies");
            }
        }
    
        // Manual spawn for testing
        [ContextMenu("Force Spawn Next Anomaly")]
        public void ForceSpawnNext()
        {
            if (_scheduledEntries != null && _scheduledEntries.Count > 0)
            {
                SpawnAnomaly(_scheduledEntries[0]);
                _scheduledEntries.RemoveAt(0);
            }
            else
            {
                Debug.Log("No more anomalies to spawn.");
            }
        }
    
        [ContextMenu("Force Spawn All Remaining")]
        public void ForceSpawnAll()
        {
            if (_scheduledEntries != null)
            {
                while (_scheduledEntries.Count > 0)
                {
                    SpawnAnomaly(_scheduledEntries[0]);
                    _scheduledEntries.RemoveAt(0);
                }
            }
        }
    
        [ContextMenu("Trigger Response for All Spawned Anomalies")]
        public void TriggerResponseForAllSpawnedAnomaliesContextMenu()
        {
            TriggerResponseForAllSpawnedAnomalies();
        }
    
        void OnDestroy()
        {
            // Unsubscribe from events
            if (nightTimer != null)
            {
                nightTimer.OnTimeChanged -= CheckForSpawns;
                nightTimer.OnNightEnded -= OnNightEnded;
            }
        }
    }
}
