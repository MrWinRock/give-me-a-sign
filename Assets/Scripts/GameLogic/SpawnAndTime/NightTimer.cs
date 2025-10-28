using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class NightTimer : MonoBehaviour
{
    [Header("Night Timer Settings")]
    [SerializeField] private float nightDurationMinutes = 4f; // Real-time duration in minutes
    [SerializeField] private TextMeshProUGUI timeDisplayText; // Reference to UI text element
    [SerializeField] private int nextSceneIndex = 1; // Scene to load when night ends
    
    [Header("Debug Info")]
    [SerializeField] private bool showDebugInfo = false;
    
    private float totalNightDuration; // Total duration in seconds
    private float currentTime = 0f; // Current elapsed time in seconds
    private bool isNightActive = true;
    
    // Events for other systems to hook into
    public System.Action<float> OnTimeChanged; // Sends normalized time (0-1)
    public System.Action OnNightEnded;
    
    void Start()
    {
        // Convert minutes to seconds
        totalNightDuration = nightDurationMinutes * 60f;
        
        // Validate references
        if (timeDisplayText == null)
        {
            Debug.LogError("NightTimer: No TextMeshPro reference assigned! Please assign timeDisplayText in the Inspector.");
        }
        
        // Initialize display
        UpdateTimeDisplay();
    }
    
    void Update()
    {
        if (!isNightActive) return;
        
        // Update timer
        currentTime += Time.deltaTime;
        
        // Check if night has ended
        if (currentTime >= totalNightDuration)
        {
            EndNight();
            return;
        }
        
        // Update display and notify listeners
        UpdateTimeDisplay();
        OnTimeChanged?.Invoke(GetNormalizedTime());
    }
    
    private void UpdateTimeDisplay()
    {
        if (timeDisplayText == null) return;
        
        // Map real time (0 to totalDuration) to game time (0:00 to 6:00)
        float gameTime = Mathf.Lerp(0f, 6f, currentTime / totalNightDuration);
        
        // Convert to hours and minutes
        int hours = Mathf.FloorToInt(gameTime);
        int minutes = Mathf.FloorToInt((gameTime - hours) * 60f);
        
        // Format as HH:MM
        string timeString = string.Format("{0}:{1:00} AM", hours, minutes);
        timeDisplayText.text = timeString;
        
        // Debug info
        if (showDebugInfo)
        {
            Debug.Log($"Real Time: {currentTime:F1}s / {totalNightDuration:F1}s | Game Time: {timeString}");
        }
    }
    
    private void EndNight()
    {
        isNightActive = false;
        
        if (showDebugInfo)
        {
            Debug.Log("Night ended! Loading next scene...");
        }
        
        // Notify listeners
        OnNightEnded?.Invoke();
        
        // Load next scene
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.LogWarning($"Next scene index {nextSceneIndex} is out of range! Scene count: {SceneManager.sceneCountInBuildSettings}");
        }
    }
    
    // Public methods for external access
    public float GetNormalizedTime()
    {
        return currentTime / totalNightDuration;
    }
    
    public float GetGameTimeHours()
    {
        return Mathf.Lerp(0f, 6f, GetNormalizedTime());
    }
    
    public bool IsNightActive()
    {
        return isNightActive;
    }
    
    public float GetRemainingTime()
    {
        return totalNightDuration - currentTime;
    }
    
    // Method to manually end night (for testing)
    [ContextMenu("End Night Now")]
    public void ForceEndNight()
    {
        EndNight();
    }
}
