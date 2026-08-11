using UnityEngine;

namespace UI
{
    /// <summary>
    /// Hex-to-Color helper used as the DEFAULT value of serialized Color fields, so the
    /// palette reads as hex in code ("#316AC5") while every colour stays editable in the
    /// Inspector. Nothing here is authoritative at runtime - once a component is serialized
    /// the Inspector value wins.
    /// </summary>
    public static class XPPalette
    {
        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        public static Color Hex(string hex, float alpha)
        {
            var c = Hex(hex);
            c.a = alpha;
            return c;
        }
    }
}
