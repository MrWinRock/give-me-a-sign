using UnityEngine;

namespace GameLogic.Data
{
    /// <summary>
    /// Put on a spawn point Transform listed in a RoomAnchor to reserve it for specific anomaly kinds.
    /// A point without this component accepts any anomaly.
    /// </summary>
    public class SpawnPointFilter : MonoBehaviour
    {
        [Tooltip("Only these anomalies may spawn here.")]
        [SerializeField] private AnomalyDefinition[] allowed;

        public bool Allows(AnomalyDefinition definition)
        {
            if (allowed == null || allowed.Length == 0) return true;
            return definition != null && System.Array.IndexOf(allowed, definition) >= 0;
        }
    }
}
