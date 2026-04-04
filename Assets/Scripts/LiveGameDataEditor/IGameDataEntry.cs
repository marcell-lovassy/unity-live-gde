namespace LiveGameDataEditor
{
    /// <summary>
    /// Marker interface for a single game data entry.
    /// Any class implementing this can be used as a row type in the Live Game Data Editor.
    /// </summary>
    public interface IGameDataEntry
    {
        /// <summary>Unique identifier for this entry. Used by validators and search.</summary>
        string Id { get; set; }
    }
}
