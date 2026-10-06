using System;
using System.Collections.Generic;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// Shared fuzzy phrase matching used by the prayer system (VoiceCommandRouter),
    /// VoicePromptSystem and radio-report matching.
    /// </summary>
    public static class PhraseMatcher
    {
        private static readonly char[] WordSeparators = { ' ', ',', '.', '!', '?' };

        public static int CountMatchingWords(
            string recognizedText,
            string targetPhrase,
            float wordSimilarity = 0.7f,
            List<string> foundWords = null)
        {
            if (string.IsNullOrWhiteSpace(recognizedText) || string.IsNullOrWhiteSpace(targetPhrase))
                return 0;

            string[] targetWords = SplitWords(targetPhrase);
            string[] recognizedWords = SplitWords(recognizedText);

            int matching = 0;
            foreach (var targetWord in targetWords)
            {
                foreach (var recognizedWord in recognizedWords)
                {
                    if (WordsMatch(recognizedWord, targetWord, wordSimilarity))
                    {
                        matching++;
                        foundWords?.Add(targetWord);
                        break; // this target word is satisfied, move to the next
                    }
                }
            }

            return matching;
        }

        public static string[] SplitWords(string phrase)
        {
            return phrase.ToLowerInvariant().Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
        }

        // Below this length a Latin word must match exactly, or "a"/"in" Contains-match nearly every target.
        private const int MinFuzzyLength = 4;

        private static bool WordsMatch(string recognizedWord, string targetWord, float similarityThreshold)
        {
            if (recognizedWord == targetWord) return true;

            // Thai has no spaces between words, so short Thai fragments legitimately need Contains().
            if (IsLatin(recognizedWord) && IsLatin(targetWord) &&
                Mathf.Min(recognizedWord.Length, targetWord.Length) < MinFuzzyLength)
                return false;

            return
                   recognizedWord.Contains(targetWord) ||
                   targetWord.Contains(recognizedWord) ||
                   Similarity(recognizedWord, targetWord) >= similarityThreshold;
        }

        private static bool IsLatin(string word)
        {
            foreach (char c in word)
            {
                if (c > 0x024F) return false;
            }
            return true;
        }

        // Same first letter and same consonant skeleton ("kichen" / "kitchen"): accents mostly move the vowels.
        public static bool SoundsAlike(string a, string b)
        {
            if (a == null || b == null || a.Length < 4 || b.Length < 4 || a[0] != b[0]) return false;

            string skeletonA = Skeleton(a);
            return skeletonA.Length >= 3 && skeletonA == Skeleton(b);
        }

        private static string Skeleton(string word)
        {
            var sb = new System.Text.StringBuilder(word.Length);
            sb.Append(word[0]);
            char previous = word[0];
            for (int i = 1; i < word.Length; i++)
            {
                char c = word[i];
                if (c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u' || c == previous) continue;
                sb.Append(c);
                previous = c;
            }
            return sb.ToString();
        }

        // Accent-tolerant closeness, 0..1, for report words and room names.
        // A weak match must at least start with the same sound, so "room" never reads as "bedroom".
        public static float FuzzyScore(string heard, string target, bool useSoundAlike = true)
        {
            if (heard == target) return 1f;

            string a = Fold(heard), b = Fold(target);
            if (a == b) return 0.97f;

            float score = Mathf.Max(Similarity(heard, target), Similarity(a, b) * 0.97f);
            if (useSoundAlike && (SoundsAlike(heard, target) || SoundsAlike(a, b))) score = Mathf.Max(score, 0.9f);

            if (score < 0.7f && (a.Length == 0 || b.Length == 0 || a[0] != b[0])) return 0f;
            return score;
        }

        // Folds sounds Thai and other non-native speakers (and Whisper) commonly swap: r/l, v/w, sh/ch/s, th/t, silent gh, doubled letters.
        public static string Fold(string word)
        {
            if (string.IsNullOrEmpty(word)) return "";

            string s = word.ToLowerInvariant()
                .Replace("tch", "ch").Replace("gh", "").Replace("ph", "f").Replace("th", "t")
                .Replace("sh", "s").Replace("ch", "s").Replace("ck", "k").Replace("qu", "kw").Replace("x", "ks");

            var sb = new System.Text.StringBuilder(s.Length);
            char previous = '\0';
            foreach (char raw in s)
            {
                char c = raw == 'r' ? 'l' : raw == 'v' ? 'w' : raw == 'z' ? 's' : raw == 'c' || raw == 'q' ? 'k' : raw == 'y' ? 'i' : raw;
                if (c == previous) continue;
                sb.Append(c);
                previous = c;
            }

            if (sb.Length > 3 && sb[sb.Length - 1] == 'e') sb.Length--; // "lite" ~ "light"
            return sb.ToString();
        }

        public static float Similarity(string a, string b)
        {
            if (a.Length == 0 && b.Length == 0) return 1f;
            int dist = Levenshtein(a, b);
            int maxLen = Mathf.Max(a.Length, b.Length);
            return 1f - (float)dist / maxLen;
        }

        // Levenshtein distance using two 1D rows to avoid the full matrix allocation.
        private static int Levenshtein(string s, string t)
        {
            int n = s.Length, m = t.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            var prev = new int[m + 1];
            var curr = new int[m + 1];
            for (int j = 0; j <= m; j++) prev[j] = j;

            for (int i = 1; i <= n; i++)
            {
                curr[0] = i;
                char si = s[i - 1];
                for (int j = 1; j <= m; j++)
                {
                    int cost = (si == t[j - 1]) ? 0 : 1;
                    int del = prev[j] + 1;
                    int ins = curr[j - 1] + 1;
                    int sub = prev[j - 1] + cost;
                    curr[j] = Mathf.Min(del, Mathf.Min(ins, sub));
                }
                (prev, curr) = (curr, prev);
            }
            return prev[m];
        }
    }
}
