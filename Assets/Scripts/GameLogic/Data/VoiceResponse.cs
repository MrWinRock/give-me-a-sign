namespace GameLogic.Data
{
    /// <summary>How loudly the guard has to deal with an anomaly over the radio.</summary>
    public enum VoiceResponse
    {
        None,    // report it at any volume
        Whisper, // report it quietly
        Shout,   // report it loudly
        Silence, // stealth: mic is held live; whisper the report (or say nothing) - anything louder is heard
    }
}
