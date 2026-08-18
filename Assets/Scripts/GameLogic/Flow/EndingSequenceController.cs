using System;
using UnityEngine;

namespace GameLogic.Flow
{
    /// <summary>
    /// Extension point for the Day 7 ending. Intentionally an empty stub - whether the ending is
    /// a video, an in-engine scene or a slideshow is not decided yet.
    ///
    /// The contract is the only thing that matters right now: GameFlowManager calls PlayEnding
    /// and waits for onComplete before wiping the save. Any real implementation just has to
    /// honour that callback exactly once.
    /// </summary>
    public class EndingSequenceController : MonoBehaviour
    {
        [Tooltip("Placeholder hold so the flow is testable before the real ending exists.")]
        [Min(0f)] [SerializeField] private float placeholderHoldSeconds = 2f;

        [SerializeField] private bool showDebugInfo = true;

        private bool _playing;

        /// <summary>
        /// Plays the ending, then invokes <paramref name="onComplete"/> exactly once.
        /// Re-entrant calls are ignored so a double-trigger cannot wipe the save twice.
        /// </summary>
        public void PlayEnding(Action onComplete)
        {
            if (_playing)
            {
                Debug.LogWarning("EndingSequenceController: PlayEnding called while already playing - ignored.", this);
                return;
            }

            _playing = true;

            if (showDebugInfo)
                Debug.Log("EndingSequenceController: playing placeholder ending (no real sequence authored yet).", this);

            StartCoroutine(PlaceholderRoutine(onComplete));
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
