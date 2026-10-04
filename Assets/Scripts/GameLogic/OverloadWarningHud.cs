using DG.Tweening;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>Runtime-built top-centre warning for <see cref="AnomalyOverloadWatcher"/>: a message plus a seconds-left countdown while too many anomalies are loose.</summary>
    public class OverloadWarningHud
    {
        private static readonly Color WarnColor = new Color(1f, 0.3f, 0.25f);

        private readonly GameObject _root;
        private readonly TextMeshProUGUI _title;
        private readonly TextMeshProUGUI _countdown;
        private Tween _flash;
        private int _lastSeconds = -1;

        public static OverloadWarningHud Create() => new OverloadWarningHud();

        private OverloadWarningHud()
        {
            _root = new GameObject("OverloadWarningHud", typeof(RectTransform));

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var font = XPTheme.Load().ResolvedFont;
            _title = CreateText("Title", 34f, new Vector2(0f, -70f), new Vector2(1100f, 48f), font);
            _countdown = CreateText("Countdown", 64f, new Vector2(0f, -122f), new Vector2(600f, 80f), font);

            _root.SetActive(false);
        }

        public void Show(int unresolved, int limit)
        {
            _root.SetActive(true);
            _lastSeconds = -1;
            _title.text = PlayerMessages.Text(MessageId.OverloadWarning, unresolved, limit);

            _flash?.Kill();
            _flash = null;
            _title.alpha = 1f;
            if (PlayerMessages.Blink(MessageId.OverloadWarning))
                _flash = TextBlink.Start(_title);
        }

        public void SetSecondsLeft(float seconds)
        {
            int whole = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            if (whole == _lastSeconds) return;

            _lastSeconds = whole;
            _countdown.text = whole.ToString();
        }

        public void Hide()
        {
            _flash?.Kill();
            _flash = null;
            if (_root != null) _root.SetActive(false);
        }

        public void Destroy()
        {
            _flash?.Kill();
            _flash = null;
            if (_root != null) Object.Destroy(_root);
        }

        private TextMeshProUGUI CreateText(string name, float size, Vector2 position, Vector2 dimensions, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.color = WarnColor;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;
            return text;
        }
    }
}
