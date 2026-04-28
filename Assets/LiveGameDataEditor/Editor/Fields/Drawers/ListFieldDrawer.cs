using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class ListFieldDrawer : ITableFieldDrawer
    {
        public bool CanDraw(TableFieldContext context)
        {
            return context.Column.IsList;
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            var display = GameDataColumnDefinition.ListFieldToString(context.CurrentValue, context.Column);
            var field = new TextField { value = display };
            field.RegisterValueChangedCallback(evt =>
            {
                context.SetValue(context.Column.ParseListField(evt.newValue));
            });
            return field;
        }
    }
}
