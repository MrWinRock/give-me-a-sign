using GameLogic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Report
{
    /// <summary>
    /// Always-on camera label: "CAM 0X — ROOM NAME" (the CameraFeedHud prefab in the gameplay Canvas). Besides
    /// security-camera flavour it is the "tell" Camera Betrayal lies through - a stuck or wrong label only reads
    /// as wrong if it has been tracking the room correctly all night.
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
        [Tooltip("Full-screen black cover shown by the Blackout glitch and behind the Mirror picture. Keep it inactive and BEHIND the label.")]
        [SerializeField] private GameObject blackout;
        [Tooltip("Picture shown by the Mirror glitch (the player's wallpaper), at its own pixel size in the centre. Keep it inactive and BEHIND the label.")]
        [SerializeField] private RawImage mirrorImage;
        [Tooltip("Full-screen picture that replaces the room view during Ghost Room. Keep it inactive and BEHIND the label.")]
        [SerializeField] private RawImage ghostRoomImage;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private GameManager _gameManager;
        private int _camIndex = 1;

        private string _labelOverride;
        private bool _labelFrozen;
        private bool _blackoutOn;
        private bool _mirrorShown;

        // What the label currently shows, so Update() only rebuilds TMP text when the value changes.
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
            if (mirrorImage != null) mirrorImage.gameObject.SetActive(false);
            if (ghostRoomImage != null) ghostRoomImage.gameObject.SetActive(false);
        }

        void Start()
        {
            _gameManager = FindFirstObjectByType<GameManager>();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Update() => UpdateLabelText();

        private void UpdateLabelText()
        {
            if (labelText == null || _labelFrozen) return;

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

        // The label stops following the camera: it keeps naming the room the player just left.
        public void FreezeLabel() => _labelFrozen = true;
        public void UnfreezeLabel() => _labelFrozen = false;

        // Replaces the room view with a picture that fills the screen (cropped, never stretched); null hides it again.
        public void SetGhostRoomImage(Texture texture)
        {
            if (ghostRoomImage == null) return;

            ghostRoomImage.gameObject.SetActive(texture != null);
            ghostRoomImage.texture = texture;
            if (texture == null) return;

            var rect = ghostRoomImage.rectTransform.rect;
            float screenAspect = rect.height > 0f ? rect.width / rect.height : 16f / 9f;
            float textureAspect = (float)texture.width / texture.height;

            ghostRoomImage.uvRect = textureAspect > screenAspect
                ? new Rect((1f - screenAspect / textureAspect) * 0.5f, 0f, screenAspect / textureAspect, 1f)
                : new Rect(0f, (1f - textureAspect / screenAspect) * 0.5f, 1f, textureAspect / screenAspect);
        }

        // Shows a texture at its own pixel size, centred - never scaled, cropped or stretched; null hides it again.
        public void SetMirrorImage(Texture texture)
        {
            if (mirrorImage == null) return;

            _mirrorShown = texture != null;
            mirrorImage.gameObject.SetActive(_mirrorShown);
            mirrorImage.texture = texture;
            if (blackout != null) blackout.SetActive(_mirrorShown || _blackoutOn); // black backdrop behind the picture
            if (texture == null) return;

            // The Canvas scales with the screen, so divide by its scale factor to land on real pixels.
            float scale = mirrorImage.canvas != null ? Mathf.Max(0.01f, mirrorImage.canvas.scaleFactor) : 1f;

            var rect = mirrorImage.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(texture.width, texture.height) / scale;
            mirrorImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        }

        public void SetBlackout(bool on)
        {
            _blackoutOn = on;
            if (blackout != null) blackout.SetActive(on || _mirrorShown);
            if (labelText != null) labelText.gameObject.SetActive(!on);
        }
    }
}
