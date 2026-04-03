namespace LiveGameDataEditor
{
    /// <summary>
    /// Future extension point: implement this interface to add per-entry validation
    /// without changing the table or row UI code.
    /// Plug an implementation into GameDataService.OnValidateEntry.
    /// </summary>
    public interface IDataValidator
    {
        /// <summary>
        /// Validates a single entry. Returns true if valid.
        /// </summary>
        bool Validate(GameDataEntry entry, out string errorMessage);
    }
}
