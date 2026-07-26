using TMPro;
using UnityEngine;

namespace UIHelpers
{
    /// <summary>
    /// Classic WinForms GroupBox title "notch": sizes a background panel (colored to match
    /// the window background) to hug the title text width, so it sits on top of the
    /// GroupBox's border line and masks it out where the title reads.
    ///
    /// Setup: put this on the title label's parent panel. Assign the label and the panel's
    /// own RectTransform (or a dedicated background Image's RectTransform). The panel should
    /// be positioned so its vertical center lines up with the GroupBox top border.
    /// </summary>
    [ExecuteAlways]
    public class XPGroupBoxTitle : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private RectTransform backgroundPanel;
        [SerializeField] private float horizontalPadding = 6f;

        void OnEnable()
        {
            Refresh();
        }

        [ContextMenu("Refresh")]
        public void Refresh()
        {
            if (titleText == null || backgroundPanel == null) return;

            var size = backgroundPanel.sizeDelta;
            size.x = titleText.preferredWidth + horizontalPadding * 2f;
            backgroundPanel.sizeDelta = size;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            Refresh();
        }
#endif
    }
}
