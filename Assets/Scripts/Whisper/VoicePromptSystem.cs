using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// Generalized "the game is listening for a specific phrase right now" system. The Incident
    /// Report form and the prayer panel each have their own dedicated routing already (they were
    /// here first); this exists for everything added after them - Sprint 4's haunt loops and
    /// Sprint 5's Radio Check - so each one doesn't grow its own copy of the same fuzzy-match
    /// plumbing PhraseMatcher already provides.
    /// </summary>
    public class VoicePromptSystem : MonoBehaviour
    {
        private static VoicePromptSystem _instance;

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

        public bool IsAwaitingPrompt => _current != null;

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

        public void Cancel() => _current = null;

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
