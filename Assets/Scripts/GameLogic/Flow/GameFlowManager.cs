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
        [Tooltip("Plays a brief glitch/static clip (e.g. Overlay.mp4) before the day-end roll, every day. Auto-found if left empty; skipped without one.")]
        [SerializeField] private DayEndTransitionOverlay dayEndOverlay;

        [Tooltip("Rolls the optional Short VDO / Minigame. Auto-found if left empty; no event plays without one.")]
        [SerializeField] private RandomEventDirector randomEventDirector;

        [Tooltip("Dedicated scene a rolled Short VDO plays in - loaded after the day-end overlay, unloaded again the moment AdvanceDay moves on to MainMenu. Must be in Build Settings. Only loaded when a VDO actually rolls.")]
        [SerializeField] private string shortVdoSceneName = "ShortVDO";

        [Tooltip("Plays the rolled Short VDO fullscreen. Lives in the Short VDO scene above, not on this persistent object - auto-found there once that scene loads.")]
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

        /// <summary>Score Debug Skip Night applies before ending the night.</summary>
        public enum DebugSkipScoreMode
        {
            /// <summary>Force score = the night's requirement. Guaranteed win.</summary>
            Full,
            /// <summary>Leave score exactly as already earned - may still lose.</summary>
            CurrentScore
        }

        /// <summary>What Debug Skip Night does with today's documents.</summary>
        public enum DebugSkipMailMode
        {
            /// <summary>Force-collect every MailPickup scheduled for today.</summary>
            Collect,
            /// <summary>Don't touch mail at all.</summary>
            Leave
        }

        /// <summary>What Debug Skip Night does with the day-end event roll. Only matters if the night is actually survived.</summary>
        public enum DebugSkipDayEndMode
        {
            /// <summary>The real RandomEventDirector roll, same as normal play.</summary>
            RollNormally,
            /// <summary>Always plays an unconsumed Short VDO, ignoring the day-chance roll.</summary>
            ForceShortVDO,
            /// <summary>Always skips straight to the next day.</summary>
            ForceNone
        }

        [Header("Debug - Skip Night Checklist")]
        [Tooltip("Score used when Debug Skip Night runs.")]
        [SerializeField] private DebugSkipScoreMode debugSkipScore = DebugSkipScoreMode.Full;
        [Tooltip("What happens to today's documents when Debug Skip Night runs.")]
        [SerializeField] private DebugSkipMailMode debugSkipMail = DebugSkipMailMode.Collect;
        [Tooltip("What the day-end event does when Debug Skip Night runs.")]
        [SerializeField] private DebugSkipDayEndMode debugSkipDayEndEvent = DebugSkipDayEndMode.RollNormally;

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

        // Set by DebugSkipNight right before it forces the night to end, consumed (and cleared)
        // the moment RunDayEndEvent reads it - a real day's roll must never inherit a stale override.
        private DebugSkipDayEndMode? _debugDayEndOverride;

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
            PrepareForDayGameplay();
            LoadSceneByName(gameplaySceneName, "gameplay");
        }

        /// <summary>
        /// Everything a fresh day's gameplay needs reset before it begins, short of the scene
        /// load itself - shared by StartDayGameplay and the OnSceneLoaded safety net above, so
        /// both paths agree regardless of which one actually triggered the load.
        /// </summary>
        private void PrepareForDayGameplay()
        {
            State = GameFlowState.DayGameplay;
            _ending = false; // the guard is per-day; a persistent instance would keep the last day's
            ClearLastResult();

            // A fresh plan for every attempt - see RestartCurrentDay for why this matters.
            NightPlanProvider.Clear();

            if (showDebugInfo)
                Debug.Log($"GameFlowManager: starting day {CurrentDay}.", this);

            OnDayStarted?.Invoke(CurrentDay);
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
        /// DayEndEvent state: a brief glitch cut, then roll for a VDO/Minigame, play it, mark it
        /// consumed, then advance. An empty or exhausted pool simply advances immediately after
        /// the cut - that is the designed behaviour, not an error.
        /// </summary>
        private IEnumerator RunDayEndEvent()
        {
            State = GameFlowState.DayEndEvent;

            yield return PlayDayEndOverlay();
            // The overlay's black cover is still up here on purpose - it stays up (surviving the
            // scene load below) until PlayDayEndEvent explicitly drops it once the Short VDO's own
            // screen is ready to take over, or the fallback CloseDayEndOverlayCover() below does it
            // for every path that never gets that far.

            // One-shot: read and clear immediately, so a real day's roll afterward never inherits it.
            DebugSkipDayEndMode? debugOverride = _debugDayEndOverride;
            _debugDayEndOverride = null;

            if (debugOverride == DebugSkipDayEndMode.ForceNone)
            {
                CloseDayEndOverlayCover();
                AdvanceDay();
                yield break;
            }

            var director = ResolveEventDirector();
            var type = DayEventType.None;
            DayEventData data = null;
            bool rolled = false;

            if (director != null)
            {
                rolled = debugOverride == DebugSkipDayEndMode.ForceShortVDO
                    ? director.TryForceShortVDO(out type, out data)
                    : director.TryGetDayEndEvent(CurrentDay, out type, out data);
            }

            if (rolled)
            {
                yield return PlayDayEndEvent(type, data);

                // Only consumed once it has actually finished, so quitting mid-event doesn't burn it.
                director.MarkConsumed(data);
            }

            CloseDayEndOverlayCover(); // no-op if PlayDayEndEvent already dropped it
            AdvanceDay();
        }

        /// <summary>Plays the glitch/static cut ahead of the roll. Skipped without an overlay or clip.</summary>
        private IEnumerator PlayDayEndOverlay()
        {
            var overlay = ResolveDayEndOverlay();
            if (overlay == null) yield break;

            bool done = false;
            overlay.Play(() => done = true);
            while (!done) yield return null;
        }

        /// <summary>Tears down the overlay's lingering black cover, if one is still up. Safe to call
        /// unconditionally - a no-op once CloseCover has already run.</summary>
        private void CloseDayEndOverlayCover() => ResolveDayEndOverlay()?.CloseCover();

        /// <summary>
        /// Runs the rolled event and waits for it to finish. Short VDOs load the dedicated
        /// shortVdoSceneName scene and play through whatever DayEventPlayer lives there; minigames
        /// are still a hook, since none exist yet.
        /// </summary>
        private IEnumerator PlayDayEndEvent(DayEventType type, DayEventData data)
        {
            if (type == DayEventType.ShortVDO && data is ShortVDOData vdo)
            {
                yield return LoadShortVdoSceneAsync();

                if (SceneManager.GetActiveScene().name != shortVdoSceneName)
                    yield break; // already logged why - cover stays up, RunDayEndEvent's fallback drops it

                var player = ResolveEventPlayer();
                if (player == null)
                {
                    Debug.LogWarning($"GameFlowManager: no DayEventPlayer in '{shortVdoSceneName}' - skipping '{vdo.Label}'.", this);
                    yield break;
                }

                bool done = false;
                player.Play(vdo, () => done = true);

                // The Short VDO's own screen starts fading in from black right now - safe to drop
                // the transition cover on top of it this instant, since both are solid black.
                CloseDayEndOverlayCover();

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

        /// <summary>
        /// Loads shortVdoSceneName and waits for AsyncOperation.isDone, rather than trusting the
        /// synchronous LoadScene to have every object queryable the instant it returns - that
        /// assumption was the actual cause of "no DayEventPlayer in 'ShortVDO'": the scene loaded,
        /// but FindFirstObjectByType ran before its objects were reliably registered.
        /// </summary>
        private IEnumerator LoadShortVdoSceneAsync()
        {
            if (string.IsNullOrWhiteSpace(shortVdoSceneName))
            {
                Debug.LogError("GameFlowManager: no short vdo scene name configured.", this);
                yield break;
            }

            if (!Application.CanStreamedLevelBeLoaded(shortVdoSceneName))
            {
                Debug.LogError($"GameFlowManager: short vdo scene '{shortVdoSceneName}' is not in Build Settings.", this);
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(shortVdoSceneName);
            if (op == null) yield break;

            while (!op.isDone) yield return null;
        }

        private DayEventPlayer ResolveEventPlayer()
        {
            if (dayEventPlayer == null)
                dayEventPlayer = FindFirstObjectByType<DayEventPlayer>();

            return dayEventPlayer;
        }

        private DayEndTransitionOverlay ResolveDayEndOverlay()
        {
            if (dayEndOverlay == null)
                dayEndOverlay = FindFirstObjectByType<DayEndTransitionOverlay>();

            return dayEndOverlay;
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

        /// <summary>Returns false (after logging why) instead of loading, so callers that need to
        /// know can skip whatever depended on the scene rather than pressing on blindly.</summary>
        private bool LoadSceneByName(string sceneName, string label)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError($"GameFlowManager: no {label} scene name configured.", this);
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"GameFlowManager: {label} scene '{sceneName}' is not in Build Settings.", this);
                return false;
            }

            SceneManager.LoadScene(sceneName);
            return true;
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

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>
        /// Safety net for the gameplay scene loading through a path that never calls
        /// StartDayGameplay/RestartCurrentDay directly - e.g. ShutdownSequence's boot animation
        /// loads it with its own SceneManager call. Without this, _ending (and OnDayStarted,
        /// ClearLastResult, NightPlanProvider.Clear) only ever ran once per persistent instance:
        /// EndNight's "if (_ending) return;" guard would silently swallow every night-end call
        /// from day 2 onward, in real play as much as from a debug skip.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != gameplaySceneName) return;
            if (State == GameFlowState.DayGameplay) return; // already prepped before this load

            PrepareForDayGameplay();
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

        // ── Debug: skip the night ───────────────────────────────────────────────────────────
        // One action, driven by the three checklist fields above (score / mail / day-end event)
        // so any combination can be tested freely - e.g. current score (maybe a loss), mail
        // collected, day-end event forced to a Short VDO - without touching code.

        [ContextMenu("Debug/Skip Night (Use Checklist Above)")]
        private void DebugSkipNight()
        {
            var scoreManager = ScoreManager.Instance;
            if (scoreManager == null)
            {
                Debug.LogWarning("GameFlowManager: no ScoreManager in the scene - can't skip the night.", this);
                return;
            }

            if (debugSkipScore == DebugSkipScoreMode.Full)
                scoreManager.TestWinCondition(); // score = the night's requirement
            // else CurrentScore: leave it exactly as already earned - may still lose.

            if (debugSkipMail == DebugSkipMailMode.Collect)
                DebugCollectTodaysMail();

            // Read by RunDayEndEvent once, then cleared - only applies to THIS skip.
            _debugDayEndOverride = debugSkipDayEndEvent;

            scoreManager.ForceEndNight(); // closes scoring, then calls EndNight(Survived/whatever the score earns)

            if (showDebugInfo)
                Debug.Log($"GameFlowManager: debug-skipped day {CurrentDay} " +
                          $"(score={debugSkipScore}, mail={debugSkipMail}, dayEnd={debugSkipDayEndEvent}).", this);
        }

        /// <summary>Force-collects every MailPickup scheduled for today, wherever it sits in the scene.</summary>
        private void DebugCollectTodaysMail()
        {
            var pickups = FindObjectsByType<MailPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var pickup in pickups)
                pickup.ForceCollect();
        }
    }
}
