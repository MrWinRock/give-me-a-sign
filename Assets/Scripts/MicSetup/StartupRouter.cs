using GameLogic.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using Whisper;

namespace MicSetup
{
    /// <summary>
    /// The single startup decision: when the game starts on the main menu and the microphone has never been set up
    /// (or the calibration version is outdated), go through the MicSetup scene first. Scene names come from MicCalibrationConfig.
    /// </summary>
    public static class StartupRouter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RouteFirstLaunch()
        {
            var config = MicCalibrationStore.Config;

            // Only when the player starts on the menu - opening GamePlay etc. directly in the Editor is left alone.
            if (SceneManager.GetActiveScene().name != config.mainMenuSceneName) return;
            if (MicCalibrationStore.IsSetupCompleted) return;

            if (!Application.CanStreamedLevelBeLoaded(config.micSetupSceneName))
            {
                Debug.LogWarning($"StartupRouter: scene '{config.micSetupSceneName}' is not in Build Settings - skipping the microphone setup.");
                return;
            }

            SceneManager.LoadScene(config.micSetupSceneName);
        }

        /// <summary>Set by the menu's Restart button so the setup scene runs even though the mic is already calibrated.</summary>
        public static bool ForceSetupOnce;

        /// <summary>Set by Restart so the opening cutscene plays again even though it was already seen.</summary>
        public static bool ReplayCutsceneOnce;

        /// <summary>True when the MicSetup scene should skip straight to the menu (already set up, not asked to re-run).</summary>
        public static bool ShouldSkipSetupScene()
        {
            bool force = ForceSetupOnce;
            ForceSetupOnce = false;
            return !force && MicCalibrationStore.IsSetupCompleted;
        }

        // Back to the menu once the wizard is done. Uses the flow manager when one already exists (it would not on a first launch).
        public static void LoadMainMenu()
        {
            var flow = Object.FindFirstObjectByType<GameFlowManager>();
            if (flow != null)
            {
                flow.LoadMainMenu();
                return;
            }

            SceneManager.LoadScene(MicCalibrationStore.Config.mainMenuSceneName);
        }
    }
}
