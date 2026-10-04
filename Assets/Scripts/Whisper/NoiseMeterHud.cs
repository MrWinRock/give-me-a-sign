using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Whisper
{
    /// <summary>
    /// Runtime-built bottom-left volume slider for <see cref="NoiseMeter"/>: a track split into
    /// quiet / normal / loud zones with a bar that slides right as the player gets louder. The zone
    /// the current threat asks for stays bright; the others dim. No words say "loud" or "quiet".
    /// </summary>
    public class NoiseMeterHud
    {
        private static readonly Color QuietColor = new Color(0.35f, 0.85f, 0.45f);
        private static readonly Color NormalColor = new Color(0.95f, 0.8f, 0.25f);
        private static readonly Color LoudColor = new Color(0.9f, 0.2f, 0.2f);

        private const float TrackWidth = 360f;
        private const float TrackHeight = 16f;

        private readonly GameObject _root;
        private readonly CanvasGroup _group;
        private readonly Image _zoneQuiet;
        private readonly Image _zoneNormal;
        private readonly Image _zoneLoud;
        private readonly Image _fill;
        private readonly RectTransform _marker;

        private float _whisperEdge = 0.4f;
        private float _shoutEdge = 0.75f;

        public static NoiseMeterHud Create() => new NoiseMeterHud();

        private NoiseMeterHud()
        {
            // Not DontDestroyOnLoad - dies with the gameplay scene if NoiseMeter never gets to clean up.
            _root = new GameObject("NoiseMeterHud", typeof(RectTransform));

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _group = _root.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var theme = XPTheme.Load();
            var label = CreateText(_root.transform, "Label", "MIC", 18f, new Vector2(32f, 54f), new Vector2(100f, 24f));
            label.color = new Color(1f, 1f, 1f, 0.7f);
            label.font = theme.ResolvedFont;

            var track = NewImage(_root.transform, "Track", new Color(0f, 0f, 0f, 0.55f));
            Place(track.rectTransform, new Vector2(32f, 32f), new Vector2(TrackWidth + 4f, TrackHeight + 4f));

            var inner = NewImage(track.transform, "Inner", Color.clear);
            Stretch(inner.rectTransform, 2f);

            _zoneQuiet = NewImage(inner.transform, "ZoneQuiet", QuietColor);
            _zoneNormal = NewImage(inner.transform, "ZoneNormal", NormalColor);
            _zoneLoud = NewImage(inner.transform, "ZoneLoud", LoudColor);

            _fill = NewImage(inner.transform, "Fill", Color.white);
            _fill.rectTransform.anchorMin = new Vector2(0f, 0.3f);
            _fill.rectTransform.anchorMax = new Vector2(0f, 0.7f);
            _fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _fill.rectTransform.offsetMin = Vector2.zero;
            _fill.rectTransform.offsetMax = Vector2.zero;

            var markerImage = NewImage(inner.transform, "Marker", Color.white);
            _marker = markerImage.rectTransform;
            _marker.anchorMin = new Vector2(0f, -0.35f);
            _marker.anchorMax = new Vector2(0f, 1.35f);
            _marker.pivot = new Vector2(0.5f, 0.5f);
            _marker.sizeDelta = new Vector2(4f, 0f);
            _marker.anchoredPosition = Vector2.zero;

            LayoutZones();
            SetRequirement(NoiseMeter.Requirement.None);
            SetMicOpen(false);
        }

        public void SetZones(float whisperEdge, float shoutEdge)
        {
            _whisperEdge = Mathf.Clamp01(whisperEdge);
            _shoutEdge = Mathf.Clamp(shoutEdge, _whisperEdge, 1f);
            LayoutZones();
        }

        public void SetLevel(float position)
        {
            position = Mathf.Clamp01(position);

            _fill.rectTransform.anchorMax = new Vector2(position, 0.7f);
            _fill.color = position >= _shoutEdge ? LoudColor : position > _whisperEdge ? NormalColor : QuietColor;

            _marker.anchorMin = new Vector2(position, -0.35f);
            _marker.anchorMax = new Vector2(position, 1.35f);
            _marker.anchoredPosition = Vector2.zero;
        }

        // The zone the current threat wants stays bright; the rest dim down.
        public void SetRequirement(NoiseMeter.Requirement requirement)
        {
            const float lit = 0.85f, base_ = 0.35f, dim = 0.12f;

            switch (requirement)
            {
                case NoiseMeter.Requirement.StayQuiet:
                    SetAlpha(_zoneQuiet, lit); SetAlpha(_zoneNormal, dim); SetAlpha(_zoneLoud, dim);
                    break;
                case NoiseMeter.Requirement.BeLoud:
                    SetAlpha(_zoneQuiet, dim); SetAlpha(_zoneNormal, dim); SetAlpha(_zoneLoud, lit);
                    break;
                default:
                    SetAlpha(_zoneQuiet, base_); SetAlpha(_zoneNormal, base_); SetAlpha(_zoneLoud, base_);
                    break;
            }
        }

        // Faded while the mic is closed: nothing is being measured.
        public void SetMicOpen(bool open)
        {
            if (_group != null) _group.alpha = open ? 1f : 0.35f;
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
        }

        private void LayoutZones()
        {
            SetZone(_zoneQuiet, 0f, _whisperEdge);
            SetZone(_zoneNormal, _whisperEdge, _shoutEdge);
            SetZone(_zoneLoud, _shoutEdge, 1f);
        }

        private static void SetZone(Image zone, float from, float to)
        {
            var rect = zone.rectTransform;
            rect.anchorMin = new Vector2(from, 0f);
            rect.anchorMax = new Vector2(to, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var c = graphic.color;
            graphic.color = new Color(c.r, c.g, c.b, alpha);
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, Vector2 position, Vector2 dimensions)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.BottomLeft;
            tmp.raycastTarget = false;

            Place(tmp.rectTransform, position, dimensions);
            return tmp;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
