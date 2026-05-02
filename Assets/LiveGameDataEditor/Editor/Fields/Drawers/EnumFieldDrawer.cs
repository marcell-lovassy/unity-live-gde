using System;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class EnumFieldDrawer : ITableFieldDrawer
    {
        public bool CanDraw(TableFieldContext context)
        {
            return context.Column.IsEnum;
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            var enumValue = context.CurrentValue as Enum
                            ?? (Enum)Enum.GetValues(context.FieldType).GetValue(0);
            var field = new EnumField(enumValue);
            field.RegisterValueChangedCallback(evt => { context.SetValue(evt.newValue); });
            return field;
        }
    }
}