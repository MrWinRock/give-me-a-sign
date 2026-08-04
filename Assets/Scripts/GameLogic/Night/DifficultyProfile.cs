using UnityEngine;

namespace GameLogic.Night
{
    /// <summary>
    /// Every knob that decides how hard a night is. One asset, tuned by ear - no code edit and
    /// no re-authoring of timelines.
    ///
    /// The pacing rules here are also what the generated plan is validated against, so a value
    /// that makes nights unplayable shows up as rejected plans in the console rather than as a
    /// night the player quietly cannot win.
    /// </summary>
    [CreateAssetMenu(fileName = "DifficultyProfile", menuName = "Give Me A Sign/Difficulty Profile")]
    public class DifficultyProfile : ScriptableObject
    {
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

        /// <summary>Threat budget for a given night, clamped to the ceiling.</summary>
        public int ThreatBudgetFor(int nightIndex)
        {
            int extra = Mathf.RoundToInt(budgetGrowthPerNight * Mathf.Max(0, nightIndex - 1));
            return Mathf.Clamp(baseThreatBudget + extra, 1, maxThreatBudget);
        }

        /// <summary>Number of scheduled glitches for a given night.</summary>
        public int GlitchCountFor(int nightIndex)
        {
            int extra = Mathf.RoundToInt(glitchGrowthPerNight * Mathf.Max(0, nightIndex - 1));
            return Mathf.Max(0, baseGlitchCount + extra);
        }

        /// <summary>Score needed to survive a night with this many anomalies.</summary>
        public int RequiredScoreFor(int anomalyCount)
        {
            if (anomalyCount <= 0) return 0;

            // At least one - a night you win by doing nothing isn't a night.
            return Mathf.Clamp(Mathf.CeilToInt(anomalyCount * winRatio), 1, anomalyCount);
        }
    }
}
