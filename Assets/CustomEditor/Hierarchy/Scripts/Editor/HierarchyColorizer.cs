using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using CustomEditor.Hierarchy.Scripts.Data;

namespace CustomEditor.Hierarchy.Scripts.Editor
{
    [InitializeOnLoad]
    public class HierarchyColorizer : MonoBehaviour
    {
        public enum ColorType
        {
            None,
            Red,
            Blue,
            Purple,
            Green,
            Yellow,
            Pink
        }

        private static Dictionary<ColorType, Texture2D> _gradientTexture = new Dictionary<ColorType, Texture2D>();

        private static GUIStyle _labelStyle;

        [Obsolete("Obsolete")]
        static HierarchyColorizer()
        {
            EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyWindowItemOnGUI;
            CreateTextutre();
        }

        [Obsolete("Obsolete")]
        private static void HandleHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (obj == null) return;

            bool isFromParent;
            ColorType colorType = GetColorIncludingParent(obj, out isFromParent);
            if (colorType == ColorType.None) return;

            if (Selection.Contains(instanceID)) return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.label);
                _labelStyle.normal.textColor = Color.white;
                _labelStyle.fontStyle = FontStyle.Bold;
            }

            Rect textRect = new Rect(selectionRect);
            EditorGUI.DrawRect(textRect, new Color(0.22f, 0.22f, 0.22f)); // Dark gray to match Unity's hierarchy background

            Rect bgRect = new Rect(selectionRect);
            bgRect.x = 32;
            bgRect.width = EditorGUIUtility.currentViewWidth - bgRect.x;

            if (_gradientTexture.ContainsKey(colorType))
            {
                Color originalColor = GUI.color;
                if (isFromParent)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.5f);
                }

                GUI.DrawTexture(bgRect, _gradientTexture[colorType]);

                GUI.color = originalColor;
            }

            Rect iconRect = new Rect(selectionRect);
            iconRect.x = selectionRect.x - 16;
            iconRect.width = 16;
            iconRect.height = 16;

            Texture2D icon = AssetPreview.GetMiniThumbnail(obj);
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon);
            }

            if (obj.transform.childCount > 0)
            {
                Rect foldoutRect = new Rect(selectionRect);
                foldoutRect.x = selectionRect.x - 28;
                foldoutRect.width = 16;

                bool isExpanded = IsExpanded(obj);
                EditorGUI.Foldout(foldoutRect, isExpanded, GUIContent.none, true);
            }

            EditorGUI.LabelField(selectionRect, obj.name, _labelStyle);
        }

        private static void CreateTextutre()
        {
            _gradientTexture[ColorType.Red] = CreateGradientTexture(new Color(0.6f, 0.0f, 0.0f), new Color(1.0f, 0.4f, 0.4f));
            _gradientTexture[ColorType.Blue] = CreateGradientTexture(new Color(0.0f, 0.3f, 0.6f), new Color(0.4f, 0.6f, 1.0f));
            _gradientTexture[ColorType.Purple] = CreateGradientTexture(new Color(0.4f, 0.0f, 0.6f), new Color(0.8f, 0.4f, 1.0f));
            _gradientTexture[ColorType.Green] = CreateGradientTexture(new Color(0.0f, 0.5f, 0.2f), new Color(0.4f, 1.0f, 0.5f));
            _gradientTexture[ColorType.Yellow] = CreateGradientTexture(new Color(0.6f, 0.5f, 0.0f), new Color(1.0f, 0.9f, 0.3f));
            _gradientTexture[ColorType.Pink] = CreateGradientTexture(new Color(0.8f, 0.2f, 0.5f), new Color(1.0f, 0.6f, 0.8f));
        }

        private static Texture2D CreateGradientTexture(Color leftColor, Color rightColor)
        {
            int width = 64;
            int height = 1;
            Texture2D texture = new Texture2D(width, height);

            for (int i = 0; i < width; i++)
            {
                float t = (float)i / (width - 1);
                Color color = Color.Lerp(leftColor, rightColor, t);
                
                float alpha = Mathf.Lerp(1.0f, 0.8f, t);
                color.a = alpha;
                
                texture.SetPixel(i, 0, color);
            }
            
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        // Context Menu 

        [MenuItem("GameObject/Hierarchy Enhance/Color: Red", false, 10)]
        private static void SetRed() { SetColorForSelection(ColorType.Red); }

        [MenuItem("GameObject/Hierarchy Enhance/Color: Blue", false, 10)]
        private static void SetBlue() { SetColorForSelection(ColorType.Blue); }

        [MenuItem("GameObject/Hierarchy Enhance/Color: Purple", false, 10)]
        private static void SetPurple() { SetColorForSelection(ColorType.Purple); }

        [MenuItem("GameObject/Hierarchy Enhance/Color: Green", false, 10)]
        private static void SetGreen() { SetColorForSelection(ColorType.Green); }

        [MenuItem("GameObject/Hierarchy Enhance/Color: Yellow", false, 10)]
        private static void SetYellow() { SetColorForSelection(ColorType.Yellow); }

        [MenuItem("GameObject/Hierarchy Enhance/Color: Pink", false, 10)]
        private static void SetPink() { SetColorForSelection(ColorType.Pink); }

        [MenuItem("GameObject/Hierarchy Enhance/Reset Color", false, 10)]
        private static void ResetColor() { SetColorForSelection(ColorType.None); }


        private static void SetColorForSelection(ColorType type)
        {
            foreach (GameObject obj in Selection.gameObjects)
            {
                SaveColor(obj, type);
            }
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void SaveColor(GameObject obj, ColorType type)
        {
            string id = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
            HierarchyColorDataManager.SetColor(id, (int)type);
        }

        private static ColorType LoadColor(GameObject obj)
        {
            string id = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString();
            int colorType = HierarchyColorDataManager.GetColor(id);
            return (ColorType)colorType;
        }

        private static ColorType GetColorIncludingParent(GameObject obj, out bool isFromParent)
        {
            ColorType colorType = LoadColor(obj);
            if (colorType != ColorType.None)
            {
                isFromParent = false;
                return colorType;
            }

            Transform parent = obj.transform.parent;
            if (parent != null)
            {
                colorType = GetColorIncludingParent(parent.gameObject, out _);
                if (colorType != ColorType.None)
                {
                    isFromParent = true;
                    return colorType;
                }
            }

            isFromParent = false;
            return ColorType.None;
        }

        private static bool IsExpanded(GameObject obj)
        {
            var sceneHierarchyType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
            if (sceneHierarchyType != null)
            {
                var lastInteractedHierarchyWindowProperty = sceneHierarchyType.GetProperty("lastInteractedHierarchyWindow", BindingFlags.Public | BindingFlags.Static);
                if (lastInteractedHierarchyWindowProperty != null)
                {
                    var hierarchyWindow = lastInteractedHierarchyWindowProperty.GetValue(null);
                    if (hierarchyWindow != null)
                    {
                        var sceneHierarchyProperty = sceneHierarchyType.GetProperty("sceneHierarchy");
                        if (sceneHierarchyProperty != null)
                        {
                            var sceneHierarchy = sceneHierarchyProperty.GetValue(hierarchyWindow);
                            if (sceneHierarchy != null)
                            {
                                var isExpandedMethod = sceneHierarchy.GetType().GetMethod("IsExpanded", new[] { typeof(int) });
                                if (isExpandedMethod != null)
                                {
                                    return (bool)isExpandedMethod.Invoke(sceneHierarchy, new object[] { obj.GetInstanceID() });
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }
    }
}
