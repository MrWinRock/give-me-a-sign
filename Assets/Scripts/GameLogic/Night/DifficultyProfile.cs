using System.Collections.Generic;
using UnityEngine;

namespace GameLogic.Night
{
    /// <summary>
    /// Hand-authored settings for ONE night of the campaign, overriding the linear growth
    /// formulas below. This exists because the formulas alone cannot describe the shape the
    /// campaign actually wants: night 1 has to be a gentle tutorial, and nights 2-5 have to
    /// keep getting harder in ways a single "+2 per night" curve saturates against.
    ///
    /// Every numeric field uses a SENTINEL for "not overridden" (0, or -1 where 0 is a
    /// meaningful value) so a half-filled entry falls back to the formula field by field
    /// instead of silently zeroing a knob nobody meant to touch.
    /// </summary>
    [System.Serializable]
    public class NightTuning
    {
        [Tooltip("Which night this row describes. 1 = the first night.")]
        [Min(1)] public int nightIndex = 1;

        [Tooltip("Label for the Inspector only - never read by the game.")]
        public string notes = "";

        [Header("Overrides (0 = use the formula above)")]
        [Tooltip("Real minutes this night lasts. Pushed into the scene's NightTimer by NightPlanRunner. 0 = keep whatever the scene is set to.")]
        [Min(0f)] public float nightDurationMinutes;

        [Tooltip("Threat budget the generator may spend. 0 = use baseThreatBudget + growth.")]
        [Min(0)] public int threatBudget;

        [Tooltip("Fraction of the night's anomalies that must be handled. 0 = use the shared winRatio.")]
        [Range(0f, 1f)] public float winRatio;

        [Tooltip("Minimum gap between two anomalies, in seconds. Lower = a busier night. 0 = use the shared value.")]
        [Min(0f)] public float minimumSpacingSeconds;

        [Tooltip("Scheduled form glitches. -1 = use baseGlitchCount + growth. 0 IS a valid override meaning 'none at all'.")]
        [Min(-1)] public int glitchCount = -1;

        [Tooltip("Extra anomalies spawned as the penalty for one wrong Incident Report. -1 = use the shared default. 0 IS valid, meaning 'wrong reports cost nothing' (what night 1 wants).")]
        [Min(-1)] public int penaltyAnomaliesPerWrongReport = -1;
    }

    /// <summary>
    /// Every knob that decides how hard a night is. One asset, tuned by ear - no code edit and
    /// no re-authoring of timelines.
    ///
    /// Two layers, in this order:
    ///   1. the linear formulas (base + growth per night) - a sane curve for any night index
    ///   2. the <see cref="nights"/> table - hand-authored overrides for the designed 1-5 arc
    ///
    /// Layer 2 wins where it is filled in. The formulas are kept underneath so a night index
    /// with no row (or a half-filled row) still produces a playable night rather than a blank one.
    ///
    /// The pacing rules here are also what the generated plan is validated against, so a value
    /// that makes nights unplayable shows up as rejected plans in the console rather than as a
    /// night the player quietly cannot win.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyProfile", menuName = "Give Me A Sign/Difficulty Profile")]
    public class DifficultyProfile : ScriptableObject
    {
        [Header("Campaign (per-night overrides, applied on top of the formulas below)")]
        [Tooltip("One row per night of the designed arc. A night with no row here falls back entirely to the growth formulas.")]
        public List<NightTuning> nights = new List<NightTuning>();

        [Header("Threat Budget")]
        [Tooltip("Threat cost the generator may spend on night 1.")]
        [Min(1)] public int baseThreatBudget = 8;

        [Tooltip("Extra budget per night after the first.")]
        [Min(0f)] public float budgetGrowthPerNight = 2f;

        [Tooltip("Hard ceiling, so a late night can't become a wall of anomalies.")]
        [Min(1)] public int maxThreatBudget = 24;

        [Header("Win Requirement")]
        [Tooltip("Fraction of the night's anomalies the player must handle. requiredScore = ceil(count * this).")]
        [Range(0.3f, 1f)] public float winRatio = 0.7f;

        [Header("Wrong Reports")]
        [Tooltip("Extra anomalies spawned when an Incident Report comes back wrong. Overridable per night. 0 = wrong reports cost nothing.")]
        [Min(0)] public int penaltyAnomaliesPerWrongReport = 1;

        [Header("Anomaly Pacing")]
        [Tooltip("Nothing spawns before this minute - gives the player a moment to settle in.")]
        [Min(0f)] public float firstSpawnMinute = 0.25f;

        [Tooltip("Fraction of the night after which nothing new spawns, so the last anomaly is still beatable.")]
        [Range(0.5f, 1f)] public float lastSpawnFraction = 0.9f;

        [Tooltip("Minimum gap between two anomalies. Validation rejects plans that breach this.")]
        [Min(1f)] public float minimumSpacingSeconds = 25f;

        [Header("Glitches")]
        [Tooltip("Scheduled form glitches on night 1 (GlitchDirector's own ambient system runs on top of these).")]
        [Min(0)] public int baseGlitchCount = 2;

        [Tooltip("Extra scheduled glitches per night after the first.")]
        [Min(0f)] public float glitchGrowthPerNight = 1f;

        [Tooltip("Opening stretch of the night with no glitches at all, as a fraction. Spec calls this Onboarding.")]
        [Range(0f, 0.5f)] public float onboardingQuietFraction = 0.2f;

        [Header("Climax")]
        [Tooltip("The costliest anomaly of the night must land inside this closing fraction.")]
        [Range(0.1f, 0.5f)] public float climaxFraction = 0.25f;

        [Header("Solvability")]
        [Tooltip("Seconds a perfect player needs per anomaly: open form + pick room + speak + wait for STT + submit + switch camera.")]
        [Min(1f)] public float handleCostSeconds = 10f;

        [Header("Generation")]
        [Tooltip("How many times to re-roll a rejected plan before falling back to a guaranteed-valid one.")]
        [Min(1)] public int maxAttempts = 50;

        // ── Per-night lookup ─────────────────────────────────────────────────────────────

        /// <summary>The authored row for a night, or null when that night runs on formulas alone.</summary>
        public NightTuning TuningFor(int nightIndex)
        {
            if (nights == null) return null;

            foreach (var entry in nights)
            {
                if (entry != null && entry.nightIndex == nightIndex) return entry;
            }
            return null;
        }

        /// <summary>Threat budget for a given night, clamped to the ceiling.</summary>
        public int ThreatBudgetFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            if (tuning != null && tuning.threatBudget > 0)
                return Mathf.Clamp(tuning.threatBudget, 1, maxThreatBudget);

            int extra = Mathf.RoundToInt(budgetGrowthPerNight * Mathf.Max(0, nightIndex - 1));
            return Mathf.Clamp(baseThreatBudget + extra, 1, maxThreatBudget);
        }

        /// <summary>Number of scheduled glitches for a given night.</summary>
        public int GlitchCountFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            if (tuning != null && tuning.glitchCount >= 0)
                return tuning.glitchCount;

            int extra = Mathf.RoundToInt(glitchGrowthPerNight * Mathf.Max(0, nightIndex - 1));
            return Mathf.Max(0, baseGlitchCount + extra);
        }

        /// <summary>Fraction of the night's anomalies that must be handled, for a given night.</summary>
        public float WinRatioFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            return tuning != null && tuning.winRatio > 0f ? tuning.winRatio : winRatio;
        }

        /// <summary>Minimum gap the generator must leave between two anomalies, for a given night.</summary>
        public float MinimumSpacingFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            return tuning != null && tuning.minimumSpacingSeconds > 0f
                ? tuning.minimumSpacingSeconds
                : minimumSpacingSeconds;
        }

        /// <summary>
        /// Real minutes this night should last, or 0 meaning "leave the scene's NightTimer alone".
        /// A longer night is the lever that actually raises the anomaly ceiling: the generator can
        /// only fit so many anomalies into a night at a fair spacing, so growing the budget alone
        /// saturates once the clock is full.
        /// </summary>
        public float NightDurationFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            return tuning != null ? Mathf.Max(0f, tuning.nightDurationMinutes) : 0f;
        }

        /// <summary>Anomalies spawned as the cost of one wrong Incident Report, for a given night.</summary>
        public int PenaltyAnomaliesFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            if (tuning != null && tuning.penaltyAnomaliesPerWrongReport >= 0)
                return tuning.penaltyAnomaliesPerWrongReport;

            return Mathf.Max(0, penaltyAnomaliesPerWrongReport);
        }

        /// <summary>Score needed to survive a night with this many anomalies, at a given ratio.</summary>
        public int RequiredScoreFor(int anomalyCount, float ratio)
        {
            if (anomalyCount <= 0) return 0;

            // At least one - a night you win by doing nothing isn't a night.
            return Mathf.Clamp(Mathf.CeilToInt(anomalyCount * ratio), 1, anomalyCount);
        }

        /// <summary>Score needed to survive, using the shared ratio. Kept for callers with no night index.</summary>
        public int RequiredScoreFor(int anomalyCount) => RequiredScoreFor(anomalyCount, winRatio);
    }
}
