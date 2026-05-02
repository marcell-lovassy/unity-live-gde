using System;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Overrides the column header label shown in the Game Data Spreadsheet Editor table.
    ///     If absent the editor uses the field's name as-is.
    /// </summary>
    /// <example>
    ///     <code>
    /// [ColumnHeader("Max HP")]
    /// public int MaxHealth;
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ColumnHeaderAttribute : Attribute
    {
        public ColumnHeaderAttribute(string label)
        {
            Label = label;
        }

        /// <summary>The label to display in the column header.</summary>
        public string Label { get; }
    }
}