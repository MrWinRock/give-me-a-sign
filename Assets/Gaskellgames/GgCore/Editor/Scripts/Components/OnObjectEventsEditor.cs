#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomEditor(typeof(OnObjectEvents)), CanEditMultipleObjects]
    public class OnObjectEventsEditor : GgEditor
    {
        #region Serialized Properties / OnEnable

        private SerializedProperty verboseLogs;
        private SerializedProperty gizmosOnSelected;
        
        private SerializedProperty useOnStart;
        private SerializedProperty useOnEnable;
        private SerializedProperty useOnDisable;
        private SerializedProperty useOnDestroy;
        private SerializedProperty useOnEnter;
        private SerializedProperty useOnStay;
        private SerializedProperty useOnExit;
        
        private SerializedProperty onStart;
        private SerializedProperty onEnable;
        private SerializedProperty onDisable;
        private SerializedProperty onDestroy;
        
        private SerializedProperty onEnter;
        private SerializedProperty onStay;
        private SerializedProperty onExit;
        
        private SerializedProperty triggerColour;
        private SerializedProperty triggerOutlineColour;
        
        private static int selectedTab = 0;
        private string[] tabs = new[] { "Settings", "Events", "Debug" };
        private int settingsTab = 0;
        private int eventsTab = 1;
        private int debugTab = 2;

        private const string packageRefName = "GgCore";
        private Texture banner;

        private void OnEnable()
        {
            banner = EditorWindowUtility.LoadInspectorBanner();
            
            verboseLogs = serializedObject.FindProperty(nameof(verboseLogs));
            gizmosOnSelected = serializedObject.FindProperty(nameof(gizmosOnSelected));
            
            useOnStart = serializedObject.FindProperty(nameof(useOnStart));
            useOnEnable = serializedObject.FindProperty(nameof(useOnEnable));
            useOnDisable = serializedObject.FindProperty(nameof(useOnDisable));
            useOnDestroy = serializedObject.FindProperty(nameof(useOnDestroy));
            useOnEnter = serializedObject.FindProperty(nameof(useOnEnter));
            useOnStay = serializedObject.FindProperty(nameof(useOnStay));
            useOnExit = serializedObject.FindProperty(nameof(useOnExit));
            
            onStart = serializedObject.FindProperty(nameof(onStart));
            onEnable = serializedObject.FindProperty(nameof(onEnable));
            onDisable = serializedObject.FindProperty(nameof(onDisable));
            onDestroy = serializedObject.FindProperty(nameof(onDestroy));
            onEnter = serializedObject.FindProperty(nameof(onEnter));
            onStay = serializedObject.FindProperty(nameof(onStay));
            onExit = serializedObject.FindProperty(nameof(onExit));
            
            triggerColour = serializedObject.FindProperty(nameof(triggerColour));
            triggerOutlineColour = serializedObject.FindProperty(nameof(triggerOutlineColour));
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region OnInspectorGUI

        public override void OnInspectorGUI()
        {
            // get & update references
            OnObjectEvents onObjectEvents = (OnObjectEvents)target;
            serializedObject.Update();

            // draw banner if turned on in Gaskellgames settings
            EditorWindowUtility.TryDrawBanner(banner, nameof(OnObjectEvents).NicifyName());

            // draw inspector
            selectedTab = GUILayout.Toolbar(selectedTab, tabs);
            EditorGUILayout.Space();
            if (selectedTab == settingsTab)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(useOnStart);
                EditorGUILayout.PropertyField(useOnEnable);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(useOnDisable);
                EditorGUILayout.PropertyField(useOnDestroy);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(useOnEnter);
                EditorGUILayout.PropertyField(useOnStay);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(useOnExit);
            }
            else if (selectedTab == eventsTab)
            {
                if (useOnStart.boolValue) { EditorGUILayout.PropertyField(onStart); }
                if (useOnEnable.boolValue) { EditorGUILayout.PropertyField(onEnable); }
                if (useOnDisable.boolValue) { EditorGUILayout.PropertyField(onDisable); }
                if (useOnDestroy.boolValue) { EditorGUILayout.PropertyField(onDestroy); }
                if (useOnEnter.boolValue) { EditorGUILayout.PropertyField(onEnter); }
                if (useOnStay.boolValue) { EditorGUILayout.PropertyField(onStay); }
                if (useOnExit.boolValue) { EditorGUILayout.PropertyField(onExit); }
            }
            else if (selectedTab == debugTab)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(verboseLogs);
                EditorGUILayout.PropertyField(gizmosOnSelected);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(triggerColour);
                EditorGUILayout.PropertyField(triggerOutlineColour);
            }

            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

    } // class end
}

#endif