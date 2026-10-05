using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif

namespace Report
{
    /// <summary>
    /// Finds the player's own desktop picture for the Camera Betrayal "Mirror" glitch, in this order:
    /// 1. Wallpaper Engine (if installed): the current wallpaper's still image, else its preview.jpg/png.
    /// 2. The Windows wallpaper file.
    /// Nothing found (or not Windows, or the player turned it off) means no Mirror at all. Read locally and kept only
    /// while the glitch runs - never saved or sent. Switch: Control Panel (PrefKey).
    /// </summary>
    public static class PlayerWallpaper
    {
        public const string PrefKey = "Opt_MirrorWallpaper";

        private const long MaxFileBytes = 60L * 1024 * 1024;
        private const float CacheSeconds = 60f;
        private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" }; // what Texture2D.LoadImage can read

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint SpiGetDeskWallpaper = 0x0073;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, StringBuilder buffer, uint winIni);

        // Microsoft.Win32.Registry is not part of Unity's .NET profile, so ask Windows directly.
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegGetValue(UIntPtr hkey, string subKey, string value, uint flags, IntPtr type, StringBuilder data, ref uint size);

        private static readonly UIntPtr HkeyCurrentUser = new UIntPtr(0x80000001u);
        private const uint RrfRtRegSz = 0x00000002;
#endif

        public enum Source { WallpaperEngine, Windows }

        private struct Candidate
        {
            public Source source;
            public string path;
        }

        [Serializable]
        private class WallpaperProject
        {
            public string file;
            public string preview;
        }

        private static List<Candidate> _candidates;
        private static float _resolvedAt = float.NegativeInfinity;

        // On by default; the Control Panel toggle writes 0/1.
        public static bool Enabled => PlayerPrefs.GetInt(PrefKey, 1) == 1;

        public static bool IsSupported
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return true;
#else
                return false;
#endif
            }
        }

        // True when the player allows it AND an image was found - the Mirror glitch only happens then.
        public static bool CanShow => Enabled && Candidates().Count > 0;

        // Debug panel: what the game would use right now (searches again, ignoring the cache).
        public static string Describe()
        {
            _candidates = null;
            var sb = new StringBuilder();
            sb.Append(IsSupported ? "Windows build/editor: yes" : "Not Windows: Mirror never happens");
            sb.Append(Enabled ? " | player switch: ON" : " | player switch: OFF (no Mirror)");

            var list = Candidates();
            if (list.Count == 0) sb.Append(" | no picture found: Mirror will not happen");
            for (int i = 0; i < list.Count; i++)
                sb.Append(i == 0 ? " | USING " : " | fallback ").Append(list[i].source).Append(": ").Append(list[i].path);
            return sb.ToString();
        }

        // Returns a texture the caller must Destroy, or null.
        public static Texture2D TryLoad()
        {
            if (!Enabled) return null;

            foreach (var candidate in Candidates())
            {
                var texture = Decode(candidate.path);
                if (texture == null) continue;

                Debug.Log($"PlayerWallpaper: showing the {candidate.source} wallpaper ({Path.GetFileName(candidate.path)}, {texture.width}x{texture.height}).");
                return texture;
            }
            return null;
        }

        private static Texture2D Decode(string path)
        {
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "PlayerWallpaper", hideFlags = HideFlags.HideAndDontSave };
                if (texture.LoadImage(File.ReadAllBytes(path), markNonReadable: true)) return texture;

                UnityEngine.Object.Destroy(texture);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PlayerWallpaper: could not read the wallpaper ({e.GetType().Name}).");
            }
            return null;
        }

        // ── discovery ────────────────────────────────────────────────────────────────────

        private static List<Candidate> Candidates()
        {
            if (_candidates != null && Time.realtimeSinceStartup - _resolvedAt < CacheSeconds) return _candidates;

            var list = new List<Candidate>();
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            string engine = SafeFind(FindWallpaperEngineImage);
            if (engine != null) list.Add(new Candidate { source = Source.WallpaperEngine, path = engine });

            string windows = SafeFind(FindWindowsWallpaper);
            if (windows != null) list.Add(new Candidate { source = Source.Windows, path = windows });
#endif
            _candidates = list;
            _resolvedAt = Time.realtimeSinceStartup;
            return list;
        }

        private static string SafeFind(Func<string> find)
        {
            try { return find(); }
            catch (Exception e)
            {
                Debug.LogWarning($"PlayerWallpaper: lookup failed ({e.GetType().Name}).");
                return null;
            }
        }

        private static bool IsUsableFile(string path, bool allowNoExtension = false)
        {
            if (string.IsNullOrEmpty(path)) return false;

            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0 || info.Length > MaxFileBytes) return false;

            string extension = info.Extension.ToLowerInvariant();
            return Array.IndexOf(ImageExtensions, extension) >= 0 || (allowNoExtension && extension.Length == 0);
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // 2. The Windows wallpaper. Slideshow/Spotlight wallpapers show up as the extensionless TranscodedWallpaper (a JPEG).
        private static string FindWindowsWallpaper()
        {
            var buffer = new StringBuilder(520);
            if (!SystemParametersInfo(SpiGetDeskWallpaper, (uint)buffer.Capacity, buffer, 0)) return null;

            string path = buffer.ToString();
            return IsUsableFile(path, allowNoExtension: true) ? path : null;
        }

        // 1. Wallpaper Engine: find its config.json, read the wallpaper selected for this Windows user, then pick a still image.
        private static string FindWallpaperEngineImage()
        {
            foreach (string configPath in WallpaperEngineConfigs())
            {
                string selected = ReadSelectedWallpaper(configPath);
                string image = selected != null ? StillImageFor(selected) : null;
                if (image != null) return image;
            }
            return null;
        }

        private static IEnumerable<string> WallpaperEngineConfigs()
        {
            var libraries = new List<string>();

            string steam = SteamPath();
            if (!string.IsNullOrEmpty(steam))
            {
                libraries.Add(steam);
                string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        libraries.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
                }
            }

            // Usual install spots, in case the registry value is missing.
            libraries.Add(Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)", "Steam"));
            libraries.Add(Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files", "Steam"));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string library in libraries)
            {
                string config = Path.GetFullPath(Path.Combine(library, "steamapps", "common", "wallpaper_engine", "config.json"));
                if (File.Exists(config) && seen.Add(config)) yield return config;
            }
        }

        private static string SteamPath()
        {
            var buffer = new StringBuilder(520);
            uint size = (uint)(buffer.Capacity * 2); // bytes
            int result = RegGetValue(HkeyCurrentUser, @"Software\Valve\Steam", "SteamPath", RrfRtRegSz, IntPtr.Zero, buffer, ref size);
            return result == 0 ? buffer.ToString().Replace('/', Path.DirectorySeparatorChar) : null;
        }

        // config.json: { "<WindowsUser>": { "general": { "wallpaperconfig": { "selectedwallpapers": { "Monitor0": { "file": "..." } } } } } }
        private static string ReadSelectedWallpaper(string configPath)
        {
            var root = MiniJson.Parse(File.ReadAllText(configPath)) as Dictionary<string, object>;
            if (root == null) return null;

            var users = new List<string>();
            foreach (string name in root.Keys)
            {
                if (string.Equals(name, Environment.UserName, StringComparison.OrdinalIgnoreCase)) users.Insert(0, name);
                else users.Add(name);
            }

            foreach (string user in users)
            {
                var monitors = MiniJson.Get(root, user, "general", "wallpaperconfig", "selectedwallpapers") as Dictionary<string, object>;
                if (monitors == null || monitors.Count == 0) continue;

                object monitor;
                if (!monitors.TryGetValue("Monitor0", out monitor))
                {
                    foreach (var any in monitors.Values) { monitor = any; break; }
                }

                string file = MiniJson.Get(monitor, "file") as string;
                if (!string.IsNullOrEmpty(file)) return file;
            }
            return null;
        }

        // The selected entry is a project.json, a scene.pkg, or a plain media file. Still image first, then preview.jpg/png.
        private static string StillImageFor(string selected)
        {
            selected = selected.Replace('/', Path.DirectorySeparatorChar);
            if (IsUsableFile(selected)) return selected; // the wallpaper itself is a png/jpg

            string folder = Path.GetDirectoryName(selected);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;

            var project = new WallpaperProject();
            string projectFile = Path.Combine(folder, "project.json");
            if (File.Exists(projectFile))
            {
                try { project = JsonUtility.FromJson<WallpaperProject>(File.ReadAllText(projectFile)) ?? project; }
                catch (Exception) { /* fall through to the default preview names */ }
            }

            foreach (string name in new[] { project.file, project.preview, "preview.jpg", "preview.png" })
            {
                string path = InFolder(folder, name);
                if (IsUsableFile(path)) return path;
            }
            return null;
        }

        // Workshop projects are other people's files: never follow a name out of the wallpaper's own folder.
        private static string InFolder(string folder, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(Path.Combine(folder, name));
            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full : null;
        }
#endif

        /// <summary>Just enough JSON to walk Wallpaper Engine's config.json without a package dependency.</summary>
        private static class MiniJson
        {
            public static object Parse(string text)
            {
                int i = 0;
                try { return Value(text.TrimStart('\uFEFF'), ref i); }
                catch (Exception) { return null; }
            }

            public static object Get(object node, params string[] keys)
            {
                foreach (string key in keys)
                {
                    var dict = node as Dictionary<string, object>;
                    if (dict == null || !dict.TryGetValue(key, out node)) return null;
                }
                return node;
            }

            private static void Skip(string s, ref int i)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            private static object Value(string s, ref int i)
            {
                Skip(s, ref i);
                switch (s[i])
                {
                    case '{': return Obj(s, ref i);
                    case '[': return Arr(s, ref i);
                    case '"': return Str(s, ref i);
                    default:
                        int start = i;
                        while (i < s.Length && ",}] \t\r\n".IndexOf(s[i]) < 0) i++;
                        return s.Substring(start, i - start); // number / true / false / null, kept as text
                }
            }

            private static Dictionary<string, object> Obj(string s, ref int i)
            {
                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                i++; // {
                Skip(s, ref i);
                if (s[i] == '}') { i++; return dict; }

                while (true)
                {
                    Skip(s, ref i);
                    string key = Str(s, ref i);
                    Skip(s, ref i);
                    i++; // :
                    dict[key] = Value(s, ref i);
                    Skip(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; // }
                    return dict;
                }
            }

            private static List<object> Arr(string s, ref int i)
            {
                var list = new List<object>();
                i++; // [
                Skip(s, ref i);
                if (s[i] == ']') { i++; return list; }

                while (true)
                {
                    list.Add(Value(s, ref i));
                    Skip(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; // ]
                    return list;
                }
            }

            private static string Str(string s, ref int i)
            {
                var sb = new StringBuilder();
                i++; // opening quote
                while (s[i] != '"')
                {
                    char c = s[i++];
                    if (c != '\\') { sb.Append(c); continue; }

                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                        default: sb.Append(e); break; // \" \\ \/
                    }
                }
                i++; // closing quote
                return sb.ToString();
            }
        }
    }
}
