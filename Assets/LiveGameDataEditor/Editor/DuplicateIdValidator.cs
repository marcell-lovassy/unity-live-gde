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

            foreach (var t in entries)
            {
                var id = t.Id ?? string.Empty;
                idCount.TryGetValue(id, out var count);
                idCount[id] = count + 1;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var id = entries[i].Id ?? string.Empty;
                if (idCount.TryGetValue(id, out var count) && count > 1)
                {
                    yield return new ValidationResult(
                        i, nameof(IGameDataEntry.Id),
                        $"Duplicate Id \"{id}\"",
                        ValidationSeverity.Error);
                }
            }
        }
    }
}
