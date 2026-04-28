using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class FloatFieldDrawer : ITableFieldDrawer
    {
        public bool CanDraw(TableFieldContext context)
        {
            return context.Column.IsFloat;
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            var value = context.CurrentValue is float floatValue ? floatValue : 0f;
            var field = new FloatField { value = value };
            field.RegisterValueChangedCallback(evt =>
            {
                context.SetValue(evt.newValue);
            });
            return field;
        }
    }
}
