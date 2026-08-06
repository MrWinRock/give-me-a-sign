using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// Generalized "the game is listening for a specific phrase right now" system. The Incident
    /// Report form and the prayer panel each have their own dedicated routing already (they were
    /// here first); this exists for everything added after them - Sprint 4's haunt loops and
    /// Sprint 5's Radio Check - so each one doesn't grow its own copy of the same fuzzy-match
    /// plumbing PhraseMatcher already provides.
    ///
    /// Only one prompt can be awaited at a time. Starting a new one silently drops the old one
    /// (no callback) rather than queuing - a caller that needs to know it got pre-empted should
    /// check <see cref="IsAwaitingPrompt"/> itself before starting a new prompt, or hold its own
    /// timeout instead of relying on this to say "you lost".
    ///
    /// Self-bootstrapping like GameFlowManager - no scene wiring required. WhisperMicInput routes
    /// every recognized chunk here whenever a prompt is active, the same way it already routes to
    /// the prayer and incident-report systems.
    /// </summary>
    public class VoicePromptSystem : MonoBehaviour
    {
        private static VoicePromptSystem _instance;

        /// <summary>
        /// Do NOT call this from OnDisable/OnDestroy - if the real instance already tore itself
        /// down, this would spawn a brand new GameObject mid scene-teardown that Unity cannot
        /// account for ("Some objects were not cleaned up when closing the scene"). Use
        /// <see cref="ExistingInstance"/> from teardown code instead.
        /// </summary>
        public static VoicePromptSystem Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (!Application.isPlaying) return null;

                _instance = FindFirstObjectByType<VoicePromptSystem>();
                if (_instance == null)
                {
                    var host = new GameObject("VoicePromptSystem (auto-created)");
                    _instance = host.AddComponent<VoicePromptSystem>();
                }
                return _instance;
            }
        }

        /// <summary>The instance if one currently exists, otherwise null - never creates one. Safe from OnDisable/OnDestroy.</summary>
        public static VoicePromptSystem ExistingInstance => _instance;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private class ActivePrompt
        {
            public string phrase;
            public int minimumWordsRequired;
            public float wordSimilarity;
            public System.Action<bool> onMatched;
        }

        private ActivePrompt _current;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>True while something is actively listening for a phrase.</summary>
        public bool IsAwaitingPrompt => _current != null;

        /// <summary>
        /// Starts listening for <paramref name="phrase"/>. Replaces whatever prompt was active
        /// before (that caller gets no callback - it should have already given up via its own
        /// timeout, or checked IsAwaitingPrompt first).
        /// </summary>
        public void Expect(string phrase, System.Action<bool> onMatched,
                           int minimumWordsRequired = 1, float wordSimilarity = 0.7f)
        {
            _current = new ActivePrompt
            {
                phrase = phrase,
                onMatched = onMatched,
                minimumWordsRequired = Mathf.Max(1, minimumWordsRequired),
                wordSimilarity = wordSimilarity,
            };
        }

        /// <summary>Stops listening without firing a callback - use when the caller's own timeout/cleanup already handled it.</summary>
        public void Cancel() => _current = null;

        /// <summary>Called by WhisperMicInput with every recognized chunk while a prompt is active.</summary>
        public void Route(string recognizedText)
        {
            if (_current == null || string.IsNullOrWhiteSpace(recognizedText)) return;

            int matches = PhraseMatcher.CountMatchingWords(recognizedText, _current.phrase, _current.wordSimilarity);
            bool isMatch = matches >= _current.minimumWordsRequired;

            if (showDebugInfo)
                Debug.Log($"VoicePromptSystem: '{recognizedText}' vs '{_current.phrase}' - {matches} word(s) matched -> {isMatch}");

            if (!isMatch) return;

            var callback = _current.onMatched;
            _current = null;
            callback?.Invoke(true);
        }
    }
}
