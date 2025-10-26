using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Whisper.Utils;

namespace Whisper
{
    public class WhisperMicInput : MonoBehaviour
    {
        [Header("Config")]
        public int sampleRate = 16000;          // Whisper models are usually 16 kHz
        public float windowSec = 2.5f;          // target segment length
        public float hopSec = 0.8f;             // step size between updates
        public string deviceName = null;        // null = default mic
        public string modelPath;                // relative to StreamingAssets if isModelPathInStreamingAssets = true
        public bool modelPathInStreamingAssets = true; // whisper manager flag
        public bool toggleWithSpacebar = true;  // press Space to start/stop listening

        [Header("Wiring")]
        public VoiceCommandRouter router;

        [Header("Optional (auto-created if null)")]
        public WhisperManager whisperManager;
        public MicrophoneRecord microphone;

        private WhisperStream _stream;
        private readonly ConcurrentQueue<string> _pendingRoutes = new ConcurrentQueue<string>();
        private bool _createdWhisperManager;
        private bool _createdMicrophone;
        private bool _isListening;
        private string _lastQueuedText;
        private float _nextDispatchTime;
        [SerializeField] private float dispatchCooldownSec = 0.7f; // debounce routing for early updates

        private CancellationTokenSource _cts;

        private async void Start()
        {
            if (router == null)
            {
                Debug.LogWarning("VoiceCommandRouter not assigned");
            }

            // Ensure WhisperManager exists; create on inactive GO so we can set ModelPath before Awake
            if (whisperManager == null)
            {
                var go = new GameObject("WhisperManager");
                go.SetActive(false);
                whisperManager = go.AddComponent<WhisperManager>();
                _createdWhisperManager = true;

                // Configure before Awake/InitModel
                var desiredModel = string.IsNullOrWhiteSpace(modelPath) ? "Models/ggml-small.bin" : modelPath;
                if (modelPathInStreamingAssets && !string.IsNullOrEmpty(desiredModel))
                {
                    desiredModel = desiredModel.TrimStart('\\', '/');
                    // If user pasted full Assets/StreamingAssets path, reduce to relative under StreamingAssets
                    const string assetsPrefixWin = "Assets\\StreamingAssets\\";
                    const string assetsPrefixUnix = "Assets/StreamingAssets/";
                    if (desiredModel.StartsWith(assetsPrefixWin, StringComparison.OrdinalIgnoreCase))
                        desiredModel = desiredModel.Substring(assetsPrefixWin.Length);
                    else if (desiredModel.StartsWith(assetsPrefixUnix, StringComparison.OrdinalIgnoreCase))
                        desiredModel = desiredModel.Substring(assetsPrefixUnix.Length);
                }
                try
                {
                    whisperManager.IsModelPathInStreamingAssets = modelPathInStreamingAssets;
                    whisperManager.ModelPath = desiredModel; // safe: not loaded yet

                    // Auto-detect language (Thai/English)
                    whisperManager.language = "th";
                    whisperManager.translateToEnglish = false;

                    // Lower latency streaming settings
                    whisperManager.noContext = true;
                    whisperManager.singleSegment = true;   // faster finalization per chunk
                    whisperManager.enableTokens = false;
                    whisperManager.tokensTimestamps = false;

                    // Tune stream: shorter step, fewer recurrent iterations
                    var step = Mathf.Max(0.2f, hopSec);
                    whisperManager.stepSec = step;
                    whisperManager.keepSec = 0.1f;
                    whisperManager.lengthSec = Mathf.Max(step * 2f, 0.6f);
                    whisperManager.updatePrompt = false;    // avoid growing prompt cost
                    whisperManager.dropOldBuffer = true;    // original ggml sliding window
                    whisperManager.useVad = true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to preconfigure WhisperManager: {e}");
                    enabled = false;
                    return;
                }

                // Now activate so Awake runs and loads with our settings
                go.SetActive(true);
            }
            else
            {
                // Using existing manager in scene; avoid changing ModelPath if already loading/loaded
                try
                {
                    if (!whisperManager.IsLoaded && !whisperManager.IsLoading)
                    {
                        var desiredModel = string.IsNullOrWhiteSpace(modelPath) ? "Models/ggml-base.bin" : modelPath;
                        if (modelPathInStreamingAssets && !string.IsNullOrEmpty(desiredModel))
                        {
                            desiredModel = desiredModel.TrimStart('\\', '/');
                            const string assetsPrefixWin = "Assets\\StreamingAssets\\";
                            const string assetsPrefixUnix = "Assets/StreamingAssets/";
                            if (desiredModel.StartsWith(assetsPrefixWin, StringComparison.OrdinalIgnoreCase))
                                desiredModel = desiredModel.Substring(assetsPrefixWin.Length);
                            else if (desiredModel.StartsWith(assetsPrefixUnix, StringComparison.OrdinalIgnoreCase))
                                desiredModel = desiredModel.Substring(assetsPrefixUnix.Length);
                        }
                        whisperManager.IsModelPathInStreamingAssets = modelPathInStreamingAssets;
                        whisperManager.ModelPath = desiredModel;
                    }

                    whisperManager.language = "auto";
                    whisperManager.translateToEnglish = false;

                    // Lower latency streaming settings
                    whisperManager.noContext = true;
                    whisperManager.singleSegment = true;
                    whisperManager.enableTokens = false;
                    whisperManager.tokensTimestamps = false;

                    var step = Mathf.Max(0.2f, hopSec);
                    whisperManager.stepSec = step;
                    whisperManager.keepSec = 0.1f;
                    whisperManager.lengthSec = Mathf.Max(step * 2f, 0.6f);
                    whisperManager.updatePrompt = false;
                    whisperManager.dropOldBuffer = true;
                    whisperManager.useVad = true;

                    if (!whisperManager.IsLoaded && !whisperManager.IsLoading)
                    {
                        await whisperManager.InitModel();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to setup existing WhisperManager: {e}");
                    enabled = false;
                    return;
                }
            }

            // Do NOT auto-start microphone/stream; wait for Spacebar toggle
            _cts = new CancellationTokenSource();
            _isListening = false;
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

        private async void StartListening()
        {
            if (_isListening)
                return;

            if (whisperManager == null)
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
                _stream.OnSegmentUpdated += OnStreamSegmentUpdated;   // new: earlier updates
                _stream.OnResultUpdated += OnStreamResultUpdated;     // new: full transcript updates
                _stream.OnStreamFinished += OnStreamFinished;
            }

            _stream.StartStream();
            _isListening = true;
            _lastQueuedText = null;
            _nextDispatchTime = 0f;
            Debug.Log("[Voice] Listening started (Spacebar to stop)");
        }

        private void StopListening()
        {
            if (!_isListening)
                return;

            try
            {
                _stream?.StopStream();
                if (microphone != null && microphone.IsRecording)
                {
                    microphone.StopRecord();
                }
            }
            finally
            {
                _isListening = false;
                Debug.Log("[Voice] Listening stopped (Spacebar to start)");
            }
        }

        private void Update()
        {
            // Toggle with Spacebar (New Input System or Legacy fallback)
            bool spacePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                spacePressed = Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            spacePressed = Input.GetKeyDown(KeyCode.Space);
#endif
            if (toggleWithSpacebar && spacePressed)
            {
                if (_isListening) StopListening();
                else StartListening();
            }

            // Drain recognized texts on main thread and route
            while (_pendingRoutes.TryDequeue(out var text))
            {
                var trimmed = (text ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    try { router?.Route(trimmed); }
                    catch (Exception e) { Debug.LogException(e, this); }
                }
            }
        }

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

            // Debounce & de-dup to avoid spamming router
            if (Time.unscaledTime < _nextDispatchTime) return;
            if (_lastQueuedText != null && string.Equals(_lastQueuedText, cleaned, StringComparison.Ordinal)) return;

            _pendingRoutes.Enqueue(cleaned);
            _lastQueuedText = cleaned;
            _nextDispatchTime = Time.unscaledTime + dispatchCooldownSec;
        }

        private void OnStreamSegmentFinished(WhisperResult segment)
        {
            if (segment == null) return;
            var text = segment.Result;
            if (!string.IsNullOrWhiteSpace(text))
            {
                _pendingRoutes.Enqueue(text.Trim());
            }
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
                _cts?.Cancel();

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
                {
                    microphone.StopRecord();
                }

                if (_createdMicrophone && microphone != null)
                {
                    Destroy(microphone.gameObject);
                }

                if (_createdWhisperManager && whisperManager != null)
                {
                    Destroy(whisperManager.gameObject);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }
    }
}
