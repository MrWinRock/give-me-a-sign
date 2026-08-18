namespace GameLogic.Flow
{
    /// <summary>How a night finished. Everything except Survived is a loss.</summary>
    public enum NightOutcome
    {
        Survived,
        KilledByAnomaly,
        KilledByDemon,
        Negligence,
    }

    /// <summary>
    /// The complete record of one night, built once by
    /// <see cref="GameFlowManager"/> and read by the Result scene.
    /// </summary>
    [System.Serializable]
    public class NightResult
    {
        public const int FinalNightIndex = 7;

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

        public bool Won => outcome == NightOutcome.Survived && score >= requiredScore;

        public bool IsCampaignComplete => Won && nightIndex >= FinalNightIndex;

        public bool KilledByThreat =>
            outcome == NightOutcome.KilledByAnomaly || outcome == NightOutcome.KilledByDemon;

        public static NightResult Dummy() => new NightResult
        {
            outcome = NightOutcome.Survived,
            score = 0,
            requiredScore = 1,
            survivedUntilHour = 6f,
        };
    }
}
