using DG.Tweening;
using Gaskellgames;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Animated colour treatment for a TMP label - pulses, waves, flickers or breathes.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TextShineEffect : MonoBehaviour
    {
        public enum AnimationType
        {
            PulseShine,      // base <-> shine
            PulseDarken,     // base <-> darken
            ShineToDarken,   // shine <-> darken, slower
            WaveShine,       // base -> shine with an intensity boost, sine-shaped
            FlickerShine,    // hard cuts at random intervals
            BreathingGlow    // darken <-> shine with an intensity swell
        }

        [Header("Animation Settings")]
        [SerializeField] private AnimationType animationType = AnimationType.PulseShine;

        [Tooltip("Higher = faster. Durations are divided by this.")]
        [SerializeField] private float animationSpeed = 1f;

        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool loop = true;

        [Header("Shine Colors")]
        [SerializeField] private Color baseColor = Color.white;
        [SerializeField] private Color shineColor = Color.yellow;
        [SerializeField] private Color darkenColor = Color.gray;

        // NOTE: the ShowIf comparisons are cast to int on purpose. Gaskellgames' drawer unboxes
        // an enum condition with `(int)comparison`, which only works when the boxed object really
        // is an int - passing the enum value itself risks an InvalidCastException in the editor.
        [Header("Intensity Settings")]
        [Tooltip("Colour multiplier at the brightest point of Wave / Breathing.")]
        [ShowIf(new[] { nameof(animationType), nameof(animationType) },
                new object[] { (int)AnimationType.WaveShine, (int)AnimationType.BreathingGlow },
                LogicGate.OR)]
        [SerializeField] private float maxShineIntensity = 2f;

        [Tooltip("Colour multiplier at the dimmest point of Breathing.")]
        [ShowIf(nameof(animationType), (int)AnimationType.BreathingGlow)]
        [SerializeField] private float minDarkenIntensity = 0.3f;

        [Header("Wave Settings")]
        [ShowIf(nameof(animationType), (int)AnimationType.WaveShine)]
        [SerializeField] private float waveFrequency = 2f;

        [ShowIf(nameof(animationType), (int)AnimationType.WaveShine)]
        [SerializeField] private float waveAmplitude = 0.5f;

        // MinMaxSlider (not MinMax): MinMax clamps a single float, MinMaxSlider is the one that
        // draws a two-handled range over a Vector2, which is what a random on/off window is.
        [Header("Flicker Settings")]
        [ShowIf(nameof(animationType), (int)AnimationType.FlickerShine)]
        [MinMaxSlider(0.05f, 1f, true)]
        [SerializeField] private Vector2 flickerOnRange = new Vector2(0.1f, 0.3f);

        [ShowIf(nameof(animationType), (int)AnimationType.FlickerShine)]
        [MinMaxSlider(0.05f, 2f, true)]
        [SerializeField] private Vector2 flickerOffRange = new Vector2(0.2f, 0.8f);

        private TextMeshProUGUI _textMesh;
        private Color _originalColor;
        private Tween _tween;

        private float UnitDuration => 1f / Mathf.Max(0.01f, animationSpeed);

        void Awake()
        {
            _textMesh = GetComponent<TextMeshProUGUI>();
            _originalColor = _textMesh.color;

            // An untouched white baseColor means "whatever the label was authored as".
            if (baseColor == Color.white && _originalColor != Color.white)
                baseColor = _originalColor;
        }

        void Start()
        {
            if (playOnStart) StartAnimation();
        }

        void OnDisable() => StopAnimation();

        void OnDestroy() => StopAnimation();

        [Button]
        public void StartAnimation()
        {
            // The Inspector button can fire before Awake (or in edit mode), where there is no
            // cached label to drive yet.
            if (_textMesh == null) return;

            StopAnimation();

            int loops = loop ? -1 : 1;

            switch (animationType)
            {
                case AnimationType.PulseShine:
                    _tween = ColorLoop(baseColor, shineColor, UnitDuration, loops, Ease.InOutSine);
                    break;

                case AnimationType.PulseDarken:
                    _tween = ColorLoop(baseColor, darkenColor, UnitDuration, loops, Ease.InOutSine);
                    break;

                case AnimationType.ShineToDarken:
                    _tween = ColorLoop(shineColor, darkenColor, UnitDuration * 2f, loops, Ease.InOutSine);
                    break;

                case AnimationType.WaveShine:
                    _tween = IntensityLoop(baseColor, shineColor, 1f, maxShineIntensity,
                                           UnitDuration / Mathf.Max(0.01f, waveFrequency),
                                           Mathf.Clamp01(waveAmplitude * 2f), loops);
                    break;

                case AnimationType.BreathingGlow:
                    _tween = IntensityLoop(darkenColor, shineColor, minDarkenIntensity, maxShineIntensity,
                                           UnitDuration, 1f, loops);
                    break;

                case AnimationType.FlickerShine:
                    _tween = BuildFlicker(loop);
                    break;
            }
        }

        [Button]
        public void StopAnimation()
        {
            _tween?.Kill();
            _tween = null;

            if (_textMesh != null) _textMesh.color = _originalColor;
        }

        private Tween ColorLoop(Color from, Color to, float duration, int loops, Ease ease)
        {
            _textMesh.color = from;

            return DOTween.To(() => _textMesh.color, c => _textMesh.color = c, to, duration)
                .SetEase(ease)
                .SetLoops(loops, LoopType.Yoyo)
                .SetTarget(this);
        }

        private Tween IntensityLoop(Color from, Color to, float fromIntensity, float toIntensity,
                                    float duration, float reach, int loops)
        {
            float driver = 0f;

            return DOTween.To(() => driver, value =>
                {
                    driver = value;

                    Color color = Color.Lerp(from, to, value) * Mathf.Lerp(fromIntensity, toIntensity, value);
                    color.a = baseColor.a; // brightness must never eat the alpha
                    _textMesh.color = color;
                }, reach, duration)
                .SetEase(Ease.InOutSine)
                .SetLoops(loops, LoopType.Yoyo)
                .SetTarget(this);
        }

        private Tween BuildFlicker(bool looping)
        {
            float speed = Mathf.Max(0.01f, animationSpeed);

            var sequence = DOTween.Sequence()
                .AppendCallback(() => _textMesh.color = shineColor)
                .AppendInterval(RandomIn(flickerOnRange) / speed)
                .AppendCallback(() => _textMesh.color = baseColor)
                .AppendInterval(RandomIn(flickerOffRange) / speed)
                .SetTarget(this);

            if (looping)
                sequence.OnComplete(() => _tween = BuildFlicker(true));

            return sequence;
        }

        private static float RandomIn(Vector2 range) => Random.Range(range.x, range.y);

        // ── External control ─────────────────────────────────────────────────────────────

        public void SetAnimationType(AnimationType newType)
        {
            animationType = newType;
            if (_tween != null) StartAnimation(); // only restart if something was already running
        }

        public void SetColors(Color newBaseColor, Color newShineColor, Color newDarkenColor)
        {
            baseColor = newBaseColor;
            shineColor = newShineColor;
            darkenColor = newDarkenColor;
        }

        public void SetAnimationSpeed(float newSpeed) => animationSpeed = Mathf.Max(0.1f, newSpeed);
    }
}
