using UnityEngine;
using System;
using System.Collections.Generic;
using GameLogic;
using Pray;
using Score;

namespace Whisper
{
    public class VoiceCommandRouter : MonoBehaviour
    {

        [Header("Prayer System")]
        public PrayUiManager prayUiManager;
        public ScoreManager scoreManager;
        
        // Prayer detection settings
        [Range(0.3f, 1f)] public float prayerThreshold = 0.7f; // Lower threshold for prayer matching
        public string targetPrayer = "In the name of the father son and holy spirit";
        [Range(1, 10)] public int minimumWordsRequired = 5; // Minimum words that must match
        public int pointsForSuccessfulPrayer = 1;

        // Fuzzy match threshold (0..1). Higher = stricter.
        [Range(0.5f, 1f)] public float fuzzyThreshold = 0.82f;
        
        // Events
        public Action<bool> OnPrayerAttempted; // true = success, false = failed

        public void Route(string recognizedText)
        {
            if (string.IsNullOrWhiteSpace(recognizedText)) return;
            var text = recognizedText.Trim();

            // Priority 1: Check if PrayPanel is active and handle prayer detection
            if (IsPrayPanelActive())
            {
                bool prayerSuccess = CheckPrayerMatch(text);
                OnPrayerAttempted?.Invoke(prayerSuccess);
                
                if (prayerSuccess)
                {
                    Debug.Log($"Prayer successful! Recognized: '{text}'");
                    HandleSuccessfulPrayer();
                }
                else
                {
                    Debug.Log($"Prayer not recognized or incorrect. Got: '{text}'");
                    // Optional: You could show feedback to player here
                }
                
            }
        }

        private bool IsPrayPanelActive()
        {
            if (prayUiManager == null) return false;
            return prayUiManager.gameObject.activeInHierarchy && prayUiManager.IsPrayPanelActive();
        }

        private bool CheckPrayerMatch(string recognizedText)
        {
            if (string.IsNullOrWhiteSpace(recognizedText) || string.IsNullOrWhiteSpace(targetPrayer))
                return false;

            // Split target prayer into words
            var targetWords = targetPrayer.ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Split recognized text into words
            var recognizedWords = recognizedText.ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

            // Count matching words
            int matchingWords = 0;
            foreach (var targetWord in targetWords)
            {
                // Check if any recognized word matches this target word
                foreach (var recognizedWord in recognizedWords)
                {
                    // Check for exact match or similar match
                    if (recognizedWord == targetWord || 
                        recognizedWord.Contains(targetWord) || 
                        targetWord.Contains(recognizedWord) ||
                        Similarity(recognizedWord, targetWord) >= 0.7f) // High similarity for individual words
                    {
                        matchingWords++;
                        break; // Found a match for this target word, move to next
                    }
                }
            }

            bool isMatch = matchingWords >= minimumWordsRequired; // Use configurable minimum words

            Debug.Log($"Prayer word match check: '{recognizedText}' vs target '{targetPrayer}' - Matching words: {matchingWords}/{targetWords.Length}, Required: {minimumWordsRequired}, Match: {isMatch}");
            
            // Also log which words were found
            var foundWords = new List<string>();
            foreach (var targetWord in targetWords)
            {
                foreach (var recognizedWord in recognizedWords)
                {
                    if (recognizedWord == targetWord || 
                        recognizedWord.Contains(targetWord) || 
                        targetWord.Contains(recognizedWord) ||
                        Similarity(recognizedWord, targetWord) >= 0.7f)
                    {
                        foundWords.Add(targetWord);
                        break;
                    }
                }
            }
            Debug.Log($"Found words: [{string.Join(", ", foundWords)}]");

            return isMatch;
        }

        private void HandleSuccessfulPrayer()
        {
            var anomalies = new List<Anomaly>(Anomaly.ActiveAnomalies);
            bool anomalyBanished = false;

            foreach (Anomaly anomaly in anomalies)
            {
                if (anomaly != null && anomaly.CanBePrayerBanished())
                {
                    anomaly.OnPrayerSuccessful();
                    anomalyBanished = true;
                    Debug.Log($"Anomaly '{anomaly.name}' banished by prayer!");
                }
            }

            if (anomalyBanished)
            {
                // Add score for successful prayer
                if (scoreManager != null)
                {
                    scoreManager.AddScore(pointsForSuccessfulPrayer);
                    Debug.Log($"Added {pointsForSuccessfulPrayer} points for successful prayer!");
                }
            }
            else
            {
                Debug.Log("No anomalies available to banish with prayer.");
            }
        }

        private static float Similarity(string a, string b)
        {
            if (a.Length == 0 && b.Length == 0) return 1f;
            var dist = Levenshtein(a, b);
            var maxLen = Mathf.Max(a.Length, b.Length);
            return 1f - (float)dist / maxLen;
        }

        // Optimized Levenshtein distance (uses two 1D rows)
        private static int Levenshtein(string s, string t)
        {
            int n = s.Length, m = t.Length;
            if (n == 0) return m; if (m == 0) return n;

            var prev = new int[m + 1];
            var curr = new int[m + 1];
            for (int j = 0; j <= m; j++) prev[j] = j;

            for (int i = 1; i <= n; i++)
            {
                curr[0] = i;
                var si = s[i - 1];
                for (int j = 1; j <= m; j++)
                {
                    var cost = (si == t[j - 1]) ? 0 : 1;
                    var del = prev[j] + 1;
                    var ins = curr[j - 1] + 1;
                    var sub = prev[j - 1] + cost;
                    curr[j] = Mathf.Min(del, Mathf.Min(ins, sub));
                }
                // swap rows
                var tmp = prev; prev = curr; curr = tmp;
            }
            return prev[m];
        }
    }
}

