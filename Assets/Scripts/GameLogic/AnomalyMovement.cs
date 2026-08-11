using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// Moves an anomaly toward its target and swells it as it comes. Nothing here knows about
    /// prayers, reports or losing the night - it just travels and reports when it has arrived.
    /// </summary>
    public class AnomalyMovement : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Empty GameObject the anomaly walks toward (usually just in front of the camera).")]
        [SerializeField] private Transform moveTarget;
        [SerializeField] private float moveSpeed = 3f;

        [Header("Scale Animation")]
        [Tooltip("Multiplier applied to the starting scale as it approaches. 1 = no growth.")]
        [SerializeField] private float scaleUpAmount = 1.5f;
        [SerializeField] private float scaleAnimationSpeed = 2f;

        private float ScaleDuration => 1f / Mathf.Max(0.01f, scaleAnimationSpeed);

        private Vector3 _originalScale;
        private Tween _moveTween;
        private Tween _scaleTween;

        public bool IsMoving { get; private set; }

        public bool HasTarget => moveTarget != null;

        void Awake()
        {
            // Captured before anything can scale us, so a re-run always grows from the same base.
            _originalScale = transform.localScale;
        }

        public IEnumerator MoveToTarget()
        {
            if (moveTarget == null)
            {
                Debug.LogWarning($"{name} has no target assigned!", this);
                yield break;
            }

            IsMoving = true;
            KillTweens();

            _scaleTween = transform.DOScale(_originalScale * scaleUpAmount, ScaleDuration)
                .SetEase(Ease.OutSine)
                .SetTarget(this);

            _moveTween = transform.DOMove(moveTarget.position, moveSpeed)
                .SetSpeedBased()
                .SetEase(Ease.Linear)
                .SetTarget(this);

            yield return _moveTween.WaitForCompletion();

            IsMoving = false;
        }

        public void SetMoveSpeed(float speed) => moveSpeed = speed;

        public void Stop()
        {
            StopAllCoroutines();
            KillTweens();
            IsMoving = false;
        }

        void OnDestroy() => KillTweens();

        private void KillTweens()
        {
            _moveTween?.Kill();
            _scaleTween?.Kill();
            _moveTween = null;
            _scaleTween = null;
        }

        public void ConfigureFromLegacy(Transform target, float speed, float scaleAmount, float scaleSpeed)
        {
            moveTarget = target;
            moveSpeed = speed;
            scaleUpAmount = scaleAmount;
            scaleAnimationSpeed = scaleSpeed;
        }
    }
}
