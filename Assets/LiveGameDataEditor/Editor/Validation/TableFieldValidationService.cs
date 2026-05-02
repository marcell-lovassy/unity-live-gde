using System.Collections.Generic;
using System.Linq;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    ///     Runs registered field validators against every reflected column in a table.
    /// </summary>
    public static class TableFieldValidationService
    {
        public static readonly List<ITableFieldValidator> Validators = new()
        {
            new ReferenceFieldValidator(),
            new ColorStringFieldValidator(),
            new AssetGuidFieldValidator(),
            new RangeFieldValidator(),
            new FlagsFieldValidator()
        };

        public static IEnumerable<ValidationResult> RunAll(IGameDataContainer container)
        {
            if (container == null) yield break;

            var entries = container.GetEntries().Cast<IGameData>().ToList();
            var columns = GameDataColumnDefinition.FromType(container.EntryType);

            for (var rowIndex = 0; rowIndex < entries.Count; rowIndex++)
            {
                var entry = entries[rowIndex];
                foreach (var column in columns)
                {
                    var context = new TableValidationContext(
                        container,
                        entries,
                        columns,
                        entry,
                        rowIndex,
                        column,
                        column.Field.GetValue(entry));

                    foreach (var validator in Validators)
                    {
                        if (!validator.CanValidate(context)) continue;

                        foreach (var result in validator.Validate(context)) yield return result;
                    }
                }
            }
        }
    }
}