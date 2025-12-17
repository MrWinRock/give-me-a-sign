using GameLogic;
using GameLogic.SpawnAndTime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Score
{
    /// <summary>
    /// Manages the scoring system for the night gameplay.
    /// Tracks when anomalies disappear and awards points.
    /// Determines win/lose condition based on score threshold.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        [Header("Score Settings")]
        [SerializeField] private int pointsPerAnomaly = 1; // Points awarded when an anomaly disappears
        [SerializeField] private int winThreshold = 3; // Minimum score needed to win
    
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI scoreText; // Optional: Display current score during gameplay
        [SerializeField] private TextMeshProUGUI thresholdText; // Optional: Display win threshold
    
        [Header("System References")]
        [SerializeField] private NightTimer nightTimer; // Reference to night timer
        [SerializeField] private AnomalySpawnManager spawnManager; // Reference to spawn manager
        [SerializeField] private bool autoFindReferences = true; // Auto-find references if not assigned
    
        [Header("Scene Management")]
        [SerializeField] private string resultSceneName = "ResultScene"; // Name of the result scene
        [SerializeField] private bool useSceneIndex; // Use scene index instead of name
        [SerializeField] private int resultSceneIndex = 1; // Index of result scene
    
        [Header("Debug Settings")]
        [SerializeField] private bool showDebugInfo;
    
        // Score tracking
        private int _currentScore;
        private bool _gameEnded;
    
        // Events
        public System.Action<int> OnScoreChanged; // Sends current score
        public System.Action<bool> OnGameEnded; // Sends win status (true = win, false = lose)
    
        // Singleton pattern for easy access
        public static ScoreManager Instance { get; private set; }
    
        void Awake()
        {
            // Singleton setup
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning("Multiple ScoreManager instances found! Destroying duplicate.");
                Destroy(gameObject);
            }
        }
    
        void Start()
        {
            InitializeSystem();
        
            // Start periodic check for new anomalies every 2 seconds
            InvokeRepeating(nameof(CheckForNewAnomalies), 2f, 2f);
        }
    
        private void CheckForNewAnomalies()
        {
            if (_gameEnded) return;
        
            // Find all anomaly components in the scene
            Anomaly[] allAnomalies = FindObjectsOfType<Anomaly>();
        
            foreach (Anomaly anomaly in allAnomalies)
            {
                // Check if this anomaly is already subscribed by trying to unsubscribe and resubscribe
                // This is a safe way to ensure we don't double-subscribe
                anomaly.OnAnomalyDisappeared -= OnAnomalyDisappeared;
                anomaly.OnAnomalyDisappeared += OnAnomalyDisappeared;
            }
        }
    
        private void InitializeSystem()
        {
            // Auto-find references if enabled
            if (autoFindReferences)
            {
                if (nightTimer == null)
                    nightTimer = FindObjectOfType<NightTimer>();
            
                if (spawnManager == null)
                    spawnManager = FindObjectOfType<AnomalySpawnManager>();
            }
        
            // Subscribe to night timer events
            if (nightTimer != null)
            {
                nightTimer.OnNightEnded += OnNightEnded;
            
                if (showDebugInfo)
                {
                    Debug.Log("ScoreManager: Subscribed to NightTimer events.");
                }
            }
            else
            {
                Debug.LogWarning("ScoreManager: No NightTimer reference found!");
            }
        
            // Subscribe to spawn manager events (if available)
            if (spawnManager != null)
            {
                spawnManager.OnAnomalySpawned += OnAnomalySpawned;
            
                if (showDebugInfo)
                {
                    Debug.Log("ScoreManager: Subscribed to AnomalySpawnManager events.");
                }
            }
        
            // Find and subscribe to all existing anomalies in the scene
            SubscribeToAllExistingAnomalies();
        
            // Initialize UI
            UpdateUI();
        
        // Clear any previous score data
        ClearSavedData();
        
        // Explicitly clear anomaly timeout flag to ensure clean game start
        PlayerPrefs.DeleteKey("AnomalyTimeout");
        
            if (showDebugInfo)
            {
                Debug.Log($"ScoreManager initialized. Win threshold: {winThreshold}");
            }
        }
    
        private void SubscribeToAllExistingAnomalies()
        {
            // Find all anomaly components in the scene
            Anomaly[] allAnomalies = FindObjectsOfType<Anomaly>();
        
            foreach (Anomaly anomaly in allAnomalies)
            {
                // Subscribe to each anomaly's disappear event
                anomaly.OnAnomalyDisappeared += OnAnomalyDisappeared;
            
                if (showDebugInfo)
                {
                    Debug.Log($"ScoreManager: Subscribed to existing anomaly '{anomaly.name}' disappear event.");
                }
            }
        
            if (showDebugInfo)
            {
                Debug.Log($"ScoreManager: Found and subscribed to {allAnomalies.Length} existing anomalies.");
            }
        }
    
        private void OnAnomalySpawned(GameObject anomaly, AnomalySpawnEntry entry)
        {
            // Subscribe to when this specific anomaly disappears
            Anomaly anomalyComponent = anomaly.GetComponent<Anomaly>();
            if (anomalyComponent != null)
            {
                // Subscribe to the anomaly's disappear event
                anomalyComponent.OnAnomalyDisappeared += OnAnomalyDisappeared;
            
                if (showDebugInfo)
                {
                    Debug.Log($"ScoreManager: Subscribed to {entry.entryName} disappear event.");
                }
            }
            else
            {
                Debug.LogWarning($"ScoreManager: Spawned object '{anomaly.name}' doesn't have an Anomaly component!");
            }
        }
    
        /// <summary>
        /// Call this method to manually subscribe a new anomaly to the scoring system
        /// Useful for anomalies created at runtime
        /// </summary>
        public void SubscribeToAnomaly(Anomaly anomaly)
        {
            if (anomaly != null && !_gameEnded)
            {
                anomaly.OnAnomalyDisappeared += OnAnomalyDisappeared;
            
                if (showDebugInfo)
                {
                    Debug.Log($"ScoreManager: Manually subscribed to anomaly '{anomaly.name}' disappear event.");
                }
            }
        }
    
        private void OnAnomalyDisappeared(Anomaly anomaly)
        {
            // Anomaly has disappeared - award points
            if (!_gameEnded)
            {
                AddScore(pointsPerAnomaly);
            
                if (showDebugInfo)
                {
                    Debug.Log($"Anomaly '{anomaly.name}' disappeared! +{pointsPerAnomaly} points. Current score: {_currentScore}");
                }
            }
        
            // Unsubscribe from the event to prevent memory leaks
            anomaly.OnAnomalyDisappeared -= OnAnomalyDisappeared;
        }
    
        public void AddScore(int points)
        {
            if (_gameEnded) return;
        
            _currentScore += points;
            OnScoreChanged?.Invoke(_currentScore);
            UpdateUI();
        
            if (showDebugInfo)
            {
                Debug.Log($"Score added: +{points}. Total: {_currentScore}/{winThreshold}");
            }
        }
    
        public void SubtractScore(int points)
        {
            if (_gameEnded) return;
        
            _currentScore = Mathf.Max(0, _currentScore - points);
            OnScoreChanged?.Invoke(_currentScore);
            UpdateUI();
        
            if (showDebugInfo)
            {
                Debug.Log($"Score subtracted: -{points}. Total: {_currentScore}/{winThreshold}");
            }
        }
    
        private void UpdateUI()
        {
            // Update score display
            if (scoreText != null)
            {
                scoreText.text = $"Score: {_currentScore}";
            }
        
            // Update threshold display
            if (thresholdText != null)
            {
                thresholdText.text = $"Goal: {winThreshold}";
            }
        }
    
        private void OnNightEnded()
        {
            if (_gameEnded) return;
            
            // Check if an anomaly timeout occurred - if so, don't process normal game end
            if (PlayerPrefs.GetInt("AnomalyTimeout", 0) == 1)
            {
                if (showDebugInfo)
                {
                    Debug.Log("ScoreManager: Anomaly timeout detected, skipping normal game end");
                }
                return;
            }

            if (showDebugInfo)
            {
                Debug.Log("ScoreManager: Normal night end detected, processing game end");
            }
            
            EndGame();
        }
    
        private void EndGame()
        {
            _gameEnded = true;
        
            // Determine win/lose status
            bool gameWon = _currentScore >= winThreshold;
        
            // Save score data for result scene
            SaveScoreData(gameWon);
        
            // Fire event
            OnGameEnded?.Invoke(gameWon);
        
            if (showDebugInfo)
            {
                string status = gameWon ? "WON" : "LOST";
                Debug.Log($"Game ended! Status: {status} | Final Score: {_currentScore}/{winThreshold}");
            }
        
            // Load result scene after a short delay
            Invoke(nameof(LoadResultScene), 1f);
        }
    
        private void SaveScoreData(bool gameWon)
        {
            PlayerPrefs.SetInt("FinalScore", _currentScore);
            PlayerPrefs.SetInt("GameWon", gameWon ? 1 : 0);
            PlayerPrefs.SetInt("WinThreshold", winThreshold);
            
            // Ensure anomaly timeout flag is cleared for normal game endings
            PlayerPrefs.DeleteKey("AnomalyTimeout");
            
            PlayerPrefs.Save();
        
            if (showDebugInfo)
            {
                Debug.Log($"Score data saved: Score={_currentScore}, Won={gameWon}, Threshold={winThreshold}");
            }
        }
    
        private void ClearSavedData()
        {
            PlayerPrefs.DeleteKey("FinalScore");
            PlayerPrefs.DeleteKey("GameWon");
            PlayerPrefs.DeleteKey("WinThreshold");
            PlayerPrefs.DeleteKey("AnomalyTimeout"); // Clear anomaly timeout flag to prevent persistent state
        }
    
        private void LoadResultScene()
        {
            if (useSceneIndex)
            {
                if (resultSceneIndex < SceneManager.sceneCountInBuildSettings)
                {
                    SceneManager.LoadScene(resultSceneIndex);
                }
                else
                {
                    Debug.LogError($"Result scene index {resultSceneIndex} is out of range!");
                }
            }
            else
            {
                SceneManager.LoadScene(resultSceneName);
            }
        }
    
        // Public getter methods
        public int GetCurrentScore() => _currentScore;
        public int GetWinThreshold() => winThreshold;
        public bool IsGameWon() => _currentScore >= winThreshold;
        public bool IsGameEnded() => _gameEnded;
    
        // Public setter for win threshold (can be called from other scripts)
        public void SetWinThreshold(int newThreshold)
        {
            winThreshold = Mathf.Max(0, newThreshold);
            UpdateUI();
        
            if (showDebugInfo)
            {
                Debug.Log($"Win threshold changed to: {winThreshold}");
            }
        }
    
        // Manual game end (for testing or special conditions)
        public void ForceEndGame()
        {
            if (!_gameEnded)
            {
                if (showDebugInfo)
                {
                    Debug.Log("Game manually ended!");
                }
            
                EndGame();
            }
        }
    
        // Context menu methods for testing
        [ContextMenu("Add Test Score")]
        public void AddTestScore()
        {
            AddScore(1);
        }
    
        [ContextMenu("Test Win Condition")]
        public void TestWinCondition()
        {
            _currentScore = winThreshold;
            UpdateUI();
            Debug.Log($"Score set to win threshold: {_currentScore}");
        }
        
        [ContextMenu("Test Normal Lose Condition")]
        public void TestNormalLoseCondition()
        {
            _currentScore = winThreshold - 1; // Set score below threshold
            PlayerPrefs.DeleteKey("AnomalyTimeout"); // Ensure no anomaly timeout flag
            ForceEndGame();
            Debug.Log($"Testing normal lose: Score={_currentScore}, Threshold={winThreshold}");
        }
    
        [ContextMenu("Force End Game")]
        public void ForceEndGameContextMenu()
        {
            ForceEndGame();
        }
    
        void OnDestroy()
        {
            // Unsubscribe from events
            if (nightTimer != null)
            {
                nightTimer.OnNightEnded -= OnNightEnded;
            }
        
            if (spawnManager != null)
            {
                spawnManager.OnAnomalySpawned -= OnAnomalySpawned;
            }
        
            // Clear singleton reference
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
