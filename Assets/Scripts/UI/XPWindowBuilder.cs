using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Runtime chrome for a Windows-XP dialog: dim backdrop, 1px-bordered panel, gradient
    /// titlebar with a red close button, matching the dimensions in
    /// Docs/MainMenu-XP-Desktop.md's TextContentWindow (24px titlebar, 4px radius, 14px body
    /// padding). Built entirely from script - the same disposable-wrapper pattern as
    /// SilenceProtocolHud/CinematicPlayer - so any gameplay-scene overlay can look like it
    /// belongs on the same desktop as the main menu without a prefab.
    /// </summary>
    public static class XPWindowBuilder
    {
        public class Window
        {
            public GameObject Root;
            public RectTransform Panel; // body content area, below the titlebar
            public Button CloseButton;
        }

        public static Window Build(string name, string title, Vector2 size, int sortingOrder, XPTheme theme, System.Action onClose, bool cctv = false)
        {
            var root = new GameObject(name, typeof(RectTransform));

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            root.AddComponent<GraphicRaycaster>();

            CreateImage(root.transform, "Dim", theme.dim, Stretch(), theme).raycastTarget = true;

            var border = CreateImage(root.transform, "Border", theme.border, Anchored(new Vector2(0.5f, 0.5f), size), theme);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(border.transform, false);
            var panelImage = panelGo.GetComponent<Image>();
            panelImage.color = theme.body;
            panelImage.raycastTarget = true;
            var panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = new Vector2(1f, 1f); // 1px border showing all around
            panelRect.offsetMax = new Vector2(-1f, -1f);

            var titlebar = CreateImage(panelRect, "Titlebar", theme.titlebarTop,
                rect =>
                {
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 1f);
                    rect.sizeDelta = new Vector2(0f, theme.titlebarHeight);
                    rect.anchoredPosition = Vector2.zero;
                }, theme);
            var gradient = titlebar.gameObject.AddComponent<UIGradient>();
            gradient.SetColors(theme.titlebarTop, theme.titlebarBottom);

            var titleText = CreateText(titlebar.transform, "Title", 18f, theme,
                rect =>
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = new Vector2(8f, 0f);
                    rect.offsetMax = new Vector2(-30f, 0f);
                });
            titleText.text = title;
            titleText.color = theme.titlebarText;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            titleText.fontStyle = FontStyles.Bold;

            if (cctv)
            {
                BuildRecIndicator(titlebar.transform, theme);
                ApplyCctvTint(panelImage, titlebar, titleText, theme);
            }

            var closeButton = BuildCloseButton(titlebar.transform, theme, onClose);

            if (cctv)
                AddScanlineOverlay(panelRect, theme);

            var window = new Window { Root = root, Panel = panelRect, CloseButton = closeButton };
            return window;
        }

        // Every window in the gameplay scene is a feed on SEC-04's monitor, not a real desktop
        // window - a faint green tint and a pulsing REC dot sell that without touching the
        // camera's own post-processing (Canvas Overlay renders above it and is unaffected).
        private static void ApplyCctvTint(Image panel, Image titlebar, TextMeshProUGUI title, XPTheme theme)
        {
            panel.color = Color.Lerp(panel.color, theme.cctvTint, 0.12f);
            title.color = Color.Lerp(title.color, theme.cctvTint, 0.25f);
        }

        private static void BuildRecIndicator(Transform titlebarParent, XPTheme theme)
        {
            var dot = CreateImage(titlebarParent, "RecDot", theme.recDotColor,
                rect =>
                {
                    rect.anchorMin = new Vector2(0f, 0.5f);
                    rect.anchorMax = new Vector2(0f, 0.5f);
                    rect.pivot = new Vector2(0f, 0.5f);
                    rect.sizeDelta = new Vector2(8f, 8f);
                    rect.anchoredPosition = new Vector2(6f, 0f);
                }, theme);

            var tween = DOTween.To(() => dot.color.a, a => dot.color = new Color(dot.color.r, dot.color.g, dot.color.b, a), 0.25f, 0.6f);
            tween.SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetTarget(dot);
        }

        // Cheap scanline look: a repeating 1px-on/off horizontal stripe texture generated once,
        // tiled over the panel at low opacity. No new art, no shader.
        private static Texture2D _scanlineTexture;

        /// <summary>Adds a low-opacity scanline overlay to any full-screen HUD, not just a window
        /// built by <see cref="Build"/> - used by SilenceProtocolHud/NoiseMeterHud so a threat
        /// encounter reads as the same monitor feed without wrapping it in a titlebar.</summary>
        public static void AddScanlineOverlay(Transform parent, XPTheme theme)
        {
            var overlay = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage));
            overlay.transform.SetParent(parent, false);

            var raw = overlay.GetComponent<RawImage>();
            raw.texture = ResolveScanlineTexture();
            raw.color = new Color(0f, 0f, 0f, theme.scanlineOpacity);
            raw.raycastTarget = false;

            var rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            raw.uvRect = new Rect(0f, 0f, 1f, Screen.height / Mathf.Max(1f, theme.scanlineSpacing));
        }

        private static Texture2D ResolveScanlineTexture()
        {
            if (_scanlineTexture != null) return _scanlineTexture;

            _scanlineTexture = new Texture2D(1, 2, TextureFormat.Alpha8, false) { name = "ScanlineTile", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            _scanlineTexture.SetPixels(new[] { new Color(0f, 0f, 0f, 1f), new Color(0f, 0f, 0f, 0f) });
            _scanlineTexture.Apply();
            return _scanlineTexture;
        }

        private static Button BuildCloseButton(Transform titlebarParent, XPTheme theme, System.Action onClose)
        {
            var go = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(titlebarParent, false);

            var image = go.GetComponent<Image>();
            image.color = theme.closeTop;
            var gradient = go.AddComponent<UIGradient>();
            gradient.SetColors(theme.closeTop, theme.closeBottom);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(20f, 18f);
            rect.anchoredPosition = new Vector2(-4f, 0f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClose != null) button.onClick.AddListener(() => onClose());

            var label = CreateText(go.transform, "X", 16f, theme, Stretch());
            label.text = "X";
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;

            return button;
        }

        /// <summary>One row of a left-hand list, styled like MailWindow's inbox rows: transparent
        /// normal state, blue highlight + white bold text when selected.</summary>
        public static Button BuildVerticalTab(Transform parent, XPTheme theme, string label, int index, int count, float rowHeight, float width, bool active, System.Action onClick)
        {
            var go = new GameObject($"Tab_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = active ? theme.listSelected : theme.listNormal;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, rowHeight);
            rect.anchoredPosition = new Vector2(0f, -index * rowHeight);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var text = CreateText(go.transform, "Label", 16f, theme, rect2 =>
            {
                rect2.anchorMin = Vector2.zero;
                rect2.anchorMax = Vector2.one;
                rect2.offsetMin = new Vector2(10f, 0f);
                rect2.offsetMax = new Vector2(-6f, 0f);
            });
            text.text = label;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = active ? theme.listSelectedText : theme.listNormalText;
            text.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            text.raycastTarget = false;

            return button;
        }

        /// <summary>Thin XP-grey line splitting a list column from its reading pane, matching
        /// MailWindow's own two-pane divider.</summary>
        public static void BuildVerticalDivider(Transform parent, XPTheme theme, float x)
        {
            CreateImage(parent, "Divider", theme.divider, rect =>
            {
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(x, 0f);
            }, theme);
        }

        /// <summary>Plain rectangular XP-grey button for window bodies (Resume, Quit, OK...).</summary>
        public static Button BuildButton(Transform parent, XPTheme theme, string label, System.Action<RectTransform> layout, System.Action onClick)
        {
            var go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.12f);

            layout(go.GetComponent<RectTransform>());

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            var text = CreateText(go.transform, "Label", 18f, theme, Stretch());
            text.text = label;
            text.raycastTarget = false;

            return button;
        }

        public static void RefreshVerticalTab(Button tab, XPTheme theme, bool active)
        {
            var image = tab.GetComponent<Image>();
            if (image != null) image.color = active ? theme.listSelected : theme.listNormal;

            var text = tab.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = active ? theme.listSelectedText : theme.listNormalText;
                text.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        // ── shared primitives ────────────────────────────────────────────────────────────

        public static Image CreateImage(Transform parent, string name, Color color, System.Action<RectTransform> layout, XPTheme theme)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            layout(go.GetComponent<RectTransform>());
            return image;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, XPTheme theme, System.Action<RectTransform> layout)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = theme.ResolvedFont;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = theme.bodyText;
            text.raycastTarget = false;

            layout(go.GetComponent<RectTransform>());
            return text;
        }

        public static System.Action<RectTransform> Stretch() => rect =>
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        };

        public static System.Action<RectTransform> Anchored(Vector2 anchor, Vector2 size) => rect =>
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        };
    }
}
