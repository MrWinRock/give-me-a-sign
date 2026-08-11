namespace GameLogic.Data
{
    /// <summary>
    /// Identifies one ambient "haunt loop" - a scary system independent of any single anomaly,
    /// scheduled by <see cref="GameLogic.Night.HauntProfile"/> into
    /// <see cref="GameLogic.Night.NightPlan.haunts"/> and fired by Report.HauntDirector at its
    /// scheduled minute. Also referenced by <see cref="AnomalyDefinition.linkedHaunt"/> for an
    /// anomaly kind that is thematically tied to one (e.g. a future "Listener" anomaly to
    /// SilenceProtocol) without that link having any code meaning yet.
    /// </summary>
    public enum HauntLoopId
    {
        None = 0,

        SilenceProtocol = 1,

        RadioCheck = 2,

        CameraBetrayal = 3,

        ImpostorCase = 4,
    }
}
