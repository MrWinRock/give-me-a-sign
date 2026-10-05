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

            [Tooltip("Any of these spoken counts. Multi-word phrases need every word heard. List likely mishearings too.")]
            public string[] phrases;
        }

        [Tooltip("Words shorter than this must be heard exactly - fuzzy matching tiny words ('a', 'in') matches almost anything.")]
        [Min(1)] [SerializeField] private int minFuzzyWordLength = 4;

        [Tooltip("How close a heard word must be to a vocabulary word (1 = exact). LOWER = more forgiving of accents and mishearings.")]
        [Range(0.5f, 1f)] [SerializeField] private float wordSimilarity = 0.65f;

        [Tooltip("Same, for room names (Kitchen, Hallway, Bedroom).")]
        [Range(0.5f, 1f)] [SerializeField] private float roomSimilarity = 0.6f;

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

        public bool Mentions(string spoken, ObservationType type)
        {
            foreach (var entry in entries)
            {
                if (entry == null || entry.type != type || entry.phrases == null) continue;

                foreach (var phrase in entry.phrases)
                {
                    if (PhraseHeard(spoken, phrase)) return true;
                }
            }
            return false;
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
                if (word == target) return true;

                if (word.Length < minFuzzyWordLength || target.Length < minFuzzyWordLength) continue;

                // Plurals and tense ("doors", "moved") without letting short words match everything.
                if (word.StartsWith(target, StringComparison.Ordinal) || target.StartsWith(word, StringComparison.Ordinal))
                    return true;

                if (Whisper.PhraseMatcher.Similarity(word, target) >= wordSimilarity)
                    return true;

                if (useSoundAlike && Whisper.PhraseMatcher.SoundsAlike(word, target))
                    return true;
            }
            return false;
        }

        private static readonly char[] Separators = { ' ', ',', '.', '!', '?', '-', ':', ';', '"', '\'' };

        private static string[] Split(string text) =>
            text.ToLowerInvariant().Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        [ContextMenu("Reset To Defaults")]
        private void ResetToDefaults() => entries = DefaultEntries();

        private static List<Entry> DefaultEntries() => new List<Entry>
        {
            new Entry { type = ObservationType.Intruder, label = "Intruder",
                phrases = new[] { "person", "someone", "somebody", "figure", "intruder", "man", "woman", "people", "body", "guy", "stranger" } },
            new Entry { type = ObservationType.Shadow, label = "Shadow",
                phrases = new[] { "shadow", "shape", "blob", "darkness", "dark shape", "silhouette" } },
            new Entry { type = ObservationType.ObjectMoved, label = "Object moved",
                phrases = new[] { "moved", "chair", "furniture", "misplaced", "fallen", "knocked", "displaced", "out of place", "pillow", "cushion" } },
            new Entry { type = ObservationType.ExtraObject, label = "Extra object",
                phrases = new[] { "extra", "appeared", "new object", "wardrobe", "cabinet", "closet", "something new", "wasn't there" } },
            new Entry { type = ObservationType.MissingObject, label = "Missing object",
                phrases = new[] { "missing", "gone", "disappeared", "vanished", "removed", "taken" } },
            new Entry { type = ObservationType.Door, label = "Door",
                phrases = new[] { "door", "doorway", "opened", "open door" } },
            new Entry { type = ObservationType.Light, label = "Light",
                phrases = new[] { "light", "lights", "lamp", "bulb", "flicker", "flickering" } },
            new Entry { type = ObservationType.Picture, label = "Picture",
                phrases = new[] { "picture", "painting", "photo", "portrait", "frame" } },
            new Entry { type = ObservationType.Demon, label = "Demon",
                phrases = new[] { "demon", "devil", "monster", "creature" } },
        };
    }
}
