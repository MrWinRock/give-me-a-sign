using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>Opens the scene's Field Manual when this Button is clicked (e.g. the Incident Report's Help menu item).</summary>
    [RequireComponent(typeof(Button))]
    public class OpenFieldManualOnClick : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        private static void OnClick()
        {
            var manual = FindFirstObjectByType<FieldManualUI>(FindObjectsInactive.Include);
            if (manual != null) manual.Open();
        }
    }
}
