using System.Collections.Generic;
using UnityEngine;

// Aliased, not imported: this file uses UnityEngine's [Min] and [Range], and a plain
// `using Gaskellgames;` would make both simple names ambiguous (CS0104).
// See CLAUDE.md - "Gaskellgames" for the project-wide rule.
using GG = Gaskellgames;

namespace GameLogic.Night
{
    /// <summary>
    /// Hand-authored settings for ONE night of the campaign, overriding the linear growth
    /// formulas below. This exists because the formulas alone cannot describe the shape the
    /// campaign actually wants: night 1 has to be a gentle tutorial, and nights 2-5 have to
    /// keep getting harder in ways a single "+2 per night" curve saturates against.
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

        [Header("Lose conditions (0 = use the shared value)")]
        [Tooltip("Seconds an unresolved Demon report may sit before the night is lost.")]
        [Min(0f)] public float demonTimeoutSeconds;

        [Tooltip("Unresolved anomalies allowed on screen at once. Going ABOVE this starts the overload timer.")]
        [Min(0)] public int maxConcurrentAnomalies;

        [Tooltip("Seconds the overload must be sustained before the night is lost.")]
        [Min(0f)] public float overloadDurationSeconds;
    }

    /// <summary>
    /// Every knob that decides how hard a night is. One asset, tuned by ear - no code edit and
    /// no re-authoring of timelines.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyProfile", menuName = "Give Me A Sign/Difficulty Profile")]
    public class DifficultyProfile : ScriptableObject
    {
        [Header("Campaign (per-night overrides, applied on top of the formulas below)")]
        [GG.InfoBox("Rows here WIN over the growth formulas below, field by field. Use 'Log Campaign Curve' to see what the current numbers actually produce.")]
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

        [Header("Lose Conditions")]
        [Tooltip("Seconds an unresolved Demon report may sit before the night is lost. Overridable per night.")]
        [Min(1f)] public float demonTimeoutSeconds = 30f;

        [Tooltip("Unresolved anomalies allowed on screen at once. Going ABOVE this starts the overload timer.")]
        [Min(1)] public int maxConcurrentAnomalies = 3;

        [Tooltip("Seconds the overload must be SUSTAINED before the night is lost. Dropping back to the limit resets it.")]
        [Min(1f)] public float overloadDurationSeconds = 120f;

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

        public NightTuning TuningFor(int nightIndex)
        {
            if (nights == null) return null;

            foreach (var entry in nights)
            {
                if (entry != null && entry.nightIndex == nightIndex) return entry;
            }
            return null;
        }

        public int ThreatBudgetFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            if (tuning != null && tuning.threatBudget > 0)
                return Mathf.Clamp(tuning.threatBudget, 1, maxThreatBudget);

            int extra = Mathf.RoundToInt(budgetGrowthPerNight * Mathf.Max(0, nightIndex - 1));
            return Mathf.Clamp(baseThreatBudget + extra, 1, maxThreatBudget);
        }

        public int GlitchCountFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            if (tuning != null && tuning.glitchCount >= 0)
                return tuning.glitchCount;

            int extra = Mathf.RoundToInt(glitchGrowthPerNight * Mathf.Max(0, nightIndex - 1));
            return Mathf.Max(0, baseGlitchCount + extra);
        }

        public float WinRatioFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            return tuning != null && tuning.winRatio > 0f ? tuning.winRatio : winRatio;
        }

        public float MinimumSpacingFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            return tuning != null && tuning.minimumSpacingSeconds > 0f
                ? tuning.minimumSpacingSeconds
                : minimumSpacingSeconds;
        }

        public float NightDurationFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            return tuning != null ? Mathf.Max(0f, tuning.nightDurationMinutes) : 0f;
        }

        public float DemonTimeoutFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            return tuning != null && tuning.demonTimeoutSeconds > 0f
                ? tuning.demonTimeoutSeconds
                : demonTimeoutSeconds;
        }

        public int MaxConcurrentAnomaliesFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            return tuning != null && tuning.maxConcurrentAnomalies > 0
                ? tuning.maxConcurrentAnomalies
                : maxConcurrentAnomalies;
        }

        public float OverloadDurationFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            return tuning != null && tuning.overloadDurationSeconds > 0f
                ? tuning.overloadDurationSeconds
                : overloadDurationSeconds;
        }

        public int PenaltyAnomaliesFor(int nightIndex)
        {
            var tuning = TuningFor(nightIndex);
            if (tuning != null && tuning.penaltyAnomaliesPerWrongReport >= 0)
                return tuning.penaltyAnomaliesPerWrongReport;

            return Mathf.Max(0, penaltyAnomaliesPerWrongReport);
        }

        public int RequiredScoreFor(int anomalyCount, float ratio)
        {
            if (anomalyCount <= 0) return 0;

            // At least one - a night you win by doing nothing isn't a night.
            return Mathf.Clamp(Mathf.CeilToInt(anomalyCount * ratio), 1, anomalyCount);
        }

        public int RequiredScoreFor(int anomalyCount) => RequiredScoreFor(anomalyCount, winRatio);

        [GG.Button]
        public void LogCampaignCurve()
        {
            int lastNight = Mathf.Max(Flow.NightResult.FinalNightIndex, nights != null ? nights.Count : 0);

            var report = new System.Text.StringBuilder($"=== Campaign curve ({name}) ===\n");
            report.AppendLine("  day  length  budget  win%   spacing  glitch  penalty  demon  maxAnom  overload  source");

            for (int night = 1; night <= lastNight; night++)
            {
                float duration = NightDurationFor(night);
                string durationLabel = duration > 0f ? $"{duration:0.#}m" : "scene";

                report.AppendLine(
                    $"  {night,3}  {durationLabel,6}  {ThreatBudgetFor(night),6}  " +
                    $"{WinRatioFor(night) * 100f,4:0}%  {MinimumSpacingFor(night),7:0.#}s  " +
                    $"{GlitchCountFor(night),6}  {PenaltyAnomaliesFor(night),7}  " +
                    $"{DemonTimeoutFor(night),5:0}s  {MaxConcurrentAnomaliesFor(night),7}  " +
                    $"{OverloadDurationFor(night),7:0}s  " +
                    $"{(TuningFor(night) != null ? "table" : "formula")}");
            }

            Debug.Log(report.ToString(), this);
        }
    }
}
