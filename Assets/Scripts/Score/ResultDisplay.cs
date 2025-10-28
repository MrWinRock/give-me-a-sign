using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

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
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    
    void Start()
    {
        LoadAndDisplayResults();
        SetupButtons();
    }
    
    private void LoadAndDisplayResults()
    {
        // Load saved score data from Scene 1
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
        LoadAndDisplayResults();
    }
}
