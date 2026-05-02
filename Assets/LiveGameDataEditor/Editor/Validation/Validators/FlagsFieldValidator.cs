using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiveGameDataEditor.Editor
{
    public sealed class FlagsFieldValidator : ITableFieldValidator
    {
        public bool CanValidate(TableValidationContext context)
        {
            return context.FieldInfo.GetCustomAttribute<TableFlagsAttribute>() != null;
        }

        public IEnumerable<ValidationResult> Validate(TableValidationContext context)
        {
            if (!context.FieldType.IsEnum)
            {
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    $"[TableFlags] can only be used on enum fields: {context.FieldInfo.Name}.",
                    ValidationSeverity.Error);
                yield break;
            }

            if (context.FieldType.GetCustomAttribute<FlagsAttribute>() == null)
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    $"[TableFlags] is used on enum {context.FieldType.Name}, but the enum is missing [Flags].",
                    ValidationSeverity.Warning);
        }
    }
}