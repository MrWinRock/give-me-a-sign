using Report;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

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

        [Header("Visual - pick ONE")]
        [Tooltip("If assigned, plays this video fullscreen INSTEAD of the static image on Overlay Root. Built at runtime as a textured quad - no manual VideoPlayer setup needed.")]
        [SerializeField] private VideoClip jumpscareVideo;
        [SerializeField] private bool loopVideo = true;
        [Tooltip("Material used for the video quad (must use an Unlit shader with a _BaseMap texture, e.g. Universal Render Pipeline/Unlit). Leave empty to auto-create one at runtime - fine for testing, but for BUILDS assign a real Material asset here so its shader isn't stripped.")]
        [SerializeField] private Material videoOverlayMaterial;

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

        private VideoPlayer _videoPlayer;
        private GameObject _videoOverlayObject;

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

            if (_videoOverlayObject != null)
                Destroy(_videoOverlayObject);

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

        /// <summary>Activates the overlay (video takes priority over the static image if assigned) and scales it to cover the camera view of this room.</summary>
        private void ShowOverlay()
        {
            if (overlayRoot == null)
            {
                Debug.LogWarning("DemonAnomaly: no overlayRoot assigned - the jumpscare has no visual!", this);
                return;
            }

            overlayRoot.SetActive(true);

            if (jumpscareVideo != null)
                ShowVideoOverlay();
            else
                ShowImageOverlay();
        }

        private void ShowImageOverlay()
        {
            var sr = overlayRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null) return;

            sr.gameObject.SetActive(true);
            if (_videoOverlayObject != null)
                _videoOverlayObject.SetActive(false);

            if (sr.sprite == null) return;
            CoverCameraView(sr.transform, sr.sprite.bounds.size);
        }

        private void ShowVideoOverlay()
        {
            if (_videoOverlayObject == null)
                BuildVideoOverlay();

            var sr = overlayRoot.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null)
                sr.gameObject.SetActive(false);

            _videoOverlayObject.SetActive(true);
            CoverCameraView(_videoOverlayObject.transform, Vector2.one); // default Quad is 1x1 in local units

            _videoPlayer.clip = jumpscareVideo;
            _videoPlayer.isLooping = loopVideo;

            // The video's embedded audio bypasses AudioSource volume, so apply the player's
            // SFX/master sliders to its direct output by hand.
            if (Audio.AudioManager.Instance != null)
                _videoPlayer.SetDirectAudioVolume(0, Audio.AudioManager.Instance.GetEffectiveVolume(Audio.AudioChannel.Sfx));

            _videoPlayer.Play();
        }

        /// <summary>Builds a textured quad + VideoPlayer once, parented under overlayRoot. No manual scene setup required.</summary>
        private void BuildVideoOverlay()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "JumpscareVideoOverlay";

            // CreatePrimitive adds a 3D MeshCollider by default; this project's clicks use
            // Physics2D raycasts, so a stray 3D collider is dead weight - remove it.
            var collider3D = quad.GetComponent<Collider>();
            if (collider3D != null)
                Destroy(collider3D);

            quad.transform.SetParent(overlayRoot.transform, worldPositionStays: false);
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.identity;

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material = videoOverlayMaterial != null
                ? new Material(videoOverlayMaterial)
                : new Material(Shader.Find("Universal Render Pipeline/Unlit"));

            _videoPlayer = quad.AddComponent<VideoPlayer>();
            _videoPlayer.playOnAwake = false;
            _videoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            _videoPlayer.targetMaterialRenderer = renderer;
            _videoPlayer.targetMaterialProperty = "_BaseMap";
            _videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

            _videoOverlayObject = quad;
        }

        /// <summary>Scales+positions an overlay transform (in world units of the given base size) to fill this room's camera view.</summary>
        private void CoverCameraView(Transform overlay, Vector2 baseSize)
        {
            if (_camera == null || !_camera.orthographic || baseSize.x <= 0f || baseSize.y <= 0f) return;

            float camHeight = _camera.orthographicSize * 2f;
            float camWidth = camHeight * _camera.aspect;

            overlay.position = new Vector3(_roomCameraX, _camera.transform.position.y, overlay.position.z);

            float cover = Mathf.Max(camWidth / baseSize.x, camHeight / baseSize.y);
            overlay.localScale = new Vector3(cover, cover, 1f);
        }

        private void OnResolved(Anomaly _)
        {
            if (_resolved) return;
            _resolved = true;

            EndThreat();

            if (overlayRoot != null)
                overlayRoot.SetActive(false);
            if (_videoPlayer != null)
                _videoPlayer.Stop();

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
