using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class RangeNumberFieldDrawer : ITableFieldDrawer
    {
        public bool CanDraw(TableFieldContext context)
        {
            return context.FieldInfo.GetCustomAttribute<TableRangeAttribute>() != null;
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            var attribute = context.FieldInfo.GetCustomAttribute<TableRangeAttribute>();
            if (attribute.Min > attribute.Max)
            {
                return CreateUnsupported(context, "[TableRange] minimum must be less than or equal to maximum.");
            }

            if (context.FieldType == typeof(int))
            {
                return CreateIntCell(context, attribute);
            }

            if (context.FieldType == typeof(float))
            {
                return CreateFloatCell(context, attribute);
            }

            return CreateUnsupported(context, "[TableRange] can only be used on int or float fields.");
        }

        private static VisualElement CreateIntCell(TableFieldContext context, TableRangeAttribute attribute)
        {
            var root = CreateRoot();
            var min = Mathf.RoundToInt(attribute.Min);
            var max = Mathf.RoundToInt(attribute.Max);
            var value = context.CurrentValue is int intValue ? Mathf.Clamp(intValue, min, max) : min;

            var slider = new SliderInt(min, max) { value = value };
            slider.style.flexGrow = 1;
            var field = new IntegerField { value = value };
            field.style.width = 56;

            slider.RegisterValueChangedCallback(evt =>
            {
                field.SetValueWithoutNotify(evt.newValue);
                context.SetValue(evt.newValue);
            });
            field.RegisterValueChangedCallback(evt =>
            {
                var clamped = Mathf.Clamp(evt.newValue, min, max);
                field.SetValueWithoutNotify(clamped);
                slider.SetValueWithoutNotify(clamped);
                context.SetValue(clamped);
            });

            root.Add(slider);
            root.Add(field);
            return root;
        }

        private static VisualElement CreateFloatCell(TableFieldContext context, TableRangeAttribute attribute)
        {
            var root = CreateRoot();
            var value = context.CurrentValue is float floatValue
                ? Mathf.Clamp(floatValue, attribute.Min, attribute.Max)
                : attribute.Min;

            var slider = new Slider(attribute.Min, attribute.Max) { value = value };
            slider.style.flexGrow = 1;
            var field = new FloatField { value = value };
            field.style.width = 64;

            slider.RegisterValueChangedCallback(evt =>
            {
                field.SetValueWithoutNotify(evt.newValue);
                context.SetValue(evt.newValue);
            });
            field.RegisterValueChangedCallback(evt =>
            {
                var clamped = Mathf.Clamp(evt.newValue, attribute.Min, attribute.Max);
                field.SetValueWithoutNotify(clamped);
                slider.SetValueWithoutNotify(clamped);
                context.SetValue(clamped);
            });

            root.Add(slider);
            root.Add(field);
            return root;
        }

        private static VisualElement CreateRoot()
        {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;
            return root;
        }

        private static VisualElement CreateUnsupported(TableFieldContext context, string tooltip)
        {
            var unsupported = new Label(context.CurrentValue?.ToString() ?? string.Empty);
            unsupported.AddToClassList("col-readonly");
            unsupported.tooltip = tooltip;
            return unsupported;
        }
    }
}
