using GameLogic.Flow;
using GameLogic.Night;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Score
{
    /// <summary>
    /// Shows how the night went. Reads the single <see cref="NightResult"/> that
    /// <see cref="GameFlowManager"/> built, instead of reassembling it from four PlayerPrefs
    /// keys written by three different scripts.
    /// </summary>
    public class ResultDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI thresholdText; // Shows what score was needed to win

        [Header("Status Colors")]
        [SerializeField] private Color winColor = Color.green;
        [SerializeField] private Color loseColor = Color.red;

        [Header("Sprint 6 - Seed / Progression")]
        [Tooltip("Optional. Shows 'Seed 123456 · Night 2'.")]
        [SerializeField] private TextMeshProUGUI seedText;
        [Tooltip("Optional. Shows an 'unlocked Night N' message on a win, blank otherwise.")]
        [SerializeField] private TextMeshProUGUI progressionText;
        [Tooltip("Optional. Reloads the gameplay scene forced back onto this exact seed - same NightPlanProvider.ForcedSeed mechanism the debug Night Plan HUD (F3) already uses.")]
        [SerializeField] private Button replaySeedButton;
        [Tooltip("Optional. Resets progression to Night 1 and starts a fresh run - most relevant after finishing the campaign, but works from any Result screen.")]
        [SerializeField] private Button restartCampaignButton;

        [Header("Buttons")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button quitButton;

        [Header("Scene Names")]
        [SerializeField] private string gameSceneName = "GameScene"; // Scene 1 (Night gameplay)

        [Header("Anomaly Defeat Objects")]
        [SerializeField] private GameObject[] anomalyDefeatObjects; // Objects to activate when defeated by anomaly
        [SerializeField] private GameObject[] normalResultObjects; // Objects to activate for normal results

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private int _resultSeed;

        void Start()
        {
            Display(ResolveResult());
            SetupButtons();
        }

        private NightResult ResolveResult()
        {
            var result = GameFlowManager.LastResult;
            if (result != null) return result;

            Debug.LogWarning("ResultDisplay: no NightResult recorded (Result scene opened directly?). Showing placeholder values.", this);
            return NightResult.Dummy();
        }

        private void Display(NightResult result)
        {
            _resultSeed = result.seed;

            if (seedText != null)
                seedText.text = $"Seed {result.seed} · Night {result.nightIndex}";

            if (progressionText != null)
            {
                progressionText.text = result.IsCampaignComplete
                    ? "Campaign complete."
                    : result.Won
                        ? $"Night {GameFlowManager.UnlockedNightIndex} unlocked."
                        : string.Empty;
            }

            // Being caught reads as a different kind of ending from simply not scoring enough,
            // so it gets its own objects and copy.
            if (result.KilledByThreat)
            {
                SetTexts(
                    status: "YOU LOSE!",
                    statusColor: loseColor,
                    score: "You were consumed by the darkness...",
                    threshold: "?????????????????????????");

                SetActiveAll(anomalyDefeatObjects, true);
                SetActiveAll(normalResultObjects, false);
            }
            else if (result.IsCampaignComplete)
            {
                // Sprint 6, S-603: the designed 5-night arc ends here, not on a silently-scaling
                // night 6 - see NightResult.FinalNightIndex.
                SetTexts(
                    status: "YOU SURVIVED THE WEEK",
                    statusColor: winColor,
                    score: $"Final Score: {result.score}",
                    threshold: "Thank you for playing.");

                SetActiveAll(normalResultObjects, true);
                SetActiveAll(anomalyDefeatObjects, false);
            }
            else
            {
                SetTexts(
                    status: result.Won ? "YOU WIN!" : "YOU LOSE!",
                    statusColor: result.Won ? winColor : loseColor,
                    score: $"Final Score: {result.score}",
                    threshold: $"(Need {result.requiredScore} points to win)");

                SetActiveAll(normalResultObjects, true);
                SetActiveAll(anomalyDefeatObjects, false);
            }

            if (showDebugInfo)
            {
                Debug.Log(
                    $"Results: outcome={result.outcome}, score={result.score}/{result.requiredScore}, " +
                    $"won={result.Won}, reports={result.reportsFiled} ({result.reportsFailed} failed), " +
                    $"survivedUntil={result.survivedUntilHour:0.00}h, killedBy='{result.killedByAnomalyId ?? "-"}'.", this);
            }
        }

        private void SetTexts(string status, Color statusColor, string score, string threshold)
        {
            if (statusText != null)
            {
                statusText.text = status;
                statusText.color = statusColor;
            }

            if (scoreText != null) scoreText.text = score;
            if (thresholdText != null) thresholdText.text = threshold;
        }

        private void SetActiveAll(GameObject[] objects, bool active)
        {
            if (objects == null) return;

            foreach (var obj in objects)
            {
                if (obj == null) continue;

                obj.SetActive(active);

                if (active && showDebugInfo)
                    Debug.Log($"Activated result object: {obj.name}");
            }
        }

        private void SetupButtons()
        {
            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(PlayAgain);

            if (quitButton != null)
                quitButton.onClick.AddListener(QuitGame);

            if (replaySeedButton != null)
                replaySeedButton.onClick.AddListener(ReplaySeed);

            if (restartCampaignButton != null)
                restartCampaignButton.onClick.AddListener(RestartCampaign);
        }

        public void RestartCampaign()
        {
            if (showDebugInfo)
                Debug.Log("Restarting campaign from Night 1...");

            GameFlowManager.ResetProgression();
            GameFlowManager.StartNewNight(gameSceneName);
        }

        /// <summary>
        /// The one "continue" button. Hands back to the day loop rather than reloading the scene
        /// itself: a survived day goes on to the day-end event and the next day, a lost one
        /// retries the same day with a fresh roll.
        /// </summary>
        public void PlayAgain()
        {
            var flow = GameFlowManager.Instance;
            if (flow == null)
            {
                // Result scene opened directly while testing - nothing to continue into.
                Debug.LogWarning("ResultDisplay: no GameFlowManager - reloading the gameplay scene directly.", this);
                GameFlowManager.StartNewNight(gameSceneName);
                return;
            }

            if (showDebugInfo)
                Debug.Log("Continuing from result...");

            flow.ContinueFromResult();
        }

        public void ReplaySeed()
        {
            if (showDebugInfo)
                Debug.Log($"Replaying seed {_resultSeed}...");

            NightPlanProvider.ForcedSeed = _resultSeed;
            GameFlowManager.StartNewNight(gameSceneName);
        }

        public void QuitGame()
        {
            if (showDebugInfo)
                Debug.Log("Quitting game...");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Context menus for checking the layout without playing a whole night.
        [ContextMenu("Test Win Result")]
        public void TestWinResult() => Display(new NightResult
        {
            outcome = NightOutcome.Survived, score = 5, requiredScore = 3, survivedUntilHour = 6f,
        });

        [ContextMenu("Test Lose Result")]
        public void TestLoseResult() => Display(new NightResult
        {
            outcome = NightOutcome.Survived, score = 1, requiredScore = 3, survivedUntilHour = 6f,
        });

        [ContextMenu("Test Anomaly Defeat")]
        public void TestAnomalyDefeat() => Display(new NightResult
        {
            outcome = NightOutcome.KilledByAnomaly, score = 2, requiredScore = 3,
            survivedUntilHour = 3.4f, killedByAnomalyId = "shadow",
        });

        [ContextMenu("Test Demon Defeat")]
        public void TestDemonDefeat() => Display(new NightResult
        {
            outcome = NightOutcome.KilledByDemon, score = 2, requiredScore = 3,
            survivedUntilHour = 4.1f, killedByAnomalyId = "demon",
        });
    }
}
