using System.Collections.Generic;
using Report;
using TMPro;
using UIHelpers;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// Builds the Windows-XP-styled Incident Report window Canvas hierarchy in code and saves
    /// it as a prefab, wiring every serialized field on IncidentReportUI / IncidentReportManager.
    /// Run via Tools > Give Me A Sign > Build Incident Report Prefab.
    /// </summary>
    public static class IncidentReportPrefabBuilder
    {
        // ---- XP palette ----
        private static readonly Color TitleBarTop = HexColor("#1A5FB0");
        private static readonly Color TitleBarBottom = HexColor("#0A3C8A");
        private static readonly Color WindowBg = HexColor("#ECE9D8");
        private static readonly Color BorderColor = HexColor("#ACA899");
        private static readonly Color OuterBorder = HexColor("#003C74");
        private static readonly Color InputBg = HexColor("#FFFFFF");
        private static readonly Color ReadonlyBg = HexColor("#F5F5EE");
        private static readonly Color ButtonBg = HexColor("#DCDCD0");
        private static readonly Color ButtonBorder = HexColor("#003C74");
        private static readonly Color AccentBlue = HexColor("#316AC5");
        private static readonly Color ValueText = HexColor("#0A246A");
        private static readonly Color AlertRed = HexColor("#CC0000");
        private static readonly Color FieldBorder = HexColor("#7F9DB9");
        private static readonly Color CloseRed = HexColor("#CC2222");

        private static TMP_FontAsset _font;

        [MenuItem("Tools/Give Me A Sign/Build Incident Report Prefab")]
        public static void Build()
        {
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/tahoma SDF.asset");
            if (_font == null)
                Debug.LogWarning("IncidentReportPrefabBuilder: tahoma SDF.asset not found, falling back to default TMP font.");

            // ---------- Root ----------
            var root = NewUI("IncidentReportWindow", null, new Vector2(420, 480));
            var rootRT = root.GetComponent<RectTransform>();
            rootRT.anchorMin = rootRT.anchorMax = new Vector2(0.5f, 0.5f);
            rootRT.anchoredPosition = Vector2.zero;

            var rootImg = root.AddComponent<Image>();
            rootImg.color = WindowBg;
            var outline = root.AddComponent<Outline>();
            outline.effectColor = OuterBorder;
            outline.effectDistance = new Vector2(1, -1);

            var ui = root.AddComponent<IncidentReportUI>();
            var manager = root.AddComponent<IncidentReportManager>();

            // ---------- TitleBar ----------
            var titleBar = NewUI("TitleBar", root.transform, Vector2.zero);
            StretchTop(titleBar, 32);
            var titleBarImg = titleBar.AddComponent<Image>();
            titleBarImg.color = TitleBarTop; // flat approximation of the vertical gradient

            var titleIcon = NewUI("TitleIcon", titleBar.transform, new Vector2(18, 18));
            AnchorLeftMiddle(titleIcon, new Vector2(7, 0));
            var titleIconImg = titleIcon.AddComponent<Image>();
            titleIconImg.color = Color.white;

            var titleText = AddText(titleBar.transform, "TitleText", "Incident Report", 17, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
            var titleTextRT = titleText.rectTransform;
            titleTextRT.anchorMin = new Vector2(0, 0);
            titleTextRT.anchorMax = new Vector2(1, 1);
            titleTextRT.offsetMin = new Vector2(32, 0);
            titleTextRT.offsetMax = new Vector2(-104, 0);

            var windowButtons = NewUI("WindowButtons", titleBar.transform, new Vector2(90, 24));
            AnchorRightMiddle(windowButtons, new Vector2(-5, 0));
            var wbLayout = windowButtons.AddComponent<HorizontalLayoutGroup>();
            wbLayout.spacing = 3;
            wbLayout.childAlignment = TextAnchor.MiddleRight;
            wbLayout.childForceExpandWidth = false;
            wbLayout.childForceExpandHeight = false;

            var minBtn = CreateXpButton(windowButtons.transform, "MinBtn", "_", TitleBarTop, Color.white, new Vector2(26, 22), 15);
            var maxBtn = CreateXpButton(windowButtons.transform, "MaxBtn", "□", TitleBarTop, Color.white, new Vector2(26, 22), 15);
            var closeBtn = CreateXpButton(windowButtons.transform, "CloseBtn", "X", CloseRed, Color.white, new Vector2(26, 22), 15);

            // ---------- Content (contentRoot: hidden while minimized) ----------
            var content = NewUI("Content", root.transform, Vector2.zero);
            content.transform.SetSiblingIndex(1);
            var contentRT = content.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 0);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.offsetMin = Vector2.zero;
            contentRT.offsetMax = new Vector2(0, -32); // below titlebar
            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;

            // ---------- MenuBar ----------
            var menuBar = NewUI("MenuBar", content.transform, Vector2.zero);
            var menuBarLE = menuBar.AddComponent<LayoutElement>();
            menuBarLE.preferredHeight = 26;
            menuBarLE.flexibleHeight = 0;
            var menuBarImg = menuBar.AddComponent<Image>();
            menuBarImg.color = WindowBg;
            var menuBarOutline = menuBar.AddComponent<Outline>();
            menuBarOutline.effectColor = BorderColor;
            var menuBarLayout = menuBar.AddComponent<HorizontalLayoutGroup>();
            menuBarLayout.padding = new RectOffset(6, 6, 2, 2);
            menuBarLayout.spacing = 12;
            menuBarLayout.childAlignment = TextAnchor.MiddleLeft;
            menuBarLayout.childForceExpandWidth = false;
            menuBarLayout.childForceExpandHeight = true;
            menuBarLayout.childControlWidth = true;
            menuBarLayout.childControlHeight = false;

            // Fixed, small preferred widths - without this each item keeps AddText's 100px
            // default box, so 4 items + spacing/padding (~454px) overflows past the window's
            // right edge (this is what pushed "Help" outside the titlebar).
            foreach (var label in new[] { "File", "Edit", "View", "Help" })
            {
                var menuItem = AddText(menuBar.transform, label + "Item", label, 14, FontStyles.Normal, Color.black, TextAlignmentOptions.MidlineLeft);
                AddLayoutElement(menuItem.gameObject, 0, 38);
            }

            // ---------- Body ----------
            var body = NewUI("Body", content.transform, Vector2.zero);
            var bodyLE = body.AddComponent<LayoutElement>();
            bodyLE.flexibleHeight = 1;
            var bodyLayout = body.AddComponent<VerticalLayoutGroup>();
            bodyLayout.padding = new RectOffset(10, 10, 10, 10);
            bodyLayout.spacing = 8;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = false;
            bodyLayout.childControlHeight = true;
            bodyLayout.childControlWidth = true;

            // ---- InfoRow ----
            var infoRow = NewUI("InfoRow", body.transform, Vector2.zero);
            var infoRowLE = infoRow.AddComponent<LayoutElement>();
            infoRowLE.preferredHeight = 48;
            infoRowLE.flexibleHeight = 0;
            var infoRowLayout = infoRow.AddComponent<HorizontalLayoutGroup>();
            infoRowLayout.spacing = 6;
            infoRowLayout.childForceExpandWidth = true;
            infoRowLayout.childForceExpandHeight = true;

            var caseValue = CreateInfoBox(infoRow.transform, "CaseBox", "CASE NO.", "#0000", out var caseValueText);
            var timeValue = CreateInfoBox(infoRow.transform, "TimeBox", "SHIFT TIME", "00:00:00", out var timeValueText);
            var officerValue = CreateInfoBox(infoRow.transform, "OfficerBox", "OFFICER ID", "SEC-04", out var officerValueText);

            // ---- GroupBox ----
            var groupBox = NewUI("GroupBox", body.transform, Vector2.zero);
            var groupBoxLE = groupBox.AddComponent<LayoutElement>();
            groupBoxLE.flexibleHeight = 1;
            var groupBoxImg = groupBox.AddComponent<Image>();
            groupBoxImg.color = new Color(0, 0, 0, 0);
            var groupBoxOutline = groupBox.AddComponent<Outline>();
            groupBoxOutline.effectColor = BorderColor;
            var groupBoxLayout = groupBox.AddComponent<VerticalLayoutGroup>();
            groupBoxLayout.padding = new RectOffset(10, 10, 14, 10);
            groupBoxLayout.spacing = 6;
            groupBoxLayout.childForceExpandWidth = true;
            groupBoxLayout.childForceExpandHeight = false;
            groupBoxLayout.childControlHeight = true;
            groupBoxLayout.childControlWidth = true;

            // GroupBox title notch (overlaps top border)
            var groupTitlePanel = NewUI("GroupBoxTitle", groupBox.transform, new Vector2(115, 20));
            var groupTitleRT = groupTitlePanel.GetComponent<RectTransform>();
            groupTitleRT.anchorMin = groupTitleRT.anchorMax = new Vector2(0, 1);
            groupTitleRT.pivot = new Vector2(0, 0.5f);
            groupTitleRT.anchoredPosition = new Vector2(10, 0);
            var groupTitleImg = groupTitlePanel.AddComponent<Image>();
            groupTitleImg.color = WindowBg;
            var groupTitleText = AddText(groupTitlePanel.transform, "GroupBoxTitleText", "Incident details", 14, FontStyles.Normal, Color.black, TextAlignmentOptions.Center);
            StretchFull(groupTitleText.gameObject);
            var xpTitle = groupTitlePanel.AddComponent<XPGroupBoxTitle>();
            SetPrivateField(xpTitle, "titleText", groupTitleText);
            SetPrivateField(xpTitle, "backgroundPanel", groupTitleRT);

            var locationLabel = AddText(groupBox.transform, "LocationLabel", "Location (room):", 14, FontStyles.Normal, Color.black, TextAlignmentOptions.MidlineLeft);
            AddLayoutElement(locationLabel.gameObject, 22, 0);

            var roomDropdown = CreateTmpDropdown(groupBox.transform, "RoomDropdown");
            AddLayoutElement(roomDropdown.gameObject, 32, 0);

            var anomalyLabel = AddText(groupBox.transform, "AnomalyLabel", "Anomaly type (verbal report):", 14, FontStyles.Normal, Color.black, TextAlignmentOptions.MidlineLeft);
            AddLayoutElement(anomalyLabel.gameObject, 22, 0);

            // ---- PTTArea ----
            var pttArea = NewUI("PTTArea", groupBox.transform, Vector2.zero);
            var pttAreaLE = pttArea.AddComponent<LayoutElement>();
            pttAreaLE.flexibleHeight = 1;
            pttAreaLE.minHeight = 130;
            var pttAreaImg = pttArea.AddComponent<Image>();
            pttAreaImg.color = InputBg;
            var pttAreaOutline = pttArea.AddComponent<Outline>();
            pttAreaOutline.effectColor = FieldBorder;
            var pttAreaLayout = pttArea.AddComponent<VerticalLayoutGroup>();
            pttAreaLayout.padding = new RectOffset(8, 8, 8, 8);
            pttAreaLayout.spacing = 6;
            pttAreaLayout.childForceExpandWidth = true;
            pttAreaLayout.childForceExpandHeight = false;
            pttAreaLayout.childControlHeight = true;
            pttAreaLayout.childControlWidth = true;

            var pttButtonGo = CreateXpButton(pttArea.transform, "PTTButton", "Hold to Speak", ButtonBg, Color.black, new Vector2(0, 36), 15);
            AddLayoutElement(pttButtonGo, 36, 0);
            var pttHoldButton = pttButtonGo.AddComponent<PointerHoldButton>();
            var pttButtonImage = pttButtonGo.GetComponent<Image>();
            var pttButtonLabel = pttButtonGo.GetComponentInChildren<TextMeshProUGUI>();

            var recStatusRow = NewUI("RecStatusRow", pttArea.transform, Vector2.zero);
            AddLayoutElement(recStatusRow, 22, 0);
            var recStatusLayout = recStatusRow.AddComponent<HorizontalLayoutGroup>();
            recStatusLayout.spacing = 5;
            recStatusLayout.childAlignment = TextAnchor.MiddleLeft;
            recStatusLayout.childForceExpandWidth = false;
            recStatusLayout.childForceExpandHeight = true;

            var recDot = NewUI("RecDot", recStatusRow.transform, new Vector2(10, 10));
            AddLayoutElement(recDot, 10, 10, false);
            var recDotImg = recDot.AddComponent<Image>();
            recDotImg.color = AlertRed;

            var recLabel = AddText(recStatusRow.transform, "RecLabel", "Recording...", 13, FontStyles.Normal, AlertRed, TextAlignmentOptions.MidlineLeft);
            recStatusRow.SetActive(false);

            var recognizedLabel = AddText(pttArea.transform, "RecognizedLabel", "Recognized:", 12, FontStyles.Normal, Color.gray, TextAlignmentOptions.MidlineLeft);
            AddLayoutElement(recognizedLabel.gameObject, 18, 0);

            var recognizedField = CreateTmpInputField(pttArea.transform, "RecognizedField", ReadonlyBg, ValueText);
            AddLayoutElement(recognizedField.gameObject, 30, 0);
            recognizedField.readOnly = true;
            recognizedField.richText = false;

            // ---- ButtonRow ----
            var buttonRow = NewUI("ButtonRow", body.transform, Vector2.zero);
            AddLayoutElement(buttonRow, 38, 0);
            var buttonRowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonRowLayout.spacing = 10;
            buttonRowLayout.childAlignment = TextAnchor.MiddleRight;
            buttonRowLayout.childForceExpandWidth = false;
            buttonRowLayout.childForceExpandHeight = true;
            var buttonRowFiller = new GameObject("Filler", typeof(RectTransform), typeof(LayoutElement));
            buttonRowFiller.transform.SetParent(buttonRow.transform, false);
            buttonRowFiller.GetComponent<LayoutElement>().flexibleWidth = 1;

            var cancelBtn = CreateXpButton(buttonRow.transform, "CancelBtn", "Cancel", ButtonBg, Color.black, new Vector2(100, 34), 15);
            AddLayoutElement(cancelBtn, 34, 100, false);

            var submitBtn = CreateXpButton(buttonRow.transform, "SubmitBtn", "Submit report", ButtonBg, Color.black, new Vector2(140, 34), 15);
            AddLayoutElement(submitBtn, 34, 140, false);
            var submitOutline = submitBtn.AddComponent<Outline>();
            submitOutline.effectColor = AccentBlue;
            submitOutline.effectDistance = new Vector2(2, -2);

            // ---------- StatusBar ----------
            var statusBar = NewUI("StatusBar", content.transform, Vector2.zero);
            AddLayoutElement(statusBar, 28, 0);
            var statusBarImg = statusBar.AddComponent<Image>();
            statusBarImg.color = WindowBg;
            var statusBarOutline = statusBar.AddComponent<Outline>();
            statusBarOutline.effectColor = BorderColor;
            statusBarOutline.effectDistance = new Vector2(0, 1);
            var statusBarLayout = statusBar.AddComponent<HorizontalLayoutGroup>();
            statusBarLayout.padding = new RectOffset(6, 6, 2, 2);
            statusBarLayout.childForceExpandWidth = false;
            statusBarLayout.childForceExpandHeight = true;

            var statusText = AddText(statusBar.transform, "StatusText", "Awaiting report details.", 13, FontStyles.Normal, Color.black, TextAlignmentOptions.MidlineLeft);
            AddLayoutElement(statusText.gameObject, 0, 0, true, true);

            var statusBadge = NewUI("StatusBadge", statusBar.transform, new Vector2(96, 22));
            AddLayoutElement(statusBadge, 22, 96, false);
            var statusBadgeImg = statusBadge.AddComponent<Image>();
            statusBadgeImg.color = Color.gray;
            var statusBadgeText = AddText(statusBadge.transform, "StatusBadgeText", "STANDBY", 12, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            StretchFull(statusBadgeText.gameObject);

            // ---------- Wire IncidentReportUI ----------
            SetPrivateField(ui, "windowRoot", root);
            SetPrivateField(ui, "windowRect", rootRT);
            SetPrivateField(ui, "contentRoot", content);
            SetPrivateField(ui, "defaultSize", new Vector2(420, 480));
            SetPrivateField(ui, "maximizedSize", new Vector2(520, 620));
            SetPrivateField(ui, "minimizedHeight", 32f);

            SetPrivateField(ui, "titleText", titleText);
            SetPrivateField(ui, "minimizeButton", minBtn.GetComponent<Button>());
            SetPrivateField(ui, "maximizeButton", maxBtn.GetComponent<Button>());
            SetPrivateField(ui, "closeButton", closeBtn.GetComponent<Button>());

            SetPrivateField(ui, "caseValueText", caseValueText);
            SetPrivateField(ui, "timeValueText", timeValueText);
            SetPrivateField(ui, "officerValueText", officerValueText);

            SetPrivateField(ui, "locationDropdown", roomDropdown);

            SetPrivateField(ui, "pttHoldButton", pttHoldButton);
            SetPrivateField(ui, "pttButtonImage", pttButtonImage);
            SetPrivateField(ui, "pttButtonLabel", pttButtonLabel);
            SetPrivateField(ui, "recStatusRow", recStatusRow);
            SetPrivateField(ui, "recDotImage", recDotImg);

            SetPrivateField(ui, "recognizedField", recognizedField);

            SetPrivateField(ui, "cancelButton", cancelBtn.GetComponent<Button>());
            SetPrivateField(ui, "submitButton", submitBtn.GetComponent<Button>());

            SetPrivateField(ui, "statusText", statusText);
            SetPrivateField(ui, "statusBadgeImage", statusBadgeImg);
            SetPrivateField(ui, "statusBadgeText", statusBadgeText);

            // ---------- Wire IncidentReportManager ----------
            SetPrivateField(manager, "reportUI", ui);

            // ---------- Save prefab ----------
            const string folder = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            const string path = "Assets/Prefabs/IncidentReportWindow.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Object.DestroyImmediate(root);

            if (success)
            {
                Debug.Log($"IncidentReportPrefabBuilder: Saved prefab to {path}");
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            else
            {
                Debug.LogError("IncidentReportPrefabBuilder: Failed to save prefab.");
            }
        }

        /// <summary>
        /// Replaces whatever "IncidentReportWindow" GameObject currently sits under the scene's
        /// UI Canvas with a fresh instance of the up-to-date prefab. Useful after regenerating
        /// the prefab (e.g. bigger fonts) so the scene isn't left with a stale, disconnected copy.
        /// Does not save the scene - review in the Editor, then Ctrl+S yourself.
        /// </summary>
        [MenuItem("Tools/Give Me A Sign/Replace Incident Report In Scene")]
        public static void ReplaceInScene()
        {
            const string path = "Assets/Prefabs/IncidentReportWindow.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"IncidentReportPrefabBuilder: prefab not found at {path}. Run 'Build Incident Report Prefab' first.");
                return;
            }

            Transform canvas = null;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var found = FindDeep(root.transform, "Canvas");
                if (found != null) { canvas = found; break; }
            }

            if (canvas == null)
            {
                Debug.LogError("IncidentReportPrefabBuilder: couldn't find a 'Canvas' GameObject in the active scene.");
                return;
            }

            var old = canvas.Find("IncidentReportWindow");
            if (old != null)
            {
                Debug.Log("IncidentReportPrefabBuilder: removing existing 'IncidentReportWindow' from Canvas.");
                Object.DestroyImmediate(old.gameObject);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            instance.name = "IncidentReportWindow";
            var rt = instance.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            EditorGUIUtility.PingObject(instance);
            Selection.activeObject = instance;
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("IncidentReportPrefabBuilder: instantiated fresh IncidentReportWindow under Canvas. Scene has unsaved changes - review then Ctrl+S.");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // ================= Helpers =================

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        private static GameObject NewUI(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            if (parent != null) rt.SetParent(parent, false);
            rt.sizeDelta = size;
            return go;
        }

        private static void StretchFull(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void StretchTop(GameObject go, float height)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0, height);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void AnchorLeftMiddle(GameObject go, Vector2 offset)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = offset;
        }

        private static void AnchorRightMiddle(GameObject go, Vector2 offset)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = offset;
        }

        private static void AddLayoutElement(GameObject go, float preferredHeight, float preferredWidth, bool flexibleWidth = false, bool flexibleHeight2 = false)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (preferredHeight > 0) { le.preferredHeight = preferredHeight; le.flexibleHeight = 0; }
            if (preferredWidth > 0) { le.preferredWidth = preferredWidth; le.flexibleWidth = 0; }
            if (flexibleWidth) le.flexibleWidth = 1;
            if (flexibleHeight2) le.flexibleHeight = 1;
        }

        private static TextMeshProUGUI AddText(Transform parent, string name, string text, float size, FontStyles style, Color color, TextAlignmentOptions align)
        {
            var go = NewUI(name, parent, new Vector2(100, 20));
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            if (_font != null) tmp.font = _font;
            return tmp;
        }

        private static GameObject CreateXpButton(Transform parent, string name, string label, Color bg, Color textColor, Vector2 size, float fontSize = 14f)
        {
            var go = NewUI(name, parent, size);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = ButtonBorder;

            var txt = AddText(go.transform, name + "Label", label, fontSize, FontStyles.Bold, textColor, TextAlignmentOptions.Center);
            StretchFull(txt.gameObject);

            return go;
        }

        private static RectTransform CreateInfoBox(Transform parent, string name, string label, string defaultValue, out TextMeshProUGUI valueText)
        {
            var box = NewUI(name, parent, Vector2.zero);
            var img = box.AddComponent<Image>();
            img.color = InputBg;
            var outline = box.AddComponent<Outline>();
            outline.effectColor = FieldBorder;
            var layout = box.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 4, 4);
            layout.spacing = 2;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            AddText(box.transform, "Label", label, 11, FontStyles.Normal, Color.gray, TextAlignmentOptions.MidlineLeft);
            valueText = AddText(box.transform, "Value", defaultValue, 15, FontStyles.Bold, ValueText, TextAlignmentOptions.MidlineLeft);

            return box.GetComponent<RectTransform>();
        }

        private static TMP_Dropdown CreateTmpDropdown(Transform parent, string name)
        {
            var go = NewUI(name, parent, new Vector2(0, 32));
            var bgImg = go.AddComponent<Image>();
            bgImg.color = InputBg;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = FieldBorder;

            var dropdown = go.AddComponent<TMP_Dropdown>();

            var label = AddText(go.transform, "Label", "Hallway", 14, FontStyles.Normal, Color.black, TextAlignmentOptions.MidlineLeft);
            var labelRT = label.rectTransform;
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(10, 1);
            labelRT.offsetMax = new Vector2(-24, -1);

            var arrow = NewUI("Arrow", go.transform, new Vector2(14, 14));
            var arrowRT = arrow.GetComponent<RectTransform>();
            arrowRT.anchorMin = arrowRT.anchorMax = new Vector2(1, 0.5f);
            arrowRT.pivot = new Vector2(1, 0.5f);
            arrowRT.anchoredPosition = new Vector2(-7, 0);
            var arrowImg = arrow.AddComponent<Image>();
            arrowImg.color = ValueText;

            // Template (dropdown list, inactive)
            var template = NewUI("Template", go.transform, new Vector2(0, 120));
            var templateRT = template.GetComponent<RectTransform>();
            templateRT.anchorMin = new Vector2(0, 0);
            templateRT.anchorMax = new Vector2(1, 0);
            templateRT.pivot = new Vector2(0.5f, 1f);
            templateRT.anchoredPosition = new Vector2(0, 2);
            templateRT.sizeDelta = new Vector2(0, 120);
            var templateImg = template.AddComponent<Image>();
            templateImg.color = InputBg;
            template.AddComponent<Outline>().effectColor = FieldBorder;
            var scrollRect = template.AddComponent<ScrollRect>();
            template.SetActive(false);

            var viewport = NewUI("Viewport", template.transform, Vector2.zero);
            StretchFull(viewport);
            var viewportImg = viewport.AddComponent<Image>();
            viewportImg.color = Color.white;
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var vpContent = NewUI("Content", viewport.transform, new Vector2(0, 32));
            var vpContentRT = vpContent.GetComponent<RectTransform>();
            vpContentRT.anchorMin = new Vector2(0, 1);
            vpContentRT.anchorMax = new Vector2(1, 1);
            vpContentRT.pivot = new Vector2(0.5f, 1f);

            var item = NewUI("Item", vpContent.transform, new Vector2(0, 28));
            var itemRT = item.GetComponent<RectTransform>();
            itemRT.anchorMin = new Vector2(0, 0.5f);
            itemRT.anchorMax = new Vector2(1, 0.5f);
            var itemToggle = item.AddComponent<Toggle>();

            var itemBg = NewUI("Item Background", item.transform, Vector2.zero);
            StretchFull(itemBg);
            var itemBgImg = itemBg.AddComponent<Image>();
            itemBgImg.color = InputBg;

            var itemCheck = NewUI("Item Checkmark", item.transform, new Vector2(16, 16));
            var itemCheckRT = itemCheck.GetComponent<RectTransform>();
            itemCheckRT.anchorMin = itemCheckRT.anchorMax = new Vector2(0, 0.5f);
            itemCheckRT.anchoredPosition = new Vector2(11, 0);
            var itemCheckImg = itemCheck.AddComponent<Image>();
            itemCheckImg.color = AccentBlue;

            var itemLabel = AddText(item.transform, "Item Label", "Option", 14, FontStyles.Normal, Color.black, TextAlignmentOptions.MidlineLeft);
            var itemLabelRT = itemLabel.rectTransform;
            itemLabelRT.anchorMin = Vector2.zero;
            itemLabelRT.anchorMax = Vector2.one;
            itemLabelRT.offsetMin = new Vector2(24, 0);
            itemLabelRT.offsetMax = new Vector2(-4, 0);

            itemToggle.targetGraphic = itemBgImg;
            itemToggle.graphic = itemCheckImg;
            itemToggle.isOn = true;

            scrollRect.content = vpContentRT;
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            dropdown.captionText = label;
            dropdown.itemText = itemLabel;
            dropdown.template = templateRT;
            dropdown.targetGraphic = bgImg;

            dropdown.options = new List<TMP_Dropdown.OptionData>
            {
                new TMP_Dropdown.OptionData("Hallway"),
                new TMP_Dropdown.OptionData("Kitchen"),
                new TMP_Dropdown.OptionData("Bedroom"),
                new TMP_Dropdown.OptionData("Living room"),
                new TMP_Dropdown.OptionData("Basement"),
                new TMP_Dropdown.OptionData("Attic"),
            };

            return dropdown;
        }

        private static TMP_InputField CreateTmpInputField(Transform parent, string name, Color bg, Color textColor)
        {
            var go = NewUI(name, parent, new Vector2(0, 30));
            var img = go.AddComponent<Image>();
            img.color = bg;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = FieldBorder;

            var inputField = go.AddComponent<TMP_InputField>();

            var textArea = NewUI("Text Area", go.transform, Vector2.zero);
            StretchFull(textArea);
            var rectMask = textArea.AddComponent<RectMask2D>();

            var placeholder = AddText(textArea.transform, "Placeholder", "Recognized keyword will appear here...", 12, FontStyles.Italic, new Color(0.5f, 0.5f, 0.5f), TextAlignmentOptions.MidlineLeft);
            var placeholderRT = placeholder.rectTransform;
            placeholderRT.anchorMin = Vector2.zero;
            placeholderRT.anchorMax = Vector2.one;
            placeholderRT.offsetMin = new Vector2(7, 2);
            placeholderRT.offsetMax = new Vector2(-7, -2);

            var text = AddText(textArea.transform, "Text", "", 14, FontStyles.Normal, textColor, TextAlignmentOptions.MidlineLeft);
            var textRT = text.rectTransform;
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(6, 2);
            textRT.offsetMax = new Vector2(-6, -2);

            inputField.textViewport = textArea.GetComponent<RectTransform>();
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.targetGraphic = img;

            return inputField;
        }

        private static void SetPrivateField(Object target, string fieldName, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"IncidentReportPrefabBuilder: field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }

            switch (value)
            {
                case Object objVal:
                    prop.objectReferenceValue = objVal;
                    break;
                case Vector2 v2:
                    prop.vector2Value = v2;
                    break;
                case float f:
                    prop.floatValue = f;
                    break;
                default:
                    Debug.LogError($"IncidentReportPrefabBuilder: unsupported value type for '{fieldName}'");
                    break;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
