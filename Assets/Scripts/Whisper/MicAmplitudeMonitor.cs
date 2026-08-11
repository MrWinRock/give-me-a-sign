using MainMenu;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// Independent raw-microphone loudness meter - no Whisper inference, just RMS amplitude off
    /// Unity's own <see cref="Microphone"/> API. This is what Sprint 4's Silence Protocol (and
    /// Sprint 5's Radio Check) watch to know whether the player is being loud; it costs nothing
    /// while idle and nothing but a cheap array scan per frame while running.
    /// </summary>
    public class MicAmplitudeMonitor : MonoBehaviour
    {
        [Tooltip("Window of audio read per sample, in seconds. Shorter = more responsive, noisier.")]
        [SerializeField] private float sampleWindowSeconds = 0.05f;
        [SerializeField] private int frequency = 16000;
        [Tooltip("0-1 per-frame smoothing toward the new reading. Higher = snappier, more jittery.")]
        [Range(0.05f, 1f)] [SerializeField] private float smoothing = 0.5f;

        private AudioClip _clip;
        private string _device;
        private float[] _samples;

        public float CurrentLevel { get; private set; }

        public bool IsAvailable { get; private set; }

        public bool IsMonitoring { get; private set; }

        public void StartMonitoring()
        {
            if (IsMonitoring) return;
            IsMonitoring = true;
            CurrentLevel = 0f;

            if (Microphone.devices.Length == 0)
            {
                IsAvailable = false;
                return;
            }

            IsAvailable = true;
            _device = ResolveDevice();
            _samples = new float[Mathf.Max(64, Mathf.RoundToInt(frequency * sampleWindowSeconds))];

            // A 1-second looping buffer is plenty - only the last sampleWindowSeconds are ever read.
            _clip = Microphone.Start(_device, true, 1, frequency);
        }

        public void StopMonitoring()
        {
            if (!IsMonitoring) return;

            IsMonitoring = false;
            CurrentLevel = 0f;

            if (IsAvailable && Microphone.IsRecording(_device))
                Microphone.End(_device);

            _clip = null;
        }

        void Update()
        {
            if (!IsMonitoring || !IsAvailable) return;

            if (_clip == null || !Microphone.IsRecording(_device))
            {
                // Something else (WhisperMicInput's push-to-talk) took or ended the device -
                // re-acquire rather than reading a stale clip for the rest of the encounter.
                _clip = Microphone.Start(_device, true, 1, frequency);
                return;
            }

            int micPosition = Microphone.GetPosition(_device);
            int start = micPosition - _samples.Length;
            if (start < 0 || start + _samples.Length > _clip.samples) return; // buffer not warmed up / wrapped this frame

            _clip.GetData(_samples, start);

            float sumSquares = 0f;
            for (int i = 0; i < _samples.Length; i++)
                sumSquares += _samples[i] * _samples[i];

            float rms = Mathf.Sqrt(sumSquares / _samples.Length);
            float gained = rms * Mathf.Max(0.01f, ControlPanelWindow.MicGain);

            CurrentLevel = Mathf.Lerp(CurrentLevel, gained, smoothing);
        }

        private static string ResolveDevice()
        {
            var saved = ControlPanelWindow.MicrophoneDevice;
            if (string.IsNullOrEmpty(saved)) return null;

            foreach (var device in Microphone.devices)
            {
                if (device == saved) return device;
            }

            return null;
        }

        void OnDestroy() => StopMonitoring();
    }
}
