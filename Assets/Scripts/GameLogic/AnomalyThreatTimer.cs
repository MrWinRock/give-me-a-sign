using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// Counts down the window the player has to banish an anomaly, then raises
    /// <see cref="OnExpired"/>.
    /// </summary>
    public class AnomalyThreatTimer : MonoBehaviour
    {
        [Tooltip("Seconds the player has once the anomaly turns threatening. 0 or less = no time limit.")]
        [SerializeField] private float timeoutSeconds = 30f;

        public System.Action OnExpired;

        private float _elapsed;

        public bool IsRunning { get; private set; }

        public float Remaining => IsRunning ? Mathf.Max(0f, timeoutSeconds - _elapsed) : 0f;

        public void Begin()
        {
            if (timeoutSeconds <= 0f) return;

            _elapsed = 0f;
            IsRunning = true;
        }

        public void Cancel() => IsRunning = false;

        public void SetTimeout(float seconds) => timeoutSeconds = seconds;

        void Update()
        {
            if (!IsRunning) return;

            _elapsed += Time.deltaTime;
            if (_elapsed < timeoutSeconds) return;

            IsRunning = false;
            OnExpired?.Invoke();
        }

        public void ConfigureFromLegacy(float legacyTimeToDisappear)
        {
            timeoutSeconds = legacyTimeToDisappear;
        }
    }
}
