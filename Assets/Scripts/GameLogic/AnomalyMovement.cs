using System.Collections;
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

        private const float ArrivalDistance = 0.05f;

        private Vector3 _originalScale;

        /// <summary>True while travelling toward the target.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>False when no target is assigned - the anomaly has nowhere to go.</summary>
        public bool HasTarget => moveTarget != null;

        void Awake()
        {
            // Captured before anything can scale us, so a re-run always grows from the same base.
            _originalScale = transform.localScale;
        }

        /// <summary>
        /// Walks to the target, growing on the way. Completes when the target is reached; returns
        /// immediately if there is no target.
        /// </summary>
        public IEnumerator MoveToTarget()
        {
            if (moveTarget == null)
            {
                Debug.LogWarning($"{name} has no target assigned!", this);
                yield break;
            }

            IsMoving = true;
            StartCoroutine(ScaleUp());

            while (moveTarget != null &&
                   Vector3.Distance(transform.position, moveTarget.position) > ArrivalDistance)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    moveTarget.position,
                    moveSpeed * Time.deltaTime);
                yield return null;
            }

            IsMoving = false;
        }

        private IEnumerator ScaleUp()
        {
            Vector3 targetScale = _originalScale * scaleUpAmount;

            while (Vector3.Distance(transform.localScale, targetScale) > 0.01f)
            {
                transform.localScale = Vector3.Lerp(
                    transform.localScale, targetScale, scaleAnimationSpeed * Time.deltaTime);
                yield return null;
            }

            transform.localScale = targetScale;
        }

        /// <summary>Overrides the authored speed - used when an AnomalyDefinition supplies it.</summary>
        public void SetMoveSpeed(float speed) => moveSpeed = speed;

        /// <summary>
        /// Halts movement and the scale animation. Anomaly.StopAllCoroutines() can't reach these
        /// because they belong to this component, so it calls this as well.
        /// </summary>
        public void Stop()
        {
            StopAllCoroutines();
            IsMoving = false;
        }

        /// <summary>
        /// Seeds this component from the legacy fields still on Anomaly. Called only when Anomaly
        /// had to add the component itself at runtime, i.e. on a prefab that hasn't been through
        /// 'Tools/Give Me A Sign/Setup/2. Migrate Anomaly Prefabs' yet.
        /// </summary>
        public void ConfigureFromLegacy(Transform target, float speed, float scaleAmount, float scaleSpeed)
        {
            moveTarget = target;
            moveSpeed = speed;
            scaleUpAmount = scaleAmount;
            scaleAnimationSpeed = scaleSpeed;
        }
    }
}
