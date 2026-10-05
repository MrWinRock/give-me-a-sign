using System.Collections;
using System.Collections.Generic;
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
        // How long each effect lasts is set per variant on CameraBetrayalHaunt (Duration Range).
        [Header("Ghost Room / Mirror text (best-effort - purely a HUD watermark trick, no new art required)")]
        [SerializeField]
        private List<string> ghostRoomLabels = new List<string>
        {
            "CAM 07 — SUB-LEVEL", "CAM 09 — UNKNOWN SECTOR", "CAM 00 — ??????"
        };
        [SerializeField] private string mirrorLabel = "CAM 00 — SECURITY OFFICE";
        [SerializeField] private string mirrorHintText = "...someone is sitting there.";
        [Tooltip("Mirror shows the player's own Windows wallpaper behind the label. The player can still turn it off in Control Panel; off here removes the feature entirely.")]
        [SerializeField] private bool useWallpaper = true;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private readonly Dictionary<CameraGlitchType, Coroutine> _running = new Dictionary<CameraGlitchType, Coroutine>();

        private Texture2D _mirrorTexture;

        public bool IsGlitchActive => _running.Count > 0;

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
                case CameraGlitchType.GhostRoom: routine = GhostRoomRoutine(hud, duration); break;
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

            var hud = CameraFeedHud.ExistingInstance;
            if (hud != null)
            {
                hud.UnfreezeLabel();
                hud.ClearLabelOverride();
                hud.SetBlackout(false);
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

        // Frozen - announced outright via the label, a more overt "something is wrong" beat than Loop's quiet version.
        private IEnumerator FrozenRoutine(CameraFeedHud hud, float duration)
        {
            hud.SetLabelOverride("● REC — SIGNAL FROZEN");
            yield return new WaitForSecondsRealtime(duration);
            hud.ClearLabelOverride();
            _running.Remove(CameraGlitchType.Frozen);
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

        // Ghost Room - the label briefly claims a camera/room that does not exist anywhere in
        // RoomRegistry. A real extra room is Sprint 3+ art/content work; the watermark lie is what
        // this system can honestly deliver today.
        private IEnumerator GhostRoomRoutine(CameraFeedHud hud, float duration)
        {
            string label = PickRandom(ghostRoomLabels) ?? "CAM 0? — ??????";
            hud.SetLabelOverride(label);
            yield return new WaitForSecondsRealtime(duration);
            hud.ClearLabelOverride();
            _running.Remove(CameraGlitchType.GhostRoom);
        }

        // Mirror - the feed claims to be looking at the security office itself, i.e. the player. When the player
        // allows it, the "feed" is their own desktop wallpaper (read locally, kept only while this runs).
        private IEnumerator MirrorRoutine(CameraFeedHud hud, float duration)
        {
            if (useWallpaper && PlayerWallpaper.Enabled)
            {
                _mirrorTexture = PlayerWallpaper.TryLoad();
                hud.SetMirrorImage(_mirrorTexture);
            }

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
