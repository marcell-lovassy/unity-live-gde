using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class UnityObjectFieldDrawer : ITableFieldDrawer
    {
        public bool CanDraw(TableFieldContext context)
        {
            return context.Column.IsUnityObject;
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            var field = new ObjectField
            {
                objectType = context.FieldType,
                value = context.CurrentValue as Object
            };
            field.RegisterValueChangedCallback(evt => { context.SetValue(evt.newValue); });
            return field;
        }
    }
}