using System;

namespace LiveGameDataEditor
{
    /// <summary>
    /// Renders a field as a reference to a row in another game data table.
    /// The field stores the referenced row key, not an object reference.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class TableReferenceAttribute : Attribute
    {
        public Type TargetTableType { get; }

        public TableReferenceAttribute(Type targetTableType)
        {
            TargetTableType = targetTableType;
        }
    }
}
