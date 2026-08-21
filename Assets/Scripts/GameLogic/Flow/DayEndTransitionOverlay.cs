using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace GameLogic.Flow
{
    /// <summary>
    /// Fullscreen glitch/static clip (e.g. Overlay.mp4) snapped on screen for a fixed hold time
    /// before the day-end event decision - a jarring VHS-style splice, not a cross-fade. The
    /// screen is built at runtime, same approach as DayEventPlayer, so dropping this on any
    /// GameObject is all the setup it needs.
    /// </summary>
    public class DayEndTransitionOverlay : MonoBehaviour
    {
        [Header("Clip")]
        [Tooltip("Fullscreen overlay clip, e.g. Assets/Video/Overlay.mp4. No clip = the overlay is skipped.")]
        [SerializeField] private VideoClip overlayClip;

        [Tooltip("How long the overlay holds the screen before cutting away. The clip is cut off here if it runs longer.")]
        [Min(0.05f)] [SerializeField] private float holdSeconds = 0.5f;

        [Header("Screen")]
        [Tooltip("Sort order of the overlay canvas. Must sit above every gameplay/desktop HUD, including DayEventPlayer's screen.")]
        [SerializeField] private int canvasSortOrder = 950;
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private Vector2Int videoResolution = new Vector2Int(1920, 1080);

        [Header("Audio")]
        [Tooltip("Route the clip's own audio through the player's SFX volume slider.")]
        [SerializeField] private bool applySfxVolume = true;

        [Tooltip("One-shot sound played the instant the overlay appears, e.g. Assets/Audio/PcStart.mp3. Plays through AudioManager's SFX channel. Optional - left empty, only the clip's own audio plays.")]
        [SerializeField] private AudioClip startupSound;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private GameObject _root;
        private RawImage _videoImage;
        private VideoPlayer _videoPlayer;
        private RenderTexture _renderTexture;
        private Coroutine _routine;

        /// <summary>True while the overlay is on screen.</summary>
        public bool IsPlaying => _routine != null;

        /// <summary>
        /// Plays the overlay, then invokes <paramref name="onComplete"/> exactly once - after
        /// holdSeconds, or immediately if no clip is assigned. GameFlowManager's day-end
        /// coroutine waits on this callback before rolling the day-end event.
        /// </summary>
        public void Play(Action onComplete)
        {
            if (overlayClip == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (IsPlaying)
            {
                Debug.LogWarning("DayEndTransitionOverlay: already playing - ignoring the new request.", this);
                return;
            }

            _routine = StartCoroutine(PlayRoutine(onComplete));
        }

        private IEnumerator PlayRoutine(Action onComplete)
        {
            if (showDebugInfo)
                Debug.Log($"DayEndTransitionOverlay: playing '{overlayClip.name}' for {holdSeconds:0.##}s.", this);

            BuildScreen();
            StartClip();
            PlayStartupSound();

            // Realtime, not WaitForSeconds: the day loop must not be at the mercy of Time.timeScale.
            yield return new WaitForSecondsRealtime(holdSeconds);

            DestroyScreen();
            _routine = null;
            onComplete?.Invoke();
        }

        private void StartClip()
        {
            _renderTexture = new RenderTexture(
                Mathf.Max(16, videoResolution.x), Mathf.Max(16, videoResolution.y), 0);
            _videoImage.texture = _renderTexture;

            _videoPlayer = _root.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.clip = overlayClip;
            _videoPlayer.isLooping = false;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _renderTexture;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

            if (applySfxVolume && Audio.AudioManager.Instance != null)
                _videoPlayer.SetDirectAudioVolume(0, Audio.AudioManager.Instance.GetEffectiveVolume(Audio.AudioChannel.Sfx));

            _videoPlayer.Play();
        }

        private void PlayStartupSound()
        {
            if (startupSound == null) return;

            Audio.AudioManager.Instance?.PlayClip(startupSound);
        }

        // ── Runtime UI ───────────────────────────────────────────────────────────────────

        private void BuildScreen()
        {
            _root = new GameObject("DayEndOverlayScreen", typeof(RectTransform));
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortOrder;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _root.AddComponent<GraphicRaycaster>().enabled = false;

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(_root.transform, false);
            var bg = bgGo.GetComponent<Image>();
            bg.color = backgroundColor;
            bg.raycastTarget = false;
            StretchFull(bgGo.GetComponent<RectTransform>());

            var videoGo = new GameObject("Video", typeof(RectTransform), typeof(RawImage));
            videoGo.transform.SetParent(_root.transform, false);
            _videoImage = videoGo.GetComponent<RawImage>();
            _videoImage.raycastTarget = false;
            StretchFull(videoGo.GetComponent<RectTransform>());
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void DestroyScreen()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (_root != null)
            {
                Destroy(_root);
                _root = null;
            }

            _videoImage = null;
            _videoPlayer = null;
        }

        void OnDestroy()
        {
            if (_routine != null)
                StopCoroutine(_routine);

            DestroyScreen();
        }
    }
}
