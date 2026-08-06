using MainMenu;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// Best-effort short mic clip capture, independent of the Whisper pipeline - raw
    /// <see cref="Microphone"/> API only, same family as <see cref="MicAmplitudeMonitor"/> but
    /// keeping actual sample data instead of just a loudness reading.
    ///
    /// Built for Radio Check's "own voice" variant (see the roadmap: "เก็บ AudioClip จาก mic
    /// buffer ตอนตอบครั้งก่อน แล้ว playback" - capture the clip from a previous response, play it
    /// back later). <see cref="RadioCheckHaunt"/> calls BeginCapture()/EndCapture() around a
    /// Normal-variant response window; whatever gets captured becomes <see cref="LastClip"/> for
    /// a future Own-Voice call to play through <see cref="Audio.AudioManager.PlayClip"/>.
    ///
    /// Deliberately best-effort: if the mic device is already busy (WhisperMicInput's push-to-talk,
    /// or MicAmplitudeMonitor's own capture during an overlapping Silence Protocol encounter),
    /// BeginCapture() just does nothing rather than fight another system for the same physical
    /// device. LastClip simply stays whatever it was before (possibly null, meaning the Own-Voice
    /// variant falls back to the normal call sound) - same "a missing entry just stays silent"
    /// philosophy FormGlitchController's GlitchAudio already uses.
    /// </summary>
    public class PlayerVoiceRecorder : MonoBehaviour
    {
        [SerializeField] private int frequency = 16000;
        [SerializeField] private float maxClipSeconds = 6f;

        /// <summary>Last successfully captured clip, or null if nothing has ever been captured.</summary>
        public AudioClip LastClip { get; private set; }
        public bool HasClip => LastClip != null;

        private string _device;
        private bool _capturing;
        private float _captureStartTime;
        private float _requestedDuration;
        private AudioClip _pendingClip;

        /// <summary>
        /// Starts a short one-shot recording, up to <paramref name="seconds"/> long (clamped to
        /// maxClipSeconds). No-ops silently if there is no mic, or the device is already recording
        /// for something else.
        /// </summary>
        public void BeginCapture(float seconds)
        {
            if (_capturing) return;
            if (Microphone.devices.Length == 0) return;

            _device = ResolveDevice();

            // Someone else already holds this device (WhisperMicInput's push-to-talk, or another
            // system's amplitude monitor) - don't steal it, just skip this capture attempt.
            if (Microphone.IsRecording(_device)) return;

            _requestedDuration = Mathf.Clamp(seconds, 0.5f, Mathf.Max(0.5f, maxClipSeconds));

            var clip = Microphone.Start(_device, false, Mathf.CeilToInt(_requestedDuration) + 1, frequency);
            if (clip == null) return;

            _pendingClip = clip;
            _capturing = true;
            _captureStartTime = Time.unscaledTime;
        }

        /// <summary>
        /// Stops the recording started by BeginCapture() and, if enough was actually captured,
        /// trims it to what was said and stores it as LastClip. Safe to call even if BeginCapture()
        /// never actually started anything.
        /// </summary>
        public void EndCapture()
        {
            if (!_capturing)
            {
                _pendingClip = null;
                return;
            }

            _capturing = false;

            float actualSeconds = Mathf.Min(_requestedDuration, Time.unscaledTime - _captureStartTime);

            if (Microphone.IsRecording(_device))
                Microphone.End(_device);

            if (_pendingClip == null || actualSeconds < 0.3f)
            {
                // Too short to be worth keeping (e.g. the player never actually spoke) - leave
                // whatever LastClip already held untouched rather than overwriting it with noise.
                _pendingClip = null;
                return;
            }

            var trimmed = TrimClip(_pendingClip, actualSeconds, frequency);
            if (trimmed != null)
                LastClip = trimmed;

            _pendingClip = null;
        }

        private static AudioClip TrimClip(AudioClip source, float seconds, int freq)
        {
            int sampleCount = Mathf.Min(source.samples, Mathf.RoundToInt(seconds * freq));
            if (sampleCount <= 0) return null;

            var data = new float[sampleCount * Mathf.Max(1, source.channels)];
            source.GetData(data, 0);

            var trimmed = AudioClip.Create("PlayerVoiceClip", sampleCount, source.channels, freq, false);
            trimmed.SetData(data, 0);
            return trimmed;
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

        void OnDestroy()
        {
            if (_capturing && Microphone.IsRecording(_device))
                Microphone.End(_device);
        }
    }
}
