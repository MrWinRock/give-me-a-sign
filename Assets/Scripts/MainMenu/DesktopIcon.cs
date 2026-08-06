using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// One desktop icon: 32x32 image with a label underneath. Pure view + click forwarding -
    /// <see cref="DesktopManager"/> owns selection state and double-click timing, so an icon
    /// never has to know about its siblings.
    ///
    /// The label is drawn twice (a black copy offset 1px behind the white one) because TMP does
    /// not run uGUI's Shadow mesh effect.
    /// </summary>
    public class DesktopIcon : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string label = "Start Shift";
        [Tooltip("What a double-click on this icon does.")]
        [SerializeField] private DesktopAction action = DesktopAction.StartShift;

        [Header("View")]
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI labelText;
        [Tooltip("Black copy of the label sitting 1px behind, for the XP drop shadow.")]
        [SerializeField] private TextMeshProUGUI labelShadowText;

        [Header("Selection Visual")]
        [Tooltip("Highlight quad behind the label/icon. Tinted with selectedColor while selected.")]
        [SerializeField] private Image selectionBackground;
        [Tooltip("1px dotted white outline shown only while selected.")]
        [SerializeField] private GameObject selectionOutline;
        [SerializeField] private Color selectedColor = XPPalette.Hex("#316AC5", 0.4f);
        [SerializeField] private Color deselectedColor = new Color(0f, 0f, 0f, 0f);

        private DesktopManager _desktop;

        public DesktopAction Action => action;
        public string Label => label;
        public bool IsSelected { get; private set; }

        void Awake()
        {
            ApplyLabel();
            SetSelected(false);
        }

        /// <summary>Called by DesktopManager in Awake so the icon doesn't need a manager reference wired.</summary>
        public void Bind(DesktopManager desktop)
        {
            _desktop = desktop;

            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
                button.onClick.AddListener(OnClicked);
            }
        }

        private void OnClicked()
        {
            _desktop?.OnIconClicked(this);
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;

            if (selectionBackground != null)
                selectionBackground.color = selected ? selectedColor : deselectedColor;

            if (selectionOutline != null)
                selectionOutline.SetActive(selected);
        }

        public void SetIcon(Sprite sprite)
        {
            if (iconImage != null)
                iconImage.sprite = sprite;
        }

        private void ApplyLabel()
        {
            if (labelText != null) labelText.text = label;
            if (labelShadowText != null) labelShadowText.text = label;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying)
                ApplyLabel();
        }
#endif
    }
}
