using GameLogic.Data;
using GameLogic.Flow;
using Report;
using UnityEngine;
using UnityEngine.Video;

namespace GameLogic
{
    /// <summary>
    /// The "Demon" - a jumpscare that IS an anomaly. It hides in one room; the moment the
    /// player pans the camera into that room it takes over the whole screen (a fullscreen
    /// overlay that lives inside the room, so panning away still works) and screams.
    /// </summary>
    [RequireComponent(typeof(Anomaly))]
    public class DemonAnomaly : MonoBehaviour
    {
        [Header("Room")]
        [Tooltip("Which room this demon hides in. Leave empty to use the room assigned at spawn time; if neither is set, it falls back to its own X position.")]
        [SerializeField] private RoomDefinition room;

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
        [Tooltip("Fallback seconds to file a correct report before losing. 0 = no time limit. Overridden per night by the DifficultyProfile unless 'Use Per Night Timeout' is off.")]
        [SerializeField] private float timeLimitSeconds = 30f;

        [Tooltip("Take the time limit from this night's DifficultyProfile instead of the value above.")]
        [SerializeField] private bool usePerNightTimeout = true;

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
        private RoomDefinition _room;
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

            ApplyPerNightTimeout();

            // The demon's room is data now, not something inferred from its position by
            // scanning a hardcoded list of camera X values.
            _room = ResolveRoom();

            if (_room != null)
            {
                _roomCameraX = _room.cameraX;
            }
            else
            {
                // No room assigned anywhere: reveal where it stands. Keeps a half-configured
                // prefab playable instead of making it silently never trigger.
                _roomCameraX = transform.position.x;
                Debug.LogWarning(
                    $"DemonAnomaly '{name}' has no RoomDefinition (neither the Room field nor a room " +
                    $"assigned at spawn). Falling back to its own X position ({_roomCameraX:0.##}).", this);
            }
        }

        // The window shrinks as the campaign goes on, so the number lives with the rest of the
        // per-night tuning rather than being baked into the prefab.
        private void ApplyPerNightTimeout()
        {
            if (!usePerNightTimeout) return;

            var library = GameLogic.Night.NightContentLibrary.Load();
            if (library == null || library.difficulty == null) return;

            int night = GameLogic.Night.NightPlanProvider.HasPlan
                ? GameLogic.Night.NightPlanProvider.Current.nightIndex
                : Flow.GameFlowManager.CurrentDay;

            timeLimitSeconds = library.difficulty.DemonTimeoutFor(night);

            if (showDebugInfo)
                Debug.Log($"DemonAnomaly: night {night} timeout = {timeLimitSeconds:0}s.", this);
        }

        private RoomDefinition ResolveRoom()
        {
            if (_anomaly != null && _anomaly.AssignedRoom != null) return _anomaly.AssignedRoom;
            return room;
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
            {
                string expected = _anomaly.Definition != null
                    ? string.Join("' or '", _anomaly.Definition.correctKeywords)
                    : "(no definition)";
                Debug.Log($"DemonAnomaly: REVEALED in room '{(_room != null ? _room.Label : _roomCameraX.ToString("0.##"))}'. Speak '{expected}' to banish it!", this);
            }
        }

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

            // Just report what happened. GameFlowManager decides what it means, closes the
            // report window (which stops any live mic recording first - loading the scene out
            // from under one used to hang the game) and moves to the Result scene.
            GameFlowManager.Instance?.EndNight(
                NightOutcome.KilledByDemon,
                _anomaly.Definition != null ? _anomaly.Definition.anomalyId : null,
                _room != null ? _room.roomId : null);
        }

        private void EndThreat()
        {
            if (!_revealed) return;

            _revealedCount = Mathf.Max(0, _revealedCount - 1);
            IncidentReportManager.Instance?.SetAlert(false);
            _glitchDirector?.SetIntensity(1f);
        }
    }
}
