using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// Shows the (normally hidden) info panel whenever an anomaly is driven off.
    /// Keep this on an ACTIVE object (e.g. the Canvas) - the panel itself starts inactive.
    /// The panel's own UnActiveSelf hides it again after its delay.
    /// </summary>
    public class AnomalyBanishedNotice : MonoBehaviour
    {
        [Tooltip("The InfoTextPanel to show. Its text is NOT touched here - edit it by hand.")]
        [SerializeField] private GameObject panel;

        void OnEnable() => Anomaly.OnAnyAnomalyDisappeared += Show;
        void OnDisable() => Anomaly.OnAnyAnomalyDisappeared -= Show;

        private void Show(Anomaly _)
        {
            if (panel == null) return;
            panel.SetActive(false); // restart UnActiveSelf's timer if it is still showing
            panel.SetActive(true);
        }
    }
}
