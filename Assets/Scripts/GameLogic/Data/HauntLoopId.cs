namespace GameLogic.Data
{
    /// <summary>
    /// Identifies the ambient "haunt loop" an anomaly is tied to. Sprint 4 builds the loops
    /// themselves; this enum exists now only so
    /// <see cref="AnomalyDefinition.linkedHaunt"/> has a type, and so authoring a definition
    /// today doesn't have to be revisited later just to add the field.
    /// </summary>
    public enum HauntLoopId
    {
        None = 0,
    }
}
