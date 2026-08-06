using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// Base class for every XP dialog on the desktop. Handles the parts every window shares:
    /// titlebar text, the red close button, Show/Hide, bring-to-front on click, and telling
    /// <see cref="DesktopManager"/> so only one window is on screen at a time.
    ///
    /// Subclasses override <see cref="OnShown"/> / <see cref="OnHiding"/> for their own content;
    /// they must call base.Awake() if they declare Awake.
    /// </summary>
    public class XPWindowController : MonoBehaviour, IPointerDownHandler
    {
        [Header("Window")]
        [Tooltip("The object toggled on/off. Leave empty to use this GameObject.")]
        [SerializeField] protected GameObject windowRoot;
        [Tooltip("The window's RectTransform, used to apply windowWidth. Leave empty to use this one.")]
        [SerializeField] protected RectTransform windowRect;
        [SerializeField] private float windowWidth = 275f;

        [Header("Titlebar")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private string windowTitle = "Window";
        [SerializeField] private Button closeButton;

        /// <summary>Fired after the window hides, whatever closed it.</summary>
        public event Action<XPWindowController> Closed;

        /// <summary>The desktop that spawned this window. Null until <see cref="Bind"/> is called.</summary>
        protected DesktopManager Desktop { get; private set; }

        public bool IsOpen => windowRoot != null && windowRoot.activeSelf;
        public string WindowTitle => windowTitle;

        protected virtual void Awake()
        {
            if (windowRoot == null) windowRoot = gameObject;
            if (windowRect == null) windowRect = GetComponent<RectTransform>();

            if (titleText != null)
                titleText.text = windowTitle;

            if (windowRect != null && windowWidth > 0f)
                windowRect.sizeDelta = new Vector2(windowWidth, windowRect.sizeDelta.y);

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(OnCloseButtonClicked);
                closeButton.onClick.AddListener(OnCloseButtonClicked);
            }
        }

        /// <summary>Called by DesktopManager right after the instance is spawned.</summary>
        public void Bind(DesktopManager desktop)
        {
            Desktop = desktop;
            OnBound();
        }

        /// <summary>
        /// Hides the freshly spawned instance WITHOUT firing the closed callbacks - the window
        /// was never open, so it must not count as a close (no sound, no state change).
        /// Awake() has already run at this point: Instantiate() on an active prefab runs it
        /// synchronously, so windowRoot is resolved. The fallback keeps this safe regardless.
        /// </summary>
        public void InitializeHidden()
        {
            (windowRoot != null ? windowRoot : gameObject).SetActive(false);
        }

        public virtual void Show()
        {
            if (windowRoot == null) return;

            windowRoot.SetActive(true);
            Desktop?.NotifyWindowShown(this);
            OnShown();
        }

        public virtual void Hide()
        {
            if (!IsOpen) return;

            OnHiding();
            windowRoot.SetActive(false);

            Closed?.Invoke(this);
            Desktop?.NotifyWindowClosed(this);
        }

        /// <summary>Raise this window above its siblings.</summary>
        public void Focus() => Desktop?.BringToFront(this);

        /// <summary>Clicking anywhere in the window raises it. (Trivial today - only one window
        /// is ever open - but it keeps z-order correct if that rule is ever relaxed.)</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            Focus();
        }

        /// <summary>
        /// What the titlebar's X does. Virtual because some windows need it to mean something
        /// more specific than "hide" - the Control Panel treats it as Cancel, like real XP.
        /// </summary>
        protected virtual void OnCloseButtonClicked() => Hide();

        // ---- subclass hooks ----
        protected virtual void OnBound() { }
        protected virtual void OnShown() { }
        protected virtual void OnHiding() { }
    }
}
