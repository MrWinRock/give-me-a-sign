using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// Counts down the window the player has to banish an anomaly, then raises
    /// <see cref="OnExpired"/>.
    ///
    /// It only ever announces that time ran out - deciding what that costs the player is
    /// Anomaly's and GameFlowManager's business. That separation is what lets Sprint 4 add new
    /// consequences without editing this file.
    /// </summary>
    public class AnomalyThreatTimer : MonoBehaviour
    {
        [Tooltip("Seconds the player has once the anomaly turns threatening. 0 or less = no time limit.")]
        [SerializeField] private float timeoutSeconds = 30f;

        /// <summary>Raised once when the window closes without the anomaly being banished.</summary>
        public System.Action OnExpired;

        private float _elapsed;

        public bool IsRunning { get; private set; }

        /// <summary>Seconds left, or 0 when not running.</summary>
        public float Remaining => IsRunning ? Mathf.Max(0f, timeoutSeconds - _elapsed) : 0f;

        /// <summary>Starts the countdown. Does nothing when the timeout is 0 or less.</summary>
        public void Begin()
        {
            if (timeoutSeconds <= 0f) return;

            _elapsed = 0f;
            IsRunning = true;
        }

        /// <summary>Stops the countdown without raising OnExpired - the anomaly was dealt with.</summary>
        public void Cancel() => IsRunning = false;

        /// <summary>Overrides the authored window - used when an AnomalyDefinition supplies it.</summary>
        public void SetTimeout(float seconds) => timeoutSeconds = seconds;

        void Update()
        {
            if (!IsRunning) return;

            _elapsed += Time.deltaTime;
            if (_elapsed < timeoutSeconds) return;

            IsRunning = false;
            OnExpired?.Invoke();
        }

        /// <summary>
        /// Seeds this component from the legacy field still on Anomaly. Called only when Anomaly
        /// had to add the component itself at runtime, i.e. on a prefab that hasn't been through
        /// 'Tools/Give Me A Sign/Setup/2. Migrate Anomaly Prefabs' yet.
        /// </summary>
        public void ConfigureFromLegacy(float legacyTimeToDisappear)
        {
            timeoutSeconds = legacyTimeToDisappear;
        }
    }
}
