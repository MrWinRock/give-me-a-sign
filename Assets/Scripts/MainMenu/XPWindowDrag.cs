using UnityEngine;
using UnityEngine.EventSystems;

namespace MainMenu
{
    /// <summary>
    /// Makes an XP window draggable by its titlebar, like real Windows XP. Lives ON the
    /// titlebar object (drag events must land on the raycast target being grabbed) and moves
    /// the whole window's RectTransform.
    ///
    /// Assumes the window's pivot/anchors are centered (which is how the builder authors every
    /// window). Clamps so the titlebar can never be dragged above the screen top or below the
    /// taskbar line - a window you can't grab anymore is a window you can't close.
    /// </summary>
    public class XPWindowDrag : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private XPWindowController window;
        [SerializeField] private RectTransform windowRect;
        [Tooltip("Horizontal pixels of the window that must always remain on screen.")]
        [SerializeField] private float minHorizontalVisible = 48f;
        [Tooltip("The titlebar can't sink below this many pixels from the bottom (keeps it above the taskbar).")]
        [SerializeField] private float bottomKeepOut = 44f;

        private Canvas _canvas;

        public void OnPointerDown(PointerEventData eventData)
        {
            // Grabbing the titlebar focuses the window, exactly like clicking its body.
            window?.Focus();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (windowRect == null) return;

            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();

            // Screen-space delta -> canvas units (Screen Space Overlay: divide by scale factor).
            float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            windowRect.anchoredPosition += eventData.delta / scale;

            ClampToParent();
        }

        private void ClampToParent()
        {
            var parent = windowRect.parent as RectTransform;
            if (parent == null) return;

            Vector2 halfParent = parent.rect.size * 0.5f;
            Vector2 halfWindow = windowRect.rect.size * 0.5f;
            Vector2 position = windowRect.anchoredPosition;

            // At least minHorizontalVisible px of the window stays inside the left/right edges,
            // so it can hang off screen XP-style but never disappear entirely.
            float maxX = halfParent.x + halfWindow.x - minHorizontalVisible;
            if (maxX > 0f)
                position.x = Mathf.Clamp(position.x, -maxX, maxX);

            // The titlebar (top edge) stays between the screen top and the taskbar keep-out.
            float topEdge = position.y + halfWindow.y;
            float clampedTop = Mathf.Clamp(topEdge, -halfParent.y + bottomKeepOut, halfParent.y);
            position.y += clampedTop - topEdge;

            windowRect.anchoredPosition = position;
        }
    }
}
