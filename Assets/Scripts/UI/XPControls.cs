using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [System.Serializable]
    public class XPControlStyle
    {
        [Header("Window")]
        public Color windowBody = XPPalette.Hex("#ECE9D8");
        public Color windowBorder = XPPalette.Hex("#003C74");
        public Color titlebarTop = XPPalette.Hex("#1A5FB0");
        public Color titlebarBottom = XPPalette.Hex("#0A3C8A");
        public Color closeButton = XPPalette.Hex("#CC2222");

        [Header("Controls")]
        public Color accent = XPPalette.Hex("#316AC5");
        public Color groupBorder = XPPalette.Hex("#ACA899");
        public Color buttonFill = XPPalette.Hex("#E6E3D3");
        public Color buttonBorder = XPPalette.Hex("#003C74");
        [Min(1f)] public float defaultButtonInnerBorder = 2f;

        [Header("Text")]
        public Color label = Color.black;
        public Color value = XPPalette.Hex("#0A246A");
        public Color badgeText = Color.white;
        public float headerSize = 14f;
        public float labelSize = 12f;
        public float smallSize = 11f;
    }

    /// <summary>Runtime-built classic XP controls (button, trackbar, group box) for windows made with XPWindowBuilder.</summary>
    public static class XPControls
    {
        public static Image Img(Transform parent, string name, Color color, System.Action<RectTransform> layout)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            layout?.Invoke(go.GetComponent<RectTransform>());
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, string text, float size, Color color, bool bold, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static void SetPreferred(GameObject go, float width = -1f, float height = -1f, float flexibleWidth = -1f)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            if (width >= 0f) element.preferredWidth = width;
            if (height >= 0f) element.preferredHeight = height;
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
        }

        // Standard XP button; the default one gets the blue inner border and a bold label.
        public static Button CreateButton(Transform parent, XPControlStyle style, TMP_FontAsset font, string label, float width, float height, bool isDefault, System.Action onClick)
        {
            var outer = Img(parent, $"Button_{label}", style.buttonBorder, null);
            outer.raycastTarget = true;
            SetPreferred(outer.gameObject, width, height);

            float inset = 1f;
            if (isDefault)
            {
                Img(outer.transform, "DefaultBorder", style.accent, rect => Stretch(rect, 1f));
                inset += style.defaultButtonInnerBorder;
            }

            var fill = Img(outer.transform, "Fill", style.buttonFill, rect => Stretch(rect, inset));
            fill.raycastTarget = false;

            var text = Text(outer.transform, "Label", font, label, style.labelSize, style.label, isDefault, TextAlignmentOptions.Center);
            Stretch(text.rectTransform);

            var button = outer.gameObject.AddComponent<Button>();
            button.targetGraphic = outer;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 0.9f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.6f, 0.74f, 1f, 1f);
            colors.fadeDuration = 0.05f; // CrossFadeColor ignores timeScale
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            return button;
        }

        // Classic XP trackbar: sunken 4px groove and a small rectangular thumb.
        public static Slider CreateSlider(Transform parent, XPControlStyle style, float min, float max, float height, float thumbWidth, float thumbHeight)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            SetPreferred(go, height: height, flexibleWidth: 1f);

            var groove = Img(go.transform, "Groove", style.groupBorder, rect =>
            {
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.sizeDelta = new Vector2(0f, 4f);
            });
            Img(groove.transform, "GrooveInner", Color.white, rect => Stretch(rect, 1f));

            var area = new GameObject("HandleArea", typeof(RectTransform));
            area.transform.SetParent(go.transform, false);
            var areaRect = area.GetComponent<RectTransform>();
            // Slider stretches the handle to the area's height, so the area itself is thumbHeight tall.
            areaRect.anchorMin = new Vector2(0f, 0.5f);
            areaRect.anchorMax = new Vector2(1f, 0.5f);
            areaRect.sizeDelta = new Vector2(-thumbWidth, thumbHeight);
            areaRect.anchoredPosition = Vector2.zero;

            var thumb = Img(area.transform, "Handle", style.buttonBorder, rect => rect.sizeDelta = new Vector2(thumbWidth, 0f));
            thumb.raycastTarget = true;
            Img(thumb.transform, "HandleFill", style.buttonFill, rect => Stretch(rect, 1f));

            var slider = go.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;
            slider.handleRect = thumb.rectTransform;
            slider.targetGraphic = thumb;
            var colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 0.9f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.6f, 0.74f, 1f, 1f);
            colors.fadeDuration = 0.05f;
            slider.colors = colors;
            return slider;
        }

        // WinForms GroupBox: bordered frame with the legend sitting on the top line. Returns the content parent (vertical layout).
        public static RectTransform CreateGroupBox(Transform parent, XPControlStyle style, TMP_FontAsset font, string legend, float padding, float spacing)
        {
            var root = new GameObject("GroupBox", typeof(RectTransform), typeof(VerticalLayoutGroup));
            root.transform.SetParent(parent, false);

            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)padding, (int)padding, (int)(padding + style.labelSize * 0.5f + 4f), (int)padding);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            float legendHalf = style.labelSize * 0.5f + 2f;

            var border = Img(root.transform, "Border", style.groupBorder, rect =>
            {
                Stretch(rect);
                rect.offsetMax = new Vector2(0f, -legendHalf);
            });
            border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Img(border.transform, "Inner", style.windowBody, rect => Stretch(rect, 1f));

            var legendBg = Img(root.transform, "LegendBg", style.windowBody, rect =>
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(padding - 2f, 0f);
            });
            legendBg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            var legendText = Text(legendBg.transform, "Legend", font, legend, style.labelSize, style.label, false, TextAlignmentOptions.Center);
            Stretch(legendText.rectTransform);
            legendText.ForceMeshUpdate();
            legendBg.rectTransform.sizeDelta = new Vector2(legendText.preferredWidth + 8f, legendHalf * 2f);

            return root.GetComponent<RectTransform>();
        }
    }
}
