using DG.Tweening;
using Gaskellgames;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// Waits, fades whatever renderer is on this GameObject, then switches it off.
    /// </summary>
    public class UnActiveSelf : MonoBehaviour
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Header("Deactivation Settings")]
        [Tooltip("Skip the wait and start fading immediately.")]
        [SerializeField] private bool noDelay;

        [Tooltip("Switch off instantly with no fade at all.")]
        [SerializeField] private bool noFade;

        [HideIf(nameof(noDelay))]
        [Tooltip("Seconds before this object switches itself off.")]
        [SerializeField] private float deactivateDelay = 4f;

        [HideIf(nameof(noFade))]
        [Tooltip("How long the fade-out takes. Counted as part of the delay, not added to it.")]
        [SerializeField] private float fadeDuration = 0.5f;

        [HideIf(nameof(noFade))]
        [Tooltip("Shape of the fade. Linear matches the old hand-written behaviour.")]
        [SerializeField] private Ease fadeEase = Ease.Linear;

        private Sequence _sequence;

        void OnEnable()
        {
            ResetVisuals();
            PlaySequence();
        }

        void OnDisable() => KillSequence();

        void OnDestroy() => KillSequence();

        private void PlaySequence()
        {
            KillSequence();

            _sequence = DOTween.Sequence().SetTarget(this);

            if (!noDelay)
            {
                // The fade eats into the delay rather than extending it - that was the original
                // behaviour and changing it would make every existing prefab linger longer.
                float wait = noFade ? deactivateDelay : Mathf.Max(0f, deactivateDelay - fadeDuration);
                if (wait > 0f) _sequence.AppendInterval(wait);
            }

            if (!noFade)
            {
                var fade = BuildFadeTween();
                if (fade != null) _sequence.Append(fade.SetEase(fadeEase));
            }

            _sequence.OnComplete(() =>
            {
                // Dropped BEFORE deactivating: SetActive(false) runs OnDisable synchronously,
                // which would otherwise try to Kill the very sequence delivering this callback.
                _sequence = null;
                gameObject.SetActive(false);
            });
        }

        private Tween BuildFadeTween()
        {
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                return canvasGroup.DOFade(0f, fadeDuration);

            var graphic = GetComponent<Graphic>();
            if (graphic != null)
                return graphic.DOFade(0f, fadeDuration);

            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                return spriteRenderer.DOFade(0f, fadeDuration);

            var renderer3D = GetComponent<Renderer>();
            if (renderer3D != null && renderer3D.material != null && renderer3D.material.HasProperty(ColorId))
                return renderer3D.material.DOFade(0f, fadeDuration);

            return null;
        }

        private void ResetVisuals()
        {
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                return;
            }

            var graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.color = WithFullAlpha(graphic.color);
                return;
            }

            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = WithFullAlpha(spriteRenderer.color);
                return;
            }

            var renderer3D = GetComponent<Renderer>();
            if (renderer3D != null && renderer3D.material != null && renderer3D.material.HasProperty(ColorId))
                renderer3D.material.color = WithFullAlpha(renderer3D.material.color);
        }

        private static Color WithFullAlpha(Color color) => new Color(color.r, color.g, color.b, 1f);

        private void KillSequence()
        {
            if (_sequence == null) return;

            _sequence.Kill();
            _sequence = null;
        }
    }
}
