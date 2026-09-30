using System.Collections.Generic;
using Audio;
using DG.Tweening;
using GameLogic;
using GameLogic.Data;
using GameLogic.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace Whisper
{
    /// <summary>
    /// Anomalies that must be answered with silence: while one is out, the game holds the radio mic
    /// open and the guard has to stay quiet until it leaves. Any speech is heard - jumpscare, night lost.
    /// </summary>
    public class QuietResponse : MonoBehaviour
    {
        [Tooltip("Seconds of unbroken silence needed after the anomaly appears.")]
        [Min(1f)] [SerializeField] private float quietSeconds = 8f;

        [Tooltip("Mic level (x the calibrated noise floor) that counts as a sound. Above breathing, below speech.")]
        [Min(1.1f)] [SerializeField] private float soundThresholdMultiplier = 2.5f;

        [Tooltip("Seconds of sound it takes to be caught - ignores a single cough or click.")]
        [Min(0f)] [SerializeField] private float toleranceSeconds = 0.3f;

        [Tooltip("Seconds after the mic is forced open before sound can catch the player (the open-mic click, reaching for the desk).")]
        [Min(0f)] [SerializeField] private float graceSeconds = 1f;

        [Tooltip("How long the jumpscare shows before the night ends.")]
        [Min(0f)] [SerializeField] private float jumpScareSeconds = 0.9f;

        [SerializeField] private string jumpScareSound = "JumpScare";

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
                SetHint($"MIC LIVE - DON'T MAKE A SOUND... {Mathf.CeilToInt(soonest)}");
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

            float floor = Mathf.Max(0.001f, MicCalibration.NoiseFloor);
            if (rms < floor * soundThresholdMultiplier)
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

            var audio = AudioManager.Instance;
            audio?.Play(jumpScareSound);

            var def = anomaly.Definition;
            ShowJumpScare(def != null ? def.manualImage : null);

            string roomId = anomaly.AssignedRoom != null ? anomaly.AssignedRoom.roomId : null;
            DOVirtual.DelayedCall(jumpScareSeconds, () =>
                GameFlowManager.Instance?.EndNight(NightOutcome.KilledByAnomaly, def != null ? def.anomalyId : anomaly.name, roomId))
                .SetUpdate(true);
        }

        private void ShowJumpScare(Sprite face)
        {
            var root = new GameObject("SilenceJumpScare", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            root.AddComponent<CanvasScaler>();

            var black = NewImage(root.transform, "Black", Color.black);
            Stretch(black.rectTransform);

            if (face != null)
            {
                var image = NewImage(root.transform, "Face", Color.white);
                image.sprite = face;
                image.preserveAspect = true;
                Stretch(image.rectTransform);
                image.rectTransform.localScale = Vector3.one * 0.6f;
                image.rectTransform.DOScale(1.25f, jumpScareSeconds * 0.4f).SetEase(Ease.OutExpo).SetUpdate(true);
                image.rectTransform.DOShakeAnchorPos(jumpScareSeconds, 40f, 40).SetUpdate(true);
            }

            Destroy(root, jumpScareSeconds + 1f);
        }

        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetHint(string text)
        {
            if (text == _lastHint) return;
            _lastHint = text;
            GlobalPushToTalk.Instance?.ShowHint(text);
        }
    }
}
