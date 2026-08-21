using System.Collections.Generic;
using GameLogic.Data;
using GameLogic.Save;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// Start &gt; Mail: an old-fashioned two-pane email client (inbox list left, reading pane
    /// right) showing every document the player has actually found so far (see MailPickup /
    /// SaveData.foundMailIds). A document appears the moment its pickup is clicked in the world,
    /// and stays forever after that - nothing here is consumed, only marked read
    /// (see SaveData.readEmailIds).
    /// </summary>
    public class MailWindow : XPWindowController
    {
        [Header("Data Source")]
        [Tooltip("Every document that can ever appear. Filtered down to what's actually been found (SaveData.foundMailIds).")]
        [SerializeField] private List<MailData> pool = new List<MailData>();

        [Header("Inbox List")]
        [Tooltip("Vertical container the rows are spawned into.")]
        [SerializeField] private RectTransform rowContainer;
        [Tooltip("Inactive template row (Button + Image background + TMP label) cloned per document.")]
        [SerializeField] private GameObject rowTemplate;
        [Tooltip("Shown instead of the list when nothing has been found yet.")]
        [SerializeField] private GameObject emptyInboxLabel;

        [Header("Row Colors")]
        [SerializeField] private Color rowNormalColor = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color rowSelectedColor = XPPalette.Hex("#316AC5", 0.35f);
        [SerializeField] private Color unreadTextColor = Color.black;
        [SerializeField] private Color readTextColor = XPPalette.Hex("#808080");
        [SerializeField] private Color selectedTextColor = Color.white;

        [Header("Reading Pane")]
        [SerializeField] private GameObject readingPaneRoot;
        [SerializeField] private TextMeshProUGUI subjectText;
        [SerializeField] private TextMeshProUGUI senderText;
        [SerializeField] private TextMeshProUGUI dateText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private Image attachmentImage;
        [SerializeField] private GameObject attachmentRoot;
        [Tooltip("Shown in the reading pane before any document is selected.")]
        [SerializeField] private GameObject noSelectionLabel;

        [Header("Buttons")]
        [Tooltip("Optional footer Close button, in addition to the titlebar's X.")]
        [SerializeField] private Button cancelButton;

        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly List<MailData> _visible = new List<MailData>();
        private int _selectedIndex = -1;

        protected override void Awake()
        {
            base.Awake();

            if (rowTemplate != null)
                rowTemplate.SetActive(false);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(Hide);
        }

        protected override void OnShown()
        {
            RebuildRows();
            ShowSelected(-1);
        }

        /// <summary>Rebuilds the visible list from the pool, newest document first.</summary>
        private void RebuildRows()
        {
            if (rowContainer == null || rowTemplate == null)
            {
                Debug.LogWarning("MailWindow: rowContainer / rowTemplate not assigned.", this);
                return;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] == null) continue;
                _rows[i].transform.SetParent(null, false);
                Destroy(_rows[i]);
            }
            _rows.Clear();
            _visible.Clear();

            var save = SaveManager.Current;

            foreach (var mail in pool)
            {
                if (mail == null || !mail.IsPlayable()) continue;
                if (!save.IsMailFound(mail.emailId)) continue;
                _visible.Add(mail);
            }

            // Newest first, matching how an inbox usually reads.
            _visible.Sort((a, b) => b.unlockDay.CompareTo(a.unlockDay));

            for (int i = 0; i < _visible.Count; i++)
            {
                var mail = _visible[i];

                var row = Instantiate(rowTemplate, rowContainer);
                row.name = "Row_" + mail.emailId;
                row.SetActive(true);
                _rows.Add(row);

                var rowLabel = row.GetComponentInChildren<TextMeshProUGUI>(true);
                if (rowLabel != null)
                    rowLabel.text = $"{mail.subject} — {mail.sender}";

                var rowButton = row.GetComponent<Button>();
                if (rowButton != null)
                {
                    int index = i;
                    rowButton.onClick.RemoveAllListeners();
                    rowButton.onClick.AddListener(() => OnRowClicked(index));
                }
            }

            if (emptyInboxLabel != null)
                emptyInboxLabel.SetActive(_visible.Count == 0);

            RefreshRowVisuals(save);
        }

        private void OnRowClicked(int index)
        {
            if (index < 0 || index >= _visible.Count) return;

            var mail = _visible[index];
            SaveManager.Current.MarkEmailRead(mail.emailId);
            SaveManager.Save();

            ShowSelected(index);
        }

        private void ShowSelected(int index)
        {
            _selectedIndex = index;
            RefreshRowVisuals(SaveManager.Current);

            bool hasSelection = index >= 0 && index < _visible.Count;

            if (noSelectionLabel != null) noSelectionLabel.SetActive(!hasSelection);
            if (readingPaneRoot != null) readingPaneRoot.SetActive(hasSelection);

            if (!hasSelection) return;

            var mail = _visible[index];

            if (subjectText != null) subjectText.text = mail.subject;
            if (senderText != null) senderText.text = mail.sender;
            if (dateText != null) dateText.text = mail.DateLabel;
            if (bodyText != null) bodyText.text = mail.body;

            bool hasAttachment = mail.attachment != null;
            if (attachmentRoot != null) attachmentRoot.SetActive(hasAttachment);
            if (attachmentImage != null) attachmentImage.sprite = mail.attachment;
        }

        private void RefreshRowVisuals(SaveData save)
        {
            for (int i = 0; i < _rows.Count && i < _visible.Count; i++)
            {
                bool selected = i == _selectedIndex;
                bool read = save != null && save.IsEmailRead(_visible[i].emailId);

                var bg = _rows[i].GetComponent<Image>();
                if (bg != null)
                    bg.color = selected ? rowSelectedColor : rowNormalColor;

                var rowLabel = _rows[i].GetComponentInChildren<TextMeshProUGUI>(true);
                if (rowLabel != null)
                {
                    rowLabel.color = selected ? selectedTextColor : (read ? readTextColor : unreadTextColor);
                    rowLabel.fontStyle = read ? FontStyles.Normal : FontStyles.Bold;
                }
            }
        }

        protected override void OnHiding()
        {
            _selectedIndex = -1;
        }
    }
}
