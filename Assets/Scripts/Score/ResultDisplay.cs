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

        /// <summary>
        /// Falls back to a placeholder when there is no recorded night - that happens when the
        /// Result scene is opened directly in the editor, and a null-ref there is just noise.
        /// </summary>
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
                progressionText.text = result.Won
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
        }

        public void PlayAgain()
        {
            if (showDebugInfo)
                Debug.Log("Loading game scene...");

            GameFlowManager.StartNewNight(gameSceneName);
        }

        /// <summary>
        /// Sprint 6, S-607: forces the next night generated to reuse this exact seed - same
        /// mechanism (NightPlanProvider.ForcedSeed) the debug Night Plan HUD's "Replay THIS seed"
        /// button already uses. Note this pins the RNG stream only, not the night index: replaying
        /// an old seed after unlocking a later night generates that seed against the CURRENTLY
        /// unlocked night's difficulty, same as the debug tool - not a new limitation.
        /// </summary>
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
