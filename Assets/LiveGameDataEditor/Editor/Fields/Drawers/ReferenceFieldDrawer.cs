using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    public sealed class ReferenceFieldDrawer : ITableFieldDrawer
    {
        private const string EmptyKey = "";

        public bool CanDraw(TableFieldContext context)
        {
            return ReferenceTableResolver.IsReferenceField(context.FieldInfo);
        }

        public VisualElement CreateCell(TableFieldContext context)
        {
            if (context.FieldType != typeof(string))
            {
                var unsupported = new Label(context.CurrentValue?.ToString() ?? string.Empty);
                unsupported.AddToClassList("col-readonly");
                unsupported.tooltip = "[TableReference] can only be used on string fields.";
                return unsupported;
            }

            var resolved = ReferenceTableResolver.Resolve(context.FieldInfo);
            var currentKey = context.CurrentValue as string ?? EmptyKey;

            var keysByLabel = new Dictionary<string, string>();
            var labels = new List<string>();
            AddOption(labels, keysByLabel, "(None)", EmptyKey);

            foreach (var option in resolved.Options)
            {
                AddOption(labels, keysByLabel, ReferenceTableResolver.GetOptionLabel(option), option.Key);
            }

            string selectedLabel;
            if (string.IsNullOrEmpty(currentKey))
            {
                selectedLabel = "(None)";
            }
            else
            {
                selectedLabel = labels.FirstOrDefault(label => keysByLabel[label] == currentKey);
                if (string.IsNullOrEmpty(selectedLabel))
                {
                    selectedLabel = $"Missing: {currentKey}";
                    AddOption(labels, keysByLabel, selectedLabel, currentKey);
                }
            }

            var popup = new PopupField<string>(labels, selectedLabel);
            popup.tooltip = resolved.Errors.Count > 0
                ? string.Join("\n", resolved.Errors)
                : "Select a referenced row key.";
            popup.RegisterValueChangedCallback(evt =>
            {
                if (keysByLabel.TryGetValue(evt.newValue, out var key))
                {
                    context.SetValue(key);
                }
            });

            return popup;
        }

        private static void AddOption(
            List<string> labels,
            Dictionary<string, string> keysByLabel,
            string label,
            string key)
        {
            var uniqueLabel = label;
            var suffix = 2;
            while (keysByLabel.ContainsKey(uniqueLabel))
            {
                uniqueLabel = $"{label} ({suffix})";
                suffix++;
            }

            labels.Add(uniqueLabel);
            keysByLabel[uniqueLabel] = key;
        }
    }
}
