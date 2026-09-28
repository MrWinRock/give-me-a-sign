using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Single source of truth for the game's Windows-XP look, shared by every runtime-built
    /// window (Field Manual, and anything after it) so they all read as the same desktop as
    /// the main menu instead of each inventing its own blue. Colours match
    /// Docs/MainMenu-XP-Desktop.md exactly - XPPalette.Hex() defaults, editable here without
    /// touching code. One asset: Resources/UI/XPTheme.
    /// </summary>
    [CreateAssetMenu(fileName = "XPTheme", menuName = "Give Me A Sign/UI/XP Theme")]
    public class XPTheme : ScriptableObject
    {
        private const string ResourcePath = "UI/XPTheme";

        [Header("Font")]
        [Tooltip("Falls back to TMP_Settings.defaultFontAsset if left empty.")]
        public TMP_FontAsset font;

        [Header("Titlebar")]
        public Color titlebarTop = XPPalette.Hex("#2B6EDE");
        public Color titlebarBottom = XPPalette.Hex("#1854BE");
        public Color titlebarText = Color.white;
        public float titlebarHeight = 24f;

        [Header("Window")]
        public Color border = XPPalette.Hex("#003C74");
        public Color body = XPPalette.Hex("#ECE9D8");
        public Color dim = new Color(0f, 0f, 0f, 0.55f);
        public Color bodyText = XPPalette.Hex("#000000");
        public Color mutedText = XPPalette.Hex("#5A5A5A");
        public Color accentText = XPPalette.Hex("#0A246A");

        [Header("Close Button")]
        public Color closeTop = XPPalette.Hex("#E87A7A");
        public Color closeBottom = XPPalette.Hex("#B02020");

        [Header("List rows (matches MailWindow's inbox list)")]
        [Tooltip("Selected-row highlight - same blue as MailWindow.rowSelectedColor.")]
        public Color listSelected = XPPalette.Hex("#316AC5", 0.35f);
        public Color listNormal = new Color(1f, 1f, 1f, 0f);
        public Color listSelectedText = Color.white;
        public Color listNormalText = XPPalette.Hex("#000000");
        [Tooltip("Row/pane divider line - matches MailWindow's Divider colour.")]
        public Color divider = XPPalette.Hex("#D5D8C4");

        [Header("CCTV Overlay (Gameplay scene only)")]
        [Tooltip("Every window opened during the night is a feed on SEC-04's monitor, not a real OS window - this tints and scans it.")]
        public Color cctvTint = XPPalette.Hex("#D8F0E0");
        [Range(0f, 0.3f)] public float scanlineOpacity = 0.10f;
        public float scanlineSpacing = 3f;
        public Color recDotColor = XPPalette.Hex("#CC2222");

        private static XPTheme _cached;

        public static XPTheme Load()
        {
            if (_cached != null) return _cached;

            _cached = Resources.Load<XPTheme>(ResourcePath);
            if (_cached == null)
            {
                Debug.LogWarning($"XPTheme: no Resources/{ResourcePath} asset - using built-in defaults.");
                _cached = CreateInstance<XPTheme>();
            }
            return _cached;
        }

        public TMP_FontAsset ResolvedFont => font != null ? font : TMP_Settings.defaultFontAsset;
    }
}
