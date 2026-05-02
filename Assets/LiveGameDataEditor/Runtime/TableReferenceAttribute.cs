using System;

namespace LiveGameDataEditor
{
    /// <summary>
    ///     Renders a field as a reference to a row in another game data table.
    ///     The field stores the referenced row key, not an object reference.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TableReferenceAttribute : Attribute
    {
        public TableReferenceAttribute(Type targetTableType)
        {
            TargetTableType = targetTableType;
        }

        public Type TargetTableType { get; }
    }
}