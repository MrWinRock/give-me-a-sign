using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic.SpawnAndTime
{
    public class NightTimer : MonoBehaviour
    {
        [Header("Night Timer Settings")]
        [SerializeField] private float nightDurationMinutes = 4f; // Real-time duration in minutes
        [SerializeField] private TextMeshProUGUI timeDisplayText; // Reference to UI text element
        [SerializeField] private int nextSceneIndex = 1; // Scene to load when night ends
    
        [Header("Debug Info")]
        [SerializeField] private bool showDebugInfo = false;
    
        private float _totalNightDuration; // Total duration in seconds
        private float _currentTime = 0f; // Current elapsed time in seconds
        private bool _isNightActive = true;
    
        // Events for other systems to hook into
        public System.Action<float> OnTimeChanged; // Sends normalized time (0-1)
        public System.Action OnNightEnded;
    
        void Start()
        {
            // Convert minutes to seconds
            _totalNightDuration = nightDurationMinutes * 60f;
        
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
            if (!_isNightActive) return;
        
            // Update timer
            _currentTime += Time.deltaTime;
        
            // Check if night has ended
            if (_currentTime >= _totalNightDuration)
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
            float gameTime = Mathf.Lerp(0f, 6f, _currentTime / _totalNightDuration);
            string timeString = FormatGameTime(gameTime, includeSeconds: false);
            timeDisplayText.text = timeString;

            // Debug info
            if (showDebugInfo)
            {
                Debug.Log($"Real Time: {_currentTime:F1}s / {_totalNightDuration:F1}s | Game Time: {timeString}");
            }
        }

        /// <summary>
        /// Formats a game-time value (0-6 hours) the same way this timer's own on-screen clock
        /// does, so any other UI reading from GetGameTimeHours() (e.g. the Incident Report
        /// window) renders identically and can never drift out of sync with the main HUD clock.
        /// </summary>
        public static string FormatGameTime(float gameTimeHours, bool includeSeconds = true)
        {
            int hours = Mathf.FloorToInt(gameTimeHours);
            float minutesFloat = (gameTimeHours - hours) * 60f;
            int minutes = Mathf.FloorToInt(minutesFloat);

            if (!includeSeconds)
                return $"{hours}:{minutes:00} AM";

            int seconds = Mathf.FloorToInt((minutesFloat - minutes) * 60f);
            return $"{hours}:{minutes:00}:{seconds:00} AM";
        }
    
        private void EndNight()
        {
            _isNightActive = false;
        
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
            return _currentTime / _totalNightDuration;
        }
    
        public float GetGameTimeHours()
        {
            return Mathf.Lerp(0f, 6f, GetNormalizedTime());
        }
    
        public bool IsNightActive()
        {
            return _isNightActive;
        }
    
        public float GetRemainingTime()
        {
            return _totalNightDuration - _currentTime;
        }
    
        // Method to manually end night (for testing)
        [ContextMenu("End Night Now")]
        public void ForceEndNight()
        {
            EndNight();
        }
    }
}
