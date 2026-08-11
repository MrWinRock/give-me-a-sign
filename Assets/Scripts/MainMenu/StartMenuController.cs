using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// The XP Start menu: open/close, the depressed start-button look while open, and routing
    /// each item to its <see cref="DesktopAction"/>.
    /// </summary>
    public class StartMenuController : MonoBehaviour
    {
        [Header("Menu")]
        [Tooltip("The panel that opens upward from the start button. Toggled active/inactive.")]
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private TextMeshProUGUI userNameText;
        [SerializeField] private string userName = "SEC-04";

        [Header("Start Button")]
        [SerializeField] private Button startButton;
        [SerializeField] private UIGradient startButtonGradient;
        [SerializeField] private RectTransform startButtonLabel;
        [SerializeField] private Color idleTopColor = XPPalette.Hex("#5EAC56");
        [SerializeField] private Color idleBottomColor = XPPalette.Hex("#2D7D28");
        [Tooltip("Darker while the menu is open, so the button reads as held down.")]
        [SerializeField] private Color pressedTopColor = XPPalette.Hex("#3F7C39");
        [SerializeField] private Color pressedBottomColor = XPPalette.Hex("#1E5A1B");
        [Tooltip("Label nudge (px) while depressed. XP shifts the text down-right by 1.")]
        [SerializeField] private Vector2 pressedLabelOffset = new Vector2(1f, -1f);

        [Header("Items")]
        [Tooltip("Start Shift / My Reports / Control Panel / Help and Support, in display order.")]
        [SerializeField] private XPMenuItem[] items = new XPMenuItem[0];
        [Tooltip("Log Off / Turn Off Computer.")]
        [SerializeField] private XPMenuItem[] footerItems = new XPMenuItem[0];

        private DesktopManager _desktop;
        private Vector2 _labelHomePosition;

        public bool IsOpen => menuRoot != null && menuRoot.activeSelf;

        void Awake()
        {
            if (startButtonLabel != null)
                _labelHomePosition = startButtonLabel.anchoredPosition;

            if (userNameText != null)
                userNameText.text = userName;

            if (menuRoot != null)
                menuRoot.SetActive(false);

            ApplyStartButtonVisual(false);
        }

        public void Bind(DesktopManager desktop)
        {
            _desktop = desktop;

            if (startButton != null)
            {
                startButton.onClick.RemoveListener(Toggle);
                startButton.onClick.AddListener(Toggle);
            }

            BindItems(items);
            BindItems(footerItems);
        }

        private void BindItems(XPMenuItem[] menuItems)
        {
            for (int i = 0; i < menuItems.Length; i++)
            {
                var item = menuItems[i];
                if (item == null || item.Button == null) continue;

                var captured = item;
                captured.Button.onClick.RemoveAllListeners();
                captured.Button.onClick.AddListener(() => OnItemClicked(captured));
            }
        }

        private void OnItemClicked(XPMenuItem item)
        {
            Close();
            _desktop?.Execute(item.Action);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (menuRoot == null || IsOpen) return;

            // No SetAsLastSibling here: the menu is authored above the window layer already, and
            // reordering would also lift it above the boot/shutdown overlay.
            menuRoot.SetActive(true);
            ApplyStartButtonVisual(true);
            _desktop?.PlayCue(MenuCue.StartMenuOpen);
        }

        public void Close()
        {
            if (menuRoot == null || !IsOpen) return;

            menuRoot.SetActive(false);
            ApplyStartButtonVisual(false);
        }

        private void ApplyStartButtonVisual(bool pressed)
        {
            if (startButtonGradient != null)
            {
                startButtonGradient.SetColors(
                    pressed ? pressedTopColor : idleTopColor,
                    pressed ? pressedBottomColor : idleBottomColor);
            }

            if (startButtonLabel != null)
                startButtonLabel.anchoredPosition = pressed ? _labelHomePosition + pressedLabelOffset : _labelHomePosition;
        }
    }
}
