using DG.Tweening;
using GameLogic.Data;
using GameLogic.Flow;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// The Loudmouth: 8-bit music starts when it spawns and gets louder as time runs out. The player
    /// has to find its room and report it before the music peaks, or it gets them (jumpscare).
    /// </summary>
    [RequireComponent(typeof(Anomaly))]
    public class LoudmouthAnomaly : MonoBehaviour
    {
        [Header("Pressure")]
        [Tooltip("Seconds from spawning until it catches the player.")]
        [Min(5f)] [SerializeField] private float timeLimitSeconds = 45f;

        [Header("Music")]
        [Tooltip("The looping 8-bit track.")]
        [SerializeField] private AudioSource music;
        [Tooltip("Music level the moment it spawns (before the player's volume sliders).")]
        [Range(0f, 1f)] [SerializeField] private float startVolume = 0.15f;
        [Tooltip("Music level when time is nearly up.")]
        [Range(0f, 1f)] [SerializeField] private float peakVolume = 1f;
        [Tooltip("How loud it is while the camera is in a different room, as a share of the level above. Lower = the room itself is easier to hear.")]
        [Range(0f, 1f)] [SerializeField] private float otherRoomShare = 0.5f;
        [Tooltip("How close (world X) the camera must be to the Loudmouth's room to count as being in it.")]
        [Min(0.1f)] [SerializeField] private float sameRoomDistance = 5f;
        [Tooltip("How hard the music pans towards the side the Loudmouth is on (1 = only that ear). Same room = centred.")]
        [Range(0f, 1f)] [SerializeField] private float stereoPan = 1f;
        [Tooltip("Seconds the music fades in at the start.")]
        [Min(0.05f)] [SerializeField] private float fadeSeconds = 1f;

        private const float RoomBlendPerSecond = 2f;
        private const float PanBlendPerSecond = 4f;
        private const float ResolveFadeSeconds = 0.5f; // the anomaly deactivates 0.6s after it is reported, taking this object with it

        private Anomaly _anomaly;
        private float _spawnedAt;
        private float _roomShare = 1f;
        private float _pan;
        private GameManager _gameManager;
        private float _fadeIn;
        private bool _active;
        private bool _resolved;
        private Tween _fadeOutTween;

        void Awake()
        {
            _anomaly = GetComponent<Anomaly>();
            _anomaly.OnAnomalyDisappeared += OnResolved;
        }

        void OnEnable()
        {
            _spawnedAt = Time.time;
            _roomShare = 1f;
            _fadeIn = 0f;
            _pan = 0f;
            _active = _anomaly.State != AnomalyState.Resolved;
            if (!_active || music == null || music.clip == null) return;

            SetMusicLevel(0f);
            music.loop = true;
            music.Play();
        }

        void OnDisable()
        {
            if (!_resolved) StopMusic();
        }

        void OnDestroy()
        {
            if (_anomaly != null) _anomaly.OnAnomalyDisappeared -= OnResolved;
            _fadeOutTween?.Kill();
        }

        void Update()
        {
            if (!_active || Time.timeScale <= 0f) return;

            // The Watcher locks the camera, so the clock stands still while it is on screen.
            if (SilentStalker.AnyActive) _spawnedAt += Time.deltaTime;

            float elapsed = Time.time - _spawnedAt;
            float pressure = Mathf.Clamp01(elapsed / timeLimitSeconds);

            // Blend towards "in its room" so panning past it swells the music instead of snapping.
            float target = InSameRoom() ? 1f : otherRoomShare;
            _roomShare = Mathf.MoveTowards(_roomShare, target, RoomBlendPerSecond * Time.deltaTime);
            _fadeIn = Mathf.MoveTowards(_fadeIn, 1f, Time.deltaTime / fadeSeconds);

            float level = Mathf.Lerp(startVolume, peakVolume, pressure * pressure);
            SetMusicLevel(level * _roomShare * _fadeIn);
            UpdatePan();

            if (elapsed >= timeLimitSeconds)
                CatchPlayer();
        }

        private bool InSameRoom()
        {
            var cam = Camera.main;
            if (cam == null) return true;

            float roomX = _anomaly.AssignedRoom != null ? _anomaly.AssignedRoom.cameraX : transform.position.x;
            return Mathf.Abs(cam.transform.position.x - roomX) <= sameRoomDistance;
        }

        // Left/right follows the camera arrows: the shorter way round the rooms from the one on screen.
        private void UpdatePan()
        {
            if (music == null) return;

            _pan = Mathf.MoveTowards(_pan, PanDirection() * stereoPan, PanBlendPerSecond * Time.deltaTime);
            music.panStereo = _pan;
        }

        private float PanDirection()
        {
            if (_gameManager == null) _gameManager = FindFirstObjectByType<GameManager>();

            var there = _anomaly.AssignedRoom;
            var here = _gameManager != null ? _gameManager.CurrentRoom : null;
            int rooms = RoomRegistry.Count;

            if (here != null && there != null && rooms > 1)
            {
                int a = IndexOfRoom(here), b = IndexOfRoom(there);
                if (a < 0 || b < 0 || a == b) return 0f;

                int stepsNext = (b - a + rooms) % rooms;
                return stepsNext * 2 <= rooms ? 1f : -1f;
            }

            // Placed by hand with no room: fall back to which side of the camera it stands on.
            var cam = Camera.main;
            if (cam == null || InSameRoom()) return 0f;
            return Mathf.Sign(transform.position.x - cam.transform.position.x);
        }

        private static int IndexOfRoom(RoomDefinition room)
        {
            for (int i = 0; i < RoomRegistry.Count; i++)
                if (RoomRegistry.RoomAt(i).roomId == room.roomId) return i;
            return -1;
        }

        private void CatchPlayer()
        {
            _active = false;
            StopMusic();

            var def = _anomaly.Definition;
            GameFlowManager.Instance?.EndNight(
                NightOutcome.KilledByAnomaly,
                def != null ? def.anomalyId : name,
                _anomaly.AssignedRoom != null ? _anomaly.AssignedRoom.roomId : null);
        }

        // Reported in time: the music fades away with it.
        private void OnResolved(Anomaly _)
        {
            _active = false;
            _resolved = true;
            if (music == null || !music.isPlaying) return;

            var manager = Audio.AudioManager.Instance;
            var source = music;
            _fadeOutTween?.Kill();
            if (manager != null)
            {
                _fadeOutTween = manager.FadeSource(source, 0f, ResolveFadeSeconds)
                    .OnComplete(() => { if (source != null) source.Stop(); });
            }
            else
            {
                source.Stop();
            }
        }

        private void SetMusicLevel(float level)
        {
            if (music == null) return;

            var manager = Audio.AudioManager.Instance;
            if (manager != null) manager.FadeSource(music, level, 0f);
            else music.volume = level;
        }

        private void StopMusic()
        {
            _fadeOutTween?.Kill();
            _fadeOutTween = null;
            if (music != null && music.isPlaying) music.Stop();
        }
    }
}
