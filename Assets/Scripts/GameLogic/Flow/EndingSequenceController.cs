using System;
using UnityEngine;
using UnityEngine.Video;
using GameLogic.Story;

namespace GameLogic.Flow
{
    /// <summary>
    /// The Day 7 ending: plays the found-footage video through the shared CinematicPlayer when
    /// one is assigned, otherwise a short placeholder hold. PlayEnding must invoke onComplete
    /// exactly once - GameFlowManager wipes the save after it.
    /// </summary>
    public class EndingSequenceController : MonoBehaviour
    {
        [Tooltip("The found-footage ending video. Empty = placeholder hold (see below).")]
        [SerializeField] private VideoClip endingVideo;

        [Tooltip("Let the player skip the ending after this many seconds. 0 = not skippable.")]
        [Min(0f)] [SerializeField] private float skippableAfterSeconds = 4f;

        [Tooltip("Placeholder hold so the flow is testable before the real ending exists.")]
        [Min(0f)] [SerializeField] private float placeholderHoldSeconds = 2f;

        [SerializeField] private bool showDebugInfo = true;

        private bool _playing;
        private float _preEndingTimeScale = 1f;
        private CinematicPlayer _player;

        void OnDestroy()
        {
            // Same safety net as PauseMenuController - never leave the game frozen.
            if (_playing) Time.timeScale = _preEndingTimeScale;
            // Races CinematicPlayer's own teardown on scene unload; no-op if it already restored.
            _player?.RestoreDuckedAudio();
        }

        /// <summary>Plays the ending, then invokes <paramref name="onComplete"/> exactly once.</summary>
        public void PlayEnding(Action onComplete)
        {
            if (_playing)
            {
                Debug.LogWarning("EndingSequenceController: PlayEnding called while already playing - ignored.", this);
                return;
            }

            _playing = true;

            if (endingVideo != null)
            {
                PlayVideoEnding(onComplete);
                return;
            }

            // Fallback: placeholder hold when no footage is authored yet.
            if (showDebugInfo)
                Debug.Log("EndingSequenceController: no ending video assigned - playing placeholder hold.", this);

            StartCoroutine(PlaceholderRoutine(onComplete));
        }

        private void PlayVideoEnding(Action onComplete)
        {
            _player = GetComponent<CinematicPlayer>();
            if (_player == null) _player = gameObject.AddComponent<CinematicPlayer>();

            if (showDebugInfo)
                Debug.Log($"EndingSequenceController: playing ending video '{endingVideo.name}'.", this);

            var data = ScriptableObject.CreateInstance<CinematicData>();
            data.cinematicId = "ending_found_footage";
            data.clip = endingVideo;
            data.skippableAfterSeconds = skippableAfterSeconds;
            data.badgeLabel = "";

            // Pause the game so the night clock / timers can't run under the ending.
            _preEndingTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            _player.Play(data, () =>
            {
                Time.timeScale = _preEndingTimeScale;
                _playing = false;
                onComplete?.Invoke();
            });
        }

        private System.Collections.IEnumerator PlaceholderRoutine(Action onComplete)
        {
            if (placeholderHoldSeconds > 0f)
                yield return new WaitForSecondsRealtime(placeholderHoldSeconds);

            _playing = false;
            onComplete?.Invoke();
        }
    }
}
