using System;
using System.Collections.Generic;
using GameLogic;
using Pray;
using Score;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// Receives recognized speech (from WhisperMicInput) while the prayer panel is open and
    /// banishes the active anomaly when enough words of the target prayer are heard.
    /// Word matching lives in <see cref="PhraseMatcher"/>, shared with SignRequestSystem.
    /// </summary>
    public class VoiceCommandRouter : MonoBehaviour
    {
        [Header("Prayer System")]
        public PrayUiManager prayUiManager;
        public ScoreManager scoreManager;

        [Header("Prayer Detection")]
        public string targetPrayer = "In the name of the father son and holy spirit";
        [Tooltip("How many words of the target prayer must be heard for the prayer to count.")]
        [Range(1, 10)] public int minimumWordsRequired = 5;
        [Tooltip("Per-word fuzzy similarity threshold (1 = exact match only).")]
        [Range(0.5f, 1f)] public float wordSimilarity = 0.7f;
        public int pointsForSuccessfulPrayer = 1;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        /// <summary>Fired on every prayer attempt: true = success, false = failed.</summary>
        public Action<bool> OnPrayerAttempted;

        public void Route(string recognizedText)
        {
            if (string.IsNullOrWhiteSpace(recognizedText)) return;
            if (!IsPrayPanelActive()) return;

            string text = recognizedText.Trim();
            bool prayerSuccess = CheckPrayerMatch(text);
            OnPrayerAttempted?.Invoke(prayerSuccess);

            if (prayerSuccess)
                HandleSuccessfulPrayer();
        }

        private bool IsPrayPanelActive()
        {
            return prayUiManager != null &&
                   prayUiManager.gameObject.activeInHierarchy &&
                   prayUiManager.IsPrayPanelActive();
        }

        private bool CheckPrayerMatch(string recognizedText)
        {
            List<string> foundWords = showDebugInfo ? new List<string>() : null;
            int matchingWords = PhraseMatcher.CountMatchingWords(recognizedText, targetPrayer, wordSimilarity, foundWords);
            bool isMatch = matchingWords >= minimumWordsRequired;

            if (showDebugInfo)
            {
                Debug.Log($"Prayer match: '{recognizedText}' vs '{targetPrayer}' - " +
                          $"{matchingWords} words matched (need {minimumWordsRequired}) -> {isMatch}. " +
                          $"Found: [{string.Join(", ", foundWords)}]");
            }

            return isMatch;
        }

        private void HandleSuccessfulPrayer()
        {
            // Copy the list: OnPrayerSuccessful() deactivates anomalies, which mutates ActiveAnomalies.
            var anomalies = new List<Anomaly>(Anomaly.ActiveAnomalies);
            bool anomalyBanished = false;

            foreach (Anomaly anomaly in anomalies)
            {
                if (anomaly != null && anomaly.CanBePrayerBanished())
                {
                    anomaly.OnPrayerSuccessful();
                    anomalyBanished = true;

                    if (showDebugInfo)
                        Debug.Log($"Anomaly '{anomaly.name}' banished by prayer!");
                }
            }

            if (anomalyBanished && scoreManager != null)
                scoreManager.AddScore(pointsForSuccessfulPrayer);
        }
    }
}
