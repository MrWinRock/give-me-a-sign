using System.Collections.Generic;
using GameLogic;
using GameLogic.Data;
using GameLogic.Flow;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// Stealth anomalies: while one is out, the game holds the radio mic open. Whispering (or saying
    /// nothing) is safe - a whispered report with the right words banishes it like any other, and
    /// 8 quiet seconds make it leave. Speaking louder than a whisper is heard: night lost.
    /// </summary>
    public class QuietResponse : MonoBehaviour
    {
        [Tooltip("Seconds of unbroken silence needed after the anomaly appears.")]
        [Min(1f)] [SerializeField] private float quietSeconds = 8f;

        [Tooltip("Seconds of louder-than-a-whisper sound it takes to be caught - ignores a single cough or click. The whisper band comes from NoiseMeter.")]
        [Min(0f)] [SerializeField] private float toleranceSeconds = 0.3f;

        [Tooltip("Seconds after the mic is forced open before sound can catch the player (the open-mic click, reaching for the desk).")]
        [Min(0f)] [SerializeField] private float graceSeconds = 1f;

        private readonly Dictionary<Anomaly, float> _quietSince = new Dictionary<Anomaly, float>();
        private readonly List<Anomaly> _pending = new List<Anomaly>();
        private Anomaly _watched;
        private float _micOpenedAt;
        private float _soundSeconds;
        private bool _caught;
        private string _lastHint;

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

                if (!_quietSince.TryGetValue(anomaly, out float since))
                {
                    since = Time.time;
                    _quietSince[anomaly] = since;
                }

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
                anomaly.MarkReported();
                anomaly.ResolveByReport();
            }

            if (soonestAnomaly != null)
            {
                Hold(soonestAnomaly);
                SetHint($"MIC LIVE - WHISPER ONLY... {Mathf.CeilToInt(soonest)}");
            }
            else
            {
                Release();
            }
        }

        private void Hold(Anomaly anomaly)
        {
            if (_watched == null)
            {
                _micOpenedAt = Time.time;
                _soundSeconds = 0f;
            }
            _watched = anomaly;
            GlobalPushToTalk.Instance?.SetForcedOpen(true);
        }

        private void Release()
        {
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

            // Breathing and a low whisper pass; normal speech or louder is heard.
            var meter = NoiseMeter.Instance;
            var level = meter != null ? meter.Classify(rms) : VoiceLevel.Normal;
            if (level != VoiceLevel.Normal && level != VoiceLevel.Shout)
            {
                _soundSeconds = 0f;
                return;
            }

            _soundSeconds += seconds;
            if (_soundSeconds >= toleranceSeconds)
                Caught(_watched);
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

        private void SetHint(string text)
        {
            if (text == _lastHint) return;
            _lastHint = text;
            GlobalPushToTalk.Instance?.ShowHint(text);
        }
    }
}
