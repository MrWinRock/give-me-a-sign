using System;
using System.Collections.Generic;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// Listens for the "Give me a sign" phrase in recognized speech and activates the
    /// assigned GameObjects when it is heard.
    /// Word matching lives in <see cref="PhraseMatcher"/>, shared with VoiceCommandRouter.
    /// </summary>
    public class SignRequestSystem : MonoBehaviour
    {
        [Header("Sign Request Settings")]
        [Tooltip("GameObjects activated when the sign request phrase is detected.")]
        public GameObject[] signGameObjects;

        [Header("Sign Detection")]
        public string targetSignRequest = "Give me a sign";
        [Tooltip("How many words of the target phrase must be heard for the request to count.")]
        [Range(1, 5)] public int minimumWordsRequired = 3;
        [Tooltip("Per-word fuzzy similarity threshold (1 = exact match only).")]
        [Range(0.5f, 1f)] public float wordSimilarity = 0.7f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        /// <summary>Fired on every routed phrase: true = sign request detected.</summary>
        public Action<bool> OnSignRequested;

        public void Route(string recognizedText)
        {
            if (string.IsNullOrWhiteSpace(recognizedText)) return;

            string text = recognizedText.Trim();
            bool signSuccess = CheckSignMatch(text);
            OnSignRequested?.Invoke(signSuccess);

            if (signSuccess)
                HandleSuccessfulSignRequest();
        }

        private bool CheckSignMatch(string recognizedText)
        {
            List<string> foundWords = showDebugInfo ? new List<string>() : null;
            int matchingWords = PhraseMatcher.CountMatchingWords(recognizedText, targetSignRequest, wordSimilarity, foundWords);
            bool isMatch = matchingWords >= minimumWordsRequired;

            if (showDebugInfo)
            {
                Debug.Log($"Sign request match: '{recognizedText}' vs '{targetSignRequest}' - " +
                          $"{matchingWords} words matched (need {minimumWordsRequired}) -> {isMatch}. " +
                          $"Found: [{string.Join(", ", foundWords)}]");
            }

            return isMatch;
        }

        private void HandleSuccessfulSignRequest()
        {
            if (signGameObjects == null || signGameObjects.Length == 0)
            {
                if (showDebugInfo)
                    Debug.Log("SignRequestSystem: no GameObjects assigned to activate.");
                return;
            }

            foreach (var gameObj in signGameObjects)
            {
                if (gameObj != null)
                    gameObj.SetActive(true);
            }

            if (showDebugInfo)
                Debug.Log($"Sign request handled! Activated {signGameObjects.Length} GameObjects.");
        }
    }
}
