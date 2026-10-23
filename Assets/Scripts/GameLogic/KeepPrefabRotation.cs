using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// Marker: when the scheduler spawns this prefab, the spawn point's rotation is applied ON TOP of
    /// the prefab's own root rotation (point * prefab) instead of replacing it.
    /// </summary>
    public class KeepPrefabRotation : MonoBehaviour { }
}
