using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// Sprint 7, S-707. Every voice-gated interaction in this game (prayer panel, Incident Report
    /// keyword, Radio Check response, Give Me A Sign, ...) ultimately goes through
    /// WhisperMicInput's recognized-text queue. This gives players with no microphone - or one
    /// Windows won't grant permission for - a way to feed that same queue by typing instead, so the
    /// game stays completable rather than soft-locking on anyone without a working mic.
    /// </summary>
    public class TypedInputFallback : MonoBehaviour
    {
        [Tooltip("Show even when a microphone IS present - lets anyone switch to typing by choice, not only when a mic is genuinely missing.")]
        [SerializeField] private bool forceEnabled;

        [Tooltip("Auto-found in the scene if left empty.")]
        [SerializeField] private WhisperMicInput whisperMicInput;

        [SerializeField] private int boxWidth = 420;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private string _typed = "";
        private bool _noMicDetected;

        void Start()
        {
            _noMicDetected = Microphone.devices.Length == 0;

            if (whisperMicInput == null)
                whisperMicInput = FindFirstObjectByType<WhisperMicInput>();

            if (_noMicDetected)
                Debug.Log("TypedInputFallback: no microphone detected - typing fallback is active.", this);
        }

        void OnGUI()
        {
            if (!_noMicDetected && !forceEnabled) return;
            if (whisperMicInput == null) return;

            GUILayout.BeginArea(new Rect(10f, Screen.height - 60f, boxWidth, 50f), GUI.skin.box);
            GUILayout.BeginHorizontal();

            GUILayout.Label(_noMicDetected ? "No mic - type:" : "Type:", GUILayout.Width(90f));

            GUI.SetNextControlName("TypedInputFallbackField");
            _typed = GUILayout.TextField(_typed, GUILayout.ExpandWidth(true));

            bool enterPressed = Event.current.type == EventType.KeyDown &&
                                 (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);

            if (GUILayout.Button("Send", GUILayout.Width(60f)) || enterPressed)
                Submit();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void Submit()
        {
            if (string.IsNullOrWhiteSpace(_typed)) return;

            whisperMicInput.EnqueueTypedText(_typed.Trim());

            if (showDebugInfo)
                Debug.Log($"TypedInputFallback: sent '{_typed.Trim()}'.", this);

            _typed = "";
        }
    }
}
