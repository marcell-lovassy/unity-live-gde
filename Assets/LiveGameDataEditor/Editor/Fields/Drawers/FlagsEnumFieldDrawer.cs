using System;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class FlagsEnumFieldDrawer : ITableFieldDrawer
    {
        public bool CanDraw(TableFieldContext context)
        {
            return context.FieldInfo.GetCustomAttribute<TableFlagsAttribute>() != null;
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            if (!context.FieldType.IsEnum)
            {
                var unsupported = new Label(context.CurrentValue?.ToString() ?? string.Empty);
                unsupported.AddToClassList("col-readonly");
                unsupported.tooltip = "[TableFlags] can only be used on enum fields.";
                return unsupported;
            }

            var value = context.CurrentValue as Enum
                        ?? (Enum)Enum.GetValues(context.FieldType).GetValue(0);
            var field = new EnumFlagsField(value);
            field.RegisterValueChangedCallback(evt => { context.SetValue(evt.newValue); });
            return field;
        }
    }
}