using System.Collections.Generic;

namespace LiveGameDataEditor.Editor
{
    public sealed class ReferenceFieldValidator : ITableFieldValidator
    {
        public bool CanValidate(TableValidationContext context)
        {
            return ReferenceTableResolver.IsReferenceField(context.FieldInfo);
        }

        public IEnumerable<ValidationResult> Validate(TableValidationContext context)
        {
            var resolved = ReferenceTableResolver.Resolve(context.FieldInfo);

            foreach (var error in resolved.Errors)
            {
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    error,
                    ValidationSeverity.Error);
            }

            if (context.FieldType != typeof(string))
            {
                yield break;
            }

            if (!resolved.HasKeyField)
            {
                yield break;
            }

            var currentKey = context.CurrentValue as string;
            if (string.IsNullOrEmpty(currentKey))
            {
                yield break;
            }

            if (!resolved.ContainsKey(currentKey))
            {
                yield return new ValidationResult(
                    context.RowIndex,
                    context.FieldInfo.Name,
                    $"Missing reference: {context.FieldInfo.Name} = '{currentKey}' does not exist in {resolved.TargetTableType.Name}.",
                    ValidationSeverity.Error);
            }
        }
    }
}
