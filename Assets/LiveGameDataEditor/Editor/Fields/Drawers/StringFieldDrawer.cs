using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class StringFieldDrawer : ITableFieldDrawer
    {
        public bool CanDraw(TableFieldContext context)
        {
            return context.Column.IsString;
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            var field = new TextField { value = (string)(context.CurrentValue ?? string.Empty) };
            field.RegisterValueChangedCallback(evt => { context.SetValue(evt.newValue); });
            return field;
        }
    }
}