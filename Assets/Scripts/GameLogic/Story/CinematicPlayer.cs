using System;
using System.Collections;
using System.Collections.Generic;
using GameLogic.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace GameLogic.Story
{
    /// <summary>
    /// Renders one CinematicData fullscreen (video or placeholder), built from code like
    /// PauseMenuController so authoring a cutscene needs no scene wiring. The very first time a
    /// player sees a given cinematic (per save) it cannot be skipped at all; later viewings of
    /// the same id are skippable after skippableAfterSeconds, with a hint shown once the skip
    /// window opens. While playing, every other scene AudioSource is paused and restored on exit.
    /// An empty clip auto-completes after placeholderHoldSeconds.
    /// </summary>
    public class CinematicPlayer : MonoBehaviour
    {
        private Canvas _canvas;
        private RawImage _videoImage;
        private VideoPlayer _videoPlayer;
        private RenderTexture _rt;
        private TextMeshProUGUI _badge;
        private TextMeshProUGUI _skipHint;
        private readonly List<AudioSource> _duckedSources = new();
        private static readonly List<CinematicPlayer> _live = new();

        private void OnEnable() => _live.Add(this);

        private void OnDisable()
        {
            _live.Remove(this);
            // Object disabled mid-playback (not destroyed) would otherwise hang the caller
            // waiting on onComplete forever with Time.timeScale left frozen.
            if (IsPlaying) Stop();
        }

        /// <summary>True while any active CinematicPlayer is mid-playback (pause menu reads this).</summary>
        public static bool IsAnyPlaying
        {
            get
            {
                for (int i = 0; i < _live.Count; i++)
                {
                    if (_live[i] != null && _live[i].IsPlaying) return true;
                }
                return false;
            }
        }

        private CinematicData _current;
        private float _startTime;
        private Action _onComplete;
        private bool _firstViewing;

        /// <summary>True while a cinematic is on screen.</summary>
        public bool IsPlaying { get; private set; }

        public void Play(CinematicData data, Action onComplete)
        {
            if (data == null || !data.IsPlayable())
            {
                onComplete?.Invoke();
                return;
            }

            if (IsPlaying)
            {
                Debug.LogWarning("CinematicPlayer: already playing - ignoring duplicate Play call.", this);
                onComplete?.Invoke();
                return;
            }

            _current = data;
            IsPlaying = true;
            _startTime = Time.unscaledTime;
            _onComplete = onComplete; // set once here so Stop() invokes the right callback
            // Finish() marks the id played only at the end, so this reads "not played yet" on the
            // very first viewing and "played" on every retry - exactly the mandatory-watch rule.
            _firstViewing = !SaveManager.Current.IsCinematicPlayed(data.cinematicId);

            DuckSceneAudio();
            BuildUi(data);

            if (data.clip != null)
            {
                PrepareVideo(data.clip);
                StartCoroutine(RunVideo(onComplete));
            }
            else
            {
                _videoImage.gameObject.SetActive(false);
                StartCoroutine(RunPlaceholder(onComplete));
            }
        }

        public void Stop()
        {
            var callback = _onComplete;
            StopAllCoroutines();
            Teardown();
            IsPlaying = false;
            _current = null;
            _onComplete = null;
            callback?.Invoke(); // never leave a waiter hanging
        }

        // ── UI build ────────────────────────────────────────────────────────────────────

        private void BuildUi(CinematicData data)
        {
            var root = new GameObject("CinematicPlayer");
            root.transform.SetParent(transform, false);
            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 20000; // above game HUD

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Solid fade-to-black background.
            var bg = NewRect("Background", root.transform);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = Color.black;
            Stretch(bg);

            // The video / placeholder surface.
            var surface = NewRect("Surface", root.transform);
            Stretch(surface);
            _videoImage = surface.gameObject.AddComponent<RawImage>();
            _videoImage.color = Color.white;

            // Placeholder text (title + subtitle), hidden when a real video plays.
            var title = NewText(root.transform, "PlaceholderTitle", data.placeholderTitle);
            title.fontSize = 72;
            title.color = Color.white;
            title.alignment = TextAlignmentOptions.Center;
            Stretch((RectTransform)title.transform);
            title.margin = new Vector4(120f, 120f, 120f, 120f);
            title.enabled = false; // shown only for placeholder

            var subtitle = NewText(root.transform, "PlaceholderSubtitle", data.placeholderSubtitle);
            subtitle.fontSize = 28;
            subtitle.color = new Color(1f, 1f, 1f, 0.7f);
            subtitle.alignment = TextAlignmentOptions.Center;
            Stretch((RectTransform)subtitle.transform);
            subtitle.margin = new Vector4(120f, 220f, 120f, 220f);
            subtitle.enabled = false;

            // Corner badge (e.g. 'C.N.A. STAFF BROADCAST').
            _badge = NewText(root.transform, "Badge", data.badgeLabel);
            _badge.fontSize = 20;
            _badge.color = new Color(1f, 1f, 1f, 0.6f);
            _badge.alignment = TextAlignmentOptions.TopLeft;
            var badgeRt = (RectTransform)_badge.transform;
            badgeRt.anchorMin = new Vector2(0f, 1f);
            badgeRt.anchorMax = new Vector2(0f, 1f);
            badgeRt.pivot = new Vector2(0f, 1f);
            badgeRt.anchoredPosition = new Vector2(24f, -20f);
            badgeRt.sizeDelta = new Vector2(600f, 40f);
            _badge.gameObject.SetActive(!string.IsNullOrWhiteSpace(data.badgeLabel));

            // Skip hint, bottom-center, only ever shown once CanSkip() actually opens up.
            _skipHint = NewText(root.transform, "SkipHint", "PRESS ESC TO SKIP");
            _skipHint.fontSize = 24;
            _skipHint.color = new Color(1f, 1f, 1f, 0.7f);
            _skipHint.alignment = TextAlignmentOptions.Bottom;
            Stretch((RectTransform)_skipHint.transform);
            _skipHint.margin = new Vector4(0f, 0f, 0f, 48f);
            _skipHint.enabled = false;
        }

        // ── Audio ducking ───────────────────────────────────────────────────────────────

        /// <summary>Pauses every currently-playing scene AudioSource outside this player's own
        /// hierarchy (the video's own audio uses Direct output, not an AudioSource, so it's
        /// unaffected). Restored 1:1 by RestoreDuckedAudio.</summary>
        private void DuckSceneAudio()
        {
            _duckedSources.Clear();
            var sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var src in sources)
            {
                if (src == null || src.transform.IsChildOf(transform)) continue;
                if (!src.isPlaying) continue;
                src.Pause();
                _duckedSources.Add(src);
            }
        }

        /// <summary>Unpauses only the sources this player itself paused. Safe to call more than
        /// once (e.g. an OnDestroy safety net racing this component's own teardown) - UnPause on
        /// an already-stopped/already-unpaused source is a no-op.</summary>
        public void RestoreDuckedAudio()
        {
            for (int i = 0; i < _duckedSources.Count; i++)
            {
                if (_duckedSources[i] != null) _duckedSources[i].UnPause();
            }
            _duckedSources.Clear();
        }

        private void PrepareVideo(VideoClip clip)
        {
            _videoPlayer = gameObject.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.clip = clip;
            _videoPlayer.isLooping = false;
            _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            _rt = new RenderTexture(1920, 1080, 24);
            _videoPlayer.targetTexture = _rt;
            _videoImage.texture = _rt;
            _videoImage.gameObject.SetActive(true);
        }

        private void Teardown()
        {
            RestoreDuckedAudio();
            if (_videoPlayer != null)
            {
                _videoPlayer.errorReceived -= OnVideoError;
                _videoPlayer.Stop();
                Destroy(_videoPlayer);
                _videoPlayer = null;
            }
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
                _canvas = null;
            }
            _videoImage = null;
            _badge = null;
            _skipHint = null;
        }

        // ── Playback coroutines ─────────────────────────────────────────────────────────

        private IEnumerator RunVideo(Action onComplete)
        {
            _videoPlayer.errorReceived += OnVideoError;
            _videoPlayer.Prepare();

            // Corrupt/unsupported clip = isPrepared never arrives; bail out after 10s.
            float prepareWait = 0f;
            while (!_videoPlayer.isPrepared)
            {
                prepareWait += Time.unscaledDeltaTime;
                if (prepareWait > 10f)
                {
                    Debug.LogWarning($"CinematicPlayer: clip '{_current.clip.name}' never prepared - skipping.", this);
                    Finish(onComplete);
                    yield break;
                }
                yield return null;
            }

            _videoPlayer.Play();

            // isPlaying can still read false on the frame right after Play() - give it a frame
            // before trusting it, or the loop below would exit instantly.
            yield return null;

            while (_videoPlayer.isPlaying)
            {
                bool canSkip = CanSkip();
                if (_skipHint != null) _skipHint.enabled = canSkip;
                if (canSkip && TrySkip())
                    break;
                yield return null;
            }

            Finish(onComplete);
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"CinematicPlayer: video error on '{(_current != null ? _current.cinematicId : "?")}': {message} - skipping.", this);
            Stop();
        }

        private IEnumerator RunPlaceholder(Action onComplete)
        {
            _videoImage.gameObject.SetActive(false);
            ShowPlaceholder(true);

            float hold = Mathf.Max(0.1f, _current.placeholderHoldSeconds);
            float elapsed = 0f;
            while (elapsed < hold)
            {
                bool canSkip = CanSkip();
                if (_skipHint != null) _skipHint.enabled = canSkip;
                if (canSkip && TrySkip())
                    break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            ShowPlaceholder(false);
            Finish(onComplete);
        }

        private void ShowPlaceholder(bool visible)
        {
            if (_canvas == null) return;
            var title = _canvas.transform.Find("PlaceholderTitle");
            var subtitle = _canvas.transform.Find("PlaceholderSubtitle");
            if (title != null) title.GetComponent<TextMeshProUGUI>().enabled = visible;
            if (subtitle != null) subtitle.GetComponent<TextMeshProUGUI>().enabled = visible;
        }

        private bool CanSkip()
        {
            if (_current == null) return false;
            if (_firstViewing) return false; // mandatory first watch - no skip, ever
            if (_current.skippableAfterSeconds <= 0f) return false;
            return Time.unscaledTime - _startTime >= _current.skippableAfterSeconds;
        }

        private bool TrySkip()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                || (UnityEngine.InputSystem.Mouse.current?.leftButton.wasPressedThisFrame ?? false);
#else
            return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#endif
        }

        private void Finish(Action onComplete)
        {
            if (_current != null && !string.IsNullOrEmpty(_current.cinematicId))
                SaveManager.Current.MarkCinematicPlayed(_current.cinematicId);
            SaveManager.Save();

            Teardown();
            IsPlaying = false;
            _current = null;
            _onComplete = null;
            onComplete?.Invoke();
        }

        // ── Rect/Text helpers ───────────────────────────────────────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            // Must be born as a RectTransform - reparenting under one does not convert a plain Transform.
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI NewText(Transform parent, string name, string text)
        {
            var rt = NewRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text ?? "";
            return tmp;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}