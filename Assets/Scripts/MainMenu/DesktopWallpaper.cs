using Report;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// Shows the player's own wallpaper (Wallpaper Engine first, else the Windows one) over the Desktop's
    /// cover picture, cover-cropped so it never stretches. Falls back to the cover picture when none is found
    /// or the Control Panel switch is off - the same source and switch as the Mirror glitch.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DesktopWallpaper : MonoBehaviour
    {
        const float RecheckSeconds = 0.5f;

        RawImage _layer;
        Texture2D _texture;
        bool _wasEnabled;
        float _nextCheck;

        void OnEnable() => Apply();

        void OnDisable() => Release();

        void OnDestroy() => Release();

        void Update()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + RecheckSeconds;

            // The Control Panel writes the switch to PlayerPrefs; pick it up without a scene reload.
            if (PlayerWallpaper.Enabled != _wasEnabled) Apply();
        }

        void OnRectTransformDimensionsChange()
        {
            if (_layer != null) FitCover();
        }

        void Apply()
        {
            Release();
            _wasEnabled = PlayerWallpaper.Enabled;
            if (!_wasEnabled) return;

            _texture = PlayerWallpaper.TryLoad();
            if (_texture == null) return;

            EnsureLayer();
            _layer.texture = _texture;
            _layer.enabled = true;
            FitCover();
        }

        void Release()
        {
            if (_layer != null) _layer.enabled = false;
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
        }

        void EnsureLayer()
        {
            if (_layer != null) return;

            var go = new GameObject("PlayerWallpaper", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            _layer = go.GetComponent<RawImage>();
            _layer.raycastTarget = false; // clicks still reach the Desktop's own button
        }

        void FitCover()
        {
            if (_texture == null) return;

            var area = ((RectTransform)transform).rect;
            if (area.width <= 0f || area.height <= 0f) return;

            float areaAspect = area.width / area.height;
            float textureAspect = (float)_texture.width / _texture.height;

            // Crop the longer side evenly instead of stretching.
            _layer.uvRect = textureAspect > areaAspect
                ? new Rect((1f - areaAspect / textureAspect) * 0.5f, 0f, areaAspect / textureAspect, 1f)
                : new Rect(0f, (1f - textureAspect / areaAspect) * 0.5f, 1f, textureAspect / areaAspect);
        }
    }
}
