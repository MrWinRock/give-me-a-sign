using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Flow
{
    /// <summary>
    /// Sprint 6, S-606. Full-screen fade + a cause-of-death line, shown for
    /// GameFlowManager.delayAfterDeath seconds before the Result scene loads - replaces the
    /// previous "just wait, then cut" pause (delayAfterDeath used to just sit there blank) with
    /// something that actually reads as an ending. Runtime-built, same disposable-wrapper pattern
    /// as SilenceProtocolHud/RadioCheckHud/CameraFeedHud/SignHintHud - no scene wiring needed.
    /// </summary>
    public class DeathSequenceHud
    {
        private readonly GameObject _root;
        private readonly Image _fade;
        private readonly TextMeshProUGUI _causeText;

        private Sequence _sequence;

        public static DeathSequenceHud Create() => new DeathSequenceHud();

        private DeathSequenceHud()
        {
            _root = new GameObject("DeathSequenceHud", typeof(RectTransform));

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000; // above every other gameplay HUD - this is the last thing the player sees

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var raycaster = _root.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            _fade = CreateImage(_root.transform, "Fade", new Color(0f, 0f, 0f, 0f));

            var textGo = new GameObject("Cause", typeof(RectTransform));
            textGo.transform.SetParent(_root.transform, false);

            _causeText = textGo.AddComponent<TextMeshProUGUI>();
            _causeText.font = TMP_Settings.defaultFontAsset;
            _causeText.fontSize = 42f;
            _causeText.alignment = TextAlignmentOptions.Center;
            _causeText.color = new Color(0.85f, 0.15f, 0.15f, 0f);
            _causeText.raycastTarget = false;
            _causeText.text = string.Empty;

            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1200f, 120f);
            rect.anchoredPosition = Vector2.zero;
        }

        public Tween PlayFadeIn(string cause, float duration)
        {
            if (_causeText != null) _causeText.text = cause;

            _sequence?.Kill();
            _sequence = DOTween.Sequence().SetUpdate(true);

            if (_fade != null)
                _sequence.Join(_fade.DOFade(1f, duration).SetEase(Ease.InQuad));

            if (_causeText != null)
            {
                _sequence.Join(DOTween.To(
                    () => _causeText.color.a,
                    a => _causeText.color = new Color(_causeText.color.r, _causeText.color.g, _causeText.color.b, a),
                    1f,
                    duration).SetEase(Ease.InQuad));
            }

            return _sequence;
        }

        public void Destroy()
        {
            // Killed first: a tween still writing into a destroyed Image/TMP is a null-ref waiting
            // to happen, and DOTween has no way to know the objects went away.
            _sequence?.Kill();
            _sequence = null;

            if (_root != null) Object.Destroy(_root);
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return image;
        }
    }
}
