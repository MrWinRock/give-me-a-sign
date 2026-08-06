using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Whisper
{
    /// <summary>
    /// Minimal runtime-built one-line popup for HL-7's hint text ("Give me a sign"). Same
    /// disposable-wrapper pattern as SilenceProtocolHud/RadioCheckHud/CameraFeedHud - no scene
    /// wiring, built entirely from script.
    /// </summary>
    public class SignHintHud
    {
        private readonly GameObject _root;
        private readonly TextMeshProUGUI _text;

        public static SignHintHud Create() => new SignHintHud();

        private SignHintHud()
        {
            _root = new GameObject("SignHintHud", typeof(RectTransform));

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 490;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var raycaster = _root.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);

            _text = go.AddComponent<TextMeshProUGUI>();
            _text.font = TMP_Settings.defaultFontAsset;
            _text.fontSize = 32f;
            _text.alignment = TextAlignmentOptions.Center;
            _text.color = new Color(0.9f, 0.85f, 0.6f, 0.95f);
            _text.raycastTarget = false;
            _text.text = string.Empty;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.22f);
            rect.anchorMax = new Vector2(0.5f, 0.22f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 60f);
            rect.anchoredPosition = Vector2.zero;
        }

        public void SetText(string text)
        {
            if (_text != null) _text.text = text;
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
        }
    }
}
