using GameLogic.Data;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// A "replace the background" anomaly: while alive, the assigned room's Background sprite is swapped for this
    /// object's own sprite (its SpriteRenderer is kept disabled; it only carries the image). Restored when it goes away.
    /// </summary>
    public class ReplaceRoomBackground : MonoBehaviour
    {
        [SerializeField] private string backgroundName = "Background";

        private SpriteRenderer _target;
        private Sprite _original;

        void Start() => Apply();
        void OnEnable() { if (_target == null && Time.frameCount > 1) Apply(); }
        void OnDisable() => Restore();
        void OnDestroy() => Restore();

        private void Apply()
        {
            var anomaly = GetComponent<Anomaly>();
            var own = GetComponent<SpriteRenderer>();
            if (anomaly == null || anomaly.AssignedRoom == null || own == null || own.sprite == null) return;

            foreach (var anchor in FindObjectsByType<RoomAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (anchor.Room != anomaly.AssignedRoom) continue;
                var bg = anchor.transform.Find(backgroundName);
                var sr = bg != null ? bg.GetComponent<SpriteRenderer>() : null;
                if (sr == null) return;
                _target = sr;
                _original = sr.sprite;
                sr.sprite = own.sprite;
                return;
            }
        }

        private void Restore()
        {
            if (_target != null) _target.sprite = _original;
            _target = null;
            _original = null;
        }
    }
}
