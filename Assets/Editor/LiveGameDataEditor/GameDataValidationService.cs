using System.Collections.Generic;
using System.Linq;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Runs all registered <see cref="IGameDataValidator"/> implementations and
    /// returns per-row results. Designed to be called after every data mutation.
    /// </summary>
    public static class GameDataValidationService
    {
        /// <summary>
        /// The active validators. Add or remove entries to customise validation behaviour.
        /// Populated with the built-in validators by default.
        /// </summary>
        public static readonly List<IGameDataValidator> Validators = new()
        {
            new EmptyIdValidator(),
            new DuplicateIdValidator(),
        };

        /// <summary>
        /// Runs all <see cref="Validators"/> against the entries from <paramref name="container"/>
        /// and returns a dictionary keyed by row index. Rows with no issues are absent.
        /// </summary>
        public static Dictionary<int, List<ValidationResult>> RunAll(IGameDataContainer container)
        {
            var entries = container.GetEntries().Cast<IGameDataEntry>().ToList();
            return RunAll(entries);
        }

        /// <summary>
        /// Runs all <see cref="Validators"/> against <paramref name="entries"/> and returns
        /// a dictionary keyed by row index. Rows with no issues are absent from the result.
        /// </summary>
        public static Dictionary<int, List<ValidationResult>> RunAll(
            IReadOnlyList<IGameDataEntry> entries)
        {
            var results = new Dictionary<int, List<ValidationResult>>();

            foreach (var validator in Validators)
            {
                foreach (var result in validator.Validate(entries))
                {
                    if (!results.TryGetValue(result.RowIndex, out var list))
                    {
                        list = new List<ValidationResult>();
                        results[result.RowIndex] = list;
                    }
                    list.Add(result);
                }
            }

            return results;
        }
    }
}
