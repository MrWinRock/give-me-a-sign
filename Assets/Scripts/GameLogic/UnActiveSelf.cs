using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    public class UnActiveSelf : MonoBehaviour
    {
        private static readonly int Color1 = Shader.PropertyToID("_Color");

        [Header("Deactivation Settings")]
        [SerializeField] private float deactivateDelay = 4f; // Time in seconds before deactivating
        [SerializeField] private float fadeDuration = 0.5f;  // Fade out duration

        private Coroutine _deactRoutine;

        void OnEnable()
        {
            ResetVisuals();
            if (_deactRoutine != null) StopCoroutine(_deactRoutine);
            _deactRoutine = StartCoroutine(DeactivationRoutine());
        }

        void OnDisable()
        {
            if (_deactRoutine != null)
            {
                StopCoroutine(_deactRoutine);
                _deactRoutine = null;
            }
        }

        private void ResetVisuals()
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                return;
            }

            var graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                Color c = graphic.color;
                graphic.color = new Color(c.r, c.g, c.b, 1f);
                return;
            }

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                sr.color = new Color(c.r, c.g, c.b, 1f);
                return;
            }

            var rend = GetComponent<Renderer>();
            if (rend != null && rend.material != null && rend.material.HasProperty(Color1))
            {
                Color c = rend.material.color;
                rend.material.color = new Color(c.r, c.g, c.b, 1f);
            }
        }

        private IEnumerator DeactivationRoutine()
        {
            float waitBeforeFade = Mathf.Max(0f, deactivateDelay - fadeDuration);
            if (waitBeforeFade > 0f) yield return new WaitForSeconds(waitBeforeFade);

            yield return StartCoroutine(FadeOut(fadeDuration));

            gameObject.SetActive(false);
            _deactRoutine = null;
        }

        private IEnumerator FadeOut(float duration)
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg != null)
            {
                float start = cg.alpha;
                for (float t = 0f; t < duration; t += Time.deltaTime)
                {
                    cg.alpha = Mathf.Lerp(start, 0f, t / duration);
                    yield return null;
                }
                cg.alpha = 0f;
                yield break;
            }

            var graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                Color start = graphic.color;
                for (float t = 0f; t < duration; t += Time.deltaTime)
                {
                    graphic.color = Color.Lerp(start, new Color(start.r, start.g, start.b, 0f), t / duration);
                    yield return null;
                }
                graphic.color = new Color(start.r, start.g, start.b, 0f);
                yield break;
            }

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color start = sr.color;
                for (float t = 0f; t < duration; t += Time.deltaTime)
                {
                    sr.color = Color.Lerp(start, new Color(start.r, start.g, start.b, 0f), t / duration);
                    yield return null;
                }
                sr.color = new Color(start.r, start.g, start.b, 0f);
                yield break;
            }

            var rend = GetComponent<Renderer>();
            if (rend != null && rend.material != null && rend.material.HasProperty(Color1))
            {
                Color start = rend.material.color;
                for (float t = 0f; t < duration; t += Time.deltaTime)
                {
                    rend.material.color = Color.Lerp(start, new Color(start.r, start.g, start.b, 0f), t / duration);
                    yield return null;
                }
                rend.material.color = new Color(start.r, start.g, start.b, 0f);
                yield break;
            }

            if (duration > 0f) yield return new WaitForSeconds(duration);
        }
    }
}