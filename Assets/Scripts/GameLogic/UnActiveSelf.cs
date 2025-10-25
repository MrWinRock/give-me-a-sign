using UnityEngine;

namespace GameLogic
{
    public class UnActiveSelf : MonoBehaviour
    {
        [Header("Deactivation Settings")]
        [SerializeField] private float deactivateDelay = 4f; // Time in seconds before deactivating

        // OnEnable is called every time the GameObject becomes active
        void OnEnable()
        {
            // Cancel any previous invoke to avoid multiple timers
            CancelInvoke("DeactivateSelf");
            // Schedule new deactivation after specified delay
            Invoke("DeactivateSelf", deactivateDelay);
        }

        void OnDisable()
        {
            // Cancel the invoke when disabled to clean up
            CancelInvoke("DeactivateSelf");
        }

        private void DeactivateSelf()
        {
            gameObject.SetActive(false);
        }
    }
}
