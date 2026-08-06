using System;
using System.Collections.Concurrent;
using Pray;
using Report;
using UnityEngine;
using Whisper.Utils;

namespace Whisper
{
    /// <summary>
    /// Owns the microphone + Whisper streaming pipeline behind a push-to-talk gate.
    /// The mic only records (and Whisper only runs) between BeginPushToTalk() and
    /// EndPushToTalk() - e.g. while the Incident Report window's "Hold to Speak"
    /// button is held - so speech recognition costs nothing the rest of the time.
    ///
    /// Recognized text is queued from the Whisper worker thread and drained on the
    /// main thread in Update(), then routed to the prayer, incident report, and
    /// sign request systems.
    /// </summary>
    public class WhisperMicInput : MonoBehaviour
    {
        private const string DefaultModelPath = "Models/ggml-tiny.bin";

        [Header("Config")]
        [Tooltip("Whisper models expect 16 kHz input.")]
        public int sampleRate = 16000;
        [Tooltip("Step size between streaming updates, in seconds. Smaller = lower latency but more CPU.")]
        public float hopSec = 0.8f;
        [Tooltip("Microphone device name. Leave empty for the system default mic.")]
        public string deviceName;
        [Tooltip("Model file path, relative to StreamingAssets when the checkbox below is on.")]
        public string modelPath = DefaultModelPath;
        public bool modelPathInStreamingAssets = true;
        [Tooltip("Spoken language passed to Whisper ('en', 'th', or 'auto'). 'auto' detects per phrase but costs a little extra processing.")]
        public string language = "en";

        [Header("Wiring")]
        public VoiceCommandRouter router;
        public SignRequestSystem signRequestSystem;
        public PrayUiManager prayUiManager;
        public IncidentReportManager incidentReportManager;

        [Header("Optional (auto-created if null)")]
        public WhisperManager whisperManager;
        public MicrophoneRecord microphone;

        [Header("Routing")]
        [Tooltip("Debounce for early (partial) recognition updates, so the routers aren't spammed.")]
        [SerializeField] private float dispatchCooldownSec = 0.7f;

        private WhisperStream _stream;
        private readonly ConcurrentQueue<string> _pendingRoutes = new ConcurrentQueue<string>();
        private bool _createdWhisperManager;
        private bool _createdMicrophone;
        private bool _isListening;
        private string _lastQueuedText;
        private float _nextDispatchTime;

        private async void Start()
        {
            // Both are genuinely optional - every call site already null-checks with ?. - and
            // SignRequestSystem in particular has no component in the scene yet by design (its
            // "Give me a sign" hint mechanic is Sprint 6 work). A missing reference here is
            // expected, not a misconfiguration, so it no longer warns.

            try
            {
                if (whisperManager == null)
                {
                    // Create on an inactive GO so ModelPath is set before its Awake loads the model.
                    var go = new GameObject("WhisperManager");
                    go.SetActive(false);
                    whisperManager = go.AddComponent<WhisperManager>();
                    _createdWhisperManager = true;

                    ApplyModelPath();
                    ApplyStreamingSettings();

                    go.SetActive(true); // Awake runs now, loading the model with our settings
                }
                else
                {
                    // Existing manager in the scene: avoid changing ModelPath if already loading/loaded.
                    if (!whisperManager.IsLoaded && !whisperManager.IsLoading)
                        ApplyModelPath();

                    ApplyStreamingSettings();

                    if (!whisperManager.IsLoaded && !whisperManager.IsLoading)
                        await whisperManager.InitModel();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to configure WhisperManager: {e}");
                enabled = false;
                return;
            }

            // Do NOT auto-start the microphone; wait for an explicit BeginPushToTalk() call.
            _isListening = false;
        }

        private void ApplyModelPath()
        {
            var desiredModel = string.IsNullOrWhiteSpace(modelPath) ? DefaultModelPath : modelPath;
            if (modelPathInStreamingAssets)
                desiredModel = NormalizeModelPath(desiredModel);

            whisperManager.IsModelPathInStreamingAssets = modelPathInStreamingAssets;
            whisperManager.ModelPath = desiredModel;
        }

        /// <summary>Low-latency streaming settings shared by both the created and scene-provided manager.</summary>
        private void ApplyStreamingSettings()
        {
            whisperManager.language = string.IsNullOrWhiteSpace(language) ? "en" : language;
            whisperManager.translateToEnglish = false;

            whisperManager.noContext = true;
            whisperManager.singleSegment = true;   // faster finalization per chunk
            whisperManager.enableTokens = false;
            whisperManager.tokensTimestamps = false;

            // Shorter step keeps latency low; lengthSec bounds how much audio each pass chews on.
            float step = Mathf.Max(0.2f, hopSec);
            whisperManager.stepSec = step;
            whisperManager.keepSec = 0.1f;
            whisperManager.lengthSec = Mathf.Max(step * 2f, 0.6f);
            whisperManager.updatePrompt = false;    // avoid ever-growing prompt cost
            whisperManager.dropOldBuffer = true;    // original ggml sliding window
            whisperManager.useVad = true;           // skip inference while the player is silent
        }

        private static string NormalizeModelPath(string inputPath)
        {
            if (string.IsNullOrEmpty(inputPath))
                return inputPath;

            var normalized = inputPath.TrimStart('\\', '/');

            const string assetsPrefixWin = "Assets\\StreamingAssets\\";
            const string assetsPrefixUnix = "Assets/StreamingAssets/";

            if (normalized.StartsWith(assetsPrefixWin, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(assetsPrefixWin.Length);
            else if (normalized.StartsWith(assetsPrefixUnix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(assetsPrefixUnix.Length);

            return normalized;
        }

        private void ConfigureMicrophoneIfNeeded()
        {
            if (microphone == null)
            {
                var goMic = new GameObject("MicrophoneRecord");
                microphone = goMic.AddComponent<MicrophoneRecord>();
                _createdMicrophone = true;
            }

            microphone.frequency = sampleRate;
            microphone.SelectedMicDevice = string.IsNullOrEmpty(deviceName) ? null : deviceName;
            microphone.useVad = true;
            microphone.vadUpdateRateSec = 0.08f;   // check VAD a bit faster
            microphone.vadLastSec = 0.9f;          // shorter window for earlier speech detection
            microphone.vadThd = 1.0f;
            microphone.vadFreqThd = 100.0f;
            microphone.chunksLengthSec = Mathf.Max(0.15f, hopSec * 0.5f); // smaller chunks for lower latency
            microphone.maxLengthSec = 60;
            microphone.loop = true;
            microphone.echo = false;
        }

        /// <summary>
        /// Explicit push-to-talk entry point for UI-driven mic capture (e.g. the Incident Report
        /// window's "Hold to Speak" button). This is the only way the microphone is triggered.
        /// </summary>
        public void BeginPushToTalk() => StartListening();

        /// <summary>Counterpart to BeginPushToTalk(); call when the UI button is released.</summary>
        public void EndPushToTalk() => StopListening();

        private async void StartListening()
        {
            if (_isListening || whisperManager == null)
                return;

            if (whisperManager.IsLoading)
            {
                Debug.Log("Whisper model is still loading. Please wait...");
                return;
            }

            if (!whisperManager.IsLoaded)
            {
                // As a fallback try to init now
                await whisperManager.InitModel();
                if (!whisperManager.IsLoaded)
                {
                    Debug.LogError("Whisper model failed to load; cannot start listening.");
                    return;
                }
            }

            ConfigureMicrophoneIfNeeded();
            if (!microphone.IsRecording) microphone.StartRecord();

            if (_stream == null)
            {
                _stream = await whisperManager.CreateStream(microphone);
                if (_stream == null)
                {
                    Debug.LogError("Failed to create WhisperStream");
                    return;
                }
                _stream.OnSegmentFinished += OnStreamSegmentFinished;
                _stream.OnSegmentUpdated += OnStreamSegmentUpdated;
                _stream.OnResultUpdated += OnStreamResultUpdated;
                _stream.OnStreamFinished += OnStreamFinished;
            }

            _stream.StartStream();
            _isListening = true;
            _lastQueuedText = null;
            _nextDispatchTime = 0f;
        }

        private void StopListening()
        {
            if (!_isListening)
                return;

            try
            {
                _stream?.StopStream();
                if (microphone != null && microphone.IsRecording)
                    microphone.StopRecord();
            }
            finally
            {
                _isListening = false;
            }
        }

        private void Update()
        {
            // Drain recognized texts on the main thread and route them to each system.
            while (_pendingRoutes.TryDequeue(out var text))
            {
                var trimmed = (text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                try
                {
                    if (IsPrayPanelActive())
                        router?.Route(trimmed);

                    if (IsIncidentReportActive())
                        incidentReportManager?.Route(trimmed);

                    // Sprint 4+: haunt loops (and Sprint 5's Radio Check) register a phrase with
                    // VoicePromptSystem instead of each growing their own routing path here.
                    var voicePrompt = VoicePromptSystem.Instance;
                    if (voicePrompt != null && voicePrompt.IsAwaitingPrompt)
                        voicePrompt.Route(trimmed);

                    signRequestSystem?.Route(trimmed);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        private bool IsPrayPanelActive()
        {
            return prayUiManager != null &&
                   prayUiManager.gameObject.activeInHierarchy &&
                   prayUiManager.IsPrayPanelActive();
        }

        private bool IsIncidentReportActive()
        {
            return incidentReportManager != null &&
                   incidentReportManager.gameObject.activeInHierarchy &&
                   incidentReportManager.IsReportOpen;
        }

        // ---- Whisper stream callbacks (may run off the main thread; only enqueue here) ----

        private void OnStreamSegmentUpdated(WhisperResult segment)
        {
            if (segment == null) return;
            TryEnqueueEarly(segment.Result);
        }

        private void OnStreamResultUpdated(string updated)
        {
            TryEnqueueEarly(updated);
        }

        private void TryEnqueueEarly(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var cleaned = text.Trim();
            if (cleaned.Length < 2) return;

            // Debounce & de-dup to avoid spamming the routers with partial updates.
            if (Time.unscaledTime < _nextDispatchTime) return;
            if (string.Equals(_lastQueuedText, cleaned, StringComparison.Ordinal)) return;

            _pendingRoutes.Enqueue(cleaned);
            _lastQueuedText = cleaned;
            _nextDispatchTime = Time.unscaledTime + dispatchCooldownSec;
        }

        private void OnStreamSegmentFinished(WhisperResult segment)
        {
            if (segment == null) return;
            if (!string.IsNullOrWhiteSpace(segment.Result))
                _pendingRoutes.Enqueue(segment.Result.Trim());
        }

        private void OnStreamFinished(string finalResult)
        {
            if (!string.IsNullOrWhiteSpace(finalResult))
                _pendingRoutes.Enqueue(finalResult.Trim());
        }

        private void OnDestroy()
        {
            try
            {
                if (_stream != null)
                {
                    _stream.OnSegmentFinished -= OnStreamSegmentFinished;
                    _stream.OnSegmentUpdated -= OnStreamSegmentUpdated;
                    _stream.OnResultUpdated -= OnStreamResultUpdated;
                    _stream.OnStreamFinished -= OnStreamFinished;
                    _stream.StopStream();
                    _stream = null;
                }

                if (microphone != null && microphone.IsRecording)
                    microphone.StopRecord();

                if (_createdMicrophone && microphone != null)
                    Destroy(microphone.gameObject);

                if (_createdWhisperManager && whisperManager != null)
                    Destroy(whisperManager.gameObject);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }
    }
}
