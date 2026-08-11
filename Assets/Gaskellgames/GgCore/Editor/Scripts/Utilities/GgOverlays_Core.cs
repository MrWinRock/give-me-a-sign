#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    [Overlay(typeof(SceneView), "GgCore")]
    public class GgOverlays_Core : ToolbarOverlay
    {
        GgOverlays_Core() : base(elementIds) { }
        
        private static string[] elementIds = 
        {
            GgToolbarIEditorUpdateCount.id,
            GgToolbarAddComment.id
        };
        
    } // class end
    
    //----------------------------------------------------------------------------------------------------
    
    #region Comment
    
    [EditorToolbarElement(id, typeof(SceneView))]
    public class GgToolbarAddComment : EditorToolbarButton
    {
        private const string packageRefName = "GgCore";
        private const string relativePath = "/Editor/Icons/";
        
        public const string id = "GGToolbar/AddComment";

        public GgToolbarAddComment()
        {
            if (!GgPackageRef.TryGetFullFilePath(packageRefName, relativePath, out string filePath)) { return; }
            icon = AssetDatabase.LoadAssetAtPath(filePath + "Icon_Comment.png", typeof(Texture2D)) as Texture2D;
            tooltip = "Add a comment at the scene view camera look at position.";
            clicked += OnClick;
        }

        private void OnClick()
        {
            // create comment at scene view look at
            EditorExtensions.GetSceneViewLookAt(out Vector3 point);
            GameObject go = new GameObject();
            go.name = "Comment";
            go.transform.position = point;
            go.AddComponent<Comment>();
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            // select and ping comment
            EditorGUIUtility.PingObject(go);
            Selection.activeObject = go;
        }

    } // class end
    
    #endregion
    
    //----------------------------------------------------------------------------------------------------
    
    #region GgToolbarIEditorUpdateCount
    
    [EditorToolbarElement(id, typeof(SceneView))]
    public class GgToolbarIEditorUpdateCount : EditorToolbarButton
    {
        public const string id = "GGToolbar/IEditorUpdateCount";

        public GgToolbarIEditorUpdateCount()
        {
            text = GgEditorUpdateLoop.iEditorUpdateList.Count.ToString();
            tooltip = "IEditorUpdate count: Click to refresh editor callbacks.";
            clicked += OnClick;
            
            GgEditorUpdateLoop.OnIEditorUpdateListUpdated += UpdateText;
        }

        private void OnClick()
        {
            GgEditorUpdateLoop.ForceUpdateComponentList();
        }

        private void UpdateText()
        {
            text = GgEditorUpdateLoop.iEditorUpdateList.Count.ToString();
        }

    } // class end
    
    #endregion
    
    //----------------------------------------------------------------------------------------------------
    
    // [EditorToolbarElement(id, typeof(SceneView))]
    // public class SceneViewZoom : EditorToolbarButton
    // {
    //     public const string id = "GGToolbar/SceneViewZoom";
    //
    //     public SceneViewZoom()
    //     {
    //         text = GetTextValue();
    //         tooltip = "SceneView Zoom: Click to refresh.";
    //         clicked += OnClick;
    //     }
    //
    //     private void OnClick()
    //     {
    //         text = GetTextValue();
    //     }
    //
    //     private string GetTextValue()
    //     {
    //         return SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.size.ToString() : "?";
    //     }
    //
    // } // class end
    
}

#endif