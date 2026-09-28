using GameLogic.Data;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// One sidebar row in the Field Manual: a label plus its three visual states (normal / hover /
    /// selected), all serialized colours so the look can be tuned in the Inspector without a code
    /// change. Cloned from a template per entry by <see cref="FieldManualUI"/>.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class FieldManualEntryButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Refs")]
        [SerializeField] private Image background;
        [SerializeField] private Image selectedBar; // 3px left accent bar, shown only when selected
        [SerializeField] private TextMeshProUGUI label;

        [Header("Colors")]
        [SerializeField] private Color normalText = XPPalette.Hex("#6B6A5E");
        [SerializeField] private Color selectedText = XPPalette.Hex("#0A246A");
        [SerializeField] private Color normalBg = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color hoverBg = XPPalette.Hex("#DCDCD0");
        [SerializeField] private Color selectedBg = XPPalette.Hex("#C5D5EE");
        [SerializeField] private Color selectedBarColor = XPPalette.Hex("#316AC5");

        public Button Button { get; private set; }
        public AnomalyDefinition Entry { get; private set; }

        private bool _selected;
        private bool _hovering;

        void Awake()
        {
            EnsureButton();
        }

        // Instantiate() doesn't guarantee Awake() has already run by the time the caller touches
        // the new instance (observed when a window is opened from outside Unity's own Update loop) -
        // resolve lazily instead of trusting Awake alone, so Setup() never NREs on a fresh clone.
        private void EnsureButton()
        {
            if (Button == null) Button = GetComponent<Button>();
        }

        public void Setup(AnomalyDefinition entry, System.Action<AnomalyDefinition> onClick)
        {
            EnsureButton();

            Entry = entry;
            if (label != null) label.text = entry != null ? entry.Label : "-";

            Button.onClick.RemoveAllListeners();
            if (onClick != null) Button.onClick.AddListener(() => onClick(entry));

            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovering = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            Refresh();
        }

        private void Refresh()
        {
            if (background != null)
                background.color = _selected ? selectedBg : (_hovering ? hoverBg : normalBg);

            if (selectedBar != null)
                selectedBar.gameObject.SetActive(_selected);
            if (selectedBar != null)
                selectedBar.color = selectedBarColor;

            if (label != null)
            {
                label.color = _selected ? selectedText : normalText;
                label.fontStyle = _selected ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }
}
