using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class IntegerFieldDrawer : ITableFieldDrawer
    {
        public bool CanDraw(TableFieldContext context)
        {
            return context.Column.IsInt;
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            var value = context.CurrentValue is int intValue ? intValue : 0;
            var field = new IntegerField { value = value };
            field.RegisterValueChangedCallback(evt => { context.SetValue(evt.newValue); });
            return field;
        }
    }
}