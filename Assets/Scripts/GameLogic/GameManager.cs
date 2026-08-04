using System.Collections.Generic;
using GameLogic.Data;
using UnityEngine;

namespace GameLogic
{
    public class GameManager : MonoBehaviour
    {
        [Header("Background Settings")]
        public List<GameObject> backgrounds = new List<GameObject>();
        private int _currentRoomIndex;
        [Header("CameraOBJ")]
        public GameObject cameraObjects;

        [Header("GameObj")] public GameObject screen;

        [Header("Audio")] public AudioSource audioSource;

        [Header("Input Lock")]
        [Tooltip("When true, camera switching is paused (e.g. while an Incident Report window is open).")]
        public bool inputLocked;

        // Which room the camera is currently showing. Rooms (and their camera X positions)
        // come from the RoomAnchors in the scene via RoomRegistry - they used to be a
        // hardcoded float[] here, which meant adding a room required a code change.
        public RoomDefinition CurrentRoom => RoomRegistry.RoomAt(_currentRoomIndex);

        private bool _warnedNoRooms;

        // Runs every frame so the camera can never drift off its area, but only
        // writes the transform when the X actually differs.
        void Update()
        {
            var room = CurrentRoom;
            if (room == null)
            {
                WarnNoRoomsOnce();
                return;
            }

            Vector3 position = cameraObjects.transform.position;

            if (!Mathf.Approximately(position.x, room.cameraX))
            {
                position.x = room.cameraX;
                cameraObjects.transform.position = position;
            }
        }

        public void OnNextClick() => StepRoom(+1);

        public void OnPreviousClick() => StepRoom(-1);

        private void StepRoom(int direction)
        {
            if (inputLocked || DemonAnomaly.AnyRevealed) return;

            int roomCount = RoomRegistry.Count;
            if (roomCount == 0)
            {
                WarnNoRoomsOnce();
                return;
            }

            screen.SetActive(true);
            audioSource.Play();
            _currentRoomIndex = (_currentRoomIndex + direction + roomCount) % roomCount;
        }

        private void WarnNoRoomsOnce()
        {
            if (_warnedNoRooms) return;
            _warnedNoRooms = true;

            Debug.LogError(
                "GameManager: no RoomAnchors registered, so the camera has nowhere to go. " +
                "Run 'Tools/Give Me A Sign/Setup/1. Create Rooms And Anchors' to build them.", this);
        }
    }
}
