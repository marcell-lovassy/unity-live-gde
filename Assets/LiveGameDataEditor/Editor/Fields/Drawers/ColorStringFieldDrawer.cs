using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class ColorStringFieldDrawer : ITableFieldDrawer
    {
        public bool CanDraw(TableFieldContext context)
        {
            return context.FieldInfo.GetCustomAttribute<TableColorAttribute>() != null;
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            var attribute = context.FieldInfo.GetCustomAttribute<TableColorAttribute>();
            if (context.FieldType != typeof(string))
            {
                var unsupported = new Label(context.CurrentValue?.ToString() ?? string.Empty);
                unsupported.AddToClassList("col-readonly");
                unsupported.tooltip = "[TableColor] can only be used on string fields.";
                return unsupported;
            }

            var currentText = context.CurrentValue as string;
            if (!ColorStringUtility.TryParseHtmlColor(currentText, out var color)) color = Color.white;

            var field = new ColorField
            {
                value = color,
                showAlpha = attribute.IncludeAlpha
            };
            field.RegisterValueChangedCallback(evt =>
            {
                context.SetValue(ColorStringUtility.ToHex(evt.newValue, attribute.IncludeAlpha));
            });
            return field;
        }
    }
}