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

            [Tooltip("คำที่ผู้เล่นพูดเพี้ยนหรือ Whisper ได้ยินผิด แต่ให้นับเป็นชื่อนี้ (ไม่แสดงในคู่มือ). Spaces are ignored ('word robe' = 'wordrobe').")]
            public string[] mishearings;
        }

        [Tooltip("Words shorter than this must be heard exactly - fuzzy matching tiny words ('a', 'in') matches almost anything.")]
        [Min(1)] [SerializeField] private int minFuzzyWordLength = 3;

        [Tooltip("How close a heard word must be to a vocabulary word (1 = exact). LOWER = more forgiving of accents and mishearings.")]
        [Range(0.3f, 1f)] [SerializeField] private float wordSimilarity = 0.55f;

        [Tooltip("Once the ROOM has been heard, the anomaly name may be this sloppy instead (it still has to be the closest name). Lower = more forgiving.")]
        [Range(0.3f, 1f)] [SerializeField] private float wordSimilarityWithRoom = 0.45f;

        [Tooltip("Same, for room names (Kitchen, Hallway, Bedroom). Rooms are matched FIRST and their words removed before the anomaly name is looked for.")]
        [Range(0.3f, 1f)] [SerializeField] private float roomSimilarity = 0.45f;

        [Tooltip("Filler words that are never taken as an anomaly name, so a looser match can't fire on 'the', 'see', 'there'...")]
        [SerializeField] private string[] ignoredWords =
        {
            "the", "a", "an", "in", "on", "at", "is", "it", "its", "i", "im", "see", "saw", "there", "theres", "here",
            "and", "to", "of", "this", "that", "with", "my", "room", "near", "by", "some", "something", "thing", "one",
            "please", "copy", "over", "hq", "hello", "hey", "yes", "no", "okay", "ok", "now", "just", "think", "look",
            "wall", "floor", "corner", "window", "table", "bed", "inside", "outside", "front", "behind", "next",
        };

        [Tooltip("Same, for the Radio Check answer words ('copy', 'all clear' ...).")]
        [Range(0.5f, 1f)] [SerializeField] private float phraseSimilarity = 0.65f;

        [Tooltip("Also accept a word with the same first letter and the same consonants ('kichen' for 'kitchen') - helps speakers whose vowels differ from the model's English.")]
        [SerializeField] private bool useSoundAlike = true;

        [SerializeField] private List<Entry> entries = DefaultEntries();

        private static ObservationVocabulary _cached;
        private static ObservationVocabulary _fallback;

        public IReadOnlyList<Entry> Entries => entries;

        public float WordSimilarity => wordSimilarity;
        public float WordSimilarityWithRoom => wordSimilarityWithRoom;
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
        // roomKnown: the room was already heard, so the name may be sloppier (wordSimilarityWithRoom).
        public bool Mentions(string spoken, ObservationType type, bool roomKnown = false)
        {
            if (string.IsNullOrWhiteSpace(spoken)) return false;

            float threshold = roomKnown ? Mathf.Min(wordSimilarity, wordSimilarityWithRoom) : wordSimilarity;
            var words = Split(spoken);
            for (int i = 0; i < words.Length; i++)
            {
                if (IsIgnored(words[i])) continue;
                if (ClosestType(words[i], threshold, out var closest) && closest == type) return true;

                // Whisper sometimes splits a word in two ("word robe", "pic ture").
                if (i + 1 < words.Length && !IsIgnored(words[i + 1])
                    && ClosestType(words[i] + words[i + 1], threshold, out closest) && closest == type) return true;
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

        private bool IsIgnored(string word)
        {
            if (IsRoomWord(word)) return true;
            if (ignoredWords == null) return false;
            foreach (var ignored in ignoredWords)
            {
                if (string.Equals(word, ignored, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // A room's own name (or one of its listed mishearings) is never also an anomaly name ("kitchen" is not "pitcher").
        private static bool IsRoomWord(string word)
        {
            foreach (var anchor in RoomRegistry.All)
            {
                var room = anchor != null ? anchor.Room : null;
                if (room == null) continue;

                if (string.Equals(word, room.Label, StringComparison.OrdinalIgnoreCase)) return true;
                if (room.mishearings == null) continue;

                foreach (var misheard in room.mishearings)
                {
                    if (!string.IsNullOrWhiteSpace(misheard) && misheard.IndexOf(' ') < 0
                        && string.Equals(word, misheard, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            return false;
        }

        private bool ClosestType(string word, float threshold, out ObservationType type)
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

                if (entry.mishearings == null) continue;
                foreach (var misheard in entry.mishearings)
                {
                    if (string.IsNullOrWhiteSpace(misheard)) continue;

                    float score = WordScore(word, misheard.Replace(" ", "").Trim().ToLowerInvariant());
                    if (score > best) { best = score; type = entry.type; }
                }
            }
            return best >= threshold;
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

        // 1 = exact. Plurals/tense ("doors"), accents ("sadow", "lite", "pigture") score high; tiny words only match exactly.
        public float WordScore(string word, string target)
        {
            if (word == target) return 1f;
            if (word.Length < minFuzzyWordLength || target.Length < minFuzzyWordLength) return 0f;

            // Prefix needs 4+ letters, or "war"/"pic" would count for wardrobe/picture.
            if (Mathf.Min(word.Length, target.Length) >= 4 &&
                (word.StartsWith(target, StringComparison.Ordinal) || target.StartsWith(word, StringComparison.Ordinal)))
                return 0.95f;

            return Whisper.PhraseMatcher.FuzzyScore(word, target, useSoundAlike);
        }

        private static readonly char[] Separators = { ' ', ',', '.', '!', '?', '-', ':', ';', '"', '\'' };

        private static string[] Split(string text) =>
            text.ToLowerInvariant().Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        // Same order as DefaultEntries. Accent swaps (r/l, sh/s, th/t) are already handled by the matcher - these are the bigger slips.
        public static readonly string[][] DefaultMishearings =
        {
            new[] { "figger", "figur", "figa", "finger", "feature", "figures", "pigure" },
            new[] { "shado", "shadoe", "sadow", "chadow", "shallow", "shadows", "shatter", "shadowy" },
            new[] { "furnitur", "furniter", "funiture", "fernichure", "fur niture", "furnish", "furnished" },
            new[] { "wardrop", "word robe", "ward robe", "wad robe", "war drop", "ward rope", "wardrobes" },
            new[] { "mising", "messing", "missed", "misting" },
            new[] { "dor", "doa", "dore", "doors", "dour", "dorr" },
            new[] { "lite", "right", "lait", "rite", "lights", "lighting" },
            new[] { "pigture", "pitcher", "picher", "pikchur", "pick sure", "pictures", "pickture" },
            new[] { "daemon", "deamon", "deemon", "diamond", "damon", "demons", "lemon" },
        };

        [ContextMenu("Reset To Defaults")]
        private void ResetToDefaults() => entries = DefaultEntries();

        private static List<Entry> DefaultEntries() => new List<Entry>
        {
            new Entry { type = ObservationType.Intruder, label = "Figure", phrases = new[] { "figure" }, mishearings = DefaultMishearings[0] },
            new Entry { type = ObservationType.Shadow, label = "Shadow", phrases = new[] { "shadow" }, mishearings = DefaultMishearings[1] },
            new Entry { type = ObservationType.ObjectMoved, label = "Furniture", phrases = new[] { "furniture" }, mishearings = DefaultMishearings[2] },
            new Entry { type = ObservationType.ExtraObject, label = "Wardrobe", phrases = new[] { "wardrobe" }, mishearings = DefaultMishearings[3] },
            new Entry { type = ObservationType.MissingObject, label = "Missing", phrases = new[] { "missing" }, mishearings = DefaultMishearings[4] },
            new Entry { type = ObservationType.Door, label = "Door", phrases = new[] { "door" }, mishearings = DefaultMishearings[5] },
            new Entry { type = ObservationType.Light, label = "Light", phrases = new[] { "light" }, mishearings = DefaultMishearings[6] },
            new Entry { type = ObservationType.Picture, label = "Picture", phrases = new[] { "picture" }, mishearings = DefaultMishearings[7] },
            new Entry { type = ObservationType.Demon, label = "Demon", phrases = new[] { "demon" }, mishearings = DefaultMishearings[8] },
        };
    }
}
