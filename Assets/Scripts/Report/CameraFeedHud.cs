using GameLogic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Report
{
    /// <summary>
    /// Always-on camera watermark: "CAM 0X — ROOM NAME" plus a running timestamp, built entirely
    /// from script like <see cref="SilenceProtocolHud"/>/<see cref="RadioCheckHud"/>. It exists for
    /// its own sake as a bit of security-camera flavour, but its real job is being the "tell" HL-5
    /// Camera Betrayal lies through - a stuck timestamp or a wrong label only reads as wrong if the
    /// player has already learned what right looks like, which means this has to run the whole
    /// night, not just during a glitch.
    /// </summary>
    public class CameraFeedHud : MonoBehaviour
    {
        private static CameraFeedHud _instance;

        public static CameraFeedHud Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (!Application.isPlaying) return null;

                _instance = FindFirstObjectByType<CameraFeedHud>();
                if (_instance == null)
                {
                    var host = new GameObject("CameraFeedHud (auto-created)");
                    _instance = host.AddComponent<CameraFeedHud>();
                }
                return _instance;
            }
        }

        public static CameraFeedHud ExistingInstance => _instance;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private TextMeshProUGUI _labelText;
        private TextMeshProUGUI _clockText;
        private Image _blackout;

        private GameManager _gameManager;
        private int _camIndex = 1;

        private string _labelOverride;
        private bool _timestampFrozen;
        private float _frozenElapsed;
        private float _elapsed;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            BuildUi();
        }

        void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            if (!_timestampFrozen)
                _elapsed += Time.unscaledDeltaTime;

            UpdateClockText();
            UpdateLabelText();
        }

        private void UpdateClockText()
        {
            if (_clockText == null) return;

            float shown = _timestampFrozen ? _frozenElapsed : _elapsed;
            int h = Mathf.FloorToInt(shown / 3600f);
            int m = Mathf.FloorToInt((shown % 3600f) / 60f);
            int s = Mathf.FloorToInt(shown % 60f);
            _clockText.text = $"{h:00}:{m:00}:{s:00}";
        }

        private void UpdateLabelText()
        {
            if (_labelText == null) return;

            if (!string.IsNullOrEmpty(_labelOverride))
            {
                _labelText.text = _labelOverride;
                return;
            }

            var room = _gameManager != null ? _gameManager.CurrentRoom : null;
            // Prefer the room's own cameraOrder (its real position in the Next/Previous cycle)
            // over the manually-set _camIndex fallback, so the number on screen always matches
            // which room is actually showing without needing anyone to keep it in sync by hand.
            int camIndex = room != null ? room.cameraOrder + 1 : _camIndex;
            string roomLabel = room != null ? room.Label.ToUpperInvariant() : "NO SIGNAL";
            _labelText.text = $"CAM 0{camIndex} — {roomLabel}";
        }

        // ── public API used by CameraFeedController ─────────────────────────────────────────

        public void SetCamIndex(int index) => _camIndex = Mathf.Max(1, index);

        public void SetLabelOverride(string text) => _labelOverride = text;
        public void ClearLabelOverride() => _labelOverride = null;

        public void FreezeTimestamp()
        {
            if (_timestampFrozen) return;
            _timestampFrozen = true;
            _frozenElapsed = _elapsed;
        }

        public void UnfreezeTimestamp() => _timestampFrozen = false;

        public void SetBlackout(bool on)
        {
            if (_blackout != null) _blackout.gameObject.SetActive(on);
            if (_labelText != null) _labelText.gameObject.SetActive(!on);
            if (_clockText != null) _clockText.gameObject.SetActive(!on);
        }

        // ── UI build ─────────────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            var root = new GameObject("CameraFeedHudCanvas", typeof(RectTransform));
            root.transform.SetParent(transform, false);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400; // below Radio Check (480) and Silence Protocol (500)

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var raycaster = root.AddComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            _labelText = CreateText(root.transform, "Label", 20f, new Vector2(0.02f, 0.97f));
            _labelText.color = new Color(0.85f, 0.9f, 0.85f, 0.75f);

            _clockText = CreateText(root.transform, "Clock", 20f, new Vector2(0.02f, 0.935f));
            _clockText.color = new Color(0.85f, 0.9f, 0.85f, 0.6f);

            var blackoutGo = new GameObject("Blackout", typeof(RectTransform), typeof(Image));
            blackoutGo.transform.SetParent(root.transform, false);
            _blackout = blackoutGo.GetComponent<Image>();
            _blackout.color = Color.black;
            _blackout.raycastTarget = false;

            var rect = blackoutGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            blackoutGo.SetActive(false);
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = false;
            text.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(500f, 30f);
            rect.anchoredPosition = Vector2.zero;

            return text;
        }
    }
}
