using GameLogic.Flow;
using Report;
using TMPro;
using UnityEngine;

namespace GameLogic.SpawnAndTime
{
    /// <summary>
    /// FNAF-style night clock. Real time (default 5 minutes) is mapped onto an in-game
    /// clock that runs 0:00 AM -> 6:00 AM, then the Result scene is loaded.
    /// </summary>
    public class NightTimer : MonoBehaviour
    {
        public const float GameHoursPerNight = 6f;

        [Header("Night Timer Settings")]
        [Tooltip("How long the whole night lasts in REAL minutes. The in-game clock always shows 0:00-6:00 AM regardless of this value.")]
        [Min(0.1f)]
        [SerializeField] private float nightDurationMinutes = 5f;
        [SerializeField] private TextMeshProUGUI timeDisplayText;

        [Header("Debug Info")]
        [SerializeField] private bool showDebugInfo = false;

        private float _totalNightDuration; // seconds
        private float _currentTime;        // elapsed seconds
        private bool _isNightActive = true;
        private int _lastDisplayedMinute = -1; // avoid rebuilding the TMP string every frame

        public System.Action<float> OnTimeChanged;
        public System.Action OnNightEnded;

        public float NightDurationMinutes => nightDurationMinutes;

        public float ElapsedMinutes => _currentTime / 60f;

        public void SetNightDuration(float minutes)
        {
            if (minutes <= 0f) return;

            nightDurationMinutes = minutes;
            _totalNightDuration = minutes * 60f;

            // The clock label is a function of the duration, so a mid-flight change has to
            // invalidate the cached minute or the HUD would keep showing the old mapping.
            _lastDisplayedMinute = -1;
            UpdateTimeDisplay();
        }

        void Start()
        {
            _totalNightDuration = nightDurationMinutes * 60f;

            if (timeDisplayText == null)
                Debug.LogError("NightTimer: No TextMeshPro reference assigned! Please assign timeDisplayText in the Inspector.");

            UpdateTimeDisplay();
        }

        void Update()
        {
            if (!_isNightActive) return;

            // Clamp instead of letting it run past the cap, so the clock can sit at exactly
            // 6:00 AM for as long as we end up waiting below.
            _currentTime = Mathf.Min(_currentTime + Time.deltaTime, _totalNightDuration);

            if (_currentTime >= _totalNightDuration)
            {
                // Don't yank the scene out from under the player mid-report (e.g. still holding
                // the mic button) - finish that first, then end the night the moment it closes.
                if (IncidentReportManager.Instance != null && IncidentReportManager.Instance.IsReportOpen)
                {
                    UpdateTimeDisplay();
                    return;
                }

                EndNight();
                return;
            }

            UpdateTimeDisplay();
            OnTimeChanged?.Invoke(GetNormalizedTime());
        }

        private void UpdateTimeDisplay()
        {
            if (timeDisplayText == null) return;

            float gameTime = GetGameTimeHours();

            // The HUD clock only shows hours:minutes, so only rebuild the string
            // (and re-layout the TMP mesh) when the displayed minute actually changes.
            int displayedMinute = Mathf.FloorToInt(gameTime * 60f);
            if (displayedMinute == _lastDisplayedMinute) return;
            _lastDisplayedMinute = displayedMinute;

            string timeString = FormatGameTime(gameTime, includeSeconds: false);
            timeDisplayText.text = timeString;

            if (showDebugInfo)
                Debug.Log($"Real Time: {_currentTime:F1}s / {_totalNightDuration:F1}s | Game Time: {timeString}");
        }

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
                Debug.Log("Night ended! Handing over to GameFlowManager.");

            // Listeners first (ScoreManager freezes the score here), then hand the outcome to
            // the one system that decides what a finished night means and loads the Result
            // scene. This timer no longer touches SceneManager itself.
            OnNightEnded?.Invoke();

            GameFlowManager.Instance?.EndNight(NightOutcome.Survived);
        }

        public float GetNormalizedTime()
        {
            return _totalNightDuration > 0f ? _currentTime / _totalNightDuration : 0f;
        }

        public float GetGameTimeHours()
        {
            return GetNormalizedTime() * GameHoursPerNight;
        }

        public bool IsNightActive()
        {
            return _isNightActive;
        }

        public float GetRemainingTime()
        {
            return _totalNightDuration - _currentTime;
        }

        [ContextMenu("End Night Now")]
        public void ForceEndNight()
        {
            EndNight();
        }
    }
}
