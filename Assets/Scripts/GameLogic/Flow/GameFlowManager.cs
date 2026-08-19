using System.Collections;
using DG.Tweening;
using GameLogic.Data;
using GameLogic.Night;
using GameLogic.Save;
using GameLogic.SpawnAndTime;
using Report;
using Score;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace GameLogic.Flow
{
    /// <summary>
    /// Owns the whole campaign loop: which day it is, when a day ends, what happens between
    /// days, and when the run is over.
    ///
    /// Anomalies, the demon and the clock only report what happened - this decides the outcome,
    /// records it, and drives the state machine
    /// MainMenu -> DayGameplay -> DayEndEvent -> MainMenu(next day) -> ... -> Ending -> reset.
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        [Header("Scenes")]
        [Tooltip("The XP desktop / main menu scene, returned to between days.")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [Tooltip("The Incident Report gameplay scene.")]
        [SerializeField] private string gameplaySceneName = "GamePlay";

        [Header("Campaign")]
        [Tooltip("How many days a full run lasts.")]
        [Min(1)] [SerializeField] private int finalDay = NightResult.FinalNightIndex;

        [Tooltip("Survive scene loads. Needed because the day-end event and ending run as coroutines that outlive a scene. Put this component on its own GameObject if enabled.")]
        [SerializeField] private bool persistAcrossScenes = true;

        [Header("Day-End Event")]
        [Tooltip("Rolls the optional Short VDO / Minigame. Auto-found if left empty; no event plays without one.")]
        [SerializeField] private RandomEventDirector randomEventDirector;

        [Tooltip("Plays the rolled Short VDO fullscreen. Auto-found if left empty; VDOs are skipped without one.")]
        [SerializeField] private DayEventPlayer dayEventPlayer;

        [Header("Ending")]
        [Tooltip("Plays the Day 7 ending. Auto-found if left empty; the ending is skipped without one.")]
        [SerializeField] private EndingSequenceController endingSequence;

        [Header("Pacing")]
        [Tooltip("Pause after surviving to 6:00 AM before moving on to the day-end event.")]
        [SerializeField] private float delayAfterSurviving = 1f;
        [Tooltip("Total time the death sequence (fade + cause-of-death line, see DeathSequenceHud) holds before the day restarts.")]
        [SerializeField] private float delayAfterDeath = 2.5f;

        // Concrete subclass, not UnityEvent<int> directly: Unity only serializes a generic
        // UnityEvent through a named [Serializable] type, and without it these would compile but
        // never appear in the Inspector.
        [System.Serializable] public class DayEvent : UnityEvent<int> { }

        [Header("Transition Events")]
        [Tooltip("Fired with the day number whenever a day's gameplay begins (including a retry).")]
        public DayEvent OnDayStarted = new DayEvent();
        [Tooltip("Fired with the day number when a day is survived.")]
        public DayEvent OnDayEnded = new DayEvent();
        [Tooltip("Fired with the day number when a day is lost, just before it restarts.")]
        public DayEvent OnDayLost = new DayEvent();
        [Tooltip("Fired when the final day is cleared, before the ending plays.")]
        public UnityEvent OnDay7Complete = new UnityEvent();
        [Tooltip("Fired as the ending begins.")]
        public UnityEvent OnGameWon = new UnityEvent();

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

        /// <summary>
        /// Which day of the run is being played (1..finalDay). Read-only to the outside world -
        /// only AdvanceDay and the save file move it. Backed by the save so it survives a quit.
        /// </summary>
        public static int CurrentDay => Mathf.Max(1, SaveManager.Current.currentDay);

        /// <summary>Kept as the name the night-generation systems already use. Same number as CurrentDay.</summary>
        public static int CurrentNightIndex => CurrentDay;

        public static int CurrentSeed { get; set; }

        public static GameFlowState State { get; private set; } = GameFlowState.MainMenu;

        /// <summary>Day the run ends on. Static mirror of the serialized field, for callers with no instance.</summary>
        public static int FinalDay { get; private set; } = NightResult.FinalNightIndex;

        private bool _ending;

        // ── Day loop ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Not part of the automatic flow (EndNight -> FinishDayFromOutcome already calls
        /// EndDayGameplay directly). Kept for the Result scene as a manual/debug entry point -
        /// opening it directly and pressing Play Again resumes the day loop instead of hanging.
        /// </summary>
        public void ContinueFromResult()
        {
            bool survived = LastResult != null && LastResult.Won;
            EndDayGameplay(survived);
        }

        /// <summary>Enters the gameplay scene for CurrentDay.</summary>
        public void StartDayGameplay()
        {
            State = GameFlowState.DayGameplay;
            _ending = false; // the guard is per-day; a persistent instance would keep the last day's
            ClearLastResult();

            // A fresh plan for every attempt - see RestartCurrentDay for why this matters.
            NightPlanProvider.Clear();

            if (showDebugInfo)
                Debug.Log($"GameFlowManager: starting day {CurrentDay}.", this);

            OnDayStarted?.Invoke(CurrentDay);
            LoadSceneByName(gameplaySceneName, "gameplay");
        }

        /// <summary>
        /// Called by the anomaly/incident systems when a day finishes. Survived days go to the
        /// day-end event; lost days restart the same day without advancing.
        /// </summary>
        public void EndDayGameplay(bool survived)
        {
            if (showDebugInfo)
                Debug.Log($"GameFlowManager: day {CurrentDay} ended, survived={survived}.", this);

            if (!survived)
            {
                OnDayLost?.Invoke(CurrentDay);
                RestartCurrentDay();
                return;
            }

            OnDayEnded?.Invoke(CurrentDay);
            StartCoroutine(RunDayEndEvent());
        }

        /// <summary>
        /// Replays the current day with everything re-rolled. The day counter does NOT move.
        ///
        /// Clearing ForcedSeed is the part that actually matters: without it a night replayed
        /// after using "Replay this seed" would deal the player the identical anomaly and glitch
        /// sequence they just lost to.
        /// </summary>
        public void RestartCurrentDay()
        {
            _ending = false;
            NightPlanProvider.Clear();
            NightPlanProvider.ForcedSeed = null;

            if (showDebugInfo)
                Debug.Log($"GameFlowManager: restarting day {CurrentDay} with a fresh roll.", this);

            State = GameFlowState.DayGameplay;
            OnDayStarted?.Invoke(CurrentDay);
            LoadSceneByName(gameplaySceneName, "gameplay");
        }

        /// <summary>
        /// Moves to the next day, checkpoints the save, and returns to the menu. On the final day
        /// this hands over to the ending instead.
        /// </summary>
        public void AdvanceDay()
        {
            if (CurrentDay >= FinalDay)
            {
                OnDay7Complete?.Invoke();
                StartCoroutine(RunEnding());
                return;
            }

            SaveManager.Current.currentDay = CurrentDay + 1;
            SaveManager.Save();

            State = GameFlowState.MainMenu;

            if (showDebugInfo)
                Debug.Log($"GameFlowManager: advanced to day {CurrentDay} (checkpointed).", this);

            LoadSceneByName(mainMenuSceneName, "main menu");
        }

        /// <summary>
        /// DayEndEvent state: roll for a VDO/Minigame, play it, mark it consumed, then advance.
        /// An empty or exhausted pool simply advances immediately - that is the designed
        /// behaviour, not an error.
        /// </summary>
        private IEnumerator RunDayEndEvent()
        {
            State = GameFlowState.DayEndEvent;

            var director = ResolveEventDirector();
            if (director != null &&
                director.TryGetDayEndEvent(CurrentDay, out DayEventType type, out DayEventData data))
            {
                yield return PlayDayEndEvent(type, data);

                // Only consumed once it has actually finished, so quitting mid-event doesn't burn it.
                director.MarkConsumed(data);
            }

            AdvanceDay();
        }

        /// <summary>
        /// Runs the rolled event and waits for it to finish. Short VDOs play through
        /// DayEventPlayer; minigames are still a hook, since none exist yet.
        /// </summary>
        private IEnumerator PlayDayEndEvent(DayEventType type, DayEventData data)
        {
            if (type == DayEventType.ShortVDO && data is ShortVDOData vdo)
            {
                var player = ResolveEventPlayer();
                if (player == null)
                {
                    Debug.LogWarning($"GameFlowManager: no DayEventPlayer in the scene - skipping '{vdo.Label}'.", this);
                    yield break;
                }

                bool done = false;
                player.Play(vdo, () => done = true);
                while (!done) yield return null;
                yield break;
            }

            if (type == DayEventType.Minigame)
            {
                // No minigames authored yet. When they are, load/instantiate here and yield until
                // the minigame reports back, exactly like the VDO branch above.
                Debug.Log($"GameFlowManager: minigame '{data.Label}' rolled, but minigame playback is not implemented yet - skipping.", this);
                yield break;
            }
        }

        private DayEventPlayer ResolveEventPlayer()
        {
            if (dayEventPlayer == null)
                dayEventPlayer = FindFirstObjectByType<DayEventPlayer>();

            return dayEventPlayer;
        }

        private IEnumerator RunEnding()
        {
            State = GameFlowState.Ending;
            OnGameWon?.Invoke();

            if (showDebugInfo)
                Debug.Log("GameFlowManager: run complete - playing ending.", this);

            var ending = ResolveEndingSequence();
            if (ending != null)
            {
                bool done = false;
                ending.PlayEnding(() => done = true);
                while (!done) yield return null;
            }

            // A finished run is a finished run: wipe everything so the next launch is New Game.
            ResetAllSaveData();

            State = GameFlowState.MainMenu;
            LoadSceneByName(mainMenuSceneName, "main menu");
        }

        /// <summary>Full wipe - day progress, consumed event pools, email flags. Same as New Game.</summary>
        public static void ResetAllSaveData()
        {
            SaveManager.ResetAll();
            NightPlanProvider.Clear();
            NightPlanProvider.ForcedSeed = null;
            ClearLastResult();
        }

        private RandomEventDirector ResolveEventDirector()
        {
            if (randomEventDirector == null)
                randomEventDirector = FindFirstObjectByType<RandomEventDirector>();

            if (randomEventDirector == null && showDebugInfo)
                Debug.Log("GameFlowManager: no RandomEventDirector in the scene - no day-end event.", this);

            return randomEventDirector;
        }

        private EndingSequenceController ResolveEndingSequence()
        {
            if (endingSequence == null)
                endingSequence = FindFirstObjectByType<EndingSequenceController>();

            if (endingSequence == null)
                Debug.LogWarning("GameFlowManager: no EndingSequenceController found - skipping the ending.", this);

            return endingSequence;
        }

        private void LoadSceneByName(string sceneName, string label)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError($"GameFlowManager: no {label} scene name configured.", this);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"GameFlowManager: {label} scene '{sceneName}' is not in Build Settings.", this);
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                // A scene copy showing up after a persistent one already exists is normal, not a
                // misconfiguration - the surviving instance keeps the run's state.
                Destroy(persistAcrossScenes ? gameObject : (Object)this);
                return;
            }

            _instance = this;
            FinalDay = Mathf.Max(1, finalDay);

            if (persistAcrossScenes)
                DontDestroyOnLoad(gameObject);
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

            if (showDebugInfo)
            {
                Debug.Log(
                    $"GameFlowManager: night ended as {outcome}. " +
                    $"Score {LastResult.score}/{LastResult.requiredScore}, won={LastResult.Won}, " +
                    $"cause='{causeAnomalyId ?? "-"}' in room '{causeRoomId ?? "-"}'.", this);
            }

            StartCoroutine(FinishDayFromOutcome(outcome));
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

        /// <summary>
        /// Plays the in-place feedback for how the day ended (a short pause on a win, the death
        /// fade + cause line on a loss), then feeds the result straight into EndDayGameplay -
        /// there is no Result-scene detour, so a win goes on to the day-end event and a loss
        /// restarts immediately, exactly per the day-loop state machine.
        ///
        /// LastResult.Won (not just outcome == Survived) decides survived: reaching 6:00 AM
        /// without the required score is still a loss that retries the same day.
        /// </summary>
        private IEnumerator FinishDayFromOutcome(NightOutcome outcome)
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

            EndDayGameplay(LastResult != null && LastResult.Won);
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
                    if (LastResult == null) return "NEGLIGENCE.";
                    if (LastResult.killedByAnomalyId == "silence_protocol") return "IT HEARD YOU.";
                    if (LastResult.killedByAnomalyId == AnomalyOverloadWatcher.OverloadCauseId)
                        return "YOU LET TOO MANY IN.";
                    return "NEGLIGENCE.";

                case NightOutcome.KilledByAnomaly:
                    return LastResult != null && !string.IsNullOrEmpty(LastResult.killedInRoomId)
                        ? $"IT CAUGHT YOU IN THE {LastResult.killedInRoomId.ToUpperInvariant()}."
                        : "IT CAUGHT YOU.";

                default:
                    return "YOU DID NOT SURVIVE.";
            }
        }

        public static void ClearLastResult() => LastResult = null;

        /// <summary>The day a returning player resumes on.</summary>
        public static int UnlockedNightIndex => CurrentDay;

        /// <summary>Back to day 1 with everything re-locked. Used by New Game.</summary>
        public static void ResetProgression() => ResetAllSaveData();

        /// <summary>
        /// Reloads the gameplay scene with a fresh roll. Kept for the Result screen's Play Again,
        /// which is a retry of the current day rather than a new day.
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
