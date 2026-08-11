#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomPropertyDrawer(typeof(GgPose))]
    public class GgPoseDrawer : PropertyDrawer
    {
        #region variables

        private SerializedProperty position;
        private SerializedProperty eulerAngles;
        
        private float singleLine = EditorGUIUtility.singleLineHeight;
        private float spacing = EditorGUIUtility.standardVerticalSpacing;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Property Height
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (label.text == "")
            {
                return (singleLine + spacing) * 2;
            }
            
            return (singleLine + spacing) * 3;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGUI

        public override void OnGUI(Rect positionRect, SerializedProperty property, GUIContent label)
        {
            // open property and get reference to instance
            EditorGUI.BeginProperty(positionRect, label, property);
            
            // get reference to SerializeFields
            position = property.FindPropertyRelative("position");
            eulerAngles = property.FindPropertyRelative("eulerAngles");
            
            // calculate label size
            bool showMainLabel = (singleLine + spacing) * 2 < positionRect.height;
            float subLabelWidth = 55;
            float labelOffset = showMainLabel ? EditorGUIUtility.labelWidth : subLabelWidth;
            
            // calculate rects
            Rect mainLabelRect = new Rect(positionRect.x, positionRect.y, positionRect.width, singleLine);
            Rect posRect = new Rect(positionRect.x + labelOffset, showMainLabel ? (mainLabelRect.yMax + spacing) : mainLabelRect.yMin, positionRect.width - labelOffset, singleLine);
            Rect rotRect = new Rect(positionRect.x + labelOffset, posRect.yMax + spacing, positionRect.width - labelOffset, singleLine);
            
            Rect posLabelRect = new Rect(posRect.x - subLabelWidth, posRect.y, subLabelWidth, singleLine);
            Rect rotLabelRect = new Rect(rotRect.x - subLabelWidth, rotRect.y, subLabelWidth, singleLine);
            
            // draw property
            if (showMainLabel)
            {
                EditorGUI.PrefixLabel(mainLabelRect, new GUIContent(label.text, label.tooltip));
            }
            EditorGUI.LabelField(posLabelRect, "Position");
            EditorGUI.LabelField(rotLabelRect, "Rotation");
            if (GgGUI.Vector3Field(posRect, GUIContent.none, position.vector3Value, out Vector3 newPosition, position.hasMultipleDifferentValues))
            {
                position.vector3Value = newPosition;
            }
            if (GgGUI.Vector3Field(rotRect, GUIContent.none, eulerAngles.vector3Value, out Vector3 newEulerAngles, eulerAngles.hasMultipleDifferentValues))
            {
                eulerAngles.vector3Value = newEulerAngles;
            }
            
            // close property
            EditorGUI.EndProperty();
        }

        #endregion
        
    } // class end
}
#endif
