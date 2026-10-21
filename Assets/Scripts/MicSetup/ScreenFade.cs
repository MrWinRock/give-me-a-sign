using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MicSetup
{
    /// <summary>
    /// A black overlay that survives scene loads. Call <see cref="CutToBlack"/> the moment a cutscene ends, load the next
    /// scene, and the overlay fades out by itself once that scene is up - so the cut-off ending never shows a raw jump.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        private static ScreenFade _instance;
        private CanvasGroup _group;
        private float _fadeOutSeconds = 1.2f;
        private bool _fadeOnLoad;

        public static void CutToBlack(float fadeOutSeconds = 1.2f)
        {
            var f = Get();
            f._fadeOutSeconds = fadeOutSeconds;
            f._fadeOnLoad = true;
            f.StopAllCoroutines();
            f._group.alpha = 1f;
        }

        private static ScreenFade Get()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("ScreenFade");
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000; // above everything, including the cinematic canvas

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var imgGo = new GameObject("Black", typeof(RectTransform));
            imgGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)imgGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = imgGo.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;

            _instance = go.AddComponent<ScreenFade>();
            _instance._group = group;
            SceneManager.sceneLoaded += _instance.OnSceneLoaded;
            return _instance;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_fadeOnLoad) return;
            _fadeOnLoad = false;
            StopAllCoroutines();
            StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            yield return null; // let the new scene draw one frame behind the black first
            for (float t = 0f; t < _fadeOutSeconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / _fadeOutSeconds);
                yield return null;
            }
            _group.alpha = 0f;
        }

        private void OnDestroy()
        {
            if (_instance == this) { SceneManager.sceneLoaded -= OnSceneLoaded; _instance = null; }
        }
    }
}
