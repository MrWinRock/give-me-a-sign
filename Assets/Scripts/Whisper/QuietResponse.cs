using System.Collections.Generic;
using GameLogic;
using GameLogic.Data;
using UnityEngine;

namespace Whisper
{
    /// <summary>
    /// Handles anomalies that must be answered with silence: while one is on screen the guard has
    /// to stay off the radio. Any speech resets the countdown; make it through and the anomaly
    /// leaves (and scores like a correct report).
    /// </summary>
    public class QuietResponse : MonoBehaviour
    {
        [Tooltip("Seconds of unbroken quiet needed after the anomaly appears.")]
        [Min(1f)] [SerializeField] private float quietSeconds = 8f;

        private readonly Dictionary<Anomaly, float> _quietSince = new Dictionary<Anomaly, float>();
        private readonly List<Anomaly> _pending = new List<Anomaly>();
        private string _lastHint;

        void OnEnable() => WhisperMicInput.OnSpeechChunk += HandleSpeechChunk;

        void OnDisable()
        {
            WhisperMicInput.OnSpeechChunk -= HandleSpeechChunk;
            SetHint(null);
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return;

            _pending.Clear();
            float soonest = float.MaxValue;

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
                    _pending.Add(anomaly);
                else
                    soonest = Mathf.Min(soonest, remaining);
            }

            foreach (var anomaly in _pending)
            {
                _quietSince.Remove(anomaly);
                anomaly.MarkReported();
                anomaly.ResolveByReport();
            }

            SetHint(soonest < float.MaxValue ? $"STAY QUIET... {Mathf.CeilToInt(soonest)}" : null);
        }

        private static bool NeedsSilence(Anomaly anomaly)
        {
            return anomaly != null && anomaly.isActiveAndEnabled && !anomaly.IsReported
                   && anomaly.State != AnomalyState.Resolved
                   && anomaly.Definition != null && anomaly.Definition.voiceResponse == VoiceResponse.Silence;
        }

        private void HandleSpeechChunk(float rms, float seconds)
        {
            var meter = NoiseMeter.Instance;
            if (meter != null && meter.Classify(rms) == VoiceLevel.Silent) return;

            float now = Time.time;
            var keys = new List<Anomaly>(_quietSince.Keys);
            foreach (var anomaly in keys) _quietSince[anomaly] = now;
        }

        private void SetHint(string text)
        {
            if (text == _lastHint) return;
            _lastHint = text;
            GlobalPushToTalk.Instance?.ShowHint(text);
        }
    }
}
