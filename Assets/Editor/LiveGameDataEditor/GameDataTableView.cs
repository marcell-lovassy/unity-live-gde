using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Renders a scrollable table of <see cref="IGameDataEntry"/> rows for any
    /// <see cref="IGameDataContainer"/>. Column definitions are derived from the
    /// container's entry type via <see cref="GameDataColumnDefinition.FromType"/>.
    ///
    /// Responsibilities:
    ///   - Dynamic column headers (reflection-based)
    ///   - Inline row editing
    ///   - Search / filter (show/hide rows without rebuild)
    ///   - Column sorting (click header to toggle ascending/descending)
    ///   - Row validation highlighting
    ///   - Multi-row selection with Ctrl/Shift/Cmd support
    /// </summary>
    public class GameDataTableView : VisualElement
    {
        // ── Callbacks wired by the EditorWindow ────────────────────────────────────

        /// <summary>Called when a row field is edited. Args: (rowIndex, newEntryInstance).</summary>
        private readonly Action<int, IGameDataEntry> _onEntryChanged;
        private readonly Action _onAddEntry;
        private readonly Action<List<int>> _onRemoveEntries;

        /// <summary>Fired whenever the selection changes. Argument = selected data indices.</summary>
        public event Action<List<int>> OnSelectionChanged;

        // ── State ──────────────────────────────────────────────────────────────────
        private IGameDataContainer _container;
        private List<GameDataColumnDefinition> _columns = new();
        private ScrollView _scrollView;
        private VisualElement _headerRow;
        private readonly List<GameDataRowView> _rows = new();
        private readonly List<int> _selectedIndices = new();

        // Filter / sort
        private string _searchText  = string.Empty;
        private bool   _enabledOnly = false;
        private string _sortField   = null;
        private bool   _sortAsc     = true;

        // Header sort-indicator labels, keyed by field name
        private readonly Dictionary<string, Label> _sortIndicators = new();

        public GameDataTableView(
            Action<int, IGameDataEntry> onEntryChanged,
            Action onAddEntry,
            Action<List<int>> onRemoveEntries)
        {
            _onEntryChanged  = onEntryChanged;
            _onAddEntry      = onAddEntry;
            _onRemoveEntries = onRemoveEntries;

            AddToClassList("table-view");
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            // Placeholder header — rebuilt in Populate() once we know the entry type
            _headerRow = new VisualElement();
            _headerRow.AddToClassList("table-header");
            Add(_headerRow);

            BuildScrollView();
            BuildFooter();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Full rebuild of column headers and row VisualElements from <paramref name="container"/>.
        /// Clears selection. Header is rebuilt when container type changes.
        /// </summary>
        public void Populate(IGameDataContainer container)
        {
            _container = container;
            _selectedIndices.Clear();
            _rows.Clear();
            _scrollView.Clear();
            _sortIndicators.Clear();

            if (container == null) return;

            // Rebuild columns from the container's entry type
            _columns = GameDataColumnDefinition.FromType(container.EntryType);
            RebuildHeader();

            var entries = container.GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                int capturedIndex = i;
                var entry = (IGameDataEntry)entries[capturedIndex];
                var row = new GameDataRowView(entry, _columns, i % 2 == 1);

                row.OnEntryChanged    += (updated) => _onEntryChanged?.Invoke(capturedIndex, updated);
                row.OnSelectionToggled += (isMulti) => HandleRowSelection(capturedIndex, isMulti);

                _rows.Add(row);
            }

            ApplyFilterAndSort();
        }

        /// <summary>
        /// Updates filter criteria and refreshes the visible row order.
        /// Row VisualElements are reused — not rebuilt.
        /// </summary>
        public void SetFilter(string searchText, bool enabledOnly)
        {
            _searchText  = searchText ?? string.Empty;
            _enabledOnly = enabledOnly;
            ApplyFilterAndSort();
        }

        /// <summary>Applies per-row validation results. Rows absent from the dict are cleared.</summary>
        public void ApplyValidation(Dictionary<int, List<ValidationResult>> results)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                results.TryGetValue(i, out var rowResults);
                _rows[i].SetValidationState(rowResults);
            }
        }

        // ── Header ─────────────────────────────────────────────────────────────────

        private void RebuildHeader()
        {
            _headerRow.Clear();

            var gutter = new VisualElement();
            gutter.AddToClassList("col-gutter");
            _headerRow.Add(gutter);

            foreach (var col in _columns)
            {
                var cell = new VisualElement();
                cell.AddToClassList("col-header-cell");
                cell.AddToClassList($"col-{col.Field.Name.ToLower()}");
                ApplySizing(cell, col);

                var label = new Label(col.Label);
                label.AddToClassList("col-header");

                var indicator = new Label(string.Empty);
                indicator.AddToClassList("sort-indicator");
                _sortIndicators[col.Field.Name] = indicator;

                cell.Add(label);
                cell.Add(indicator);

                string fieldName = col.Field.Name;
                cell.RegisterCallback<ClickEvent>(_ => OnHeaderClicked(fieldName));

                _headerRow.Add(cell);
            }
        }

        // ── ScrollView ─────────────────────────────────────────────────────────────

        private void BuildScrollView()
        {
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.AddToClassList("table-scroll");
            _scrollView.style.flexGrow = 1;
            Add(_scrollView);
        }

        // ── Footer ─────────────────────────────────────────────────────────────────

        private void BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("table-footer");

            var addBtn    = new Button(() => _onAddEntry?.Invoke()) { text = "+ Add Row" };
            addBtn.AddToClassList("footer-button");

            var removeBtn = new Button(RemoveSelected) { text = "− Remove Selected" };
            removeBtn.AddToClassList("footer-button");
            removeBtn.AddToClassList("footer-button--danger");

            footer.Add(addBtn);
            footer.Add(removeBtn);
            Add(footer);
        }

        // ── Filter / Sort ──────────────────────────────────────────────────────────

        private void ApplyFilterAndSort()
        {
            if (_container == null) return;

            var entries = _container.GetEntries();

            // Cached reflection for optional fields used in filter (e.g. "Enabled")
            FieldInfo enabledField = _container.EntryType.GetField("Enabled");

            // Step 1: filter
            var visible = new List<int>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = (IGameDataEntry)entries[i];

                // Enabled-only filter (uses reflection; skipped for types without an Enabled field)
                if (_enabledOnly && enabledField != null)
                {
                    var enabledVal = enabledField.GetValue(entry);
                    if (enabledVal is bool b && !b) continue;
                }

                if (!string.IsNullOrEmpty(_searchText) &&
                    (entry.Id == null ||
                     entry.Id.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;

                visible.Add(i);
            }

            // Step 2: sort
            if (!string.IsNullOrEmpty(_sortField))
            {
                var fieldInfo = _container.EntryType.GetField(_sortField);
                if (fieldInfo != null)
                {
                    visible.Sort((a, b) =>
                    {
                        var va = fieldInfo.GetValue(entries[a]) as IComparable;
                        var vb = fieldInfo.GetValue(entries[b]) as IComparable;
                        int cmp = va?.CompareTo(vb) ?? 0;
                        return _sortAsc ? cmp : -cmp;
                    });
                }
            }

            // Step 3: repopulate ScrollView with the ordered, filtered rows
            _scrollView.Clear();
            foreach (int i in visible)
                _scrollView.Add(_rows[i]);
        }

        private void OnHeaderClicked(string fieldName)
        {
            if (_sortField == fieldName)
                _sortAsc = !_sortAsc;
            else
            {
                _sortField = fieldName;
                _sortAsc   = true;
            }

            RefreshSortIndicators();
            ApplyFilterAndSort();
        }

        private void RefreshSortIndicators()
        {
            foreach (var col in _columns)
            {
                if (!_sortIndicators.TryGetValue(col.Field.Name, out var indicator)) continue;

                if (col.Field.Name == _sortField)
                {
                    indicator.text = _sortAsc ? " ▲" : " ▼";
                    indicator.AddToClassList("sort-indicator--active");
                }
                else
                {
                    indicator.text = string.Empty;
                    indicator.RemoveFromClassList("sort-indicator--active");
                }
            }
        }

        // ── Selection ──────────────────────────────────────────────────────────────

        private void HandleRowSelection(int index, bool isMultiSelect)
        {
            if (!isMultiSelect)
            {
                foreach (int i in _selectedIndices)
                    if (i < _rows.Count) _rows[i].SetSelected(false);
                _selectedIndices.Clear();
            }

            if (_selectedIndices.Contains(index))
            {
                _selectedIndices.Remove(index);
                _rows[index].SetSelected(false);
            }
            else
            {
                _selectedIndices.Add(index);
                _rows[index].SetSelected(true);
            }

            OnSelectionChanged?.Invoke(new List<int>(_selectedIndices));
        }

        private void RemoveSelected()
        {
            if (_selectedIndices.Count == 0) return;
            _onRemoveEntries?.Invoke(new List<int>(_selectedIndices));
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

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
    }
}
