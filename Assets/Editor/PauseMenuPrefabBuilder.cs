using Audio;
using GameLogic.Flow;
using TMPro;
using UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// One-time generator for Assets/Prefabs/Gameplay/PauseMenu.prefab (the ESC "System Menu.exe" window),
    /// plus a helper that drops it into the gameplay Canvas. After generating, edit the prefab by hand -
    /// re-running the generator overwrites it.
    /// </summary>
    public static class PauseMenuPrefabBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Gameplay/PauseMenu.prefab";
        private const string CanvasName = "Canvas";

        private const float WindowWidth = 360f, WindowHeight = 262f, WindowScale = 2f;
        private const float ConfirmWidth = 320f, ConfirmHeight = 140f;
        private const float TitlebarHeight = 26f, StatusBarHeight = 22f;
        private const float BodyPadding = 14f, SectionSpacing = 10f, GroupPadding = 12f;
        private const float ButtonHeight = 26f, ButtonSpacing = 8f, ButtonRowExtraTop = 4f;
        private const float CaptionSize = 21f, TitleIconSize = 16f, TitleFontSize = 12f;

        [MenuItem("Tools/Give Me A Sign/Build Pause Menu Prefab")]
        public static void Build()
        {
            var style = new XPControlStyle();
            var font = XPTheme.Load().ResolvedFont;

            var root = new GameObject("PauseMenu", typeof(RectTransform), typeof(PauseMenuController));
            XPControls.Stretch(root.GetComponent<RectTransform>());

            var window = NewRect("Window", root.transform);
            XPControls.Stretch(window);

            var dim = XPControls.Img(window, "DimOverlay", new Color(0f, 0f, 0f, 0.6f), r => XPControls.Stretch(r));
            dim.raycastTarget = true; // blocks clicks reaching the game behind the menu

            // ── main window ──
            var frame = BuildFrame(window, "PauseWindow", new Vector2(WindowWidth, WindowHeight), "System Menu.exe", true, style, font, out var panel, out var closeButton);

            float statusHeight = StatusBarHeight;
            var body = BuildBody(panel, style, statusHeight, TitlebarHeight);

            var header = XPControls.Text(body, "HeaderText", font, "Shift paused", style.headerSize, style.value, true, TextAlignmentOptions.MidlineLeft);
            XPControls.SetPreferred(header.gameObject, height: style.headerSize + 6f);

            var group = XPControls.CreateGroupBox(body, style, font, "Audio", GroupPadding, 6f);
            group.name = "AudioGroupBox";
            var audioPanel = AudioSettingsPanel.Build(group, new AudioSettingsPanel.BuildConfig(), style, font);

            var buttonRow = NewButtonRow(body, ButtonHeight + ButtonRowExtraTop, ButtonRowExtraTop);
            var returnButton = XPControls.CreateButton(buttonRow, style, font, "Return to main menu", 150f, ButtonHeight, false, null);
            var resumeButton = XPControls.CreateButton(buttonRow, style, font, "Resume", 86f, ButtonHeight, true, null);

            BuildStatusBar(panel, style, font);

            // ── confirm dialog ──
            var confirm = NewRect("ConfirmDialog", window);
            XPControls.Stretch(confirm);
            XPControls.Img(confirm, "Dim", new Color(0f, 0f, 0f, 0.35f), r => XPControls.Stretch(r)).raycastTarget = true;

            BuildFrame(confirm, "ConfirmWindow", new Vector2(ConfirmWidth, ConfirmHeight), "System Menu.exe", false, style, font, out var confirmPanel, out var confirmClose);
            var confirmBody = BuildBody(confirmPanel, style, 0f, TitlebarHeight);

            var message = XPControls.Text(confirmBody, "Message", font, "Return to the main menu? Today's shift progress will be lost.",
                style.labelSize, style.label, false, TextAlignmentOptions.TopLeft);
            message.enableWordWrapping = true;
            XPControls.SetPreferred(message.gameObject, height: ConfirmHeight - TitlebarHeight - ButtonHeight - BodyPadding * 2f - SectionSpacing);

            var confirmRow = NewButtonRow(confirmBody, ButtonHeight, 0f);
            var yesButton = XPControls.CreateButton(confirmRow, style, font, "Yes", 76f, ButtonHeight, false, null);
            var noButton = XPControls.CreateButton(confirmRow, style, font, "No", 76f, ButtonHeight, true, null);

            // ── wire the controller ──
            var controller = root.GetComponent<PauseMenuController>();
            var so = new SerializedObject(controller);
            so.FindProperty("windowRoot").objectReferenceValue = window.gameObject;
            so.FindProperty("audioPanel").objectReferenceValue = audioPanel;
            so.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            so.FindProperty("returnButton").objectReferenceValue = returnButton;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("confirmRoot").objectReferenceValue = confirm.gameObject;
            so.FindProperty("yesButton").objectReferenceValue = yesButton;
            so.FindProperty("noButton").objectReferenceValue = noButton;
            so.FindProperty("confirmCloseButton").objectReferenceValue = confirmClose;
            so.ApplyModifiedPropertiesWithoutUndo();

            window.gameObject.SetActive(false);
            confirm.gameObject.SetActive(false);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            Object.DestroyImmediate(root);

            if (success) Debug.Log($"PauseMenuPrefabBuilder: saved {PrefabPath}.", prefab);
            else Debug.LogError($"PauseMenuPrefabBuilder: could not save {PrefabPath}.");
        }

        [MenuItem("Tools/Give Me A Sign/Place Pause Menu In Scene")]
        public static void PlaceInScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("PauseMenuPrefabBuilder: build the prefab first."); return; }

            Canvas canvas = null;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.isRootCanvas && c.name == CanvasName) { canvas = c; break; }
            }
            if (canvas == null) { Debug.LogError($"PauseMenuPrefabBuilder: no root Canvas named '{CanvasName}' in the open scene."); return; }

            // Replace any older pause menu (the runtime-built one lived on a bare manager object).
            foreach (var old in Object.FindObjectsByType<PauseMenuController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = old.gameObject;
                if (PrefabUtility.IsPartOfPrefabInstance(go)) Undo.DestroyObjectImmediate(PrefabUtility.GetOutermostPrefabInstanceRoot(go));
                else if (go.GetComponents<Component>().Length <= 2) Undo.DestroyObjectImmediate(go); // Transform + controller only
                else Undo.DestroyObjectImmediate(old);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Place Pause Menu");
            instance.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(instance.scene);
            Debug.Log("PauseMenuPrefabBuilder: PauseMenu placed in the Canvas (last sibling, draws on top). Save the scene.", instance);
        }

        // ── pieces ───────────────────────────────────────────────────────────────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform BuildFrame(Transform parent, string name, Vector2 size, string title, bool captionButtons,
            XPControlStyle style, TMP_FontAsset font, out RectTransform panel, out Button closeButton)
        {
            var frame = XPControls.Img(parent, name, style.windowBorder, r =>
            {
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = size;
                r.anchoredPosition = Vector2.zero;
                r.localScale = Vector3.one * WindowScale;
            });
            frame.raycastTarget = true;

            var panelImage = XPControls.Img(frame.transform, "Panel", style.windowBody, r => XPControls.Stretch(r, 1f));
            panelImage.raycastTarget = true;
            panel = panelImage.rectTransform;

            var titlebar = XPControls.Img(panel, "Titlebar", style.titlebarTop, r =>
            {
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = Vector2.one;
                r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(0f, TitlebarHeight);
                r.anchoredPosition = Vector2.zero;
            });
            titlebar.gameObject.AddComponent<UIGradient>().SetColors(style.titlebarTop, style.titlebarBottom);

            var icon = XPControls.Img(titlebar.transform, "Icon", new Color(1f, 1f, 1f, 0.9f), r =>
            {
                r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0f, 0.5f);
                r.sizeDelta = new Vector2(TitleIconSize, TitleIconSize);
                r.anchoredPosition = new Vector2(6f, 0f);
            });
            XPControls.Img(icon.transform, "Screen", style.accent, r => XPControls.Stretch(r, 2f));

            var titleText = XPControls.Text(titlebar.transform, "Title", font, title, TitleFontSize, Color.white, true, TextAlignmentOptions.MidlineLeft);
            int captionCount = captionButtons ? 3 : 1;
            XPControls.Stretch(titleText.rectTransform);
            titleText.rectTransform.offsetMin = new Vector2(TitleIconSize + 12f, 0f);
            titleText.rectTransform.offsetMax = new Vector2(-(captionCount * (CaptionSize + 2f) + 4f), 0f);

            if (captionButtons)
            {
                float step = CaptionSize + 2f;
                BuildCaptionGlyph(titlebar.rectTransform, style, "Maximize", -4f - step, true);
                BuildCaptionGlyph(titlebar.rectTransform, style, "Minimize", -4f - 2f * step, false);
            }

            closeButton = BuildCloseButton(titlebar.rectTransform, style, font);
            return frame.rectTransform;
        }

        private static Button BuildCloseButton(RectTransform titlebar, XPControlStyle style, TMP_FontAsset font)
        {
            var image = XPControls.Img(titlebar, "CloseButton", style.closeButton, r =>
            {
                r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
                r.pivot = new Vector2(1f, 0.5f);
                r.sizeDelta = new Vector2(CaptionSize, CaptionSize);
                r.anchoredPosition = new Vector2(-4f, 0f);
            });
            image.raycastTarget = true;
            image.gameObject.AddComponent<UIGradient>().SetColors(Color.Lerp(style.closeButton, Color.white, 0.25f), style.closeButton);

            var label = XPControls.Text(image.transform, "X", font, "X", TitleFontSize, Color.white, true, TextAlignmentOptions.Center);
            XPControls.Stretch(label.rectTransform);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        // Decorative Min/Max: no Button, so they are inert.
        private static void BuildCaptionGlyph(RectTransform titlebar, XPControlStyle style, string name, float x, bool maximize)
        {
            Color face = Color.Lerp(style.titlebarTop, Color.white, 0.3f);
            var button = XPControls.Img(titlebar, name, new Color(1f, 1f, 1f, 0.8f), r =>
            {
                r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
                r.pivot = new Vector2(1f, 0.5f);
                r.sizeDelta = new Vector2(CaptionSize, CaptionSize);
                r.anchoredPosition = new Vector2(x, 0f);
            });
            XPControls.Img(button.transform, "Face", face, r => XPControls.Stretch(r, 1f));

            if (maximize)
            {
                var frame = XPControls.Img(button.transform, "Frame", Color.white, r =>
                {
                    r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                    r.sizeDelta = new Vector2(9f, 9f);
                });
                XPControls.Img(frame.transform, "Hole", face, r =>
                {
                    XPControls.Stretch(r, 1f);
                    r.offsetMax = new Vector2(-1f, -3f);
                });
            }
            else
            {
                XPControls.Img(button.transform, "Bar", Color.white, r =>
                {
                    r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                    r.sizeDelta = new Vector2(8f, 3f);
                    r.anchoredPosition = new Vector2(0f, -3f);
                });
            }
        }

        private static RectTransform BuildBody(RectTransform panel, XPControlStyle style, float bottomInset, float topInset)
        {
            var body = NewRect("Body", panel);
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(0f, bottomInset);
            body.offsetMax = new Vector2(0f, -topInset);

            var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            int pad = Mathf.RoundToInt(BodyPadding);
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = SectionSpacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return body;
        }

        private static RectTransform NewButtonRow(Transform parent, float height, float extraTop)
        {
            var row = NewRect("ButtonRow", parent);
            XPControls.SetPreferred(row.gameObject, height: height);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, Mathf.RoundToInt(extraTop), 0);
            layout.spacing = ButtonSpacing;
            layout.childAlignment = TextAnchor.LowerRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        private static void BuildStatusBar(RectTransform panel, XPControlStyle style, TMP_FontAsset font)
        {
            var bar = XPControls.Img(panel, "StatusBar", style.windowBody, r =>
            {
                r.anchorMin = Vector2.zero;
                r.anchorMax = new Vector2(1f, 0f);
                r.pivot = new Vector2(0.5f, 0f);
                r.sizeDelta = new Vector2(0f, StatusBarHeight);
                r.anchoredPosition = Vector2.zero;
            });

            XPControls.Img(bar.transform, "TopBorder", style.groupBorder, r =>
            {
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = Vector2.one;
                r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(0f, 1f);
            });

            var hint = XPControls.Text(bar.transform, "HintText", font, "Press Esc to resume", style.smallSize, style.label, false, TextAlignmentOptions.MidlineLeft);
            XPControls.Stretch(hint.rectTransform);
            hint.rectTransform.offsetMin = new Vector2(BodyPadding, 0f);

            var badge = XPControls.Img(bar.transform, "Badge", style.accent, r =>
            {
                r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f);
                r.pivot = new Vector2(1f, 0.5f);
                r.anchoredPosition = new Vector2(-BodyPadding, 0f);
            });
            var badgeText = XPControls.Text(badge.transform, "BadgeText", font, "PAUSED", style.smallSize, style.badgeText, true, TextAlignmentOptions.Center);
            XPControls.Stretch(badgeText.rectTransform);
            badgeText.ForceMeshUpdate();
            badge.rectTransform.sizeDelta = new Vector2(badgeText.preferredWidth + 14f, StatusBarHeight - 6f);
        }
    }
}
