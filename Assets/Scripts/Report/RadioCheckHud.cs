using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Report
{
    /// <summary>
    /// Minimal runtime-built HUD for Radio Check: a small corner panel (not a full-screen dim like
    /// Silence Protocol - this is a ping, not a threat you're inside of) showing the call line, a
    /// countdown bar, and the hint of what to say. Same "programmer art now, restyle later" spirit
    /// as <see cref="SilenceProtocolHud"/>.
    /// </summary>
    public class RadioCheckHud
    {
        private readonly GameObject _root;
        private readonly Image _panel;
        private readonly TextMeshProUGUI _callText;
        private readonly TextMeshProUGUI _hintText;
        private readonly Image _countdownFill;

        public static RadioCheckHud Create() => new RadioCheckHud();

        private RadioCheckHud()
        {
            _root = new GameObject("RadioCheckHud", typeof(RectTransform));

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 480; // below Silence Protocol's dim (500) so both can be visible at once

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var raycaster = _root.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            _panel = CreateImage(_root.transform, "Panel", new Color(0f, 0f, 0f, 0.55f),
                Anchored(new Vector2(0.86f, 0.86f), new Vector2(360f, 110f)));

            _callText = CreateText(_panel.transform, "Call", 24f,
                Anchored(new Vector2(0.5f, 0.72f), new Vector2(330f, 40f)));
            _callText.alignment = TextAlignmentOptions.Center;
            _callText.color = new Color(0.9f, 0.95f, 1f, 0.95f);
            _callText.enableWordWrapping = true;

            _hintText = CreateText(_panel.transform, "Hint", 18f,
                Anchored(new Vector2(0.5f, 0.42f), new Vector2(330f, 30f)));
            _hintText.alignment = TextAlignmentOptions.Center;
            _hintText.color = new Color(0.7f, 0.85f, 0.7f, 0.85f);

            var barBg = CreateImage(_panel.transform, "BarBg", new Color(1f, 1f, 1f, 0.15f),
                Anchored(new Vector2(0.5f, 0.16f), new Vector2(320f, 12f)));
            _countdownFill = CreateImage(barBg.transform, "BarFill", new Color(0.3f, 0.8f, 0.9f, 0.9f), Stretch());
            _countdownFill.type = Image.Type.Filled;
            _countdownFill.fillMethod = Image.FillMethod.Horizontal;
            _countdownFill.fillAmount = 1f;
        }

        public void SetCall(string text)
        {
            if (_callText != null) _callText.text = text;
        }

        public void SetHint(string text)
        {
            if (_hintText != null) _hintText.text = text;
        }

        public void SetCountdown(float remaining, float total)
        {
            if (_countdownFill == null) return;

            float t = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
            _countdownFill.fillAmount = t;
            _countdownFill.color = t < 0.25f
                ? new Color(0.9f, 0.25f, 0.2f, 0.95f)
                : new Color(0.3f, 0.8f, 0.9f, 0.9f);
        }

        public void FlashResult(bool good)
        {
            if (_panel != null)
                _panel.color = good ? new Color(0.1f, 0.4f, 0.15f, 0.7f) : new Color(0.45f, 0.05f, 0.05f, 0.7f);
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
        }

        // ── tiny builder helpers (mirrors SilenceProtocolHud) ──────────────────────────────

        private static Image CreateImage(Transform parent, string name, Color color, System.Action<RectTransform> layout)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            layout(go.GetComponent<RectTransform>());
            return image;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, System.Action<RectTransform> layout)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.raycastTarget = false;

            layout(go.GetComponent<RectTransform>());
            return text;
        }

        private static System.Action<RectTransform> Stretch() => rect =>
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        };

        private static System.Action<RectTransform> Anchored(Vector2 anchor, Vector2 size) => rect =>
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        };
    }
}
