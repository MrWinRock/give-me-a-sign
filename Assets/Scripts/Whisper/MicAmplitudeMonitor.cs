using MainMenu;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// Independent raw-microphone loudness meter - no Whisper inference, just RMS amplitude off
    /// Unity's own <see cref="Microphone"/> API. This is what Sprint 4's Silence Protocol (and
    /// Sprint 5's Radio Check) watch to know whether the player is being loud; it costs nothing
    /// while idle and nothing but a cheap array scan per frame while running.
    ///
    /// Device sharing gotcha: WhisperMicInput's push-to-talk pipeline records from the SAME OS
    /// device through the Whisper.Utils MicrophoneRecord wrapper, which calls Microphone.End() on
    /// stop. If that happens while this monitor is also reading the same device, this monitor's
    /// clip goes stale. Update() detects that (the clip stops reporting IsRecording) and silently
    /// re-acquires the device rather than reading dead data for the rest of the night - so a
    /// report filed mid-encounter recovers on its own within a frame or two instead of needing a
    /// scripted hand-off between the two systems.
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

        /// <summary>Smoothed RMS loudness, gained by the player's mic-gain setting. 0 when not monitoring or no mic exists.</summary>
        public float CurrentLevel { get; private set; }

        /// <summary>False when the system has no capture device at all - callers should treat that as "always quiet", never as a fault of the player.</summary>
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

        /// <summary>The player's saved device if it still exists, else the system default (null = default in the Microphone API).</summary>
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
