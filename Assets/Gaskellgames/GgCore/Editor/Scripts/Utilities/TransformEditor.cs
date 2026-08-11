#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Gaskellgames.EditorOnly
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com/Unity_TransformUtilities
    /// </summary>

    [CustomEditor(typeof(Transform)), CanEditMultipleObjects]
    public class TransformEditor : Editor
    {
        #region Serialized Properties & Variables
        
        private Type transformInspectorType => typeof(EditorApplication).Assembly.GetType("UnityEditor.TransformInspector");
        private Editor transformEditor;
        private List<Transform> transformTargets = new List<Transform>();
        
        private readonly float standardGap = 4; // double standard gap width
        private static bool utilitiesOpen;

        #endregion

        //----------------------------------------------------------------------------------------------------
        
        #region OnEnable / OnDisable

        private void OnEnable()
        {
            AssignTransformEditor();
        }

        private void OnDisable()
        {
            ClearTransformEditor();
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnInspectorGUI

        public override void OnInspectorGUI()
        {
            // null check
            if (!transformEditor || transformEditor.target == null)
            {
                base.OnInspectorGUI();
                return;
            }
            
            // get reference changes
            serializedObject.Update();
            
            GUI.changed = false;
            if (!GaskellgamesSettings_SO.Instance || !GaskellgamesSettings_SO.Instance.ShowTransformTools)
            {
                transformEditor.OnInspectorGUI();
            }
            else
            {
                // draw inspector
                if (GaskellgamesSettings_SO.Instance.ShowResetButtons) { OnInspectorGUI_ResetButtons(); }
                else { transformEditor.OnInspectorGUI(); }
                if (GaskellgamesSettings_SO.Instance.ShowTransformUtilities) { OnInspectorGUI_TransformUtilities(); }
            }
            
            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }

        private void OnInspectorGUI_ResetButtons()
        {
	        // start wrapped transform editor and custom reset buttons
	        GUILayout.BeginHorizontal();
	        
            // default transform
            GUILayout.BeginVertical();
            transformEditor.OnInspectorGUI();
            GUILayout.EndVertical();
            
            // reset buttons
            GUILayout.BeginVertical(GUILayout.Width(20));
            GUIContent buttonContent = EditorGUIUtility.IconContent("d_Refresh", "Zero out Local Position.");
            if (GUILayout.Button(buttonContent, EditorStyles.iconButton, GUILayout.Width(20), GUILayout.Height(20)))
            {
                string names = "";
                foreach (Transform transform in transformTargets)
                {
                    if (transform.localPosition == Vector3.zero) { continue;}
                    
                    Undo.RecordObject(transform, $"Local position reset for {transform.gameObject.name}");
                    transform.localPosition = Vector3.zero;
                    names += names == "" ? transform.gameObject.name : ", " + transform.gameObject.name;
                }
                if (names != "")
                {
                    AssignTransformEditor();
                    GgLogs.Log(this, GgLogType.Info, "Local position reset for {0}", names);
                }
            }
            GUILayout.Space(1);
            buttonContent = EditorGUIUtility.IconContent("d_Refresh", "Zero out Local Rotation.");
            if (GUILayout.Button(buttonContent, EditorStyles.iconButton, GUILayout.Width(20), GUILayout.Height(20)))
            {
                string names = "";
                foreach (Transform transform in transformTargets)
                {
                    if (transform.localEulerAngles == Vector3.zero) { continue;}
                    Undo.RecordObject(transform, $"Local rotation reset for {transform.gameObject.name}");
                    transform.localEulerAngles = Vector3.zero;
                    names += names == "" ? transform.gameObject.name : ", " + transform.gameObject.name;
                }
                if (names != "")
                {
                    AssignTransformEditor();
                    GgLogs.Log(this, GgLogType.Info, "Local rotation reset for {0}", names);
                }
            }
            GUILayout.Space(1);
            buttonContent = EditorGUIUtility.IconContent("d_Refresh", "Zero out Local Scale.");
            if (GUILayout.Button(buttonContent, EditorStyles.iconButton, GUILayout.Width(20), GUILayout.Height(20)))
            {
                string names = "";
                foreach (Transform transform in transformTargets)
                {
                    if (transform.localScale == Vector3.one) { continue;}
                    Undo.RecordObject(transform, $"Local scale reset for {transform.gameObject.name}");
                    transform.localScale = Vector3.one;
                    names += names == "" ? transform.gameObject.name : ", " + transform.gameObject.name;
                }
                if (names != "")
                {
                    AssignTransformEditor();
                    GgLogs.Log(this, GgLogType.Info, "Local scale reset for {0}", names);
                }
            }
            GUILayout.EndVertical();
            
            // end wrapped transform editor and custom reset buttons
            GUILayout.EndHorizontal();
        }

        private void OnInspectorGUI_TransformUtilities()
        {
            // get & update references
            Color defaultBackground = GUI.backgroundColor;
            
            // scale warning: top
            GUILayout.Space(2);
            DrawScaleWarning(transformTargets, 1, -2);
            
            // utilities
            EditorExtensions.BeginCustomInspectorBackground(InspectorExtensions.backgroundNormalColor, 1, -3);
            GUILayout.Space(2);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = new Color32(223, 223, 223, 079);
            if (GUILayout.Button(new GUIContent("Transform Utilities", "View extra readonly transform utility info."), GgGUI.StealthButtonStyle, GUILayout.Width(100), GUILayout.Height(10)))
            {
                utilitiesOpen = !utilitiesOpen;
            }
            GUI.backgroundColor = defaultBackground;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(2);
            if (utilitiesOpen)
            {
                GUILayout.Space(-1);
                EditorExtensions.DrawInspectorLineFull(InspectorExtensions.backgroundSeperatorColor, 2, 2);
                GUILayout.Space(2);
                GUI.enabled = false;
                GUILayout.Space(2);
                if (1 == transformTargets.Count && NonUniformScaleInObject(transformTargets[0]) || NonUniformScaleInParent(transformTargets))
                {
                    // non-uniform scale
                    GUI.enabled = true;
                    EditorGUILayout.HelpBox("Non-uniform scale detected. It is recommended to keep scale at '1, 1, 1' where possible.", MessageType.Warning);
                    GUI.enabled = false;
                }
                GUILayout.Space(2);
                OnInspectorGUI_GlobalPositions();
                EditorExtensions.DrawInspectorLineFull(InspectorExtensions.backgroundSeperatorColor, 2, 2);
                OnInspectorGUI_AssetTags();
                GUILayout.Space(2);
                GUI.enabled = true;
            }
            GUILayout.Space(2);
            EditorExtensions.EndCustomInspectorBackground();
            
            // scale warning: bottom
            DrawScaleWarning(transformTargets, 2, -3);
        }
        
        private void OnInspectorGUI_GlobalPositions()
        {
            bool defaultGUI = GUI.enabled;
            GUI.enabled = false;
            if (targets.Length <= 1)
            {
                // global transform properties
                EditorGUILayout.Vector3Field(new GUIContent("Global Position", "The world space position of the Transform."), transformTargets[0].position);
                EditorGUILayout.Vector3Field(new GUIContent("Global Rotation", "A Quaternion that stores the rotation of the Transform in world space."), transformTargets[0].eulerAngles);
                EditorGUILayout.Vector3Field(new GUIContent("Lossy Scale", "The global scale of the object (Read Only)."), transformTargets[0].lossyScale);
            }
            else
            {
                EditorGUILayout.LabelField("Global properties not available when multiple objects are selected.");
            }
            GUI.enabled = defaultGUI;
        }

        private void OnInspectorGUI_AssetTags()
        {
            bool defaultGUI = GUI.enabled;
            GUI.enabled = false;
            if (targets.Length <= 1)
            {
                // prefab labels
                EditorGUILayout.LabelField(new GUIContent("Asset Labels:", "If this gameObject is a prefab, all labels applied to it will show here."));
                Object sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(transformTargets[0].gameObject);
                string[] labels = EditorExtensions.GetAllObjectLabels(sourceObject);
                if (0 < labels.Length)
                {
                    float labelLineWidth = 0;
                    EditorGUILayout.BeginHorizontal();
                    Color defaultBackground = GUI.backgroundColor;
                    GUI.backgroundColor = new Color32(179, 179, 179, 255);
                    for (int i = 0; i < labels.Length; i++)
                    {
                        float labelWidth = StringExtensions.GetStringWidth(labels[i]) + (standardGap * 1.5f);
                        labelLineWidth += labelWidth + standardGap;
                        if (Screen.width <= labelLineWidth + standardGap)
                        {
                            GUILayout.FlexibleSpace();
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.BeginHorizontal();
                            labelLineWidth = labelWidth + standardGap;
                        }
                        GUILayout.Button(labels[i], GUILayout.Width(labelWidth));
                    }
                    GUILayout.FlexibleSpace();
                    GUI.backgroundColor = defaultBackground;
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.LabelField("n/a");
                }
            }
            else
            {
                EditorGUILayout.LabelField("Labels not available when multiple objects are selected.");
            }
            GUI.enabled = defaultGUI;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Methods

        private void ClearTransformEditor()
        {
            // clear target transforms
            transformTargets = new List<Transform>();
            
            // destroy editor instance if exists
            if (!transformEditor) { return; }
            MethodInfo disableMethod = transformEditor.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (disableMethod != null) { disableMethod?.Invoke(transformEditor, null); }
            DestroyImmediate(transformEditor);
            transformEditor = null;
        }
        
        private void AssignTransformEditor()
        {
            ClearTransformEditor();
            
            // null checks
            if (target == null || transformInspectorType == null) { return; }
            if (1 < targets.Length && targets[0] != null)
            {
                // assign selected targets
                transformTargets.Clear();
                foreach (Object targetObject in targets)
                {
                    Transform transform = targetObject as Transform;
                    if (transform && !transformTargets.Contains(transform))
                    {
                        transformTargets.Add(transform);
                    }
                }
                
                // assign transform editor
                transformEditor = CreateEditorWithContext(targets, target, transformInspectorType);
            }
            else
            {
                // assign selected targets
                transformTargets.Clear();
                Transform transform = target as Transform;
                transformTargets.Add(transform);
                
                // assign transform editor
                transformEditor = CreateEditor(target, transformInspectorType);
            }
        }
        
        private void DrawScaleWarning(List<Transform> transformTargets, float paddingTop = -4F, float paddingBottom = -15F, float paddingLeft = -18F, float paddingRight = -4F)
        {
            if (1 == transformTargets.Count && NonUniformScaleInObject(transformTargets[0]))
            {
                EditorExtensions.BeginCustomInspectorBackground(new Color32(223, 050, 050, 255), paddingTop, paddingBottom, paddingLeft, paddingRight);
                EditorExtensions.EndCustomInspectorBackground();
            }
            else if (NonUniformScaleInParent(transformTargets))
            {
                EditorExtensions.BeginCustomInspectorBackground(new Color32(179, 128, 000, 255), paddingTop, paddingBottom, paddingLeft, paddingRight);
                EditorExtensions.EndCustomInspectorBackground();
            }
            else
            {
                EditorExtensions.BeginCustomInspectorBackground(new Color32(028, 128, 028, 255), paddingTop, paddingBottom, paddingLeft, paddingRight);
                EditorExtensions.EndCustomInspectorBackground();
            }
        }
        
        private bool NonUniformScaleInParent(List<Transform> transformTargets)
        {
            foreach (Transform transformTarget in transformTargets)
            {
                if (transformTarget.lossyScale != Vector3.one) { return true; }
            }
            
            return false;
        }

        private bool NonUniformScaleInObject(Transform transformTarget)
        {
            return transformTarget.localScale != Vector3.one;
        }

		

		#endregion

    } // class end
}
#endif
