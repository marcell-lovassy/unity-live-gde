using System.Collections.Generic;

namespace LiveGameDataEditor.Editor
{
    /// <summary>Severity level of a validation result.</summary>
    public enum ValidationSeverity
    {
        Warning,
        Error
    }

    /// <summary>
    /// The result of a single validation rule applied to one entry.
    /// Immutable value type.
    /// </summary>
    public readonly struct ValidationResult
    {
        /// <summary>Zero-based index of the offending entry in the container's Entries list.</summary>
        public readonly int RowIndex;

        /// <summary>
        /// Name of the specific field that failed (e.g. "Id"), or empty string for entry-level issues.
        /// </summary>
        public readonly string FieldName;

        /// <summary>Human-readable description of the validation failure.</summary>
        public readonly string Message;

        /// <summary>Severity of this failure.</summary>
        public readonly ValidationSeverity Severity;

        public ValidationResult(int rowIndex, string fieldName, string message, ValidationSeverity severity)
        {
            RowIndex  = rowIndex;
            FieldName = fieldName;
            Message   = message;
            Severity  = severity;
        }
    }

    // ── Collection-level validator interface ───────────────────────────────────────

    /// <summary>
    /// Validates a full list of entries (enables cross-row rules such as duplicate checks).
    /// Implement this interface and register with <see cref="GameDataValidationService"/>
    /// to plug in custom validation without changing any UI code.
    /// </summary>
    public interface IGameDataValidator
    {
        IEnumerable<ValidationResult> Validate(IReadOnlyList<IGameDataEntry> entries);
    }
}
