using GameLogic;
using TMPro;
using UnityEngine;

namespace Report
{
    /// <summary>
    /// Always-on camera watermark: "CAM 0X — ROOM NAME" plus a running timestamp (the CameraFeedHud prefab in the
    /// gameplay Canvas). Besides security-camera flavour it is the "tell" Camera Betrayal lies through - a stuck
    /// timestamp or a wrong label only reads as wrong if it has been running correctly all night.
    /// </summary>
    public class CameraFeedHud : MonoBehaviour
    {
        private static CameraFeedHud _instance;
        private static bool _warnedMissing;

        public static CameraFeedHud Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (!Application.isPlaying) return null;

                _instance = FindFirstObjectByType<CameraFeedHud>(FindObjectsInactive.Include);
                if (_instance == null && !_warnedMissing)
                {
                    _warnedMissing = true;
                    Debug.LogWarning("CameraFeedHud: no CameraFeedHud prefab in the scene - drag Assets/Prefabs/Gameplay/CameraFeedHud.prefab into the gameplay Canvas.");
                }
                return _instance;
            }
        }

        public static CameraFeedHud ExistingInstance => _instance;

        [Header("Refs (set in the prefab)")]
        [SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private TextMeshProUGUI timestampText;
        [Tooltip("Full-screen black cover shown by the Blackout glitch. Keep it inactive and BEHIND the texts.")]
        [SerializeField] private GameObject blackout;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private GameManager _gameManager;
        private int _camIndex = 1;

        private string _labelOverride;
        private bool _timestampFrozen;
        private float _frozenElapsed;
        private float _elapsed;

        // What the labels currently show, so Update() only rebuilds TMP text when the value changes.
        private int _lastDisplayedSecond = -1;
        private string _lastDisplayedLabel;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (blackout != null) blackout.SetActive(false);
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
            // Unscaled so it ignores timeScale quirks, but it must still stand still while the pause menu is open.
            if (!_timestampFrozen && Time.timeScale > 0f)
                _elapsed += Time.unscaledDeltaTime;

            UpdateTimestampText();
            UpdateLabelText();
        }

        private void UpdateTimestampText()
        {
            if (timestampText == null) return;

            float shown = _timestampFrozen ? _frozenElapsed : _elapsed;
            int totalSeconds = Mathf.FloorToInt(shown);

            if (totalSeconds == _lastDisplayedSecond) return;
            _lastDisplayedSecond = totalSeconds;

            int h = totalSeconds / 3600;
            int m = (totalSeconds % 3600) / 60;
            int s = totalSeconds % 60;
            timestampText.text = $"{h:00}:{m:00}:{s:00}";
        }

        private void UpdateLabelText()
        {
            if (labelText == null) return;

            string label;
            if (!string.IsNullOrEmpty(_labelOverride))
            {
                label = _labelOverride;
            }
            else
            {
                var room = _gameManager != null ? _gameManager.CurrentRoom : null;
                // The room's own cameraOrder keeps the number matching the room actually showing.
                int camIndex = room != null ? room.cameraOrder + 1 : _camIndex;
                string roomLabel = room != null ? room.Label.ToUpperInvariant() : "NO SIGNAL";
                label = $"CAM 0{camIndex} — {roomLabel}";
            }

            if (label == _lastDisplayedLabel) return;
            _lastDisplayedLabel = label;
            labelText.text = label;
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
            if (blackout != null) blackout.SetActive(on);
            if (labelText != null) labelText.gameObject.SetActive(!on);
            if (timestampText != null) timestampText.gameObject.SetActive(!on);
        }
    }
}
