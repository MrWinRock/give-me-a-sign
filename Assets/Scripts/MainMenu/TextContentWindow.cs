using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// A read-only XP dialog whose entire content lives in serialized strings, so the copy can be
    /// rewritten in the Inspector without touching code. Four windows use it:
    /// </summary>
    public class TextContentWindow : XPWindowController
    {
        [Header("Body")]
        [SerializeField] private TextMeshProUGUI bodyText;
        [TextArea(4, 20)]
        [SerializeField] private string content =
            "NIGHT SHIFT PROTOCOL\n" +
            "1. Monitor all cameras.\n" +
            "2. Report anomalies via form.\n" +
            "3. Do not leave the terminal.\n" +
            "4. Do not answer if it speaks first.\n" +
            "— Management";

        [Header("Footer (optional greyed line)")]
        [SerializeField] private TextMeshProUGUI footerText;
        [TextArea(1, 4)]
        [SerializeField] private string footerContent = "";
        [SerializeField] private Color footerColor = XPPalette.Hex("#808080");

        [Header("Confirm Button")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI confirmLabel;
        [SerializeField] private string confirmLabelText = "OK";

        protected override void Awake()
        {
            base.Awake();

            ApplyContent();

            if (confirmButton != null)
                confirmButton.onClick.AddListener(Hide);
        }

        protected override void OnShown()
        {
            // Re-applied on every open so live Inspector edits show up without a domain reload.
            ApplyContent();
        }

        private void ApplyContent()
        {
            if (bodyText != null)
                bodyText.text = content;

            if (footerText != null)
            {
                footerText.text = footerContent;
                footerText.color = footerColor;
                footerText.gameObject.SetActive(!string.IsNullOrEmpty(footerContent));
            }

            if (confirmLabel != null)
                confirmLabel.text = confirmLabelText;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying)
                ApplyContent();
        }
#endif
    }
}
