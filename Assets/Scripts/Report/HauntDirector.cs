using System.Collections.Generic;
using System.Text;
using GameLogic.Data;
using GameLogic.Night;
using GameLogic.SpawnAndTime;
using UnityEngine;

namespace Report
{
    /// <summary>
    /// Something a HauntBeat can trigger. A haunt loop component registers itself in OnEnable
    /// (see <see cref="SilenceProtocolHaunt"/> for the pattern) and HauntDirector fires it
    /// automatically at its scheduled minute - no change to this file needed for Sprint 5+'s
    /// Radio Check / Camera Betrayal / Impostor Case.
    /// </summary>
    public interface IHauntLoop
    {
        HauntLoopId LoopId { get; }

        /// <summary>True while an encounter from this loop is in progress.</summary>
        bool IsActive { get; }

        /// <summary>
        /// True (the common case) means HauntDirector will not start a new beat of THIS loop, nor
        /// let a new EXCLUSIVE loop start, while this one is active - the Sprint 4 rule.
        ///
        /// False opts a loop out of that rule entirely: it can always fire, even while an
        /// exclusive loop (e.g. Silence Protocol) is active, and its own activity never blocks
        /// anything else either. Sprint 5's Radio Check is the first user of this - the roadmap's
        /// whole point for it is "the radio still calls while The Listener has you," a deliberate
        /// dilemma, not a scheduling conflict to avoid.
        /// </summary>
        bool IsExclusive { get; }

        void Trigger(HauntBeat beat);
    }

    /// <summary>
    /// Fires ambient haunt loops at the minutes <see cref="NightPlanGenerator"/> scheduled them -
    /// the same minute-cursor pattern AnomalyScheduler and GlitchScheduler already use.
    ///
    /// HauntDirector only decides WHEN; each <see cref="IHauntLoop"/> decides WHAT HAPPENS. That
    /// is the same split GlitchDirector (when) / FormGlitchController (how) already established,
    /// carried over on purpose so a new haunt loop is a new component, not a change here.
    ///
    /// Self-bootstrapping like GameFlowManager - drop one in the scene to tune its Inspector
    /// values, or don't; the first access creates one.
    /// </summary>
    public class HauntDirector : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Auto-found in the scene if left empty.")]
        [SerializeField] private NightTimer nightTimer;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private static HauntDirector _instance;

        /// <summary>
        /// The scene's instance, created on demand so nothing breaks if it was never placed.
        /// Returns null outside Play mode rather than littering the scene with objects.
        ///
        /// Do NOT call this from OnDisable/OnDestroy - if the real instance already tore itself
        /// down (its own OnDestroy runs before some other object's OnDisable, order is not
        /// guaranteed), this would spawn a brand new GameObject in the middle of scene teardown
        /// that Unity cannot account for, producing "Some objects were not cleaned up when
        /// closing the scene". Use <see cref="ExistingInstance"/> from teardown code instead.
        /// </summary>
        public static HauntDirector Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (!Application.isPlaying) return null;

                _instance = FindFirstObjectByType<HauntDirector>();
                if (_instance == null)
                {
                    var host = new GameObject("HauntDirector (auto-created)");
                    _instance = host.AddComponent<HauntDirector>();
                }
                return _instance;
            }
        }

        /// <summary>
        /// The instance if one currently exists, otherwise null - never creates one. Safe to call
        /// from OnDisable/OnDestroy, unlike <see cref="Instance"/>.
        /// </summary>
        public static HauntDirector ExistingInstance => _instance;

        private readonly List<HauntBeat> _sorted = new List<HauntBeat>();
        private int _nextIndex;
        private bool _built;
        private readonly Dictionary<HauntLoopId, IHauntLoop> _loops = new Dictionary<HauntLoopId, IHauntLoop>();

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("Multiple HauntDirector instances found! Destroying duplicate.", this);
                Destroy(this);
                return;
            }

            _instance = this;
        }

        void Start()
        {
            if (nightTimer == null)
                nightTimer = FindFirstObjectByType<NightTimer>();

            if (nightTimer == null)
            {
                Debug.LogError("HauntDirector: no NightTimer in the scene - no haunt beat will ever fire.", this);
                enabled = false;
                return;
            }

            nightTimer.OnTimeChanged += OnTimeChanged;
        }

        void OnDestroy()
        {
            if (nightTimer != null)
                nightTimer.OnTimeChanged -= OnTimeChanged;

            if (_instance == this)
                _instance = null;
        }

        /// <summary>Called by an IHauntLoop's OnEnable. Registering twice for the same loop id just replaces the entry.</summary>
        public void Register(IHauntLoop loop)
        {
            if (loop == null) return;
            _loops[loop.LoopId] = loop;
        }

        /// <summary>Called by an IHauntLoop's OnDisable. No-ops if a different instance already replaced this one.</summary>
        public void Unregister(IHauntLoop loop)
        {
            if (loop == null) return;
            if (_loops.TryGetValue(loop.LoopId, out var current) && current == loop)
                _loops.Remove(loop.LoopId);
        }

        /// <summary>True while any registered loop is mid-encounter (exclusive or not).</summary>
        public bool IsAnyHauntActive
        {
            get
            {
                foreach (var loop in _loops.Values)
                {
                    if (loop != null && loop.IsActive) return true;
                }
                return false;
            }
        }

        /// <summary>True while an EXCLUSIVE loop is mid-encounter - the thing new exclusive beats
        /// have to wait out. Non-exclusive loops (Radio Check) never count here.</summary>
        private bool IsAnyExclusiveHauntActive
        {
            get
            {
                foreach (var loop in _loops.Values)
                {
                    if (loop != null && loop.IsExclusive && loop.IsActive) return true;
                }
                return false;
            }
        }

        private void OnTimeChanged(float normalizedTime)
        {
            if (!_built)
            {
                _built = true;
                Build();
            }

            float elapsedMinutes = normalizedTime * nightTimer.NightDurationMinutes;

            while (_nextIndex < _sorted.Count && _sorted[_nextIndex].atMinute <= elapsedMinutes)
            {
                Fire(_sorted[_nextIndex]);
                _nextIndex++;
            }
        }

        private void Build()
        {
            _sorted.Clear();
            _nextIndex = 0;

            if (NightPlanProvider.HasPlan)
                _sorted.AddRange(NightPlanProvider.Current.haunts);

            _sorted.Sort((a, b) => a.atMinute.CompareTo(b.atMinute));

            if (showDebugInfo)
                LogSchedule();
        }

        private void Fire(HauntBeat beat)
        {
            if (beat.loop == HauntLoopId.None) return;

            if (!_loops.TryGetValue(beat.loop, out var loop) || loop == null)
            {
                Debug.LogWarning($"HauntDirector: no IHauntLoop registered for {beat.loop} - is its component in the scene?", this);
                return;
            }

            // Non-exclusive loops (Radio Check) always fire, even over an active exclusive loop -
            // that overlap is the point (see IHauntLoop.IsExclusive). Exclusive loops still wait
            // out whatever exclusive loop is currently running; an active non-exclusive loop never
            // blocks them.
            if (loop.IsExclusive && IsAnyExclusiveHauntActive)
            {
                Debug.LogWarning($"HauntDirector: skipped {beat.loop} at {beat.atMinute:0.##}m - another exclusive haunt is already active.", this);
                return;
            }

            loop.Trigger(beat);

            if (showDebugInfo)
                Debug.Log($"HauntDirector: fired {beat.loop} at {beat.atMinute:0.##}m.", this);
        }

        private void LogSchedule()
        {
            var sb = new StringBuilder("=== Haunt Schedule ===\n");
            foreach (var beat in _sorted)
                sb.AppendLine($"  minute {beat.atMinute,5:0.##}  {beat.loop} in {(beat.room != null ? beat.room.Label : "(no room)")}");
            Debug.Log(sb.ToString(), this);
        }

        [ContextMenu("Force Fire Next")]
        public void ForceFireNext()
        {
            if (!_built) Build();

            if (_nextIndex < _sorted.Count)
            {
                var beat = _sorted[_nextIndex];
                _nextIndex++;
                Fire(beat);
            }
            else
            {
                Debug.Log("HauntDirector: no more haunt beats to fire.", this);
            }
        }
    }
}
