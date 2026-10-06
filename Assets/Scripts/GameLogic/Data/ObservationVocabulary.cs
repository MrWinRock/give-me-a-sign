using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Data
{
    /// <summary>
    /// The spoken words that count as reporting each <see cref="ObservationType"/>. One asset
    /// for the whole game (Resources/ObservationVocabulary), so the report vocabulary is tuned in
    /// one place instead of per anomaly.
    /// </summary>
    [CreateAssetMenu(fileName = "ObservationVocabulary", menuName = "Give Me A Sign/Observation Vocabulary")]
    public class ObservationVocabulary : ScriptableObject
    {
        private const string ResourcePath = "ObservationVocabulary";

        [Serializable]
        public class Entry
        {
            public ObservationType type;

            [Tooltip("Shown to the player (Field Manual, report feedback).")]
            public string label;

            [Tooltip("The name the player says. Keep it to ONE word - each heard word counts for the closest name, so extra names only add false matches.")]
            public string[] phrases;
        }

        [Tooltip("Words shorter than this must be heard exactly - fuzzy matching tiny words ('a', 'in') matches almost anything.")]
        [Min(1)] [SerializeField] private int minFuzzyWordLength = 3;

        [Tooltip("How close a heard word must be to a vocabulary word (1 = exact). LOWER = more forgiving of accents and mishearings.")]
        [Range(0.4f, 1f)] [SerializeField] private float wordSimilarity = 0.6f;

        [Tooltip("Same, for room names (Kitchen, Hallway, Bedroom). Rooms are matched FIRST and their words removed before the anomaly name is looked for.")]
        [Range(0.4f, 1f)] [SerializeField] private float roomSimilarity = 0.5f;

        [Tooltip("Same, for the Radio Check answer words ('copy', 'all clear' ...).")]
        [Range(0.5f, 1f)] [SerializeField] private float phraseSimilarity = 0.65f;

        [Tooltip("Also accept a word with the same first letter and the same consonants ('kichen' for 'kitchen') - helps speakers whose vowels differ from the model's English.")]
        [SerializeField] private bool useSoundAlike = true;

        [SerializeField] private List<Entry> entries = DefaultEntries();

        private static ObservationVocabulary _cached;
        private static ObservationVocabulary _fallback;

        public IReadOnlyList<Entry> Entries => entries;

        public float WordSimilarity => wordSimilarity;
        public float RoomSimilarity => roomSimilarity;
        public float PhraseSimilarity => phraseSimilarity;
        public bool UseSoundAlike => useSoundAlike;

        public static ObservationVocabulary Load()
        {
            if (_cached != null) return _cached;

            _cached = Resources.Load<ObservationVocabulary>(ResourcePath);
            if (_cached != null) return _cached;

            // No asset yet: run on the built-in defaults rather than making every report fail.
            if (_fallback == null)
            {
                Debug.LogWarning($"ObservationVocabulary: no Resources/{ResourcePath} asset - using built-in defaults.");
                _fallback = CreateInstance<ObservationVocabulary>();
            }
            return _fallback;
        }

        public string LabelFor(ObservationType type)
        {
            foreach (var entry in entries)
            {
                if (entry != null && entry.type == type && !string.IsNullOrWhiteSpace(entry.label))
                    return entry.label;
            }
            return type.ToString();
        }

        /// <summary>Up to <paramref name="max"/> example phrases for this observation, for the Field Manual.</summary>
        public string[] SamplePhrases(ObservationType type, int max = 4)
        {
            foreach (var entry in entries)
            {
                if (entry == null || entry.type != type || entry.phrases == null) continue;

                int count = Mathf.Min(max, entry.phrases.Length);
                var sample = new string[count];
                System.Array.Copy(entry.phrases, sample, count);
                return sample;
            }
            return System.Array.Empty<string>();
        }

        // Each heard word counts for the ONE observation it sounds closest to, so a loose threshold
        // can't make a single mishearing report two different things.
        public bool Mentions(string spoken, ObservationType type)
        {
            if (string.IsNullOrWhiteSpace(spoken)) return false;

            var words = Split(spoken);
            for (int i = 0; i < words.Length; i++)
            {
                if (ClosestType(words[i], out var closest) && closest == type) return true;

                // Whisper sometimes splits a word in two ("word robe", "pic ture").
                if (i + 1 < words.Length && ClosestType(words[i] + words[i + 1], out closest) && closest == type) return true;
            }

            // Multi-word phrases ("dark shape") still need every word heard.
            foreach (var entry in entries)
            {
                if (entry == null || entry.type != type || entry.phrases == null) continue;

                foreach (var phrase in entry.phrases)
                {
                    if (phrase != null && phrase.IndexOf(' ') >= 0 && PhraseHeard(spoken, phrase)) return true;
                }
            }
            return false;
        }

        private bool ClosestType(string word, out ObservationType type)
        {
            type = default;
            float best = 0f;

            foreach (var entry in entries)
            {
                if (entry == null || entry.phrases == null) continue;

                foreach (var phrase in entry.phrases)
                {
                    if (string.IsNullOrWhiteSpace(phrase) || phrase.IndexOf(' ') >= 0) continue;

                    float score = WordScore(word, phrase.Trim().ToLowerInvariant());
                    if (score > best) { best = score; type = entry.type; }
                }
            }
            return best >= wordSimilarity;
        }

        public bool PhraseHeard(string spoken, string phrase)
        {
            if (string.IsNullOrWhiteSpace(spoken) || string.IsNullOrWhiteSpace(phrase)) return false;

            var heard = Split(spoken);
            foreach (var target in Split(phrase))
            {
                if (!AnyWordMatches(heard, target)) return false;
            }
            return true;
        }

        private bool AnyWordMatches(string[] heard, string target)
        {
            foreach (var word in heard)
            {
                if (WordScore(word, target) >= wordSimilarity) return true;
            }
            return false;
        }

        // 1 = exact. Plurals/tense ("doors", "moved") and accents ("figur", "shadoe") score high; tiny words only match exactly.
        public float WordScore(string word, string target)
        {
            if (word == target) return 1f;
            if (word.Length < minFuzzyWordLength || target.Length < minFuzzyWordLength) return 0f;

            // Prefix needs 4+ letters, or "war"/"pic" would count for wardrobe/picture.
            if (Mathf.Min(word.Length, target.Length) >= 4 &&
                (word.StartsWith(target, StringComparison.Ordinal) || target.StartsWith(word, StringComparison.Ordinal)))
                return 0.95f;

            float score = Whisper.PhraseMatcher.Similarity(word, target);
            if (useSoundAlike && Whisper.PhraseMatcher.SoundsAlike(word, target)) score = Mathf.Max(score, 0.9f);
            return score;
        }

        private static readonly char[] Separators = { ' ', ',', '.', '!', '?', '-', ':', ';', '"', '\'' };

        private static string[] Split(string text) =>
            text.ToLowerInvariant().Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        [ContextMenu("Reset To Defaults")]
        private void ResetToDefaults() => entries = DefaultEntries();

        private static List<Entry> DefaultEntries() => new List<Entry>
        {
            new Entry { type = ObservationType.Intruder, label = "Figure", phrases = new[] { "figure" } },
            new Entry { type = ObservationType.Shadow, label = "Shadow", phrases = new[] { "shadow" } },
            new Entry { type = ObservationType.ObjectMoved, label = "Furniture", phrases = new[] { "furniture" } },
            new Entry { type = ObservationType.ExtraObject, label = "Wardrobe", phrases = new[] { "wardrobe" } },
            new Entry { type = ObservationType.MissingObject, label = "Missing", phrases = new[] { "missing" } },
            new Entry { type = ObservationType.Door, label = "Door", phrases = new[] { "door" } },
            new Entry { type = ObservationType.Light, label = "Light", phrases = new[] { "light" } },
            new Entry { type = ObservationType.Picture, label = "Picture", phrases = new[] { "picture" } },
            new Entry { type = ObservationType.Demon, label = "Demon", phrases = new[] { "demon" } },
        };
    }
}
