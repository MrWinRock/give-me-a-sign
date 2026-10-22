using System;
using System.Collections.Generic;
using TMPro;
using GameLogic.Flow;
using GameLogic.Save;
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
            Available,
            Incomplete,
            Corrupted,
            Empty
        }

        [Serializable]
        public class SaveSlot
        {
            public string fileName = "shift_01.log";
            public string detail = "3 reports filed";
            public SlotState state = SlotState.Available;
        }

        [Header("Data Source")]
        private List<SaveSlot> slots = new List<SaveSlot>();

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
        [Tooltip("Deletes the selected slot's save. Click twice: the first click asks 'Sure?'.")]
        [SerializeField] private Button deleteButton;

        /// <summary>Set by DesktopManager just before opening: the next Show() is the New Game slot picker.</summary>
        public static bool PendingNewGameMode;

        private bool _newGameMode;
        private bool _overwriteArmed;
        private string _openLabelOriginal;
        private bool _deleteArmed;
        private string _deleteLabelOriginal;

        private readonly List<GameObject> _rows = new List<GameObject>();
        private int _selectedIndex = -1;

        public IReadOnlyList<SaveSlot> Slots => slots;

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

            if (openButton != null)
            {
                openButton.onClick.AddListener(OnOpenClicked);
                var ol = openButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (ol != null) _openLabelOriginal = ol.text;
            }
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
            if (deleteButton != null)
            {
                deleteButton.onClick.AddListener(OnDeleteClicked);
                var l = deleteButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (l != null) _deleteLabelOriginal = l.text;
            }
        }

        protected override void OnShown()
        {
            _newGameMode = PendingNewGameMode;
            PendingNewGameMode = false;
            _overwriteArmed = false;
            SetOpenLabel(_newGameMode ? "New Game" : _openLabelOriginal);
            slots = BuildSlotsFromSave();
            RebuildRows();
        }

        // Three fixed slots, each its own save file. Empty slots are listed too: opening one starts a new shift there.
        private static List<SaveSlot> BuildSlotsFromSave()
        {
            var list = new List<SaveSlot>();
            for (int n = 1; n <= SaveManager.SlotCount; n++)
            {
                string file = $"shift_{n:00}.log";
                if (!SaveManager.Peek(n, out var save, out bool corrupted))
                {
                    list.Add(new SaveSlot { fileName = file, detail = "empty", state = SlotState.Empty });
                    continue;
                }
                if (corrupted)
                {
                    list.Add(new SaveSlot { fileName = file, detail = "corrupted", state = SlotState.Corrupted });
                    continue;
                }

                int day = Mathf.Clamp(save.currentDay, 1, GameFlowManager.FinalDay);
                int seen = save.seenAnomalyIds != null ? save.seenAnomalyIds.Count : 0;
                bool fresh = day <= 1 && seen == 0;
                list.Add(new SaveSlot
                {
                    fileName = file,
                    detail = fresh ? "incomplete" : $"Night {day} of {GameFlowManager.FinalDay}" + (seen > 0 ? $", {seen} anomalies logged" : ""),
                    state = fresh ? SlotState.Incomplete : SlotState.Available
                });
            }
            return list;
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
                    rowButton.interactable = _newGameMode || (slot.state != SlotState.Corrupted && slot.state != SlotState.Empty);
                }
            }

            RefreshRowVisuals();
            UpdateOpenInteractable();
        }

        private void OnRowClicked(int index)
        {
            if (index < 0 || index >= slots.Count) return;

            if (!_newGameMode && (slots[index].state == SlotState.Corrupted || slots[index].state == SlotState.Empty))
            {
                Desktop?.PlayCue(MenuCue.Error);
                return;
            }

            _selectedIndex = index;
            if (_overwriteArmed) { _overwriteArmed = false; SetOpenLabel("New Game"); }
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
                case SlotState.Incomplete:
                case SlotState.Empty: return incompleteTextColor;
                case SlotState.Corrupted: return corruptedTextColor;
                default: return availableTextColor;
            }
        }

        private void UpdateOpenInteractable()
        {
            if (openButton != null)
                openButton.interactable = _selectedIndex >= 0;
            DisarmDelete();
            if (deleteButton != null)
                deleteButton.interactable = _selectedIndex >= 0 && _selectedIndex < slots.Count
                                            && slots[_selectedIndex].state != SlotState.Empty;
        }

        private void SetOpenLabel(string text)
        {
            if (openButton == null || text == null) return;
            var l = openButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (l != null) l.text = text;
        }

        private void DisarmDelete()
        {
            _deleteArmed = false;
            if (deleteButton == null) return;
            var l = deleteButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (l != null && _deleteLabelOriginal != null) l.text = _deleteLabelOriginal;
        }

        private void OnDeleteClicked()
        {
            if (_selectedIndex < 0 || _selectedIndex >= slots.Count || slots[_selectedIndex].state == SlotState.Empty)
            {
                Desktop?.PlayCue(MenuCue.Error);
                return;
            }

            if (!_deleteArmed)
            {
                _deleteArmed = true; // first click only asks
                var l = deleteButton.GetComponentInChildren<TextMeshProUGUI>(true);
                if (l != null) l.text = "Sure?";
                return;
            }

            SaveManager.DeleteSlot(_selectedIndex + 1);
            slots = BuildSlotsFromSave();
            RebuildRows(); // resets selection and disarms
        }

        private void OnOpenClicked()
        {
            if (_selectedIndex < 0 || _selectedIndex >= slots.Count)
            {
                Desktop?.PlayCue(MenuCue.Error);
                return;
            }

            // Slot rows are in order, so the row index is the slot number - 1.
            int slotNumber = _selectedIndex + 1;

            if (_newGameMode)
            {
                bool occupied = slots[_selectedIndex].state != SlotState.Empty;
                if (occupied && !_overwriteArmed)
                {
                    _overwriteArmed = true; // overwriting a real save: ask once
                    SetOpenLabel("Overwrite?");
                    return;
                }
                _newGameMode = false;
                Hide();
                Desktop?.NewGameInSlot(slotNumber);
                return;
            }

            SaveManager.SelectSlot(slotNumber);
            Debug.Log($"MyReportsWindow: loading slot {slotNumber} ({slots[_selectedIndex].detail}).", this);

            Hide();
            Desktop?.StartShift();
        }

        protected override void OnHiding()
        {
            _newGameMode = false;
            _overwriteArmed = false;
            DisarmDelete();
            _selectedIndex = -1;
        }
    }
}
