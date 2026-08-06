using System.Collections.Generic;
using System.IO;
using TMPro;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// Widget factory + generated-sprite helpers for <see cref="MainMenuSceneBuilder"/>.
    /// Nothing game-specific lives here - just "make me an XP button / slider / rounded sprite".
    /// </summary>
    public static partial class MainMenuSceneBuilder
    {
        private const string GeneratedSpriteFolder = "Assets/Sprites/GeneratedUI";

        private static TMP_FontAsset _font;

        private static TMP_FontAsset Font
        {
            get
            {
                if (_font == null)
                {
                    _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/tahoma SDF.asset");
                    if (_font == null)
                        Debug.LogWarning("MainMenuSceneBuilder: 'Assets/Fonts/tahoma SDF.asset' not found - " +
                                         "falling back to the default TMP font (Arial/LiberationSans).");
                }
                return _font;
            }
        }

        // =======================================================================================
        // Basic construction
        // =======================================================================================

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        private static Color Hex(string hex, float alpha)
        {
            var c = Hex(hex);
            c.a = alpha;
            return c;
        }

        private static GameObject NewUI(string name, Transform parent, Vector2 size = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            if (parent != null) rt.SetParent(parent, false);
            rt.sizeDelta = size;
            return go;
        }

        private static Image AddImage(GameObject go, Color color, Sprite sprite = null, Image.Type type = Image.Type.Simple)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = type;
            }
            return img;
        }

        private static UIGradient AddGradient(GameObject go, Color top, Color bottom)
        {
            var gradient = go.AddComponent<UIGradient>();
            gradient.SetColors(top, bottom);
            return gradient;
        }

        private static void Stretch(GameObject go, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void StretchTop(GameObject go, float height, float inset = 0)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(inset, -height);
            rt.offsetMax = new Vector2(-inset, 0);
        }

        private static void StretchBottom(GameObject go, float height)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0, 0);
            rt.offsetMax = new Vector2(0, height);
        }

        private static void Anchor(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static LayoutElement Layout(GameObject go, float preferredHeight = -1, float preferredWidth = -1,
                                            float flexibleWidth = -1, float flexibleHeight = -1, float minWidth = -1)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();

            if (preferredHeight >= 0) { le.preferredHeight = preferredHeight; le.flexibleHeight = 0; }
            if (preferredWidth >= 0) { le.preferredWidth = preferredWidth; le.flexibleWidth = 0; }
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
            if (minWidth >= 0) le.minWidth = minWidth;
            return le;
        }

        private static VerticalLayoutGroup AddVertical(GameObject go, RectOffset padding, float spacing,
                                                       TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding = padding;
            vlg.spacing = spacing;
            vlg.childAlignment = alignment;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            return vlg;
        }

        private static HorizontalLayoutGroup AddHorizontal(GameObject go, RectOffset padding, float spacing,
                                                           TextAnchor alignment = TextAnchor.MiddleLeft,
                                                           bool expandWidth = false)
        {
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = padding;
            hlg.spacing = spacing;
            hlg.childAlignment = alignment;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = expandWidth;
            hlg.childForceExpandHeight = false;
            return hlg;
        }

        private static ContentSizeFitter FitVertical(GameObject go)
        {
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return fitter;
        }

        private static TextMeshProUGUI AddText(Transform parent, string name, string text, float size,
                                               FontStyles style, Color color, TextAlignmentOptions align,
                                               bool wrap = false)
        {
            var go = NewUI(name, parent, new Vector2(100, 20));
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            if (Font != null) tmp.font = Font;
            return tmp;
        }

        // =======================================================================================
        // Composite widgets
        // =======================================================================================

        /// <summary>Standard XP push button: light-to-dark face gradient, 1px #003C74 border, 3px corners.</summary>
        private static GameObject CreateXpButton(Transform parent, string name, string label, Vector2 size,
                                                 float fontSize = 11f)
        {
            var go = NewUI(name, parent, size);
            var img = AddImage(go, Color.white, RoundedSprite(3), Image.Type.Sliced);
            // Gradient BEFORE Outline: mesh effects run in component order, so the outline copies
            // are added after tinting and keep their own solid border colour.
            AddGradient(go, Hex("#FDFDF6"), Hex("#E3DFCE"));
            var outline = go.AddComponent<Outline>();
            outline.effectColor = WindowBorder;
            outline.effectDistance = new Vector2(1, -1);

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;

            var text = AddText(go.transform, "Label", label, fontSize, FontStyles.Normal, Color.black,
                               TextAlignmentOptions.Center);
            Stretch(text.gameObject);

            Layout(go, size.y, size.x);
            return go;
        }

        /// <summary>Row: fixed-width label on the left, control filling the rest.</summary>
        private static GameObject CreateLabeledRow(Transform parent, string name, string labelText,
                                                   float labelWidth, float height)
        {
            var row = NewUI(name, parent, new Vector2(0, height));
            AddHorizontal(row, new RectOffset(0, 0, 0, 0), 6, TextAnchor.MiddleLeft);
            Layout(row, height, -1, 1);

            var label = AddText(row.transform, "Label", labelText, 11, FontStyles.Normal, Color.black,
                                TextAlignmentOptions.MidlineLeft);
            Layout(label.gameObject, height, labelWidth);

            return row;
        }

        private static Slider CreateSlider(Transform parent, string name, float width, float height = 18f)
        {
            var go = NewUI(name, parent, new Vector2(width, height));
            Layout(go, height, -1, 1);

            // Transparent full-height hit area: without it only the 4px track and the handle are
            // draggable, which is a miserable target at a 1920-wide reference resolution.
            AddImage(go, new Color(0, 0, 0, 0));

            var slider = go.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            var background = NewUI("Background", go.transform);
            var bgRt = background.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(1, 0.5f);
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = new Vector2(0, 4);
            bgRt.anchoredPosition = Vector2.zero;
            AddImage(background, FieldBorder, RoundedSprite(2), Image.Type.Sliced);

            var fillArea = NewUI("Fill Area", go.transform);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = new Vector2(0, 0.5f);
            fillAreaRt.anchorMax = new Vector2(1, 0.5f);
            fillAreaRt.pivot = new Vector2(0.5f, 0.5f);
            fillAreaRt.sizeDelta = new Vector2(-height, 4);
            fillAreaRt.anchoredPosition = Vector2.zero;

            var fill = NewUI("Fill", fillArea.transform, new Vector2(10, 4));
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(0, 1);
            fillRt.pivot = new Vector2(0.5f, 0.5f);
            AddImage(fill, TitlebarTop, RoundedSprite(2), Image.Type.Sliced);

            var handleArea = NewUI("Handle Slide Area", go.transform);
            var handleAreaRt = handleArea.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = new Vector2(0, 0);
            handleAreaRt.anchorMax = new Vector2(1, 1);
            handleAreaRt.offsetMin = new Vector2(height * 0.5f, 0);
            handleAreaRt.offsetMax = new Vector2(-height * 0.5f, 0);

            // XP volume-slider thumb: a light vertical-gradient tab, slightly wider than tall feels
            // wrong here - keep it a tall grab tab like the real Sounds control panel.
            var handle = NewUI("Handle", handleArea.transform, new Vector2(height * 0.75f, height));
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0, 0);
            handleRt.anchorMax = new Vector2(0, 1);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            var handleImage = AddImage(handle, Color.white, RoundedSprite(2), Image.Type.Sliced);
            AddGradient(handle, Hex("#FDFDF6"), Hex("#D8D4C4"));
            handle.AddComponent<Outline>().effectColor = WindowBorder;

            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }

        private static Toggle CreateToggle(Transform parent, string name, string labelText, float height = 18f)
        {
            var go = NewUI(name, parent, new Vector2(0, height));
            Layout(go, height, -1, 1);

            // Transparent hit area over the whole row so the LABEL toggles too, not just the box.
            AddImage(go, new Color(0, 0, 0, 0));

            var toggle = go.AddComponent<Toggle>();

            var box = NewUI("Box", go.transform, new Vector2(13, 13));
            Anchor(box, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(13, 13));
            var boxImage = AddImage(box, Color.white);
            var boxOutline = box.AddComponent<Outline>();
            boxOutline.effectColor = FieldBorder;

            var check = NewUI("Checkmark", box.transform, new Vector2(9, 9));
            Anchor(check, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9, 9));
            var checkImage = AddImage(check, Hex("#1E5ECC"));

            var label = AddText(go.transform, "Label", labelText, 11, FontStyles.Normal, Color.black,
                                TextAlignmentOptions.MidlineLeft);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0, 0);
            labelRt.anchorMax = new Vector2(1, 1);
            labelRt.offsetMin = new Vector2(19, 0);
            labelRt.offsetMax = Vector2.zero;

            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.isOn = true;

            return toggle;
        }

        private static TMP_Dropdown CreateDropdown(Transform parent, string name, IEnumerable<string> options,
                                                   float height = 20f)
        {
            var go = NewUI(name, parent, new Vector2(0, height));
            Layout(go, height, -1, 1);

            var bgImage = AddImage(go, Color.white);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = FieldBorder;

            var dropdown = go.AddComponent<TMP_Dropdown>();

            var caption = AddText(go.transform, "Label", "", 11, FontStyles.Normal, Color.black,
                                  TextAlignmentOptions.MidlineLeft);
            var captionRt = caption.rectTransform;
            captionRt.anchorMin = Vector2.zero;
            captionRt.anchorMax = Vector2.one;
            captionRt.offsetMin = new Vector2(6, 1);
            captionRt.offsetMax = new Vector2(-18, -1);

            var arrow = NewUI("Arrow", go.transform, new Vector2(9, 9));
            Anchor(arrow, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-5, 0), new Vector2(9, 9));
            AddImage(arrow, Hex("#0A246A"));

            var template = NewUI("Template", go.transform, new Vector2(0, 90));
            var templateRt = template.GetComponent<RectTransform>();
            templateRt.anchorMin = new Vector2(0, 0);
            templateRt.anchorMax = new Vector2(1, 0);
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.anchoredPosition = new Vector2(0, 1);
            templateRt.sizeDelta = new Vector2(0, 90);
            AddImage(template, Color.white);
            template.AddComponent<Outline>().effectColor = FieldBorder;
            var scrollRect = template.AddComponent<ScrollRect>();

            var viewport = NewUI("Viewport", template.transform);
            Stretch(viewport);
            AddImage(viewport, Color.white);
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = NewUI("Content", viewport.transform, new Vector2(0, 20));
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1f);

            var item = NewUI("Item", content.transform, new Vector2(0, 18));
            var itemRt = item.GetComponent<RectTransform>();
            itemRt.anchorMin = new Vector2(0, 0.5f);
            itemRt.anchorMax = new Vector2(1, 0.5f);
            itemRt.sizeDelta = new Vector2(0, 18);
            var itemToggle = item.AddComponent<Toggle>();

            var itemBackground = NewUI("Item Background", item.transform);
            Stretch(itemBackground);
            var itemBackgroundImage = AddImage(itemBackground, Color.white);

            var itemCheck = NewUI("Item Checkmark", item.transform);
            Stretch(itemCheck);
            var itemCheckImage = AddImage(itemCheck, Hex("#316AC5"));

            var itemLabel = AddText(item.transform, "Item Label", "Option", 11, FontStyles.Normal, Color.black,
                                    TextAlignmentOptions.MidlineLeft);
            var itemLabelRt = itemLabel.rectTransform;
            itemLabelRt.anchorMin = Vector2.zero;
            itemLabelRt.anchorMax = Vector2.one;
            itemLabelRt.offsetMin = new Vector2(6, 0);
            itemLabelRt.offsetMax = new Vector2(-4, 0);

            itemToggle.targetGraphic = itemBackgroundImage;
            itemToggle.graphic = itemCheckImage;
            itemToggle.isOn = true;

            scrollRect.content = contentRt;
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            dropdown.template = templateRt;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.targetGraphic = bgImage;

            var optionList = new List<string>(options);
            if (optionList.Count > 0)
                dropdown.AddOptions(optionList);

            template.SetActive(false);
            return dropdown;
        }

        // =======================================================================================
        // Generated sprites
        // =======================================================================================

        /// <summary>
        /// 9-sliced rounded rectangle. <paramref name="corners"/> is a bitmask:
        /// 1 = top-left, 2 = top-right, 4 = bottom-right, 8 = bottom-left. Default = all four.
        /// </summary>
        private static Sprite RoundedSprite(int radius, int corners = 0xF)
        {
            string assetPath = $"{GeneratedSpriteFolder}/XP_Round{radius}_{corners:X}.png";
            var cached = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (cached != null) return cached;

            int pad = radius + 2;
            int size = pad * 2 + 2;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = RoundedRectAlpha(x + 0.5f, y + 0.5f, size, radius, corners);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            return WriteSprite(assetPath, size, size, pixels, new Vector4(pad, pad, pad, pad));
        }

        private static float RoundedRectAlpha(float x, float y, int size, int radius, int corners)
        {
            if (radius <= 0) return 1f;

            // (corner centre, bitmask flag) for each of the four corners.
            float lo = radius;
            float hi = size - radius;

            bool left = x < lo;
            bool right = x > hi;
            bool bottom = y < lo;
            bool top = y > hi;

            if (!(left || right) || !(bottom || top))
                return 1f; // straight edge or interior

            int flag;
            Vector2 centre;
            if (left && top) { flag = 1; centre = new Vector2(lo, hi); }
            else if (right && top) { flag = 2; centre = new Vector2(hi, hi); }
            else if (right && bottom) { flag = 4; centre = new Vector2(hi, lo); }
            else { flag = 8; centre = new Vector2(lo, lo); }

            if ((corners & flag) == 0)
                return 1f; // this corner stays square

            float distance = Vector2.Distance(new Vector2(x, y), centre);
            return Mathf.Clamp01(radius - distance + 0.5f);
        }

        private static Sprite CircleSprite()
        {
            const string assetPath = GeneratedSpriteFolder + "/XP_Circle.png";
            var cached = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (cached != null) return cached;

            const int size = 64;
            var pixels = new Color32[size * size];
            var centre = new Vector2(size * 0.5f, size * 0.5f);
            const float radius = size * 0.5f - 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            return WriteSprite(assetPath, size, size, pixels, Vector4.zero);
        }

        /// <summary>1-on/1-off dash used, tiled, for the icon focus rectangle.</summary>
        private static Sprite DotSprite(bool horizontal)
        {
            string assetPath = $"{GeneratedSpriteFolder}/XP_Dot{(horizontal ? "H" : "V")}.png";
            var cached = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (cached != null) return cached;

            int width = horizontal ? 2 : 1;
            int height = horizontal ? 1 : 2;
            var pixels = new Color32[2];
            pixels[0] = new Color32(255, 255, 255, 255);
            pixels[1] = new Color32(255, 255, 255, 0);

            return WriteSprite(assetPath, width, height, pixels, Vector4.zero);
        }

        private static Sprite WriteSprite(string assetPath, int width, int height, Color32[] pixels, Vector4 border)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Sprites"))
                AssetDatabase.CreateFolder("Assets", "Sprites");
            if (!AssetDatabase.IsValidFolder(GeneratedSpriteFolder))
                AssetDatabase.CreateFolder("Assets/Sprites", "GeneratedUI");

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();

            string systemPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            File.WriteAllBytes(systemPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;   // required for Sliced / Tiled
            settings.spriteBorder = border;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        // =======================================================================================
        // Serialized-field wiring (fields are private, so it all goes through SerializedObject)
        // =======================================================================================

        private static void Set(Object target, string fieldName, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"MainMenuSceneBuilder: field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            switch (value)
            {
                case null: prop.objectReferenceValue = null; break;
                case Object objectValue: prop.objectReferenceValue = objectValue; break;
                case Color colorValue: prop.colorValue = colorValue; break;
                case Vector2 vectorValue: prop.vector2Value = vectorValue; break;
                case bool boolValue: prop.boolValue = boolValue; break;
                case float floatValue: prop.floatValue = floatValue; break;
                case int intValue: prop.intValue = intValue; break;
                case string stringValue: prop.stringValue = stringValue; break;
                case System.Enum enumValue: prop.enumValueIndex = System.Convert.ToInt32(enumValue); break;
                default:
                    Debug.LogError($"MainMenuSceneBuilder: unsupported value type for '{fieldName}'.");
                    break;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string fieldName, IList<Object> values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null || !prop.isArray)
            {
                Debug.LogError($"MainMenuSceneBuilder: array field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }

            prop.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
