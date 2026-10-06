using System.Collections.Generic;
using DG.Tweening;
using GameLogic;
using GameLogic.Data;
using GameLogic.Flow;
using UI;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Whisper
{
    /// <summary>
    /// Stealth anomalies: once the player finds one with the cursor, the game holds the radio mic open
    /// and the player must not speak at all. Enough quiet seconds make it leave; any voice - even a
    /// whisper - is heard: night lost.
    /// </summary>
    public class QuietResponse : MonoBehaviour
    {
        // True once the player has found a stealth anomaly and the quiet countdown is running.
        public static bool Engaged { get; private set; }

        [Tooltip("Seconds of unbroken silence needed after the anomaly appears.")]
        [Min(1f)] [SerializeField] private float quietSeconds = 8f;

        [Tooltip("Seconds of normal-or-louder voice it takes to be caught - ignores a single cough or click. The bands come from NoiseMeter.")]
        [Min(0f)] [SerializeField] private float toleranceSeconds = 0.3f;

        [Tooltip("Seconds of whisper-level sound it takes to be caught. Longer than the above so breathing or a chair creak doesn't count, but a whispered sentence does.")]
        [Min(0f)] [SerializeField] private float whisperToleranceSeconds = 0.8f;

        [Tooltip("Seconds after the mic is forced open before sound can catch the player (the open-mic click, reaching for the desk).")]
        [Min(0f)] [SerializeField] private float graceSeconds = 1f;

        private readonly Dictionary<Anomaly, float> _quietSince = new Dictionary<Anomaly, float>();
        private readonly List<Anomaly> _pending = new List<Anomaly>();
        private Anomaly _watched;
        private float _micOpenedAt;
        private float _soundSeconds;
        private float _whisperSeconds;
        private bool _caught;
        private string _lastHint;
        private readonly HashSet<Anomaly> _found = new HashSet<Anomaly>();

        void OnEnable() => WhisperMicInput.OnSpeechChunk += HandleSpeechChunk;

        void OnDisable()
        {
            WhisperMicInput.OnSpeechChunk -= HandleSpeechChunk;
            Release();
        }

        void Update()
        {
            if (_caught || Time.timeScale <= 0f) return;

            _pending.Clear();
            float soonest = float.MaxValue;
            Anomaly soonestAnomaly = null;

            foreach (var anomaly in Anomaly.ActiveAnomalies)
            {
                if (!NeedsSilence(anomaly)) continue;

                // It lurks until the player finds it with the cursor - only then does the mic go live and the countdown start.
                if (!_found.Contains(anomaly))
                {
                    if (!PointerOver(anomaly)) continue;

                    _found.Add(anomaly);
                    _quietSince[anomaly] = Time.time;
                    anomaly.transform.DOPunchScale(Vector3.one * 0.12f, 0.5f, 8).SetLink(anomaly.gameObject);
                }

                float since = _quietSince[anomaly];

                float remaining = quietSeconds - (Time.time - since);
                if (remaining <= 0f)
                {
                    _pending.Add(anomaly);
                }
                else if (remaining < soonest)
                {
                    soonest = remaining;
                    soonestAnomaly = anomaly;
                }
            }

            foreach (var anomaly in _pending)
            {
                _quietSince.Remove(anomaly);
                _found.Remove(anomaly);
                anomaly.MarkReported();
                anomaly.ResolveByReport();
            }

            Engaged = soonestAnomaly != null;

            if (soonestAnomaly != null)
            {
                Hold(soonestAnomaly);
                SetHint(PlayerMessages.Text(MessageId.StealthHint, Mathf.CeilToInt(soonest)), PlayerMessages.Blink(MessageId.StealthHint));
            }
            else
            {
                Release();
            }
        }

        private static bool PointerOver(Anomaly anomaly)
        {
#if ENABLE_INPUT_SYSTEM
            var cam = Camera.main;
            var mouse = Mouse.current;
            if (cam == null || mouse == null) return false;

            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));

            foreach (var r in anomaly.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || !r.enabled) continue;

                var bounds = r.bounds;
                world.z = bounds.center.z;
                if (bounds.Contains(world)) return true;
            }
#endif
            return false;
        }

        private void Hold(Anomaly anomaly)
        {
            if (_watched == null)
            {
                _micOpenedAt = Time.time;
                _soundSeconds = 0f;
                _whisperSeconds = 0f;
            }
            _watched = anomaly;
            GlobalPushToTalk.Instance?.SetForcedOpen(true);
        }

        private void Release()
        {
            Engaged = false;
            _watched = null;
            GlobalPushToTalk.Instance?.SetForcedOpen(false);
            SetHint(null);
        }

        private static bool NeedsSilence(Anomaly anomaly)
        {
            return anomaly != null && anomaly.isActiveAndEnabled && !anomaly.IsReported
                   && anomaly.State != AnomalyState.Resolved
                   && anomaly.Definition != null && anomaly.Definition.voiceResponse == VoiceResponse.Silence;
        }

        private void HandleSpeechChunk(float rms, float seconds)
        {
            if (_caught || _watched == null || Time.timeScale <= 0f) return;
            if (Time.time - _micOpenedAt < graceSeconds) return;

            // Silence resets the count; a whisper counts too, it just takes a little longer than speech.
            var meter = NoiseMeter.Instance;
            var level = meter != null ? meter.Classify(rms) : VoiceLevel.Normal;
            if (level == VoiceLevel.Silent)
            {
                _soundSeconds = 0f;
                _whisperSeconds = 0f;
                return;
            }

            if (level == VoiceLevel.Whisper) _whisperSeconds += seconds;
            else _soundSeconds += seconds;

            if (_soundSeconds >= toleranceSeconds || _soundSeconds + _whisperSeconds >= whisperToleranceSeconds)
                Caught(_watched);
        }

        [ContextMenu("Debug/Pretend I found the stealth anomaly")]
        public void DebugFindAll()
        {
            foreach (var anomaly in Anomaly.ActiveAnomalies)
            {
                if (!NeedsSilence(anomaly) || _found.Contains(anomaly)) continue;
                _found.Add(anomaly);
                _quietSince[anomaly] = Time.time;
            }
        }

        [ContextMenu("Debug/Get Caught Now")]
        public void DebugGetCaughtNow()
        {
            if (_watched != null) Caught(_watched);
        }

        private void Caught(Anomaly anomaly)
        {
            _caught = true;
            Release();

            // The Demon jumpscare + cut to the lose screen is GameFlowManager's job on every loss.
            var def = anomaly.Definition;
            string roomId = anomaly.AssignedRoom != null ? anomaly.AssignedRoom.roomId : null;
            GameFlowManager.Instance?.EndNight(NightOutcome.KilledByAnomaly, def != null ? def.anomalyId : anomaly.name, roomId);
        }

        private void SetHint(string text, bool blink = false)
        {
            if (text == _lastHint) return;
            _lastHint = text;
            GlobalPushToTalk.Instance?.ShowHint(text, blink);
        }
    }
}
