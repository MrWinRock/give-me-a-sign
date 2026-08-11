using DG.Tweening;
using UnityEngine;

namespace Player.Click
{
    /// <summary>
    /// The little puff that plays where the player clicked: swells while fading out, then
    /// deletes itself. Driven by one DOTween sequence instead of a per-frame Update, so it costs
    /// nothing once the tween is handed over and cannot outlive its own GameObject.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ClickEffect : MonoBehaviour
    {
        [Tooltip("เวลาที่ใช้ก่อนหายไป - how long the puff takes to swell, fade and die.")]
        public float duration = 0.5f;

        [Tooltip("Final scale as a multiple of the starting scale.")]
        public float scaleUp = 1.5f;

        [Tooltip("Shape of the swell. Linear matches the old per-frame behaviour; OutQuad reads punchier.")]
        public Ease scaleEase = Ease.OutQuad;

        private Sequence _sequence;

        void Start()
        {
            var spriteRenderer = GetComponent<SpriteRenderer>();

            // Join, not Append: the swell and the fade are the same beat, not a sequence of two.
            _sequence = DOTween.Sequence()
                .SetTarget(this)
                .Append(transform.DOScale(transform.localScale * scaleUp, duration).SetEase(scaleEase))
                .Join(spriteRenderer.DOFade(0f, duration).SetEase(Ease.Linear))
                .OnComplete(() =>
                {
                    _sequence = null;
                    Destroy(gameObject);
                });
        }

        void OnDestroy()
        {
            _sequence?.Kill();
            _sequence = null;
        }
    }
}
