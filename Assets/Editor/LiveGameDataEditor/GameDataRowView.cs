using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Renders a single <see cref="GameDataEntry"/> as an inline-editable table row.
    /// Field widgets are built dynamically from <see cref="GameDataColumnDefinition"/> so the
    /// row adapts automatically when the data model changes.
    ///
    /// The row never mutates the original entry directly.
    /// It fires <see cref="OnEntryChanged"/> with a freshly constructed entry so the
    /// caller can call Undo.RecordObject before committing the change.
    /// </summary>
    public class GameDataRowView : VisualElement
    {
        /// <summary>
        /// Raised when any field is edited. Argument is a new entry with updated values.
        /// Caller must call Undo.RecordObject before assigning it to the container.
        /// </summary>
        public event Action<GameDataEntry> OnEntryChanged;

        /// <summary>
        /// Raised when the row is clicked. Bool = whether Ctrl/Shift/Cmd was held (multi-select).
        /// </summary>
        public event Action<bool> OnSelectionToggled;

        // Local field values keyed by field name — never references the original entry.
        private readonly Dictionary<string, object> _fieldValues = new();
        private readonly IReadOnlyList<GameDataColumnDefinition> _columns;

        public GameDataRowView(
            GameDataEntry entry,
            IReadOnlyList<GameDataColumnDefinition> columns,
            bool isAlternateRow)
        {
            _columns = columns;

            // Snapshot initial values from the entry
            foreach (var col in _columns)
                _fieldValues[col.Field.Name] = col.Field.GetValue(entry);

            AddToClassList("table-row");
            if (isAlternateRow) AddToClassList("table-row--alternate");

            RegisterCallback<ClickEvent>(evt =>
                OnSelectionToggled?.Invoke(evt.ctrlKey || evt.shiftKey || evt.commandKey));

            BuildFields();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        public void SetSelected(bool selected) =>
            EnableInClassList("table-row--selected", selected);

        /// <summary>
        /// Applies validation highlighting and a tooltip to the row.
        /// Pass <c>null</c> or an empty list to clear any existing state.
        /// </summary>
        public void SetValidationState(List<ValidationResult> results)
        {
            bool hasError   = results != null && results.Any(r => r.Severity == ValidationSeverity.Error);
            bool hasWarning = results != null && results.Any(r => r.Severity == ValidationSeverity.Warning);

            EnableInClassList("table-row--invalid", hasError);
            EnableInClassList("table-row--warning", hasWarning && !hasError);

            tooltip = (results != null && results.Count > 0)
                ? string.Join("\n", results.Select(r => $"[{r.Severity}] {r.Message}"))
                : string.Empty;
        }

        // ── UI construction ────────────────────────────────────────────────────────

        private void BuildFields()
        {
            // Narrow gutter for alignment with the header
            var gutter = new VisualElement();
            gutter.AddToClassList("col-gutter");
            Add(gutter);

            foreach (var col in _columns)
            {
                var field = CreateField(col);
                field.AddToClassList($"col-{col.Field.Name.ToLower()}");
                ApplySizing(field, col);
                Add(field);
            }
        }

        /// <summary>Creates the appropriate input widget for the column's field type.</summary>
        private VisualElement CreateField(GameDataColumnDefinition col)
        {
            string name = col.Field.Name;

            if (col.IsString)
            {
                var tf = new TextField { value = (string)(_fieldValues[name] ?? "") };
                tf.RegisterValueChangedCallback(evt =>
                {
                    _fieldValues[name] = evt.newValue;
                    OnEntryChanged?.Invoke(MakeEntry());
                });
                return tf;
            }

            if (col.IsInt)
            {
                var intf = new IntegerField { value = (int)_fieldValues[name] };
                intf.RegisterValueChangedCallback(evt =>
                {
                    _fieldValues[name] = evt.newValue;
                    OnEntryChanged?.Invoke(MakeEntry());
                });
                return intf;
            }

            if (col.IsFloat)
            {
                var ff = new FloatField { value = (float)_fieldValues[name] };
                ff.RegisterValueChangedCallback(evt =>
                {
                    _fieldValues[name] = evt.newValue;
                    OnEntryChanged?.Invoke(MakeEntry());
                });
                return ff;
            }

            if (col.IsBool)
            {
                var toggle = new Toggle { value = (bool)_fieldValues[name] };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    _fieldValues[name] = evt.newValue;
                    OnEntryChanged?.Invoke(MakeEntry());
                });
                return toggle;
            }

            // Fallback for unsupported types: read-only label
            var label = new Label(_fieldValues[name]?.ToString() ?? "");
            label.AddToClassList("col-readonly");
            return label;
        }

        /// <summary>
        /// Applies flex sizing from the column definition as inline styles.
        /// USS classes can still override these values if desired.
        /// </summary>
        private static void ApplySizing(VisualElement el, GameDataColumnDefinition col)
        {
            el.style.minWidth = col.MinWidth;
            if (col.FlexGrow < 0.01f)
            {
                el.style.flexGrow   = 0;
                el.style.flexShrink = 0;
                el.style.width      = col.MinWidth;
            }
            else
            {
                el.style.flexGrow   = col.FlexGrow;
                el.style.flexShrink = 1;
            }
        }

        /// <summary>
        /// Constructs a new <see cref="GameDataEntry"/> from the row's current local field values.
        /// </summary>
        private GameDataEntry MakeEntry()
        {
            var entry = new GameDataEntry();
            foreach (var col in _columns)
            {
                if (_fieldValues.TryGetValue(col.Field.Name, out object val))
                    col.Field.SetValue(entry, val);
            }
            return entry;
        }
    }
}
