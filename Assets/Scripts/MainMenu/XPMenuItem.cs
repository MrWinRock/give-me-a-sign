using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// One row of the Start menu (and of its footer). Owns nothing but its own hover visuals -
    /// <see cref="StartMenuController"/> wires the click, <see cref="DesktopManager"/> runs the
    /// <see cref="DesktopAction"/>.
    /// </summary>
    public class XPMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Content")]
        [SerializeField] private string labelString = "Start Shift";
        [SerializeField] private string subtitleString = "Begin monitoring";
        [SerializeField] private DesktopAction action = DesktopAction.StartShift;

        [Header("View")]
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI label;
        [Tooltip("Optional 9px grey line under the label. Leave empty for items without a subtitle.")]
        [SerializeField] private TextMeshProUGUI subtitle;

        [Header("Colors")]
        [SerializeField] private Color normalBackground = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color hoverBackground = XPPalette.Hex("#316AC5");
        [SerializeField] private Color normalLabelColor = Color.black;
        [SerializeField] private Color hoverLabelColor = Color.white;
        [SerializeField] private Color normalSubtitleColor = XPPalette.Hex("#808080");
        [SerializeField] private Color hoverSubtitleColor = XPPalette.Hex("#CFE0FF");

        public Button Button => button;
        public DesktopAction Action => action;

        void Awake()
        {
            ApplyContent();
            ApplyHover(false);
        }

        public void OnPointerEnter(PointerEventData eventData) => ApplyHover(true);
        public void OnPointerExit(PointerEventData eventData) => ApplyHover(false);

        void OnDisable() => ApplyHover(false);

        private void ApplyHover(bool hovered)
        {
            if (background != null)
                background.color = hovered ? hoverBackground : normalBackground;

            if (label != null)
                label.color = hovered ? hoverLabelColor : normalLabelColor;

            if (subtitle != null)
                subtitle.color = hovered ? hoverSubtitleColor : normalSubtitleColor;
        }

        private void ApplyContent()
        {
            if (label != null)
                label.text = labelString;

            if (subtitle != null)
            {
                subtitle.text = subtitleString;
                subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitleString));
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying)
                ApplyContent();
        }
#endif
    }
}
