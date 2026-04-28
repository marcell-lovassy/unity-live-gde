using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class BoolFieldDrawer : ITableFieldDrawer
    {
        public bool CanDraw(TableFieldContext context)
        {
            return context.Column.IsBool;
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            var value = context.CurrentValue is bool boolValue && boolValue;
            var field = new Toggle { value = value };
            field.RegisterValueChangedCallback(evt =>
            {
                context.SetValue(evt.newValue);
            });
            return field;
        }
    }
}
