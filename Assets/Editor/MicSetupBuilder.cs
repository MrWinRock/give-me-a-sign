using MainMenu;
using MicSetup;
using TMPro;
using UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Whisper;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// Generates the MicSetupWizard prefab and the MicSetup scene (reusing the pause menu's XP window pieces, the Control Panel's
    /// dropdown and the NoiseMeter prefab), plus the config asset and a reset command. Re-running overwrites the generated assets.
    /// </summary>
    public static class MicSetupBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/MicSetup/MicSetupWizard.prefab";
        private const string ScenePath = "Assets/Scenes/MicSetup.unity";
        private const string ConfigPath = "Assets/Resources/MicCalibrationConfig.asset";
        private const string MeterPrefabPath = "Assets/Prefabs/Gameplay/NoiseMeter.prefab";
        private const string ControlPanelPrefabPath = "Assets/Prefabs/MainMenu/ControlPanelWindow.prefab";
        private const string WallpaperSpritePath = "Assets/Sprites/MainMenuBG.jpg";

        private const float GainRowHeight = 22f;
        private const float WindowWidth = 480f, WindowHeight = 458f; // +34 for the mic volume row
        private const int ProgressBlockCount = 14;

        [MenuItem("Tools/Give Me A Sign/Mic Setup/Create Config Asset")]
        public static MicCalibrationConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<MicCalibrationConfig>(ConfigPath);
            if (config != null) return config;

            config = ScriptableObject.CreateInstance<MicCalibrationConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"MicSetupBuilder: created {ConfigPath}.", config);
            return config;
        }

        [MenuItem("Tools/Give Me A Sign/Mic Setup/Reset Microphone Calibration")]
        public static void ResetCalibration()
        {
            MicCalibrationStore.ResetToDefaults();
            Debug.Log("MicSetupBuilder: microphone calibration cleared - the setup wizard shows on the next launch.");
        }

        [MenuItem("Tools/Give Me A Sign/Mic Setup/Build Wizard Prefab")]
        public static void BuildPrefab()
        {
            EnsureConfig();
            if (!System.IO.Directory.Exists("Assets/Prefabs/MicSetup")) System.IO.Directory.CreateDirectory("Assets/Prefabs/MicSetup");

            var style = new XPControlStyle();
            var font = XPTheme.Load().ResolvedFont;
            var cpWindow = LoadControlPanelDropdownSource(out var cpRoot, out var dropdownSource);

            var root = new GameObject("MicSetupWizard", typeof(RectTransform), typeof(MicSetupWizard));
            XPControls.Stretch(root.GetComponent<RectTransform>());

            var dim = XPControls.Img(root.transform, "DimOverlay", new Color(0f, 0f, 0f, 0.6f), r => XPControls.Stretch(r));
            dim.raycastTarget = true;

            var frame = PauseMenuPrefabBuilder.BuildFrame(root.transform, "Window", new Vector2(WindowWidth, WindowHeight), "Microphone Setup.exe", false,
                style, font, out var panel, out var closeButton);

            const float statusHeight = PauseMenuPrefabBuilder.StatusBarHeight;
            var body = PauseMenuPrefabBuilder.BuildBody(panel, style, statusHeight, PauseMenuPrefabBuilder.TitlebarHeight);

            // ── step content (hidden as a whole when there is no microphone) ──
            var step = PauseMenuPrefabBuilder.NewRect("StepRoot", body);
            var stepLayout = step.gameObject.AddComponent<VerticalLayoutGroup>();
            stepLayout.spacing = PauseMenuPrefabBuilder.SectionSpacing;
            stepLayout.childControlWidth = stepLayout.childControlHeight = true;
            stepLayout.childForceExpandWidth = true;
            stepLayout.childForceExpandHeight = false;
            XPControls.SetPreferred(step.gameObject, flexibleWidth: 1f);

            var header = XPControls.Text(step, "HeaderText", font, "Step 1 of 3: Stay quiet", style.headerSize, style.value, true, TextAlignmentOptions.MidlineLeft);
            XPControls.SetPreferred(header.gameObject, height: style.headerSize + 6f);

            var instruction = XPControls.Text(step, "InstructionText", font, "Instruction", style.labelSize, style.label, false, TextAlignmentOptions.TopLeft);
            instruction.textWrappingMode = TextWrappingModes.Normal;
            XPControls.SetPreferred(instruction.gameObject, height: 34f);

            // device row
            var deviceRow = PauseMenuPrefabBuilder.NewRect("DeviceRow", step);
            XPControls.SetPreferred(deviceRow.gameObject, height: 24f);
            var deviceLayout = deviceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            deviceLayout.spacing = 8f;
            deviceLayout.childAlignment = TextAnchor.MiddleLeft;
            deviceLayout.childControlWidth = deviceLayout.childControlHeight = true;
            deviceLayout.childForceExpandWidth = false;
            deviceLayout.childForceExpandHeight = false;

            var deviceLabel = XPControls.Text(deviceRow, "DeviceLabel", font, "Microphone:", style.labelSize, style.label, false, TextAlignmentOptions.MidlineLeft);
            XPControls.SetPreferred(deviceLabel.gameObject, width: 84f, height: 24f);

            TMP_Dropdown dropdown = null;
            if (dropdownSource != null)
            {
                var copy = Object.Instantiate(dropdownSource.gameObject, deviceRow, false);
                copy.name = "DeviceDropdown";
                dropdown = copy.GetComponent<TMP_Dropdown>();
                dropdown.onValueChanged.RemoveAllListeners();
                XPControls.SetPreferred(copy, height: 24f, flexibleWidth: 1f);
            }
            else
            {
                Debug.LogWarning("MicSetupBuilder: Control Panel dropdown not found - the device row has no dropdown.");
            }

            // mic volume row: same 0..1 range as the Control Panel's gain slider (it writes the same PlayerPrefs key)
            var gainRow = PauseMenuPrefabBuilder.NewRect("GainRow", step);
            XPControls.SetPreferred(gainRow.gameObject, height: GainRowHeight);
            var gainLayout = gainRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            gainLayout.spacing = 8f;
            gainLayout.childAlignment = TextAnchor.MiddleLeft;
            gainLayout.childControlWidth = gainLayout.childControlHeight = true;
            gainLayout.childForceExpandWidth = false;
            gainLayout.childForceExpandHeight = false;

            var gainLabel = XPControls.Text(gainRow, "GainLabel", font, "Mic volume:", style.labelSize, style.label, false, TextAlignmentOptions.MidlineLeft);
            XPControls.SetPreferred(gainLabel.gameObject, width: 84f, height: GainRowHeight);
            var gainSlider = XPControls.CreateSlider(gainRow, style, MainMenu.ControlPanelWindow.MinMicGain, MainMenu.ControlPanelWindow.MaxMicGain, GainRowHeight, 11f, 21f);
            gainSlider.wholeNumbers = false;
            gainSlider.SetValueWithoutNotify(1f);
            var gainValue = XPControls.Text(gainRow, "GainValue", font, "100%", style.labelSize, style.value, true, TextAlignmentOptions.MidlineRight);
            XPControls.SetPreferred(gainValue.gameObject, width: 38f, height: GainRowHeight);

            // meter panel: black inset holding the CCTV meter
            var meterOuter = XPControls.Img(step, "MeterPanel", style.groupBorder, null);
            XPControls.SetPreferred(meterOuter.gameObject, height: 112f);
            var meterInner = XPControls.Img(meterOuter.transform, "Inset", Color.black, r => XPControls.Stretch(r, 2f));
            meterInner.raycastTarget = false;
            var meter = meterInner.gameObject.AddComponent<NoiseMeter>();

            var meterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeterPrefabPath);
            if (meterPrefab != null)
            {
                var hud = (GameObject)PrefabUtility.InstantiatePrefab(meterPrefab, meterInner.transform);
                var hudRect = (RectTransform)hud.transform;
                hudRect.anchorMin = new Vector2(0f, 0.5f);
                hudRect.anchorMax = new Vector2(1f, 0.5f);
                hudRect.pivot = new Vector2(0.5f, 0.5f);
                hudRect.anchoredPosition = Vector2.zero;
                hudRect.sizeDelta = new Vector2(-12f, 96f);
            }
            else
            {
                Debug.LogWarning($"MicSetupBuilder: {MeterPrefabPath} not found - the meter panel is empty.");
            }

            // action slot: key hint (voice pages) and progress blocks (quiet page) share the same space
            var slot = PauseMenuPrefabBuilder.NewRect("ActionSlot", step);
            XPControls.SetPreferred(slot.gameObject, height: 28f);

            var keyRow = PauseMenuPrefabBuilder.NewRect("KeyHintRow", slot);
            XPControls.Stretch(keyRow);
            var keyLayout = keyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            keyLayout.spacing = 10f;
            keyLayout.childAlignment = TextAnchor.MiddleLeft;
            keyLayout.childControlWidth = keyLayout.childControlHeight = true;
            keyLayout.childForceExpandWidth = false;
            keyLayout.childForceExpandHeight = false;

            var keyCapBorder = XPControls.Img(keyRow, "KeyCap", style.buttonBorder, null);
            XPControls.SetPreferred(keyCapBorder.gameObject, 28f, 28f);
            var keyCapFill = XPControls.Img(keyCapBorder.transform, "Fill", XPPalette.Hex("#F7F5EA"), r => XPControls.Stretch(r, 1f));
            var keyCapText = XPControls.Text(keyCapBorder.transform, "KeyText", font, "V", style.labelSize + 2f, style.label, true, TextAlignmentOptions.Center);
            XPControls.Stretch(keyCapText.rectTransform);

            var keyHint = XPControls.Text(keyRow, "KeyHintText", font, "Hold the key and say", style.labelSize, style.label, false, TextAlignmentOptions.MidlineLeft);
            XPControls.SetPreferred(keyHint.gameObject, height: 28f, flexibleWidth: 1f);

            var progressRoot = XPControls.Img(slot, "ProgressRoot", style.groupBorder, r =>
            {
                r.anchorMin = new Vector2(0f, 0.5f);
                r.anchorMax = new Vector2(0f, 0.5f);
                r.pivot = new Vector2(0f, 0.5f);
                r.sizeDelta = new Vector2(ProgressBlockCount * 16f + 10f, 24f);
                r.anchoredPosition = Vector2.zero;
            });
            var progressInner = XPControls.Img(progressRoot.transform, "Inset", Color.white, r => XPControls.Stretch(r, 1f));
            var progressLayout = progressInner.gameObject.AddComponent<HorizontalLayoutGroup>();
            progressLayout.padding = new RectOffset(3, 3, 3, 3);
            progressLayout.spacing = 2f;
            progressLayout.childControlWidth = progressLayout.childControlHeight = true;
            progressLayout.childForceExpandWidth = progressLayout.childForceExpandHeight = true;

            var blocks = new Image[ProgressBlockCount];
            for (int i = 0; i < blocks.Length; i++)
                blocks[i] = XPControls.Img(progressInner.transform, $"Block_{i}", XPPalette.Hex("#D6D2C2"), null);

            // feedback (space reserved so the layout never jumps)
            var feedback = XPControls.Text(step, "FeedbackText", font, string.Empty, style.labelSize, style.label, false, TextAlignmentOptions.TopLeft);
            feedback.textWrappingMode = TextWrappingModes.Normal;
            XPControls.SetPreferred(feedback.gameObject, height: 34f);

            // buttons
            var buttonRow = PauseMenuPrefabBuilder.NewButtonRow(step, PauseMenuPrefabBuilder.ButtonHeight + PauseMenuPrefabBuilder.ButtonRowExtraTop, PauseMenuPrefabBuilder.ButtonRowExtraTop);
            var skipButton = XPControls.CreateButton(buttonRow, style, font, "Skip setup", 96f, PauseMenuPrefabBuilder.ButtonHeight, false, null);
            var spacer = PauseMenuPrefabBuilder.NewRect("Spacer", buttonRow);
            XPControls.SetPreferred(spacer.gameObject, width: 0f, height: PauseMenuPrefabBuilder.ButtonHeight, flexibleWidth: 1f);
            var useDefaultsButton = XPControls.CreateButton(buttonRow, style, font, "Use default settings", 138f, PauseMenuPrefabBuilder.ButtonHeight, false, null);
            var backButton = XPControls.CreateButton(buttonRow, style, font, "< Back", 76f, PauseMenuPrefabBuilder.ButtonHeight, false, null);
            var nextButton = XPControls.CreateButton(buttonRow, style, font, "Next >", 84f, PauseMenuPrefabBuilder.ButtonHeight, true, null);
            useDefaultsButton.gameObject.SetActive(false);

            // ── "no microphone" replacement for the step content ──
            var problem = PauseMenuPrefabBuilder.NewRect("ProblemRoot", body);
            var problemLayout = problem.gameObject.AddComponent<VerticalLayoutGroup>();
            problemLayout.spacing = PauseMenuPrefabBuilder.SectionSpacing;
            problemLayout.childAlignment = TextAnchor.UpperLeft;
            problemLayout.childControlWidth = problemLayout.childControlHeight = true;
            problemLayout.childForceExpandWidth = true;
            problemLayout.childForceExpandHeight = false;

            var problemHeader = XPControls.Text(problem, "ProblemHeader", font, "Microphone Setup", style.headerSize, style.value, true, TextAlignmentOptions.MidlineLeft);
            XPControls.SetPreferred(problemHeader.gameObject, height: style.headerSize + 6f);
            var problemText = XPControls.Text(problem, "ProblemText", font, "No microphone detected.", style.labelSize, style.label, false, TextAlignmentOptions.TopLeft);
            problemText.textWrappingMode = TextWrappingModes.Normal;
            XPControls.SetPreferred(problemText.gameObject, height: 60f);
            var problemRow = PauseMenuPrefabBuilder.NewButtonRow(problem, PauseMenuPrefabBuilder.ButtonHeight, 0f);
            var rescanButton = XPControls.CreateButton(problemRow, style, font, "Re-scan devices", 120f, PauseMenuPrefabBuilder.ButtonHeight, false, null);
            var continueButton = XPControls.CreateButton(problemRow, style, font, "Continue with typed input", 190f, PauseMenuPrefabBuilder.ButtonHeight, true, null);
            problem.gameObject.SetActive(false);

            // ── status bar ──
            var statusText = BuildStatusBar(panel, style, font);

            // ── wire the wizard ──
            var wizard = root.GetComponent<MicSetupWizard>();
            var so = new SerializedObject(wizard);
            so.FindProperty("windowFrame").objectReferenceValue = frame;
            so.FindProperty("dim").objectReferenceValue = dim;
            so.FindProperty("titleText").objectReferenceValue = frame.Find("Panel/Titlebar/Title").GetComponent<TextMeshProUGUI>();
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("stepRoot").objectReferenceValue = step.gameObject;
            so.FindProperty("headerText").objectReferenceValue = header;
            so.FindProperty("instructionText").objectReferenceValue = instruction;
            so.FindProperty("deviceDropdown").objectReferenceValue = dropdown;
            so.FindProperty("gainSlider").objectReferenceValue = gainSlider;
            so.FindProperty("gainValueText").objectReferenceValue = gainValue;
            so.FindProperty("meter").objectReferenceValue = meter;
            so.FindProperty("keyHintRow").objectReferenceValue = keyRow.gameObject;
            so.FindProperty("keyCapFill").objectReferenceValue = keyCapFill;
            so.FindProperty("keyCapText").objectReferenceValue = keyCapText;
            so.FindProperty("keyHintText").objectReferenceValue = keyHint;
            so.FindProperty("progressRoot").objectReferenceValue = progressRoot.gameObject;
            var blockArray = so.FindProperty("progressBlocks");
            blockArray.arraySize = blocks.Length;
            for (int i = 0; i < blocks.Length; i++) blockArray.GetArrayElementAtIndex(i).objectReferenceValue = blocks[i];
            so.FindProperty("feedbackText").objectReferenceValue = feedback;
            so.FindProperty("skipButton").objectReferenceValue = skipButton;
            so.FindProperty("skipLabel").objectReferenceValue = skipButton.GetComponentInChildren<TextMeshProUGUI>();
            so.FindProperty("backButton").objectReferenceValue = backButton;
            so.FindProperty("nextButton").objectReferenceValue = nextButton;
            so.FindProperty("nextLabel").objectReferenceValue = nextButton.GetComponentInChildren<TextMeshProUGUI>();
            so.FindProperty("useDefaultsButton").objectReferenceValue = useDefaultsButton;
            so.FindProperty("problemRoot").objectReferenceValue = problem.gameObject;
            so.FindProperty("problemText").objectReferenceValue = problemText;
            so.FindProperty("continueTypedButton").objectReferenceValue = continueButton;
            so.FindProperty("rescanButton").objectReferenceValue = rescanButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool success);
            Object.DestroyImmediate(root);
            if (cpRoot != null) PrefabUtility.UnloadPrefabContents(cpRoot);

            if (success) Debug.Log($"MicSetupBuilder: saved {PrefabPath}.", prefab);
            else Debug.LogError($"MicSetupBuilder: could not save {PrefabPath}.");
        }

        [MenuItem("Tools/Give Me A Sign/Mic Setup/Build MicSetup Scene")]
        public static void BuildScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError("MicSetupBuilder: build the wizard prefab first."); return; }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("MicSetupCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // The menu's wallpaper, with no desktop icons and no taskbar.
            var desktop = XPControls.Img(canvasGo.transform, "Desktop", Color.white, r => XPControls.Stretch(r));
            desktop.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WallpaperSpritePath);
            desktop.raycastTarget = false;
            desktop.gameObject.AddComponent<DesktopWallpaper>();

            var wizard = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);

            var host = new GameObject("MicSetupSceneHost", typeof(MicSetupSceneHost));
            var hostSo = new SerializedObject(host.GetComponent<MicSetupSceneHost>());
            hostSo.FindProperty("wizard").objectReferenceValue = wizard.GetComponent<MicSetupWizard>();
            hostSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                int menuIndex = scenes.FindIndex(s => s.path.EndsWith("/MainMenu.unity"));
                // After MainMenu: the first enabled scene is the one a build starts on, and the router sends first launches here.
                scenes.Insert(menuIndex >= 0 ? menuIndex + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            Debug.Log($"MicSetupBuilder: saved {ScenePath} and added it to Build Settings.");
        }

        // ── pieces ───────────────────────────────────────────────────────────────────────

        private static ControlPanelWindow LoadControlPanelDropdownSource(out GameObject contents, out TMP_Dropdown dropdown)
        {
            contents = null;
            dropdown = null;

            var path = ControlPanelPrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return null;

            contents = PrefabUtility.LoadPrefabContents(path);
            var window = contents.GetComponent<ControlPanelWindow>();
            if (window == null) return null;

            dropdown = new SerializedObject(window).FindProperty("micDeviceDropdown").objectReferenceValue as TMP_Dropdown;
            return window;
        }

        private static TextMeshProUGUI BuildStatusBar(RectTransform panel, XPControlStyle style, TMP_FontAsset font)
        {
            float height = PauseMenuPrefabBuilder.StatusBarHeight;
            float pad = PauseMenuPrefabBuilder.BodyPadding;

            var bar = XPControls.Img(panel, "StatusBar", style.windowBody, r =>
            {
                r.anchorMin = Vector2.zero;
                r.anchorMax = new Vector2(1f, 0f);
                r.pivot = new Vector2(0.5f, 0f);
                r.sizeDelta = new Vector2(0f, height);
                r.anchoredPosition = Vector2.zero;
            });

            XPControls.Img(bar.transform, "TopBorder", style.groupBorder, r =>
            {
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = Vector2.one;
                r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(0f, 1f);
            });

            var text = XPControls.Text(bar.transform, "StatusText", font, string.Empty, style.smallSize, style.label, false, TextAlignmentOptions.MidlineLeft);
            XPControls.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(pad, 0f);
            text.rectTransform.offsetMax = new Vector2(-pad, 0f);
            return text;
        }
    }
}
