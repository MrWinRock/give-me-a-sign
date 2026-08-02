using System.Collections.Generic;
using UnityEngine;

namespace GameLogic
{
    public class GameManager : MonoBehaviour
    {
        [Header("Background Settings")]
        public List<GameObject> backgrounds = new List<GameObject>();
        private int _currentBackgroundIndex;
        [Header("CameraOBJ")]
        public GameObject cameraObjects;

        [Header("GameObj")] public GameObject screen;

        [Header("Audio")] public AudioSource audioSource;

        [Header("Input Lock")]
        [Tooltip("When true, camera switching is paused (e.g. while an Incident Report window is open).")]
        public bool inputLocked;

        // X positions of the three camera areas, indexed by _currentBackgroundIndex.
        // Public so other systems (e.g. DemonAnomaly) can tell which room the camera is showing.
        public static readonly float[] CameraPositionsX = { 0f, 17.73f, 36.12f };

        // Runs every frame so the camera can never drift off its area, but only
        // writes the transform when the X actually differs.
        void Update()
        {
            Vector3 position = cameraObjects.transform.position;
            float targetX = CameraPositionsX[_currentBackgroundIndex];

            if (!Mathf.Approximately(position.x, targetX))
            {
                position.x = targetX;
                cameraObjects.transform.position = position;
            }
        }
    
        public void OnNextClick()
        {
            if (inputLocked || DemonAnomaly.AnyRevealed) return;

            screen.SetActive(true);
            audioSource.Play();
            _currentBackgroundIndex = (_currentBackgroundIndex + 1) % CameraPositionsX.Length;
        }

        public void OnPreviousClick()
        {
            if (inputLocked || DemonAnomaly.AnyRevealed) return;

            screen.SetActive(true);
            audioSource.Play();
            _currentBackgroundIndex = (_currentBackgroundIndex + CameraPositionsX.Length - 1) % CameraPositionsX.Length;
        }
    }
}
