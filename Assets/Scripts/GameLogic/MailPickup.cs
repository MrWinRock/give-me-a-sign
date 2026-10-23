using GameLogic.Data;
using GameLogic.Flow;
using GameLogic.Save;
using Player; // PlayerInputActions - generated Input Actions wrapper, same one ClickManager uses
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Video;

namespace GameLogic
{
    /// <summary>
    /// A clickable document sitting in a room. Clicking it marks its MailData as found (readable
    /// afterwards from the desktop Mail window) and removes it from the world.
    ///
    /// Active only during the shift its MailData.unlockDay names, and only until it's actually
    /// been found - so a retry of the same day, or any later day, never re-shows a document
    /// that's already sitting in the player's inbox. Same click-detection approach as
    /// Player/Click/ClickManager.cs (a 2D physics raycast from the current camera), kept
    /// self-contained here so placing a pickup needs no wiring beyond dropping it in a room.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class MailPickup : MonoBehaviour
    {
        [Tooltip("Which document this pickup grants. Its unlockDay decides which shift this pickup is active during.")]
        [SerializeField] private MailData mailData;

        [Tooltip("Clip played fullscreen when this document is collected (e.g. Assets/Video/Transition.mp4). The player stays in the current scene.")]
        [SerializeField] private VideoClip transitionClip;

        [Tooltip("How long the clip stays on screen before it is cut.")]
        [Min(0.02f)] [SerializeField] private float videoSeconds = 0.1f;

        [Tooltip("Played through the game's SFX channel on collect (e.g. Assets/Audio/camera-shutter-314056.mp3).")]
        [SerializeField] private AudioClip collectSound;

        [Tooltip("Camera clicks are raycast from. Auto-resolved via Camera.main if left empty.")]
        [SerializeField] private Camera mainCamera;

        [Tooltip("Fired the moment this document is collected - hook VFX/SFX here.")]
        public UnityEvent OnCollected = new UnityEvent();

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        /// <summary>Debug panel switch: while true every pickup ignores the unlockDay / already-found gate.</summary>
        public static bool DebugShowAll;

        [Tooltip("Fraction of the night that must pass before this document appears (0.5 = halfway through the shift).")]
        [Range(0f, 1f)] [SerializeField] private float appearAtNightFraction = 0.5f;

        private Collider2D _collider;
        private GameLogic.SpawnAndTime.NightTimer _timer;
        private bool _revealed = true;
        private PlayerInputActions _inputActions;

        void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _inputActions = new PlayerInputActions();
        }

        void OnEnable()
        {
            if (mailData == null || string.IsNullOrEmpty(mailData.emailId))
            {
                Debug.LogWarning("MailPickup: no MailData assigned - disabling.", this);
                enabled = false;
                return;
            }

            // Already found earlier, or not today's document - never show it.
            if (!DebugShowAll && (SaveManager.Current.IsMailFound(mailData.emailId) ||
                !mailData.IsScheduledForDay(GameFlowManager.CurrentDay)))
            {
                gameObject.SetActive(false);
                return;
            }

            if (mainCamera == null) mainCamera = Camera.main;
            _timer = FindFirstObjectByType<GameLogic.SpawnAndTime.NightTimer>();
            SetRevealed(TimeReached());
            _inputActions.Player.Click.performed += OnClick;
            _inputActions.Player.Enable();
        }

        void Update()
        {
            if (!_revealed && TimeReached()) SetRevealed(true);
        }

        private bool TimeReached() =>
            DebugShowAll || _timer == null || _timer.GetNormalizedTime() >= appearAtNightFraction;

        /// <summary>Hides/shows the model and collider while the object itself stays active to watch the clock.</summary>
        private void SetRevealed(bool on)
        {
            _revealed = on;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = on;
            if (_collider != null) _collider.enabled = on;
        }

        void OnDisable()
        {
            _inputActions.Player.Click.performed -= OnClick;
            _inputActions.Player.Disable();
        }

        void OnDestroy()
        {
            _inputActions.Dispose();
        }

        private static readonly System.Collections.Generic.List<RaycastResult> _uiHits = new System.Collections.Generic.List<RaycastResult>();

        /// <summary>
        /// True only if a UI element you can actually see sits under the pointer. Invisible full-screen
        /// panels (alpha 0 but Raycast Target on) must not swallow world clicks. Queried live, because
        /// EventSystem.IsPointerOverGameObject() returns last frame's state inside an input callback.
        /// </summary>
        private bool IsBlockedByVisibleUi(Vector2 screenPos)
        {
            if (EventSystem.current == null) return false;
            _uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPos }, _uiHits);
            foreach (var hit in _uiHits)
            {
                var g = hit.gameObject.GetComponent<UnityEngine.UI.Graphic>();
                bool invisible = g != null && g.color.a < 0.02f && !(g is TMPro.TMP_Text);
                if (invisible) continue;
                if (showDebugInfo) Debug.Log($"MailPickup: click blocked by UI '{hit.gameObject.name}'.", this);
                return true;
            }
            return false;
        }

        private void OnClick(InputAction.CallbackContext ctx)
        {
            if (!_revealed || Mouse.current == null) return;
            if (IsBlockedByVisibleUi(Mouse.current.position.ReadValue()))
                return;

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) return;
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(ray);

            foreach (var hit in hits)
            {
                if (hit.collider == _collider)
                {
                    if (showDebugInfo) Debug.Log("MailPickup: clicked.", this);
                    Collect();
                    return;
                }
            }
        }

        /// <summary>
        /// Debug-only entry point (see GameFlowManager's Skip Night actions): collects immediately
        /// without a click, but still respects the same day/already-found gate as normal play, so
        /// it never grants a document that isn't actually today's.
        /// </summary>
        public void ForceCollect()
        {
            if (mailData == null) return;
            if (!mailData.IsScheduledForDay(GameFlowManager.CurrentDay)) return;
            if (SaveManager.Current.IsMailFound(mailData.emailId)) return;

            Collect();
        }

        /// <summary>Debug panel: show/hide every MailPickup in the scene, regardless of day or whether it was found.</summary>
        public static int DebugSetAllVisible(bool visible)
        {
            DebugShowAll = visible;
            var all = FindObjectsByType<MailPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var p in all)
            {
                p.gameObject.SetActive(false); // re-run OnEnable so the click handler is wired once
                if (visible) p.gameObject.SetActive(true);
            }
            return all.Length;
        }

        private void Collect()
        {
            SaveManager.Current.MarkMailFound(mailData.emailId);
            SaveManager.Save();

            if (showDebugInfo)
                Debug.Log($"MailPickup: found '{mailData.subject}'.", this);

            MailPickupVideo.Play(transitionClip, videoSeconds, collectSound);
            OnCollected?.Invoke();
            gameObject.SetActive(false);
        }
    }
}
