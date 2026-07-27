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

        // Update is called once per frame
        void Update()
        {
            if (_currentBackgroundIndex == 0)
            {
                Vector3 position = cameraObjects.transform.position;
                position.x = 0;
                cameraObjects.transform.position = position;
            }
            else if (_currentBackgroundIndex == 1)
            {
                Vector3 position = cameraObjects.transform.position;
                position.x = 17.73f;
                cameraObjects.transform.position = position;
            }
            else if (_currentBackgroundIndex == 2)
            {
                Vector3 position = cameraObjects.transform.position;
                position.x = 36.12f;
                cameraObjects.transform.position = position;
            }
        }
    
        public void OnNextClick()
        {
            if (inputLocked) return;

            screen.SetActive(true);
            audioSource.Play();
            if (_currentBackgroundIndex == 2)
            {
                _currentBackgroundIndex = 0;
                return;
            }
            _currentBackgroundIndex = _currentBackgroundIndex + 1;
        }
    
        public void OnPreviousClick()
        {
            if (inputLocked) return;

            screen.SetActive(true);
            audioSource.Play();
            if (_currentBackgroundIndex == 0)
            {
                _currentBackgroundIndex = 2;
                return;
            }
            _currentBackgroundIndex = _currentBackgroundIndex - 1;
        }
    }
}
