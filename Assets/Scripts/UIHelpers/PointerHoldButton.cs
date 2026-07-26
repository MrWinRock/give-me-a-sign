using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace UIHelpers
{
    /// <summary>
    /// Fires UnityEvents on pointer down / up / exit-while-held. Used for press-and-hold
    /// controls (e.g. the Incident Report window's Push-to-Talk button) where the built-in
    /// Button component's onClick isn't enough.
    /// Dragging off the button while held, or the button being disabled mid-press, counts
    /// as a release so callers never get stuck in a "held" state.
    /// </summary>
    public class PointerHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool interactable = true;
        public UnityEvent onPress;
        public UnityEvent onRelease;

        private bool _isHeld;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable || _isHeld) return;
            _isHeld = true;
            onPress?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ReleaseIfHeld();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ReleaseIfHeld();
        }

        private void ReleaseIfHeld()
        {
            if (!_isHeld) return;
            _isHeld = false;
            onRelease?.Invoke();
        }

        void OnDisable()
        {
            ReleaseIfHeld();
        }
    }
}
