using UnityEngine;

namespace GameLogic.Data
{
    /// <summary>
    /// The bridge between a <see cref="RoomDefinition"/> asset and the actual scene: one of
    /// these per room, placed in the scene, holding the Transforms anomalies can spawn on.
    ///
    /// Why this exists at all: RoomDefinition is a ScriptableObject, and a ScriptableObject
    /// cannot reference a scene Transform. So the asset owns the room's identity and camera
    /// position, and this component owns its scene geometry.
    /// </summary>
    public class RoomAnchor : MonoBehaviour
    {
        [Tooltip("Which room this anchor represents. Required - the anchor is ignored without it.")]
        [SerializeField] private RoomDefinition room;

        [Tooltip("Positions an anomaly may spawn at inside this room. Leave empty to spawn on this GameObject itself.")]
        [SerializeField] private Transform[] spawnPoints;

        public RoomDefinition Room => room;
        public int SpawnPointCount => spawnPoints != null ? spawnPoints.Length : 0;

        /// <summary>
        /// Picks a spawn point using the caller's own RNG. Takes System.Random rather than
        /// using UnityEngine.Random on purpose: night plans must be reproducible from a seed,
        /// and UnityEngine.Random is global state that any other system can advance.
        /// </summary>
        public Transform GetSpawnPoint(System.Random rng)
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return transform;

            // Skip empty slots left behind by editing the list in the Inspector.
            int start = rng != null ? rng.Next(spawnPoints.Length) : 0;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var candidate = spawnPoints[(start + i) % spawnPoints.Length];
                if (candidate != null) return candidate;
            }

            return transform;
        }

        void OnEnable() => RoomRegistry.Register(this);
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
