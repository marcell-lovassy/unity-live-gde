using System.Collections.Generic;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Validates one field on one row.
    /// </summary>
    public interface ITableFieldValidator
    {
        bool CanValidate(TableValidationContext context);
        IEnumerable<ValidationResult> Validate(TableValidationContext context);
    }
}
