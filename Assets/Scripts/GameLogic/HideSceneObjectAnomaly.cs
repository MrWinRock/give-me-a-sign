using GameLogic.Data;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// An anomaly that is something MISSING: while this object is alive it switches off a named object that
    /// is normally part of the camera image (e.g. the fence), and switches it back on when it goes away.
    /// </summary>
    public class HideSceneObjectAnomaly : MonoBehaviour
    {
        [Tooltip("Name of the scene object to hide. If several share the name, the one under the assigned room's camera is used.")]
        [SerializeField] private string targetName = "Fence";

        private GameObject _target;

        void OnEnable()
        {
            _target = FindTarget();
            if (_target != null) _target.SetActive(false);
        }

        void OnDisable() => Restore();
        void OnDestroy() => Restore();

        private void Restore()
        {
            if (_target != null) _target.SetActive(true);
            _target = null;
        }

        private GameObject FindTarget()
        {
            var anomaly = GetComponent<Anomaly>();
            var roomId = anomaly != null && anomaly.AssignedRoom != null ? anomaly.AssignedRoom.roomId : null;

            GameObject fallback = null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != targetName) continue;
                fallback = t.gameObject;
                if (roomId == null) return fallback;
                var anchor = t.GetComponentInParent<RoomAnchor>(true);
                if (anchor != null && anchor.Room != null && anchor.Room.roomId == roomId) return t.gameObject;
            }
            return fallback;
        }
    }
}
