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

        public event Action<XPWindowController> Closed;

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

        public void Bind(DesktopManager desktop)
        {
            Desktop = desktop;
            OnBound();
        }

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

        public void Focus() => Desktop?.BringToFront(this);

        public void OnPointerDown(PointerEventData eventData)
        {
            Focus();
        }

        protected virtual void OnCloseButtonClicked() => Hide();

        // ---- subclass hooks ----
        protected virtual void OnBound() { }
        protected virtual void OnShown() { }
        protected virtual void OnHiding() { }
    }
}
