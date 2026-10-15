using DG.Tweening;
using GameLogic.Data;
using UnityEngine;
// Aliased: UnityEngine's [Min] would be ambiguous with Gaskellgames' own (CS0104). See CLAUDE.md.
using GG = Gaskellgames;

namespace GameLogic
{
    /// <summary>
    /// A silent-type anomaly that rises from below the screen and stares at the player. The camera is
    /// locked and the screen glitches from the start of the rise until it has sunk away again. The
    /// silence countdown (run by QuietResponse) only starts once it has fully risen.
    /// </summary>
    [RequireComponent(typeof(Anomaly))]
    public class SilentStalker : MonoBehaviour
    {
        private enum Phase { Waiting, Rising, Watching, Leaving, Done }

        [Header("Trigger")]
        [Tooltip("Rise on whichever screen the player is looking at as soon as it spawns. Off = wait until the camera enters its room.")]
        [SerializeField] private bool ambushAnywhere;
        [Tooltip("How close (world X) the camera must be to its room before it rises. Ignored when Ambush Anywhere is on.")]
        [SerializeField] private float revealDistance = 5f;

        [Header("Motion")]
        [Tooltip("Seconds to slide up from below the screen.")]
        [Min(0.1f)] [SerializeField] private float riseSeconds = 6f;
        [Tooltip("Seconds to slide back down once the silence is over.")]
        [Min(0.1f)] [SerializeField] private float leaveSeconds = 4f;
        [Tooltip("Where the focus point (or the middle of its body) stops, relative to the middle of the screen.")]
        [SerializeField] private Vector2 restOffset = new Vector2(0f, -0.5f);
        [Tooltip("Extra distance below the bottom edge it starts from / sinks to.")]
        [Min(0f)] [SerializeField] private float hiddenMargin = 0.5f;
        [Tooltip("Optional, e.g. a point on the face. When set, rest and hidden positions are measured from it instead of the whole body, so a huge model can show only its face.")]
        [SerializeField] private Transform focusPoint;
        [Tooltip("Only with a Focus Point: how far the part to hide extends above it, so the face starts fully below the screen.")]
        [Min(0f)] [SerializeField] private float focusRadius = 6f;

        [Header("Silence")]
        [Tooltip("Seconds the player must stay silent once it has fully risen.")]
        [Min(1f)] [SerializeField] private float watchSeconds = 12f;

        [Header("Screen Glitch")]
        [Tooltip("Glitch strength while it is on screen (1 = full). Eases in while rising and out while leaving.")]
        [Range(0f, 1f)] [SerializeField] private float glitchIntensity = 1f;

        [Header("Audio")]
        [GG.InfoBox("SFX slots are optional - leave them empty until the sounds exist. Put each clip on its AudioSource.")]
        [SerializeField] private AudioSource riseSfx;
        [SerializeField] private AudioSource watchLoopSfx;
        [SerializeField] private AudioSource leaveSfx;
        [Tooltip("Looping presence sound. Fades in as it rises and out as it leaves.")]
        [SerializeField] private AudioSource presenceLoopSfx;
        [Tooltip("Level of the presence loop once faded in (before the player's volume sliders).")]
        [Range(0f, 1f)] [SerializeField] private float presenceVolume = 1f;
        [Tooltip("What everything else in the mix is lowered to while it is here (1 = no change). Fades down while rising, back up while leaving.")]
        [Range(0f, 1f)] [SerializeField] private float duckLevel = 0.25f;

        private static int _activeCount;
        public static bool AnyActive => _activeCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _activeCount = 0;

        private Anomaly _anomaly;
        private Phase _phase = Phase.Waiting;
        private Renderer[] _renderers;
        private Collider2D[] _colliders;
        private Vector3 _hiddenPosition;
        private Vector3 _restPosition;
        private float _homeX;
        private float _watchStartedAt;
        private bool _hasQuietResponse;
        private bool _holding;
        private GameManager _gameManager;
        private ScreenGlitch.Token _glitch;
        private Tween _moveTween;
        private Tween _glitchTween;
        private Tween _presenceTween;
        private bool _ducked;

        // True from the moment it has fully risen until it starts sinking: QuietResponse keys off this.
        public bool IsWatching => _phase == Phase.Watching;
        public float WatchSeconds => watchSeconds;

        void Awake()
        {
            _anomaly = GetComponent<Anomaly>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _colliders = GetComponentsInChildren<Collider2D>(true);
            _homeX = transform.position.x;
            SetVisible(false);
        }

        void Update()
        {
            switch (_phase)
            {
                case Phase.Waiting:
                    if (CanStart()) StartRise();
                    break;

                case Phase.Watching:
                    // Safety net when the scene has no QuietResponse: never leave the camera locked forever.
                    if (!_hasQuietResponse && Time.time - _watchStartedAt >= watchSeconds)
                        BeginLeave();
                    break;
            }
        }

        void OnDestroy() => ReleaseAll();

        void OnDisable()
        {
            if (_phase == Phase.Rising || _phase == Phase.Watching || _phase == Phase.Leaving)
                ReleaseAll();
        }

        private bool CanStart()
        {
            if (Time.timeScale <= 0f || AnyActive || DemonAnomaly.AnyRevealed) return false;
            if (_anomaly.State == AnomalyState.Resolved) return false;

            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return false;
            if (ambushAnywhere) return true;

            float roomX = _anomaly.AssignedRoom != null ? _anomaly.AssignedRoom.cameraX : _homeX;
            return Mathf.Abs(cam.transform.position.x - roomX) <= revealDistance;
        }

        private void StartRise()
        {
            var cam = Camera.main;
            _phase = Phase.Rising;
            _activeCount++;
            _holding = true;

            _hasQuietResponse = FindFirstObjectByType<Whisper.QuietResponse>() != null;
            if (!_hasQuietResponse)
                Debug.LogWarning("SilentStalker: no QuietResponse in the scene - falling back to a plain timer.", this);

            _gameManager = FindFirstObjectByType<GameManager>();
            if (_gameManager != null) _gameManager.LockCamera();

            SetVisible(true); // before measuring: a disabled skinned renderer can report stale bounds
            PlaceOffscreen(cam);

            _glitch = ScreenGlitch.Acquire(0f);
            TweenGlitch(glitchIntensity, riseSeconds);

            Play(riseSfx);
            StartPresenceAudio();

            _moveTween = transform.DOMove(_restPosition, riseSeconds)
                .SetEase(Ease.InOutSine)
                .SetTarget(this)
                .OnComplete(OnRisen);
        }

        private void OnRisen()
        {
            _moveTween = null;
            _phase = Phase.Watching;
            _watchStartedAt = Time.time;
            Play(watchLoopSfx);
        }

        // Called by QuietResponse when the silence has held long enough.
        public void BeginLeave()
        {
            if (_phase != Phase.Watching) return;

            _phase = Phase.Leaving;
            Stop(watchLoopSfx);
            Play(leaveSfx);
            TweenGlitch(0f, leaveSeconds);
            EndPresenceAudio();

            _moveTween = transform.DOMove(_hiddenPosition, leaveSeconds)
                .SetEase(Ease.InOutSine)
                .SetTarget(this)
                .OnComplete(Finish);
        }

        private void Finish()
        {
            _moveTween = null;
            _phase = Phase.Done;
            SetVisible(false);
            ReleaseAll();

            _anomaly.MarkReported();
            _anomaly.ResolveByReport();
        }

        // Positions are computed from the visible bounds, so any model pivot works.
        private void PlaceOffscreen(Camera cam)
        {
            Vector3 pivotToCenter;
            float hiddenExtent;
            if (focusPoint != null)
            {
                pivotToCenter = focusPoint.position - transform.position;
                hiddenExtent = focusRadius;
            }
            else
            {
                Bounds bounds = BoundsOfRenderers();
                pivotToCenter = bounds.center - transform.position;
                hiddenExtent = bounds.extents.y;
            }

            Vector3 camPos = cam.transform.position;
            float halfView = cam.orthographicSize;
            float x = camPos.x + restOffset.x;
            float z = transform.position.z;

            Vector3 hiddenCenter = new Vector3(x, camPos.y - halfView - hiddenExtent - hiddenMargin, z);
            Vector3 restCenter = new Vector3(x, camPos.y + restOffset.y, z);

            _hiddenPosition = hiddenCenter - pivotToCenter;
            _restPosition = restCenter - pivotToCenter;
            transform.position = _hiddenPosition;
        }

        private Bounds BoundsOfRenderers()
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                if (first) { bounds = r.bounds; first = false; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in _renderers)
                if (r != null) r.enabled = visible;
            foreach (var c in _colliders)
                if (c != null) c.enabled = visible;
        }

        private void TweenGlitch(float target, float seconds)
        {
            _glitchTween?.Kill();
            if (_glitch == null) return;

            var token = _glitch;
            _glitchTween = DOTween.To(() => token.Intensity, v => token.Intensity = v, target, seconds)
                .SetEase(Ease.InSine)
                .SetTarget(this);
        }

        private void ReleaseAll()
        {
            _moveTween?.Kill();
            _glitchTween?.Kill();
            _presenceTween?.Kill();
            _moveTween = null;
            _glitchTween = null;
            _presenceTween = null;

            Stop(watchLoopSfx);
            Stop(presenceLoopSfx);

            // Scene change or night end mid-stalk: put the mix back at once.
            if (_ducked)
            {
                _ducked = false;
                var manager = Audio.AudioManager.Instance;
                if (manager != null) manager.DuckOthers(1f, 0f);
            }

            ScreenGlitch.Release(_glitch);
            _glitch = null;

            if (!_holding) return;
            _holding = false;
            _activeCount = Mathf.Max(0, _activeCount - 1);
            if (_gameManager != null) _gameManager.UnlockCamera();
        }

        private void StartPresenceAudio()
        {
            var manager = Audio.AudioManager.Instance;
            if (manager == null) return;

            manager.ExemptFromDuck(riseSfx);
            manager.ExemptFromDuck(watchLoopSfx);
            manager.ExemptFromDuck(leaveSfx);
            manager.ExemptFromDuck(presenceLoopSfx);

            _ducked = true;
            manager.DuckOthers(duckLevel, riseSeconds);

            if (presenceLoopSfx == null || presenceLoopSfx.clip == null) return;

            manager.FadeSource(presenceLoopSfx, 0f, 0f);
            presenceLoopSfx.loop = true;
            presenceLoopSfx.Play();
            _presenceTween = manager.FadeSource(presenceLoopSfx, presenceVolume, riseSeconds);
        }

        // Fades the presence loop out and the rest of the mix back in over the sink.
        private void EndPresenceAudio()
        {
            var manager = Audio.AudioManager.Instance;
            if (manager == null) return;

            _ducked = false;
            manager.DuckOthers(1f, leaveSeconds);

            _presenceTween?.Kill();
            if (presenceLoopSfx == null) return;

            var source = presenceLoopSfx;
            _presenceTween = manager.FadeSource(source, 0f, leaveSeconds)
                .OnComplete(() => { if (source != null) source.Stop(); });
        }

        private static void Play(AudioSource source)
        {
            if (source != null && source.clip != null) source.Play();
        }

        private static void Stop(AudioSource source)
        {
            if (source != null && source.isPlaying) source.Stop();
        }
    }
}
