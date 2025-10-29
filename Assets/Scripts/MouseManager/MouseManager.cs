using UnityEngine;
using UnityEngine.InputSystem;

namespace MouseManager
{
    public class MouseManager : MonoBehaviour
    {
        [Header("Spotlight Settings")]
        public Transform spotlight; // Assign the spotlight GameObject in the inspector
        public Camera mainCamera;   // Assign the main camera
    
        private Vector2 _mousePosition;
    
        void Start()
        {
            // If camera is not assigned, use the main camera
            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        void Update()
        {
            // Get mouse position using new Input System
            _mousePosition = Mouse.current.position.ReadValue();
        
            // Convert screen position to world position
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(_mousePosition.x, _mousePosition.y, mainCamera.nearClipPlane));
            worldPosition.z = 0f; // Keep it on the 2D plane
        
            // Move spotlight to follow mouse
            if (spotlight != null)
            {
                spotlight.position = worldPosition;
            }
        }
    }
}
