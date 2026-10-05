using System.IO;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
using System.Text;
#endif

namespace Report
{
    /// <summary>
    /// Reads the player's own Windows desktop wallpaper for the Camera Betrayal "Mirror" glitch. Local only: the
    /// image is decoded into a texture for the length of the glitch, never saved or sent anywhere, and the
    /// caller destroys it afterwards. The player can switch it off in Control Panel (PrefKey).
    /// </summary>
    public static class PlayerWallpaper
    {
        public const string PrefKey = "Opt_MirrorWallpaper";
        private const long MaxFileBytes = 60L * 1024 * 1024;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint SpiGetDeskWallpaper = 0x0073;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SystemParametersInfo(uint action, uint param, StringBuilder buffer, uint winIni);
#endif

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

        // Returns a texture the caller must Destroy, or null (unsupported platform, no wallpaper, BMP/other format).
        public static Texture2D TryLoad()
        {
            string path = GetPath();
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length == 0 || info.Length > MaxFileBytes) return null;

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "PlayerWallpaper", hideFlags = HideFlags.HideAndDontSave };
                if (!texture.LoadImage(File.ReadAllBytes(path), markNonReadable: true)) // PNG/JPG only
                {
                    Object.Destroy(texture);
                    return null;
                }
                return texture;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"PlayerWallpaper: could not read the wallpaper ({e.GetType().Name}).");
                return null;
            }
        }

        private static string GetPath()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var buffer = new StringBuilder(520);
            return SystemParametersInfo(SpiGetDeskWallpaper, (uint)buffer.Capacity, buffer, 0) ? buffer.ToString() : null;
#else
            return null;
#endif
        }
    }
}
