using System.Collections.Generic;

namespace LiveGameDataEditor.Editor
{
    /// <summary>Flags entries whose <see cref="IGameData.Id" /> is null or empty.</summary>
    public class EmptyIdValidator : IGameDataValidator
    {
        public IEnumerable<ValidationResult> Validate(IReadOnlyList<IGameData> entries)
        {
            for (var i = 0; i < entries.Count; i++)
                if (string.IsNullOrEmpty(entries[i].Id))
                    yield return new ValidationResult(
                        i, nameof(IGameData.Id),
                        "Id must not be empty.",
                        ValidationSeverity.Error);
        }
    }
}