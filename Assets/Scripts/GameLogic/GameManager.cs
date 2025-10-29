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

        // Update is called once per frame
        void Update()
        {
            Debug.Log(backgrounds.Count);
            Debug.Log(_currentBackgroundIndex);
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
