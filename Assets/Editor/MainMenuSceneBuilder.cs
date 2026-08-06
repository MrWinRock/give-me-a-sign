using System.Collections.Generic;
using MainMenu;
using TMPro;
using UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// Builds the fake Windows XP desktop that stands in for the main menu: the Canvas hierarchy
    /// in the ACTIVE scene, plus one prefab per dialog window under Assets/Prefabs/MainMenu.
    /// Every serialized reference is wired here, so nothing has to be dragged by hand.
    ///
    /// Run: Tools > Give Me A Sign > Build Main Menu Scene (with MainMenu.unity open).
    /// Re-running is safe - it deletes the roots it created last time and rebuilds them.
    /// The scene is left DIRTY on purpose: review it, then Ctrl+S yourself.
    /// </summary>
    public static partial class MainMenuSceneBuilder
    {
        private const string CanvasRootName = "MainMenuCanvas";
        private const string SystemsRootName = "MainMenuSystems";
        private const string WindowPrefabFolder = "Assets/Prefabs/MainMenu";

        // ---- palette (defaults; every one of these is also a serialized Color on the components) ----
        private static readonly Color DesktopTop = Hex("#1A2838");
        private static readonly Color DesktopMid = Hex("#24384C");
        private static readonly Color DesktopBottom = Hex("#1E3326");
        private static readonly Color TaskbarTop = Hex("#3C81F3");
        private static readonly Color TaskbarBottom = Hex("#1E5ECC");
        private static readonly Color StartTop = Hex("#5EAC56");
        private static readonly Color StartBottom = Hex("#2D7D28");
        private static readonly Color TrayBlue = Hex("#146AB8");
        private static readonly Color TitlebarTop = Hex("#2B6EDE");
        private static readonly Color TitlebarBottom = Hex("#1854BE");
        private static readonly Color HeaderAccent = Hex("#FF9D3C");
        private static readonly Color MenuHover = Hex("#316AC5");
        private static readonly Color MenuSubtitleHover = Hex("#CFE0FF");
        private static readonly Color Divider = Hex("#D4D0C8");
        private static readonly Color FooterTop = Hex("#4A8CE8");
        private static readonly Color FooterBottom = Hex("#2B6EDE");
        private static readonly Color WindowBorder = Hex("#003C74");
        private static readonly Color WindowFace = Hex("#ECE9D8");
        private static readonly Color ButtonFace = Hex("#ECE9D8");
        private static readonly Color FieldBorder = Hex("#7F9DB9");
        private static readonly Color CloseTop = Hex("#E87A7A");
        private static readonly Color CloseBottom = Hex("#B02020");
        private static readonly Color SubtitleGrey = Hex("#808080");
        private static readonly Color BootGreen = Hex("#4A9A4A");

        [MenuItem("Tools/Give Me A Sign/Build Main Menu Scene")]
        public static void BuildScene()
        {
            var scene = SceneManager.GetActiveScene();

            RemoveExistingRoot(CanvasRootName);
            RemoveExistingRoot(SystemsRootName);
            EnsureEventSystem();

            // ---------- Canvas ----------
            var canvasGo = new GameObject(CanvasRootName, typeof(RectTransform), typeof(Canvas),
                                          typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // 1024x768 - the canonical XP-era screen. Every size in this builder is authored in
            // real XP pixels (30px taskbar, 275px dialogs, Tahoma 10-13), so referencing XP's
            // native resolution makes the whole desktop land at authentic proportions on any
            // modern screen (~1.6x bigger than the old 1920x1080 reference).
            scaler.referenceResolution = new Vector2(1024, 768);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Sibling order IS the z-order: desktop, icons, windows, taskbar, start menu, overlay.
            BuildDesktop(canvasGo.transform, out var desktopImage, out var desktopGradient, out var desktopButton);
            var icons = BuildIcons(canvasGo.transform);
            var windowLayer = BuildWindowLayer(canvasGo.transform);
            var taskbar = BuildTaskbar(canvasGo.transform, out var startButton, out var startButtonGradient,
                                       out var startButtonLabel);
            var startMenu = BuildStartMenu(canvasGo.transform, out var userNameText,
                                           out var menuItems, out var footerItems);
            var bootOverlay = BuildBootOverlay(canvasGo.transform, out var overlayBackground, out var overlayText);

            // ---------- Window prefabs ----------
            var turnOffPrefab = BuildTurnOffWindowPrefab();
            var controlPanelPrefab = BuildControlPanelWindowPrefab();
            var myReportsPrefab = BuildMyReportsWindowPrefab();
            var notepadPrefab = BuildNotepadWindowPrefab();
            var recycleBinPrefab = BuildRecycleBinWindowPrefab();
            var logOffPrefab = BuildLogOffWindowPrefab();
            var helpPrefab = BuildHelpWindowPrefab();

            // ---------- Systems ----------
            var systemsGo = new GameObject(SystemsRootName);
            var audioSource = systemsGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;

            var shutdown = systemsGo.AddComponent<ShutdownSequence>();
            Set(shutdown, "overlayRoot", bootOverlay);
            Set(shutdown, "overlayBackground", overlayBackground);
            Set(shutdown, "overlayText", overlayText);

            var startMenuController = taskbar.AddComponent<StartMenuController>();
            Set(startMenuController, "menuRoot", startMenu);
            Set(startMenuController, "userNameText", userNameText);
            Set(startMenuController, "startButton", startButton);
            Set(startMenuController, "startButtonGradient", startButtonGradient);
            Set(startMenuController, "startButtonLabel", startButtonLabel);
            SetArray(startMenuController, "items", menuItems);
            SetArray(startMenuController, "footerItems", footerItems);

            var desktopManager = systemsGo.AddComponent<DesktopManager>();
            Set(desktopManager, "desktopBackground", desktopImage);
            Set(desktopManager, "desktopGradient", desktopGradient);
            Set(desktopManager, "desktopClickCatcher", desktopButton);
            SetArray(desktopManager, "icons", icons);
            Set(desktopManager, "startMenu", startMenuController);
            Set(desktopManager, "windowLayer", windowLayer);
            Set(desktopManager, "turnOffWindowPrefab", turnOffPrefab);
            Set(desktopManager, "controlPanelWindowPrefab", controlPanelPrefab);
            Set(desktopManager, "myReportsWindowPrefab", myReportsPrefab);
            Set(desktopManager, "notepadWindowPrefab", notepadPrefab);
            Set(desktopManager, "recycleBinWindowPrefab", recycleBinPrefab);
            Set(desktopManager, "logOffWindowPrefab", logOffPrefab);
            Set(desktopManager, "helpWindowPrefab", helpPrefab);
            Set(desktopManager, "shutdownSequence", shutdown);
            Set(desktopManager, "uiAudioSource", audioSource);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = canvasGo;

            WarnIfSceneNotInBuildSettings(scene);
            Debug.Log("MainMenuSceneBuilder: desktop built. Window prefabs are in " + WindowPrefabFolder +
                      ". Scene has unsaved changes - review, then Ctrl+S.");
        }

        // =======================================================================================
        // Desktop / icons / taskbar / start menu / overlay
        // =======================================================================================

        private static GameObject BuildDesktop(Transform parent, out Image image, out UIGradient gradient,
                                               out Button clickCatcher)
        {
            var go = NewUI("Desktop", parent);
            Stretch(go);

            image = AddImage(go, Color.white);
            gradient = AddGradient(go, DesktopTop, DesktopBottom);
            gradient.UseMidColor = true;
            gradient.MidColor = DesktopMid;

            clickCatcher = go.AddComponent<Button>();
            clickCatcher.transition = Selectable.Transition.None;
            clickCatcher.targetGraphic = image;

            return go;
        }

        private static List<Object> BuildIcons(Transform parent)
        {
            var container = NewUI("DesktopIcons", parent, new Vector2(76, 0));
            var rt = container.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(12, -12);
            AddVertical(container, new RectOffset(0, 0, 0, 0), 8);
            FitVertical(container);

            var icons = new List<Object>
            {
                BuildIcon(container.transform, "Icon_StartShift", "Start Shift", DesktopAction.StartShift),
                BuildIcon(container.transform, "Icon_ReadMe", "READ ME.txt", DesktopAction.OpenNotepad),
                BuildIcon(container.transform, "Icon_RecycleBin", "Recycle Bin", DesktopAction.OpenRecycleBin),
            };
            return icons;
        }

        private static DesktopIcon BuildIcon(Transform parent, string name, string label, DesktopAction action)
        {
            var go = NewUI(name, parent, new Vector2(76, 68));
            Layout(go, 68, 76);

            var selection = AddImage(go, new Color(0, 0, 0, 0));
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = selection;

            // Dotted focus rectangle: four tiled 1px edges, hidden until the icon is selected.
            var outline = NewUI("SelectionOutline", go.transform);
            Stretch(outline);
            BuildDottedEdge(outline.transform, "Top", true, true);
            BuildDottedEdge(outline.transform, "Bottom", true, false);
            BuildDottedEdge(outline.transform, "Left", false, true);
            BuildDottedEdge(outline.transform, "Right", false, false);
            outline.SetActive(false);

            var iconImage = NewUI("IconImage", go.transform, new Vector2(32, 32));
            Anchor(iconImage, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(32, 32));
            var iconImageComponent = AddImage(iconImage, Color.white);
            iconImageComponent.raycastTarget = false;

            // Shadow first so it draws BEHIND the white label.
            var shadow = AddText(go.transform, "LabelShadow", label, 10, FontStyles.Normal, Color.black,
                                 TextAlignmentOptions.Top, wrap: true);
            Anchor(shadow.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(1, -41),
                   new Vector2(72, 24));

            var text = AddText(go.transform, "Label", label, 10, FontStyles.Normal, Color.white,
                               TextAlignmentOptions.Top, wrap: true);
            Anchor(text.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40),
                   new Vector2(72, 24));

            var icon = go.AddComponent<DesktopIcon>();
            Set(icon, "label", label);
            Set(icon, "action", action);
            Set(icon, "button", button);
            Set(icon, "iconImage", iconImageComponent);
            Set(icon, "labelText", text);
            Set(icon, "labelShadowText", shadow);
            Set(icon, "selectionBackground", selection);
            Set(icon, "selectionOutline", outline);

            return icon;
        }

        private static void BuildDottedEdge(Transform parent, string name, bool horizontal, bool firstSide)
        {
            var go = NewUI(name, parent);
            var rt = go.GetComponent<RectTransform>();

            if (horizontal)
            {
                rt.anchorMin = new Vector2(0, firstSide ? 1 : 0);
                rt.anchorMax = new Vector2(1, firstSide ? 1 : 0);
                rt.pivot = new Vector2(0.5f, firstSide ? 1 : 0);
                rt.sizeDelta = new Vector2(0, 1);
            }
            else
            {
                rt.anchorMin = new Vector2(firstSide ? 0 : 1, 0);
                rt.anchorMax = new Vector2(firstSide ? 0 : 1, 1);
                rt.pivot = new Vector2(firstSide ? 0 : 1, 0.5f);
                rt.sizeDelta = new Vector2(1, 0);
            }

            rt.anchoredPosition = Vector2.zero;
            var image = AddImage(go, Color.white, DotSprite(horizontal), Image.Type.Tiled);
            image.raycastTarget = false;
        }

        private static RectTransform BuildWindowLayer(Transform parent)
        {
            var go = NewUI("WindowLayer", parent);
            Stretch(go);
            return go.GetComponent<RectTransform>();
        }

        private static GameObject BuildTaskbar(Transform parent, out Button startButton,
                                               out UIGradient startGradient, out RectTransform startLabel)
        {
            var go = NewUI("Taskbar", parent);
            StretchBottom(go, 30);
            AddImage(go, Color.white);
            AddGradient(go, TaskbarTop, TaskbarBottom);

            // 1px light sheen along the top edge - the XP taskbar's signature highlight.
            var topSheen = NewUI("TopHighlight", go.transform);
            StretchTop(topSheen, 1);
            AddImage(topSheen, Hex("#7FB2F8")).raycastTarget = false;

            // ---- start button: rounded on the RIGHT only (corner mask 2 | 4) ----
            var startGo = NewUI("StartButton", go.transform, new Vector2(68, 26));
            Anchor(startGo, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(68, 26));
            var startImage = AddImage(startGo, Color.white, RoundedSprite(8, 2 | 4), Image.Type.Sliced);
            startGradient = AddGradient(startGo, StartTop, StartBottom);
            startButton = startGo.AddComponent<Button>();
            startButton.transition = Selectable.Transition.None;
            startButton.targetGraphic = startImage;

            var startText = AddText(startGo.transform, "StartLabel", "start", 13,
                                    FontStyles.Bold | FontStyles.Italic, Color.white, TextAlignmentOptions.Center);
            Anchor(startText.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                   new Vector2(60, 20));
            startLabel = startText.rectTransform;

            // ---- system tray ----
            var tray = NewUI("SystemTray", go.transform, new Vector2(100, 26));
            Anchor(tray, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-2, 0), new Vector2(100, 26));
            AddImage(tray, TrayBlue);

            var speaker = NewUI("SpeakerIcon", tray.transform, new Vector2(12, 12));
            Anchor(speaker, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(7, 0), new Vector2(12, 12));
            AddImage(speaker, Color.white).raycastTarget = false;

            var mic = NewUI("MicrophoneIcon", tray.transform, new Vector2(12, 12));
            Anchor(mic, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(12, 12));
            AddImage(mic, Color.white).raycastTarget = false;

            var clockText = AddText(tray.transform, "ClockText", "2:34 AM", 11, FontStyles.Normal, Color.white,
                                    TextAlignmentOptions.MidlineRight);
            Anchor(clockText.gameObject, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-7, 0),
                   new Vector2(54, 16));

            var clock = tray.AddComponent<TaskbarClock>();
            Set(clock, "clockText", clockText);

            return go;
        }

        private static GameObject BuildStartMenu(Transform parent, out TextMeshProUGUI userNameText,
                                                 out List<Object> items, out List<Object> footerItems)
        {
            var go = NewUI("StartMenu", parent, new Vector2(230, 200));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(0, 30); // sits on top of the 30px taskbar
            AddImage(go, Color.white, RoundedSprite(4, 1 | 2), Image.Type.Sliced);
            go.AddComponent<Outline>().effectColor = WindowBorder;
            AddVertical(go, new RectOffset(0, 0, 0, 0), 0);
            FitVertical(go);

            // ---- header ----
            var header = NewUI("Header", go.transform, new Vector2(230, 34));
            Layout(header, 34);
            AddImage(header, Color.white);
            AddGradient(header, TitlebarTop, TitlebarBottom);

            var avatar = NewUI("Avatar", header.transform, new Vector2(26, 26));
            Anchor(avatar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(5, 0), new Vector2(26, 26));
            AddImage(avatar, Color.white).raycastTarget = false;

            userNameText = AddText(header.transform, "UserName", "SEC-04", 12, FontStyles.Bold, Color.white,
                                   TextAlignmentOptions.MidlineLeft);
            Anchor(userNameText.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(38, 0),
                   new Vector2(150, 18));

            var accent = NewUI("AccentBorder", header.transform);
            StretchBottom(accent, 2);
            AddImage(accent, HeaderAccent).raycastTarget = false;

            // ---- items ----
            var itemsGo = NewUI("Items", go.transform, new Vector2(230, 0));
            AddImage(itemsGo, Color.white);
            AddVertical(itemsGo, new RectOffset(0, 0, 4, 4), 0);

            items = new List<Object>
            {
                BuildMenuItem(itemsGo.transform, "Item_StartShift", "Start Shift", "Begin monitoring",
                              DesktopAction.StartShift, 34),
                BuildMenuItem(itemsGo.transform, "Item_MyReports", "My Reports", "Continue / load",
                              DesktopAction.OpenMyReports, 34),
            };

            var divider = NewUI("Divider", itemsGo.transform, new Vector2(0, 1));
            Layout(divider, 1);
            AddImage(divider, Divider).raycastTarget = false;

            items.Add(BuildMenuItem(itemsGo.transform, "Item_ControlPanel", "Control Panel", "Settings",
                                    DesktopAction.OpenControlPanel, 34));
            items.Add(BuildMenuItem(itemsGo.transform, "Item_Help", "Help and Support", "",
                                    DesktopAction.OpenHelp, 24));

            // ---- footer ----
            var footer = NewUI("Footer", go.transform, new Vector2(230, 30));
            Layout(footer, 30);
            AddImage(footer, Color.white);
            AddGradient(footer, FooterTop, FooterBottom);
            AddHorizontal(footer, new RectOffset(0, 8, 3, 3), 4, TextAnchor.MiddleRight);

            footerItems = new List<Object>
            {
                BuildFooterItem(footer.transform, "Item_LogOff", "Log Off", DesktopAction.LogOff, 62),
                BuildFooterItem(footer.transform, "Item_TurnOff", "Turn Off Computer",
                                DesktopAction.TurnOffComputer, 112),
            };

            go.SetActive(false);
            return go;
        }

        private static XPMenuItem BuildMenuItem(Transform parent, string name, string label, string subtitle,
                                                DesktopAction action, float height)
        {
            var go = NewUI(name, parent, new Vector2(230, height));
            Layout(go, height);

            var background = AddImage(go, new Color(1, 1, 1, 0));
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = background;

            bool hasSubtitle = !string.IsNullOrEmpty(subtitle);

            var labelText = AddText(go.transform, "Label", label, 11, FontStyles.Normal, Color.black,
                                    TextAlignmentOptions.MidlineLeft);
            Anchor(labelText.gameObject, new Vector2(0, 1), new Vector2(0, 1),
                   new Vector2(12, hasSubtitle ? -5 : -(height - 14) * 0.5f), new Vector2(206, 14));

            TextMeshProUGUI subtitleText = null;
            if (hasSubtitle)
            {
                subtitleText = AddText(go.transform, "Subtitle", subtitle, 9, FontStyles.Normal, SubtitleGrey,
                                       TextAlignmentOptions.MidlineLeft);
                Anchor(subtitleText.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -19),
                       new Vector2(206, 12));
            }

            var item = go.AddComponent<XPMenuItem>();
            Set(item, "labelString", label);
            Set(item, "subtitleString", subtitle);
            Set(item, "action", action);
            Set(item, "button", button);
            Set(item, "background", background);
            Set(item, "label", labelText);
            Set(item, "subtitle", subtitleText);
            Set(item, "normalBackground", new Color(1, 1, 1, 0));
            Set(item, "hoverBackground", MenuHover);
            Set(item, "normalLabelColor", Color.black);
            Set(item, "hoverLabelColor", Color.white);
            Set(item, "normalSubtitleColor", SubtitleGrey);
            Set(item, "hoverSubtitleColor", MenuSubtitleHover);

            return item;
        }

        private static XPMenuItem BuildFooterItem(Transform parent, string name, string label,
                                                  DesktopAction action, float width)
        {
            var go = NewUI(name, parent, new Vector2(width, 22));
            Layout(go, 22, width);

            var background = AddImage(go, new Color(1, 1, 1, 0), RoundedSprite(3), Image.Type.Sliced);
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = background;

            var labelText = AddText(go.transform, "Label", label, 10, FontStyles.Normal, Color.white,
                                    TextAlignmentOptions.Center);
            Stretch(labelText.gameObject);

            var item = go.AddComponent<XPMenuItem>();
            Set(item, "labelString", label);
            Set(item, "subtitleString", "");
            Set(item, "action", action);
            Set(item, "button", button);
            Set(item, "background", background);
            Set(item, "label", labelText);
            Set(item, "subtitle", null);
            Set(item, "normalBackground", new Color(1, 1, 1, 0));
            Set(item, "hoverBackground", new Color(1, 1, 1, 0.2f));
            Set(item, "normalLabelColor", Color.white);
            Set(item, "hoverLabelColor", Color.white);

            return item;
        }

        private static GameObject BuildBootOverlay(Transform parent, out Image background, out TextMeshProUGUI text)
        {
            var go = NewUI("BootOverlay", parent);
            Stretch(go);
            background = AddImage(go, Color.black);

            text = AddText(go.transform, "OverlayText", "", 12, FontStyles.Normal, BootGreen,
                           TextAlignmentOptions.Center, wrap: true);
            Anchor(text.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                   new Vector2(600, 300));
            text.lineSpacing = 40f;

            go.SetActive(false);
            return go;
        }

        // =======================================================================================
        // Window prefabs
        // =======================================================================================

        /// <summary>Titlebar + close button + empty body. Returns the root; body comes back via out.</summary>
        private static GameObject BuildWindowShell(string name, string title, float width, out GameObject body,
                                                   out TextMeshProUGUI titleText, out Button closeButton)
        {
            var root = NewUI(name, null, new Vector2(width, 120));
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            AddImage(root, WindowFace, RoundedSprite(4), Image.Type.Sliced);
            root.AddComponent<Outline>().effectColor = WindowBorder;
            // Shadow AFTER Outline so the drop shadow silhouettes the bordered window, not the
            // other way round (mesh effects run in component order).
            var dropShadow = root.AddComponent<Shadow>();
            dropShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            dropShadow.effectDistance = new Vector2(3, -3);
            AddVertical(root, new RectOffset(0, 0, 0, 0), 0);
            FitVertical(root);

            // Rounded on TOP only, matching the window's 4px radius - XP titlebars curve into
            // the frame at the top and sit flush against the body below.
            var titleBar = NewUI("TitleBar", root.transform, new Vector2(width, 24));
            Layout(titleBar, 24);
            AddImage(titleBar, Color.white, RoundedSprite(4, 1 | 2), Image.Type.Sliced);
            AddGradient(titleBar, TitlebarTop, TitlebarBottom);
            titleBar.AddComponent<XPWindowDrag>();

            titleText = AddText(titleBar.transform, "TitleText", title, 11, FontStyles.Bold, Color.white,
                                TextAlignmentOptions.MidlineLeft);
            var titleRt = titleText.rectTransform;
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = new Vector2(8, 0);
            titleRt.offsetMax = new Vector2(-26, 0);

            var closeGo = NewUI("CloseButton", titleBar.transform, new Vector2(18, 16));
            Anchor(closeGo, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-3, 0), new Vector2(18, 16));
            var closeImage = AddImage(closeGo, Color.white, RoundedSprite(2), Image.Type.Sliced);
            AddGradient(closeGo, CloseTop, CloseBottom);
            closeButton = closeGo.AddComponent<Button>();
            closeButton.transition = Selectable.Transition.None;
            closeButton.targetGraphic = closeImage;
            var closeLabel = AddText(closeGo.transform, "Label", "x", 10, FontStyles.Bold, Color.white,
                                     TextAlignmentOptions.Center);
            Stretch(closeLabel.gameObject);

            body = NewUI("Body", root.transform, new Vector2(width, 0));
            AddVertical(body, new RectOffset(14, 14, 14, 14), 8);

            return root;
        }

        private static void ApplyWindowBase(XPWindowController window, GameObject root, string title, float width,
                                            TextMeshProUGUI titleText, Button closeButton)
        {
            Set(window, "windowRoot", root);
            Set(window, "windowRect", root.GetComponent<RectTransform>());
            Set(window, "windowWidth", width);
            Set(window, "titleText", titleText);
            Set(window, "windowTitle", title);
            Set(window, "closeButton", closeButton);

            // Titlebar drag handle (added by BuildWindowShell) - wire it to this window.
            var drag = root.GetComponentInChildren<XPWindowDrag>(true);
            if (drag != null)
            {
                Set(drag, "window", window);
                Set(drag, "windowRect", root.GetComponent<RectTransform>());
            }
        }

        private static XPWindowController SavePrefab(GameObject root, string fileName)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(WindowPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs", "MainMenu");

            string path = $"{WindowPrefabFolder}/{fileName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Object.DestroyImmediate(root);

            if (!success)
            {
                Debug.LogError($"MainMenuSceneBuilder: failed to save prefab '{path}'.");
                return null;
            }

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return asset != null ? asset.GetComponent<XPWindowController>() : null;
        }

        // ---- 1. Turn off computer ----
        private static XPWindowController BuildTurnOffWindowPrefab()
        {
            const float width = 275f;
            const string title = "Turn off computer";

            var root = BuildWindowShell("TurnOffWindow", title, width, out var body, out var titleText,
                                        out var closeButton);
            var window = root.AddComponent<TurnOffWindow>();
            ApplyWindowBase(window, root, title, width, titleText, closeButton);

            var row = NewUI("Buttons", body.transform, new Vector2(0, 62));
            Layout(row, 62);
            AddHorizontal(row, new RectOffset(0, 0, 0, 0), 20, TextAnchor.MiddleCenter);

            var standBy = BuildCircleButton(row.transform, "StandBy", "Stand By", out var standByFill,
                                            out var standByBorder, out var standByLabel);
            var turnOff = BuildCircleButton(row.transform, "TurnOff", "Turn Off", out var turnOffFill,
                                            out var turnOffBorder, out var turnOffLabel);
            var restart = BuildCircleButton(row.transform, "Restart", "Restart", out var restartFill,
                                            out var restartBorder, out var restartLabel);

            var cancelRow = NewUI("CancelRow", body.transform, new Vector2(0, 23));
            Layout(cancelRow, 23);
            AddHorizontal(cancelRow, new RectOffset(0, 0, 0, 0), 0, TextAnchor.MiddleCenter);
            var cancel = CreateXpButton(cancelRow.transform, "CancelButton", "Cancel", new Vector2(75, 23));

            Set(window, "standByButton", standBy);
            Set(window, "turnOffButton", turnOff);
            Set(window, "restartButton", restart);
            Set(window, "cancelButton", cancel.GetComponent<Button>());
            Set(window, "standByFill", standByFill);
            Set(window, "standByBorder", standByBorder);
            Set(window, "turnOffFill", turnOffFill);
            Set(window, "turnOffBorder", turnOffBorder);
            Set(window, "restartFill", restartFill);
            Set(window, "restartBorder", restartBorder);
            Set(window, "standByLabel", standByLabel);
            Set(window, "turnOffLabel", turnOffLabel);
            Set(window, "restartLabel", restartLabel);

            return SavePrefab(root, "TurnOffWindow");
        }

        /// <summary>34px circle with a 2px ring, plus a caption underneath. The Button is on the circle.</summary>
        private static Button BuildCircleButton(Transform parent, string name, string label, out Image fill,
                                                out Image border, out TextMeshProUGUI caption)
        {
            var column = NewUI(name, parent, new Vector2(62, 58));
            Layout(column, 58, 62);

            var circle = NewUI("Circle", column.transform, new Vector2(34, 34));
            Anchor(circle, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(34, 34));
            border = AddImage(circle, Color.white, CircleSprite());
            var button = circle.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = border;

            var inner = NewUI("Fill", circle.transform);
            Stretch(inner, 2, 2, 2, 2);
            fill = AddImage(inner, Color.white, CircleSprite());
            fill.raycastTarget = false;

            caption = AddText(column.transform, "Caption", label, 10, FontStyles.Bold, WindowBorder,
                              TextAlignmentOptions.Top, wrap: true);
            Anchor(caption.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -37),
                   new Vector2(62, 16));

            return button;
        }

        // ---- 2. Control Panel ----
        private static XPWindowController BuildControlPanelWindowPrefab()
        {
            const float width = 320f;
            const string title = "Control Panel — System Properties";

            var root = BuildWindowShell("ControlPanelWindow", title, width, out var body, out var titleText,
                                        out var closeButton);
            var window = root.AddComponent<ControlPanelWindow>();
            ApplyWindowBase(window, root, title, width, titleText, closeButton);

            // ---- tabs ----
            var tabRow = NewUI("TabRow", body.transform, new Vector2(0, 22));
            Layout(tabRow, 22);
            AddHorizontal(tabRow, new RectOffset(0, 0, 0, 0), 2, TextAnchor.MiddleLeft);

            var tabButtons = new List<Object>();
            var tabBackgrounds = new List<Object>();
            foreach (string tabName in new[] { "Audio", "Display", "Input" })
            {
                var tab = CreateXpButton(tabRow.transform, "Tab_" + tabName, tabName, new Vector2(66, 22));
                tabButtons.Add(tab.GetComponent<Button>());
                tabBackgrounds.Add(tab.GetComponent<Image>());
            }

            // ---- panels (only one active at a time; fixed height keeps the window from jumping) ----
            var tabContent = NewUI("TabContent", body.transform, new Vector2(0, 150));
            Layout(tabContent, 150);

            var audioPanel = BuildTabPanel(tabContent.transform, "AudioPanel");
            var masterRow = CreateLabeledRow(audioPanel.transform, "MasterRow", "Master volume", 110, 18);
            var masterSlider = CreateSlider(masterRow.transform, "MasterSlider", 160);
            var ambienceRow = CreateLabeledRow(audioPanel.transform, "AmbienceRow", "Ambience", 110, 18);
            var ambienceSlider = CreateSlider(ambienceRow.transform, "AmbienceSlider", 160);
            var micRow = CreateLabeledRow(audioPanel.transform, "MicGainRow", "Microphone gain", 110, 18);
            var micSlider = CreateSlider(micRow.transform, "MicGainSlider", 160);
            var subtitlesToggle = CreateToggle(audioPanel.transform, "SubtitlesToggle", "Enable subtitles");
            var flashingToggle = CreateToggle(audioPanel.transform, "ReduceFlashingToggle", "Reduce flashing effects");

            var displayPanel = BuildTabPanel(tabContent.transform, "DisplayPanel");
            var resolutionRow = CreateLabeledRow(displayPanel.transform, "ResolutionRow", "Resolution", 110, 20);
            var resolutionDropdown = CreateDropdown(resolutionRow.transform, "ResolutionDropdown",
                                                    new[] { "1920 x 1080" });
            var fullscreenToggle = CreateToggle(displayPanel.transform, "FullscreenToggle", "Fullscreen");
            displayPanel.SetActive(false);

            var inputPanel = BuildTabPanel(tabContent.transform, "InputPanel");
            var pttRow = CreateLabeledRow(inputPanel.transform, "PushToTalkRow", "Push-to-Talk key", 110, 22);
            var pttButton = CreateXpButton(pttRow.transform, "PushToTalkButton", "Space", new Vector2(120, 22));
            var pttLabel = pttButton.GetComponentInChildren<TextMeshProUGUI>(true);
            var deviceRow = CreateLabeledRow(inputPanel.transform, "MicDeviceRow", "Microphone", 110, 20);
            var micDropdown = CreateDropdown(deviceRow.transform, "MicDeviceDropdown", new[] { "Default device" });
            inputPanel.SetActive(false);

            // ---- OK / Cancel ----
            var buttonRow = NewUI("ButtonRow", body.transform, new Vector2(0, 23));
            Layout(buttonRow, 23);
            AddHorizontal(buttonRow, new RectOffset(0, 0, 0, 0), 6, TextAnchor.MiddleRight);
            var ok = CreateXpButton(buttonRow.transform, "OkButton", "OK", new Vector2(75, 23));
            var cancel = CreateXpButton(buttonRow.transform, "CancelButton", "Cancel", new Vector2(75, 23));

            SetArray(window, "tabButtons", tabButtons);
            SetArray(window, "tabPanels", new List<Object> { audioPanel, displayPanel, inputPanel });
            SetArray(window, "tabBackgrounds", tabBackgrounds);
            Set(window, "masterVolumeSlider", masterSlider);
            Set(window, "ambienceVolumeSlider", ambienceSlider);
            Set(window, "micGainSlider", micSlider);
            Set(window, "subtitlesToggle", subtitlesToggle);
            Set(window, "reduceFlashingToggle", flashingToggle);
            Set(window, "resolutionDropdown", resolutionDropdown);
            Set(window, "fullscreenToggle", fullscreenToggle);
            Set(window, "pushToTalkRebindButton", pttButton.GetComponent<Button>());
            Set(window, "pushToTalkKeyLabel", pttLabel);
            Set(window, "micDeviceDropdown", micDropdown);
            Set(window, "okButton", ok.GetComponent<Button>());
            Set(window, "cancelButton", cancel.GetComponent<Button>());

            return SavePrefab(root, "ControlPanelWindow");
        }

        private static GameObject BuildTabPanel(Transform parent, string name)
        {
            var go = NewUI(name, parent);
            Stretch(go);
            AddVertical(go, new RectOffset(0, 0, 4, 0), 8);
            return go;
        }

        // ---- 3. My Reports ----
        private static XPWindowController BuildMyReportsWindowPrefab()
        {
            const float width = 275f;
            const string title = "My Reports";

            var root = BuildWindowShell("MyReportsWindow", title, width, out var body, out var titleText,
                                        out var closeButton);
            var window = root.AddComponent<MyReportsWindow>();
            ApplyWindowBase(window, root, title, width, titleText, closeButton);

            var caption = AddText(body.transform, "Caption", "Select a shift log to open:", 11, FontStyles.Normal,
                                  Color.black, TextAlignmentOptions.MidlineLeft);
            Layout(caption.gameObject, 16);

            var listBox = NewUI("ListBox", body.transform, new Vector2(0, 92));
            Layout(listBox, 92);
            AddImage(listBox, Color.white);
            listBox.AddComponent<Outline>().effectColor = FieldBorder;
            AddVertical(listBox, new RectOffset(3, 3, 3, 3), 1);

            var rowTemplate = NewUI("RowTemplate", listBox.transform, new Vector2(0, 18));
            Layout(rowTemplate, 18);
            var rowImage = AddImage(rowTemplate, new Color(1, 1, 1, 0));
            var rowButton = rowTemplate.AddComponent<Button>();
            rowButton.transition = Selectable.Transition.None;
            rowButton.targetGraphic = rowImage;
            var rowLabel = AddText(rowTemplate.transform, "Label", "shift_00.log", 11, FontStyles.Normal,
                                   Color.black, TextAlignmentOptions.MidlineLeft);
            Stretch(rowLabel.gameObject, 4, 0, 4, 0);
            rowTemplate.SetActive(false);

            var buttonRow = NewUI("ButtonRow", body.transform, new Vector2(0, 23));
            Layout(buttonRow, 23);
            AddHorizontal(buttonRow, new RectOffset(0, 0, 0, 0), 6, TextAnchor.MiddleRight);
            var open = CreateXpButton(buttonRow.transform, "OpenButton", "Open", new Vector2(75, 23));
            var cancel = CreateXpButton(buttonRow.transform, "CancelButton", "Cancel", new Vector2(75, 23));

            Set(window, "rowContainer", listBox.GetComponent<RectTransform>());
            Set(window, "rowTemplate", rowTemplate);
            Set(window, "openButton", open.GetComponent<Button>());
            Set(window, "cancelButton", cancel.GetComponent<Button>());

            return SavePrefab(root, "MyReportsWindow");
        }

        // ---- 4. Notepad ----
        private static XPWindowController BuildNotepadWindowPrefab()
        {
            const float width = 275f;
            const string title = "READ ME.txt — Notepad";

            var root = BuildWindowShell("NotepadWindow", title, width, out var body, out var titleText,
                                        out var closeButton);
            var window = root.AddComponent<TextContentWindow>();
            ApplyWindowBase(window, root, title, width, titleText, closeButton);

            var page = NewUI("TextArea", body.transform, new Vector2(0, 130));
            Layout(page, 130);
            AddImage(page, Color.white);
            page.AddComponent<Outline>().effectColor = FieldBorder;

            var bodyText = AddText(page.transform, "Content", "", 11, FontStyles.Normal, Color.black,
                                   TextAlignmentOptions.TopLeft, wrap: true);
            Stretch(bodyText.gameObject, 6, 6, 6, 6);
            bodyText.lineSpacing = 80f; // ~1.8 line-height

            Set(window, "bodyText", bodyText);
            Set(window, "content",
                "NIGHT SHIFT PROTOCOL\n" +
                "1. Monitor all cameras.\n" +
                "2. Report anomalies via form.\n" +
                "3. Do not leave the terminal.\n" +
                "4. Do not answer if it speaks first.\n" +
                "— Management");
            Set(window, "footerText", null);
            Set(window, "footerContent", "");
            Set(window, "confirmButton", null);
            Set(window, "confirmLabel", null);

            return SavePrefab(root, "NotepadWindow");
        }

        // ---- 5/6/7. Simple text dialogs ----
        private static XPWindowController BuildRecycleBinWindowPrefab()
        {
            return BuildSimpleTextWindow("RecycleBinWindow", "Recycle Bin", "3 items.",
                                         "shift_00.log — deleted by SEC-03", "Close");
        }

        private static XPWindowController BuildLogOffWindowPrefab()
        {
            return BuildSimpleTextWindow("LogOffWindow", "Log Off Windows", "Cannot log off.",
                                         "Shift is not complete.", "OK");
        }

        private static XPWindowController BuildHelpWindowPrefab()
        {
            return BuildSimpleTextWindow("HelpWindow", "Help and Support",
                "CONTROLS\n" +
                "Mouse — aim the spotlight, click to interact\n" +
                "Space — hold to speak\n" +
                "Arrow keys — switch camera\n\n" +
                "THE SHIFT\n" +
                "Watch every room. When something is wrong, file an\n" +
                "Incident Report and say the anomaly out loud.\n" +
                "Survive until 6:00 AM.\n\n" +
                "CREDITS\n" +
                "Give Me A Sign", "", "OK");
        }

        private static XPWindowController BuildSimpleTextWindow(string name, string title, string content,
                                                                string footer, string confirmLabel)
        {
            const float width = 275f;

            var root = BuildWindowShell(name, title, width, out var body, out var titleText, out var closeButton);
            var window = root.AddComponent<TextContentWindow>();
            ApplyWindowBase(window, root, title, width, titleText, closeButton);

            var bodyText = AddText(body.transform, "Content", content, 11, FontStyles.Normal, Color.black,
                                   TextAlignmentOptions.TopLeft, wrap: true);
            // No ContentSizeFitter here: the parent VerticalLayoutGroup already reads TMP's
            // preferred height (TMP_Text implements ILayoutElement), and stacking both fights.
            Layout(bodyText.gameObject, -1, -1, 1, -1);

            var footerText = AddText(body.transform, "Footer", footer, 11, FontStyles.Normal, SubtitleGrey,
                                     TextAlignmentOptions.TopLeft, wrap: true);
            Layout(footerText.gameObject, 16);

            var buttonRow = NewUI("ButtonRow", body.transform, new Vector2(0, 23));
            Layout(buttonRow, 23);
            AddHorizontal(buttonRow, new RectOffset(0, 0, 0, 0), 6, TextAnchor.MiddleRight);
            var confirm = CreateXpButton(buttonRow.transform, "ConfirmButton", confirmLabel, new Vector2(75, 23));

            Set(window, "bodyText", bodyText);
            Set(window, "content", content);
            Set(window, "footerText", footerText);
            Set(window, "footerContent", footer);
            Set(window, "confirmButton", confirm.GetComponent<Button>());
            Set(window, "confirmLabel", confirm.GetComponentInChildren<TextMeshProUGUI>(true));
            Set(window, "confirmLabelText", confirmLabel);

            return SavePrefab(root, name);
        }

        // =======================================================================================
        // Scene housekeeping
        // =======================================================================================

        private static void RemoveExistingRoot(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != name) continue;
                Debug.Log($"MainMenuSceneBuilder: replacing existing '{name}'.");
                Object.DestroyImmediate(root);
                return;
            }
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Debug.Log("MainMenuSceneBuilder: added an EventSystem (InputSystemUIInputModule) - the scene had none.");
            Selection.activeObject = go;
        }

        private static void WarnIfSceneNotInBuildSettings(Scene scene)
        {
            if (string.IsNullOrEmpty(scene.path)) return;

            foreach (var entry in EditorBuildSettings.scenes)
                if (entry.path == scene.path) return;

            Debug.LogWarning($"MainMenuSceneBuilder: '{scene.path}' is not in File > Build Settings. " +
                             "Start > Turn Off Computer > Restart reloads the scene by build index, " +
                             "so add it there (and make it the first scene) before building.");
        }
    }
}
