namespace LiveGameDataEditor
{
    /// <summary>
    /// Runtime extension point for per-entry validation.
    /// Passes a single entry for lightweight, entry-scoped checks
    /// (e.g. range validation, format checks).
    ///
    /// For cross-entry rules (e.g. duplicate Id detection), use the editor-side
    /// <c>LiveGameDataEditor.Editor.IGameDataValidator</c> instead.
    ///
    /// Plug an implementation into <c>GameDataService.OnValidateEntry</c>.
    /// </summary>
    public interface IDataValidator
    {
        /// <summary>
        /// Validates a single entry. Returns true if valid.
        /// </summary>
        bool Validate(IGameDataEntry entry, out string errorMessage);
    }
}
