using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace GameLogic
{
    /// <summary>
    /// Plays a fullscreen clip over the current scene when a document is picked up, then removes itself.
    /// The scene underneath is never changed or paused. Built at runtime on a throwaway object, so a
    /// pickup that deactivates itself right after the click can't cut the video short.
    /// </summary>
    public class MailPickupVideo : MonoBehaviour
    {
        private const int CanvasSortOrder = 960;
        private static MailPickupVideo _active;
        private static float _holdSeconds = 0.1f;

        public static void Play(VideoClip clip, float seconds = 0.1f, AudioClip sfx = null)
        {
            // Through the game's SFX channel so the player's volume slider applies.
            if (sfx != null) Audio.AudioManager.Instance?.PlayClip(sfx);
            if (clip == null || _active != null) return;
            _holdSeconds = seconds;
            var go = new GameObject("MailPickupVideo");
            _active = go.AddComponent<MailPickupVideo>();
            _active.StartCoroutine(_active.Run(clip));
        }

        private IEnumerator Run(VideoClip clip)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;

            var bg = new GameObject("Black", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            Stretch(bg.GetComponent<RectTransform>());
            bg.GetComponent<Image>().color = Color.black;
            bg.GetComponent<Image>().raycastTarget = false;
            bg.SetActive(false);

            var rawGo = new GameObject("Video", typeof(RectTransform), typeof(RawImage));
            rawGo.transform.SetParent(canvasGo.transform, false);
            Stretch(rawGo.GetComponent<RectTransform>());
            var raw = rawGo.GetComponent<RawImage>();
            raw.raycastTarget = false;
            raw.enabled = false;

            var rt = new RenderTexture(1920, 1080, 0);
            raw.texture = rt;

            var vp = gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.isLooping = false;
            vp.clip = clip;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.audioOutputMode = VideoAudioOutputMode.None;
            vp.Prepare();

            float timeout = Time.realtimeSinceStartup + 5f;
            while (!vp.isPrepared && Time.realtimeSinceStartup < timeout) yield return null;

            bg.SetActive(true);
            raw.enabled = true;
            vp.Play();

            float end = Time.realtimeSinceStartup + Mathf.Min(_holdSeconds, (float)clip.length);
            while (Time.realtimeSinceStartup < end) yield return null;

            vp.Stop();
            rt.Release();
            Destroy(rt);
            _active = null;
            Destroy(gameObject);
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
    }
}
