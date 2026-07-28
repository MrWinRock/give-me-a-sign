using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    /// <summary>
    /// Moves the flashlight/spotlight sprite so it follows the mouse cursor on the 2D plane.
    /// </summary>
    public class MouseManager : MonoBehaviour
    {
        [Header("Spotlight Settings")]
        public Transform spotlight; // Assign the spotlight GameObject in the inspector
        public Camera mainCamera;   // Assign the main camera

        void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        void Update()
        {
            if (spotlight == null || mainCamera == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return; // no mouse device connected

            Vector2 mousePosition = mouse.position.ReadValue();
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(
                new Vector3(mousePosition.x, mousePosition.y, mainCamera.nearClipPlane));
            worldPosition.z = 0f; // keep it on the 2D plane

            spotlight.position = worldPosition;
        }
    }
}
