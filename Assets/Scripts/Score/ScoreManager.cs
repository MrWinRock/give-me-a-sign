using GameLogic;
using GameLogic.SpawnAndTime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Score
{
    /// <summary>
    /// Manages the scoring system for the night gameplay.
    /// Listens to the global Anomaly.OnAnyAnomalyDisappeared event, so every anomaly -
    /// scene-placed or spawned at runtime by AnomalyScheduler - scores automatically with
    /// no polling and no FindObjectsOfType scans.
    /// Determines win/lose at 6:00 AM and hands the result to the Result scene via PlayerPrefs.
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
        [SerializeField] private NightTimer nightTimer; // Auto-found if left empty

        [Header("Scene Management")]
        [SerializeField] private string resultSceneName = "ResultScene"; // Name of the result scene
        [SerializeField] private bool useSceneIndex; // Use scene index instead of name
        [SerializeField] private int resultSceneIndex = 1; // Index of result scene

        [Header("Debug Settings")]
        [SerializeField] private bool showDebugInfo;

        private int _currentScore;
        private bool _gameEnded;

        public System.Action<int> OnScoreChanged; // Sends current score
        public System.Action<bool> OnGameEnded; // Sends win status (true = win, false = lose)

        public static ScoreManager Instance { get; private set; }

        void Awake()
        {
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

        void OnEnable()
        {
            Anomaly.OnAnyAnomalyDisappeared += HandleAnomalyDisappeared;
        }

        void OnDisable()
        {
            Anomaly.OnAnyAnomalyDisappeared -= HandleAnomalyDisappeared;
        }

        void Start()
        {
            if (nightTimer == null)
                nightTimer = FindFirstObjectByType<NightTimer>();

            if (nightTimer != null)
                nightTimer.OnNightEnded += OnNightEnded;
            else
                Debug.LogWarning("ScoreManager: No NightTimer reference found!");

            UpdateUI();

            // Clear any previous run's result data so the Result scene never reads stale values.
            ClearSavedData();

            if (showDebugInfo)
                Debug.Log($"ScoreManager initialized. Win threshold: {winThreshold}");
        }

        private void HandleAnomalyDisappeared(Anomaly anomaly)
        {
            if (_gameEnded) return;

            AddScore(pointsPerAnomaly);

            if (showDebugInfo)
                Debug.Log($"Anomaly '{anomaly.name}' disappeared! +{pointsPerAnomaly} points. Current score: {_currentScore}");
        }

        public void AddScore(int points)
        {
            if (_gameEnded) return;

            _currentScore += points;
            OnScoreChanged?.Invoke(_currentScore);
            UpdateUI();

            if (showDebugInfo)
                Debug.Log($"Score added: +{points}. Total: {_currentScore}/{winThreshold}");
        }

        public void SubtractScore(int points)
        {
            if (_gameEnded) return;

            _currentScore = Mathf.Max(0, _currentScore - points);
            OnScoreChanged?.Invoke(_currentScore);
            UpdateUI();

            if (showDebugInfo)
                Debug.Log($"Score subtracted: -{points}. Total: {_currentScore}/{winThreshold}");
        }

        private void UpdateUI()
        {
            if (scoreText != null)
                scoreText.text = $"Score: {_currentScore}";

            if (thresholdText != null)
                thresholdText.text = $"Goal: {winThreshold}";
        }

        private void OnNightEnded()
        {
            if (_gameEnded) return;

            // An anomaly timeout loss already saved its own result and is loading the Result
            // scene itself - don't overwrite that with a normal night-end result.
            if (PlayerPrefs.GetInt("AnomalyTimeout", 0) == 1)
            {
                if (showDebugInfo)
                    Debug.Log("ScoreManager: Anomaly timeout detected, skipping normal game end");
                return;
            }

            if (showDebugInfo)
                Debug.Log("ScoreManager: Normal night end detected, processing game end");

            EndGame();
        }

        private void EndGame()
        {
            _gameEnded = true;

            bool gameWon = _currentScore >= winThreshold;

            SaveScoreData(gameWon);
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
                Debug.Log($"Score data saved: Score={_currentScore}, Won={gameWon}, Threshold={winThreshold}");
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
                    SceneManager.LoadScene(resultSceneIndex);
                else
                    Debug.LogError($"Result scene index {resultSceneIndex} is out of range!");
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

        public void SetWinThreshold(int newThreshold)
        {
            winThreshold = Mathf.Max(0, newThreshold);
            UpdateUI();

            if (showDebugInfo)
                Debug.Log($"Win threshold changed to: {winThreshold}");
        }

        public void ForceEndGame()
        {
            if (!_gameEnded)
            {
                if (showDebugInfo)
                    Debug.Log("Game manually ended!");

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
            if (nightTimer != null)
                nightTimer.OnNightEnded -= OnNightEnded;

            if (Instance == this)
                Instance = null;
        }
    }
}
