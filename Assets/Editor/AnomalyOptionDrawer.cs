using System.Collections.Generic;
using GameLogic;
using GameLogic.Data;
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

            var attr = (AnomalyOptionAttribute)attribute;
            var catalog = attr.Kind == AnomalyOptionAttribute.OptionKind.AnomalyType ? LoadCatalog() : null;

            // Rooms no longer live in the catalog - they are RoomDefinition assets, so the
            // Location dropdown reads those directly and can never drift out of sync with the
            // rooms the game actually has.
            List<string> options = attr.Kind == AnomalyOptionAttribute.OptionKind.AnomalyType
                ? catalog?.anomalyTypes
                : LoadRoomDisplayNames();

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

            // Jump-to-source button: the catalog for anomaly types, the room assets folder for
            // locations. Hidden when there is nothing to jump to.
            Object source = catalog != null ? (Object)catalog : FirstRoomAsset();
            if (source != null && GUI.Button(editRect, "…", EditorStyles.miniButton))
            {
                Selection.activeObject = source;
                EditorGUIUtility.PingObject(source);
            }

            EditorGUI.EndProperty();
        }

        private static List<string> LoadRoomDisplayNames()
        {
            var names = new List<string>();
            foreach (var room in LoadRoomAssets())
                names.Add(room.Label);
            return names;
        }

        private static Object FirstRoomAsset()
        {
            var rooms = LoadRoomAssets();
            return rooms.Count > 0 ? rooms[0] : null;
        }

        /// <summary>All RoomDefinition assets in the project, in camera order.</summary>
        private static List<RoomDefinition> LoadRoomAssets()
        {
            var rooms = new List<RoomDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:RoomDefinition"))
            {
                var room = AssetDatabase.LoadAssetAtPath<RoomDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (room != null) rooms.Add(room);
            }
            rooms.Sort((a, b) => a.cameraOrder.CompareTo(b.cameraOrder));
            return rooms;
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
