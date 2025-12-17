using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pray
{
    public class PrayUiManager : MonoBehaviour
    {
        [Header("Prayer UI Components")]
        [SerializeField] private GameObject prayPanel;
        [SerializeField] private Image prayImage1;
        [SerializeField] private Image prayImage2;
        [SerializeField] private TextMeshProUGUI instructionText;
    
        [Header("Prayer Settings")]
        [SerializeField] private string defaultInstructionText = "Press SPACEBAR to pray and banish the anomaly!";

        void Start()
        {
            // Initially hide the pray panel
            if (prayPanel != null)
                prayPanel.SetActive(false);
            
            // Set default instruction text
            if (instructionText != null)
                instructionText.text = defaultInstructionText;
        }

        public void ShowPrayPanel()
        {
            if (prayPanel != null)
                prayPanel.SetActive(true);
        }

        public void HidePrayPanel()
        {
            if (prayPanel != null)
                prayPanel.SetActive(false);
        }

        public void SetInstructionText(string text)
        {
            if (instructionText != null)
                instructionText.text = text;
        }

        public bool IsPrayPanelActive()
        {
            return prayPanel != null && prayPanel.activeInHierarchy;
        }
    }
}
