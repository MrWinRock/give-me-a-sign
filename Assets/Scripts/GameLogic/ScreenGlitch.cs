using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameLogic
{
    /// <summary>
    /// Whole-screen glitch: shakes the main camera (the HUD canvas rides on it) and spikes chromatic
    /// aberration / lens distortion. Effects hold a <see cref="Token"/> and set its intensity (0-1).
    /// </summary>
    public static class ScreenGlitch
    {
        public sealed class Token
        {
            public float Intensity;
        }

        internal static readonly List<Token> Tokens = new List<Token>();
        private static ScreenGlitchDriver _driver;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Tokens.Clear();
            _driver = null;
        }

        public static Token Acquire(float intensity = 0f)
        {
            var token = new Token { Intensity = intensity };
            Tokens.Add(token);

            if (_driver == null)
            {
                var go = new GameObject("ScreenGlitchDriver");
                _driver = go.AddComponent<ScreenGlitchDriver>();
            }
            return token;
        }

        public static void Release(Token token)
        {
            if (token != null) Tokens.Remove(token);
        }
    }

    [DefaultExecutionOrder(-32000)]
    internal class ScreenGlitchDriver : MonoBehaviour
    {
        private const float StepSeconds = 0.04f;
        private const float MaxShake = 0.12f;      // world units at full intensity
        private const float MaxRoll = 0.6f;        // degrees at full intensity
        private const float BurstShakeScale = 3f;

        private Camera _camera;
        private Vector3 _appliedOffset;
        private float _appliedRoll;

        private float _nextStepAt;
        private Vector3 _stepOffset;
        private float _stepRoll;

        private float _burstUntil;
        private float _nextBurstAt;

        private ChromaticAberration _chromatic;
        private LensDistortion _lens;
        private float _chromaticBase, _lensBase;
        private bool _chromaticWasOverridden, _lensWasOverridden;
        private bool _volumeCaptured;

        void OnDestroy()
        {
            ScreenGlitch.Tokens.Clear();
            Restore();
        }

        // Runs before every other Update (execution order) so GameManager sees a clean camera position.
        void Update()
        {
            UndoCameraOffset();
        }

        void LateUpdate()
        {
            float intensity = 0f;
            foreach (var token in ScreenGlitch.Tokens)
                intensity = Mathf.Max(intensity, token.Intensity);

            if (intensity <= 0.001f)
            {
                Restore();
                return;
            }

            if (_camera == null) _camera = Camera.main;
            float now = Time.unscaledTime;

            if (now >= _nextBurstAt)
            {
                _burstUntil = now + Random.Range(0.08f, 0.18f);
                _nextBurstAt = now + Random.Range(0.35f, 1.1f);
            }
            bool burst = now < _burstUntil;

            if (now >= _nextStepAt)
            {
                _nextStepAt = now + StepSeconds;
                float amount = intensity * MaxShake * (burst ? BurstShakeScale : 1f);
                _stepOffset = new Vector3(Random.Range(-amount, amount), Random.Range(-amount, amount) * 0.6f, 0f);
                _stepRoll = Random.Range(-1f, 1f) * intensity * MaxRoll * (burst ? 2f : 1f);
            }

            if (_camera != null)
            {
                _camera.transform.position += _stepOffset;
                _camera.transform.rotation *= Quaternion.Euler(0f, 0f, _stepRoll);
                _appliedOffset = _stepOffset;
                _appliedRoll = _stepRoll;
            }

            ApplyVolume(intensity, burst);
        }

        private void UndoCameraOffset()
        {
            if (_camera == null || (_appliedOffset == Vector3.zero && _appliedRoll == 0f)) return;

            _camera.transform.position -= _appliedOffset;
            _camera.transform.rotation *= Quaternion.Euler(0f, 0f, -_appliedRoll);
            _appliedOffset = Vector3.zero;
            _appliedRoll = 0f;
        }

        private void CaptureVolume()
        {
            _volumeCaptured = true;

            foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (!volume.isGlobal || volume.profile == null) continue;

                if (_chromatic == null && volume.profile.TryGet(out ChromaticAberration ca))
                {
                    _chromatic = ca;
                    _chromaticBase = ca.intensity.value;
                    _chromaticWasOverridden = ca.intensity.overrideState;
                }
                if (_lens == null && volume.profile.TryGet(out LensDistortion ld))
                {
                    _lens = ld;
                    _lensBase = ld.intensity.value;
                    _lensWasOverridden = ld.intensity.overrideState;
                }
            }
        }

        private void ApplyVolume(float intensity, bool burst)
        {
            if (!_volumeCaptured) CaptureVolume();

            if (_chromatic != null)
            {
                float spike = intensity * (burst ? 0.9f : 0.35f);
                _chromatic.intensity.Override(Mathf.Clamp01(_chromaticBase + spike));
            }

            if (_lens != null)
            {
                float wobble = burst ? Random.Range(-0.35f, 0.35f) * intensity : 0f;
                _lens.intensity.Override(Mathf.Clamp(_lensBase + wobble, -1f, 1f));
            }
        }

        private void Restore()
        {
            UndoCameraOffset();
            if (!_volumeCaptured) return;

            if (_chromatic != null)
            {
                _chromatic.intensity.value = _chromaticBase;
                _chromatic.intensity.overrideState = _chromaticWasOverridden;
            }
            if (_lens != null)
            {
                _lens.intensity.value = _lensBase;
                _lens.intensity.overrideState = _lensWasOverridden;
            }

            _chromatic = null;
            _lens = null;
            _volumeCaptured = false;
        }
    }
}
