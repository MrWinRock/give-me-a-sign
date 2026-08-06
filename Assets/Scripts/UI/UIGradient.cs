using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Vertical two- or three-stop gradient for any uGUI <see cref="Graphic"/> (Image, RawImage).
    /// Tints the generated mesh's vertex colours, so it costs no extra draw call and no texture.
    ///
    /// NOTE: this is an IMeshModifier, and TextMeshPro does NOT run mesh modifiers - put it on
    /// Images only. For gradient text use TMP's own vertex gradient instead.
    ///
    /// <see cref="midPosition"/> is measured FROM THE TOP (0 = top edge, 1 = bottom edge) so it
    /// matches how the XP palette is authored ("#1A2838 top -> #24384C at 45% -> #1E3326 bottom").
    /// </summary>
    [AddComponentMenu("UI/Effects/UI Gradient")]
    [DisallowMultipleComponent]
    public class UIGradient : BaseMeshEffect
    {
        [SerializeField] private Color topColor = Color.white;
        [SerializeField] private Color bottomColor = Color.gray;

        [Tooltip("Adds a third stop between top and bottom. Off = straight two-stop gradient.")]
        [SerializeField] private bool useMidColor;
        [SerializeField] private Color midColor = Color.white;
        [Tooltip("Position of the middle stop, measured from the TOP edge (0 = top, 1 = bottom).")]
        [Range(0f, 1f)] [SerializeField] private float midPosition = 0.45f;

        private static readonly List<UIVertex> Verts = new List<UIVertex>();

        public Color TopColor
        {
            get => topColor;
            set { topColor = value; Refresh(); }
        }

        public Color BottomColor
        {
            get => bottomColor;
            set { bottomColor = value; Refresh(); }
        }

        public Color MidColor
        {
            get => midColor;
            set { midColor = value; Refresh(); }
        }

        public bool UseMidColor
        {
            get => useMidColor;
            set { useMidColor = value; Refresh(); }
        }

        /// <summary>Position of the middle stop, measured from the TOP edge (0 = top, 1 = bottom).</summary>
        public float MidPosition
        {
            get => midPosition;
            set { midPosition = Mathf.Clamp01(value); Refresh(); }
        }

        /// <summary>Sets both stops at once (the common case) with a single mesh rebuild.</summary>
        public void SetColors(Color top, Color bottom)
        {
            topColor = top;
            bottomColor = bottom;
            Refresh();
        }

        public void Refresh()
        {
            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0)
                return;

            Verts.Clear();
            vh.GetUIVertexStream(Verts);

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < Verts.Count; i++)
            {
                float y = Verts[i].position.y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            float height = maxY - minY;
            if (height <= Mathf.Epsilon)
                return;

            for (int i = 0; i < Verts.Count; i++)
            {
                var v = Verts[i];
                float fromTop = 1f - Mathf.Clamp01((v.position.y - minY) / height);
                v.color = Evaluate(fromTop) * (Color)v.color;
                Verts[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(Verts);
        }

        private Color Evaluate(float fromTop)
        {
            if (!useMidColor)
                return Color.Lerp(topColor, bottomColor, fromTop);

            float mid = Mathf.Clamp(midPosition, 0.0001f, 0.9999f);
            return fromTop < mid
                ? Color.Lerp(topColor, midColor, fromTop / mid)
                : Color.Lerp(midColor, bottomColor, (fromTop - mid) / (1f - mid));
        }
    }
}
