using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// Snaps the head bone between two Z rotations with no easing, so the motion
    /// looks wrong for a human. Runs in LateUpdate so it wins over any Animator.
    /// Put it on the model root (DemonModel).
    /// </summary>
    public class DemonHeadTwitch : MonoBehaviour
    {
        [SerializeField] private string headBoneName = "mixamorig:Head";
        [Tooltip("Resting head Rotation Z (degrees).")]
        [SerializeField] private float angleA = 1.078f;
        [Tooltip("Twisted head Rotation Z (degrees).")]
        [SerializeField] private float angleB = 26.389f;
        [Tooltip("Seconds it stays in the resting pose (random between the two).")]
        [SerializeField] private Vector2 holdA = new Vector2(0.35f, 0.9f);
        [Tooltip("Seconds it stays in the twisted pose (random between the two).")]
        [SerializeField] private Vector2 holdB = new Vector2(0.15f, 0.5f);

        private Transform _head;
        private Vector3 _baseEuler;
        private bool _atB;
        private float _nextSwitch;

        private void OnEnable()
        {
            if (_head == null)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name == headBoneName) { _head = t; break; }
                if (_head == null) { Debug.LogWarning("DemonHeadTwitch: bone not found: " + headBoneName, this); enabled = false; return; }
                _baseEuler = _head.localEulerAngles;
            }
            _atB = false;
            _nextSwitch = Time.time + Random.Range(holdA.x, holdA.y);
        }

        private void LateUpdate()
        {
            if (_head == null) return;
            if (Time.time >= _nextSwitch)
            {
                _atB = !_atB;
                var h = _atB ? holdB : holdA;
                _nextSwitch = Time.time + Random.Range(h.x, h.y);
            }
            _head.localEulerAngles = new Vector3(_baseEuler.x, _baseEuler.y, _atB ? angleB : angleA);
        }
    }
}
