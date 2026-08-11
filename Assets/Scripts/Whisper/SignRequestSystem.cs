using System;
using System.Collections;
using System.Collections.Generic;
using GameLogic;
using GameLogic.Night;
using Report;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// HL-7 "Give Me A Sign" - the mechanic the game is named after. Listens for the phrase at any
    /// time (WhisperMicInput routes every recognized chunk here unconditionally, not gated behind
    /// any panel) and, when heard, points at the nearest active anomaly's room - for a price.
    /// </summary>
    public class SignRequestSystem : MonoBehaviour
    {
        [Header("Sign Request Settings")]
        [Tooltip("GameObjects activated when the sign request phrase is detected. Legacy hook - kept for whatever the scene already wires here.")]
        public GameObject[] signGameObjects;

        [Header("Sign Detection")]
        public string targetSignRequest = "Give me a sign";
        [Tooltip("How many words of the target phrase must be heard for the request to count.")]
        [Range(1, 5)] public int minimumWordsRequired = 3;
        [Tooltip("Per-word fuzzy similarity threshold (1 = exact match only).")]
        [Range(0.5f, 1f)] public float wordSimilarity = 0.7f;

        [Header("HL-7 Hint (Sprint 6)")]
        [Tooltip("How many times the player can ask per night. 0 = disabled (legacy activate-only behaviour).")]
        [Min(0)] [SerializeField] private int usesPerNight = 3;
        [Tooltip("GlitchDirector intensity floor added per use (1 + this * usesSpent). Permanent for the rest of the night.")]
        [SerializeField] private float intensityBumpPerUse = 0.15f;
        [SerializeField] private float hintDisplaySeconds = 4f;
        [Tooltip("Force an immediate Camera Betrayal glitch on each successful use, if one isn't already running and a CameraBetrayalHaunt exists in the scene.")]
        [SerializeField] private bool triggerCameraBetrayalOnUse = true;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        public Action<bool> OnSignRequested;

        private int _usesRemaining;
        private GlitchDirector _glitchDirector;
        private CameraBetrayalHaunt _cameraBetrayal;
        private SignHintHud _hud;
        private Coroutine _hudHide;

        void Start()
        {
            _usesRemaining = usesPerNight;
            _glitchDirector = FindFirstObjectByType<GlitchDirector>();
            _cameraBetrayal = FindFirstObjectByType<CameraBetrayalHaunt>();
        }

        void OnDestroy()
        {
            _hud?.Destroy();
        }

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
            // Legacy behaviour kept as-is for whatever the scene already wires here.
            if (signGameObjects != null)
            {
                foreach (var gameObj in signGameObjects)
                {
                    if (gameObj != null) gameObj.SetActive(true);
                }

                if (showDebugInfo && signGameObjects.Length > 0)
                    Debug.Log($"Sign request handled! Activated {signGameObjects.Length} GameObjects.");
            }

            GiveHint();
        }

        private void GiveHint()
        {
            if (usesPerNight <= 0) return; // hint mechanic disabled - legacy activate-only behaviour

            if (_usesRemaining <= 0)
            {
                ShowHudMessage("...nothing answers.", 2f);
                return;
            }

            _usesRemaining--;

            var target = FindNearestUnreportedAnomaly();
            string message = target != null && target.AssignedRoom != null
                ? $"⚠ {target.AssignedRoom.Label}"
                : "...nothing is out there right now.";

            ShowHudMessage(message, hintDisplaySeconds);

            // Cost 1: floors GlitchDirector's intensity a little higher per use spent. A floor, not
            // a stacking multiply-every-time bump - asking three times shouldn't compound into an
            // unplayable form, it should just mean "the rest of tonight is worse than before I asked".
            if (_glitchDirector != null)
            {
                int spent = usesPerNight - _usesRemaining;
                _glitchDirector.SetIntensity(1f + intensityBumpPerUse * spent);
            }

            // Cost 2: something answers for real, right now.
            if (triggerCameraBetrayalOnUse && _cameraBetrayal != null && !_cameraBetrayal.IsActive)
                _cameraBetrayal.Trigger(default);

            if (showDebugInfo)
                Debug.Log($"SignRequestSystem: hint given ('{message}'). {_usesRemaining}/{usesPerNight} uses left.", this);
        }

        private static Anomaly FindNearestUnreportedAnomaly()
        {
            foreach (var anomaly in Anomaly.ActiveAnomalies)
            {
                if (anomaly != null && anomaly.gameObject.activeInHierarchy && !anomaly.IsReported)
                    return anomaly;
            }
            return null;
        }

        private void ShowHudMessage(string text, float seconds)
        {
            if (_hud == null) _hud = SignHintHud.Create();
            _hud.SetText(text);

            if (_hudHide != null) StopCoroutine(_hudHide);
            _hudHide = StartCoroutine(HideAfter(seconds));
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _hud?.SetText(string.Empty);
        }
    }
}
