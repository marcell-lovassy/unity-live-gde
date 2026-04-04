using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Renders a single <see cref="IGameDataEntry"/> as an inline-editable table row.
    ///
    /// Supported field types (via <see cref="GameDataColumnDefinition"/>):
    ///   string → TextField
    ///   int    → IntegerField
    ///   float  → FloatField
    ///   bool   → Toggle
    ///   enum   → EnumField
    ///   UnityEngine.Object subtype → ObjectField (with thumbnail)
    ///   List&lt;T&gt; / T[] with [ListField] → TextField (items joined by separator)
    ///
    /// Keyboard navigation:
    ///   Enter      → move focus to next field in the same row
    ///   Enter (last field) / Down arrow → fire <see cref="OnRequestNextRow"/>
    ///   Escape     → blur the active field
    /// </summary>
    public class GameDataRowView : VisualElement
    {
        // ── Events ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised when any field is edited. Argument is a new entry instance with the
        /// updated values. The caller records Undo before committing.
        /// </summary>
        public event Action<IGameDataEntry> OnEntryChanged;

        /// <summary>Raised on click. Bool = whether Ctrl/Shift/Cmd was held (multi-select).</summary>
        public event Action<bool> OnSelectionToggled;

        /// <summary>
        /// Raised when the user presses Enter on the last column or the Down arrow on any column.
        /// Argument = column index where navigation originated, so the next row can focus the same column.
        /// </summary>
        public event Action<int> OnRequestNextRow;

        // ── State ──────────────────────────────────────────────────────────────────

        private readonly Dictionary<string, object>          _fieldValues   = new();
        private readonly IReadOnlyList<GameDataColumnDefinition> _columns;
        private readonly Type                                _entryType;
        private readonly List<VisualElement>                 _fieldElements = new();

        // ── Constructor ────────────────────────────────────────────────────────────

        public GameDataRowView(
            IGameDataEntry                         entry,
            IReadOnlyList<GameDataColumnDefinition> columns,
            bool                                   isAlternateRow)
        {
            _columns   = columns;
            _entryType = entry.GetType();

            // Snapshot initial values via reflection.
            foreach (var col in _columns)
                _fieldValues[col.Field.Name] = col.Field.GetValue(entry);

            AddToClassList("table-row");
            if (isAlternateRow) AddToClassList("table-row--alternate");

            // Selection is handled at the row level; fields stop propagation of their own clicks.
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

            EnableInClassList("table-row--invalid",  hasError);
            EnableInClassList("table-row--warning",  hasWarning && !hasError);

            tooltip = (results != null && results.Count > 0)
                ? string.Join("\n", results.Select(r => $"[{r.Severity}] {r.Message}"))
                : string.Empty;
        }

        /// <summary>Moves keyboard focus to the field at the given column index.</summary>
        public void FocusColumn(int colIndex)
        {
            if (colIndex < 0 || colIndex >= _fieldElements.Count) return;
            var el = _fieldElements[colIndex];
            // Defer by one frame so layout is settled before focusing.
            el.schedule.Execute(() => el.Focus()).ExecuteLater(0);
        }

        // ── UI construction ────────────────────────────────────────────────────────

        private void BuildFields()
        {
            var gutter = new VisualElement();
            gutter.AddToClassList("col-gutter");
            Add(gutter);

            for (int colIndex = 0; colIndex < _columns.Count; colIndex++)
            {
                var col   = _columns[colIndex];
                var field = CreateField(col, colIndex);
                field.AddToClassList($"col-{col.Field.Name.ToLower()}");
                ApplySizing(field, col);
                Add(field);
                _fieldElements.Add(field);
            }
        }

        private VisualElement CreateField(GameDataColumnDefinition col, int colIndex)
        {
            string name = col.Field.Name;
            VisualElement field;

            if (col.IsList)
            {
                string display = GameDataColumnDefinition.ListFieldToString(_fieldValues[name], col);
                var tf = new TextField { value = display };
                tf.RegisterValueChangedCallback(evt =>
                {
                    _fieldValues[name] = col.ParseListField(evt.newValue);
                    OnEntryChanged?.Invoke(MakeEntry());
                });
                field = tf;
            }
            else if (col.IsString)
            {
                var tf = new TextField { value = (string)(_fieldValues[name] ?? "") };
                tf.RegisterValueChangedCallback(evt =>
                {
                    _fieldValues[name] = evt.newValue;
                    OnEntryChanged?.Invoke(MakeEntry());
                });
                field = tf;
            }
            else if (col.IsInt)
            {
                var intf = new IntegerField { value = (int)_fieldValues[name] };
                intf.RegisterValueChangedCallback(evt =>
                {
                    _fieldValues[name] = evt.newValue;
                    OnEntryChanged?.Invoke(MakeEntry());
                });
                field = intf;
            }
            else if (col.IsFloat)
            {
                var ff = new FloatField { value = (float)_fieldValues[name] };
                ff.RegisterValueChangedCallback(evt =>
                {
                    _fieldValues[name] = evt.newValue;
                    OnEntryChanged?.Invoke(MakeEntry());
                });
                field = ff;
            }
            else if (col.IsBool)
            {
                var toggle = new Toggle { value = (bool)_fieldValues[name] };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    _fieldValues[name] = evt.newValue;
                    OnEntryChanged?.Invoke(MakeEntry());
                });
                field = toggle;
            }
            else if (col.IsEnum)
            {
                // GetValue returns a boxed enum; cast via Enum base type.
                var enumVal = (_fieldValues[name] as Enum)
                    ?? (Enum)Enum.GetValues(col.FieldType).GetValue(0);
                var ef = new EnumField(enumVal);
                ef.RegisterValueChangedCallback(evt =>
                {
                    _fieldValues[name] = evt.newValue;
                    OnEntryChanged?.Invoke(MakeEntry());
                });
                field = ef;
            }
            else if (col.IsUnityObject)
            {
                var objVal = _fieldValues[name] as UnityEngine.Object;
                var of = new ObjectField { objectType = col.FieldType, value = objVal };
                of.RegisterValueChangedCallback(evt =>
                {
                    _fieldValues[name] = evt.newValue;
                    OnEntryChanged?.Invoke(MakeEntry());
                });
                field = of;
            }
            else
            {
                // Unsupported type: read-only label.
                field = new Label(_fieldValues[name]?.ToString() ?? "");
                field.AddToClassList("col-readonly");
            }

            RegisterKeyboardNavigation(field, colIndex);
            return field;
        }

        // ── Keyboard navigation ────────────────────────────────────────────────────

        /// <summary>
        /// Attaches keyboard navigation callbacks to a field:
        ///   Enter → next field in this row (or fire <see cref="OnRequestNextRow"/> at last column)
        ///   Down  → fire <see cref="OnRequestNextRow"/> (move to same column in next row)
        ///   Escape → blur (dismiss focus)
        /// </summary>
        private void RegisterKeyboardNavigation(VisualElement field, int colIndex)
        {
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                bool isEnter  = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter;
                bool isDown   = evt.keyCode == KeyCode.DownArrow;
                bool isEscape = evt.keyCode == KeyCode.Escape;

                if (isEscape)
                {
                    field.Blur();
                    evt.StopPropagation();
                    return;
                }

                if (isDown || (isEnter && colIndex >= _columns.Count - 1))
                {
                    // Last column Enter or Down arrow → move to next row, same column.
                    OnRequestNextRow?.Invoke(colIndex);
                    evt.StopPropagation();
                    return;
                }

                if (isEnter)
                {
                    // Mid-row Enter → move to next field in this row.
                    FocusColumn(colIndex + 1);
                    evt.StopPropagation();
                }
            });
        }

        // ── Sizing ─────────────────────────────────────────────────────────────────

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

        // ── Entry reconstruction ───────────────────────────────────────────────────

        /// <summary>
        /// Constructs a new entry of the original type from the row's current field values.
        /// Uses <see cref="Activator.CreateInstance"/> so the concrete type is preserved.
        /// </summary>
        private IGameDataEntry MakeEntry()
        {
            var entry = (IGameDataEntry)Activator.CreateInstance(_entryType);
            foreach (var col in _columns)
            {
                if (_fieldValues.TryGetValue(col.Field.Name, out object val))
                    col.Field.SetValue(entry, val);
            }
            return entry;
        }

    }
}

