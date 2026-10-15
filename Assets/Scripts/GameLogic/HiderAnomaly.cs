using DG.Tweening;
using Report;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameLogic
{
    /// <summary>
    /// The Hider: it clings to a wall and is chased off by holding the mouse pointer on it. It fades
    /// the longer it is pointed at and comes back if the pointer leaves; once gone it scores like any banish.
    /// </summary>
    [RequireComponent(typeof(Anomaly))]
    public class HiderAnomaly : MonoBehaviour
    {
        [Tooltip("Seconds the pointer must stay on it to chase it off.")]
        [Min(0.2f)] [SerializeField] private float dispelSeconds = 2.5f;

        [Tooltip("How much of the bar it wins back per second while the pointer is away (1 = full bar per second, 0 = never recovers).")]
        [Range(0f, 2f)] [SerializeField] private float recoverPerSecond = 0.3f;

        [Tooltip("How faint it gets just before it vanishes.")]
        [Range(0f, 1f)] [SerializeField] private float minAlpha = 0.1f;

        private const float VanishSeconds = 0.25f;

        private Anomaly _anomaly;
        private SpriteRenderer[] _sprites;
        private Color[] _baseColors;
        private Collider2D[] _colliders;
        private float _progress;
        private bool _gone;
        private Tween _vanishTween;

        void Awake()
        {
            _anomaly = GetComponent<Anomaly>();
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
            _colliders = GetComponentsInChildren<Collider2D>(true);

            _baseColors = new Color[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++)
                _baseColors[i] = _sprites[i].color;
        }

        void OnDestroy() => _vanishTween?.Kill();

        void Update()
        {
            if (_gone || Time.timeScale <= 0f) return;

            if (IsPointedAt())
                _progress += Time.deltaTime / dispelSeconds;
            else
                _progress -= recoverPerSecond * Time.deltaTime;

            _progress = Mathf.Clamp01(_progress);
            SetAlpha(Mathf.Lerp(1f, minAlpha, _progress));

            if (_progress >= 1f) Dispel();
        }

        private bool IsPointedAt()
        {
#if ENABLE_INPUT_SYSTEM
            var cam = Camera.main;
            var mouse = Mouse.current;
            if (cam == null || mouse == null) return false;

            // The report window is modal: the pointer is busy there.
            var reports = IncidentReportManager.Instance;
            if (reports != null && reports.IsReportOpen) return false;

            Vector2 screen = mouse.position.ReadValue();
            Vector2 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));

            foreach (var c in _colliders)
            {
                if (c != null && c.enabled && c.gameObject.activeInHierarchy && c.OverlapPoint(world))
                    return true;
            }
#endif
            return false;
        }

        private void SetAlpha(float alpha)
        {
            for (int i = 0; i < _sprites.Length; i++)
            {
                if (_sprites[i] == null) continue;
                var c = _baseColors[i];
                c.a *= alpha;
                _sprites[i].color = c;
            }
        }

        private void Dispel()
        {
            _gone = true;

            // The sprite finishes fading while the anomaly's own despawn delay runs.
            _vanishTween = DOTween.To(() => minAlpha, SetAlpha, 0f, VanishSeconds)
                .SetEase(Ease.OutSine)
                .SetTarget(this);

            _anomaly.MarkReported();
            _anomaly.ResolveByReport();
        }
    }
}
