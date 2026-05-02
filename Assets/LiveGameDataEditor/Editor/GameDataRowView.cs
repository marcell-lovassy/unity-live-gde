using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    ///     Renders a single <see cref="IGameData" /> as an inline-editable table row.
    ///     Supported field types (via <see cref="GameDataColumnDefinition" />):
    ///     string → TextField
    ///     int    → IntegerField
    ///     float  → FloatField
    ///     bool   → Toggle
    ///     enum   → EnumField
    ///     UnityEngine.Object subtype → ObjectField (with thumbnail)
    ///     List&lt;T&gt; / T[] with [ListField] → TextField (items joined by separator)
    ///     Keyboard navigation:
    ///     Enter      → move focus to next field in the same row
    ///     Enter (last field) / Down arrow → fire <see cref="OnRequestNextRow" />
    ///     Escape     → blur the active field
    /// </summary>
    public class GameDataRowView : VisualElement
    {
        private readonly Dictionary<VisualElement, string> _baseTooltips = new();
        private readonly IReadOnlyList<GameDataColumnDefinition> _columns;
        private readonly Type _entryType;
        private readonly List<VisualElement> _fieldElements = new();
        private readonly Dictionary<string, VisualElement> _fieldElementsByName = new();

        // ── State ──────────────────────────────────────────────────────────────────

        private readonly Dictionary<string, object> _fieldValues = new();
        private readonly IGameData _source;
        private VisualElement _dragHandle;

        // ── Constructor ────────────────────────────────────────────────────────────

        public GameDataRowView(
            IGameData entry,
            IReadOnlyList<GameDataColumnDefinition> columns,
            bool isAlternateRow)
        {
            _columns = columns;
            _entryType = entry.GetType();
            _source = entry;

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
        // ── Events ─────────────────────────────────────────────────────────────────

        /// <summary>
        ///     Raised when any field is edited. Argument is a new entry instance with the
        ///     updated values. The caller records Undo before committing.
        /// </summary>
        public event Action<IGameData> OnEntryChanged;

        /// <summary>Raised on click. Bool = whether Ctrl/Shift/Cmd was held (multi-select).</summary>
        public event Action<bool> OnSelectionToggled;

        /// <summary>
        ///     Raised when the user presses Enter on the last column or the Down arrow on any column.
        ///     Argument = column index where navigation originated, so the next row can focus the same column.
        /// </summary>
        public event Action<int> OnRequestNextRow;

        /// <summary>
        ///     Raised when the drag handle receives a pointer-down (left button).
        ///     The table view subscribes to begin a drag operation for this row.
        /// </summary>
        public event Action<Vector2> OnDragHandlePointerDown;

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        ///     Shows or hides the drag handle. Call with <c>false</c> when sorting/filtering
        ///     is active — reordering is disabled in that state.
        /// </summary>
        public void SetDragEnabled(bool enabled)
        {
            if (_dragHandle == null) return;

            _dragHandle.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
            _dragHandle.tooltip = enabled
                ? "Drag to reorder"
                : "Remove sorting to enable drag-to-reorder";
        }

        public void SetSelected(bool selected)
        {
            EnableInClassList("table-row--selected", selected);
        }

        /// <summary>
        ///     Applies validation highlighting and a tooltip to the row.
        ///     Pass <c>null</c> or an empty list to clear any existing state.
        /// </summary>
        public void SetValidationState(List<ValidationResult> results)
        {
            var hasError = results != null && results.Any(r => r.Severity == ValidationSeverity.Error);
            var hasWarning = results != null && results.Any(r => r.Severity == ValidationSeverity.Warning);

            EnableInClassList("table-row--invalid", hasError);
            EnableInClassList("table-row--warning", hasWarning && !hasError);

            tooltip = results != null && results.Count > 0
                ? string.Join("\n", results.Select(r => $"[{r.Severity}] {r.Message}"))
                : string.Empty;

            ClearCellValidationState();

            if (results == null || results.Count == 0) return;

            foreach (var group in results
                         .Where(result => !string.IsNullOrEmpty(result.FieldName))
                         .GroupBy(result => result.FieldName))
            {
                if (!_fieldElementsByName.TryGetValue(group.Key, out var field)) continue;

                var cellResults = group.ToList();
                var cellHasError = cellResults.Any(r => r.Severity == ValidationSeverity.Error);
                var cellHasWarning = cellResults.Any(r => r.Severity == ValidationSeverity.Warning);

                field.EnableInClassList("table-cell--invalid", cellHasError);
                field.EnableInClassList("table-cell--warning", cellHasWarning && !cellHasError);
                field.tooltip = string.Join("\n", cellResults.Select(r => $"[{r.Severity}] {r.Message}"));
            }
        }

        /// <summary>Moves keyboard focus to the field at the given column index.</summary>
        public void FocusColumn(int colIndex)
        {
            if (colIndex < 0 || colIndex >= _fieldElements.Count) return;

            var el = _fieldElements[colIndex];
            el.schedule.Execute(() => el.Focus()).ExecuteLater(0);
        }

        /// <summary>
        ///     Applies a fixed width override to the field cell for the given column field name.
        ///     Called by <see cref="GameDataTableView" /> when the user drags a resize handle.
        /// </summary>
        public void SetColumnWidth(string fieldName, float width)
        {
            var el = this.Q(className: $"col-{fieldName.ToLower()}");
            if (el == null) return;

            el.style.width = width;
            el.style.minWidth = width;
            el.style.flexGrow = 0;
            el.style.flexShrink = 0;
        }

        /// <summary>
        ///     Resets a column back to default flex sizing (removes the width override).
        /// </summary>
        public void ResetColumnSizing(string fieldName, GameDataColumnDefinition col)
        {
            var el = this.Q(className: $"col-{fieldName.ToLower()}");
            if (el == null) return;

            ApplySizing(el, col);
        }

        // ── UI construction ────────────────────────────────────────────────────────

        private void BuildFields()
        {
            var gutter = new VisualElement();
            gutter.AddToClassList("col-gutter");

            // Drag handle — shown when drag-to-reorder is enabled (no active sort)
            _dragHandle = new Label("⠿");
            _dragHandle.AddToClassList("drag-handle");
            _dragHandle.tooltip = "Drag to reorder";

            _dragHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;

                OnDragHandlePointerDown?.Invoke(evt.position);
                // Do NOT stop propagation — let the ClickEvent bubble up so clicking
                // the handle also selects the row (drag activates only on movement).
            }, TrickleDown.TrickleDown);

            gutter.Add(_dragHandle);
            Add(gutter);

            for (var colIndex = 0; colIndex < _columns.Count; colIndex++)
            {
                var col = _columns[colIndex];
                var field = CreateField(col, colIndex);
                field.AddToClassList($"col-{col.Field.Name.ToLower()}");
                ApplySizing(field, col);
                Add(field);
                _fieldElements.Add(field);
                _fieldElementsByName[col.Field.Name] = field;
                _baseTooltips[field] = field.tooltip;
            }
        }

        private VisualElement CreateField(GameDataColumnDefinition col, int colIndex)
        {
            var name = col.Field.Name;
            var context = new TableFieldContext(
                _source,
                _columns,
                col,
                _fieldValues[name],
                value =>
                {
                    _fieldValues[name] = value;
                    OnEntryChanged?.Invoke(MakeEntry());
                },
                () => { });

            var field = TableFieldDrawerRegistry.CreateCell(context);

            RegisterKeyboardNavigation(field, colIndex);
            return field;
        }

        // ── Keyboard navigation ────────────────────────────────────────────────────

        /// <summary>
        ///     Attaches keyboard navigation callbacks to a field:
        ///     Enter → next field in this row (or fire <see cref="OnRequestNextRow" /> at last column)
        ///     Down  → fire <see cref="OnRequestNextRow" /> (move to same column in next row)
        ///     Escape → blur (dismiss focus)
        /// </summary>
        private void RegisterKeyboardNavigation(VisualElement field, int colIndex)
        {
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                var isEnter = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter;
                var isDown = evt.keyCode == KeyCode.DownArrow;
                var isEscape = evt.keyCode == KeyCode.Escape;

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
                el.style.flexGrow = 0;
                el.style.flexShrink = 0;
                el.style.width = col.MinWidth;
            }
            else
            {
                el.style.flexGrow = col.FlexGrow;
                el.style.flexShrink = 1;
            }
        }

        private void ClearCellValidationState()
        {
            foreach (var field in _fieldElements)
            {
                field.RemoveFromClassList("table-cell--invalid");
                field.RemoveFromClassList("table-cell--warning");
                field.tooltip = _baseTooltips.TryGetValue(field, out var baseTooltip)
                    ? baseTooltip
                    : string.Empty;
            }
        }

        // ── Entry reconstruction ───────────────────────────────────────────────────

        /// <summary>
        ///     Constructs a new entry of the original type from the row's current field values.
        ///     Uses <see cref="Activator.CreateInstance" /> so the concrete type is preserved.
        /// </summary>
        private IGameData MakeEntry()
        {
            var entry = (IGameData)Activator.CreateInstance(_entryType);
            foreach (var col in _columns)
                if (_fieldValues.TryGetValue(col.Field.Name, out var val))
                    col.Field.SetValue(entry, val);

            return entry;
        }
    }
}