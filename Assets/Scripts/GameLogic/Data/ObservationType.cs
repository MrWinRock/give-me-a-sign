namespace GameLogic.Data
{
    /// <summary>
    /// What the guard reports seeing - the kind of change in the room, not the anomaly's name.
    /// The words that count for each live in <see cref="ObservationVocabulary"/>.
    /// </summary>
    public enum ObservationType
    {
        Intruder,
        Shadow,
        ObjectMoved,
        ExtraObject,
        MissingObject,
        Door,
        Light,
        Picture,
        Demon,
    }
}
