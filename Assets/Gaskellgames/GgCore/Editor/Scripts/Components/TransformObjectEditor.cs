#if UNITY_EDITOR
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomEditor(typeof(TransformObject)), CanEditMultipleObjects]
    public class TransformObjectEditor : GgEditor
    {
        #region Serialized Properties / OnEnable
        
        private SerializedProperty gizmosOnSelected;
        private SerializedProperty targetObject;
        private SerializedProperty updateMethod;
        private SerializedProperty start;
        private SerializedProperty end;
        private SerializedProperty lerpValue;
        private SerializedProperty autoLerpSpeed;
        private SerializedProperty rotationSpeed;
        private SerializedProperty canUpdate;

        private const string packageRefName = "GgCore";
        private Texture banner;
        
        private void OnEnable()
        {
            banner = EditorWindowUtility.LoadInspectorBanner();
            
            gizmosOnSelected = serializedObject.FindProperty(nameof(gizmosOnSelected));
            targetObject = serializedObject.FindProperty(nameof(targetObject));
            updateMethod = serializedObject.FindProperty(nameof(updateMethod));
            start = serializedObject.FindProperty(nameof(start));
            end = serializedObject.FindProperty(nameof(end));
            lerpValue = serializedObject.FindProperty(nameof(lerpValue));
            autoLerpSpeed = serializedObject.FindProperty(nameof(autoLerpSpeed));
            rotationSpeed = serializedObject.FindProperty(nameof(rotationSpeed));
            canUpdate = serializedObject.FindProperty(nameof(canUpdate));
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnInspectorGUI

        public override void OnInspectorGUI()
        {
            // get & update references
            TransformObject transformObject = (TransformObject)target;
            serializedObject.Update();

            // draw banner if turned on in Gaskellgames settings
            EditorWindowUtility.TryDrawBanner(banner, nameof(TransformObject).NicifyName());
            
            // draw inspector
            EditorGUILayout.PropertyField(gizmosOnSelected);
            EditorGUILayout.PropertyField(targetObject);
            EditorGUILayout.PropertyField(updateMethod);
            EditorGUILayout.Space();

            if (updateMethod.enumValueIndex == TransformObject.UpdateMethod.ManualLerp.ToInt())
            {
                EditorGUILayout.PropertyField(start);
                EditorGUILayout.PropertyField(end);
                EditorGUILayout.PropertyField(lerpValue);
            }
            if (updateMethod.enumValueIndex == TransformObject.UpdateMethod.AutoLerp.ToInt())
            {
                EditorGUILayout.PropertyField(start);
                EditorGUILayout.PropertyField(end);
                EditorGUILayout.PropertyField(lerpValue);
                EditorGUILayout.PropertyField(autoLerpSpeed);
                EditorGUILayout.PropertyField(canUpdate);
            }
            if (updateMethod.enumValueIndex == TransformObject.UpdateMethod.AutoRotate.ToInt())
            {
                EditorGUILayout.PropertyField(rotationSpeed);
                EditorGUILayout.PropertyField(canUpdate);
            }
            
            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }

        #endregion
        
    } // class end
}
#endif