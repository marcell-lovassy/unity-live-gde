using System;
using System.Collections.Generic;

namespace LiveGameDataEditor.Editor
{
    /// <summary>Flags entries whose <see cref="IGameDataEntry.Id"/> appears more than once.</summary>
    public class DuplicateIdValidator : IGameDataValidator
    {
        public IEnumerable<ValidationResult> Validate(IReadOnlyList<IGameDataEntry> entries)
        {
            var idCount = new Dictionary<string, int>(entries.Count, StringComparer.Ordinal);

            for (int i = 0; i < entries.Count; i++)
            {
                string id = entries[i].Id ?? string.Empty;
                idCount.TryGetValue(id, out int count);
                idCount[id] = count + 1;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                string id = entries[i].Id ?? string.Empty;
                if (idCount.TryGetValue(id, out int count) && count > 1)
                    yield return new ValidationResult(
                        i, nameof(IGameDataEntry.Id),
                        $"Duplicate Id \"{id}\"",
                        ValidationSeverity.Error);
            }
        }
    }
}
