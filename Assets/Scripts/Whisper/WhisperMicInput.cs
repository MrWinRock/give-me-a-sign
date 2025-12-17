using System;
using System.Collections.Concurrent;
using System.Threading;
using Pray;
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
        public string deviceName;        // null = default mic
        public string modelPath = @"C:\Users\UsEr\Desktop\give-me-a-sign\Assets\StreamingAssets\Models\ggml-small.en.bin"; // absolute path requested by user
        public bool modelPathInStreamingAssets; // treat modelPath as an absolute path, not relative to StreamingAssets
        public bool toggleWithSpacebar = true;  // press Space to start/stop listening
        public bool holdToTalk = true;          // if true, hold spacebar to talk; if false, toggle mode

        [Header("Wiring")]
        public VoiceCommandRouter router;
        public SignRequestSystem signRequestSystem; // New: Reference to sign request system
        public PrayUiManager prayUiManager; // Reference to prayer system

        [Header("Audio")]
        public AudioSource keyDownAudioSource; // AudioSource that plays when spacebar is pressed down
        public AudioSource keyHoldAudioSource; // AudioSource that plays continuously while spacebar is held
        public AudioSource keyUpAudioSource; // AudioSource that plays when spacebar is released
        
        [Header("Audio Settings")]
        [Range(0f, 1f)] public float audioVolume = 1f; // Master volume for all spacebar audio
        public bool enableAudioFeedback = true; // Toggle to enable/disable audio feedback
        public float holdAudioDelay = 0.1f; // Delay before hold audio starts playing

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
        private bool _isSpaceHeld;
        private bool _isHoldAudioPlaying;
        private float _holdAudioStartTime;
        private bool _lastSpaceState;

        private async void Start()
        {
            if (router == null)
            {
                Debug.LogWarning("VoiceCommandRouter not assigned");
            }

            if (signRequestSystem == null)
            {
                Debug.LogWarning("SignRequestSystem not assigned");
            }

            // Validate audio setup
            ValidateAudioSetup();

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
                    desiredModel = NormalizeModelPath(desiredModel);
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
                            desiredModel = NormalizeModelPath(desiredModel);
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

        private string NormalizeModelPath(string inputPath)
        {
            if (string.IsNullOrEmpty(inputPath))
                return inputPath;

            var normalized = inputPath.TrimStart('\\', '/');

            // Handle both Windows and Unix style paths
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
            
            if (holdToTalk)
                Debug.Log("[Voice] Listening started (Hold Spacebar to talk)");
            else
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
                
                if (holdToTalk)
                    Debug.Log("[Voice] Listening stopped (Hold Spacebar to talk again)");
                else
                    Debug.Log("[Voice] Listening stopped (Spacebar to start)");
            }
        }

        private void Update()
        {
            // Check spacebar input
            bool spacePressed = false;
            bool spaceReleased = false;
            bool spaceHeld = false;
            
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                spacePressed = Keyboard.current.spaceKey.wasPressedThisFrame;
                spaceReleased = Keyboard.current.spaceKey.wasReleasedThisFrame;
                spaceHeld = Keyboard.current.spaceKey.isPressed;
            }
#else
            spacePressed = Input.GetKeyDown(KeyCode.Space);
            spaceReleased = Input.GetKeyUp(KeyCode.Space);
            spaceHeld = Input.GetKey(KeyCode.Space);
#endif

            // Handle spacebar audio with improved state management
            if (enableAudioFeedback)
            {
                // Key down event
                if (spacePressed && !_lastSpaceState)
                {
                    _isSpaceHeld = true;
                    _holdAudioStartTime = Time.time;
                    PlayKeyDownAudio();
                    Debug.Log("[Audio] Spacebar pressed - playing key down audio");
                }
                
                // Key hold event (with delay)
                if (spaceHeld && _isSpaceHeld && !_isHoldAudioPlaying)
                {
                    if (Time.time >= _holdAudioStartTime + holdAudioDelay)
                    {
                        PlayKeyHoldAudio();
                        Debug.Log("[Audio] Starting hold audio");
                    }
                }
                
                // Key release event
                if (spaceReleased && _lastSpaceState)
                {
                    _isSpaceHeld = false;
                    StopKeyHoldAudio();
                    PlayKeyUpAudio();
                    Debug.Log("[Audio] Spacebar released - playing key up audio");
                }
                
                // Update last state
                _lastSpaceState = spaceHeld;
            }

            if (toggleWithSpacebar)
            {
                if (holdToTalk)
                {
                    // Hold-to-talk mode: hold spacebar to listen, release to stop
                    if (spacePressed && !_isListening)
                    {
                        StartListening();
                    }
                    else if (spaceReleased && _isListening)
                    {
                        StopListening();
                    }
                }
                else
                {
                    // Toggle mode: press spacebar to toggle listening
                    if (spacePressed)
                    {
                        if (_isListening) StopListening();
                        else StartListening();
                    }
                }
            }

            // Drain recognized texts on main thread and route to both systems
            while (_pendingRoutes.TryDequeue(out var text))
            {
                var trimmed = (text ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    try 
                    { 
                        // Route to prayer system if PrayPanel is active
                        if (IsPrayPanelActive())
                        {
                            router?.Route(trimmed);
                        }
                        
                        // Always route to sign request system
                        signRequestSystem?.Route(trimmed);
                    }
                    catch (Exception e) 
                    { 
                        Debug.LogException(e, this); 
                    }
                }
            }
        }

        private bool IsPrayPanelActive()
        {
            if (prayUiManager == null) return false;
            return prayUiManager.gameObject.activeInHierarchy && prayUiManager.IsPrayPanelActive();
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

        private void PlayKeyDownAudio()
        {
            if (keyDownAudioSource != null && keyDownAudioSource.clip != null)
            {
                keyDownAudioSource.volume = audioVolume;
                keyDownAudioSource.Play();
                Debug.Log("[Audio] Playing key down audio");
            }
            else
            {
                Debug.LogWarning("[Audio] Key down audio source or clip is null");
            }
        }

        private void PlayKeyHoldAudio()
        {
            if (keyHoldAudioSource != null && keyHoldAudioSource.clip != null && !_isHoldAudioPlaying)
            {
                keyHoldAudioSource.volume = audioVolume;
                keyHoldAudioSource.loop = true;
                keyHoldAudioSource.Play();
                _isHoldAudioPlaying = true;
                Debug.Log("[Audio] Playing key hold audio (looped)");
            }
            else if (keyHoldAudioSource == null || keyHoldAudioSource.clip == null)
            {
                Debug.LogWarning("[Audio] Key hold audio source or clip is null");
            }
        }

        private void PlayKeyUpAudio()
        {
            if (keyUpAudioSource != null && keyUpAudioSource.clip != null)
            {
                keyUpAudioSource.volume = audioVolume;
                keyUpAudioSource.Play();
                Debug.Log("[Audio] Playing key up audio");
            }
            else
            {
                Debug.LogWarning("[Audio] Key up audio source or clip is null");
            }
        }

        private void StopKeyHoldAudio()
        {
            if (keyHoldAudioSource != null && _isHoldAudioPlaying)
            {
                keyHoldAudioSource.Stop();
                keyHoldAudioSource.loop = false;
                _isHoldAudioPlaying = false;
                Debug.Log("[Audio] Stopped key hold audio");
            }
        }

        private void ValidateAudioSetup()
        {
            Debug.Log("[Audio] Validating audio setup...");
            
            if (!enableAudioFeedback)
            {
                Debug.Log("[Audio] Audio feedback is disabled");
                return;
            }

            bool hasIssues = false;

            if (keyDownAudioSource == null)
            {
                Debug.LogWarning("[Audio] Key Down Audio Source is not assigned!");
                hasIssues = true;
            }
            else if (keyDownAudioSource.clip == null)
            {
                Debug.LogWarning("[Audio] Key Down Audio Source has no audio clip assigned!");
                hasIssues = true;
            }

            if (keyHoldAudioSource == null)
            {
                Debug.LogWarning("[Audio] Key Hold Audio Source is not assigned!");
                hasIssues = true;
            }
            else if (keyHoldAudioSource.clip == null)
            {
                Debug.LogWarning("[Audio] Key Hold Audio Source has no audio clip assigned!");
                hasIssues = true;
            }

            if (keyUpAudioSource == null)
            {
                Debug.LogWarning("[Audio] Key Up Audio Source is not assigned!");
                hasIssues = true;
            }
            else if (keyUpAudioSource.clip == null)
            {
                Debug.LogWarning("[Audio] Key Up Audio Source has no audio clip assigned!");
                hasIssues = true;
            }

            if (!hasIssues)
            {
                Debug.Log("[Audio] Audio setup validation passed! All AudioSources and clips are assigned.");
            }
            else
            {
                Debug.LogWarning("[Audio] Audio setup has issues. Please assign AudioSources with audio clips in the Inspector.");
            }
        }

        // Public methods for testing audio (can be called from Inspector or other scripts)
        [ContextMenu("Test Key Down Audio")]
        public void TestKeyDownAudio()
        {
            Debug.Log("[Audio Test] Testing Key Down Audio");
            PlayKeyDownAudio();
        }

        [ContextMenu("Test Key Hold Audio")]
        public void TestKeyHoldAudio()
        {
            Debug.Log("[Audio Test] Testing Key Hold Audio");
            StopKeyHoldAudio(); // Stop first in case it's already playing
            PlayKeyHoldAudio();
        }

        [ContextMenu("Test Key Up Audio")]
        public void TestKeyUpAudio()
        {
            Debug.Log("[Audio Test] Testing Key Up Audio");
            PlayKeyUpAudio();
        }

        [ContextMenu("Stop All Audio")]
        public void StopAllAudio()
        {
            Debug.Log("[Audio Test] Stopping all audio");
            StopKeyHoldAudio();
        }

        private void OnDestroy()
        {
            try
            {
                // Stop all audio first
                StopKeyHoldAudio();
                
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