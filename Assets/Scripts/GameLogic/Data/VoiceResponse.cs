namespace GameLogic.Data
{
    /// <summary>How loudly the guard has to deal with an anomaly over the radio.</summary>
    public enum VoiceResponse
    {
        // Explicit values: assets store the number, and 1 (the old Whisper) is retired.
        None = 0,    // report it at any volume
        Shout = 2,   // report it loudly
        Silence = 3, // stealth: mic is held live and the player must not speak at all until it leaves
    }
}
