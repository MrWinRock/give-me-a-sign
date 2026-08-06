using System.Collections;
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
    ///
    /// Before this existed, Anomaly, DemonAnomaly and ScoreManager each wrote the same four
    /// PlayerPrefs keys and loaded the Result scene themselves, so ScoreManager had to inspect
    /// an "AnomalyTimeout" flag to find out whether one of the others had got there first.
    /// Adding a new way to lose meant editing Anomaly.cs; now it means calling EndNight with a
    /// new outcome.
    ///
    /// No scene wiring required - the first call to <see cref="Instance"/> creates one if the
    /// scene doesn't contain it. Drop the component in the scene when you want to tune its
    /// Inspector values.
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

        /// <summary>
        /// The scene's instance, created on demand so nothing breaks if it was never placed.
        /// Returns null outside Play mode rather than littering the scene with objects.
        /// </summary>
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

        /// <summary>
        /// The night that just finished. Static, so it survives the load into the Result scene
        /// without going through PlayerPrefs. Null when no night has been played this session
        /// (e.g. the Result scene was opened directly while testing).
        /// </summary>
        public static NightResult LastResult { get; private set; }

        /// <summary>Which night is being played. Sprint 2's night plans set this; defaults to the first night.</summary>
        public static int CurrentNightIndex { get; set; } = 1;

        /// <summary>Seed the current night was generated from. Sprint 2 sets this; 0 means "not procedurally generated".</summary>
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

        /// <summary>
        /// Ends the night. Safe to call from several places at once - only the first call counts,
        /// which is what stops a dying anomaly and the 6:00 AM clock from fighting over the result.
        /// </summary>
        public void EndNight(NightOutcome outcome, string causeAnomalyId = null, string causeRoomId = null)
        {
            if (_ending) return;
            _ending = true;

            // Always close the report window first. Loading a scene out from under a live
            // WhisperMicInput recording used to hang the game and leave stale HUD elements
            // behind; IncidentReportUI.Hide() stops the mic synchronously, so doing this
            // before the load keeps that path clean.
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

        /// <summary>Sprint 6, S-606: fade to black + a cause-of-death line, held for delayAfterDeath seconds.</summary>
        private IEnumerator PlayDeathSequence(NightOutcome outcome)
        {
            var hud = DeathSequenceHud.Create();
            string cause = DescribeCause(outcome);

            const float fadeDuration = 0.6f;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / fadeDuration);
                hud.SetFade(p);
                hud.SetCause(cause, p);
                yield return null;
            }

            hud.SetFade(1f);
            hud.SetCause(cause, 1f);

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

        /// <summary>Called by the Result scene's Play Again button so the next night starts clean.</summary>
        public static void ClearLastResult() => LastResult = null;

        /// <summary>
        /// Sprint 6, S-603/S-605: the furthest night unlocked so far - the same PlayerPrefs key
        /// NightPlanRunner already reads when nothing overrides it. Static read API so nothing
        /// outside this file needs to know the PlayerPrefs key by name (same pattern as
        /// ControlPanelWindow's static settings getters).
        /// </summary>
        public static int UnlockedNightIndex =>
            Mathf.Max(1, PlayerPrefs.GetInt(GameLogic.Night.NightPlanRunner.UnlockedNightKey, 1));

        /// <summary>
        /// Advances the unlocked-night save on a win, so the next "Play Again" naturally continues
        /// to the next night - NightPlanRunner.ResolveNightIndex() already reads the same key, so
        /// no separate "Continue" flow is needed. A loss leaves the key untouched, which is exactly
        /// "Play Again" retrying the night that was just failed.
        /// </summary>
        private static void AdvanceProgression(int completedNightIndex)
        {
            int nextNight = completedNightIndex + 1;
            int unlocked = PlayerPrefs.GetInt(GameLogic.Night.NightPlanRunner.UnlockedNightKey, 1);
            if (nextNight <= unlocked) return;

            PlayerPrefs.SetInt(GameLogic.Night.NightPlanRunner.UnlockedNightKey, nextNight);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Drops the finished night and loads the gameplay scene. Kept here so scene loading stays
        /// in one place rather than being duplicated across the result UI.
        /// </summary>
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
