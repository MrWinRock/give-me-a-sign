using System;
using System.Collections.Generic;
using System.Reflection;
using Gaskellgames;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Whisper;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace GameLogic.DebugTools
{
    /// <summary>
    /// One key opens a scrollable panel listing every debug action in the currently loaded
    /// scene - every <see cref="ContextMenu"/> and Gaskellgames <see cref="ButtonAttribute"/>
    /// method on any MonoBehaviour, found by reflection instead of hand-wired one by one, plus a
    /// permanent typed-input box for driving voice-gated systems without a working microphone.
    ///
    /// Built with uGUI, not OnGUI: TypedInputFallback.forceEnabled left on in a scene was
    /// recently traced to a severe framerate hit, because OnGUI reruns on every MouseMove event
    /// on top of everything else. This panel is retained-mode Canvas UI - it costs nothing while
    /// closed and nothing extra while open beyond ordinary UI (no per-frame relayout).
    ///
    /// Drop this on any GameObject in a scene, or don't - Instance auto-creates one that
    /// survives scene loads, exactly like GameFlowManager.
    /// </summary>
    public class DebugMasterPanel : MonoBehaviour
    {
        /// <summary>Small curated set for the Inspector dropdown - not a raw KeyCode enum, so it stays readable.</summary>
        private enum ToggleKey { F12, F10, F9, BackQuote }

        [Header("Toggle")]
        [Tooltip("Key that opens/closes the panel. F12 by default - unused elsewhere in this project.")]
        [SerializeField] private ToggleKey toggleKey = ToggleKey.F12;

        [Tooltip("Survive scene loads, so the panel (and its toggle key) works from any scene without re-adding it.")]
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("Typed Input")]
        [InfoBox("Types into the same recognized-text queue WhisperMicInput feeds from actual speech - every voice-gated system (prayer, Incident Report, Radio Check, ...) reads it the same way either way.")]
        [Tooltip("Auto-found in the scene if left empty.")]
        [SerializeField] private WhisperMicInput whisperMicInput;

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new Vector2(560f, 760f);
        [Tooltip("Must sit above every other runtime overlay (Day Event Player, Death Sequence, ...) so the panel is always reachable.")]
        [SerializeField] private int canvasSortOrder = 1200;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private static DebugMasterPanel _instance;

        public static DebugMasterPanel Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (!Application.isPlaying) return null;

                _instance = FindFirstObjectByType<DebugMasterPanel>();
                if (_instance == null)
                {
                    var host = new GameObject("DebugMasterPanel (auto-created)");
                    _instance = host.AddComponent<DebugMasterPanel>();
                }
                return _instance;
            }
        }

        private class DiscoveredAction
        {
            public string ownerLabel;
            public string actionLabel;
            public MonoBehaviour target;
            public MethodInfo method;
        }

        private readonly List<DiscoveredAction> _actions = new List<DiscoveredAction>();

        private GameObject _root;
        private RectTransform _listContent;
        private TMP_InputField _typedInputField;
        private TextMeshProUGUI _statusText;
        private bool _isOpen;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(persistAcrossScenes ? gameObject : (UnityEngine.Object)this);
                return;
            }

            _instance = this;

            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Start()
        {
            if (whisperMicInput == null)
                whisperMicInput = FindFirstObjectByType<WhisperMicInput>();
        }

        void Update()
        {
            if (WasTogglePressed())
                Toggle();
        }

        // Guarded the same way every other hotkey in this project is: the new Input System is
        // the ONLY handler here (see CLAUDE.md), where legacy UnityEngine.Input throws at runtime.
        private bool WasTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;

            switch (toggleKey)
            {
                case ToggleKey.F12: return keyboard.f12Key.wasPressedThisFrame;
                case ToggleKey.F10: return keyboard.f10Key.wasPressedThisFrame;
                case ToggleKey.F9: return keyboard.f9Key.wasPressedThisFrame;
                case ToggleKey.BackQuote: return keyboard.backquoteKey.wasPressedThisFrame;
                default: return false;
            }
#else
            KeyCode kc;
            switch (toggleKey)
            {
                case ToggleKey.F12: kc = KeyCode.F12; break;
                case ToggleKey.F10: kc = KeyCode.F10; break;
                case ToggleKey.F9: kc = KeyCode.F9; break;
                case ToggleKey.BackQuote: kc = KeyCode.BackQuote; break;
                default: kc = KeyCode.F12; break;
            }
            return Input.GetKeyDown(kc);
#endif
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_root == null) BuildUi();

            _root.SetActive(true);
            _isOpen = true;
            RefreshActions();

            if (showDebugInfo) Debug.Log("DebugMasterPanel: opened.", this);
        }

        public void Close()
        {
            if (_root != null) _root.SetActive(false);
            _isOpen = false;
        }

        // ── Discovery ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Rescans every loaded MonoBehaviour for debug-callable methods. Called on open and by
        /// the panel's own Refresh button, so anything spawned after the panel first opened
        /// (a runtime-spawned anomaly, a newly created manager, ...) still shows up.
        /// </summary>
        public void RefreshActions()
        {
            _actions.Clear();

            var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var mb in all)
            {
                if (mb == null || mb == this) continue;

                var type = mb.GetType();
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (var method in methods)
                {
                    if (method.GetParameters().Length > 0) continue; // debug actions take no arguments

                    var contextMenuAttr = method.GetCustomAttribute<ContextMenu>();
                    if (contextMenuAttr != null)
                    {
                        _actions.Add(new DiscoveredAction
                        {
                            ownerLabel = $"{mb.gameObject.name}  ({type.Name})",
                            actionLabel = contextMenuAttr.menuItem,
                            target = mb,
                            method = method,
                        });
                        continue;
                    }

                    var buttonAttr = method.GetCustomAttribute<ButtonAttribute>();
                    if (buttonAttr != null)
                    {
                        _actions.Add(new DiscoveredAction
                        {
                            ownerLabel = $"{mb.gameObject.name}  ({type.Name})",
                            actionLabel = Nicify(method.Name),
                            target = mb,
                            method = method,
                        });
                    }
                }
            }

            _actions.Sort((a, b) =>
            {
                int byOwner = string.Compare(a.ownerLabel, b.ownerLabel, StringComparison.Ordinal);
                return byOwner != 0 ? byOwner : string.Compare(a.actionLabel, b.actionLabel, StringComparison.Ordinal);
            });

            RebuildActionList();

            if (showDebugInfo)
                Debug.Log($"DebugMasterPanel: found {_actions.Count} debug action(s).", this);
        }

        private void InvokeAction(DiscoveredAction action)
        {
            if (action.target == null)
            {
                SetStatus($"'{action.actionLabel}' is gone (its GameObject was destroyed) - refreshing.");
                RefreshActions();
                return;
            }

            try
            {
                action.method.Invoke(action.target, null);
                SetStatus($"Ran '{action.actionLabel}' on {action.ownerLabel}.");
            }
            catch (Exception e)
            {
                SetStatus($"'{action.actionLabel}' threw: {e.InnerException?.Message ?? e.Message}");
                Debug.LogException(e, action.target);
            }
        }

        /// <summary>"StartAnimation" -> "Start Animation" - Gaskellgames [Button] methods have no
        /// label of their own (unlike [ContextMenu("...")]), so the method name is all there is.</summary>
        private static string Nicify(string methodName)
        {
            var sb = new System.Text.StringBuilder(methodName.Length + 8);
            for (int i = 0; i < methodName.Length; i++)
            {
                char c = methodName[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(methodName[i - 1]))
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }

        private void SetStatus(string message)
        {
            if (_statusText != null) _statusText.text = message;
            if (showDebugInfo) Debug.Log($"DebugMasterPanel: {message}", this);
        }

        // ── Typed input ──────────────────────────────────────────────────────────────────

        private void SubmitTypedText()
        {
            if (_typedInputField == null) return;

            string text = _typedInputField.text;
            if (string.IsNullOrWhiteSpace(text)) return;

            if (whisperMicInput == null)
                whisperMicInput = FindFirstObjectByType<WhisperMicInput>();

            if (whisperMicInput == null)
            {
                SetStatus("No WhisperMicInput found in the scene - nothing to send the text to.");
                return;
            }

            whisperMicInput.EnqueueTypedText(text.Trim());
            SetStatus($"Sent: \"{text.Trim()}\"");

            _typedInputField.text = "";
            _typedInputField.ActivateInputField();
        }

        // ── UI build ─────────────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            _root = new GameObject("DebugMasterPanelCanvas", typeof(RectTransform));
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortOrder;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            _root.AddComponent<GraphicRaycaster>();

            // Full-screen dim backdrop: catches clicks so they don't leak through to gameplay
            // (an anomaly behind the panel, a button underneath, ...) while the panel is open.
            var backdrop = CreateImage(_root.transform, "Backdrop", new Color(0f, 0f, 0f, 0.55f));
            var backdropRect = backdrop.rectTransform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;

            var panel = CreateImage(_root.transform, "Panel", new Color(0.06f, 0.08f, 0.07f, 0.97f));
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.sizeDelta = panelSize;
            panelRect.anchoredPosition = new Vector2(-24f, 0f);

            // topOffset is a POSITIVE distance down from the panel's top edge; each Build method
            // returns the next free offset, so callers just chain them downward.
            float topOffset = 16f;
            topOffset = BuildTitleBar(panelRect, topOffset);
            topOffset = BuildTypedInputSection(panelRect, topOffset);
            topOffset = BuildStatusLine(panelRect, topOffset);
            BuildActionScrollView(panelRect, topOffset);

            _root.SetActive(false);
        }

        private float BuildTitleBar(RectTransform parent, float topOffset)
        {
            var title = CreateText(parent, "Title", $"DEBUG PANEL  —  press {toggleKey} to close",
                18f, TextAlignmentOptions.MidlineLeft, new Color(0.6f, 1f, 0.6f));
            SetTopStretch(title.rectTransform, topOffset, 26f, left: 16f, right: 92f);

            var closeButton = CreateButton(parent, "CloseButton", "✕", Close);
            var closeRect = (RectTransform)closeButton.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(64f, 26f);
            closeRect.anchoredPosition = new Vector2(-12f, -topOffset);

            var refreshButton = CreateButton(parent, "RefreshButton", "↻ Refresh", RefreshActions);
            var refreshRect = (RectTransform)refreshButton.transform;
            refreshRect.anchorMin = new Vector2(1f, 1f);
            refreshRect.anchorMax = new Vector2(1f, 1f);
            refreshRect.pivot = new Vector2(1f, 1f);
            refreshRect.sizeDelta = new Vector2(96f, 26f);
            refreshRect.anchoredPosition = new Vector2(-84f, -topOffset);

            return topOffset + 26f + 10f;
        }

        private float BuildTypedInputSection(RectTransform parent, float topOffset)
        {
            var label = CreateText(parent, "TypedInputLabel", "Type instead of speaking (Enter = Send):",
                14f, TextAlignmentOptions.MidlineLeft, new Color(0.85f, 0.85f, 0.85f));
            SetTopStretch(label.rectTransform, topOffset, 20f, left: 16f, right: 16f);
            topOffset += 20f + 4f;

            var fieldGo = new GameObject("TypedInputField", typeof(RectTransform));
            fieldGo.transform.SetParent(parent, false);
            var fieldRect = (RectTransform)fieldGo.transform;
            SetTopStretch(fieldRect, topOffset, 34f, left: 16f, right: 96f);

            var fieldImage = fieldGo.AddComponent<Image>();
            fieldImage.color = new Color(1f, 1f, 1f, 0.08f);

            _typedInputField = fieldGo.AddComponent<TMP_InputField>();
            _typedInputField.targetGraphic = fieldImage;
            _typedInputField.lineType = TMP_InputField.LineType.SingleLine;

            var textArea = new GameObject("TextArea", typeof(RectTransform));
            textArea.transform.SetParent(fieldGo.transform, false);
            var textAreaRect = (RectTransform)textArea.transform;
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(8f, 4f);
            textAreaRect.offsetMax = new Vector2(-8f, -4f);
            textArea.AddComponent<RectMask2D>();

            var placeholder = CreateText(textAreaRect, "Placeholder", "Type your message here...",
                16f, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.35f));
            SetFullStretch(placeholder.rectTransform);

            var inputText = CreateText(textAreaRect, "Text", "",
                16f, TextAlignmentOptions.MidlineLeft, Color.white);
            SetFullStretch(inputText.rectTransform);

            _typedInputField.textViewport = textAreaRect;
            _typedInputField.textComponent = inputText;
            _typedInputField.placeholder = placeholder;
            _typedInputField.onSubmit.AddListener(_ => SubmitTypedText());

            var sendButton = CreateButton(parent, "SendButton", "Send", SubmitTypedText);
            var sendRect = (RectTransform)sendButton.transform;
            sendRect.anchorMin = new Vector2(1f, 1f);
            sendRect.anchorMax = new Vector2(1f, 1f);
            sendRect.pivot = new Vector2(1f, 1f);
            sendRect.sizeDelta = new Vector2(72f, 34f);
            sendRect.anchoredPosition = new Vector2(-16f, -topOffset);

            return topOffset + 34f + 10f;
        }

        private float BuildStatusLine(RectTransform parent, float topOffset)
        {
            _statusText = CreateText(parent, "StatusLine", "Ready.",
                12f, TextAlignmentOptions.MidlineLeft, new Color(0.6f, 0.75f, 1f));
            SetTopStretch(_statusText.rectTransform, topOffset, 18f, left: 16f, right: 16f);

            var divider = CreateImage(parent, "Divider", new Color(1f, 1f, 1f, 0.12f));
            SetTopStretch(divider.rectTransform, topOffset + 18f + 6f, 1f, left: 16f, right: 16f);

            return topOffset + 18f + 6f + 10f;
        }

        private void BuildActionScrollView(RectTransform parent, float topOffset)
        {
            var scrollGo = new GameObject("ActionScrollView", typeof(RectTransform));
            scrollGo.transform.SetParent(parent, false);
            var scrollRectTransform = (RectTransform)scrollGo.transform;
            scrollRectTransform.anchorMin = new Vector2(0f, 0f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.offsetMin = new Vector2(16f, 16f);
            scrollRectTransform.offsetMax = new Vector2(-16f, -topOffset);

            var scrollImage = scrollGo.AddComponent<Image>();
            scrollImage.color = new Color(1f, 1f, 1f, 0.03f);

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 24f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRect = (RectTransform)viewportGo.transform;
            SetFullStretch(viewportRect);
            viewportGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _listContent = (RectTransform)contentGo.transform;
            _listContent.anchorMin = new Vector2(0f, 1f);
            _listContent.anchorMax = new Vector2(1f, 1f);
            _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = Vector2.zero;

            // The actual bug behind the "text is getting eaten" report: a fresh RectTransform's
            // sizeDelta defaults to (100, 100), and on a horizontally-stretched rect that default
            // is a DELTA added on top of the parent-driven width - left unset, this content was
            // 100px wider than the viewport, centered, so every row it contains (via
            // childControlWidth below) overhung ~50px past the viewport's left AND right edges,
            // and the Viewport's RectMask2D clipped that overhang. Zeroing sizeDelta.x makes the
            // stretch exact. (Same trap SetTopStretch's doc comment warns about - just missed here.)
            _listContent.sizeDelta = new Vector2(0f, _listContent.sizeDelta.y);

            var layoutGroup = contentGo.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding = new RectOffset(4, 4, 4, 4);
            layoutGroup.spacing = 4f;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childControlHeight = true;
            layoutGroup.childControlWidth = true;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = _listContent;
        }

        /// <summary>Clears and repopulates the scroll content from <see cref="_actions"/>, one
        /// header per owning GameObject and one button per action underneath it.</summary>
        private void RebuildActionList()
        {
            if (_listContent == null) return;

            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            if (_actions.Count == 0)
            {
                var empty = CreateText(_listContent, "Empty", "No debug actions found in the loaded scene(s).",
                    14f, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.5f));
                SetLayoutHeight(empty.gameObject, 24f);
                return;
            }

            string lastOwner = null;
            foreach (var action in _actions)
            {
                if (action.ownerLabel != lastOwner)
                {
                    lastOwner = action.ownerLabel;
                    var header = CreateText(_listContent, "OwnerHeader", lastOwner,
                        13f, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.85f, 0.4f));
                    header.fontStyle = FontStyles.Bold;
                    SetLayoutHeight(header.gameObject, 22f);
                }

                var captured = action; // local copy for the closure
                var button = CreateButton(_listContent, "ActionButton", captured.actionLabel,
                    () => InvokeAction(captured));
                SetLayoutHeight(button.gameObject, 30f);
            }

            // Without this, every row's TMP component measures itself against whatever width its
            // RectTransform happened to have the instant it was created (0, most of the time,
            // since the VerticalLayoutGroup hasn't run a pass yet) - the fix above (no wrap,
            // auto-size) already keeps that from eating characters, but forcing the rebuild here
            // means the list is laid out correctly on the very first frame it's visible instead
            // of visibly snapping into place a frame later.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
        }

        /// <summary>
        /// Sets a fixed row height via LayoutElement rather than RectTransform.sizeDelta -
        /// VerticalLayoutGroup's childControlHeight recomputes sizeDelta from each child's
        /// reported preferred size every layout pass, so a direct sizeDelta write here would
        /// just get overwritten on the next pass. LayoutElement.preferredHeight is the value the
        /// layout group actually reads.
        /// </summary>
        private static void SetLayoutHeight(GameObject go, float height)
        {
            var layoutElement = go.GetComponent<LayoutElement>();
            if (layoutElement == null) layoutElement = go.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;
        }

        // ── Small UI factory helpers (same pattern as DayEventPlayer/DeathSequenceHud) ─────

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;

            return image;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text,
            float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;

            // Off, matching CameraFeedHud's convention: everything in this panel is meant to be
            // a single line. With wrapping ON, a row built inside the VerticalLayoutGroup wraps
            // against whatever width its RectTransform happens to have at creation time (often
            // 0/stale, since the layout pass hasn't run yet) - the result reads as characters
            // being "eaten", not a clean wrap. Off + Overflow means text is always fully drawn,
            // worst case spilling past its row rather than ever disappearing.
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;

            // A hard floor so a very long label (a long GameObject/component name, a nested
            // "Glitch/..." menu path, ...) shrinks to fit its row instead of spilling into the
            // next one - the row heights below are tuned for one line, not two.
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = Mathf.Min(9f, fontSize);
            tmp.fontSizeMax = fontSize;

            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string label, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.1f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.25f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.4f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            var text = CreateText(go.transform, "Label", label, 13f, TextAlignmentOptions.MidlineLeft, Color.white);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);  // a little breathing room off the left edge
            textRect.offsetMax = new Vector2(-10f, 0f); // and the right, so text never touches the button border

            return button;
        }

        /// <summary>
        /// Pins a rect to a top-anchored horizontal stretch using ONLY offsetMin/offsetMax -
        /// mixing those with sizeDelta/anchoredPosition on a stretched axis is a classic source
        /// of silently-wrong layouts in Unity, since both pairs describe the same underlying
        /// rect and whichever is assigned last wins in ways that aren't obvious from the call site.
        ///
        /// <paramref name="topOffset"/> is a POSITIVE distance down from the parent's top edge
        /// (not the negative-anchoredPosition convention DeathSequenceHud-style code sometimes
        /// uses), so callers just add heights as they stack elements downward.
        /// </summary>
        private static void SetTopStretch(RectTransform rect, float topOffset, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(topOffset + height));
            rect.offsetMax = new Vector2(-right, -topOffset);
        }

        private static void SetFullStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
