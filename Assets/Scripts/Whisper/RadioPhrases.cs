using System.Text;
using GameLogic.Data;
using UnityEngine;

namespace Whisper
{
    /// <summary>Strict, speech-recognition-tolerant checks for the fixed phrases Radio Check expects.</summary>
    public static class RadioPhrases
    {
        private static readonly char[] Separators = { ' ', ',', '.', '!', '?', '-', ':', ';', '"', '\'', '(', ')', '[', ']' };

        // One configured answer phrase: '{id}' = the call sign, otherwise a word or short phrase,
        // tolerant of squashed spacing ("all clear" / "allclear") and near-miss spelling on longer words.
        public static bool SaidPhrase(string text, string phrase, string callSign)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(phrase)) return false;

            phrase = phrase.Trim();
            if (phrase == "{id}") return SaidCallSign(text, callSign);
            string squashedPhrase = Squash(phrase);
            if (squashedPhrase.Length >= 4 && Squash(text).Contains(squashedPhrase)) return true;

            foreach (var word in phrase.ToLowerInvariant().Split(Separators, System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (!HasWord(text, word, 0.75f)) return false;
            }
            return true;
        }

        public static bool SaidCopy(string text) => HasWord(text, "copy", 0.75f);

        public static bool SaidAllClear(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (Squash(text).Contains("allclear")) return true;
            return HasWord(text, "all", 1f) && HasWord(text, "clear", 0.8f);
        }

        // "SEC-04" is heard as "sec 04", "sec zero four", "sec o four" ... so match the letters and any spoken form of the digits.
        public static bool SaidCallSign(string text, string callSign)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(callSign)) return false;

            string squashed = Squash(text);
            string sign = Squash(callSign);
            if (squashed.Contains(sign)) return true;

            var letters = new StringBuilder();
            var digits = new StringBuilder();
            foreach (char c in sign)
            {
                if (char.IsDigit(c)) digits.Append(c);
                else letters.Append(c);
            }
            if (letters.Length == 0 || digits.Length == 0 || !squashed.Contains(letters.ToString())) return false;

            string spokenDigits = digits.ToString();
            return squashed.Contains(spokenDigits)
                   || squashed.Contains(Spell(spokenDigits, "zero"))
                   || squashed.Contains(Spell(spokenDigits, "o"))
                   || squashed.Contains(Spell(spokenDigits, "oh"));
        }

        private static string Spell(string digits, string zeroWord)
        {
            var sb = new StringBuilder();
            foreach (char c in digits)
            {
                switch (c)
                {
                    case '0': sb.Append(zeroWord); break;
                    case '1': sb.Append("one"); break;
                    case '2': sb.Append("two"); break;
                    case '3': sb.Append("three"); break;
                    case '4': sb.Append("four"); break;
                    case '5': sb.Append("five"); break;
                    case '6': sb.Append("six"); break;
                    case '7': sb.Append("seven"); break;
                    case '8': sb.Append("eight"); break;
                    case '9': sb.Append("nine"); break;
                }
            }
            return sb.ToString();
        }

        private static bool HasWord(string text, string target, float minSimilarity)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            foreach (var word in text.ToLowerInvariant().Split(Separators, System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (word == target) return true;
                if (minSimilarity >= 1f || word.Length < 4 || target.Length < 4) continue;

                var vocabulary = ObservationVocabulary.Load();
                if (PhraseMatcher.Similarity(word, target) >= vocabulary.PhraseSimilarity) return true;
                if (vocabulary.UseSoundAlike && PhraseMatcher.SoundsAlike(word, target)) return true;
            }
            return false;
        }

        private static string Squash(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
