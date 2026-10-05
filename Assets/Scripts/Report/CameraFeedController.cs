using System.Collections;
using System.Collections.Generic;
using GameLogic;
using GameLogic.Data;
using UnityEngine;
using UnityEngine.Serialization;

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
    /// "Camera Betrayal" executor (HL-5) - the single camera feed lies for a beat, then always reverts. Same
    /// pure-executor split as FormGlitchController: this class only knows HOW to run each variant;
    /// <see cref="CameraBetrayalHaunt"/> decides WHEN and WHICH.
    /// </summary>
    public class CameraFeedController : MonoBehaviour
    {
        // How long Loop / Frozen / Blackout last is set per variant on CameraBetrayalHaunt (Duration Range).
        // Ghost Room and Mirror have no timer: they spring on the next room the player visits and end when they change room.
        [Header("Ghost Room")]
        [Tooltip("Fake camera labels shown on the ghost room (one picked at random).")]
        [SerializeField]
        private List<string> ghostRoomLabels = new List<string>
        {
            "CAM 07 — SUB-LEVEL", "CAM 09 — UNKNOWN SECTOR", "CAM 00 — ??????"
        };
        [Tooltip("Pictures that replace the room view on the ghost room (one picked at random, never the same twice in a row). Empty = label only.")]
        [SerializeField] private List<Texture2D> ghostRoomImages = new List<Texture2D>();

        [Header("Mirror")]
        [SerializeField] private string mirrorLabel = "CAM 00 — SECURITY OFFICE";
        [SerializeField] private string mirrorHintText = "...someone is sitting there.";
        [Tooltip("Mirror shows the player's own desktop picture (Wallpaper Engine first, else the Windows wallpaper). Off here removes the Mirror glitch entirely. The player can also turn it off in Control Panel.")]
        [SerializeField] private bool useWallpaper = true;

        [Header("Ghost Room + Mirror")]
        [Tooltip("If the player never changes room, an armed Ghost Room / Mirror gives up after this many seconds. While armed, no other Camera Betrayal beat can start.")]
        [FormerlySerializedAs("ghostRoomArmTimeoutSeconds")]
        [Min(5f)] [SerializeField] private float roomEffectArmTimeoutSeconds = 90f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private readonly Dictionary<CameraGlitchType, Coroutine> _running = new Dictionary<CameraGlitchType, Coroutine>();

        private Texture2D _mirrorTexture;
        private GameManager _gameManager;
        private GameManager _frozenManager; // set while Frozen holds the camera lock
        private int _lastGhostImage = -1;
        private bool _mirrorArmed;          // Mirror is waiting for the player to change room
        private int _mirrorSpringFrame = -1; // the frame Mirror sprang, so a Ghost Room springing on that same room change yields

        public bool IsGlitchActive => _running.Count > 0;

        // Ghost Room and Mirror need a camera that can move to another room.
        public bool GhostRoomAvailable => CanChangeRooms;

        // Mirror also needs the player's picture (Wallpaper Engine, else the Windows wallpaper); without one it never happens.
        public bool MirrorAvailable => useWallpaper && CanChangeRooms && PlayerWallpaper.CanShow;

        private bool CanChangeRooms => Manager() != null && RoomRegistry.Count >= 2;

        public bool PlayGlitch(CameraGlitchType type, float duration)
        {
            if (_running.ContainsKey(type))
            {
                if (verboseLogging) Debug.Log($"[CameraFeed] {type} skipped - already running.", this);
                return false;
            }

            var hud = CameraFeedHud.Instance;
            if (hud == null) return false;

            IEnumerator routine;
            switch (type)
            {
                case CameraGlitchType.Loop:      routine = LoopRoutine(hud, duration); break;
                case CameraGlitchType.Frozen:    routine = FrozenRoutine(hud, duration); break;
                case CameraGlitchType.Blackout:  routine = BlackoutRoutine(hud, duration); break;

                case CameraGlitchType.GhostRoom:
                    // Mirror is the rarer effect, so it wins: no Ghost Room while a Mirror is waiting.
                    if (!GhostRoomAvailable || _mirrorArmed) return false;
                    routine = RoomEffectRoutine(type, () => ShowGhostRoom(hud), shown => HideGhostRoom(hud, shown));
                    break;

                case CameraGlitchType.Mirror:
                    if (!MirrorAvailable) return false;

                    // Loaded now so a failed read means "no Mirror", never a half effect.
                    _mirrorTexture = PlayerWallpaper.TryLoad();
                    if (_mirrorTexture == null) return false;
                    routine = RoomEffectRoutine(type, () => ShowMirror(hud), shown => HideMirror(hud, shown));
                    break;

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
            _mirrorArmed = false;
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

        // Ghost Room / Mirror - armed now, they spring on the NEXT room the player moves to (never the one they are
        // in) and last until the player changes room again; coming back shows the real room. No timer. If the player
        // never moves it gives up quietly, and a Demon reveal ends it so its jumpscare is never hidden.
        private IEnumerator RoomEffectRoutine(CameraGlitchType type, System.Action show, System.Action<bool> hide)
        {
            var manager = Manager();
            var startRoom = manager.CurrentRoom;
            float giveUpAt = Time.time + roomEffectArmTimeoutSeconds;
            if (type == CameraGlitchType.Mirror) _mirrorArmed = true;

            while (manager != null && manager.CurrentRoom == startRoom && Time.time <= giveUpAt)
                yield return null;

            bool sprang = manager != null && manager.CurrentRoom != startRoom;
            if (type == CameraGlitchType.Mirror)
            {
                _mirrorArmed = false;
                if (sprang) _mirrorSpringFrame = Time.frameCount;
            }

            // Both armed for the same room change: Mirror (the rarer one) plays, Ghost Room steps aside.
            bool ghostYields = type == CameraGlitchType.GhostRoom && (_mirrorArmed || _mirrorSpringFrame == Time.frameCount);

            bool shown = sprang && !ghostYields;
            if (shown)
            {
                var room = manager.CurrentRoom;
                show();

                while (manager != null && manager.CurrentRoom == room && !DemonAnomaly.AnyRevealed)
                    yield return null;
            }

            hide(shown);
            _running.Remove(type);
        }

        private void ShowGhostRoom(CameraFeedHud hud)
        {
            hud.SetLabelOverride(PickRandom(ghostRoomLabels) ?? "CAM 0? — ??????");
            hud.SetGhostRoomImage(PickGhostImage());
        }

        private void HideGhostRoom(CameraFeedHud hud, bool shown)
        {
            hud.SetGhostRoomImage(null);
            if (shown) hud.ClearLabelOverride();
        }

        private void ShowMirror(CameraFeedHud hud)
        {
            hud.SetMirrorImage(_mirrorTexture);
            hud.SetLabelOverride($"{mirrorLabel}\n{mirrorHintText}");
        }

        private void HideMirror(CameraFeedHud hud, bool shown)
        {
            ReleaseMirror(hud);
            if (shown) hud.ClearLabelOverride();
        }

        private void ReleaseMirror(CameraFeedHud hud)
        {
            if (hud != null) hud.SetMirrorImage(null);
            if (_mirrorTexture != null) Destroy(_mirrorTexture);
            _mirrorTexture = null;
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

        private static string PickRandom(List<string> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }
    }
}
