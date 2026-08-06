using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Report
{
    /// <summary>
    /// Minimal runtime-built HUD for Silence Protocol: a screen dim, a VU meter with a danger
    /// line, and three strike pips. Built entirely from script - no scene wiring, no prefab -
    /// the same way DemonAnomaly builds its video overlay, so dropping SilenceProtocolHaunt into
    /// the scene just works.
    ///
    /// Deliberately plain "programmer art". This is a Sprint 4 systems deliverable, not a Sprint
    /// 3 content one - restyle in-editor (or replace with a real prefab) once the mechanic is
    /// confirmed fun in a playtest.
    /// </summary>
    public class SilenceProtocolHud
    {
        private readonly GameObject _root;
        private readonly Image _dim;
        private readonly Image _meterFill;
        private readonly TextMeshProUGUI _instructionText;
        private readonly Image[] _strikePips;

        public static SilenceProtocolHud Create() => new SilenceProtocolHud();

        private SilenceProtocolHud()
        {
            // Deliberately NOT DontDestroyOnLoad: a normal scene object dies automatically if the
            // gameplay scene unloads before EndEncounter's explicit Destroy() runs (e.g. an
            // exception mid-encounter), instead of leaking a HUD into whatever loads next.
            _root = new GameObject("SilenceProtocolHud", typeof(RectTransform));

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above gameplay; the Incident Report window (its own Canvas) sits above this too

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var raycaster = _root.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false; // never blocks clicks on anomalies or UI behind it

            _dim = CreateImage(_root.transform, "Dim", new Color(0f, 0f, 0f, 0.5f), Stretch());

            var meterBg = CreateImage(_root.transform, "MeterBg", new Color(1f, 1f, 1f, 0.15f),
                Anchored(new Vector2(0.5f, 0.08f), new Vector2(420f, 22f)));

            _meterFill = CreateImage(meterBg.transform, "MeterFill", new Color(0.3f, 0.9f, 0.4f, 0.9f), Stretch());
            _meterFill.type = Image.Type.Filled;
            _meterFill.fillMethod = Image.FillMethod.Horizontal;
            _meterFill.fillAmount = 0f;

            var marker = CreateImage(meterBg.transform, "DangerMarker", new Color(0.9f, 0.15f, 0.15f, 0.9f),
                rect =>
                {
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(3f, 0f);
                    rect.anchoredPosition = Vector2.zero;
                });
            marker.raycastTarget = false;

            _instructionText = CreateText(_root.transform, "Instruction", 30f,
                Anchored(new Vector2(0.5f, 0.14f), new Vector2(760f, 44f)));
            _instructionText.alignment = TextAlignmentOptions.Center;
            _instructionText.color = new Color(1f, 0.94f, 0.94f, 0.95f);
            _instructionText.text = string.Empty;

            _strikePips = new Image[3];
            for (int i = 0; i < _strikePips.Length; i++)
            {
                float x = 0.5f + (i - 1) * 0.028f;
                _strikePips[i] = CreateImage(_root.transform, $"Pip{i}", new Color(1f, 1f, 1f, 0.25f),
                    Anchored(new Vector2(x, 0.105f), new Vector2(14f, 14f)));
            }
        }

        /// <summary>
        /// Scales the meter so the danger line always sits at a fixed screen position - the raw
        /// number is meaningless to the player, only "how close to the red line" matters.
        /// </summary>
        public void SetLevel(float level, float whisperCeiling, float dangerFloor)
        {
            if (_meterFill == null) return;

            float displayMax = Mathf.Max(dangerFloor * 1.35f, 0.0001f);
            _meterFill.fillAmount = Mathf.Clamp01(level / displayMax);

            _meterFill.color = level >= dangerFloor
                ? new Color(0.9f, 0.15f, 0.15f, 0.95f)
                : level > whisperCeiling
                    ? new Color(0.95f, 0.75f, 0.2f, 0.9f)
                    : new Color(0.3f, 0.9f, 0.4f, 0.9f);
        }

        public void SetInstruction(string text)
        {
            if (_instructionText != null) _instructionText.text = text;
        }

        public void SetStrikes(int strikes, int max)
        {
            for (int i = 0; i < _strikePips.Length; i++)
            {
                if (_strikePips[i] == null) continue;

                _strikePips[i].color = i < strikes
                    ? new Color(0.9f, 0.15f, 0.15f, 0.95f)
                    : new Color(1f, 1f, 1f, 0.25f);
            }
        }

        public void FlashCaught()
        {
            if (_dim != null) _dim.color = new Color(0.55f, 0f, 0f, 0.7f);
        }

        public void Destroy()
        {
            if (_root != null) Object.Destroy(_root);
        }

        // ── tiny builder helpers ─────────────────────────────────────────────────────────

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
