using System.Collections;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// The player's measured room noise floor, persisted so it only has to be measured once
    /// (re-run <see cref="MicCalibrationRunner"/> any time - e.g. the player moved to a louder
    /// room - to overwrite it).
    /// </summary>
    public static class MicCalibration
    {
        private const string FloorKey = "Opt_MicNoiseFloor";
        private const float DefaultFloor = 0.02f;

        public static float NoiseFloor
        {
            get => PlayerPrefs.GetFloat(FloorKey, DefaultFloor);
            set
            {
                PlayerPrefs.SetFloat(FloorKey, Mathf.Max(0f, value));
                PlayerPrefs.Save();
            }
        }

        public static bool HasCalibrated => PlayerPrefs.HasKey(FloorKey);
    }

    /// <summary>
    /// One-shot ambient noise measurement: listens for a few seconds, averages
    /// <see cref="MicAmplitudeMonitor.CurrentLevel"/>, and writes the result to
    /// <see cref="MicCalibration.NoiseFloor"/> with a safety margin so normal room tone doesn't
    /// sit right on the "quiet enough" line.
    /// </summary>
    public class MicCalibrationRunner : MonoBehaviour
    {
        [SerializeField] private float durationSeconds = 3f;
        [Tooltip("Stored floor = measured average x this. Higher = more forgiving of a noisy room, but slower to trip on real speech.")]
        [SerializeField] private float safetyMargin = 1.6f;

        public bool IsRunning { get; private set; }
        public float Progress01 { get; private set; }

        public System.Action<float> OnCompleted;

        public void Run(MicAmplitudeMonitor monitor)
        {
            if (IsRunning || monitor == null) return;
            StartCoroutine(RunRoutine(monitor));
        }

        private IEnumerator RunRoutine(MicAmplitudeMonitor monitor)
        {
            IsRunning = true;
            Progress01 = 0f;

            bool ownsMonitor = !monitor.IsMonitoring;
            if (ownsMonitor) monitor.StartMonitoring();

            float elapsed = 0f;
            float sum = 0f;
            int samples = 0;

            while (elapsed < durationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                Progress01 = Mathf.Clamp01(elapsed / durationSeconds);

                if (monitor.IsAvailable)
                {
                    sum += monitor.CurrentLevel;
                    samples++;
                }

                yield return null;
            }

            float average = samples > 0 ? sum / samples : MicCalibration.NoiseFloor;
            float newFloor = Mathf.Max(0.005f, average * safetyMargin);
            MicCalibration.NoiseFloor = newFloor;

            if (ownsMonitor) monitor.StopMonitoring();

            IsRunning = false;
            Progress01 = 1f;
            OnCompleted?.Invoke(newFloor);
        }
    }
}
