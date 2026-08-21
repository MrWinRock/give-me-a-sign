using GameLogic.Data;
using GameLogic.Flow;
using GameLogic.Save;
using Player; // PlayerInputActions - generated Input Actions wrapper, same one ClickManager uses
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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

        [Tooltip("Camera clicks are raycast from. Auto-resolved via Camera.main if left empty.")]
        [SerializeField] private Camera mainCamera;

        [Tooltip("Fired the moment this document is collected - hook VFX/SFX here.")]
        public UnityEvent OnCollected = new UnityEvent();

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private Collider2D _collider;
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
            if (SaveManager.Current.IsMailFound(mailData.emailId) ||
                !mailData.IsScheduledForDay(GameFlowManager.CurrentDay))
            {
                gameObject.SetActive(false);
                return;
            }

            if (mainCamera == null) mainCamera = Camera.main;
            _inputActions.Player.Click.performed += OnClick;
            _inputActions.Player.Enable();
        }

        void OnDisable()
        {
            _inputActions.Player.Click.performed -= OnClick;
            _inputActions.Player.Disable();
        }

        private void OnClick(InputAction.CallbackContext ctx)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
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

        private void Collect()
        {
            SaveManager.Current.MarkMailFound(mailData.emailId);
            SaveManager.Save();

            if (showDebugInfo)
                Debug.Log($"MailPickup: found '{mailData.subject}'.", this);

            OnCollected?.Invoke();
            gameObject.SetActive(false);
        }
    }
}
