namespace GameLogic.Flow
{
    /// <summary>How a night finished. Everything except Survived is a loss.</summary>
    public enum NightOutcome
    {
        /// <summary>Reached 6:00 AM. Still only a win if the score cleared the requirement.</summary>
        Survived,
        /// <summary>An anomaly's threat timer ran out while it was still on screen.</summary>
        KilledByAnomaly,
        /// <summary>The demon's report time limit ran out.</summary>
        KilledByDemon,
        /// <summary>Reserved for Sprint 4's negligence strikes / Silence Protocol.</summary>
        Negligence,
    }

    /// <summary>
    /// The complete record of one night, built once by
    /// <see cref="GameFlowManager"/> and read by the Result scene.
    ///
    /// This replaces four loose PlayerPrefs keys ("FinalScore", "GameWon", "WinThreshold",
    /// "AnomalyTimeout") that three different scripts wrote to, each having to check whether
    /// one of the others had already written first. There is one writer now, so there is
    /// nothing to coordinate.
    /// </summary>
    [System.Serializable]
    public class NightResult
    {
        /// <summary>
        /// Sprint 6, S-603: the designed length of the campaign arc ("เล่นได้ตั้งแต่เมนู → คืน 1-5
        /// → จบเกม"). GameFlowManager.AdvanceProgression caps the unlocked-night save here rather
        /// than unlocking an undefined "night 6" - completing this night replays as a capstone
        /// instead of drifting into difficulty numbers nobody tuned for.
        /// </summary>
        public const int FinalNightIndex = 5;

        public NightOutcome outcome;
        public int nightIndex = 1;
        public int seed;
        public int score;
        public int requiredScore;
        public int anomaliesTotal;
        public int reportsFiled;
        public int reportsFailed;
        public float survivedUntilHour;
        public string killedByAnomalyId;
        public string killedInRoomId;

        /// <summary>Surviving to 6:00 AM is necessary but not sufficient - the score has to clear the bar too.</summary>
        public bool Won => outcome == NightOutcome.Survived && score >= requiredScore;

        /// <summary>Won the final night of the designed arc - the "you survived the week" ending.</summary>
        public bool IsCampaignComplete => Won && nightIndex >= FinalNightIndex;

        /// <summary>True when the night ended because something got the player, rather than at the clock.</summary>
        public bool KilledByThreat =>
            outcome == NightOutcome.KilledByAnomaly || outcome == NightOutcome.KilledByDemon;

        /// <summary>A placeholder result so the Result scene can be opened directly while testing.</summary>
        public static NightResult Dummy() => new NightResult
        {
            outcome = NightOutcome.Survived,
            score = 0,
            requiredScore = 1,
            survivedUntilHour = 6f,
        };
    }
}
