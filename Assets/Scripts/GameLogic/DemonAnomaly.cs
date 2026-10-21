using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
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

        [Header("Visual - 3D model (optional)")]
        [Tooltip("A posed 3D model (child of Overlay Root). When set it is shown INSTEAD of the sprite/video: it is moved to the middle of the camera view and lunges in like the flat version. Size and pose it in the prefab so the face fills the screen.")]
        [SerializeField] private Transform model3D;
        [Tooltip("Slam-in: the model starts at this fraction of its placed size (1/3 = world scale 1 when placed at 3).")]
        [SerializeField] private float slamStartFraction = 0.3333f;
        [Tooltip("Seconds the slam-in takes to reach full size.")]
        [SerializeField] private float slamSeconds = 0.1f;
        [Tooltip("World Position Y of the model when it appears.")]
        [SerializeField] private float modelWorldY = -79.6f;
        [Tooltip("World Position Z of the model when it appears.")]
        [SerializeField] private float modelWorldZ = -8.69f;

        [System.Serializable]
        public struct ModelStage
        {
            [Tooltip("Where the head is, as a fraction of the camera height from the screen centre (x right, y up).")]
            public Vector2 viewOffset;
            [Tooltip("Size multiplier of the model (1 = the size set in the prefab).")]
            public float scale;
            [Tooltip("Tilt of the whole head in degrees (Z).")]
            public float rollDegrees;
            [Tooltip("Seconds the demon stays in this pose (jittering in stop-motion). Ignored for the last stage.")]
            public float holdSeconds;
            [Tooltip("Seconds of darkness (model hidden) BEFORE this pose appears.")]
            public float darkBefore;
        }

        [Tooltip("The approach, one pose per step: the model vanishes, then reappears closer. The LAST stage is the final jumpscare (the jumpscare sound plays there) and then holds, shaking, until it is banished. Leave EMPTY (default) for the single hard lunge.")]
        [SerializeField] private ModelStage[] modelStages = new ModelStage[0];
        [Tooltip("Frames per second of the stop-motion jitter. Lower = choppier and creepier.")]
        [SerializeField] private float stopMotionFps = 8f;
        [Tooltip("How hard the head shakes in the final pose (fraction of camera height).")]
        [SerializeField] private float finalShake = 0.025f;
        [Tooltip("Bones squashed to zero size at start so the unfinished arms/legs never show.")]
        [SerializeField] private string[] hideBoneNames =
        {
            "mixamorig:LeftArm", "mixamorig:RightArm", "mixamorig:LeftUpLeg", "mixamorig:RightUpLeg"
        };

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
        private static float _intensityBeforeDemon = 1f;
        public static bool AnyRevealed => _revealedCount > 0;

        // The jumpscare art, reused by DemonLossJumpscare for every loss.
        public VideoClip JumpscareVideo => jumpscareVideo;
        public Sprite JumpscareSprite =>
            overlayRoot != null ? overlayRoot.GetComponentInChildren<SpriteRenderer>(true)?.sprite : null;

        private Anomaly _anomaly;
        private GlitchDirector _glitchDirector;
        private Camera _camera;
        private RoomDefinition _room;
        private float _roomCameraX;

        private VideoPlayer _videoPlayer;
        private GameObject _videoOverlayObject;

        private float _revealedAt;
        private readonly List<Tween> _motion = new List<Tween>();
        private bool _revealed;
        private Coroutine _modelRoutine;
        private Vector3 _modelFullScale = Vector3.one;
        private bool _modelScaleCaptured;
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
            if (model3D != null)
                model3D.gameObject.SetActive(false); // hidden until the jumpscare reveals it
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
            KillMotion();

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
            PlayRevealMotion();

            // With a staged 3D approach the sound is saved for the final pose.
            if (jumpscareAudio != null && !UsesModelStages)
                jumpscareAudio.Play();

            var reportManager = IncidentReportManager.Instance;
            if (reportManager != null) reportManager.SetAlert(true);

            if (_glitchDirector != null)
            {
                if (_revealedCount == 1) _intensityBeforeDemon = _glitchDirector.Intensity;
                _glitchDirector.SetIntensity(glitchIntensityWhileActive);
            }

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
            if (model3D != null)
            {
                if (overlayRoot != null) overlayRoot.SetActive(true);
                ShowModelOverlay();
                return;
            }

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

        private bool UsesModelStages => model3D != null && modelStages != null && modelStages.Length > 0;

        private void HideBones()
        {
            if (hideBoneNames == null || hideBoneNames.Length == 0) return;
            foreach (var t in model3D.GetComponentsInChildren<Transform>(true))
                if (System.Array.IndexOf(hideBoneNames, t.name) >= 0)
                    t.localScale = Vector3.zero;
        }

        private void ShowModelOverlay()
        {
            // The flat art stays on the prefab (the loss scare reuses it) but is hidden here.
            var flat = overlayRoot != null ? overlayRoot.GetComponentInChildren<SpriteRenderer>(true) : null;
            if (flat != null) flat.enabled = false; // component only: the sprite sits on Overlay Root, which also holds the model

            model3D.gameObject.SetActive(!UsesModelStages); // staged mode reveals it itself
            HideBones();
            // X = centre of this room's camera view. Y and Z are fixed world values.
            model3D.position = new Vector3(_roomCameraX, modelWorldY, modelWorldZ);

            if (!UsesModelStages)
            {
                if (!_modelScaleCaptured) { _modelFullScale = model3D.localScale; _modelScaleCaptured = true; }
                model3D.DOKill();
                model3D.localScale = _modelFullScale * slamStartFraction;
                model3D.DOScale(_modelFullScale, Mathf.Max(0.01f, slamSeconds)).SetEase(Ease.OutExpo).SetLink(model3D.gameObject);
            }
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
            if (model3D != null)
                model3D.gameObject.SetActive(false);
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

        // Lunge in with a shake and a flicker, then keep creeping closer and twitching so the
        // face never sits still while the player scrambles to report it.
        private void PlayRevealMotion()
        {
            KillMotion();

            if (UsesModelStages)
            {
                float lead = 0f;
                for (int i = 0; i < modelStages.Length - 1; i++) lead += modelStages[i].darkBefore + modelStages[i].holdSeconds;
                lead += modelStages[modelStages.Length - 1].darkBefore;
                _revealedAt += lead; // the countdown starts when the real scare lands
                _modelRoutine = StartCoroutine(ModelSequence());
                return;
            }

            // 3D model: shown exactly as placed in the prefab/scene (no scale pop, no shake).
            // Its only motion is DemonHeadTwitch.
            if (model3D != null) return;

            Transform face = null;
            if (false)
                face = model3D;
            else if (jumpscareVideo != null && _videoOverlayObject != null)
                face = _videoOverlayObject.transform;
            else if (overlayRoot != null)
            {
                var sprite = overlayRoot.GetComponentInChildren<SpriteRenderer>(true);
                if (sprite != null) face = sprite.transform;
            }
            if (face == null) return;

            var spriteRenderer = face.GetComponent<SpriteRenderer>();
            Vector3 baseScale = face.localScale;
            float camHeight = _camera != null && _camera.orthographic ? _camera.orthographicSize * 2f : 10f;
            float shake = camHeight * 0.035f;

            if (spriteRenderer != null)
                spriteRenderer.color = Color.white;
            face.localScale = baseScale * 0.5f;

            var lunge = DOTween.Sequence().SetLink(overlayRoot);
            lunge.Append(face.DOScale(baseScale * 1.12f, 0.14f).SetEase(Ease.OutExpo));
            lunge.Join(face.DOShakePosition(0.7f, new Vector3(shake, shake * 0.6f, 0f), 60, 90f, false, true));
            if (spriteRenderer != null)
                lunge.Join(spriteRenderer.DOFade(0.2f, 0.04f).SetLoops(6, LoopType.Yoyo));
            lunge.OnComplete(() => StartCreep(face, spriteRenderer, baseScale, shake));
            _motion.Add(lunge);
        }

        private void StartCreep(Transform face, SpriteRenderer spriteRenderer, Vector3 baseScale, float shake)
        {
            if (_resolved || face == null) return;

            _motion.Add(face.DOScale(baseScale * 1.07f, 1.3f).SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo).SetLink(overlayRoot));

            ScheduleTwitch(face, spriteRenderer, shake);
        }

        private void ScheduleTwitch(Transform face, SpriteRenderer spriteRenderer, float shake)
        {
            _motion.Add(DOVirtual.DelayedCall(Random.Range(1.2f, 3.2f), () =>
            {
                if (_resolved || face == null) return;

                _motion.Add(face.DOShakePosition(0.3f, new Vector3(shake * 1.3f, shake * 0.8f, 0f), 40, 90f, false, true)
                    .SetLink(overlayRoot));
                if (spriteRenderer != null)
                    _motion.Add(spriteRenderer.DOFade(0.3f, 0.05f).SetLoops(4, LoopType.Yoyo).SetLink(overlayRoot));

                ScheduleTwitch(face, spriteRenderer, shake);
            }).SetLink(overlayRoot));
        }

        private IEnumerator ModelSequence()
        {
            float step = 1f / Mathf.Max(1f, stopMotionFps);
            var wait = new WaitForSeconds(step);
            Vector3 baseScale = model3D.localScale;
            float camH = _camera != null && _camera.orthographic ? _camera.orthographicSize * 2f : 10f;
            float cx = _roomCameraX;
            float cy = _camera != null ? _camera.transform.position.y : model3D.position.y;
            float z = model3D.position.z;

            model3D.gameObject.SetActive(false);

            for (int i = 0; i < modelStages.Length; i++)
            {
                var st = modelStages[i];
                bool last = i == modelStages.Length - 1;

                if (st.darkBefore > 0f)
                {
                    model3D.gameObject.SetActive(false);
                    yield return new WaitForSeconds(st.darkBefore);
                }

                Place(st, cx, cy, z, camH, baseScale, Vector2.zero, 0f);
                model3D.gameObject.SetActive(true);
                HideBones();

                if (last)
                {
                    if (jumpscareAudio != null) jumpscareAudio.Play();
                    break;
                }

                for (float t = 0f; t < st.holdSeconds; t += step)
                {
                    Place(st, cx, cy, z, camH, baseScale, Random.insideUnitCircle * camH * 0.006f, Random.Range(-1.5f, 1.5f));
                    yield return wait;
                }
            }

            // Final pose: stop-motion shake, with a hard snap of the head now and then.
            var fin = modelStages[modelStages.Length - 1];
            float nextSnap = Time.time + Random.Range(1.2f, 2.8f);
            float snapRoll = 0f;
            while (true)
            {
                if (Time.time >= nextSnap)
                {
                    snapRoll = Random.value < 0.5f ? Random.Range(10f, 18f) : Random.Range(-18f, -10f);
                    nextSnap = Time.time + Random.Range(1.2f, 2.8f);
                }
                else snapRoll = Mathf.MoveTowards(snapRoll, 0f, 6f);

                Place(fin, cx, cy, z, camH, baseScale,
                    Random.insideUnitCircle * camH * finalShake,
                    Random.Range(-2.5f, 2.5f) + snapRoll);
                yield return wait;
            }
        }

        private void Place(ModelStage st, float cx, float cy, float z, float camH, Vector3 baseScale, Vector2 jitter, float rollJitter)
        {
            model3D.position = new Vector3(cx + st.viewOffset.x * camH + jitter.x, cy + st.viewOffset.y * camH + jitter.y, z);
            model3D.localScale = baseScale * (st.scale <= 0f ? 1f : st.scale);
            var e = model3D.localEulerAngles;
            model3D.localEulerAngles = new Vector3(e.x, e.y, st.rollDegrees + rollJitter);
        }

        private void KillMotion()
        {
            if (_modelRoutine != null) { StopCoroutine(_modelRoutine); _modelRoutine = null; }
            foreach (var tween in _motion)
                tween?.Kill();
            _motion.Clear();
        }

        private void EndThreat()
        {
            KillMotion();
            if (!_revealed) return;

            _revealedCount = Mathf.Max(0, _revealedCount - 1);

            var reportManager = IncidentReportManager.Instance;
            if (reportManager != null) reportManager.SetAlert(false);

            if (_glitchDirector != null && _revealedCount == 0)
                _glitchDirector.SetIntensity(_intensityBeforeDemon);
        }
    }
}
