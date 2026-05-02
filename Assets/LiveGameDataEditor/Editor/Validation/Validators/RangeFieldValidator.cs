using System.Collections.Generic;
using System.Reflection;

namespace LiveGameDataEditor.Editor
{
    public sealed class RangeFieldValidator : ITableFieldValidator
    {
        public bool CanValidate(TableValidationContext context)
        {
            return context.FieldInfo.GetCustomAttribute<TableRangeAttribute>() != null;
        }

        public IEnumerable<ValidationResult> Validate(TableValidationContext context)
        {
            var attribute = context.FieldInfo.GetCustomAttribute<TableRangeAttribute>();
            if (context.FieldType != typeof(int) && context.FieldType != typeof(float))
            {
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    $"[TableRange] can only be used on int or float fields: {context.FieldInfo.Name}.",
                    ValidationSeverity.Error);
                yield break;
            }

            if (attribute.Min > attribute.Max)
            {
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    $"[TableRange] minimum is greater than maximum on {context.FieldInfo.Name}.",
                    ValidationSeverity.Error);
                yield break;
            }

            var value = context.FieldType == typeof(int)
                ? context.CurrentValue is int intValue ? intValue : 0
                : context.CurrentValue is float floatValue
                    ? floatValue
                    : 0f;

            if (value < attribute.Min || value > attribute.Max)
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    $"{context.FieldInfo.Name} is outside the allowed range {attribute.Min} to {attribute.Max}.",
                    ValidationSeverity.Warning);
        }
    }
}