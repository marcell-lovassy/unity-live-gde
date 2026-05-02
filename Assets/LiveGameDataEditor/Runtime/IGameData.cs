namespace LiveGameDataEditor
{
    /// <summary>
    ///     Marker interface for a single game data entry.
    ///     Any class implementing this can be used as a row type in the Game Data Spreadsheet Editor.
    /// </summary>
    public interface IGameData
    {
        /// <summary>Unique identifier for this entry. Used by validators and search.</summary>
        string Id { get; set; }
    }
}