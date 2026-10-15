using UnityEngine;

namespace MicSetup
{
    /// <summary>Hosts the setup wizard in the MicSetup scene (first launch) and sends the player to the main menu when it closes.</summary>
    public class MicSetupSceneHost : MonoBehaviour
    {
        [SerializeField] private MicSetupWizard wizard;

        void Start()
        {
            if (wizard == null)
            {
                Debug.LogError("MicSetupSceneHost: no wizard assigned - going straight to the main menu.", this);
                StartupRouter.LoadMainMenu();
                return;
            }

            wizard.Closed += OnWizardClosed;
            wizard.Open(MicSetupWizard.WizardMode.Scene);
        }

        void OnDestroy()
        {
            if (wizard != null) wizard.Closed -= OnWizardClosed;
        }

        private void OnWizardClosed(MicSetupWizard.Result result) => StartupRouter.LoadMainMenu();
    }
}
