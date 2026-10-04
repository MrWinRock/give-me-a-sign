using System.Collections.Generic;
using GameLogic.Data;
using GameLogic.Night;
using GameLogic.Save;
using MainMenu;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameLogic
{
    /// <summary>
    /// Sprint 3, S-306/307. The "Field Manual.exe" window: reads <see cref="AnomalyDefinition"/>
    /// assets (the same data Incident Report validation reads - no second keyword list anywhere)
    /// and shows each one's photo, how to spot it, and the words that report it. Built on the same
    /// <see cref="XPWindowController"/> base as MailWindow/TextContentWindow, so it plugs into
    /// DesktopManager on the MainMenu the same way; in the gameplay scene nothing binds a Desktop
    /// and Show()/Hide() work standalone (see FieldManualController for the TAB-key entry point).
    /// </summary>
    public class FieldManualUI : XPWindowController
    {
        [Header("Access")]
        [Tooltip("If false, this window can only be opened from the Desktop/MainMenu - gameplay entry points (TAB) are disabled.")]
        [SerializeField] private bool allowOpenDuringShift = true;
        public bool AllowOpenDuringShift => allowOpenDuringShift;

        [Header("Data")]
        [Tooltip("Left empty, auto-fills from NightContentLibrary (the same pool the night generator draws from) the first time this opens.")]
        [SerializeField] private List<AnomalyDefinition> entries = new List<AnomalyDefinition>();

        [Tooltip("On = a page stays '???' until the player has seen that anomaly appear (SaveData.seenAnomalyIds).")]
        [SerializeField] private bool useLockedEntries;

        [Header("Sidebar")]
        [SerializeField] private RectTransform sidebarContainer;
        [SerializeField] private FieldManualEntryButton entryButtonPrefab;

        [Header("Detail Panel")]
        [SerializeField] private Image previewImage;
        [Tooltip("The anomaly's name in the detail pane - NOT the titlebar (that's XPWindowController's own titleText).")]
        [SerializeField] private TextMeshProUGUI detailTitleText;
        [Tooltip("Whole 'REPORT AS: X' line - the keyword itself is bolded in code (rich text), not a second field.")]
        [SerializeField] private TextMeshProUGUI reportAsText;
        [SerializeField] private TextMeshProUGUI spotBodyText;
        [SerializeField] private TextMeshProUGUI keywordsText;

        [Header("Status Bar (optional)")]
        [SerializeField] private bool showStatusBar;
        [SerializeField] private GameObject statusBarRoot;
        [SerializeField] private TextMeshProUGUI statusBarText;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private readonly List<FieldManualEntryButton> _rows = new List<FieldManualEntryButton>();
        private int _selectedIndex = -1; // remembered for the life of the session, not persisted to disk

        protected override void Awake()
        {
            base.Awake();

            if (statusBarRoot != null)
                statusBarRoot.SetActive(showStatusBar);

            // No fake scanline overlay here: this window lives on a Screen Space - Camera canvas
            // (see === UI === > Canvas), which is rendered THROUGH Main Camera and therefore
            // already gets the scene's real CCTV/VHS post-processing (Global Volume) for free -
            // unlike a Screen Space - Overlay canvas, which would bypass it entirely. Adding our
            // own scanline RawImage on top double-applied the effect and only ever showed up in
            // Play mode (Awake() never runs in the Editor's Prefab/edit view), which is why the
            // window looked fine until you pressed Play.
        }

        protected override void OnShown()
        {
            EnsureEntries();
            EnsureRows();
            RefreshLocks();
            RefreshStatusBar();

            int jump = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, entries.Count - 1));
            Select(entries.Count > 0 ? jump : -1);
        }

        /// <summary>Opens the manual scrolled/selected to a specific entry - the "player clicked an
        /// anomaly" jump-to path.</summary>
        public void Open(AnomalyDefinition jumpTo)
        {
            Show();

            if (jumpTo == null) return;
            int index = entries.IndexOf(jumpTo);
            if (index >= 0) Select(index);
        }

        public void Open() => Show();

        public void Close() => Hide();

        public void ToggleOpen()
        {
            if (IsOpen) Hide();
            else Show();
        }

        void Update()
        {
            if (!IsOpen) return;

            bool up, down;
#if ENABLE_INPUT_SYSTEM
            up = Keyboard.current != null && Keyboard.current.upArrowKey.wasPressedThisFrame;
            down = Keyboard.current != null && Keyboard.current.downArrowKey.wasPressedThisFrame;
#else
            up = Input.GetKeyDown(KeyCode.UpArrow);
            down = Input.GetKeyDown(KeyCode.DownArrow);
#endif
            if (up) Select(_selectedIndex - 1);
            else if (down) Select(_selectedIndex + 1);
        }

        // The manual is reference material issued up front (a staff briefing), not something
        // authored per-scene - falling back to the same pool the night generator draws from means
        // one asset list to maintain, not two.
        private void EnsureEntries()
        {
            if (entries.Count > 0) return;

            var library = NightContentLibrary.Load();
            if (library == null) return;

            foreach (var def in library.anomalies)
            {
                if (def != null) entries.Add(def);
            }
            entries.Sort((a, b) => a.minNightIndex.CompareTo(b.minNightIndex));
        }

        private void EnsureRows()
        {
            if (_rows.Count == entries.Count) return;

            if (sidebarContainer == null || entryButtonPrefab == null)
            {
                Debug.LogWarning("FieldManualUI: sidebarContainer / entryButtonPrefab not assigned.", this);
                return;
            }

            foreach (var row in _rows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            _rows.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                var row = Instantiate(entryButtonPrefab, sidebarContainer);
                row.gameObject.SetActive(true);
                int captured = i;
                row.Setup(entries[i], _ => Select(captured));
                _rows.Add(row);
            }

            // VerticalLayoutGroup/ContentSizeFitter don't reliably get their first pass the same
            // frame a window is SetActive(true) and populated at once - without this, every row
            // sits stacked at (0,0) with the content collapsed to zero height until something else
            // happens to dirty the layout (resize, another window, etc).
            LayoutRebuilder.ForceRebuildLayoutImmediate(sidebarContainer);
        }

        // Discovery can change between two openings, so labels are re-derived every time the window opens.
        private void RefreshLocks()
        {
            for (int i = 0; i < _rows.Count && i < entries.Count; i++)
                _rows[i].SetLocked(IsLocked(entries[i]));
        }

        private bool IsLocked(AnomalyDefinition def) => useLockedEntries && !IsDiscovered(def);

        private void Select(int index)
        {
            if (entries.Count == 0) { _selectedIndex = -1; return; }

            index = Mathf.Clamp(index, 0, entries.Count - 1);
            _selectedIndex = index;

            for (int i = 0; i < _rows.Count; i++)
                _rows[i].SetSelected(i == index);

            ShowDetail(entries[index]);
        }

        private void ShowDetail(AnomalyDefinition def)
        {
            if (def == null) return;

            bool locked = IsLocked(def);

            if (detailTitleText != null) detailTitleText.text = locked ? "???" : def.Label;

            if (previewImage != null)
            {
                bool hasImage = !locked && def.manualImage != null;
                previewImage.enabled = hasImage;
                if (hasImage) previewImage.sprite = def.manualImage;
            }

            var vocabulary = ObservationVocabulary.Load();
            string category = vocabulary != null ? vocabulary.LabelFor(def.observation) : def.observation.ToString();

            if (reportAsText != null)
                reportAsText.text = locked ? "REPORT AS: ???" : $"REPORT AS: <b><color=#0A246A>{category}</color></b> + ROOM";

            if (spotBodyText != null)
                spotBodyText.text = locked ? "Not yet encountered." :
                    (!string.IsNullOrWhiteSpace(def.howToSpot) ? def.howToSpot : "(not yet documented)");

            if (keywordsText != null)
                keywordsText.text = locked ? "" : FormatKeywords(def, vocabulary);
        }

        // Single source of truth: the same words Incident Report validation accepts
        // (ObservationVocabulary.SamplePhrases + AnomalyDefinition.correctKeywords), never a
        // second hand-authored list.
        private static string FormatKeywords(AnomalyDefinition def, ObservationVocabulary vocabulary)
        {
            var words = new List<string>(vocabulary != null ? vocabulary.SamplePhrases(def.observation) : System.Array.Empty<string>());

            if (def.correctKeywords != null)
            {
                foreach (var extra in def.correctKeywords)
                {
                    if (!string.IsNullOrWhiteSpace(extra)) words.Add(extra);
                }
            }

            string volume = def.voiceResponse switch
            {
                VoiceResponse.Whisper => "\n<b>Whisper it.</b> Speak quietly.",
                VoiceResponse.Shout => "\n<b>Shout it.</b> Speak loudly.",
                VoiceResponse.Silence => "\n<b>Whisper only.</b> The mic goes live - anything louder finds you.",
                _ => ""
            };

            return (words.Count > 0 ? $"\"{string.Join("\", \"", words)}\"" : "(no words configured)") + volume;
        }

        private static bool IsDiscovered(AnomalyDefinition def) =>
            def != null && SaveManager.Current.IsAnomalySeen(def.anomalyId);

        private void RefreshStatusBar()
        {
            if (!showStatusBar || statusBarText == null) return;
            statusBarText.text = $"{entries.Count} entries";
        }
    }
}
