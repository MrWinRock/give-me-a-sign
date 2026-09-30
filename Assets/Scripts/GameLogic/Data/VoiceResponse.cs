namespace GameLogic.Data
{
    /// <summary>How loudly the guard has to deal with an anomaly over the radio.</summary>
    public enum VoiceResponse
    {
        None,    // report it at any volume
        Whisper, // report it quietly
        Shout,   // report it loudly
        Silence, // can't be reported - stay quiet until it leaves
    }
}
