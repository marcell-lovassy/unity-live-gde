using System.Collections.Generic;
using System.Reflection;

namespace LiveGameDataEditor.Editor
{
    public sealed class ColorStringFieldValidator : ITableFieldValidator
    {
        public bool CanValidate(TableValidationContext context)
        {
            return context.FieldInfo.GetCustomAttribute<TableColorAttribute>() != null;
        }

        public IEnumerable<ValidationResult> Validate(TableValidationContext context)
        {
            if (context.FieldType != typeof(string))
            {
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    $"[TableColor] can only be used on string fields: {context.FieldInfo.Name}.",
                    ValidationSeverity.Error);
                yield break;
            }

            var value = context.CurrentValue as string;
            if (string.IsNullOrWhiteSpace(value)) yield break;

            if (!ColorStringUtility.TryParseHtmlColor(value, out _))
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    "Invalid color format. Expected #RRGGBB or #RRGGBBAA.",
                    ValidationSeverity.Error);
        }
    }
}