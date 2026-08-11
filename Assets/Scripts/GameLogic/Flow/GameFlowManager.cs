using System.Collections;
using DG.Tweening;
using GameLogic.Night;
using GameLogic.SpawnAndTime;
using Report;
using Score;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic.Flow
{
    /// <summary>
    /// The single owner of "the night is over". Anomalies, the demon and the clock all just
    /// report what happened; this decides the outcome, records it, and moves to the Result
    /// scene.
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        [Header("Result Scene")]
        [Tooltip("Scene loaded when the night ends. Must be in Build Settings.")]
        [SerializeField] private string resultSceneName = "Result";
        [Tooltip("Fallback build index used if the scene name can't be loaded.")]
        [SerializeField] private int resultSceneIndex = 2;

        [Header("Pacing")]
        [Tooltip("Pause after surviving to 6:00 AM before the Result scene loads.")]
        [SerializeField] private float delayAfterSurviving = 1f;
        [Tooltip("Total time the death sequence (fade + cause-of-death line, see DeathSequenceHud) holds before the Result scene loads.")]
        [SerializeField] private float delayAfterDeath = 2.5f;

        [Header("Debug")]
        [SerializeField] private bool showDebugInfo;

        private static GameFlowManager _instance;

        public static GameFlowManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (!Application.isPlaying) return null;

                _instance = FindFirstObjectByType<GameFlowManager>();
                if (_instance == null)
                {
                    var host = new GameObject("GameFlowManager (auto-created)");
                    _instance = host.AddComponent<GameFlowManager>();
                }
                return _instance;
            }
        }

        public static NightResult LastResult { get; private set; }

        public static int CurrentNightIndex { get; set; } = 1;

        public static int CurrentSeed { get; set; }

        private bool _ending;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("Multiple GameFlowManager instances found! Destroying duplicate.", this);
                Destroy(this);
                return;
            }

            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public void EndNight(NightOutcome outcome, string causeAnomalyId = null, string causeRoomId = null)
        {
            if (_ending) return;
            _ending = true;

            // Close the report first - loading a scene out from under a live mic recording hangs
            // the game. Hide() stops the mic synchronously.
            var reportManager = IncidentReportManager.Instance;
            if (reportManager != null && reportManager.IsReportOpen)
                reportManager.CancelReport();

            LastResult = BuildResult(outcome, causeAnomalyId, causeRoomId);

            if (LastResult.Won)
                AdvanceProgression(LastResult.nightIndex);

            if (showDebugInfo)
            {
                Debug.Log(
                    $"GameFlowManager: night ended as {outcome}. " +
                    $"Score {LastResult.score}/{LastResult.requiredScore}, won={LastResult.Won}, " +
                    $"cause='{causeAnomalyId ?? "-"}' in room '{causeRoomId ?? "-"}'.", this);
            }

            StartCoroutine(PlayEndingThenLoad(outcome));
        }

        private NightResult BuildResult(NightOutcome outcome, string causeAnomalyId, string causeRoomId)
        {
            var scoreManager = ScoreManager.Instance;
            var nightTimer = FindFirstObjectByType<NightTimer>();
            var scheduler = FindFirstObjectByType<AnomalyScheduler>();
            var reportManager = IncidentReportManager.Instance;
            var plan = NightPlanProvider.HasPlan ? NightPlanProvider.Current : null;

            return new NightResult
            {
                outcome = outcome,
                nightIndex = plan != null ? plan.nightIndex : CurrentNightIndex,
                seed = plan != null ? plan.seed : CurrentSeed,

                score = scoreManager != null ? scoreManager.GetCurrentScore() : 0,

                // Straight from the plan that placed the anomalies, so the bar and the content
                // behind it are two views of one object and cannot disagree.
                requiredScore = plan != null ? plan.requiredScore : 0,

                anomaliesTotal = scheduler != null ? scheduler.TotalSpawned : 0,
                reportsFiled = reportManager != null ? reportManager.ReportsFiled : 0,
                reportsFailed = reportManager != null ? reportManager.ReportsFailed : 0,
                survivedUntilHour = nightTimer != null ? nightTimer.GetGameTimeHours() : 0f,

                killedByAnomalyId = causeAnomalyId,
                killedInRoomId = causeRoomId,
            };
        }

        private IEnumerator PlayEndingThenLoad(NightOutcome outcome)
        {
            if (outcome == NightOutcome.Survived)
            {
                if (delayAfterSurviving > 0f)
                    yield return new WaitForSeconds(delayAfterSurviving);
            }
            else
            {
                yield return PlayDeathSequence(outcome);
            }

            LoadResultScene();
        }

        private IEnumerator PlayDeathSequence(NightOutcome outcome)
        {
            var hud = DeathSequenceHud.Create();

            const float fadeDuration = 0.6f;
            yield return hud.PlayFadeIn(DescribeCause(outcome), fadeDuration).WaitForCompletion();

            float hold = Mathf.Max(0f, delayAfterDeath - fadeDuration);
            if (hold > 0f)
                yield return new WaitForSecondsRealtime(hold);

            hud.Destroy();
        }

        private string DescribeCause(NightOutcome outcome)
        {
            switch (outcome)
            {
                case NightOutcome.KilledByDemon:
                    return "THE DEMON FOUND YOU.";

                case NightOutcome.Negligence:
                    // Only SilenceProtocolHaunt raises this outcome today, always with this cause id.
                    return LastResult != null && LastResult.killedByAnomalyId == "silence_protocol"
                        ? "IT HEARD YOU."
                        : "NEGLIGENCE.";

                case NightOutcome.KilledByAnomaly:
                    return LastResult != null && !string.IsNullOrEmpty(LastResult.killedInRoomId)
                        ? $"IT CAUGHT YOU IN THE {LastResult.killedInRoomId.ToUpperInvariant()}."
                        : "IT CAUGHT YOU.";

                default:
                    return "YOU DID NOT SURVIVE.";
            }
        }

        private void LoadResultScene()
        {
            if (!string.IsNullOrWhiteSpace(resultSceneName) &&
                Application.CanStreamedLevelBeLoaded(resultSceneName))
            {
                SceneManager.LoadScene(resultSceneName);
                return;
            }

            if (resultSceneIndex >= 0 && resultSceneIndex < SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogWarning(
                    $"GameFlowManager: scene '{resultSceneName}' is not in Build Settings - " +
                    $"falling back to build index {resultSceneIndex}.", this);
                SceneManager.LoadScene(resultSceneIndex);
                return;
            }

            Debug.LogError(
                $"GameFlowManager: cannot load the Result scene. '{resultSceneName}' is not in " +
                $"Build Settings and index {resultSceneIndex} is out of range.", this);
        }

        public static void ClearLastResult() => LastResult = null;

        public static int UnlockedNightIndex =>
            Mathf.Max(1, PlayerPrefs.GetInt(GameLogic.Night.NightPlanRunner.UnlockedNightKey, 1));

        private static void AdvanceProgression(int completedNightIndex)
        {
            int nextNight = Mathf.Min(completedNightIndex + 1, NightResult.FinalNightIndex);
            int unlocked = PlayerPrefs.GetInt(GameLogic.Night.NightPlanRunner.UnlockedNightKey, 1);
            if (nextNight <= unlocked) return;

            PlayerPrefs.SetInt(GameLogic.Night.NightPlanRunner.UnlockedNightKey, nextNight);
            PlayerPrefs.Save();
        }

        public static void ResetProgression()
        {
            PlayerPrefs.SetInt(GameLogic.Night.NightPlanRunner.UnlockedNightKey, 1);
            PlayerPrefs.Save();
        }

        public static void StartNewNight(string gameplaySceneName)
        {
            ClearLastResult();

            // Drop the finished plan so NightPlanRunner rolls a fresh night rather than the
            // schedulers picking up the one that just ended.
            NightPlanProvider.Clear();

            if (string.IsNullOrWhiteSpace(gameplaySceneName))
            {
                Debug.LogError("GameFlowManager.StartNewNight: no gameplay scene name given.");
                return;
            }

            SceneManager.LoadScene(gameplaySceneName);
        }
    }
}
