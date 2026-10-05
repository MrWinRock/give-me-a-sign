using System.Collections;
using System.Collections.Generic;
using GameLogic;
using GameLogic.Data;
using UnityEngine;

namespace Report
{
    /// <summary>The five ways the camera feed can lie. Shared by CameraFeedController (HOW) and CameraBetrayalHaunt (WHEN/WHICH).</summary>
    public enum CameraGlitchType
    {
        Loop,
        Frozen,
        Blackout,
        GhostRoom,
        Mirror
    }

    /// <summary>
    /// "Camera Betrayal" executor (HL-5) - the single camera feed's own camera label lies
    /// for a beat, then always reverts. Same pure-executor split as FormGlitchController: this
    /// class only knows HOW to run each variant; <see cref="CameraBetrayalHaunt"/> decides WHEN
    /// and WHICH.
    /// </summary>
    public class CameraFeedController : MonoBehaviour
    {
        // How long each effect lasts is set per variant on CameraBetrayalHaunt (Duration Range) - except Ghost Room,
        // which lasts until the player changes room.
        [Header("Ghost Room")]
        [Tooltip("Fake camera labels shown on the ghost room (one picked at random).")]
        [SerializeField]
        private List<string> ghostRoomLabels = new List<string>
        {
            "CAM 07 — SUB-LEVEL", "CAM 09 — UNKNOWN SECTOR", "CAM 00 — ??????"
        };
        [Tooltip("Pictures that replace the room view on the ghost room (one picked at random, never the same twice in a row). Empty = label only.")]
        [SerializeField] private List<Texture2D> ghostRoomImages = new List<Texture2D>();
        [Tooltip("If the player never changes room, an armed Ghost Room gives up after this many seconds so it cannot block other haunts forever.")]
        [Min(5f)] [SerializeField] private float ghostRoomArmTimeoutSeconds = 90f;

        [Header("Mirror")]
        [SerializeField] private string mirrorLabel = "CAM 00 — SECURITY OFFICE";
        [SerializeField] private string mirrorHintText = "...someone is sitting there.";
        [Tooltip("Mirror shows the player's own desktop picture behind the label (Wallpaper Engine first, else the Windows wallpaper). Off here removes the Mirror glitch entirely. The player can also turn it off in Control Panel.")]
        [SerializeField] private bool useWallpaper = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private readonly Dictionary<CameraGlitchType, Coroutine> _running = new Dictionary<CameraGlitchType, Coroutine>();

        private Texture2D _mirrorTexture;
        private GameManager _gameManager;
        private GameManager _frozenManager; // set while Frozen holds the camera lock
        private int _lastGhostImage = -1;

        public bool IsGlitchActive => _running.Count > 0;

        // Mirror needs the player's picture (Wallpaper Engine, else the Windows wallpaper); without one it never happens.
        public bool MirrorAvailable => useWallpaper && PlayerWallpaper.CanShow;

        // Ghost Room needs a camera that can move to another room.
        public bool GhostRoomAvailable => Manager() != null && RoomRegistry.Count >= 2;

        public bool PlayGlitch(CameraGlitchType type, float duration)
        {
            if (_running.ContainsKey(type))
            {
                if (verboseLogging) Debug.Log($"[CameraFeed] {type} skipped - already running.", this);
                return false;
            }

            var hud = CameraFeedHud.Instance;
            if (hud == null) return false;

            if (type == CameraGlitchType.Mirror)
            {
                if (!MirrorAvailable) return false;

                _mirrorTexture = PlayerWallpaper.TryLoad();
                if (_mirrorTexture == null) return false;
            }

            if (type == CameraGlitchType.GhostRoom && !GhostRoomAvailable) return false;

            IEnumerator routine;
            switch (type)
            {
                case CameraGlitchType.Loop:      routine = LoopRoutine(hud, duration); break;
                case CameraGlitchType.Frozen:    routine = FrozenRoutine(hud, duration); break;
                case CameraGlitchType.Blackout:  routine = BlackoutRoutine(hud, duration); break;
                case CameraGlitchType.GhostRoom: routine = GhostRoomRoutine(hud); break;
                case CameraGlitchType.Mirror:    routine = MirrorRoutine(hud, duration); break;
                default: return false;
            }

            BeginTracked(type, routine);
            if (verboseLogging) Debug.Log($"[CameraFeed] FIRE {type}", this);
            return true;
        }

        private void BeginTracked(CameraGlitchType type, IEnumerator routine)
        {
            _running[type] = null;
            var handle = StartCoroutine(routine);
            if (_running.ContainsKey(type)) _running[type] = handle;
        }

        public void CancelAllGlitches()
        {
            var types = new List<CameraGlitchType>(_running.Keys);
            foreach (var t in types)
            {
                if (_running.TryGetValue(t, out var routine) && routine != null)
                    StopCoroutine(routine);
            }
            _running.Clear();
            ReleaseFrozenLock();

            var hud = CameraFeedHud.ExistingInstance;
            if (hud != null)
            {
                hud.UnfreezeLabel();
                hud.ClearLabelOverride();
                hud.SetBlackout(false);
                hud.SetGhostRoomImage(null);
            }

            ReleaseMirror(hud);
        }

        void OnDisable() => CancelAllGlitches();

        // Loop - the feed is quietly replaying old footage. The only tell: the corner label stops
        // following the camera, so it keeps naming the room the player just left.
        private IEnumerator LoopRoutine(CameraFeedHud hud, float duration)
        {
            hud.FreezeLabel();
            yield return new WaitForSecondsRealtime(duration);
            hud.UnfreezeLabel();
            _running.Remove(CameraGlitchType.Loop);
        }

        // Frozen - the feed sticks: the player cannot switch rooms until the time runs out. Scaled time on purpose,
        // so pausing the game does not burn the lock.
        private IEnumerator FrozenRoutine(CameraFeedHud hud, float duration)
        {
            _frozenManager = Manager();
            if (_frozenManager != null) _frozenManager.LockCamera();

            hud.SetLabelOverride("● REC — SIGNAL FROZEN");
            yield return new WaitForSeconds(duration);

            ReleaseFrozenLock();
            hud.ClearLabelOverride();
            _running.Remove(CameraGlitchType.Frozen);
        }

        private void ReleaseFrozenLock()
        {
            if (_frozenManager != null) _frozenManager.UnlockCamera();
            _frozenManager = null;
        }

        // Blackout - the feed just dies for a beat. CameraFeedHud.SetBlackout doubles as a
        // full-screen cover, so the player has to make a call (switch camera or wait) blind.
        private IEnumerator BlackoutRoutine(CameraFeedHud hud, float duration)
        {
            hud.SetBlackout(true);
            yield return new WaitForSecondsRealtime(duration);
            hud.SetBlackout(false);
            _running.Remove(CameraGlitchType.Blackout);
        }

        // Ghost Room - armed now, it springs on the NEXT room the player moves to (never the one they are in): that
        // room's view becomes a ghost picture with a fake camera label. No timer - it ends the moment the player
        // changes room again, and coming back shows the real room. If they never move, it gives up quietly.
        private IEnumerator GhostRoomRoutine(CameraFeedHud hud)
        {
            var manager = Manager();
            var startRoom = manager.CurrentRoom;
            float giveUpAt = Time.time + ghostRoomArmTimeoutSeconds;

            while (manager.CurrentRoom == startRoom)
            {
                if (Time.time > giveUpAt)
                {
                    _running.Remove(CameraGlitchType.GhostRoom);
                    yield break;
                }
                yield return null;
            }

            var ghostRoom = manager.CurrentRoom;
            hud.SetLabelOverride(PickRandom(ghostRoomLabels) ?? "CAM 0? — ??????");
            hud.SetGhostRoomImage(PickGhostImage());

            while (manager.CurrentRoom == ghostRoom)
                yield return null;

            hud.SetGhostRoomImage(null);
            hud.ClearLabelOverride();
            _running.Remove(CameraGlitchType.GhostRoom);
        }

        private GameManager Manager()
        {
            if (_gameManager == null) _gameManager = FindFirstObjectByType<GameManager>();
            return _gameManager;
        }

        // Never the same picture twice in a row.
        private Texture PickGhostImage()
        {
            if (ghostRoomImages == null || ghostRoomImages.Count == 0) return null;

            int index = Random.Range(0, ghostRoomImages.Count);
            if (ghostRoomImages.Count > 1 && index == _lastGhostImage) index = (index + 1) % ghostRoomImages.Count;
            _lastGhostImage = index;
            return ghostRoomImages[index];
        }

        // Mirror - the feed claims to be looking at the security office itself, i.e. the player: the "feed" is their
        // own desktop picture (loaded in PlayGlitch, kept only while this runs).
        private IEnumerator MirrorRoutine(CameraFeedHud hud, float duration)
        {
            hud.SetMirrorImage(_mirrorTexture);
            hud.SetLabelOverride($"{mirrorLabel}\n{mirrorHintText}");
            yield return new WaitForSecondsRealtime(duration);

            ReleaseMirror(hud);
            hud.ClearLabelOverride();
            _running.Remove(CameraGlitchType.Mirror);
        }

        private void ReleaseMirror(CameraFeedHud hud)
        {
            if (hud != null) hud.SetMirrorImage(null);
            if (_mirrorTexture != null) Destroy(_mirrorTexture);
            _mirrorTexture = null;
        }

        private static string PickRandom(List<string> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }
    }
}
