using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Classic WinForms GroupBox title "notch": sizes a background panel (colored to match
    /// the window background) to hug the title text width, so it sits on top of the
    /// GroupBox's border line and masks it out where the title reads.
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
