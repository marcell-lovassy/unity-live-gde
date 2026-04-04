using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
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
        private readonly Action                      _onAddEntry;
        private readonly Action<List<int>>           _onRemoveEntries;
        private readonly Action<List<int>>           _onDuplicateEntries;

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

        // Column width overrides (set when user drags a resize handle), keyed by field name.
        // When present, the column uses a fixed width instead of flexGrow.
        private readonly Dictionary<string, float> _colWidthOverrides = new();

        // Header cells tracked for resize updates, keyed by field name
        private readonly Dictionary<string, VisualElement> _headerCells = new();

        // Stats footer
        private VisualElement    _statsRow;
        private readonly List<Label> _statsLabels  = new();
        private readonly Dictionary<string, Label> _statsCells = new();
        private Label            _statsCountLabel;
        private readonly List<int>   _visibleIndices = new();

        public GameDataTableView(
            Action<int, IGameDataEntry> onEntryChanged,
            Action                      onAddEntry,
            Action<List<int>>           onRemoveEntries,
            Action<List<int>>           onDuplicateEntries = null)
        {
            _onEntryChanged     = onEntryChanged;
            _onAddEntry         = onAddEntry;
            _onRemoveEntries    = onRemoveEntries;
            _onDuplicateEntries = onDuplicateEntries;

            AddToClassList("table-view");
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            // Placeholder header — rebuilt in Populate() once we know the entry type
            _headerRow = new VisualElement();
            _headerRow.AddToClassList("table-header");
            Add(_headerRow);

            BuildScrollView();
            BuildStatsRow();
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
            _headerCells.Clear();
            _statsLabels.Clear();
            _statsCells.Clear();

            if (container == null) { RebuildStatsRow(); return; }

            // Rebuild columns from the container's entry type
            _columns = GameDataColumnDefinition.FromType(container.EntryType);
            RebuildHeader();
            RebuildStatsRow();

            var entries = container.GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                int capturedIndex = i;
                var entry = (IGameDataEntry)entries[capturedIndex];
                var row = new GameDataRowView(entry, _columns, i % 2 == 1);

                row.OnEntryChanged     += (updated) => _onEntryChanged?.Invoke(capturedIndex, updated);
                row.OnSelectionToggled += (isMulti) => HandleRowSelection(capturedIndex, isMulti);
                row.OnRequestNextRow   += colIndex  => NavigateToNextRow(row, colIndex);

                // Apply any existing column width overrides to this new row.
                foreach (var kvp in _colWidthOverrides)
                    row.SetColumnWidth(kvp.Key, kvp.Value);

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

        /// <summary>
        /// Refreshes the stats footer without rebuilding rows.
        /// Call this after an inline field edit so sum/avg stays current.
        /// </summary>
        public void UpdateStats() => UpdateStatsRow();

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
                _headerCells[col.Field.Name] = cell;

                var label = new Label(col.Label);
                label.AddToClassList("col-header");

                var indicator = new Label(string.Empty);
                indicator.AddToClassList("sort-indicator");
                _sortIndicators[col.Field.Name] = indicator;

                cell.Add(label);
                cell.Add(indicator);

                string fieldName = col.Field.Name;
                // Sort on click of the header cell (resize handle stops ClickEvent propagation)
                cell.RegisterCallback<ClickEvent>(_ => OnHeaderClicked(fieldName));

                // Resize handle — thin strip at the right edge
                cell.Add(BuildResizeHandle(col));

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

            var addBtn = new Button(() => _onAddEntry?.Invoke()) { text = "+ Add Row" };
            addBtn.AddToClassList("footer-button");

            var duplicateBtn = new Button(DuplicateSelected) { text = "⧉ Duplicate" };
            duplicateBtn.AddToClassList("footer-button");

            var removeBtn = new Button(RemoveSelected) { text = "− Remove Selected" };
            removeBtn.AddToClassList("footer-button");
            removeBtn.AddToClassList("footer-button--danger");

            footer.Add(addBtn);
            footer.Add(duplicateBtn);
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

            // Step 4: update visible index cache and stats
            _visibleIndices.Clear();
            _visibleIndices.AddRange(visible);
            UpdateStatsRow();
        }

        // ── Stats row ──────────────────────────────────────────────────────────────

        /// <summary>Creates the stats row VisualElement (called once in constructor).</summary>
        private void BuildStatsRow()
        {
            _statsRow = new VisualElement();
            _statsRow.AddToClassList("stats-row");
            Add(_statsRow); // inserted after _scrollView, before footer
        }

        /// <summary>
        /// Rebuilds stats row labels to match the current column set.
        /// Called whenever the container type changes (i.e. in Populate()).
        /// </summary>
        private void RebuildStatsRow()
        {
            _statsRow.Clear();
            _statsLabels.Clear();
            _statsCells.Clear();

            // Gutter to align with data rows
            var gutter = new VisualElement();
            gutter.AddToClassList("col-gutter");
            _statsRow.Add(gutter);

            // Row count on the left of the gutter
            _statsCountLabel = new Label();
            _statsCountLabel.AddToClassList("stats-count-label");
            _statsRow.Add(_statsCountLabel);

            if (_columns == null || _columns.Count == 0) return;

            foreach (var col in _columns)
            {
                var label = new Label();
                label.AddToClassList("stats-cell");
                label.AddToClassList($"col-{col.Field.Name.ToLower()}");
                ApplySizing(label, col);
                // Apply any existing override
                if (_colWidthOverrides.TryGetValue(col.Field.Name, out float w))
                    SetFixedWidth(label, w);
                _statsRow.Add(label);
                _statsLabels.Add(label);
                _statsCells[col.Field.Name] = label;
            }
        }

        /// <summary>
        /// Recomputes sum and average for numeric columns over currently visible rows.
        /// </summary>
        private void UpdateStatsRow()
        {
            if (_statsCountLabel != null)
                _statsCountLabel.text = $"{_visibleIndices.Count} row{(_visibleIndices.Count == 1 ? "" : "s")}";

            if (_container == null || _statsLabels.Count == 0) return;

            var entries = _container.GetEntries();

            for (int ci = 0; ci < _columns.Count && ci < _statsLabels.Count; ci++)
            {
                var col   = _columns[ci];
                var label = _statsLabels[ci];

                if (!col.IsInt && !col.IsFloat) { label.text = string.Empty; continue; }

                double sum = 0;
                int    count = 0;
                foreach (int idx in _visibleIndices)
                {
                    var raw = col.Field.GetValue(entries[idx]);
                    double val = col.IsInt ? (int)raw : (double)(float)raw;
                    sum += val;
                    count++;
                }

                double avg = count > 0 ? sum / count : 0;
                label.text = col.IsInt
                    ? $"Σ {(long)sum}  ∅ {avg:F1}"
                    : $"Σ {sum:F1}  ∅ {avg:F1}";
            }
        }

        private void OnHeaderClicked(string fieldName)        {
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

        private void DuplicateSelected()
        {
            if (_selectedIndices.Count == 0) return;
            _onDuplicateEntries?.Invoke(new List<int>(_selectedIndices));
        }

        // ── Keyboard navigation ────────────────────────────────────────────────────

        /// <summary>
        /// Called by a row's <see cref="GameDataRowView.OnRequestNextRow"/> event.
        /// Finds the next visible row and focuses the same column.
        /// </summary>
        private void NavigateToNextRow(GameDataRowView sourceRow, int colIndex)
        {
            int visibleIndex = _scrollView.IndexOf(sourceRow);
            if (visibleIndex < 0 || visibleIndex >= _scrollView.childCount - 1) return;
            (_scrollView[visibleIndex + 1] as GameDataRowView)?.FocusColumn(colIndex);
        }

        // ── Column resizing ────────────────────────────────────────────────────────

        private VisualElement BuildResizeHandle(GameDataColumnDefinition col)
        {
            var handle = new VisualElement();
            handle.AddToClassList("col-resize-handle");

            // Prevent clicks on the handle from bubbling to the header cell (which sorts)
            handle.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            float dragStartX   = 0;
            float dragStartW   = 0;
            bool  dragging     = false;
            string fieldName   = col.Field.Name;

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;

                // Double-click → reset column to default flex sizing
                if (evt.clickCount == 2)
                {
                    dragging = false;
                    handle.ReleasePointer(evt.pointerId);
                    _colWidthOverrides.Remove(fieldName);
                    if (_headerCells.TryGetValue(fieldName, out var hc))
                        ApplySizing(hc, col);
                    if (_statsCells.TryGetValue(fieldName, out var sc))
                        ApplySizing(sc, col);
                    foreach (var row in _rows)
                        row.ResetColumnSizing(fieldName, col);
                    evt.StopPropagation();
                    return;
                }

                dragging   = true;
                dragStartX = evt.position.x;
                dragStartW = _headerCells.TryGetValue(fieldName, out var headerCell)
                    ? headerCell.resolvedStyle.width
                    : col.MinWidth;
                handle.CapturePointer(evt.pointerId);
                evt.StopPropagation(); // prevent column sort
            });

            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging) return;
                float newWidth = Mathf.Max(col.MinWidth, dragStartW + (evt.position.x - dragStartX));
                ApplyWidthOverride(fieldName, newWidth);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!dragging) return;
                dragging = false;
                handle.ReleasePointer(evt.pointerId);
                evt.StopPropagation();
            });

            return handle;
        }

        /// <summary>
        /// Stores a fixed-width override for <paramref name="fieldName"/> and applies it
        /// to the header cell and all row cells immediately.
        /// </summary>
        private void ApplyWidthOverride(string fieldName, float width)
        {
            _colWidthOverrides[fieldName] = width;

            if (_headerCells.TryGetValue(fieldName, out var headerCell))
                SetFixedWidth(headerCell, width);

            if (_statsCells.TryGetValue(fieldName, out var statsCell))
                SetFixedWidth(statsCell, width);

            foreach (var row in _rows)
                row.SetColumnWidth(fieldName, width);
        }

        private static void SetFixedWidth(VisualElement el, float width)
        {
            el.style.width      = width;
            el.style.minWidth   = width;
            el.style.flexGrow   = 0;
            el.style.flexShrink = 0;
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
