using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Renders a scrollable table of <see cref="GameDataEntry"/> rows.
    ///
    /// Responsibilities:
    ///   - Dynamic column headers (driven by <see cref="GameDataColumnDefinition"/>)
    ///   - Inline row editing
    ///   - Search / filter (show/hide rows without rebuild)
    ///   - Column sorting (click header to toggle ascending/descending)
    ///   - Row validation highlighting
    ///   - Multi-row selection with Ctrl/Shift/Cmd support
    /// </summary>
    public class GameDataTableView : VisualElement
    {
        // ── Callbacks wired by the EditorWindow ────────────────────────────────────
        private readonly Action<int, GameDataEntry> _onEntryChanged;  // (dataIndex, newEntry)
        private readonly Action _onAddEntry;
        private readonly Action<List<int>> _onRemoveEntries;          // (dataIndices)

        /// <summary>Fired whenever the selection changes. Argument = current selected data indices.</summary>
        public event Action<List<int>> OnSelectionChanged;

        // ── State ──────────────────────────────────────────────────────────────────
        private GameDataContainer _container;
        private readonly List<GameDataColumnDefinition> _columns;
        private ScrollView _scrollView;
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
            Action<int, GameDataEntry> onEntryChanged,
            Action onAddEntry,
            Action<List<int>> onRemoveEntries)
        {
            _onEntryChanged  = onEntryChanged;
            _onAddEntry      = onAddEntry;
            _onRemoveEntries = onRemoveEntries;
            _columns         = GameDataColumnDefinition.FromType<GameDataEntry>();

            AddToClassList("table-view");
            style.flexGrow = 1;

            BuildHeader();
            BuildScrollView();
            BuildFooter();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>Full rebuild of all row VisualElements from the container. Clears selection.</summary>
        public void Populate(GameDataContainer container)
        {
            _container = container;
            _selectedIndices.Clear();
            _rows.Clear();
            _scrollView.Clear();

            if (container == null) return;

            for (int i = 0; i < container.Entries.Count; i++)
            {
                int capturedIndex = i;
                var row = new GameDataRowView(container.Entries[i], _columns, i % 2 == 1);

                row.OnEntryChanged    += (updated) => _onEntryChanged?.Invoke(capturedIndex, updated);
                row.OnSelectionToggled += (isMulti) => HandleRowSelection(capturedIndex, isMulti);

                _rows.Add(row);
            }

            ApplyFilterAndSort();
        }

        /// <summary>
        /// Updates filter criteria and refreshes the display.
        /// Row VisualElements are reused — not rebuilt.
        /// </summary>
        public void SetFilter(string searchText, bool enabledOnly)
        {
            _searchText  = searchText ?? string.Empty;
            _enabledOnly = enabledOnly;
            ApplyFilterAndSort();
        }

        /// <summary>
        /// Applies per-row validation results. Rows absent from the dictionary are cleared.
        /// </summary>
        public void ApplyValidation(Dictionary<int, List<ValidationResult>> results)
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                results.TryGetValue(i, out var rowResults);
                _rows[i].SetValidationState(rowResults);
            }
        }

        // ── Header ─────────────────────────────────────────────────────────────────

        private void BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("table-header");

            // Gutter placeholder aligns with row gutters
            var gutter = new VisualElement();
            gutter.AddToClassList("col-gutter");
            header.Add(gutter);

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

                string fieldName = col.Field.Name; // capture for closure
                cell.RegisterCallback<ClickEvent>(_ => OnHeaderClicked(fieldName));

                header.Add(cell);
            }

            Add(header);
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

        /// <summary>
        /// Builds a filtered + sorted index list and repopulates the ScrollView
        /// by reordering/showing/hiding existing row VisualElements (no rebuild).
        /// </summary>
        private void ApplyFilterAndSort()
        {
            if (_container == null) return;

            // Step 1: filter
            var visible = new List<int>(_rows.Count);
            for (int i = 0; i < _container.Entries.Count; i++)
            {
                var entry = _container.Entries[i];
                if (_enabledOnly && !entry.Enabled) continue;
                if (!string.IsNullOrEmpty(_searchText) &&
                    (entry.Id == null ||
                     entry.Id.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                visible.Add(i);
            }

            // Step 2: sort
            if (!string.IsNullOrEmpty(_sortField))
            {
                var fieldInfo = typeof(GameDataEntry).GetField(_sortField);
                if (fieldInfo != null)
                {
                    visible.Sort((a, b) =>
                    {
                        var va = fieldInfo.GetValue(_container.Entries[a]) as IComparable;
                        var vb = fieldInfo.GetValue(_container.Entries[b]) as IComparable;
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
