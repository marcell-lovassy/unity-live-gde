using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    ///     Creates a table cell editor for a reflected game data field.
    /// </summary>
    public interface ITableFieldDrawer
    {
        bool CanDraw(TableFieldContext context);
        VisualElement CreateCell(TableFieldContext context);
    }
}