using System.Collections.Generic;

namespace LiveGameDataEditor.Editor
{
    /// <summary>Flags entries whose <see cref="GameDataEntry.Id"/> is null or empty.</summary>
    public class EmptyIdValidator : IGameDataValidator
    {
        public IEnumerable<ValidationResult> Validate(IReadOnlyList<GameDataEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.IsNullOrEmpty(entries[i].Id))
                    yield return new ValidationResult(
                        i, nameof(GameDataEntry.Id),
                        "Id must not be empty.",
                        ValidationSeverity.Error);
            }
        }
    }
}
