using GameLogic.Flow;
using UnityEngine;

namespace Score
{
    /// <summary>
    /// Switches sets of GameObjects on or off in the Result scene depending on whether the
    /// player was caught or the night simply ended.
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
            // Reads the one recorded NightResult. This used to poll an "AnomalyTimeout"
            // PlayerPrefs flag, which no longer exists - GameFlowManager owns the outcome now.
            var result = GameFlowManager.LastResult;
            Apply(result != null && result.KilledByThreat);
        }

        private void Apply(bool killedByThreat)
        {
            if (killedByThreat)
            {
                SetActiveAll(anomalyDefeatObjects, true);

                if (deactivateOnNormalResult)
                    SetActiveAll(normalResultObjects, false);
            }
            else
            {
                SetActiveAll(normalResultObjects, true);
                SetActiveAll(anomalyDefeatObjects, false);
            }

            if (showDebugInfo)
                Debug.Log($"SampleSceneManager: showing {(killedByThreat ? "anomaly defeat" : "normal result")} objects.", this);
        }

        private void SetActiveAll(GameObject[] objects, bool active)
        {
            if (objects == null) return;

            foreach (var obj in objects)
            {
                if (obj == null) continue;

                obj.SetActive(active);

                if (active && showDebugInfo)
                    Debug.Log($"Activated result object: {obj.name}");
            }
        }

        [ContextMenu("Test Anomaly Defeat")]
        public void TestAnomalyDefeat() => Apply(true);

        [ContextMenu("Test Normal Result")]
        public void TestNormalResult() => Apply(false);
    }
}
