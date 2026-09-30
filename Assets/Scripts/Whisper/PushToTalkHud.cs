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
        private Tween _pulse;

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

            SetTalking(false);
        }

        public void SetTalking(bool talking)
        {
            if (_label == null) return;

            _pulse?.Kill();
            _pulse = null;

            _label.text = talking ? "REC" : "[V] MIC";
            _label.color = talking ? TalkColor : IdleColor;
            _dot.color = talking ? TalkColor : new Color(1f, 1f, 1f, 0.2f);

            if (talking)
            {
                _pulse = _dot.DOFade(0.25f, 0.4f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true);
            }
        }

        public void Destroy()
        {
            _pulse?.Kill();
            _pulse = null;
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
