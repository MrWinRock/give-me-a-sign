using GameLogic;
using GameLogic.Flow;
using GameLogic.SpawnAndTime;
using TMPro;
using UnityEngine;

namespace Score
{
    /// <summary>
    /// Keeps the running score for the night and the bar it has to clear.
    ///
    /// Listens to the global Anomaly.OnAnyAnomalyDisappeared event, so every anomaly -
    /// scene-placed or spawned at runtime by AnomalyScheduler - scores automatically with
    /// no polling and no FindObjectsOfType scans.
    ///
    /// It deliberately does NOT decide whether the night was won, save anything, or load the
    /// Result scene: <see cref="GameFlowManager"/> owns all of that. It used to do all three,
    /// which is why it had to check an "AnomalyTimeout" PlayerPrefs flag to avoid overwriting a
    /// result some other script had already written.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        [Header("Score Settings")]
        [SerializeField] private int pointsPerAnomaly = 1; // Points awarded when an anomaly disappears

        [Tooltip("Minimum score needed to win. Sprint 2 replaces this with NightPlan.requiredScore, computed from the number of anomalies actually scheduled.")]
        [SerializeField] private int winThreshold = 3;

        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI scoreText; // Optional: Display current score during gameplay
        [SerializeField] private TextMeshProUGUI thresholdText; // Optional: Display win threshold

        [Header("System References")]
        [SerializeField] private NightTimer nightTimer; // Auto-found if left empty

        [Header("Debug Settings")]
        [SerializeField] private bool showDebugInfo;

        private int _currentScore;
        private bool _scoringClosed;

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

            if (winThreshold > 0 && showDebugInfo)
                Debug.Log($"ScoreManager initialized. Win threshold: {winThreshold}");
        }

        private void HandleAnomalyDisappeared(Anomaly anomaly)
        {
            if (_scoringClosed) return;

            AddScore(pointsPerAnomaly);

            if (showDebugInfo)
                Debug.Log($"Anomaly '{anomaly.name}' disappeared! +{pointsPerAnomaly} points. Current score: {_currentScore}");
        }

        public void AddScore(int points)
        {
            if (_scoringClosed) return;

            _currentScore += points;
            OnScoreChanged?.Invoke(_currentScore);
            UpdateUI();

            if (showDebugInfo)
                Debug.Log($"Score added: +{points}. Total: {_currentScore}/{winThreshold}");
        }

        public void SubtractScore(int points)
        {
            if (_scoringClosed) return;

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

        /// <summary>
        /// The clock reached 6:00 AM. Freeze the score so nothing lands after the fact, then let
        /// GameFlowManager (called by NightTimer straight after this) read the final numbers.
        /// </summary>
        private void OnNightEnded()
        {
            if (_scoringClosed) return;
            _scoringClosed = true;

            bool gameWon = _currentScore >= winThreshold;
            OnGameEnded?.Invoke(gameWon);

            if (showDebugInfo)
                Debug.Log($"ScoreManager: scoring closed. Final Score: {_currentScore}/{winThreshold} ({(gameWon ? "WON" : "LOST")})");
        }

        // Public getter methods
        public int GetCurrentScore() => _currentScore;
        public int GetWinThreshold() => winThreshold;
        public bool IsGameWon() => _currentScore >= winThreshold;
        public bool IsGameEnded() => _scoringClosed;

        public void SetWinThreshold(int newThreshold)
        {
            winThreshold = Mathf.Max(0, newThreshold);
            UpdateUI();

            if (showDebugInfo)
                Debug.Log($"Win threshold changed to: {winThreshold}");
        }

        // Context menu methods for testing
        [ContextMenu("Add Test Score")]
        public void AddTestScore() => AddScore(1);

        [ContextMenu("Test Win Condition")]
        public void TestWinCondition()
        {
            _currentScore = winThreshold;
            UpdateUI();
            Debug.Log($"Score set to win threshold: {_currentScore}");
        }

        [ContextMenu("End Night Now (survived)")]
        public void ForceEndNight()
        {
            OnNightEnded();
            GameFlowManager.Instance?.EndNight(NightOutcome.Survived);
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
