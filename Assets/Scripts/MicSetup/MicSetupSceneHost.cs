using System.Collections;
using GameLogic.Save;
using GameLogic.Story;
using UnityEngine;

namespace MicSetup
{
    /// <summary>Hosts the setup wizard in the MicSetup scene (first launch), then plays the opening cutscene once and sends the player to the main menu.</summary>
    public class MicSetupSceneHost : MonoBehaviour
    {
        [SerializeField] private MicSetupWizard wizard;

        [Tooltip("Plays once, right after the microphone setup and before the main menu. Empty = straight to the menu.")]
        [SerializeField] private CinematicData openingCutscene;

        void Start()
        {
            // First launch only: a build always opens on this scene, so an already-calibrated player is sent on to the menu.
            if (StartupRouter.ShouldSkipSetupScene())
            {
                StartupRouter.LoadMainMenu();
                return;
            }

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

        private void OnWizardClosed(MicSetupWizard.Result result) => StartCoroutine(FinishSetup());

        private IEnumerator FinishSetup()
        {
            // The played flag also keeps it from repeating if the setup comes back (calibration version bump).
            if (openingCutscene != null && openingCutscene.IsPlayable()
                && !SaveManager.Current.IsCinematicPlayed(openingCutscene.cinematicId))
            {
                var player = GetComponent<CinematicPlayer>();
                if (player == null) player = gameObject.AddComponent<CinematicPlayer>();

                bool done = false;
                player.Play(openingCutscene, () =>
                {
                    ScreenFade.CutToBlack(); // same frame the cutscene UI is torn down: no flash of the setup scene
                    done = true;
                });
                while (!done) yield return null;
            }

            StartupRouter.LoadMainMenu();
        }
    }
}
