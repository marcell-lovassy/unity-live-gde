using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    ///     Renders a scrollable table of <see cref="IGameData" /> rows for any
    ///     <see cref="IGameDataContainer" />. Column definitions are derived from the
    ///     container's entry type via <see cref="GameDataColumnDefinition.FromType" />.
    ///     Responsibilities:
    ///     - Dynamic column headers (reflection-based)
    ///     - Inline row editing
    ///     - Search / filter (show/hide rows without rebuild)
    ///     - Column sorting (click header to toggle ascending/descending)
    ///     - Row validation highlighting
    ///     - Multi-row selection with Ctrl/Shift/Cmd support
    /// </summary>
    public class GameDataTableView : VisualElement
    {
        private const float DragThreshold = 5f; // pixels before drag activates
        private const float GutterWidth = 26f;
        private const int MinRowsPerBatch = 4;
        private const int MaxRowsPerBatch = 40;
        private const long MaxBatchMilliseconds = 8;
        private const int SearchDebounceMilliseconds = 150;
        private const int MinViewRowsPerBatch = 12;
        private const int MaxViewRowsPerBatch = 120;
        private const int MaxCachedTableCount = 4;

        // Column width overrides (set when user drags a resize handle), keyed by field name.
        // When present, the column uses a fixed width instead of flexGrow.
        private readonly Dictionary<string, float> _colWidthOverrides = new();

        // Shared computed column widths, keyed by field name. Header, rows, and stats
        // all consume this map so layout does not depend on resolved header timing.
        private readonly Dictionary<string, float> _computedColumnWidths = new();
        private readonly VisualElement _dropIndicator;

        // Header cells tracked for resize updates, keyed by field name
        private readonly VisualElement _headerClip;
        private readonly Dictionary<string, VisualElement> _headerCells = new();
        private readonly VisualElement _headerRow;
        private readonly VisualElement _loadingPane;
        private readonly Label _loadingLabel;
        private readonly Action _onAddEntry;

        private readonly Action<List<int>> _onDuplicateEntries;
        // ── Callbacks wired by the EditorWindow ────────────────────────────────────

        /// <summary>Called when a row field is edited. Args: (rowIndex, newEntryInstance).</summary>
        private readonly Action<int, IGameData> _onEntryChanged;

        /// <summary>
        ///     Called when the user drops a row at a new position.
        ///     Args: (fromDataIndex, insertBeforeDataIndex).
        /// </summary>
        private readonly Action<int, int> _onMoveEntry;

        private readonly Action<List<int>> _onRemoveEntries;
        private readonly Dictionary<int, RowCacheEntry> _rowCache = new();
        private readonly List<int> _rowCacheOrder = new();
        private readonly Dictionary<GameDataRowView, int> _rowIndices = new();
        private readonly List<GameDataRowView> _rows = new();
        private readonly List<int> _selectedIndices = new();

        // Header sort-indicator labels, keyed by field name
        private readonly Dictionary<string, Label> _sortIndicators = new();
        private readonly Dictionary<string, Label> _statsCells = new();
        private readonly List<Label> _statsLabels = new();
        private readonly List<int> _visibleIndices = new();
        private List<GameDataColumnDefinition> _columns = new();

        // ── State ──────────────────────────────────────────────────────────────────
        private IGameDataContainer _container;
        private int _draggingDataIndex = -1;
        private bool _dragPending; // pointer down, awaiting threshold move
        private Vector2 _dragStartPosition; // world position at pointer-down
        private int _dropTargetVisibleIdx = -1; // insert-before position in visible rows
        private bool _enabledOnly;

        // Drag-to-reorder state
        private bool _isDragging;
        private bool _isApplyingView;
        private bool _isPopulating;
        private IList _pendingEntries;
        private int _pendingEntryIndex;
        private int _pendingVisibleIndex;
        private List<int> _pendingVisibleOrder;
        private int _populateGeneration;
        private int _viewGeneration;
        private VisualElement _footer;
        private ScrollView _scrollView;

        // Filter / sort
        private string _searchText = string.Empty;
        private bool _sortAsc = true;
        private string _sortField;

        // Stats footer
        private VisualElement _statsClip;
        private Label _statsCountLabel;
        private VisualElement _statsRow;

        private sealed class RowCacheEntry
        {
            public Type EntryType;
            public int RowCount;
            public List<GameDataRowView> Rows;
        }

        public GameDataTableView(
            Action<int, IGameData> onEntryChanged,
            Action onAddEntry,
            Action<List<int>> onRemoveEntries,
            Action<List<int>> onDuplicateEntries = null,
            Action<int, int> onMoveEntry = null)
        {
            _onEntryChanged = onEntryChanged;
            _onAddEntry = onAddEntry;
            _onRemoveEntries = onRemoveEntries;
            _onDuplicateEntries = onDuplicateEntries;
            _onMoveEntry = onMoveEntry;

            AddToClassList("table-view");
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;

            // Header clip container — overflow:hidden so translated header is clipped correctly
            _headerClip = new VisualElement();
            _headerClip.AddToClassList("table-header-clip");
            Add(_headerClip);

            // Placeholder header — rebuilt in Populate() once we know the entry type
            _headerRow = new VisualElement();
            _headerRow.AddToClassList("table-header");
            _headerClip.Add(_headerRow);

            _loadingPane = new VisualElement();
            _loadingPane.AddToClassList("table-loading-pane");
            _loadingPane.style.display = DisplayStyle.None;
            _loadingLabel = new Label();
            _loadingLabel.AddToClassList("table-loading-message");
            _loadingPane.Add(_loadingLabel);
            Add(_loadingPane);

            BuildScrollView();

            // Sync header and stats horizontal position with the ScrollView's horizontal offset
            _scrollView.horizontalScroller.valueChanged += offset => { SyncHorizontalOffset(offset); };

            RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (!Mathf.Approximately(evt.oldRect.width, evt.newRect.width)) ScheduleColumnLayout();
            });

            BuildStatsRow();
            BuildFooter();

            // Drop indicator — a thin horizontal rule inserted between rows during drag
            _dropIndicator = new VisualElement();
            _dropIndicator.AddToClassList("drop-indicator");

            // Track pointer events on this element (table view) to handle drag moves and releases
            RegisterCallback<PointerMoveEvent>(OnDragPointerMove, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(OnDragPointerUp, TrickleDown.TrickleDown);
        }

        /// <summary>
        ///     Drag is only allowed when no sort and no filter are active.
        ///     With an active sort the display order differs from the data order,
        ///     so reordering would produce unexpected results.
        ///     With active filtering there are gaps in the visible index sequence.
        /// </summary>
        private bool IsDragEnabled =>
            string.IsNullOrEmpty(_sortField) &&
            string.IsNullOrEmpty(_searchText) &&
            !_enabledOnly;

        /// <summary>Fired whenever the selection changes. Argument = selected data indices.</summary>
        public event Action<List<int>> OnSelectionChanged;
        public event Action<int, int> OnPopulateProgress;
        public event Action OnPopulateComplete;

        public bool IsLoading => _isPopulating;

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        ///     Full rebuild of column headers and row VisualElements from <paramref name="container" />.
        ///     Clears selection. Header is rebuilt when container type changes.
        /// </summary>
        public void Populate(IGameDataContainer container)
        {
            var generation = ++_populateGeneration;
            _isPopulating = false;
            _isApplyingView = false;
            _pendingEntries = null;
            _pendingEntryIndex = 0;
            _pendingVisibleOrder = null;
            _pendingVisibleIndex = 0;

            _container = container;
            _selectedIndices.Clear();

            if (container == null)
            {
                HideLoading();
                ClearTableState();
                _columns = new List<GameDataColumnDefinition>();
                RebuildStatsRow();
                _headerRow.style.width = 0;
                _scrollView.contentContainer.style.minWidth = 0;
                if (_statsRow != null) _statsRow.style.width = 0;
                return;
            }

            _pendingEntries = container.GetEntries();
            _isPopulating = true;
            ShowLoading(0, _pendingEntries.Count);
            schedule.Execute(() => BeginPopulate(container, generation)).ExecuteLater(0);
        }

        private void BeginPopulate(IGameDataContainer container, int generation)
        {
            if (generation != _populateGeneration || _container != container || _pendingEntries == null) return;

            ClearTableState();

            // Rebuild columns from the container's entry type
            _columns = GameDataColumnDefinition.FromType(container.EntryType);
            RebuildHeader();
            RebuildStatsRow();

            if (TryUseCachedRows(container, _pendingEntries))
            {
                _isPopulating = false;
                HideLoading();
                ApplyFilterAndSort();
                OnPopulateProgress?.Invoke(_rows.Count, _rows.Count);
                OnPopulateComplete?.Invoke();
                return;
            }

            _pendingEntryIndex = 0;
            schedule.Execute(() => PopulateBatch(generation)).ExecuteLater(0);
        }

        private void ClearTableState()
        {
            _rows.Clear();
            _rowIndices.Clear();
            _scrollView.Clear();
            _sortIndicators.Clear();
            _headerCells.Clear();
            _statsLabels.Clear();
            _statsCells.Clear();
            _computedColumnWidths.Clear();
            _visibleIndices.Clear();
        }

        /// <summary>
        ///     Updates filter criteria and refreshes the visible row order.
        ///     Row VisualElements are reused — not rebuilt.
        /// </summary>
        public void SetFilter(string searchText, bool enabledOnly, bool applyImmediately = true)
        {
            _searchText = searchText ?? string.Empty;
            _enabledOnly = enabledOnly;

            if (!applyImmediately || _isPopulating) return;

            ScheduleApplyFilterAndSort(SearchDebounceMilliseconds);
        }

        public void CancelPopulation()
        {
            _populateGeneration++;
            _viewGeneration++;
            _isPopulating = false;
            _isApplyingView = false;
            _pendingEntries = null;
            _pendingEntryIndex = 0;
            _pendingVisibleOrder = null;
            _pendingVisibleIndex = 0;
            HideLoading();
        }

        public void InvalidateCachedRows(IGameDataContainer container)
        {
            if (container == null)
            {
                _rowCache.Clear();
                _rowCacheOrder.Clear();
                return;
            }

            var key = GetContainerCacheKey(container);
            _rowCache.Remove(key);
            _rowCacheOrder.Remove(key);
        }

        public bool TryAppendLatestEntry()
        {
            if (_container == null || _isPopulating || _isApplyingView) return false;

            var entries = _container.GetEntries();
            if (entries.Count != _rows.Count + 1) return false;

            var index = entries.Count - 1;
            var row = CreateRow(index, (IGameData)entries[index]);
            _rows.Add(row);
            ApplyCurrentColumnWidths(row);

            if (CanAppendRowsDuringPopulate())
            {
                _scrollView.Add(row);
                _visibleIndices.Add(index);
                UpdateStatsRow();
                RefreshDragHandles();
            }
            else
            {
                ApplyFilterAndSort();
            }

            StoreCurrentRowsInCache();
            ScrollToRow(row);
            return true;
        }

        public bool TryRemoveEntries(List<int> indices)
        {
            if (_container == null || _isPopulating || _isApplyingView || indices == null || indices.Count == 0)
                return false;

            var unique = new List<int>();
            foreach (var index in indices)
            {
                if (index < 0 || index >= _rows.Count || unique.Contains(index)) continue;
                unique.Add(index);
            }

            if (unique.Count == 0) return false;

            var entries = _container.GetEntries();
            if (entries.Count != _rows.Count - unique.Count) return false;

            unique.Sort();
            var firstChangedIndex = unique[0];

            for (var i = unique.Count - 1; i >= 0; i--)
            {
                var rowIndex = unique[i];
                var row = _rows[rowIndex];
                row.RemoveFromHierarchy();
                _rowIndices.Remove(row);
                _rows.RemoveAt(rowIndex);
            }

            _selectedIndices.Clear();
            RefreshRowIndices(firstChangedIndex);

            if (CanAppendRowsDuringPopulate())
            {
                _visibleIndices.Clear();
                for (var i = 0; i < _rows.Count; i++) _visibleIndices.Add(i);
                UpdateStatsRow();
                RefreshDragHandles();
                ScheduleColumnLayout();
            }
            else
            {
                ApplyFilterAndSort();
            }

            StoreCurrentRowsInCache();
            OnSelectionChanged?.Invoke(new List<int>());
            return true;
        }

        public bool TryDuplicateEntries(List<int> indices)
        {
            if (_container == null || _isPopulating || _isApplyingView || indices == null || indices.Count == 0)
                return false;

            var unique = GetValidUniqueIndices(indices);
            if (unique.Count == 0) return false;

            var entries = _container.GetEntries();
            if (entries.Count != _rows.Count + unique.Count) return false;

            unique.Sort();
            var firstChangedIndex = unique[0] + 1;
            GameDataRowView lastCloneRow = null;
            var insertedBefore = 0;

            foreach (var originalIndex in unique)
            {
                var cloneIndex = originalIndex + 1 + insertedBefore;
                if (cloneIndex < 0 || cloneIndex >= entries.Count) return false;

                var row = CreateRow(cloneIndex, (IGameData)entries[cloneIndex]);
                _rows.Insert(cloneIndex, row);
                ApplyCurrentColumnWidths(row);
                lastCloneRow = row;

                if (CanAppendRowsDuringPopulate())
                {
                    if (cloneIndex >= _scrollView.childCount)
                        _scrollView.Add(row);
                    else
                        _scrollView.Insert(cloneIndex, row);
                }

                insertedBefore++;
            }

            _selectedIndices.Clear();
            RefreshRowIndices(firstChangedIndex);

            if (CanAppendRowsDuringPopulate())
            {
                RebuildSequentialVisibleIndices();
                UpdateStatsRow();
                RefreshDragHandles();
            }
            else
            {
                ApplyFilterAndSort();
            }

            StoreCurrentRowsInCache();
            OnSelectionChanged?.Invoke(new List<int>());
            ScrollToRow(lastCloneRow);
            return true;
        }

        public bool TryMoveEntry(int fromIndex, int insertBefore)
        {
            if (_container == null || _isPopulating || _isApplyingView) return false;
            if (fromIndex < 0 || fromIndex >= _rows.Count) return false;

            var entries = _container.GetEntries();
            if (entries.Count != _rows.Count) return false;

            insertBefore = Mathf.Clamp(insertBefore, 0, _rows.Count);
            if (insertBefore == fromIndex || insertBefore == fromIndex + 1) return true;

            var finalIndex = insertBefore > fromIndex ? insertBefore - 1 : insertBefore;
            finalIndex = Mathf.Clamp(finalIndex, 0, _rows.Count - 1);

            var row = _rows[fromIndex];
            _rows.RemoveAt(fromIndex);
            _rows.Insert(finalIndex, row);

            _selectedIndices.Clear();
            RefreshRowIndices(Mathf.Min(fromIndex, finalIndex));

            if (CanAppendRowsDuringPopulate())
            {
                row.RemoveFromHierarchy();
                if (finalIndex >= _scrollView.childCount)
                    _scrollView.Add(row);
                else
                    _scrollView.Insert(finalIndex, row);

                RebuildSequentialVisibleIndices();
                UpdateStatsRow();
                RefreshDragHandles();
            }
            else
            {
                ApplyFilterAndSort();
            }

            StoreCurrentRowsInCache();
            OnSelectionChanged?.Invoke(new List<int>());
            ScrollToRow(row);
            return true;
        }

        /// <summary>Applies per-row validation results. Rows absent from the dict are cleared.</summary>
        public void ApplyValidation(Dictionary<int, List<ValidationResult>> results)
        {
            if (_isPopulating) return;

            for (var i = 0; i < _rows.Count; i++)
            {
                results.TryGetValue(i, out var rowResults);
                _rows[i].SetValidationState(rowResults);
            }
        }

        /// <summary>
        ///     Refreshes the stats footer without rebuilding rows.
        ///     Call this after an inline field edit so sum/avg stays current.
        /// </summary>
        public void UpdateStats()
        {
            if (_isPopulating) return;

            UpdateStatsRow();
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
                _headerCells[col.Field.Name] = cell;

                var label = new Label(col.Label);
                label.AddToClassList("col-header");

                var indicator = new Label(string.Empty);
                indicator.AddToClassList("sort-indicator");
                _sortIndicators[col.Field.Name] = indicator;

                cell.Add(label);
                cell.Add(indicator);

                var fieldName = col.Field.Name;
                // Sort on click of the header cell (resize handle stops ClickEvent propagation)
                cell.RegisterCallback<ClickEvent>(_ => OnHeaderClicked(fieldName));

                // Resize handle — thin strip at the right edge
                cell.Add(BuildResizeHandle(col));

                _headerRow.Add(cell);
            }
        }

        private void PopulateBatch(int generation)
        {
            if (generation != _populateGeneration || !_isPopulating || _pendingEntries == null) return;

            var total = _pendingEntries.Count;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var builtThisBatch = 0;

            while (_pendingEntryIndex < total &&
                   (builtThisBatch < MinRowsPerBatch ||
                    (builtThisBatch < MaxRowsPerBatch &&
                     stopwatch.ElapsedMilliseconds < MaxBatchMilliseconds)))
            {
                var capturedIndex = _pendingEntryIndex;
                var row = CreateRow(capturedIndex, (IGameData)_pendingEntries[capturedIndex]);
                _rows.Add(row);

                if (CanAppendRowsDuringPopulate())
                {
                    _scrollView.Add(row);
                    _visibleIndices.Add(capturedIndex);
                }

                _pendingEntryIndex++;
                builtThisBatch++;
            }

            ShowLoading(_pendingEntryIndex, total);

            if (_pendingEntryIndex < total)
            {
                schedule.Execute(() => PopulateBatch(generation)).ExecuteLater(0);
                return;
            }

            FinishPopulate(generation);
        }

        private void FinishPopulate(int generation)
        {
            if (generation != _populateGeneration) return;

            _isPopulating = false;
            _pendingEntries = null;
            StoreCurrentRowsInCache();
            HideLoading();

            if (!CanAppendRowsDuringPopulate())
                ApplyFilterAndSort();
            else
            {
                UpdateStatsRow();
                RefreshDragHandles();
                ScheduleColumnLayout(true);
            }

            OnPopulateComplete?.Invoke();
        }

        private GameDataRowView CreateRow(int index, IGameData entry)
        {
            var row = new GameDataRowView(entry, _columns, index % 2 == 1);
            _rowIndices[row] = index;

            row.OnEntryChanged += updated =>
            {
                if (_rowIndices.TryGetValue(row, out var rowIndex)) _onEntryChanged?.Invoke(rowIndex, updated);
            };
            row.OnSelectionToggled += isMulti =>
            {
                if (_rowIndices.TryGetValue(row, out var rowIndex)) HandleRowSelection(rowIndex, isMulti);
            };
            row.OnRequestNextRow += colIndex => NavigateToNextRow(row, colIndex);
            row.OnDragHandlePointerDown += pos =>
            {
                if (_rowIndices.TryGetValue(row, out var rowIndex)) BeginRowDrag(rowIndex, pos);
            };

            return row;
        }

        private void RefreshRowIndices(int startIndex)
        {
            startIndex = Mathf.Max(0, startIndex);
            for (var i = startIndex; i < _rows.Count; i++)
            {
                _rowIndices[_rows[i]] = i;
                _rows[i].SetAlternate(i % 2 == 1);
            }
        }

        private List<int> GetValidUniqueIndices(List<int> indices)
        {
            var unique = new List<int>();
            foreach (var index in indices)
            {
                if (index < 0 || index >= _rows.Count || unique.Contains(index)) continue;
                unique.Add(index);
            }

            return unique;
        }

        private void RebuildSequentialVisibleIndices()
        {
            _visibleIndices.Clear();
            for (var i = 0; i < _rows.Count; i++) _visibleIndices.Add(i);
        }

        private void ApplyCurrentColumnWidths(GameDataRowView row)
        {
            if (row == null || _computedColumnWidths.Count == 0) return;

            foreach (var width in _computedColumnWidths) row.SetColumnWidth(width.Key, width.Value);
        }

        private bool CanAppendRowsDuringPopulate()
        {
            return string.IsNullOrEmpty(_sortField) &&
                   string.IsNullOrEmpty(_searchText) &&
                   !_enabledOnly;
        }

        private void ShowLoading(int loaded, int total)
        {
            _loadingPane.style.display = DisplayStyle.Flex;
            SetTableChromeVisible(false);
            _loadingLabel.text = total > 0
                ? $"Loading rows... {loaded}/{total}"
                : "Loading rows...";
            OnPopulateProgress?.Invoke(loaded, total);
        }

        private void HideLoading()
        {
            if (_loadingPane == null) return;
            _loadingPane.style.display = DisplayStyle.None;
            SetTableChromeVisible(true);
        }

        private void SetTableChromeVisible(bool visible)
        {
            var display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_headerClip != null) _headerClip.style.display = display;
            if (_scrollView != null) _scrollView.style.display = display;
            if (_statsClip != null) _statsClip.style.display = display;
            if (_footer != null) _footer.style.display = display;
        }

        private bool TryUseCachedRows(IGameDataContainer container, IList entries)
        {
            var key = GetContainerCacheKey(container);
            if (!_rowCache.TryGetValue(key, out var cache)) return false;
            if (cache.EntryType != container.EntryType) return false;
            if (cache.RowCount != entries.Count) return false;
            if (cache.Rows == null || cache.Rows.Count != entries.Count) return false;

            _rows.AddRange(cache.Rows);
            foreach (var row in _rows)
            {
                row.SetSelected(false);
                row.SetValidationState(null);
            }

            RefreshRowIndices(0);
            _rowCacheOrder.Remove(key);
            _rowCacheOrder.Add(key);
            return true;
        }

        private void StoreCurrentRowsInCache()
        {
            if (_container == null) return;

            var key = GetContainerCacheKey(_container);
            if (_rows.Count == 0)
            {
                _rowCache.Remove(key);
                _rowCacheOrder.Remove(key);
                return;
            }

            _rowCache[key] = new RowCacheEntry
            {
                EntryType = _container.EntryType,
                RowCount = _rows.Count,
                Rows = new List<GameDataRowView>(_rows)
            };

            _rowCacheOrder.Remove(key);
            _rowCacheOrder.Add(key);

            while (_rowCacheOrder.Count > MaxCachedTableCount)
            {
                var oldestKey = _rowCacheOrder[0];
                _rowCacheOrder.RemoveAt(0);
                if (oldestKey != key) _rowCache.Remove(oldestKey);
            }
        }

        private static int GetContainerCacheKey(IGameDataContainer container)
        {
            return container is UnityEngine.Object unityObject
                ? unityObject.GetInstanceID()
                : container.GetHashCode();
        }

        private void ScrollToRow(GameDataRowView row)
        {
            if (row == null) return;
            schedule.Execute(() =>
            {
                if (row.panel == null) return;
                _scrollView.ScrollTo(row);
            }).ExecuteLater(0);
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
            _footer = new VisualElement();
            _footer.AddToClassList("table-footer");

            var addBtn = new Button(() => _onAddEntry?.Invoke()) { text = "+ Add Row" };
            addBtn.AddToClassList("footer-button");

            var duplicateBtn = new Button(DuplicateSelected) { text = "⧉ Duplicate" };
            duplicateBtn.AddToClassList("footer-button");

            var removeBtn = new Button(RemoveSelected) { text = "− Remove Selected" };
            removeBtn.AddToClassList("footer-button");
            removeBtn.AddToClassList("footer-button--danger");

            _footer.Add(addBtn);
            _footer.Add(duplicateBtn);
            _footer.Add(removeBtn);
            Add(_footer);
        }

        // ── Filter / Sort ──────────────────────────────────────────────────────────

        private void ApplyFilterAndSort()
        {
            ScheduleApplyFilterAndSort(0);
        }

        private void ScheduleApplyFilterAndSort(int delayMilliseconds)
        {
            if (_isPopulating) return;

            var generation = ++_viewGeneration;
            schedule.Execute(() => BeginApplyFilterAndSort(generation))
                .ExecuteLater(Mathf.Max(0, delayMilliseconds));
        }

        private void BeginApplyFilterAndSort(int generation)
        {
            if (generation != _viewGeneration || _isPopulating) return;
            if (_container == null) return;

            var entries = _container.GetEntries();

            // Cached reflection for optional fields used in filter (e.g. "Enabled")
            var enabledField = _container.EntryType.GetField("Enabled");

            // Step 1: filter
            var visible = new List<int>(entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = (IGameData)entries[i];

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
                    visible.Sort((a, b) =>
                    {
                        var va = fieldInfo.GetValue(entries[a]) as IComparable;
                        var vb = fieldInfo.GetValue(entries[b]) as IComparable;
                        var cmp = va?.CompareTo(vb) ?? 0;
                        return _sortAsc ? cmp : -cmp;
                    });
            }

            ApplyVisibleOrder(visible, generation);
        }

        private void ApplyVisibleOrder(List<int> visible, int generation)
        {
            if (generation != _viewGeneration || visible == null) return;

            _isApplyingView = true;
            _pendingVisibleOrder = visible;
            _pendingVisibleIndex = 0;
            _scrollView.Clear();
            _visibleIndices.Clear();
            schedule.Execute(() => ApplyVisibleOrderBatch(generation)).ExecuteLater(0);
        }

        private void ApplyVisibleOrderBatch(int generation)
        {
            if (generation != _viewGeneration || !_isApplyingView || _pendingVisibleOrder == null) return;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var addedThisBatch = 0;
            var total = _pendingVisibleOrder.Count;

            while (_pendingVisibleIndex < total &&
                   (addedThisBatch < MinViewRowsPerBatch ||
                    (addedThisBatch < MaxViewRowsPerBatch &&
                     stopwatch.ElapsedMilliseconds < MaxBatchMilliseconds)))
            {
                var rowIndex = _pendingVisibleOrder[_pendingVisibleIndex];
                _scrollView.Add(_rows[rowIndex]);
                _visibleIndices.Add(rowIndex);
                _pendingVisibleIndex++;
                addedThisBatch++;
            }

            if (_pendingVisibleIndex < total)
            {
                schedule.Execute(() => ApplyVisibleOrderBatch(generation)).ExecuteLater(0);
                return;
            }

            FinishApplyVisibleOrder(generation);
        }

        private void FinishApplyVisibleOrder(int generation)
        {
            if (generation != _viewGeneration) return;

            _isApplyingView = false;
            _pendingVisibleOrder = null;
            _pendingVisibleIndex = 0;
            UpdateStatsRow();
            RefreshDragHandles();

            if (_computedColumnWidths.Count == 0)
                ScheduleColumnLayout();
        }

        private void ScheduleColumnLayout(bool resetHorizontalScroll = false)
        {
            if (resetHorizontalScroll)
            {
                _scrollView.horizontalScroller.value = 0;
                SyncHorizontalOffset(0);
            }

            // UI Toolkit resolves viewport sizes after the current layout pass. A
            // second delayed pass catches switches where scrollbar visibility changes.
            schedule.Execute(UpdateColumnLayout).ExecuteLater(0);
            schedule.Execute(UpdateColumnLayout).ExecuteLater(50);
        }

        private void UpdateColumnLayout()
        {
            RecalculateColumnWidths();
            ApplyComputedColumnWidths();
        }

        private void RecalculateColumnWidths()
        {
            _computedColumnWidths.Clear();

            if (_columns == null || _columns.Count == 0) return;

            var availableWidth = Mathf.Max(0, GetAvailableTableWidth() - GutterWidth);
            float fixedWidth = 0;
            float flexibleMinWidth = 0;
            float totalFlexGrow = 0;

            foreach (var col in _columns)
            {
                var fieldName = col.Field.Name;

                if (_colWidthOverrides.TryGetValue(fieldName, out var overrideWidth))
                {
                    fixedWidth += Mathf.Max(col.MinWidth, overrideWidth);
                    continue;
                }

                if (col.FlexGrow < 0.01f)
                {
                    fixedWidth += col.MinWidth;
                    continue;
                }

                flexibleMinWidth += col.MinWidth;
                totalFlexGrow += col.FlexGrow;
            }

            var flexibleSpace = Mathf.Max(flexibleMinWidth, availableWidth - fixedWidth);
            var extraFlexibleSpace = Mathf.Max(0, flexibleSpace - flexibleMinWidth);

            foreach (var col in _columns)
            {
                var fieldName = col.Field.Name;

                if (_colWidthOverrides.TryGetValue(fieldName, out var overrideWidth))
                {
                    _computedColumnWidths[fieldName] = Mathf.Max(col.MinWidth, overrideWidth);
                    continue;
                }

                if (col.FlexGrow < 0.01f || totalFlexGrow < 0.01f)
                {
                    _computedColumnWidths[fieldName] = col.MinWidth;
                    continue;
                }

                var flexShare = extraFlexibleSpace * (col.FlexGrow / totalFlexGrow);
                _computedColumnWidths[fieldName] = col.MinWidth + flexShare;
            }
        }

        private void ApplyComputedColumnWidths()
        {
            if (_computedColumnWidths.Count == 0) return;

            var contentWidth = GutterWidth;

            foreach (var col in _columns)
            {
                var fieldName = col.Field.Name;
                if (!_computedColumnWidths.TryGetValue(fieldName, out var width)) continue;

                contentWidth += width;

                if (_headerCells.TryGetValue(fieldName, out var headerCell)) SetFixedWidth(headerCell, width);

                if (_statsCells.TryGetValue(fieldName, out var statsCell)) SetFixedWidth(statsCell, width);

                foreach (var row in _rows) row.SetColumnWidth(fieldName, width);
            }

            _headerRow.style.width = contentWidth;
            _scrollView.contentContainer.style.minWidth = contentWidth;
            if (_statsRow != null) _statsRow.style.width = contentWidth;

            SyncHorizontalOffset(_scrollView.horizontalScroller.value);
        }

        private float GetAvailableTableWidth()
        {
            var width = _scrollView.contentViewport.resolvedStyle.width;
            if (width > 0) return width;

            width = _scrollView.resolvedStyle.width;
            if (width > 0) return width;

            width = resolvedStyle.width;
            return width > 0 ? width : 0;
        }

        private void SyncHorizontalOffset(float offset)
        {
            var translate = new Translate(-offset, 0);
            _headerRow.style.translate = translate;
            if (_statsRow != null) _statsRow.style.translate = translate;
        }

        // ── Stats row ──────────────────────────────────────────────────────────────

        /// <summary>Creates the stats row VisualElement (called once in constructor).</summary>
        private void BuildStatsRow()
        {
            _statsClip = new VisualElement();
            _statsClip.AddToClassList("table-stats-clip");
            Add(_statsClip);

            _statsRow = new VisualElement();
            _statsRow.AddToClassList("stats-row");
            _statsClip.Add(_statsRow);
        }

        /// <summary>
        ///     Rebuilds stats row labels to match the current column set.
        ///     Called whenever the container type changes (i.e. in Populate()).
        /// </summary>
        private void RebuildStatsRow()
        {
            _statsRow.Clear();
            _statsLabels.Clear();
            _statsCells.Clear();

            // Row count occupies the same gutter slot as header/body rows so stat
            // cells line up with their matching data columns.
            _statsCountLabel = new Label();
            _statsCountLabel.AddToClassList("col-gutter");
            _statsCountLabel.AddToClassList("stats-count-label");
            _statsRow.Add(_statsCountLabel);

            if (_columns == null || _columns.Count == 0) return;

            foreach (var col in _columns)
            {
                var label = new Label();
                label.AddToClassList("stats-cell");
                label.AddToClassList($"col-{col.Field.Name.ToLower()}");
                ApplySizing(label, col);
                _statsRow.Add(label);
                _statsLabels.Add(label);
                _statsCells[col.Field.Name] = label;
            }
        }

        /// <summary>
        ///     Recomputes sum and average for numeric columns over currently visible rows.
        /// </summary>
        private void UpdateStatsRow()
        {
            if (_statsCountLabel != null)
            {
                var rowCount = _visibleIndices.Count;
                _statsCountLabel.text = rowCount.ToString();
                _statsCountLabel.tooltip = $"{rowCount} row{(rowCount == 1 ? "" : "s")}";
            }

            if (_container == null || _statsLabels.Count == 0) return;

            var entries = _container.GetEntries();

            for (var ci = 0; ci < _columns.Count && ci < _statsLabels.Count; ci++)
            {
                var col = _columns[ci];
                var label = _statsLabels[ci];

                if (!col.IsInt && !col.IsFloat)
                {
                    label.text = string.Empty;
                    continue;
                }

                double sum = 0;
                var count = 0;
                foreach (var idx in _visibleIndices)
                {
                    var raw = col.Field.GetValue(entries[idx]);
                    var val = col.IsInt ? (int)raw : (double)(float)raw;
                    sum += val;
                    count++;
                }

                var avg = count > 0 ? sum / count : 0;
                label.text = col.IsInt
                    ? $"Σ {(long)sum}  ∅ {avg:F1}"
                    : $"Σ {sum:F1}  ∅ {avg:F1}";
            }
        }

        private void OnHeaderClicked(string fieldName)
        {
            if (_sortField == fieldName)
            {
                _sortAsc = !_sortAsc;
            }
            else
            {
                _sortField = fieldName;
                _sortAsc = true;
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
                foreach (var i in _selectedIndices)
                    if (i < _rows.Count)
                        _rows[i].SetSelected(false);

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
        ///     Called by a row's <see cref="GameDataRowView.OnRequestNextRow" /> event.
        ///     Finds the next visible row and focuses the same column.
        /// </summary>
        private void NavigateToNextRow(GameDataRowView sourceRow, int colIndex)
        {
            var visibleIndex = _scrollView.IndexOf(sourceRow);
            if (visibleIndex < 0 || visibleIndex >= _scrollView.childCount - 1) return;
            (_scrollView[visibleIndex + 1] as GameDataRowView)?.FocusColumn(colIndex);
        }

        // ── Drag-to-reorder ────────────────────────────────────────────────────────

        /// <summary>Shows/hides drag handles on all rows based on <see cref="IsDragEnabled" />.</summary>
        private void RefreshDragHandles()
        {
            var enabled = IsDragEnabled;
            foreach (var row in _rows) row.SetDragEnabled(enabled);
        }

        private void BeginRowDrag(int dataIndex, Vector2 worldPosition)
        {
            if (!IsDragEnabled) return;
            _dragPending = true;
            _isDragging = false;
            _draggingDataIndex = dataIndex;
            _dragStartPosition = worldPosition;
            _dropTargetVisibleIdx = -1;
        }

        private void OnDragPointerMove(PointerMoveEvent evt)
        {
            if (_dragPending && !_isDragging)
                // Activate drag only after the pointer has moved beyond the threshold.
                if (Vector2.Distance(evt.position, _dragStartPosition) >= DragThreshold)
                    _isDragging = true;

            if (!_isDragging) return;

            var newTarget = ComputeDropTarget(evt.position);
            if (newTarget != _dropTargetVisibleIdx)
            {
                _dropTargetVisibleIdx = newTarget;
                UpdateDropIndicator();
            }
        }

        private void OnDragPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging && !_dragPending) return;

            var wasActuallyDragging = _isDragging;
            _isDragging = false;
            _dragPending = false;
            _dropIndicator.RemoveFromHierarchy();

            var from = _draggingDataIndex;
            var insertBefore = _dropTargetVisibleIdx;

            _draggingDataIndex = -1;
            _dropTargetVisibleIdx = -1;

            if (!wasActuallyDragging) return;
            if (from < 0 || insertBefore < 0) return;
            if (insertBefore == from || insertBefore == from + 1) return;

            _onMoveEntry?.Invoke(from, insertBefore);
        }

        /// <summary>
        ///     Calculates the insert-before position (0-based, 0..rowCount) by comparing
        ///     the pointer's Y position against the midpoint of each visible row.
        ///     Ignores the drop indicator element itself.
        /// </summary>
        private int ComputeDropTarget(Vector2 worldPos)
        {
            var content = _scrollView.contentContainer;
            var localPos = content.WorldToLocal(worldPos);
            var y = localPos.y;

            var realRowIdx = 0;
            for (var ci = 0; ci < content.childCount; ci++)
            {
                var child = content[ci];
                if (child == _dropIndicator) continue;

                var midY = child.layout.y + child.layout.height * 0.5f;
                if (y < midY) return realRowIdx;
                realRowIdx++;
            }

            return realRowIdx; // after all rows
        }

        /// <summary>
        ///     Repositions the drop indicator line in the scroll view content so it appears
        ///     between the appropriate rows. Clamped to valid positions.
        /// </summary>
        private void UpdateDropIndicator()
        {
            _dropIndicator.RemoveFromHierarchy();
            if (!_isDragging || _dropTargetVisibleIdx < 0) return;

            var content = _scrollView.contentContainer;

            // Count real rows (excluding the indicator itself)
            var realCount = 0;
            for (var ci = 0; ci < content.childCount; ci++)
                if (content[ci] != _dropIndicator)
                    realCount++;

            var insertAt = Mathf.Clamp(_dropTargetVisibleIdx, 0, realCount);
            if (insertAt >= content.childCount)
                content.Add(_dropIndicator);
            else
                content.Insert(insertAt, _dropIndicator);
        }

        // ── Column resizing ────────────────────────────────────────────────────────

        private VisualElement BuildResizeHandle(GameDataColumnDefinition col)
        {
            var handle = new VisualElement();
            handle.AddToClassList("col-resize-handle");

            // Prevent clicks on the handle from bubbling to the header cell (which sorts)
            handle.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            float dragStartX = 0;
            float dragStartW = 0;
            var dragging = false;
            var fieldName = col.Field.Name;

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;

                // Double-click → reset column to default flex sizing
                if (evt.clickCount == 2)
                {
                    dragging = false;
                    handle.ReleasePointer(evt.pointerId);
                    _colWidthOverrides.Remove(fieldName);
                    UpdateColumnLayout();
                    evt.StopPropagation();
                    return;
                }

                dragging = true;
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
                var newWidth = Mathf.Max(col.MinWidth, dragStartW + (evt.position.x - dragStartX));
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
        ///     Stores a fixed-width override for <paramref name="fieldName" /> and applies it
        ///     to the header cell and all row cells immediately.
        /// </summary>
        private void ApplyWidthOverride(string fieldName, float width)
        {
            _colWidthOverrides[fieldName] = width;
            UpdateColumnLayout();
        }

        private static void SetFixedWidth(VisualElement el, float width)
        {
            el.style.width = width;
            el.style.minWidth = width;
            el.style.flexGrow = 0;
            el.style.flexShrink = 0;
        }

        // ── Helpers ────────────────────────────────────────────────────────────────

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
    }
}
