using System;
using System.Collections.Generic;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// Shared fuzzy phrase matching used by both the prayer system (VoiceCommandRouter)
    /// and the sign request system (SignRequestSystem).
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

        private static bool WordsMatch(string recognizedWord, string targetWord, float similarityThreshold)
        {
            return recognizedWord == targetWord ||
                   recognizedWord.Contains(targetWord) ||
                   targetWord.Contains(recognizedWord) ||
                   Similarity(recognizedWord, targetWord) >= similarityThreshold;
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
