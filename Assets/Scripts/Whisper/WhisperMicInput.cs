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
    /// </summary>
    public class WhisperMicInput : MonoBehaviour
    {
        private const string DefaultModelPath = "Models/ggml-tiny.bin";

        public enum VoiceLanguage { English = 0, Thai = 1 }

        [Serializable]
        public class VoiceModel
        {
            public string label;
            [Tooltip("File path under StreamingAssets/Models (not committed to git - *.bin is ignored).")]
            public string modelPath;
            [Tooltip("Language code passed to Whisper: 'en', 'th' or 'auto'.")]
            public string language;
        }

        public const string LanguagePrefKey = "VoiceLanguage";

        [Header("Voice Models (swap the files/languages here, or from the Debug panel)")]
        [Tooltip("Used until the player/Debug panel picks one; the choice is then remembered (PlayerPrefs 'VoiceLanguage').")]
        [SerializeField] private VoiceLanguage defaultLanguage = VoiceLanguage.English;
        [SerializeField] private VoiceModel englishModel = new VoiceModel { label = "English (medium.en)", modelPath = "Models/ggml-medium.en.bin", language = "en" };
        [SerializeField] private VoiceModel thaiModel = new VoiceModel { label = "Thai (thonburian large-v3)", modelPath = "Models/thonburian-large-v3-q5_0.bin", language = "th" };

        [Header("Config")]
        [Tooltip("Whisper models expect 16 kHz input.")]
        public int sampleRate = 16000;
        [Tooltip("Step size between streaming updates, in seconds. Smaller = lower latency but more CPU.")]
        public float hopSec = 0.8f;
        [Tooltip("Microphone device name. Leave empty for the system default mic.")]
        public string deviceName;
        [Tooltip("Filled from the selected Voice Model at start - edit the models above instead.")]
        public string modelPath = DefaultModelPath;
        public bool modelPathInStreamingAssets = true;
        [Tooltip("Filled from the selected Voice Model at start - edit the models above instead.")]
        public string language = "en";

        [Header("Wiring")]
        public VoiceCommandRouter router;
        public PrayUiManager prayUiManager;
        public IncidentReportManager incidentReportManager;

        [Header("Optional (auto-created if null)")]
        public WhisperManager whisperManager;
        public MicrophoneRecord microphone;

        [Header("Routing")]
        [Tooltip("Debounce for early (partial) recognition updates, so the routers aren't spammed.")]
        [SerializeField] private float dispatchCooldownSec = 0.7f;

        // RMS level and duration (seconds) of each chunk heard while push-to-talk is live. NoiseMeter listens.
        public static event Action<float, float> OnSpeechChunk;

        private WhisperStream _stream;
        private readonly ConcurrentQueue<string> _pendingRoutes = new ConcurrentQueue<string>();
        private bool _createdWhisperManager;
        private bool _createdMicrophone;
        private bool _chunkHooked;
        private float _micGain = 1f; // PlayerPrefs-backed, so read once per push-to-talk, not per chunk
        private bool _isListening;
        private string _lastQueuedText;
        private float _nextDispatchTime;

        public VoiceLanguage CurrentLanguage { get; private set; }
        public string CurrentModelLabel => ModelFor(CurrentLanguage).label;
        public bool IsModelReady => whisperManager != null && whisperManager.IsLoaded;
        public bool IsModelLoading => whisperManager == null || whisperManager.IsLoading || !whisperManager.IsLoaded;

        private VoiceModel ModelFor(VoiceLanguage lang) => lang == VoiceLanguage.Thai ? thaiModel : englishModel;

        private void SelectLanguage(VoiceLanguage lang)
        {
            CurrentLanguage = lang;
            var model = ModelFor(lang);
            if (!string.IsNullOrWhiteSpace(model.modelPath)) modelPath = model.modelPath;
            if (!string.IsNullOrWhiteSpace(model.language)) language = model.language;
        }

        // Unloads the current model and loads the other one. Heavy (seconds) - Debug panel / options only.
        public void SwitchLanguage(VoiceLanguage lang)
        {
            if (lang == CurrentLanguage && IsModelReady) return;

            if (whisperManager != null && !_createdWhisperManager)
            {
                Debug.LogWarning("WhisperMicInput: the WhisperManager is scene-owned, so the model can't be swapped at runtime. Clear the 'Whisper Manager' field to let this component own it.", this);
                return;
            }

            PlayerPrefs.SetInt(LanguagePrefKey, (int)lang);
            PlayerPrefs.Save();

            StopListening();
            ReleaseStream();
            if (_createdWhisperManager && whisperManager != null)
                Destroy(whisperManager.gameObject);
            whisperManager = null;
            _createdWhisperManager = false;

            SelectLanguage(lang);
            CreateWhisperManager();
        }

        private void ReleaseStream()
        {
            if (_stream == null) return;

            _stream.OnSegmentFinished -= OnStreamSegmentFinished;
            _stream.OnSegmentUpdated -= OnStreamSegmentUpdated;
            _stream.OnResultUpdated -= OnStreamResultUpdated;
            _stream.OnStreamFinished -= OnStreamFinished;
            _stream = null;
        }

        private void CreateWhisperManager()
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

        private async void Start()
        {
            // Both are optional (every call site null-checks), so a missing reference is expected
            // rather than a misconfiguration - no warning.

            SelectLanguage((VoiceLanguage)PlayerPrefs.GetInt(LanguagePrefKey, (int)defaultLanguage));

            try
            {
                if (whisperManager == null)
                {
                    CreateWhisperManager();
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

            if (!_chunkHooked)
            {
                microphone.OnChunkReady += OnMicChunk;
                _chunkHooked = true;
            }
        }

        private void OnMicChunk(AudioChunk chunk)
        {
            if (!_isListening || chunk.Data == null || chunk.Data.Length == 0) return;

            float sumSquares = 0f;
            for (int i = 0; i < chunk.Data.Length; i++)
                sumSquares += chunk.Data[i] * chunk.Data[i];

            float rms = Mathf.Sqrt(sumSquares / chunk.Data.Length) * _micGain;
            OnSpeechChunk?.Invoke(rms, chunk.Length);
        }

        public void BeginPushToTalk() => StartListening();

        public void EndPushToTalk() => StopListening();

        public void EnqueueTypedText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _pendingRoutes.Enqueue(text.Trim());
        }

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
            _micGain = Mathf.Max(0.01f, MainMenu.ControlPanelWindow.MicGain);
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

                    GlobalPushToTalk.Instance?.OnSpeech(trimmed);
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

                if (_chunkHooked && microphone != null)
                    microphone.OnChunkReady -= OnMicChunk;

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
