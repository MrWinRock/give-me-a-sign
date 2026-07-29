using Report;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic
{
    /// <summary>
    /// The "Demon" - a jumpscare that IS an anomaly. It hides in one room; the moment the
    /// player pans the camera into that room it takes over the whole screen (a fullscreen
    /// overlay that lives inside the room, so panning away still works) and screams.
    ///
    /// The player banishes it the same way as any other anomaly: open the Incident Report
    /// (Spacebar), pick the room, and SPEAK its type ("Demon") into the mic - while the
    /// GlitchDirector makes the form misbehave, firing several glitch types at once.
    ///
    /// Flow:
    ///   spawn (hidden, not reportable) -> camera enters room -> REVEAL: overlay + scream,
    ///   Anomaly component enabled so the report can attach to it, glitch intensity raised
    ///   -> report submitted correctly = banished (+score, like every anomaly)
    ///   -> wrong report = window closes, demon stays, player may retry
    ///   -> optional time limit runs out = player loses (same path as anomaly timeout).
    /// </summary>
    [RequireComponent(typeof(Anomaly))]
    public class DemonAnomaly : MonoBehaviour
    {
        [Header("Reveal")]
        [Tooltip("Fullscreen scare visual (child object with a SpriteRenderer). Kept inactive until the camera enters the room; auto-scaled to cover the whole camera view.")]
        [SerializeField] private GameObject overlayRoot;
        [Tooltip("How close (world X) the camera must be to this demon's room before the jumpscare triggers.")]
        [SerializeField] private float revealDistance = 5f;

        [Header("Pressure")]
        [Tooltip("Seconds the player has to file a correct report after the reveal before losing. 0 = no time limit.")]
        [SerializeField] private float timeLimitSeconds = 30f;

        [Header("Audio")]
        [Tooltip("Played once at the moment of the reveal.")]
        [SerializeField] private AudioSource jumpscareAudio;

        [Header("Form Glitches")]
        [Tooltip("GlitchDirector intensity while the demon is on screen (1 = normal, 2 = twice as glitchy). Restored to 1 when it is banished.")]
        [SerializeField] private float glitchIntensityWhileActive = 2f;
        [Tooltip("How many DIFFERENT glitch types fire together shortly after the report form opens while the demon is active. 0 = none.")]
        [SerializeField] private int glitchBurstCount = 2;
        [Tooltip("Delay between the form opening and the glitch burst.")]
        [SerializeField] private float glitchBurstDelay = 0.8f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        // How many demons are currently revealed and unresolved, across the scene.
        // GlitchStateSource reads this so the glitch systems treat a demon as an ACTIVE threat.
        private static int _revealedCount;
        public static bool AnyRevealed => _revealedCount > 0;

        private Anomaly _anomaly;
        private GlitchDirector _glitchDirector;
        private Camera _camera;
        private float _roomCameraX;
        private float _revealedAt;
        private bool _revealed;
        private bool _resolved;
        private bool _reportWasOpen;
        private float _burstAt = -1f; // scheduled time for the pending glitch burst, -1 = none

        void Awake()
        {
            _anomaly = GetComponent<Anomaly>();

            // Keep the demon OUT of Anomaly.ActiveAnomalies until the jumpscare reveals it,
            // so the report window can never attach to a demon the player hasn't met yet.
            _anomaly.enabled = false;
            _anomaly.OnAnomalyDisappeared += OnResolved;

            if (overlayRoot != null)
                overlayRoot.SetActive(false);
        }

        void Start()
        {
            _camera = Camera.main;
            _glitchDirector = FindFirstObjectByType<GlitchDirector>();

            // This demon's room = the camera area closest to where it was placed.
            _roomCameraX = GameManager.CameraPositionsX[0];
            foreach (float x in GameManager.CameraPositionsX)
            {
                if (Mathf.Abs(transform.position.x - x) < Mathf.Abs(transform.position.x - _roomCameraX))
                    _roomCameraX = x;
            }
        }

        void OnDestroy()
        {
            if (_anomaly != null)
                _anomaly.OnAnomalyDisappeared -= OnResolved;

            // Scene teardown safety: never leave the global state stuck on.
            if (_revealed && !_resolved)
                EndThreat();
        }

        void Update()
        {
            if (_resolved) return;

            if (!_revealed)
            {
                TryReveal();
                return;
            }

            var reportManager = IncidentReportManager.Instance;
            bool reportOpen = reportManager != null && reportManager.IsReportOpen;

            // Form just opened while the demon is on screen -> schedule the multi-glitch burst.
            if (reportOpen && !_reportWasOpen && glitchBurstCount > 0 && _glitchDirector != null)
                _burstAt = Time.unscaledTime + Mathf.Max(0f, glitchBurstDelay);

            // Form just closed without banishing us -> the report failed or was cancelled.
            // Clear the reported flag so the player can open a new report and try again.
            if (!reportOpen && _reportWasOpen && _anomaly.IsReported)
            {
                _anomaly.ClearReportedFlag();
                _burstAt = -1f;

                if (showDebugInfo)
                    Debug.Log("DemonAnomaly: report failed/cancelled - the demon is still here. Try again!", this);
            }

            _reportWasOpen = reportOpen;

            if (_burstAt >= 0f && Time.unscaledTime >= _burstAt)
            {
                _burstAt = -1f;
                if (reportOpen)
                    _glitchDirector.TriggerBurst(glitchBurstCount);
            }

            if (timeLimitSeconds > 0f && Time.time - _revealedAt >= timeLimitSeconds)
                OnTimeLimitReached();
        }

        private void TryReveal()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null) return;
            }

            if (Mathf.Abs(_camera.transform.position.x - _roomCameraX) > revealDistance)
                return;

            _revealed = true;
            _revealedAt = Time.time;
            _revealedCount++;

            // Now the demon officially exists: the report window can attach to it.
            _anomaly.enabled = true;

            ShowOverlay();

            if (jumpscareAudio != null)
                jumpscareAudio.Play();

            IncidentReportManager.Instance?.SetAlert(true);
            _glitchDirector?.SetIntensity(glitchIntensityWhileActive);

            if (showDebugInfo)
                Debug.Log($"DemonAnomaly: REVEALED in room x={_roomCameraX}. Speak '{_anomaly.correctAnomalyType}' to banish it!", this);
        }

        /// <summary>Activates the overlay and scales it so it covers the camera view of this room.</summary>
        private void ShowOverlay()
        {
            if (overlayRoot == null)
            {
                Debug.LogWarning("DemonAnomaly: no overlayRoot assigned - the jumpscare has no visual!", this);
                return;
            }

            overlayRoot.SetActive(true);

            var sr = overlayRoot.GetComponentInChildren<SpriteRenderer>();
            if (sr == null || sr.sprite == null || _camera == null || !_camera.orthographic)
                return;

            float camHeight = _camera.orthographicSize * 2f;
            float camWidth = camHeight * _camera.aspect;

            // Park the overlay dead-center of this room's camera view...
            overlayRoot.transform.position = new Vector3(_roomCameraX, _camera.transform.position.y, 0f);

            // ...and scale it up until the sprite covers the whole view.
            Vector2 spriteSize = sr.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            float cover = Mathf.Max(camWidth / spriteSize.x, camHeight / spriteSize.y);
            overlayRoot.transform.localScale = new Vector3(cover, cover, 1f);
        }

        private void OnResolved(Anomaly _)
        {
            if (_resolved) return;
            _resolved = true;

            EndThreat();

            if (overlayRoot != null)
                overlayRoot.SetActive(false);

            if (jumpscareAudio != null && jumpscareAudio.isPlaying)
                jumpscareAudio.Stop();

            if (showDebugInfo)
                Debug.Log("DemonAnomaly: banished by a correct report.", this);
        }

        private void OnTimeLimitReached()
        {
            _resolved = true;
            EndThreat();

            if (showDebugInfo)
                Debug.Log("DemonAnomaly: time limit reached - player loses.", this);

            // Same lose path as a regular anomaly timeout, so the Result scene shows the
            // "consumed by the darkness" defeat.
            PlayerPrefs.SetInt("FinalScore", 0);
            PlayerPrefs.SetInt("GameWon", 0);
            PlayerPrefs.SetInt("WinThreshold", 1);
            PlayerPrefs.SetInt("AnomalyTimeout", 1);
            PlayerPrefs.Save();

            SceneManager.LoadScene("Result");
        }

        /// <summary>Reverts everything the reveal turned on (alert badge, glitch intensity, global flag).</summary>
        private void EndThreat()
        {
            if (!_revealed) return;

            _revealedCount = Mathf.Max(0, _revealedCount - 1);
            IncidentReportManager.Instance?.SetAlert(false);
            _glitchDirector?.SetIntensity(1f);
        }
    }
}
