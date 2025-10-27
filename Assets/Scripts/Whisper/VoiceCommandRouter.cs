using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Whisper
{
    public class VoiceCommandRouter : MonoBehaviour
    {
        [Header("Optional: wire up UI references")]
        public Button startButton;
        public Button optionsButton;
        public Button backsettingButton; 
        public Button backsoundButton; 
        public Button soundButton; 
        public Button exitButton;

        // Fuzzy match threshold (0..1). Higher = stricter.
        [Range(0.5f, 1f)] public float fuzzyThreshold = 0.82f;

        public void Route(string recognizedText)
        {
            if (string.IsNullOrWhiteSpace(recognizedText)) return;
            var text = recognizedText.Trim();

            if (Matches(text, "เริ่มเกม", "เริ่ม", "เริ่มเล่น", "start"))
            {
                if (startButton != null) startButton.onClick?.Invoke();
                else SceneManager.LoadScene("Gameplay");
                return;
            }

            // Settings: press the options button
            if (Matches(text, "ตั้งค่า", "การตั้งค่า", "options", "option"))
            {
                if (!TryPressFirst(optionsButton))
                {
                    Debug.LogWarning("Voice: Options command received but no optionsButton assigned.");
                }
                return;
            }

            // Close: press whichever back button is currently active/interactable
            if (Matches(text, "ปิด", "ปิดเมนู", "ปิดตั้งค่า","ย้อนกลับ", "close", "close menu"))
            {
                if (!TryPressFirst(backsettingButton, backsoundButton))
                {
                    Debug.LogWarning("Voice: Close command received but no back button is available/active.");
                }
                return;
            }

            if (Matches(text, "เสียง", "เมนูเสียง", "ตั้งค่าเสียง", "sound", "audio"))
            {
                if (!TryPressFirst(soundButton))
                {
                    Debug.LogWarning("Voice: Sound command received but no soundButton assigned.");
                }
                return;
            }

            if (Matches(text, "ออกเกม", "ออก", "exit", "quit"))
            {
                if (exitButton != null)
                {
                    exitButton.onClick?.Invoke();
                }
                else
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
                return;
            }

            // TODO: Add the rest of your in-game commands here.
        }

        private bool Matches(string text, params string[] any)
        {
            var normText = Normalize(text);
            if (string.IsNullOrEmpty(normText)) return false;

            float best = 0f;
            foreach (var candidate in any)
            {
                var normCand = Normalize(candidate);
                if (string.IsNullOrEmpty(normCand)) continue;

                // Exact
                if (normText == normCand) return true;

                // Contains / Prefix / Suffix heuristics (useful for Thai phrases)
                if (normText.Contains(normCand) || normCand.Contains(normText)) return true;

                // Fuzzy similarity (normalized Levenshtein)
                var sim = Similarity(normText, normCand);
                if (sim >= fuzzyThreshold) return true;
                if (sim > best) best = sim;
            }

            return false;
        }

        private bool TryPressFirst(params Button[] candidates)
        {
            // prefer active & interactable buttons
            foreach (var b in candidates)
            {
                if (b != null && b.gameObject.activeInHierarchy && b.interactable)
                {
                    b.onClick?.Invoke();
                    return true;
                }
            }
            // fallback: any assigned button
            foreach (var b in candidates)
            {
                if (b != null)
                {
                    b.onClick?.Invoke();
                    return true;
                }
            }
            return false;
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            s = s.Trim().ToLowerInvariant();

            // Remove spaces and punctuation; keep letters/digits only to be robust against diacritics/typos
            var arr = s.ToCharArray();
            var dst = new System.Text.StringBuilder(arr.Length);
            foreach (var c in arr)
            {
                if (char.IsLetterOrDigit(c)) dst.Append(c);
            }
            return dst.ToString();
        }

        private static float Similarity(string a, string b)
        {
            if (a.Length == 0 && b.Length == 0) return 1f;
            var dist = Levenshtein(a, b);
            var maxLen = Mathf.Max(a.Length, b.Length);
            return 1f - (float)dist / maxLen;
        }

        // Optimized Levenshtein distance (uses two 1D rows)
        private static int Levenshtein(string s, string t)
        {
            int n = s.Length, m = t.Length;
            if (n == 0) return m; if (m == 0) return n;

            var prev = new int[m + 1];
            var curr = new int[m + 1];
            for (int j = 0; j <= m; j++) prev[j] = j;

            for (int i = 1; i <= n; i++)
            {
                curr[0] = i;
                var si = s[i - 1];
                for (int j = 1; j <= m; j++)
                {
                    var cost = (si == t[j - 1]) ? 0 : 1;
                    var del = prev[j] + 1;
                    var ins = curr[j - 1] + 1;
                    var sub = prev[j - 1] + cost;
                    curr[j] = Mathf.Min(del, Mathf.Min(ins, sub));
                }
                // swap rows
                var tmp = prev; prev = curr; curr = tmp;
            }
            return prev[m];
        }
    }
}

