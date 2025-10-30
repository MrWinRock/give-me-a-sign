using UnityEngine;

namespace Score
{
    /// <summary>
    /// Manages GameObject activation in SampleScene based on game results
    /// This script should be placed in SampleScene to handle anomaly defeat scenarios
    /// </summary>
    public class SampleSceneManager : MonoBehaviour
    {
        [Header("Anomaly Defeat Objects")]
        [SerializeField] private GameObject[] anomalyDefeatObjects; // Objects to activate when player loses to anomaly
        [SerializeField] private bool deactivateOnNormalResult = true; // Deactivate these objects for normal win/lose
        
        [Header("Normal Game Result Objects")]
        [SerializeField] private GameObject[] normalResultObjects; // Objects to activate for normal game results
        
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;
        
        void Start()
        {
            HandleGameResult();
        }
        
        private void HandleGameResult()
        {
            // Check if game ended due to anomaly timeout
            bool anomalyTimeout = PlayerPrefs.GetInt("AnomalyTimeout", 0) == 1;
            
            if (anomalyTimeout)
            {
                ActivateAnomalyDefeatObjects();
                
                if (deactivateOnNormalResult)
                {
                    DeactivateNormalResultObjects();
                }
                
                if (showDebugInfo)
                {
                    Debug.Log("SampleSceneManager: Activated anomaly defeat objects");
                }
            }
            else
            {
                // Normal game result
                ActivateNormalResultObjects();
                DeactivateAnomalyDefeatObjects();
                
                if (showDebugInfo)
                {
                    Debug.Log("SampleSceneManager: Activated normal result objects");
                }
            }
        }
        
        private void ActivateAnomalyDefeatObjects()
        {
            foreach (GameObject obj in anomalyDefeatObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"Activated anomaly defeat object: {obj.name}");
                    }
                }
            }
        }
        
        private void DeactivateAnomalyDefeatObjects()
        {
            foreach (GameObject obj in anomalyDefeatObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
        
        private void ActivateNormalResultObjects()
        {
            foreach (GameObject obj in normalResultObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                    
                    if (showDebugInfo)
                    {
                        Debug.Log($"Activated normal result object: {obj.name}");
                    }
                }
            }
        }
        
        private void DeactivateNormalResultObjects()
        {
            foreach (GameObject obj in normalResultObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }
        
        /// <summary>
        /// Manually activate anomaly defeat objects (for testing)
        /// </summary>
        [ContextMenu("Test Anomaly Defeat")]
        public void TestAnomalyDefeat()
        {
            PlayerPrefs.SetInt("AnomalyTimeout", 1);
            HandleGameResult();
        }
        
        /// <summary>
        /// Manually activate normal result objects (for testing)
        /// </summary>
        [ContextMenu("Test Normal Result")]
        public void TestNormalResult()
        {
            PlayerPrefs.DeleteKey("AnomalyTimeout");
            HandleGameResult();
        }
        
        /// <summary>
        /// Clear all result flags (for testing)
        /// </summary>
        [ContextMenu("Clear Result Flags")]
        public void ClearResultFlags()
        {
            PlayerPrefs.DeleteKey("AnomalyTimeout");
            PlayerPrefs.DeleteKey("FinalScore");
            PlayerPrefs.DeleteKey("GameWon");
            PlayerPrefs.DeleteKey("WinThreshold");
            
            if (showDebugInfo)
            {
                Debug.Log("All result flags cleared");
            }
        }
    }
}
