using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Context passed to validators that inspect one reflected field on one row.
    /// </summary>
    public sealed class TableValidationContext
    {
        public IGameDataContainer Container { get; }
        public IReadOnlyList<IGameDataEntry> Entries { get; }
        public IReadOnlyList<GameDataColumnDefinition> Columns { get; }
        public IGameDataEntry Entry { get; }
        public int RowIndex { get; }
        public GameDataColumnDefinition Column { get; }
        public FieldInfo FieldInfo { get; }
        public Type FieldType { get; }
        public object CurrentValue { get; }

        public TableValidationContext(
            IGameDataContainer container,
            IReadOnlyList<IGameDataEntry> entries,
            IReadOnlyList<GameDataColumnDefinition> columns,
            IGameDataEntry entry,
            int rowIndex,
            GameDataColumnDefinition column,
            object currentValue)
        {
            Container = container;
            Entries = entries;
            Columns = columns;
            Entry = entry;
            RowIndex = rowIndex;
            Column = column;
            FieldInfo = column.Field;
            FieldType = column.FieldType;
            CurrentValue = currentValue;
        }
    }
}
