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

        // PlayerPrefs is registry-backed on Windows, so the gain is read once per session; SetGain updates it live.
        private float _cachedMicGain = 1f;

        public float CurrentLevel { get; private set; }

        // Unsmoothed, gained RMS of the latest window - what the setup wizard measures.
        public float RawLevel { get; private set; }

        // Windows reads that produced data since the last start; stays 0 when the OS blocks the microphone.
        public int FramesRead { get; private set; }

        // The device being read, or null for the system default. Settable so the wizard can try another before anything is saved.
        public string Device { get; private set; }

        private bool _hasDeviceOverride;
        private string _deviceOverride;

        public bool IsAvailable { get; private set; }

        public bool IsMonitoring { get; private set; }

        // The OS is actually delivering audio (position advances) - false when the microphone is blocked or held by another app.
        public bool IsReceivingAudio => IsMonitoring && IsAvailable && Microphone.IsRecording(_device) && Microphone.GetPosition(_device) > 0;

        public void StartMonitoring()
        {
            if (IsMonitoring) return;
            IsMonitoring = true;
            CurrentLevel = 0f;
            RawLevel = 0f;
            FramesRead = 0;

            if (Microphone.devices.Length == 0)
            {
                IsAvailable = false;
                return;
            }

            IsAvailable = true;
            _device = _hasDeviceOverride ? _deviceOverride : ResolveDevice();
            Device = _device;
            _samples = new float[Mathf.Max(64, Mathf.RoundToInt(frequency * sampleWindowSeconds))];
            _cachedMicGain = Mathf.Max(0.01f, ControlPanelWindow.MicGain);

            // A 1-second looping buffer is plenty - only the last sampleWindowSeconds are ever read.
            _clip = Microphone.Start(_device, true, 1, frequency);
        }

        public void SetGain(float gain) => _cachedMicGain = Mathf.Max(0.01f, gain);

        // Null = the system default. Restarts the capture if it is already running.
        public void SetDevice(string device)
        {
            _hasDeviceOverride = true;
            _deviceOverride = string.IsNullOrEmpty(device) ? null : device;

            if (!IsMonitoring) return;
            StopMonitoring();
            StartMonitoring();
        }

        public void StopMonitoring()
        {
            if (!IsMonitoring) return;

            IsMonitoring = false;
            CurrentLevel = 0f;
            RawLevel = 0f;

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
            float gained = rms * _cachedMicGain;

            RawLevel = gained;
            FramesRead++;
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
