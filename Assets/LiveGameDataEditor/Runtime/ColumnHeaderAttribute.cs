using System;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Overrides the column header label shown in the Live Game Data Editor table.
    /// If absent the editor uses the field's name as-is.
    /// </summary>
    /// <example>
    /// <code>
    /// [ColumnHeader("Max HP")]
    /// public int MaxHealth;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class ColumnHeaderAttribute : Attribute
    {
        /// <summary>The label to display in the column header.</summary>
        public string Label { get; }

        public ColumnHeaderAttribute(string label) => Label = label;
    }
}
