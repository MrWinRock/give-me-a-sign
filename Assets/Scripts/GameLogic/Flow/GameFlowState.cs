namespace GameLogic.Flow
{
    /// <summary>Where the campaign loop currently is. Owned by GameFlowManager.</summary>
    public enum GameFlowState
    {
        /// <summary>The XP desktop. Waiting for the player to start the shift.</summary>
        MainMenu,

        /// <summary>The Incident Report loop for CurrentDay is running.</summary>
        DayGameplay,

        /// <summary>The day was survived; rolling and playing the optional VDO/Minigame.</summary>
        DayEndEvent,

        /// <summary>Day 7 is done and the ending sequence is playing.</summary>
        Ending,
    }
}
