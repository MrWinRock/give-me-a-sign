using DG.Tweening;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Whisper
{
    /// <summary>
    /// Runtime-built bottom-left NOISE bar for <see cref="NoiseMeter"/>, plus a warning line that
    /// flashes when the meter fills. No scene wiring, same pattern as SilenceProtocolHud. Tahoma
    /// via XPTheme so its label reads as the same monitor feed as everything else.
    /// </summary>
    public class NoiseMeterHud
    {
        private readonly XPTheme _theme = XPTheme.Load();

        private const float PulseFrom = 0.9f;
        private const float DangerFraction = 0.8f;

        private static readonly Color QuietColor = new Color(0.75f, 0.8f, 0.75f);
        private static readonly Color WarnColor = new Color(0.95f, 0.75f, 0.2f);
        private static readonly Color DangerColor = new Color(0.9f, 0.15f, 0.15f);

        private readonly GameObject _root;
        private readonly Image _fill;
        private readonly TextMeshProUGUI _warning;

        private Tween _pulse;
        private Tween _warningFade;

        public static NoiseMeterHud Create() => new NoiseMeterHud();

        private NoiseMeterHud()
        {
            // Not DontDestroyOnLoad - dies with the gameplay scene if NoiseMeter never gets to clean up.
            _root = new GameObject("NoiseMeterHud", typeof(RectTransform));

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400; // under SilenceProtocolHud (500) and the report window

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var label = CreateText(_root.transform, "Label", 20f, BottomLeft(new Vector2(32f, 52f), new Vector2(200f, 26f)));
            label.text = "NOISE";
            label.color = new Color(1f, 1f, 1f, 0.7f);
            label.font = _theme.ResolvedFont;

            var background = CreateImage(_root.transform, "MeterBg", new Color(1f, 1f, 1f, 0.15f),
                BottomLeft(new Vector2(32f, 32f), new Vector2(280f, 14f)));

            _fill = CreateImage(background.transform, "MeterFill", QuietColor, Stretch());
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillAmount = 0f;
            SetAlpha(_fill, PulseFrom);

            _warning = CreateText(_root.transform, "Warning", 26f, BottomLeft(new Vector2(32f, 82f), new Vector2(900f, 36f)));
            _warning.color = new Color(1f, 0.85f, 0.85f, 0f);
            _warning.font = _theme.ResolvedFont;
        }

        public void SetLevel(float normalized)
        {
            if (_fill == null) return;

            normalized = Mathf.Clamp01(normalized);
            _fill.fillAmount = normalized;

            var color = normalized >= DangerFraction ? DangerColor : normalized >= 0.5f ? WarnColor : QuietColor;
            _fill.color = new Color(color.r, color.g, color.b, _fill.color.a);

            bool danger = normalized >= DangerFraction;
            if (danger && _pulse == null)
            {
                _pulse = _fill.DOFade(0.35f, 0.35f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
            else if (!danger && _pulse != null)
            {
                KillPulse();
                SetAlpha(_fill, PulseFrom);
            }
        }

        public void ShowWarning(string text)
        {
            if (_warning == null) return;

            _warningFade?.Kill();
            _warning.text = text;
            SetAlpha(_warning, 1f);

            _warningFade = DOTween.To(() => _warning.color.a, a => SetAlpha(_warning, a), 0f, 1.2f)
                .SetDelay(2f)
                .SetUpdate(true);
        }

        public void Destroy()
        {
            KillPulse();
            _warningFade?.Kill();
            _warningFade = null;

            if (_root != null) Object.Destroy(_root);
        }

        private void KillPulse()
        {
            _pulse?.Kill();
            _pulse = null;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var c = graphic.color;
            graphic.color = new Color(c.r, c.g, c.b, alpha);
        }

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
            text.alignment = TextAlignmentOptions.BottomLeft;
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

        private static System.Action<RectTransform> BottomLeft(Vector2 position, Vector2 size) => rect =>
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        };
    }
}
