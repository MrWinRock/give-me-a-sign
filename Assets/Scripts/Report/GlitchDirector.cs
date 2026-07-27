using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Report
{
    /// <summary>Coarse anomaly pressure the director reacts to. Map your own Anomaly types onto this.</summary>
    public enum GlitchAnomalyState
    {
        None,
        Passive,
        Active
    }

    /// <summary>
    /// Everything <see cref="GlitchDirector"/> needs to know about the rest of the game.
    /// Implement this on any existing manager and drag it into the director's State Source slot,
    /// or skip it entirely and push values in through the director's Set* methods / UnityEvents.
    /// </summary>
    public interface IGlitchStateSource
    {
        int ReportCount { get; }
        int GameHour { get; }
        float ShiftProgress01 { get; }
        GlitchAnomalyState AnomalyState { get; }
        int ConsecutiveFailures { get; }
    }

    /// <summary>
    /// Decides WHEN the Incident Report form betrays the player. Owns all timing;
    /// <see cref="FormGlitchController"/> owns all execution.
    ///
    /// Every time the form opens, exactly one branch is taken, evaluated in this order:
    ///   1. Scripted beat  - an authored, deterministic moment. Never mixed with random.
    ///   2. Blackout       - a guaranteed-clean window.
    ///   3. Ambient random - the weighted roll, with its chance derived from game state.
    /// </summary>
    public class GlitchDirector : MonoBehaviour
    {
        // =======================================================================================
        // Authoring types
        // =======================================================================================

        public enum TriggerCondition
        {
            OnReportCount,
            OnGameHour,
            OnFirstActiveAnomaly,
            OnConsecutiveFailures,
            Manual
        }

        public enum BlackoutCondition
        {
            BeforeReportCount,
            BeforeGameHour,
            DuringPassiveAnomaly,
            AlwaysWhenFlagSet
        }

        public enum ForceMode
        {
            Off,
            ForceClean,
            ForceAmbient,
            ForceScripted
        }

        /// <summary>One hand-authored narrative glitch moment.</summary>
        [Serializable]
        public class ScriptedGlitchBeat
        {
            [Tooltip("Label for your own reference. Also the key used by FireBeatByName().")]
            public string beatName = "New Beat";
            public GlitchType glitchType = GlitchType.StatusIntrusion;
            public TriggerCondition condition = TriggerCondition.OnReportCount;
            [Tooltip("Threshold for the condition. Report count / game hour / failure count. Ignored by OnFirstActiveAnomaly and Manual.")]
            public int conditionValue = 1;
            [Tooltip("Optional. If set, this exact string is used instead of a random pick from the controller's word list.")]
            public string overrideText = "";
            [Tooltip("If true, this beat can only ever fire once per playthrough.")]
            public bool fireOnce = true;
            [Tooltip("Seconds to wait after the form opens before firing.")]
            public float delayAfterFormOpen = 1.5f;
        }

        /// <summary>A window during which glitches are guaranteed not to happen.</summary>
        [Serializable]
        public class GlitchBlackout
        {
            [Tooltip("Label for your own reference.")]
            public string reason = "New Blackout";
            public BlackoutCondition condition = BlackoutCondition.BeforeReportCount;
            public int conditionValue = 3;
            [Tooltip("Only used by AlwaysWhenFlagSet.")]
            public string flagName = "";
        }

        [Serializable]
        public class GlitchWeight
        {
            public GlitchType type;
            public bool enabled = true;
            [Min(0f)] public float weight = 1f;
        }

        // =======================================================================================
        // Wiring
        // =======================================================================================

        [Header("Wiring")]
        [Tooltip("The executor. Required.")]
        [SerializeField] private FormGlitchController glitchController;
        [Tooltip("Used to auto-detect form open/close via its IsReportOpen property. Falls back to IncidentReportManager.Instance.")]
        [SerializeField] private IncidentReportManager reportManager;
        [Tooltip("If false, you must call NotifyFormOpened()/NotifyFormClosed() yourself.")]
        [SerializeField] private bool autoDetectFormLifecycle = true;

        [Header("State Source (optional)")]
        [Tooltip("Any MonoBehaviour implementing IGlitchStateSource. Leave empty and push values via SetReportCount() / SetGameHour() / SetAnomalyState() / SetConsecutiveFailures() instead.")]
        [SerializeField] private MonoBehaviour stateSourceBehaviour;

        // =======================================================================================
        // Part 1 - Scripted beats
        // =======================================================================================

        [Header("1. Scripted Beats (evaluated top to bottom, first match wins)")]
        [SerializeField]
        private List<ScriptedGlitchBeat> scriptedBeats = new List<ScriptedGlitchBeat>
        {
            new ScriptedGlitchBeat
            {
                beatName = "First doubt",
                glitchType = GlitchType.StatusIntrusion,
                condition = TriggerCondition.OnReportCount,
                conditionValue = 4,
                overrideText = "PREVIOUS OFFICER DID NOT REPORT",
                fireOnce = true,
                delayAfterFormOpen = 1.5f
            },
            new ScriptedGlitchBeat
            {
                beatName = "Not on shift",
                glitchType = GlitchType.StatusIntrusion,
                condition = TriggerCondition.OnReportCount,
                conditionValue = 7,
                overrideText = "SEC-04 IS NOT ON SHIFT",
                fireOnce = true,
                delayAfterFormOpen = 2f
            },
            new ScriptedGlitchBeat
            {
                beatName = "It knows",
                glitchType = GlitchType.FalseRecognition,
                condition = TriggerCondition.OnFirstActiveAnomaly,
                conditionValue = 0,
                overrideText = "behind you",
                fireOnce = true,
                delayAfterFormOpen = 0f
            },
            new ScriptedGlitchBeat
            {
                beatName = "Time breaks",
                glitchType = GlitchType.ClockDesync,
                condition = TriggerCondition.OnGameHour,
                conditionValue = 3,
                overrideText = "",
                fireOnce = true,
                delayAfterFormOpen = 1f
            },
            new ScriptedGlitchBeat
            {
                beatName = "Your room",
                glitchType = GlitchType.PhantomDropdown,
                condition = TriggerCondition.OnReportCount,
                conditionValue = 12,
                overrideText = "Your room",
                fireOnce = true,
                delayAfterFormOpen = 0.5f
            }
        };

        // =======================================================================================
        // Part 2 - Blackout windows
        // =======================================================================================

        [Header("2. Blackout Windows (guaranteed clean)")]
        [SerializeField]
        private List<GlitchBlackout> blackouts = new List<GlitchBlackout>
        {
            new GlitchBlackout
            {
                reason = "No glitches before report #3",
                condition = BlackoutCondition.BeforeReportCount,
                conditionValue = 3
            },
            new GlitchBlackout
            {
                reason = "No glitches during the first in-game hour",
                condition = BlackoutCondition.BeforeGameHour,
                conditionValue = 1
            },
            new GlitchBlackout
            {
                reason = "No glitches while the anomaly is Passive",
                condition = BlackoutCondition.DuringPassiveAnomaly,
                conditionValue = 0
            }
        };

        // =======================================================================================
        // Part 3 - Ambient random
        // =======================================================================================

        [Header("3. Ambient Random - base")]
        [Tooltip("Chance that ANY glitch happens in a given form session, before multipliers.")]
        [Range(0f, 1f)] [SerializeField] private float baseChance = 0.35f;
        [Tooltip("Minimum glitches when a session does trigger.")]
        [SerializeField] private int minGlitchesPerSession = 1;
        [Tooltip("Maximum glitches in a single session.")]
        [SerializeField] private int maxGlitchesPerSession = 2;
        [Tooltip("No two glitches may fire within this many seconds of each other.")]
        [SerializeField] private float glitchCooldownSeconds = 3f;
        [Tooltip("Delay before the first ambient glitch of a session.")]
        [SerializeField] private Vector2 ambientFirstDelayRange = new Vector2(1f, 4f);
        [Tooltip("Avoid firing the same glitch type twice within one form session.")]
        [SerializeField] private bool avoidRepeatingGlitchTypeInSession = true;

        [Header("3. Ambient Random - weights")]
        [SerializeField]
        private List<GlitchWeight> glitchWeights = new List<GlitchWeight>
        {
            new GlitchWeight { type = GlitchType.PhantomDropdown,  enabled = true, weight = 0.8f },
            new GlitchWeight { type = GlitchType.FalseRecognition, enabled = true, weight = 1.2f },
            new GlitchWeight { type = GlitchType.CaseCorruption,   enabled = true, weight = 1f },
            new GlitchWeight { type = GlitchType.StatusIntrusion,  enabled = true, weight = 1.3f },
            new GlitchWeight { type = GlitchType.ClockDesync,      enabled = true, weight = 0.9f }
        };

        [Header("3. Ambient Random - state multipliers")]
        [Tooltip("X = shift progress 0-1 (start to end of night), Y = chance multiplier. Drag to shape escalation.")]
        [SerializeField]
        private AnimationCurve hourMultiplierCurve = AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f);
        [Tooltip("Multiplier while a Passive anomaly is present.")]
        [SerializeField] private float passiveAnomalyMultiplier = 0.8f;
        [Tooltip("Multiplier while an Active anomaly is present.")]
        [SerializeField] private float activeAnomalyMultiplier = 1.6f;
        [Tooltip("Added multiplier per consecutive failed report (1 + failures * this).")]
        [SerializeField] private float failureMultiplierPerFailure = 0.25f;
        [Tooltip("Hard cap on the failure multiplier so a losing streak cannot run away.")]
        [SerializeField] private float failureMultiplierCap = 2f;
        [Tooltip("Multiplier applied once the form has been open longer than the dwell threshold. Hesitating is dangerous.")]
        [SerializeField] private float dwellMultiplier = 1.5f;
        [Tooltip("Seconds the form must stay open before the dwell multiplier kicks in.")]
        [SerializeField] private float dwellThresholdSeconds = 8f;
        [Tooltip("How often to re-roll for a bonus glitch while the player lingers on the form.")]
        [SerializeField] private float dwellRerollIntervalSeconds = 4f;

        [Header("3. Ambient Random - clamp")]
        [Tooltip("Effective chance can never fall below this.")]
        [Range(0f, 1f)] [SerializeField] private float minEffectiveChance = 0.02f;
        [Tooltip("Effective chance can never rise above this.")]
        [Range(0f, 1f)] [SerializeField] private float maxEffectiveChance = 0.85f;

        [Header("Escalation & Rhythm")]
        [Tooltip("Global multiplier set at runtime via SetIntensity().")]
        [SerializeField] private float intensityMultiplier = 1f;
        [Tooltip("After this many consecutive glitched sessions, force one completely clean session so the pattern stays unpredictable. 0 = never force.")]
        [SerializeField] private int forceCleanAfterConsecutiveGlitchedSessions = 3;

        // =======================================================================================
        // Part 5 - Debug
        // =======================================================================================

        [Header("Debug")]
        [Tooltip("Off = normal. The other modes pin every session to one branch for testing.")]
        [SerializeField] private ForceMode forceMode = ForceMode.Off;
        [Tooltip("Bypasses the chance roll entirely - every ambient session glitches.")]
        [SerializeField] private bool forceGlitchEveryTime;
        [Tooltip("Print the full decision trace every time the form opens.")]
        [SerializeField] private bool logDirectorDecisions;

        // =======================================================================================
        // Runtime state
        // =======================================================================================

        private IGlitchStateSource _stateSource;

        // Pushed state - used whenever no IGlitchStateSource is wired.
        private int _pushedReportCount;
        private int _pushedGameHour;
        private float _pushedShiftProgress;
        private GlitchAnomalyState _pushedAnomalyState = GlitchAnomalyState.None;
        private int _pushedConsecutiveFailures;

        // "Was this input ever actually provided?" - unprovided inputs are treated as neutral
        // (multiplier 1.0) and never match a beat or blackout, instead of silently reading 0.
        private bool _reportCountProvided;
        private bool _gameHourProvided;
        private bool _shiftProgressProvided;
        private bool _anomalyStateProvided;
        private bool _failuresProvided;
        private bool _warnedAboutMissingProviders;

        private readonly HashSet<string> _firedBeats = new HashSet<string>();
        private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
        private readonly List<GlitchType> _firedThisSession = new List<GlitchType>();

        private bool _sessionActive;
        private bool _wasFormOpen;
        private float _formOpenSeconds;
        private float _lastGlitchTime = -999f;
        private float _nextDwellRollTime;
        private int _sessionBudget;
        private int _consecutiveGlitchedSessions;
        private bool _hasSeenActiveAnomaly;
        private Coroutine _scheduledBeat;

        private enum SessionMode { Clean, Scripted, Ambient }
        private SessionMode _sessionMode = SessionMode.Clean;

        /// <summary>Human-readable trace of the last branch taken. Surface this in a debug overlay.</summary>
        public string LastDecisionReason { get; private set; } = "(no session yet)";

        /// <summary>Seconds the report form has been open in the current session.</summary>
        public float FormOpenSeconds => _formOpenSeconds;

        // =======================================================================================
        // Unity lifecycle
        // =======================================================================================

        void Awake()
        {
            if (glitchController == null)
                glitchController = GetComponent<FormGlitchController>();

            if (reportManager == null)
                reportManager = GetComponent<IncidentReportManager>();

            if (stateSourceBehaviour != null)
            {
                _stateSource = stateSourceBehaviour as IGlitchStateSource;
                if (_stateSource == null)
                {
                    Debug.LogWarning(
                        $"[GlitchDirector] '{stateSourceBehaviour.GetType().Name}' does not implement IGlitchStateSource. " +
                        "Falling back to pushed values.", this);
                }
            }

            if (glitchController == null)
                Debug.LogError("[GlitchDirector] No FormGlitchController assigned - no glitch can ever fire.", this);
        }

        void Update()
        {
            if (autoDetectFormLifecycle)
            {
                var mgr = reportManager != null ? reportManager : IncidentReportManager.Instance;
                bool open = mgr != null && mgr.IsReportOpen;

                if (open != _wasFormOpen)
                {
                    _wasFormOpen = open;
                    if (open) NotifyFormOpened();
                    else NotifyFormClosed();
                }
            }

            if (!_sessionActive) return;

            _formOpenSeconds += Time.unscaledDeltaTime;

            if (_sessionMode == SessionMode.Ambient)
                AmbientTick();
        }

        // =======================================================================================
        // Session lifecycle
        // =======================================================================================

        /// <summary>Call when the Incident Report window opens (automatic unless autoDetectFormLifecycle is off).</summary>
        public void NotifyFormOpened()
        {
            _sessionActive = true;
            _formOpenSeconds = 0f;
            _lastGlitchTime = -999f;
            _nextDwellRollTime = float.MaxValue;
            _sessionBudget = 0;
            _firedThisSession.Clear();
            _sessionMode = SessionMode.Clean;

            if (_scheduledBeat != null)
            {
                StopCoroutine(_scheduledBeat);
                _scheduledBeat = null;
            }

            glitchController?.NotifyFormOpened();
            WarnAboutMissingProvidersOnce();

            if (ReadAnomalyState() == GlitchAnomalyState.Active)
                _hasSeenActiveAnomaly = true;

            Decide();
        }

        /// <summary>Call when the Incident Report window closes. Cancels everything in flight.</summary>
        public void NotifyFormClosed()
        {
            _sessionActive = false;

            if (_scheduledBeat != null)
            {
                StopCoroutine(_scheduledBeat);
                _scheduledBeat = null;
            }

            glitchController?.NotifyFormClosed();

            // Track the clean/glitched rhythm on the way out, once we know what actually fired.
            if (_firedThisSession.Count > 0 || _sessionMode == SessionMode.Scripted)
                _consecutiveGlitchedSessions++;
            else
                _consecutiveGlitchedSessions = 0;
        }

        // =======================================================================================
        // The decision
        // =======================================================================================

        private void Decide()
        {
            var trace = new StringBuilder();
            trace.Append("[GlitchDirector] ").Append(DescribeState());

            // --- Force modes short-circuit everything, for isolated branch testing.
            switch (forceMode)
            {
                case ForceMode.ForceClean:
                    Conclude(SessionMode.Clean, "ForceMode=ForceClean", trace);
                    return;

                case ForceMode.ForceScripted:
                {
                    var forced = FindMatchingBeat(true);
                    if (forced != null)
                    {
                        ScheduleBeat(forced);
                        Conclude(SessionMode.Scripted, $"ForceMode=ForceScripted -> beat '{forced.beatName}'", trace);
                    }
                    else
                    {
                        Conclude(SessionMode.Clean, "ForceMode=ForceScripted but no beat available", trace);
                    }
                    return;
                }

                case ForceMode.ForceAmbient:
                    StartAmbientSession(1f, trace, "ForceMode=ForceAmbient");
                    return;
            }

            // --- 1. Scripted beat.
            var beat = FindMatchingBeat(false);
            if (beat != null)
            {
                ScheduleBeat(beat);
                Conclude(SessionMode.Scripted, $"SCRIPTED beat '{beat.beatName}' ({beat.glitchType})", trace);
                return;
            }

            // --- 2. Blackout window.
            var blackout = FindActiveBlackout();
            if (blackout != null)
            {
                Conclude(SessionMode.Clean, $"BLACKOUT '{blackout.reason}'", trace);
                return;
            }

            // --- 2b. Forced clean, so the player can never settle into a rhythm.
            if (forceCleanAfterConsecutiveGlitchedSessions > 0 &&
                _consecutiveGlitchedSessions >= forceCleanAfterConsecutiveGlitchedSessions)
            {
                Conclude(SessionMode.Clean,
                    $"FORCED CLEAN after {_consecutiveGlitchedSessions} consecutive glitched sessions", trace);
                return;
            }

            // --- 3. Ambient random.
            float chance = ComputeEffectiveChance(false, trace);
            StartAmbientSession(chance, trace, "AMBIENT");
        }

        private void StartAmbientSession(float chance, StringBuilder trace, string label)
        {
            bool hit = forceGlitchEveryTime || UnityEngine.Random.value < chance;

            if (!hit)
            {
                Conclude(SessionMode.Clean, $"{label} roll missed (chance {chance:P1})", trace);
                return;
            }

            _sessionBudget = UnityEngine.Random.Range(
                Mathf.Max(1, minGlitchesPerSession),
                Mathf.Max(minGlitchesPerSession, maxGlitchesPerSession) + 1);

            _lastGlitchTime = Time.unscaledTime - glitchCooldownSeconds + RandomInRange(ambientFirstDelayRange);
            _nextDwellRollTime = dwellThresholdSeconds;

            Conclude(SessionMode.Ambient,
                $"{label} roll HIT (chance {chance:P1}) -> budget {_sessionBudget}", trace);
        }

        private void Conclude(SessionMode mode, string reason, StringBuilder trace)
        {
            _sessionMode = mode;
            LastDecisionReason = reason;

            if (logDirectorDecisions)
            {
                trace.Append("\n  -> ").Append(mode).Append(": ").Append(reason);
                Debug.Log(trace.ToString(), this);
            }
        }

        // =======================================================================================
        // Ambient firing
        // =======================================================================================

        private void AmbientTick()
        {
            // Once the player lingers past the dwell threshold, keep re-rolling for a bonus glitch.
            if (_formOpenSeconds >= _nextDwellRollTime)
            {
                _nextDwellRollTime = _formOpenSeconds + Mathf.Max(0.5f, dwellRerollIntervalSeconds);

                if (_sessionBudget < Mathf.Max(1, maxGlitchesPerSession))
                {
                    float dwellChance = ComputeEffectiveChance(true, null);
                    if (forceGlitchEveryTime || UnityEngine.Random.value < dwellChance)
                    {
                        _sessionBudget++;
                        if (logDirectorDecisions)
                            Debug.Log($"[GlitchDirector] Dwell re-roll HIT ({dwellChance:P1}) at " +
                                      $"{_formOpenSeconds:F1}s open -> budget {_sessionBudget}", this);
                    }
                }
            }

            if (_firedThisSession.Count >= _sessionBudget) return;
            if (Time.unscaledTime < _lastGlitchTime + glitchCooldownSeconds) return;

            var type = PickWeightedGlitch();
            if (type == null) return;

            FireGlitch(type.Value, null, "ambient");
        }

        private GlitchType? PickWeightedGlitch()
        {
            float total = 0f;
            foreach (var w in glitchWeights)
            {
                if (!w.enabled || w.weight <= 0f) continue;
                if (avoidRepeatingGlitchTypeInSession && _firedThisSession.Contains(w.type)) continue;
                total += w.weight;
            }

            if (total <= 0f) return null;

            float roll = UnityEngine.Random.value * total;
            foreach (var w in glitchWeights)
            {
                if (!w.enabled || w.weight <= 0f) continue;
                if (avoidRepeatingGlitchTypeInSession && _firedThisSession.Contains(w.type)) continue;

                roll -= w.weight;
                if (roll <= 0f) return w.type;
            }

            return null;
        }

        private void FireGlitch(GlitchType type, string overrideText, string source)
        {
            if (glitchController == null) return;

            if (glitchController.PlayGlitch(type, overrideText))
            {
                _firedThisSession.Add(type);
                _lastGlitchTime = Time.unscaledTime;

                if (logDirectorDecisions)
                    Debug.Log($"[GlitchDirector] Fired {type} ({source}) at {_formOpenSeconds:F1}s into the session.", this);
            }
        }

        // =======================================================================================
        // Scripted beats
        // =======================================================================================

        private ScriptedGlitchBeat FindMatchingBeat(bool ignoreConditions)
        {
            foreach (var beat in scriptedBeats)
            {
                if (beat == null || string.IsNullOrWhiteSpace(beat.beatName)) continue;
                if (beat.fireOnce && _firedBeats.Contains(beat.beatName)) continue;
                if (beat.condition == TriggerCondition.Manual && !ignoreConditions) continue;

                if (ignoreConditions || BeatConditionMet(beat))
                    return beat;
            }
            return null;
        }

        private bool BeatConditionMet(ScriptedGlitchBeat beat)
        {
            switch (beat.condition)
            {
                case TriggerCondition.OnReportCount:
                    return _reportCountProvided && ReadReportCount() >= beat.conditionValue;

                case TriggerCondition.OnGameHour:
                    return _gameHourProvided && ReadGameHour() >= beat.conditionValue;

                case TriggerCondition.OnFirstActiveAnomaly:
                    return _anomalyStateProvided && ReadAnomalyState() == GlitchAnomalyState.Active;

                case TriggerCondition.OnConsecutiveFailures:
                    return _failuresProvided && ReadConsecutiveFailures() >= beat.conditionValue;

                default:
                    return false;
            }
        }

        private void ScheduleBeat(ScriptedGlitchBeat beat)
        {
            if (beat.fireOnce)
                _firedBeats.Add(beat.beatName);

            _scheduledBeat = StartCoroutine(FireBeatAfterDelay(beat));
        }

        private IEnumerator FireBeatAfterDelay(ScriptedGlitchBeat beat)
        {
            if (beat.delayAfterFormOpen > 0f)
                yield return new WaitForSecondsRealtime(beat.delayAfterFormOpen);

            FireGlitch(beat.glitchType, beat.overrideText, $"scripted '{beat.beatName}'");
            _scheduledBeat = null;
        }

        /// <summary>
        /// Fires a beat by name regardless of its condition - the intended way to drive
        /// Manual beats from cutscenes, Timeline signals, or other scripts.
        /// Returns false if the name is unknown or the beat is fireOnce and already spent.
        /// </summary>
        public bool FireBeatByName(string beatName)
        {
            if (string.IsNullOrWhiteSpace(beatName)) return false;

            foreach (var beat in scriptedBeats)
            {
                if (beat == null || beat.beatName != beatName) continue;
                if (beat.fireOnce && _firedBeats.Contains(beat.beatName))
                {
                    if (logDirectorDecisions)
                        Debug.Log($"[GlitchDirector] FireBeatByName('{beatName}') ignored - already fired.", this);
                    return false;
                }

                if (beat.fireOnce)
                    _firedBeats.Add(beat.beatName);

                FireGlitch(beat.glitchType, beat.overrideText, $"manual '{beat.beatName}'");
                return true;
            }

            Debug.LogWarning($"[GlitchDirector] FireBeatByName('{beatName}') - no beat with that name.", this);
            return false;
        }

        // =======================================================================================
        // Blackouts & flags
        // =======================================================================================

        private GlitchBlackout FindActiveBlackout()
        {
            foreach (var b in blackouts)
            {
                if (b == null) continue;

                switch (b.condition)
                {
                    case BlackoutCondition.BeforeReportCount:
                        if (_reportCountProvided && ReadReportCount() < b.conditionValue) return b;
                        break;

                    case BlackoutCondition.BeforeGameHour:
                        if (_gameHourProvided && ReadGameHour() < b.conditionValue) return b;
                        break;

                    case BlackoutCondition.DuringPassiveAnomaly:
                        if (_anomalyStateProvided && ReadAnomalyState() == GlitchAnomalyState.Passive) return b;
                        break;

                    case BlackoutCondition.AlwaysWhenFlagSet:
                        if (!string.IsNullOrWhiteSpace(b.flagName) &&
                            _flags.TryGetValue(b.flagName, out var set) && set) return b;
                        break;
                }
            }
            return null;
        }

        /// <summary>
        /// Gates glitches from anywhere - e.g. SetFlag("tutorial", true) during onboarding, or
        /// SetFlag("cutscene", true) while a scripted sequence plays. Pair with an
        /// AlwaysWhenFlagSet blackout using the same name.
        /// </summary>
        public void SetFlag(string flagName, bool value)
        {
            if (string.IsNullOrWhiteSpace(flagName)) return;
            _flags[flagName] = value;

            if (logDirectorDecisions)
                Debug.Log($"[GlitchDirector] Flag '{flagName}' = {value}", this);
        }

        /// <summary>Reads back a flag set via SetFlag(). Unknown flags read as false.</summary>
        public bool GetFlag(string flagName)
        {
            return !string.IsNullOrWhiteSpace(flagName) && _flags.TryGetValue(flagName, out var v) && v;
        }

        // =======================================================================================
        // Chance math
        // =======================================================================================

        private float ComputeEffectiveChance(bool includeDwell, StringBuilder trace)
        {
            float hourMul = 1f;
            if (_shiftProgressProvided)
                hourMul = Mathf.Max(0f, hourMultiplierCurve.Evaluate(Mathf.Clamp01(ReadShiftProgress())));

            float anomalyMul = 1f;
            if (_anomalyStateProvided)
            {
                switch (ReadAnomalyState())
                {
                    case GlitchAnomalyState.Passive: anomalyMul = passiveAnomalyMultiplier; break;
                    case GlitchAnomalyState.Active:  anomalyMul = activeAnomalyMultiplier;  break;
                }
            }

            float failureMul = 1f;
            if (_failuresProvided)
            {
                failureMul = Mathf.Min(
                    1f + ReadConsecutiveFailures() * failureMultiplierPerFailure,
                    Mathf.Max(1f, failureMultiplierCap));
            }

            float dwellMul = (includeDwell && _formOpenSeconds >= dwellThresholdSeconds) ? dwellMultiplier : 1f;

            float raw = baseChance * hourMul * anomalyMul * failureMul * dwellMul * Mathf.Max(0f, intensityMultiplier);
            float clamped = Mathf.Clamp(raw, Mathf.Min(minEffectiveChance, maxEffectiveChance), maxEffectiveChance);

            trace?.Append($"\n  chance = base {baseChance:F2}")
                  .Append($" x hour {hourMul:F2}")
                  .Append($" x anomaly {anomalyMul:F2}")
                  .Append($" x failure {failureMul:F2}")
                  .Append($" x dwell {dwellMul:F2}")
                  .Append($" x intensity {intensityMultiplier:F2}")
                  .Append($" = {raw:F3} -> clamped {clamped:F3}");

            return clamped;
        }

        /// <summary>
        /// Global escalation hook. Call from your anomaly/night systems to make the form more
        /// hostile later in the night. 1 = normal, 2 = twice as likely.
        /// </summary>
        public void SetIntensity(float multiplier)
        {
            intensityMultiplier = Mathf.Max(0f, multiplier);

            if (logDirectorDecisions)
                Debug.Log($"[GlitchDirector] Intensity set to {intensityMultiplier:F2}", this);
        }

        // =======================================================================================
        // Part 4 - State the director reads
        // =======================================================================================

        /// <summary>Successful submissions this playthrough.</summary>
        public void SetReportCount(int value)
        {
            _pushedReportCount = value;
            _reportCountProvided = true;
        }

        /// <summary>Current in-game hour, plus normalised progress across the whole shift (0-1).</summary>
        public void SetGameHour(int hour, float shiftProgress01)
        {
            _pushedGameHour = hour;
            _pushedShiftProgress = Mathf.Clamp01(shiftProgress01);
            _gameHourProvided = true;
            _shiftProgressProvided = true;
        }

        /// <summary>Current anomaly pressure: None / Passive / Active.</summary>
        public void SetAnomalyState(GlitchAnomalyState state)
        {
            _pushedAnomalyState = state;
            _anomalyStateProvided = true;

            if (state == GlitchAnomalyState.Active)
                _hasSeenActiveAnomaly = true;
        }

        /// <summary>Consecutive failed reports. Reset to 0 on a success.</summary>
        public void SetConsecutiveFailures(int value)
        {
            _pushedConsecutiveFailures = Mathf.Max(0, value);
            _failuresProvided = true;
        }

        /// <summary>
        /// Convenience for wiring straight off a submit result: bumps the report count on success
        /// and maintains the consecutive-failure streak.
        /// </summary>
        public void RegisterReportResult(bool success)
        {
            if (success)
            {
                SetReportCount(ReadReportCount() + 1);
                SetConsecutiveFailures(0);
            }
            else
            {
                SetConsecutiveFailures(ReadConsecutiveFailures() + 1);
            }
        }

        private int ReadReportCount() =>
            _stateSource != null ? _stateSource.ReportCount : _pushedReportCount;

        private int ReadGameHour() =>
            _stateSource != null ? _stateSource.GameHour : _pushedGameHour;

        private float ReadShiftProgress() =>
            _stateSource != null ? _stateSource.ShiftProgress01 : _pushedShiftProgress;

        private GlitchAnomalyState ReadAnomalyState() =>
            _stateSource != null ? _stateSource.AnomalyState : _pushedAnomalyState;

        private int ReadConsecutiveFailures() =>
            _stateSource != null ? _stateSource.ConsecutiveFailures : _pushedConsecutiveFailures;

        private void WarnAboutMissingProvidersOnce()
        {
            if (_warnedAboutMissingProviders) return;

            if (_stateSource != null)
            {
                _reportCountProvided = _gameHourProvided = _shiftProgressProvided = true;
                _anomalyStateProvided = _failuresProvided = true;
                _warnedAboutMissingProviders = true;
                return;
            }

            var missing = new List<string>();
            if (!_reportCountProvided) missing.Add("ReportCount");
            if (!_gameHourProvided) missing.Add("GameHour");
            if (!_shiftProgressProvided) missing.Add("ShiftProgress");
            if (!_anomalyStateProvided) missing.Add("AnomalyState");
            if (!_failuresProvided) missing.Add("ConsecutiveFailures");

            if (missing.Count == 0) return;

            _warnedAboutMissingProviders = true;
            Debug.LogWarning(
                $"[GlitchDirector] No state provider for: {string.Join(", ", missing)}. " +
                "These are treated as neutral (multiplier 1.0) and will never match a beat or blackout. " +
                "Wire an IGlitchStateSource, or call SetReportCount/SetGameHour/SetAnomalyState/SetConsecutiveFailures.",
                this);
        }

        // =======================================================================================
        // Helpers
        // =======================================================================================

        private static float RandomInRange(Vector2 range)
        {
            return UnityEngine.Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
        }

        private string DescribeState()
        {
            return $"reports={(_reportCountProvided ? ReadReportCount().ToString() : "n/a")}, " +
                   $"hour={(_gameHourProvided ? ReadGameHour().ToString() : "n/a")}, " +
                   $"progress={(_shiftProgressProvided ? ReadShiftProgress().ToString("F2") : "n/a")}, " +
                   $"anomaly={(_anomalyStateProvided ? ReadAnomalyState().ToString() : "n/a")}, " +
                   $"failures={(_failuresProvided ? ReadConsecutiveFailures().ToString() : "n/a")}, " +
                   $"glitchedStreak={_consecutiveGlitchedSessions}, seenActive={_hasSeenActiveAnomaly}";
        }

        // =======================================================================================
        // Part 5 - Authoring support
        // =======================================================================================

        /// <summary>
        /// Logs which branch the director WOULD take for the current state, without touching the
        /// form. Lets you verify authoring without playing through the whole night.
        /// </summary>
        [ContextMenu("Dump Glitch Plan")]
        public void DumpGlitchPlan()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== GLITCH PLAN ===");
            sb.AppendLine(DescribeState());
            sb.AppendLine($"ForceMode: {forceMode}, forceGlitchEveryTime: {forceGlitchEveryTime}, intensity: {intensityMultiplier:F2}");

            var beat = FindMatchingBeat(false);
            if (beat != null)
            {
                sb.AppendLine($"BRANCH: SCRIPTED -> '{beat.beatName}' ({beat.glitchType}, delay {beat.delayAfterFormOpen}s)");
                if (!string.IsNullOrWhiteSpace(beat.overrideText))
                    sb.AppendLine($"  overrideText: \"{beat.overrideText}\"");
            }
            else
            {
                var blackout = FindActiveBlackout();
                if (blackout != null)
                {
                    sb.AppendLine($"BRANCH: BLACKOUT -> '{blackout.reason}' ({blackout.condition})");
                }
                else if (forceCleanAfterConsecutiveGlitchedSessions > 0 &&
                         _consecutiveGlitchedSessions >= forceCleanAfterConsecutiveGlitchedSessions)
                {
                    sb.AppendLine($"BRANCH: FORCED CLEAN (streak {_consecutiveGlitchedSessions})");
                }
                else
                {
                    var trace = new StringBuilder();
                    float chance = ComputeEffectiveChance(false, trace);
                    sb.AppendLine("BRANCH: AMBIENT");
                    sb.AppendLine(trace.ToString().TrimStart());
                    sb.AppendLine($"  effectiveChance (at form open) = {chance:P1}");

                    float dwell = ComputeEffectiveChance(true, null);
                    sb.AppendLine($"  effectiveChance (after {dwellThresholdSeconds}s dwell) = {dwell:P1}");
                }
            }

            sb.AppendLine($"Beats already fired: {(_firedBeats.Count == 0 ? "(none)" : string.Join(", ", _firedBeats))}");
            sb.AppendLine($"LastDecisionReason: {LastDecisionReason}");

            Debug.Log(sb.ToString(), this);
        }

        [ContextMenu("Reset Fired Beats")]
        private void ResetFiredBeats()
        {
            _firedBeats.Clear();
            _hasSeenActiveAnomaly = false;
            Debug.Log("[GlitchDirector] Fired-beat history cleared.", this);
        }
    }
}
