using DG.Tweening;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Whisper
{
    /// <summary>Runtime-built bottom-right mic indicator for <see cref="GlobalPushToTalk"/>: dim "[V] MIC" idle, pulsing red "REC" while talking.</summary>
    public class PushToTalkHud
    {
        private static readonly Color IdleColor = new Color(1f, 1f, 1f, 0.45f);
        private static readonly Color TalkColor = new Color(0.9f, 0.15f, 0.15f, 1f);

        private readonly GameObject _root;
        private readonly Image _dot;
        private readonly TextMeshProUGUI _label;
        private readonly TextMeshProUGUI _status;
        private readonly TextMeshProUGUI _hint;
        private Tween _pulse;
        private Tween _statusFade;
        private Tween _hintBlink;

        public static PushToTalkHud Create() => new PushToTalkHud();

        private PushToTalkHud()
        {
            _root = new GameObject("PushToTalkHud", typeof(RectTransform));

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dotGo.transform.SetParent(_root.transform, false);
            _dot = dotGo.GetComponent<Image>();
            _dot.raycastTarget = false;
            Anchor((RectTransform)dotGo.transform, new Vector2(-190f, 34f), new Vector2(16f, 16f));

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(_root.transform, false);
            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.font = XPTheme.Load().ResolvedFont;
            _label.fontSize = 20f;
            _label.alignment = TextAlignmentOptions.BottomRight;
            _label.raycastTarget = false;
            Anchor((RectTransform)labelGo.transform, new Vector2(-32f, 28f), new Vector2(150f, 28f));

            var statusGo = new GameObject("Status", typeof(RectTransform));
            statusGo.transform.SetParent(_root.transform, false);
            _status = statusGo.AddComponent<TextMeshProUGUI>();
            _status.font = XPTheme.Load().ResolvedFont;
            _status.fontSize = 26f;
            _status.alignment = TextAlignmentOptions.BottomRight;
            _status.raycastTarget = false;
            _status.color = new Color(1f, 1f, 1f, 0f);
            Anchor((RectTransform)statusGo.transform, new Vector2(-32f, 60f), new Vector2(700f, 36f));

            var hintGo = new GameObject("Hint", typeof(RectTransform));
            hintGo.transform.SetParent(_root.transform, false);
            _hint = hintGo.AddComponent<TextMeshProUGUI>();
            _hint.font = XPTheme.Load().ResolvedFont;
            _hint.fontSize = 22f;
            _hint.alignment = TextAlignmentOptions.BottomRight;
            _hint.raycastTarget = false;
            _hint.color = new Color(1f, 0.9f, 0.6f, 0.9f);
            Anchor((RectTransform)hintGo.transform, new Vector2(-32f, 96f), new Vector2(700f, 30f));

            SetTalking(false);
        }

        // The stealth countdown re-sends its text every second - only restart the flash when the blink state changes.
        public void SetHint(string text, bool blink = false)
        {
            if (_hint == null) return;

            _hint.text = text ?? "";

            bool shouldBlink = blink && !string.IsNullOrEmpty(text);
            if (shouldBlink == (_hintBlink != null)) return;

            _hintBlink?.Kill();
            _hintBlink = null;
            _hint.alpha = 1f;
            if (shouldBlink) _hintBlink = TextBlink.Start(_hint);
        }

        public void SetTalking(bool talking, string suffix = null)
        {
            if (_label == null) return;

            _pulse?.Kill();
            _pulse = null;

            _label.text = talking ? (string.IsNullOrEmpty(suffix) ? "REC" : $"REC {suffix}") : "[V] MIC";
            _label.color = talking ? TalkColor : IdleColor;
            _dot.color = talking ? TalkColor : new Color(1f, 1f, 1f, 0.2f);

            if (talking)
            {
                _pulse = _dot.DOFade(0.25f, 0.4f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetLink(_dot.gameObject);
            }
        }

        public void ShowStatus(string text, Color color, bool blink = false)
        {
            if (_status == null) return;

            _statusFade?.Kill();
            _status.text = text;
            _status.color = color;

            if (blink)
            {
                // Flash for ~2.5s, then fade out like a normal message.
                var seq = DOTween.Sequence().SetUpdate(true).SetLink(_status.gameObject);
                seq.Append(DOTween.To(() => _status.alpha, a => _status.alpha = a, 0.15f, TextBlink.DefaultPeriod)
                    .SetEase(Ease.InOutSine).SetLoops(8, LoopType.Yoyo));
                seq.Append(DOTween.To(() => _status.alpha, a => _status.alpha = a, 0f, 0.5f));
                _statusFade = seq;
                return;
            }

            _statusFade = DOTween.To(() => _status.color.a, a => _status.color = new Color(color.r, color.g, color.b, a), 0f, 0.8f)
                .SetDelay(1.5f)
                .SetUpdate(true)
                .SetLink(_status.gameObject);
        }

        public void Destroy()
        {
            _pulse?.Kill();
            _pulse = null;
            _hintBlink?.Kill();
            _hintBlink = null;
            _statusFade?.Kill();
            _statusFade = null;
            if (_root != null) Object.Destroy(_root);
        }

        private static void Anchor(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
