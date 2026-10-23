using UnityEngine;

namespace GameLogic.Data
{
    /// <summary>
    /// The bridge between a <see cref="RoomDefinition"/> asset and the actual scene: one of
    /// these per room, placed in the scene, holding the Transforms anomalies can spawn on.
    /// </summary>
    public class RoomAnchor : MonoBehaviour
    {
        [Tooltip("Which room this anchor represents. Required - the anchor is ignored without it.")]
        [SerializeField] private RoomDefinition room;

        [Tooltip("Positions an anomaly may spawn at inside this room. Leave empty to spawn on this GameObject itself.")]
        [SerializeField] private Transform[] spawnPoints;

        public RoomDefinition Room => room;
        public int SpawnPointCount => spawnPoints != null ? spawnPoints.Length : 0;

        public Transform GetSpawnPoint(System.Random rng, AnomalyDefinition definition = null, System.Func<Transform, bool> isFree = null)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return transform;

            // Skip empty slots and points reserved for other anomaly kinds (SpawnPointFilter).
            int start = rng != null ? rng.Next(spawnPoints.Length) : 0;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var candidate = spawnPoints[(start + i) % spawnPoints.Length];
                if (candidate == null) continue;
                var filter = candidate.GetComponent<SpawnPointFilter>();
                if (filter != null && !filter.Allows(definition)) continue;
                if (isFree != null && !isFree(candidate)) continue;
                return candidate;
            }

            // Points exist but none is usable (wrong kind or already occupied): spawn nothing.
            // Never fall back to the anchor position - only designated points are allowed.
            return null;
        }

        void OnEnable()
        {
            // A camera that is not part of tonight's sector never joins the camera cycle.
            if (room != null && !room.IsActiveOn(GameLogic.Flow.GameFlowManager.CurrentNightIndex)) return;
            RoomRegistry.Register(this);
        }
        void OnDisable() => RoomRegistry.Unregister(this);

        void OnDrawGizmos()
        {
            if (room == null) return;

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.6f);

            if (spawnPoints != null)
            {
                foreach (var p in spawnPoints)
                {
                    if (p == null) continue;
                    Gizmos.DrawWireSphere(p.position, 0.35f);
                    Gizmos.DrawLine(transform.position, p.position);
                }
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 1f,
                $"[{room.cameraOrder}] {room.Label}  (camX {room.cameraX:0.##})");
#endif
        }
    }
}
