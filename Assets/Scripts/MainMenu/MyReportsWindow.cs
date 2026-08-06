using System;
using System.Collections.Generic;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// Start &gt; My Reports: the save/continue window, dressed as a folder of .log files.
    /// The list is a serialized data source, so slots can be authored in the Inspector now and
    /// swapped for real save data later (see <see cref="Slots"/> / <see cref="SetSlots"/>).
    /// </summary>
    public class MyReportsWindow : XPWindowController
    {
        public enum SlotState
        {
            /// <summary>A normal, openable save.</summary>
            Available,
            /// <summary>Openable, but drawn greyed - the shift was never finished.</summary>
            Incomplete,
            /// <summary>Cannot be selected at all.</summary>
            Corrupted
        }

        [Serializable]
        public class SaveSlot
        {
            public string fileName = "shift_01.log";
            public string detail = "3 reports filed";
            public SlotState state = SlotState.Available;
        }

        [Header("Data Source")]
        [SerializeField]
        private List<SaveSlot> slots = new List<SaveSlot>
        {
            new SaveSlot { fileName = "shift_01.log", detail = "3 reports filed", state = SlotState.Available },
            new SaveSlot { fileName = "shift_02.log", detail = "incomplete",      state = SlotState.Incomplete },
            new SaveSlot { fileName = "shift_03.log", detail = "corrupted",       state = SlotState.Corrupted },
        };

        [Header("List")]
        [Tooltip("Vertical container the rows are spawned into.")]
        [SerializeField] private RectTransform rowContainer;
        [Tooltip("Inactive template row (Button + Image background + TMP label) cloned per slot.")]
        [SerializeField] private GameObject rowTemplate;

        [Header("Row Colors")]
        [SerializeField] private Color availableTextColor = Color.black;
        [SerializeField] private Color incompleteTextColor = XPPalette.Hex("#808080");
        [SerializeField] private Color corruptedTextColor = XPPalette.Hex("#B4B4B4");
        [SerializeField] private Color rowNormalColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color rowSelectedColor = XPPalette.Hex("#316AC5", 0.35f);
        [SerializeField] private Color selectedTextColor = Color.white;

        [Header("Buttons")]
        [SerializeField] private Button openButton;
        [SerializeField] private Button cancelButton;

        private readonly List<GameObject> _rows = new List<GameObject>();
        private int _selectedIndex = -1;

        /// <summary>Read-only view of the authored slots.</summary>
        public IReadOnlyList<SaveSlot> Slots => slots;

        /// <summary>Replaces the slot list at runtime (for when real saves exist). Rebuilds the rows.</summary>
        public void SetSlots(List<SaveSlot> newSlots)
        {
            slots = newSlots ?? new List<SaveSlot>();
            if (IsOpen)
                RebuildRows();
        }

        protected override void Awake()
        {
            base.Awake();

            if (rowTemplate != null)
                rowTemplate.SetActive(false);

            if (openButton != null) openButton.onClick.AddListener(OnOpenClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
        }

        protected override void OnShown()
        {
            RebuildRows();
        }

        private void RebuildRows()
        {
            if (rowContainer == null || rowTemplate == null)
            {
                Debug.LogWarning("MyReportsWindow: rowContainer / rowTemplate not assigned.", this);
                return;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] == null) continue;

                // Unparent before Destroy: destruction is deferred to end-of-frame, and a row left
                // in the container would be laid out alongside its replacement for one frame.
                _rows[i].transform.SetParent(null, false);
                Destroy(_rows[i]);
            }
            _rows.Clear();

            _selectedIndex = -1;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;

                var row = Instantiate(rowTemplate, rowContainer);
                row.name = "Row_" + slot.fileName;
                row.SetActive(true);
                _rows.Add(row);

                var rowLabel = row.GetComponentInChildren<TextMeshProUGUI>(true);
                if (rowLabel != null)
                    rowLabel.text = string.IsNullOrEmpty(slot.detail)
                        ? slot.fileName
                        : $"{slot.fileName} — {slot.detail}";

                var rowButton = row.GetComponent<Button>();
                if (rowButton != null)
                {
                    int index = i;
                    rowButton.onClick.RemoveAllListeners();
                    rowButton.onClick.AddListener(() => OnRowClicked(index));
                    rowButton.interactable = slot.state != SlotState.Corrupted;
                }
            }

            RefreshRowVisuals();
            UpdateOpenInteractable();
        }

        private void OnRowClicked(int index)
        {
            if (index < 0 || index >= slots.Count) return;

            if (slots[index].state == SlotState.Corrupted)
            {
                Desktop?.PlayCue(MenuCue.Error);
                return;
            }

            _selectedIndex = index;
            RefreshRowVisuals();
            UpdateOpenInteractable();
        }

        private void RefreshRowVisuals()
        {
            for (int i = 0; i < _rows.Count && i < slots.Count; i++)
            {
                bool selected = i == _selectedIndex;

                var bg = _rows[i].GetComponent<Image>();
                if (bg != null)
                    bg.color = selected ? rowSelectedColor : rowNormalColor;

                var rowLabel = _rows[i].GetComponentInChildren<TextMeshProUGUI>(true);
                if (rowLabel != null)
                    rowLabel.color = selected ? selectedTextColor : StateColor(slots[i].state);
            }
        }

        private Color StateColor(SlotState state)
        {
            switch (state)
            {
                case SlotState.Incomplete: return incompleteTextColor;
                case SlotState.Corrupted: return corruptedTextColor;
                default: return availableTextColor;
            }
        }

        private void UpdateOpenInteractable()
        {
            if (openButton != null)
                openButton.interactable = _selectedIndex >= 0;
        }

        private void OnOpenClicked()
        {
            if (_selectedIndex < 0 || _selectedIndex >= slots.Count)
            {
                Desktop?.PlayCue(MenuCue.Error);
                return;
            }

            // No save system yet: opening a slot just begins the shift. Swap this for a real
            // load call once saves exist - the selected slot is slots[_selectedIndex].
            Debug.Log($"MyReportsWindow: opening '{slots[_selectedIndex].fileName}'.", this);

            Hide();
            Desktop?.StartShift();
        }

        protected override void OnHiding()
        {
            _selectedIndex = -1;
        }
    }
}
