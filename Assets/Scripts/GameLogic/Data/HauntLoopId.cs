namespace GameLogic.Data
{
    /// <summary>
    /// Identifies one ambient "haunt loop" - a scary system independent of any single anomaly,
    /// scheduled by <see cref="GameLogic.Night.HauntProfile"/> into
    /// <see cref="GameLogic.Night.NightPlan.haunts"/> and fired by Report.HauntDirector at its
    /// scheduled minute. Also referenced by <see cref="AnomalyDefinition.linkedHaunt"/> for an
    /// anomaly kind that is thematically tied to one (e.g. a future "Listener" anomaly to
    /// SilenceProtocol) without that link having any code meaning yet.
    ///
    /// Values beyond SilenceProtocol are reserved ahead of the Sprints that implement them
    /// (see Docs/Roadmap-8-Weeks-Steam.md), the same way GlitchType pre-declares all five glitch
    /// kinds before every executor exists.
    /// </summary>
    public enum HauntLoopId
    {
        None = 0,

        /// <summary>HL-3. Stay under the mic's danger threshold (or whisper to clear it faster) or get caught. Sprint 4.</summary>
        SilenceProtocol = 1,

        /// <summary>HL-4. Answer a scripted radio ping by voice within a few seconds. Sprint 5.</summary>
        RadioCheck = 2,

        /// <summary>HL-5. The camera feed itself lies (loop / frozen / blackout / ghost room / mirror). Sprint 5.</summary>
        CameraBetrayal = 3,

        /// <summary>HL-6. A report the player never filed shows up in the case history. Sprint 6.</summary>
        ImpostorCase = 4,
    }
}
