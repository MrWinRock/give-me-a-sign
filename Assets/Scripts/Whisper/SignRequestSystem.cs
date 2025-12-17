using UnityEngine;
using System;
using System.Collections.Generic;

namespace Whisper
{
    public class SignRequestSystem : MonoBehaviour
    {
        [Header("Sign Request Settings")]
        public GameObject[] signGameObjects; // GameObjects to activate when "Give me a sign" is detected
        
        // Sign detection settings
        [Range(0.3f, 1f)] public float signThreshold = 0.7f; // Lower threshold for sign matching
        public string targetSignRequest = "Give me a sign";
        [Range(1, 5)] public int minimumWordsRequired = 3; // Minimum words that must match
        
        // Fuzzy match threshold (0..1). Higher = stricter.
        [Range(0.5f, 1f)] public float fuzzyThreshold = 0.82f;
        
        // Events
        public Action<bool> OnSignRequested; // true = success, false = failed

        public void Route(string recognizedText)
        {
            if (string.IsNullOrWhiteSpace(recognizedText)) return;
            var text = recognizedText.Trim();

            // Check for sign request
            bool signSuccess = CheckSignMatch(text);
            OnSignRequested?.Invoke(signSuccess);
            
            if (signSuccess)
            {
                Debug.Log($"Sign request successful! Recognized: '{text}'");
                HandleSuccessfulSignRequest();
            }
            else
            {
                Debug.Log($"Sign request not recognized. Got: '{text}'");
            }
        }

        private bool CheckSignMatch(string recognizedText)
        {
            if (string.IsNullOrWhiteSpace(recognizedText) || string.IsNullOrWhiteSpace(targetSignRequest))
                return false;

            // Split target sign request into words
            var targetWords = targetSignRequest.ToLowerInvariant()
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

            Debug.Log($"Sign request word match check: '{recognizedText}' vs target '{targetSignRequest}' - Matching words: {matchingWords}/{targetWords.Length}, Required: {minimumWordsRequired}, Match: {isMatch}");
            
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

        private void HandleSuccessfulSignRequest()
        {
            // Activate all assigned GameObjects
            if (signGameObjects != null && signGameObjects.Length > 0)
            {
                foreach (var gameObj in signGameObjects)
                {
                    if (gameObj != null)
                    {
                        gameObj.SetActive(true);
                        Debug.Log($"Activated GameObject: {gameObj.name}");
                    }
                }
                Debug.Log($"Sign request handled! Activated {signGameObjects.Length} GameObjects.");
            }
            else
            {
                Debug.Log("No GameObjects assigned to activate for sign request.");
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
