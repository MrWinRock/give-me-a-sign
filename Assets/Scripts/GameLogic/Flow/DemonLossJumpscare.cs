using System.Collections;
using Audio;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace GameLogic.Flow
{
    /// <summary>
    /// The scare every loss ends on: the Demon fills the screen with a scream, holds, and the
    /// caller cuts straight to the lose screen. Uses the Demon prefab's own art (sprite, or its
    /// jumpscare video if one is assigned), so changing the Demon changes this too.
    /// </summary>
    public static class DemonLossJumpscare
    {
        public const string ScreamSound = "JumpScare";

        private class Host : MonoBehaviour
        {
            public RenderTexture texture;

            void OnDestroy()
            {
                if (texture != null) texture.Release();
            }
        }

        // The overlay is left up on purpose: the scene load that follows removes it, so there is no
        // flash of the game between the scare and the lose screen.
        // visual:false = the Demon already filled the screen (it killed the player): cut to black and let only the scream play.
        public static IEnumerator Play(float seconds, bool visual = true)
        {
            // Cut everything else first so the scream is the only thing heard (with a pile of
            // anomalies out, their own loops and stingers would otherwise bury it).
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (source.isPlaying) source.Stop();
            }

            var root = new GameObject("DemonLossJumpscare", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1100; // above the death/result HUDs, below the Debug panel
            root.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var host = root.AddComponent<Host>();
            var black = NewGraphic<Image>(root.transform, "Black");
            black.color = Color.black;
            Stretch(black.rectTransform);

            if (!visual)
            {
                AudioManager.Instance?.Play(ScreamSound);
                yield return new WaitForSecondsRealtime(Mathf.Max(0.3f, seconds));
                yield break;
            }

            var demon = FindDemon();
            RectTransform face = null;

            if (demon != null && demon.JumpscareVideo != null)
            {
                host.texture = new RenderTexture(1280, 720, 0);
                var video = root.AddComponent<VideoPlayer>();
                video.clip = demon.JumpscareVideo;
                video.renderMode = VideoRenderMode.RenderTexture;
                video.targetTexture = host.texture;
                video.audioOutputMode = VideoAudioOutputMode.None; // the scream below carries the sound
                video.isLooping = true;
                video.Play();

                var raw = NewGraphic<RawImage>(root.transform, "Face");
                raw.texture = host.texture;
                face = raw.rectTransform;
            }
            else if (demon != null && demon.JumpscareSprite != null)
            {
                var image = NewGraphic<Image>(root.transform, "Face");
                image.sprite = demon.JumpscareSprite;
                image.preserveAspect = true;
                face = image.rectTransform;
            }
            else
            {
                var flash = NewGraphic<Image>(root.transform, "Face");
                flash.color = new Color(0.6f, 0f, 0f, 1f);
                face = flash.rectTransform;
            }

            Stretch(face);
            face.localScale = Vector3.one * 0.6f;
            face.DOScale(1.2f, 0.3f).SetEase(Ease.OutExpo).SetUpdate(true).SetLink(root);
            face.DOShakeAnchorPos(Mathf.Max(0.3f, seconds), 40f, 45).SetUpdate(true).SetLink(root);

            AudioManager.Instance?.Play(ScreamSound);

            yield return new WaitForSecondsRealtime(Mathf.Max(0.3f, seconds));
        }

        private static DemonAnomaly FindDemon()
        {
            var library = GameLogic.Night.NightContentLibrary.Load();
            if (library == null) return null;

            foreach (var def in library.anomalies)
            {
                if (def == null || def.prefab == null) continue;

                var demon = def.prefab.GetComponentInChildren<DemonAnomaly>(true);
                if (demon != null) return demon;
            }
            return null;
        }

        private static T NewGraphic<T>(Transform parent, string name) where T : Graphic
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(T));
            go.transform.SetParent(parent, false);
            var graphic = go.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
