using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    ///     Context passed to table field drawers when constructing a cell.
    /// </summary>
    public sealed class TableFieldContext
    {
        public TableFieldContext(
            IGameData source,
            IReadOnlyList<GameDataColumnDefinition> columns,
            GameDataColumnDefinition column,
            object currentValue,
            Action<object> setValue,
            Action rebuildRequested)
        {
            Source = source;
            Columns = columns;
            Column = column;
            FieldInfo = column.Field;
            FieldType = column.FieldType;
            CurrentValue = currentValue;
            SetValue = setValue;
            RebuildRequested = rebuildRequested;
        }

        public IGameData Source { get; }
        public IReadOnlyList<GameDataColumnDefinition> Columns { get; }
        public GameDataColumnDefinition Column { get; }
        public FieldInfo FieldInfo { get; }
        public Type FieldType { get; }
        public object CurrentValue { get; }
        public Action<object> SetValue { get; }
        public Action RebuildRequested { get; }
    }
}