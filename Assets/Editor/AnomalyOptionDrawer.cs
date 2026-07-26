using System.Collections.Generic;
using GameLogic;
using UnityEditor;
using UnityEngine;

namespace GiveMeASign.EditorTools
{
    /// <summary>
    /// Renders [AnomalyOption] string fields as a dropdown sourced from the shared
    /// AnomalyOptionsCatalog asset instead of a free-typed text field. Purely an editor
    /// convenience - the underlying SerializedProperty is still a plain string, written
    /// back exactly as if you'd typed it, so Anomaly / IncidentReportManager's runtime
    /// behavior is completely unchanged.
    /// </summary>
    [CustomPropertyDrawer(typeof(AnomalyOptionAttribute))]
    public class AnomalyOptionDrawer : PropertyDrawer
    {
        private static AnomalyOptionsCatalog _catalogCache;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var catalog = LoadCatalog();
            var attr = (AnomalyOptionAttribute)attribute;
            List<string> options = catalog == null
                ? null
                : (attr.Kind == AnomalyOptionAttribute.OptionKind.AnomalyType ? catalog.anomalyTypes : catalog.locations);

            if (options == null || options.Count == 0)
            {
                // No catalog asset found (or its list is empty) - fall back to a plain text
                // field so nothing is ever blocked from being set.
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            const float editBtnWidth = 22f;
            var popupRect = new Rect(position.x, position.y, position.width - editBtnWidth - 2, position.height);
            var editRect = new Rect(position.xMax - editBtnWidth, position.y, editBtnWidth, position.height);

            string current = property.stringValue;
            int matchIndex = options.IndexOf(current);
            bool isUnlisted = matchIndex < 0;

            var display = new List<string>();
            if (isUnlisted)
                display.Add(string.IsNullOrEmpty(current) ? "(not set)" : $"{current}  (not in catalog)");
            display.AddRange(options);

            // isUnlisted inserts exactly one placeholder at index 0, so mapping a display
            // index back to `options` below simply subtracts 1 in that case.
            int currentDisplayIndex = isUnlisted ? 0 : matchIndex;

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(popupRect, label.text, currentDisplayIndex, display.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                bool keptPlaceholder = isUnlisted && newIndex == 0;
                if (!keptPlaceholder)
                {
                    int realIndex = isUnlisted ? newIndex - 1 : newIndex;
                    if (realIndex >= 0 && realIndex < options.Count)
                        property.stringValue = options[realIndex];
                }
            }

            if (GUI.Button(editRect, "…", EditorStyles.miniButton))
            {
                Selection.activeObject = catalog;
                EditorGUIUtility.PingObject(catalog);
            }

            EditorGUI.EndProperty();
        }

        private static AnomalyOptionsCatalog LoadCatalog()
        {
            if (_catalogCache != null) return _catalogCache;

            var guids = AssetDatabase.FindAssets("t:AnomalyOptionsCatalog");
            if (guids.Length == 0) return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _catalogCache = AssetDatabase.LoadAssetAtPath<AnomalyOptionsCatalog>(path);
            return _catalogCache;
        }
    }
}
