using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cutscene
{
    public class SceneTransition : MonoBehaviour
    {
        [Header("Scene Transition Settings")]
        public string nextSceneName; // Name of the scene to load
        public int nextSceneBuildIndex = -1; // Alternative: use build index (-1 to use scene name instead)
        public float delayBeforeTransition; // Optional delay before transitioning
        public bool transitionOnEnable = true; // If true, transitions when GameObject becomes active
        
        [Header("Debug")]
        public bool debugMode; // Shows debug messages

        private bool _hasTriggered; // Prevents multiple transitions

        void OnEnable()
        {
            if (transitionOnEnable && !_hasTriggered)
            {
                TriggerSceneTransition();
            }
        }

        void Start()
        {
            // If not transitioning on enable, you can call TriggerSceneTransition() manually
            if (!transitionOnEnable && !_hasTriggered)
            {
                if (debugMode)
                    Debug.Log("[SceneTransition] Ready to transition. Call TriggerSceneTransition() to proceed.");
            }
        }

        [ContextMenu("Trigger Scene Transition")]
        public void TriggerSceneTransition()
        {
            if (_hasTriggered)
            {
                if (debugMode)
                    Debug.Log("[SceneTransition] Transition already triggered, ignoring.");
                return;
            }

            _hasTriggered = true;

            if (debugMode)
                Debug.Log($"[SceneTransition] Triggering transition in {delayBeforeTransition} seconds...");

            if (delayBeforeTransition > 0)
            {
                Invoke(nameof(LoadNextScene), delayBeforeTransition);
            }
            else
            {
                LoadNextScene();
            }
        }

        private void LoadNextScene()
        {
            // Determine which scene to load
            if (nextSceneBuildIndex >= 0)
            {
                // Load by build index
                if (debugMode)
                    Debug.Log($"[SceneTransition] Loading scene by build index: {nextSceneBuildIndex}");
                
                SceneManager.LoadScene(nextSceneBuildIndex);
            }
            else if (!string.IsNullOrEmpty(nextSceneName))
            {
                // Load by scene name
                if (debugMode)
                    Debug.Log($"[SceneTransition] Loading scene by name: {nextSceneName}");
                
                SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                // Load next scene in build order
                int currentIndex = SceneManager.GetActiveScene().buildIndex;
                int nextIndex = currentIndex + 1;
                
                if (nextIndex < SceneManager.sceneCountInBuildSettings)
                {
                    if (debugMode)
                        Debug.Log($"[SceneTransition] Loading next scene in build order: {nextIndex}");
                    
                    SceneManager.LoadScene(nextIndex);
                }
                else
                {
                    Debug.LogWarning("[SceneTransition] No next scene found! Current scene is the last in build settings.");
                }
            }
        }

        // Optional: Manual trigger method for Timeline signals or other events
        public void OnTimelineSignal()
        {
            TriggerSceneTransition();
        }
    }
}
