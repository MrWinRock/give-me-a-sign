#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    [Overlay(typeof(SceneView), "Align Tools")]
    public class GgAlignTools : ToolbarOverlay
    {
        GgAlignTools() : base(elementIds) { }
        
        private static string[] elementIds = new string[]
        {
            GgToolbarAlignToolsX.id,
            GgToolbarAlignToolsY.id,
            GgToolbarAlignToolsZ.id,
            
            GgToolbarAlignToolsMin.id,
            GgToolbarAlignToolsMid.id,
            GgToolbarAlignToolsMax.id,
            GgToolbarAlignToolsDistribute.id
        };
        
    } // class end
    
    //----------------------------------------------------------------------------------------------------
    
    #region Align Tools: Axis
    
    [EditorToolbarElement(id, typeof(SceneView))]
    public class GgToolbarAlignToolsX : EditorToolbarButton
    {
        public const string id = "GGToolbar/AlignTools/X";
        
        private Texture2D onIcon;
        private Texture2D offIcon;
        
        public GgToolbarAlignToolsX()
        {
            onIcon = GgGUI.GetIcon("Icon_AxisX") as Texture2D;
            offIcon = GgGUI.GetIcon("Icon_AxisX_Greyscale") as Texture2D;
            GgAlignToolUtilities.onAxisChanged += OnAxisChanged;
            
            icon = (GgAlignToolUtilities.SelectedAxis == GgAlignToolUtilities.Axis.X) ? onIcon : offIcon;
            tooltip = "Set the alignment axis to X.";
            clicked += OnClick;
        }
        
        private void OnClick()
        {
            GgAlignToolUtilities.SelectedAxis = GgAlignToolUtilities.Axis.X;
        }
        
        private void OnAxisChanged(GgAlignToolUtilities.Axis axis)
        {
            icon = (axis == GgAlignToolUtilities.Axis.X) ? onIcon : offIcon;
        }
        
    } // class end
    
    [EditorToolbarElement(id, typeof(SceneView))]
    public class GgToolbarAlignToolsY : EditorToolbarButton
    {
        public const string id = "GGToolbar/AlignTools/Y";
        
        private Texture2D onIcon;
        private Texture2D offIcon;
        
        public GgToolbarAlignToolsY()
        {
            onIcon = GgGUI.GetIcon("Icon_AxisY") as Texture2D;
            offIcon = GgGUI.GetIcon("Icon_AxisY_Greyscale") as Texture2D;
            GgAlignToolUtilities.onAxisChanged += OnAxisChanged;
            
            icon = (GgAlignToolUtilities.SelectedAxis == GgAlignToolUtilities.Axis.Y) ? onIcon : offIcon;
            tooltip = "Set the alignment axis to Y.";
            clicked += OnClick;
        }
        
        private void OnClick()
        {
            GgAlignToolUtilities.SelectedAxis = GgAlignToolUtilities.Axis.Y;
        }
        
        private void OnAxisChanged(GgAlignToolUtilities.Axis axis)
        {
            icon = (axis == GgAlignToolUtilities.Axis.Y) ? onIcon : offIcon;
        }
        
    } // class end
    
    [EditorToolbarElement(id, typeof(SceneView))]
    public class GgToolbarAlignToolsZ : EditorToolbarButton
    {
        public const string id = "GGToolbar/AlignTools/Z";
        
        private Texture2D onIcon;
        private Texture2D offIcon;
        
        public GgToolbarAlignToolsZ()
        {
            onIcon = GgGUI.GetIcon("Icon_AxisZ") as Texture2D;
            offIcon = GgGUI.GetIcon("Icon_AxisZ_Greyscale") as Texture2D;
            GgAlignToolUtilities.onAxisChanged += OnAxisChanged;
            
            icon = (GgAlignToolUtilities.SelectedAxis == GgAlignToolUtilities.Axis.Z) ? onIcon : offIcon;
            tooltip = "Set the alignment axis to Z.";
            clicked += OnClick;
        }
        
        private void OnClick()
        {
            GgAlignToolUtilities.SelectedAxis = GgAlignToolUtilities.Axis.Z;
        }
        
        private void OnAxisChanged(GgAlignToolUtilities.Axis axis)
        {
            icon = (axis == GgAlignToolUtilities.Axis.Z) ? onIcon : offIcon;
        }
        
    } // class end

    #endregion
    
    //----------------------------------------------------------------------------------------------------
    
    #region Align Tools: Action
    
    [EditorToolbarElement(id, typeof(SceneView))]
    public class GgToolbarAlignToolsMin : EditorToolbarButton
    {
        public const string id = "GGToolbar/AlignTools/Min";
        
        public GgToolbarAlignToolsMin()
        {
            icon = GgGUI.GetIcon("Icon_AlignMin") as Texture2D;
            tooltip = "Align all selected targets to the min point on the selected Axis.";
            clicked += OnClick;
        }
        
        private void OnClick()
        {
            GgAlignToolUtilities.AlignMin(GgAlignToolUtilities.SelectedAxis);
            GgLogs.Log(null, GgLogType.Info, "Selected targets aligned to their min point in the {0} Axis.", GgAlignToolUtilities.SelectedAxis);
        }
        
    } // class end
    
    [EditorToolbarElement(id, typeof(SceneView))]
    public class GgToolbarAlignToolsMid : EditorToolbarButton
    {
        public const string id = "GGToolbar/AlignTools/Mid";
        
        public GgToolbarAlignToolsMid()
        {
            icon = GgGUI.GetIcon("Icon_AlignMid") as Texture2D;
            tooltip = "Align all selected targets to the mid point on the selected Axis.";
            clicked += OnClick;
        }
        
        private void OnClick()
        {
            GgAlignToolUtilities.AlignMid(GgAlignToolUtilities.SelectedAxis);
            GgLogs.Log(null, GgLogType.Info, "Selected targets aligned to their mid point in the {0} Axis.", GgAlignToolUtilities.SelectedAxis);
        }
        
    } // class end
    
    [EditorToolbarElement(id, typeof(SceneView))]
    public class GgToolbarAlignToolsMax : EditorToolbarButton
    {
        public const string id = "GGToolbar/AlignTools/Max";
        
        public GgToolbarAlignToolsMax()
        {
            icon = GgGUI.GetIcon("Icon_AlignMax") as Texture2D;
            tooltip = "Align all selected targets to the max point on the selected Axis.";
            clicked += OnClick;
        }
        
        private void OnClick()
        {
            GgAlignToolUtilities.AlignMax(GgAlignToolUtilities.SelectedAxis);
            GgLogs.Log(null, GgLogType.Info, "Selected targets aligned to their max point in the {0} Axis.", GgAlignToolUtilities.SelectedAxis);
        }
        
    } // class end
    
    [EditorToolbarElement(id, typeof(SceneView))]
    public class GgToolbarAlignToolsDistribute : EditorToolbarButton
    {
        public const string id = "GGToolbar/AlignTools/Distribute";
        
        public GgToolbarAlignToolsDistribute()
        {
            icon = GgGUI.GetIcon("Icon_AlignDis") as Texture2D;
            tooltip = "Distribute all selected targets evenly on the selected Axis.";
            clicked += OnClick;
        }
        
        private void OnClick()
        {
            GgAlignToolUtilities.AlignDistribute(GgAlignToolUtilities.SelectedAxis);
            GgLogs.Log(null, GgLogType.Info, "Selected targets distributed evenly in the {0} Axis.", GgAlignToolUtilities.SelectedAxis);
        }
        
    } // class end

    #endregion
    
}

#endif