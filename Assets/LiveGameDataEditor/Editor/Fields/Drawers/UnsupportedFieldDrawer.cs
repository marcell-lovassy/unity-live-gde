using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class UnsupportedFieldDrawer : ITableFieldDrawer
    {
        public bool CanDraw(TableFieldContext context)
        {
            return true;
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            var field = new Label(context.CurrentValue?.ToString() ?? string.Empty);
            field.AddToClassList("col-readonly");
            return field;
        }
    }
}
