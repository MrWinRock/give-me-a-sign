using GameLogic.Data;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// On spawn, moves this object under its assigned room's camera object (the one with the RoomAnchor)
    /// and lays it exactly over that room's "Background" sprite, so a full-frame overlay always lines up.
    /// </summary>
    public class AttachToRoomOverlay : MonoBehaviour
    {
        [SerializeField] private string backgroundName = "Background";

        void Start()
        {
            var anomaly = GetComponent<Anomaly>();
            if (anomaly == null || anomaly.AssignedRoom == null) return;

            foreach (var anchor in FindObjectsByType<RoomAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (anchor.Room != anomaly.AssignedRoom) continue;
                transform.SetParent(anchor.transform, false);
                var bg = anchor.transform.Find(backgroundName);
                if (bg != null)
                {
                    transform.localPosition = bg.localPosition;
                    transform.localRotation = bg.localRotation;
                    transform.localScale = bg.localScale;
                }
                else transform.localPosition = Vector3.zero;
                return;
            }
        }
    }
}
