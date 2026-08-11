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

        bool IsActive { get; }

        bool IsExclusive { get; }

        void Trigger(HauntBeat beat);
    }

    /// <summary>
    /// Fires ambient haunt loops at the minutes <see cref="NightPlanGenerator"/> scheduled them -
    /// the same minute-cursor pattern AnomalyScheduler and GlitchScheduler already use.
    /// </summary>
    public class HauntDirector : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Auto-found in the scene if left empty.")]
        [SerializeField] private NightTimer nightTimer;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private static HauntDirector _instance;

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

        public static HauntDirector ExistingInstance => _instance;

        private readonly List<HauntBeat> _sorted = new List<HauntBeat>();
        private int _nextIndex;
        private bool _built;
        private readonly Dictionary<HauntLoopId, IHauntLoop> _loops = new Dictionary<HauntLoopId, IHauntLoop>();
        private GlitchDirector _glitchDirector;

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
            _glitchDirector = FindFirstObjectByType<GlitchDirector>();

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

        public void Register(IHauntLoop loop)
        {
            if (loop == null) return;
            _loops[loop.LoopId] = loop;
        }

        public void Unregister(IHauntLoop loop)
        {
            if (loop == null) return;
            if (_loops.TryGetValue(loop.LoopId, out var current) && current == loop)
                _loops.Remove(loop.LoopId);
        }

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

            // Sprint 6, S-604: night 1 is the tutorial - NightPlanRunner sets this flag on
            // GlitchDirector for night 1 only. Checked here (not in NightPlanGenerator) so no
            // haunt loop, present or future, needs its own tutorial-awareness.
            if (_glitchDirector != null && _glitchDirector.GetFlag("tutorial"))
            {
                if (showDebugInfo)
                    Debug.Log($"HauntDirector: skipped {beat.loop} at {beat.atMinute:0.##}m - tutorial night.", this);
                return;
            }

            if (!_loops.TryGetValue(beat.loop, out var loop) || loop == null)
            {
                Debug.LogWarning($"HauntDirector: no IHauntLoop registered for {beat.loop} - is its component in the scene?", this);
                return;
            }

            // Non-exclusive loops (Radio Check) fire over an active exclusive loop on purpose;
            // only exclusive loops wait for each other.
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
