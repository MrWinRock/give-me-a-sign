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
    /// "Camera Betrayal" executor (HL-5) - the single camera feed's own watermark/timestamp lies
    /// for a beat, then always reverts. Same pure-executor split as FormGlitchController: this
    /// class only knows HOW to run each variant; <see cref="CameraBetrayalHaunt"/> decides WHEN
    /// and WHICH.
    ///
    /// The game has one physical camera that pans between rooms (see GameManager), not a
    /// render-texture-per-room feed, so every variant here is deliberately implemented as a HUD
    /// watermark trick on <see cref="CameraFeedHud"/> rather than a real video effect - Ghost Room
    /// and Mirror in particular are "the label lies" rather than "a new room exists", which needs
    /// no new art and stays inside this system's job. A real freeze-frame / extra camera render
    /// is future content work (Sprint 7's VHS pass and beyond), not a blocker for the mechanic.
    /// </summary>
    public class CameraFeedController : MonoBehaviour
    {
        [Header("Durations")]
        [SerializeField] private Vector2 loopDurationRange = new Vector2(4f, 8f);
        [SerializeField] private Vector2 frozenDurationRange = new Vector2(2f, 4f);
        [SerializeField] private Vector2 blackoutDurationRange = new Vector2(1.5f, 3f);
        [SerializeField] private Vector2 ghostRoomDurationRange = new Vector2(3f, 6f);
        [SerializeField] private Vector2 mirrorDurationRange = new Vector2(3f, 6f);

        [Header("Ghost Room / Mirror text (best-effort - purely a HUD watermark trick, no new art required)")]
        [SerializeField]
        private List<string> ghostRoomLabels = new List<string>
        {
            "CAM 07 — SUB-LEVEL", "CAM 09 — UNKNOWN SECTOR", "CAM 00 — ??????"
        };
        [SerializeField] private string mirrorLabel = "CAM 00 — SECURITY OFFICE";
        [SerializeField] private string mirrorHintText = "...someone is sitting there.";

        [Header("Debug")]
        [SerializeField] private bool verboseLogging;

        private readonly Dictionary<CameraGlitchType, Coroutine> _running = new Dictionary<CameraGlitchType, Coroutine>();

        /// <summary>True while at least one variant is mid-flight.</summary>
        public bool IsGlitchActive => _running.Count > 0;

        /// <summary>
        /// Runs one variant. overrideDuration &lt;= 0 picks randomly from that variant's own range.
        /// Returns false if that variant type is already running, or the HUD isn't available.
        /// </summary>
        public bool PlayGlitch(CameraGlitchType type, float overrideDuration = 0f)
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
                case CameraGlitchType.Loop:      routine = LoopRoutine(hud, overrideDuration); break;
                case CameraGlitchType.Frozen:    routine = FrozenRoutine(hud, overrideDuration); break;
                case CameraGlitchType.Blackout:  routine = BlackoutRoutine(hud, overrideDuration); break;
                case CameraGlitchType.GhostRoom: routine = GhostRoomRoutine(hud, overrideDuration); break;
                case CameraGlitchType.Mirror:    routine = MirrorRoutine(hud, overrideDuration); break;
                default: return false;
            }

            BeginTracked(type, routine);
            if (verboseLogging) Debug.Log($"[CameraFeed] FIRE {type}", this);
            return true;
        }

        /// <summary>
        /// Same "claim the slot before StartCoroutine" trick as FormGlitchController.BeginTracked -
        /// StartCoroutine runs the routine body synchronously up to its first yield, so if a routine
        /// ever finished in one go its own _running.Remove() would fire before the handle could be
        /// stored, leaving a stale entry that blocks that type forever.
        /// </summary>
        private void BeginTracked(CameraGlitchType type, IEnumerator routine)
        {
            _running[type] = null;
            var handle = StartCoroutine(routine);
            if (_running.ContainsKey(type)) _running[type] = handle;
        }

        /// <summary>Stops every running variant and forces the HUD back to normal in one shot. Safe to call anytime.</summary>
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
                hud.UnfreezeTimestamp();
                hud.ClearLabelOverride();
                hud.SetBlackout(false);
            }
        }

        void OnDisable() => CancelAllGlitches();

        private static float ResolveDuration(Vector2 range, float overrideDuration) =>
            overrideDuration > 0f ? overrideDuration : Random.Range(range.x, range.y);

        // Loop - the feed is quietly replaying old footage. Nothing visibly changes except the
        // corner timestamp, which stops advancing - the one tell the roadmap calls out by name
        // ("timestamp มุมจอไม่เดิน"). Camera navigation still works normally; the lie is passive.
        private IEnumerator LoopRoutine(CameraFeedHud hud, float overrideDuration)
        {
            hud.FreezeTimestamp();
            yield return new WaitForSecondsRealtime(ResolveDuration(loopDurationRange, overrideDuration));
            hud.UnfreezeTimestamp();
            _running.Remove(CameraGlitchType.Loop);
        }

        // Frozen - same stuck timestamp as Loop, but announced outright via the label, for a more
        // overt "something is wrong" beat than Loop's quiet version.
        private IEnumerator FrozenRoutine(CameraFeedHud hud, float overrideDuration)
        {
            hud.FreezeTimestamp();
            hud.SetLabelOverride("● REC — SIGNAL FROZEN");
            yield return new WaitForSecondsRealtime(ResolveDuration(frozenDurationRange, overrideDuration));
            hud.UnfreezeTimestamp();
            hud.ClearLabelOverride();
            _running.Remove(CameraGlitchType.Frozen);
        }

        // Blackout - the feed just dies for a beat. CameraFeedHud.SetBlackout doubles as a
        // full-screen cover, so the player has to make a call (switch camera or wait) blind.
        private IEnumerator BlackoutRoutine(CameraFeedHud hud, float overrideDuration)
        {
            hud.SetBlackout(true);
            yield return new WaitForSecondsRealtime(ResolveDuration(blackoutDurationRange, overrideDuration));
            hud.SetBlackout(false);
            _running.Remove(CameraGlitchType.Blackout);
        }

        // Ghost Room - the label briefly claims a camera/room that does not exist anywhere in
        // RoomRegistry. A real extra room is Sprint 3+ art/content work; the watermark lie is what
        // this system can honestly deliver today.
        private IEnumerator GhostRoomRoutine(CameraFeedHud hud, float overrideDuration)
        {
            string label = PickRandom(ghostRoomLabels) ?? "CAM 0? — ??????";
            hud.SetLabelOverride(label);
            yield return new WaitForSecondsRealtime(ResolveDuration(ghostRoomDurationRange, overrideDuration));
            hud.ClearLabelOverride();
            _running.Remove(CameraGlitchType.GhostRoom);
        }

        // Mirror - the feed claims to be looking at the security office itself, i.e. the player.
        private IEnumerator MirrorRoutine(CameraFeedHud hud, float overrideDuration)
        {
            hud.SetLabelOverride($"{mirrorLabel}\n{mirrorHintText}");
            yield return new WaitForSecondsRealtime(ResolveDuration(mirrorDurationRange, overrideDuration));
            hud.ClearLabelOverride();
            _running.Remove(CameraGlitchType.Mirror);
        }

        private static string PickRandom(List<string> pool)
        {
            if (pool == null || pool.Count == 0) return null;
            return pool[Random.Range(0, pool.Count)];
        }
    }
}
