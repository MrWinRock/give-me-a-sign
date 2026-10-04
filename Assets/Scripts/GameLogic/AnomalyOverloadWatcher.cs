using GameLogic.Flow;
using GameLogic.Night;
using UnityEngine;

// Aliased, not imported: this file uses UnityEngine's [Min].
// See CLAUDE.md - "Gaskellgames" for the project-wide rule.
using GG = Gaskellgames;

namespace GameLogic
{
    /// <summary>
    /// Loses the night when too many anomalies sit unresolved for too long.
    ///
    /// SUSTAINED, not cumulative: the timer resets the moment the count drops back to the limit,
    /// so the player is punished for letting the board run away from them, not for a brief spike.
    /// Reads the live count from Anomaly.ActiveAnomalies rather than keeping its own tally.
    /// </summary>
    public class AnomalyOverloadWatcher : MonoBehaviour
    {
        [Header("Tuning")]
        [GG.InfoBox("Limits come from the night's DifficultyProfile (per-night rows). The fields below are only used when no profile is available.")]
        [Tooltip("Fallback: unresolved anomalies allowed at once. Going ABOVE this starts the timer.")]
        [Min(1)] [SerializeField] private int fallbackMaxConcurrent = 3;

        [Tooltip("Fallback: seconds the overload must be sustained before losing.")]
        [Min(1f)] [SerializeField] private float fallbackOverloadDuration = 120f;

        [Tooltip("Seconds the overload must be sustained before losing. Above 0 this replaces the per-night value from the DifficultyProfile; 0 = use the profile.")]
        [Min(0f)] [SerializeField] private float overloadDurationOverride = 60f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private int _maxConcurrent;
        private float _overloadDuration;
        private float _overloadedFor;
        private bool _fired;
        private bool _warned;
        private OverloadWarningHud _hud;

        /// <summary>Seconds the board has been over the limit. 0 whenever it is not.</summary>
        public float OverloadedSeconds => _overloadedFor;

        /// <summary>0-1 progress toward the loss, for a HUD warning.</summary>
        public float OverloadProgress01 =>
            _overloadDuration > 0f ? Mathf.Clamp01(_overloadedFor / _overloadDuration) : 0f;

        public int MaxConcurrent => _maxConcurrent;

        void Start()
        {
            _hud = OverloadWarningHud.Create();
            ApplyTuning();
        }

        void OnDestroy()
        {
            _hud?.Destroy();
            _hud = null;
        }

        /// <summary>
        /// Pulls this night's limits from the plan. Called again by GameFlowManager if the night
        /// index changes without a scene reload.
        /// </summary>
        public void ApplyTuning()
        {
            _maxConcurrent = fallbackMaxConcurrent;
            _overloadDuration = overloadDurationOverride > 0f ? overloadDurationOverride : fallbackOverloadDuration;

            var library = NightContentLibrary.Load();
            if (library == null || library.difficulty == null) return;

            int night = NightPlanProvider.HasPlan
                ? NightPlanProvider.Current.nightIndex
                : GameFlowManager.CurrentDay;

            _maxConcurrent = library.difficulty.MaxConcurrentAnomaliesFor(night);
            _overloadDuration = overloadDurationOverride > 0f
                ? overloadDurationOverride
                : library.difficulty.OverloadDurationFor(night);

            if (showDebugInfo)
                Debug.Log($"[AnomalyOverloadWatcher] night {night}: >{_maxConcurrent} anomalies for {_overloadDuration:0}s = loss.", this);
        }

        void Update()
        {
            if (_fired) return;

            int unresolved = CountUnresolved();

            if (unresolved <= _maxConcurrent)
            {
                if (_overloadedFor > 0f && showDebugInfo)
                    Debug.Log($"[AnomalyOverloadWatcher] recovered at {unresolved} anomalies - timer reset.", this);

                _overloadedFor = 0f;
                if (_warned)
                {
                    _warned = false;
                    _hud?.Hide();
                }
                return;
            }

            if (!_warned)
            {
                _warned = true;
                _hud?.Show(unresolved, _maxConcurrent);
            }

            _overloadedFor += Time.deltaTime;
            _hud?.SetSecondsLeft(_overloadDuration - _overloadedFor);

            if (_overloadedFor < _overloadDuration) return;

            _fired = true;
            _hud?.Hide();
            Debug.Log($"[AnomalyOverloadWatcher] {unresolved} anomalies held above {_maxConcurrent} for {_overloadDuration:0}s - night lost.", this);

            GameFlowManager.Instance?.EndNight(NightOutcome.Negligence, causeAnomalyId: OverloadCauseId);
        }

        /// <summary>Cause id recorded on the night's result, so the death screen can name this loss.</summary>
        public const string OverloadCauseId = "anomaly_overload";

        /// <summary>
        /// Anomalies on screen and not yet dealt with. Resolved ones are excluded because they
        /// are already on their way out and the player has nothing left to do about them.
        /// </summary>
        private static int CountUnresolved()
        {
            int count = 0;
            var active = Anomaly.ActiveAnomalies;

            for (int i = 0; i < active.Count; i++)
            {
                var anomaly = active[i];
                if (anomaly != null && anomaly.State != AnomalyState.Resolved) count++;
            }

            return count;
        }
    }
}
