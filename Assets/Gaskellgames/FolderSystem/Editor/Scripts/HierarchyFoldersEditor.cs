#if UNITY_EDITOR
#if GASKELLGAMES
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.FolderSystem.EditorOnly
{
    /// <summary>
    /// Code updated by Gaskellgames
    /// </summary>
    
    [CustomEditor(typeof(HierarchyFolders)), CanEditMultipleObjects]
    public class HierarchyFoldersEditor : GgEditor
    {
        #region Serialized Properties / OnEnable

        private SerializedProperty customText;
        private SerializedProperty customIcon;
        private SerializedProperty customHighlight;
        
        private SerializedProperty textColor;
        private SerializedProperty iconColor;
        private SerializedProperty highlightColor;
        private SerializedProperty textStyle;
        private SerializedProperty textAlignment;
        
        private const string packageRefName = "FolderSystem";
        private Texture banner;

        private void OnEnable()
        {
            banner = EditorWindowUtility.LoadInspectorBanner();
            
            customText = serializedObject.FindProperty(nameof(customText));
            customIcon = serializedObject.FindProperty(nameof(customIcon));
            customHighlight = serializedObject.FindProperty(nameof(customHighlight));
            
            textColor = serializedObject.FindProperty(nameof(textColor));
            iconColor = serializedObject.FindProperty(nameof(iconColor));
            highlightColor = serializedObject.FindProperty(nameof(highlightColor));
            textStyle = serializedObject.FindProperty(nameof(textStyle));
            textAlignment = serializedObject.FindProperty(nameof(textAlignment));
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region OnInspectorGUI

        public override void OnInspectorGUI()
        {
            // get & update references
            HierarchyFolders hierarchyFolders = (HierarchyFolders)target;
            serializedObject.Update();

            // draw banner if turned on in Gaskellgames settings
            EditorWindowUtility.TryDrawBanner(banner, nameof(HierarchyFolders).NicifyName());

            // custom inspector
            EditorGUILayout.PropertyField(textStyle);
            EditorGUILayout.PropertyField(textAlignment);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Custom Colors");
            GUILayout.Space(2);
            GUIContent textLabel = new GUIContent("Text", "Custom text color");
            GUIContent iconLabel = new GUIContent("Icon", "Custom icon color");
            GUIContent backgroundLabel = new GUIContent("Highlight", "Custom highlight color");
            customText.boolValue = EditorGUILayout.ToggleLeft(textLabel, customText.boolValue, GUILayout.Width(55), GUILayout.ExpandWidth(false));
            customIcon.boolValue = EditorGUILayout.ToggleLeft(iconLabel, customIcon.boolValue, GUILayout.Width(55), GUILayout.ExpandWidth(false));
            customHighlight.boolValue = EditorGUILayout.ToggleLeft(backgroundLabel, customHighlight.boolValue, GUILayout.Width(100), GUILayout.ExpandWidth(false));
            EditorGUILayout.EndHorizontal();
            if (customText.boolValue)
            {
                EditorGUILayout.PropertyField(textColor);
            }
            if (customIcon.boolValue)
            {
                EditorGUILayout.PropertyField(iconColor);
            }
            if (customHighlight.boolValue)
            {
                EditorGUILayout.PropertyField(highlightColor);
            }

            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

    } // class end
}
        
#endif
#endif