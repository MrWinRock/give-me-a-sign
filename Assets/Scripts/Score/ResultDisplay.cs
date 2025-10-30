using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Score
{
    /// <summary>
    /// Simple result display for Scene 2. Shows final score and win/lose status.
    /// </summary>
    public class ResultDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI thresholdText; // Shows what score was needed to win
    
        [Header("Status Colors")]
        [SerializeField] private Color winColor = Color.green;
        [SerializeField] private Color loseColor = Color.red;
    
        [Header("Buttons")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button quitButton;
    
    [Header("Scene Names")]
    [SerializeField] private string gameSceneName = "GameScene"; // Scene 1 (Night gameplay)

    [Header("Anomaly Defeat Objects")]
    [SerializeField] private GameObject[] anomalyDefeatObjects; // Objects to activate when defeated by anomaly
    [SerializeField] private GameObject[] normalResultObjects; // Objects to activate for normal results

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo;
    
        void Start()
        {
            LoadAndDisplayResults();
            SetupButtons();
        }
    
        private void LoadAndDisplayResults()
        {
            // Check if game ended due to anomaly timeout
            bool anomalyTimeout = PlayerPrefs.GetInt("AnomalyTimeout", 0) == 1;
            
            if (anomalyTimeout)
            {
                // Special handling for anomaly timeout - show defeat message
                if (statusText != null)
                {
                    statusText.text = "YOU LOSE!";
                    statusText.color = loseColor;
                }
                
                if (scoreText != null)
                {
                    scoreText.text = "You were consumed by the darkness...";
                }
                
                if (thresholdText != null)
                {
                    thresholdText.text = "?????????????????????????";
                }
                
                // Activate anomaly defeat objects
                ActivateAnomalyDefeatObjects();
                DeactivateNormalResultObjects();
                
                if (showDebugInfo)
                {
                    Debug.Log("Results: Anomaly timeout defeat displayed with special objects activated");
                }
                
                return;
            }
            
            // Normal game ending - activate normal result objects
            ActivateNormalResultObjects();
            DeactivateAnomalyDefeatObjects();
            
            // Normal game ending - load saved score data from Scene 1
            int finalScore = PlayerPrefs.GetInt("FinalScore", 0);
            bool gameWon = PlayerPrefs.GetInt("GameWon", 0) == 1;
            int winThreshold = PlayerPrefs.GetInt("WinThreshold", 3);
        
            // Display final score
            if (scoreText != null)
            {
                scoreText.text = $"Final Score: {finalScore}";
            }
        
            // Display game status
            if (statusText != null)
            {
                if (gameWon)
                {
                    statusText.text = "YOU WIN!";
                    statusText.color = winColor;
                }
                else
                {
                    statusText.text = "YOU LOSE!";
                    statusText.color = loseColor;
                }
            }
        
            // Display threshold info
            if (thresholdText != null)
            {
                thresholdText.text = $"(Need {winThreshold} points to win)";
            }
        
            if (showDebugInfo)
            {
                Debug.Log($"Results loaded - Score: {finalScore}, Won: {gameWon}, Threshold: {winThreshold}");
            }
        }
    
        private void SetupButtons()
        {
            // Setup play again button
            if (playAgainButton != null)
            {
                playAgainButton.onClick.AddListener(PlayAgain);
            }
        
            // Setup quit button
            if (quitButton != null)
            {
                quitButton.onClick.AddListener(QuitGame);
            }
        }
    
        public void PlayAgain()
        {
            if (showDebugInfo)
            {
                Debug.Log("Loading game scene...");
            }
        
            // Clear saved score data for fresh start
            PlayerPrefs.DeleteKey("FinalScore");
            PlayerPrefs.DeleteKey("GameWon");
            PlayerPrefs.DeleteKey("WinThreshold");
            PlayerPrefs.DeleteKey("AnomalyTimeout"); // Clear anomaly timeout flag
        
            // Load Scene 1 (gameplay)
            SceneManager.LoadScene(gameSceneName);
        }
    
        public void QuitGame()
        {
            if (showDebugInfo)
            {
                Debug.Log("Quitting game...");
            }
        
            // Quit application
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        
        private void ActivateAnomalyDefeatObjects()
        {
            foreach (GameObject obj in anomalyDefeatObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"Activated anomaly defeat object: {obj.name}");
                    }
                }
            }
        }
        
        private void DeactivateAnomalyDefeatObjects()
        {
            foreach (GameObject obj in anomalyDefeatObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
        
        private void ActivateNormalResultObjects()
        {
            foreach (GameObject obj in normalResultObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"Activated normal result object: {obj.name}");
                    }
                }
            }
        }
        
        private void DeactivateNormalResultObjects()
        {
            foreach (GameObject obj in normalResultObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
    
        // Context menu for testing without Scene 1
        [ContextMenu("Test Win Result")]
        public void TestWinResult()
        {
            PlayerPrefs.SetInt("FinalScore", 5);
            PlayerPrefs.SetInt("GameWon", 1);
            PlayerPrefs.SetInt("WinThreshold", 3);
            LoadAndDisplayResults();
        }
    
        [ContextMenu("Test Lose Result")]
        public void TestLoseResult()
        {
            PlayerPrefs.SetInt("FinalScore", 1);
            PlayerPrefs.SetInt("GameWon", 0);
            PlayerPrefs.SetInt("WinThreshold", 3);
            PlayerPrefs.DeleteKey("AnomalyTimeout");
            LoadAndDisplayResults();
        }
        
        [ContextMenu("Test Anomaly Defeat")]
        public void TestAnomalyDefeat()
        {
            PlayerPrefs.SetInt("AnomalyTimeout", 1);
            PlayerPrefs.SetInt("FinalScore", 0);
            PlayerPrefs.SetInt("GameWon", 0);
            LoadAndDisplayResults();
        }
    }
}
