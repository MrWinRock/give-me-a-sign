using System;
using DG.Tweening;
using GameLogic.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Aliased, not imported: this file uses UnityEngine's [Min] and [Range].
// See CLAUDE.md - "Gaskellgames" for the project-wide rule.
using GG = Gaskellgames;

namespace GameLogic.Flow
{
    /// <summary>
    /// Plays a day-end Short VDO fullscreen, then reports back.
    ///
    /// The screen is built at runtime from the settings below rather than from a wired prefab,
    /// so dropping this component on any GameObject is all the setup it needs - every knob that
    /// changes how it looks is still an Inspector field.
    /// </summary>
    public class DayEventPlayer : MonoBehaviour
    {
        /// <summary>Skip keys offered in the Inspector. Kept tiny on purpose - a full keyboard enum
        /// would be unusable in a dropdown, and only a handful make sense for "skip".</summary>
        public enum SkipKey
        {
            Escape,
            Space,
            Enter,
            AnyKey,
        }

        [Header("Screen")]
        [Tooltip("Sort order of the video canvas. Must sit above every gameplay HUD.")]
        [SerializeField] private int canvasSortOrder = 900;

        [Tooltip("Letterbox / background colour behind the video.")]
        [SerializeField] private Color backgroundColor = Color.black;

        [Tooltip("Resolution of the render texture the video is drawn into.")]
        [SerializeField] private Vector2Int videoResolution = new Vector2Int(1920, 1080);

        [Tooltip("Keep the clip's aspect ratio and letterbox it, instead of stretching to fill.")]
        [SerializeField] private bool preserveAspectRatio = true;

        [Header("Fades")]
        [Tooltip("Seconds to fade from black into the video.")]
        [Min(0f)] [SerializeField] private float fadeInSeconds = 0.4f;

        [Tooltip("Seconds to fade back out to black when the video ends.")]
        [Min(0f)] [SerializeField] private float fadeOutSeconds = 0.6f;

        [Header("Skip Prompt")]
        [GG.InfoBox("This project runs the new Input System only, so the skip key is chosen from that package's Key enum - not the legacy KeyCode.")]
        [Tooltip("Key the player presses to skip, once the clip allows it.")]
        [SerializeField] private SkipKey skipKey = SkipKey.Escape;

        [Tooltip("Text shown when skipping becomes available. {key} is replaced with the skip key.")]
        [SerializeField] private string skipPromptFormat = "Press {key} to skip";

        [Tooltip("Font for the skip prompt. Leave empty to use the TMP default.")]
        [SerializeField] private TMP_FontAsset promptFont;

        [Min(1f)] [SerializeField] private float skipPromptFontSize = 22f;
        [SerializeField] private Color skipPromptColor = new Color(1f, 1f, 1f, 0.55f);

        [Tooltip("Distance of the prompt from the bottom-right corner, in reference pixels.")]
        [SerializeField] private Vector2 skipPromptMargin = new Vector2(48f, 36f);

        [Header("Audio")]
        [Tooltip("Route the video's own audio through the player's SFX volume slider.")]
        [SerializeField] private bool applySfxVolume = true;

        [Header("Safety")]
        [GG.InfoBox("A clip that never signals completion would hang the day loop. This cap force-finishes it. 0 = no cap.")]
        [Tooltip("Hard cap on how long any single VDO may hold the screen. 0 = no cap.")]
        [Min(0f)] [SerializeField] private float maxPlaybackSeconds = 300f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private GameObject _root;
        private CanvasGroup _canvasGroup;
        private RawImage _videoImage;
        private AspectRatioFitter _aspectFitter;
        private TextMeshProUGUI _skipPrompt;
        private VideoPlayer _videoPlayer;
        private RenderTexture _renderTexture;

        private Action _onComplete;
        private ShortVDOData _playing;
        private float _startedAt;
        private bool _skipAllowed;
        private bool _finished;
        private Tween _fadeTween;

        /// <summary>True while a video is on screen.</summary>
        public bool IsPlaying => _playing != null && !_finished;

        /// <summary>
        /// Plays the clip, then invokes <paramref name="onComplete"/> exactly once - on natural
        /// end, on skip, or on the safety cap. The day loop waits on that callback, so every exit
        /// path has to fire it.
        /// </summary>
        public void Play(ShortVDOData vdo, Action onComplete)
        {
            if (vdo == null || vdo.clip == null)
            {
                Debug.LogWarning("DayEventPlayer: no clip to play - completing immediately.", this);
                onComplete?.Invoke();
                return;
            }

            if (IsPlaying)
            {
                Debug.LogWarning("DayEventPlayer: already playing - ignoring the new request.", this);
                return;
            }

            _playing = vdo;
            _onComplete = onComplete;
            _finished = false;
            _skipAllowed = false;
            _startedAt = Time.unscaledTime;

            BuildScreen();
            StartClip(vdo);

            if (showDebugInfo)
                Debug.Log($"DayEventPlayer: playing '{vdo.Label}' ({vdo.clip.name}).", this);
        }

        void Update()
        {
            if (!IsPlaying) return;

            float elapsed = Time.unscaledTime - _startedAt;

            if (!_skipAllowed && _playing.skippableAfterSeconds > 0f && elapsed >= _playing.skippableAfterSeconds)
                ShowSkipPrompt();

            if (_skipAllowed && WasSkipPressed())
            {
                if (showDebugInfo) Debug.Log("DayEventPlayer: skipped by player.", this);
                Finish();
                return;
            }

            // Never let a malformed clip strand the day loop.
            if (maxPlaybackSeconds > 0f && elapsed >= maxPlaybackSeconds)
            {
                Debug.LogWarning($"DayEventPlayer: '{_playing.Label}' hit the {maxPlaybackSeconds:0}s cap - force-finishing.", this);
                Finish();
            }
        }

        // Guarded the same way PauseMenuController does it: this project ships with the new
        // Input System as the ONLY handler, where legacy UnityEngine.Input throws at runtime.
        private bool WasSkipPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;

            switch (skipKey)
            {
                case SkipKey.Escape: return keyboard.escapeKey.wasPressedThisFrame;
                case SkipKey.Space:  return keyboard.spaceKey.wasPressedThisFrame;
                case SkipKey.Enter:  return keyboard.enterKey.wasPressedThisFrame;
                case SkipKey.AnyKey: return keyboard.anyKey.wasPressedThisFrame;
                default:             return false;
            }
#else
            switch (skipKey)
            {
                case SkipKey.Escape: return Input.GetKeyDown(KeyCode.Escape);
                case SkipKey.Space:  return Input.GetKeyDown(KeyCode.Space);
                case SkipKey.Enter:  return Input.GetKeyDown(KeyCode.Return);
                case SkipKey.AnyKey: return Input.anyKeyDown;
                default:             return false;
            }
#endif
        }

        private void StartClip(ShortVDOData vdo)
        {
            _renderTexture = new RenderTexture(
                Mathf.Max(16, videoResolution.x), Mathf.Max(16, videoResolution.y), 0);

            _videoImage.texture = _renderTexture;

            if (_aspectFitter != null)
            {
                _aspectFitter.enabled = preserveAspectRatio;
                if (preserveAspectRatio && vdo.clip.height > 0)
                    _aspectFitter.aspectRatio = (float)vdo.clip.width / vdo.clip.height;
            }

            _videoPlayer = _root.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.clip = vdo.clip;
            _videoPlayer.isLooping = false;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.targetTexture = _renderTexture;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            _videoPlayer.loopPointReached += OnClipEnded;
            _videoPlayer.errorReceived += OnVideoError;

            if (applySfxVolume && Audio.AudioManager.Instance != null)
                _videoPlayer.SetDirectAudioVolume(0, Audio.AudioManager.Instance.GetEffectiveVolume(Audio.AudioChannel.Sfx));

            _videoPlayer.Play();

            _canvasGroup.alpha = 0f;
            _fadeTween?.Kill();
            _fadeTween = _canvasGroup.DOFade(1f, fadeInSeconds).SetUpdate(true).SetTarget(this);
        }

        private void OnClipEnded(VideoPlayer source) => Finish();

        private void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogError($"DayEventPlayer: video error on '{_playing?.Label}' - {message}", this);
            Finish();
        }

        private void ShowSkipPrompt()
        {
            _skipAllowed = true;

            if (_skipPrompt == null) return;

            _skipPrompt.text = skipPromptFormat.Replace("{key}", skipKey.ToString());
            _skipPrompt.gameObject.SetActive(true);
        }

        /// <summary>Fades out, tears the screen down, then hands control back. Runs once.</summary>
        private void Finish()
        {
            if (_finished) return;
            _finished = true;

            if (_videoPlayer != null) _videoPlayer.Stop();

            _fadeTween?.Kill();
            _fadeTween = _canvasGroup != null
                ? _canvasGroup.DOFade(0f, fadeOutSeconds).SetUpdate(true).SetTarget(this)
                    .OnComplete(TearDownAndReport)
                : null;

            if (_fadeTween == null) TearDownAndReport();
        }

        private void TearDownAndReport()
        {
            var callback = _onComplete;

            _onComplete = null;
            _playing = null;
            _fadeTween = null;

            DestroyScreen();
            callback?.Invoke();
        }

        private void DestroyScreen()
        {
            if (_videoPlayer != null)
            {
                _videoPlayer.loopPointReached -= OnClipEnded;
                _videoPlayer.errorReceived -= OnVideoError;
            }

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

            _canvasGroup = null;
            _videoImage = null;
            _aspectFitter = null;
            _skipPrompt = null;
            _videoPlayer = null;
        }

        void OnDestroy()
        {
            _fadeTween?.Kill();
            DestroyScreen();
        }

        // ── Runtime UI ───────────────────────────────────────────────────────────────────

        private void BuildScreen()
        {
            _root = new GameObject("DayEventScreen", typeof(RectTransform));
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortOrder;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _root.AddComponent<GraphicRaycaster>().enabled = false;
            _canvasGroup = _root.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;

            CreateFullScreenImage("Background", backgroundColor);

            var videoGo = new GameObject("Video", typeof(RectTransform), typeof(RawImage));
            videoGo.transform.SetParent(_root.transform, false);

            _videoImage = videoGo.GetComponent<RawImage>();
            _videoImage.raycastTarget = false;

            var videoRect = videoGo.GetComponent<RectTransform>();
            videoRect.anchorMin = Vector2.zero;
            videoRect.anchorMax = Vector2.one;
            videoRect.offsetMin = Vector2.zero;
            videoRect.offsetMax = Vector2.zero;

            _aspectFitter = videoGo.AddComponent<AspectRatioFitter>();
            _aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            CreateSkipPrompt();
        }

        private void CreateFullScreenImage(string objectName, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root.transform, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void CreateSkipPrompt()
        {
            var go = new GameObject("SkipPrompt", typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);

            _skipPrompt = go.AddComponent<TextMeshProUGUI>();
            _skipPrompt.font = promptFont != null ? promptFont : TMP_Settings.defaultFontAsset;
            _skipPrompt.fontSize = skipPromptFontSize;
            _skipPrompt.color = skipPromptColor;
            _skipPrompt.alignment = TextAlignmentOptions.BottomRight;
            _skipPrompt.raycastTarget = false;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(600f, 40f);
            rect.anchoredPosition = new Vector2(-skipPromptMargin.x, skipPromptMargin.y);

            go.SetActive(false);
        }
    }
}
