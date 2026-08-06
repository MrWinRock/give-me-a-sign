using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace MainMenu
{
    /// <summary>
    /// Start &gt; Turn Off Computer. The only way out of the game.
    ///   Stand By  - closes the dialog (does nothing else, exactly like the real thing on a desktop)
    ///   Turn Off  - the shutdown sequence, then Application.Quit()
    ///   Restart   - reloads the main menu scene
    /// </summary>
    public class TurnOffWindow : XPWindowController
    {
        [Header("Buttons")]
        [SerializeField] private Button standByButton;
        [SerializeField] private Button turnOffButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button cancelButton;

        // Each circular button is two stacked circle Images: the outer one is the 2px ring,
        // the inner one (inset 2px) is the fill.
        [Header("Stand By Colors")]
        [SerializeField] private Image standByFill;
        [SerializeField] private Image standByBorder;
        [SerializeField] private Color standByFillColor = XPPalette.Hex("#DCE8F8");
        [SerializeField] private Color standByBorderColor = XPPalette.Hex("#6A9AD8");

        [Header("Turn Off Colors")]
        [SerializeField] private Image turnOffFill;
        [SerializeField] private Image turnOffBorder;
        [SerializeField] private Color turnOffFillColor = XPPalette.Hex("#F8DCDC");
        [SerializeField] private Color turnOffBorderColor = XPPalette.Hex("#C66A6A");

        [Header("Restart Colors")]
        [SerializeField] private Image restartFill;
        [SerializeField] private Image restartBorder;
        [SerializeField] private Color restartFillColor = XPPalette.Hex("#DCF0DC");
        [SerializeField] private Color restartBorderColor = XPPalette.Hex("#6AC66A");

        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI standByLabel;
        [SerializeField] private TextMeshProUGUI turnOffLabel;
        [SerializeField] private TextMeshProUGUI restartLabel;
        [SerializeField] private string standByLabelText = "Stand By";
        [SerializeField] private string turnOffLabelText = "Turn Off";
        [SerializeField] private string restartLabelText = "Restart";
        [SerializeField] private Color labelColor = XPPalette.Hex("#003C74");

        protected override void Awake()
        {
            base.Awake();

            ApplyStyle();

            if (standByButton != null) standByButton.onClick.AddListener(Hide);
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
            if (turnOffButton != null) turnOffButton.onClick.AddListener(OnTurnOff);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
        }

        private void OnTurnOff()
        {
            Hide();
            Desktop?.QuitGame();
        }

        private void OnRestart()
        {
            Hide();
            Desktop?.RestartMenu();
        }

        private void ApplyStyle()
        {
            Paint(standByFill, standByFillColor, standByBorder, standByBorderColor, standByLabel, standByLabelText);
            Paint(turnOffFill, turnOffFillColor, turnOffBorder, turnOffBorderColor, turnOffLabel, turnOffLabelText);
            Paint(restartFill, restartFillColor, restartBorder, restartBorderColor, restartLabel, restartLabelText);
        }

        private void Paint(Image fill, Color fillColor, Image border, Color borderColor, TextMeshProUGUI label, string text)
        {
            if (fill != null) fill.color = fillColor;
            if (border != null) border.color = borderColor;
            if (label != null)
            {
                label.text = text;
                label.color = labelColor;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying)
                ApplyStyle();
        }
#endif
    }
}
