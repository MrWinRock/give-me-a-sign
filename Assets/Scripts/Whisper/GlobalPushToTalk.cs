using GameLogic.Story;
using Report;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Whisper
{
    /// <summary>
    /// Hold V to talk from anywhere in the shift (Spacebar already opens the report form). Drives
    /// the same WhisperMicInput push-to-talk as the report's button, so everything said still
    /// flows through NoiseMeter and the voice-prompt routing. Shows a small corner mic indicator.
    /// </summary>
    public class GlobalPushToTalk : MonoBehaviour
    {
        [Tooltip("Auto-found in the scene if left empty.")]
        [SerializeField] private WhisperMicInput mic;

        [SerializeField] private KeyCode talkKey = KeyCode.V;

        private PushToTalkHud _hud;
        private bool _talking;

        void Awake()
        {
            if (mic == null) mic = FindFirstObjectByType<WhisperMicInput>(FindObjectsInactive.Include);
        }

        void OnEnable()
        {
            _hud = PushToTalkHud.Create();
        }

        void OnDisable()
        {
            StopTalking();
            _hud?.Destroy();
            _hud = null;
        }

        void Update()
        {
            if (mic == null) return;

            if (_talking && (!IsKeyHeld() || IsBlocked()))
            {
                StopTalking();
            }
            else if (!_talking && IsKeyDown() && !IsBlocked())
            {
                _talking = true;
                mic.BeginPushToTalk();
                _hud?.SetTalking(true);
            }
        }

        private void StopTalking()
        {
            if (!_talking) return;

            _talking = false;
            if (mic != null) mic.EndPushToTalk();
            _hud?.SetTalking(false);
        }

        private bool IsKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(talkKey);
#endif
        }

        private bool IsKeyHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.vKey.isPressed;
#else
            return Input.GetKey(talkKey);
#endif
        }

        // The report form has its own Hold-to-Speak button, so two push-to-talks never fight over the mic.
        private static bool IsBlocked()
        {
            if (Time.timeScale <= 0f) return true;
            if (CinematicPlayer.IsAnyPlaying) return true;
            var report = IncidentReportManager.Instance;
            return report != null && report.IsReportOpen;
        }
    }
}
