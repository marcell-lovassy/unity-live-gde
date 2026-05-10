using System.Collections.Generic;
using System.Linq;
using LiveGameDataEditor.GoogleSheets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    ///     Main editor window for the Game Data Spreadsheet Editor.
    ///     Open via: Tools > GDE > Open Editor
    ///     Layout:
    ///     SelectionBar
    ///     Toolbar (search / filter / Browse toggle / Sheets toggle / JSON / CSV)
    ///     GoogleSheetsSyncPanel (hidden by default, shown when Sheets toggle is on)
    ///     MainArea [horizontal]
    ///     BrowserPanel (220 px, toggleable) | MainContent (EmptyState or ContentArea)
    /// </summary>
    public class LiveGameDataEditorWindow : EditorWindow
    {
        private const int MinValidationRowsPerBatch = 4;
        private const int MaxValidationRowsPerBatch = 40;
        private const long MaxValidationBatchMilliseconds = 8;

        private bool _browserOpen;
        private GameDataBrowserPanel _browserPanel;
        private VisualElement _browserResizeHandle;
        private IGameDataContainer _container;
        private VisualElement _contentArea;

        // Data type subtitle label — populated from GameDataAttribute when a container is loaded.
        private Label _dataTypeLabel;
        private VisualElement _emptyState;
        private bool _enabledOnly;
        private VisualElement _mainArea;
        private VisualElement _mainContent;

        // Toolbar filter state
        private string _searchText = string.Empty;
        private GameDataSelectionBar _selectionBar;
        private bool _sheetsOpen;
        private GoogleSheetsSyncPanel _sheetsPanel;
        private GameDataTableView _tableView;
        private readonly Dictionary<int, ValidationCacheEntry> _validationCache = new();
        private Dictionary<int, List<ValidationResult>> _collectionValidationResults;
        private Dictionary<int, List<ValidationResult>> _fieldValidationResults;
        private IReadOnlyList<GameDataColumnDefinition> _validationColumns;
        private List<IGameData> _validationEntries;
        private bool _validationApplyAllRows;
        private int _validationGeneration;
        private int _validationInProgressKey = int.MinValue;
        private int _validationInProgressRowCount = -1;
        private int _validationRowIndex;
        private List<int> _validationRows;
        private int _validationRowsIndex;
        private Dictionary<int, List<ValidationResult>> _validationResults;

        private sealed class ValidationCacheEntry
        {
            public int RowCount;
            public Dictionary<int, List<ValidationResult>> CollectionResults;
            public Dictionary<int, List<ValidationResult>> FieldResults;
            public Dictionary<int, List<ValidationResult>> Results;
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.update += UpdateDirtyIndicator;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.update -= UpdateDirtyIndicator;
            CancelValidation();
            _tableView?.CancelPopulation();
        }

        public void CreateGUI()
        {
            var ussGuids = AssetDatabase.FindAssets("LiveGameDataEditor t:StyleSheet");
            if (ussGuids is { Length: > 0 })
            {
                var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(ussGuids[0]));
                rootVisualElement.styleSheets.Add(uss);
            }

            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.flexGrow = 1;

            BuildSelectionBar();
            BuildToolbar();
            BuildSheetsPanel();
            BuildMainArea();
            RefreshView();
        }

        [MenuItem("Tools/GDE/Open Editor", priority = 100)]
        public static void OpenWindow()
        {
            var window = GetWindow<LiveGameDataEditorWindow>();
            window.titleContent = new GUIContent(
                "Game Data Spreadsheet Editor",
                EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
            window.minSize = new Vector2(700, 420);
        }

        // ── Selection bar ──────────────────────────────────────────────────────────

        private void BuildSelectionBar()
        {
            _selectionBar = new GameDataSelectionBar();
            _selectionBar.OnContainerSelected += container =>
            {
                _container = container;
                _searchText = string.Empty;
                _enabledOnly = false;
                _browserPanel?.SetActiveContainer(container as ScriptableObject);
                RefreshView();
            };
            _selectionBar.OnContainerCleared += () =>
            {
                _container = null;
                _browserPanel?.SetActiveContainer(null);
                RefreshView();
            };
            rootVisualElement.Add(_selectionBar);
        }

        // ── Toolbar ────────────────────────────────────────────────────────────────

        private void BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");

            // Browse toggle
            var browseBtn = new Button(ToggleBrowser) { text = "☰ Browse" };
            browseBtn.AddToClassList("toolbar-button");
            toolbar.Add(browseBtn);

            // Search field
            var searchField = new TextField("Search") { value = _searchText };
            searchField.AddToClassList("search-field");
            searchField.tooltip = "Search by Id";
            searchField.RegisterValueChangedCallback(evt =>
            {
                _searchText = evt.newValue;
                _tableView?.SetFilter(_searchText, _enabledOnly);
            });
            toolbar.Add(searchField);

            // Enabled-only toggle
            var enabledToggle = new Toggle("Enabled only") { value = _enabledOnly };
            enabledToggle.AddToClassList("toolbar-toggle");
            enabledToggle.RegisterValueChangedCallback(evt =>
            {
                _enabledOnly = evt.newValue;
                _tableView?.SetFilter(_searchText, _enabledOnly);
            });
            toolbar.Add(enabledToggle);

            // JSON
            var exportJsonBtn = new Button(() => GameDataService.ExportToJson(_container))
                { text = "Export JSON" };
            exportJsonBtn.AddToClassList("toolbar-button");

            var importJsonBtn = new Button(ImportJsonAndRefresh) { text = "Import JSON" };
            importJsonBtn.AddToClassList("toolbar-button");

            // CSV
            var exportCsvBtn = new Button(() => GameDataService.ExportToCsv(_container))
                { text = "Export CSV" };
            exportCsvBtn.AddToClassList("toolbar-button");

            var importCsvBtn = new Button(ImportCsvAndRefresh) { text = "Import CSV" };
            importCsvBtn.AddToClassList("toolbar-button");

            toolbar.Add(exportJsonBtn);
            toolbar.Add(importJsonBtn);
            toolbar.Add(exportCsvBtn);
            toolbar.Add(importCsvBtn);

            // Google Sheets sync toggle — rightmost
            var sheetsBtn = new Button(ToggleSheets) { text = "☁ Sheets" };
            sheetsBtn.AddToClassList("toolbar-button");
            toolbar.Add(sheetsBtn);

            rootVisualElement.Add(toolbar);
        }

        // ── Google Sheets sync panel ───────────────────────────────────────────────

        private void BuildSheetsPanel()
        {
            _sheetsPanel = new GoogleSheetsSyncPanel();
            _sheetsPanel.OnPullComplete += () =>
            {
                InvalidateCachedData(_container);
                RefreshView();
            };
            _sheetsPanel.style.display = _sheetsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            rootVisualElement.Add(_sheetsPanel);
        }

        private void ToggleSheets()
        {
            _sheetsOpen = !_sheetsOpen;
            _sheetsPanel.style.display = _sheetsOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_sheetsOpen) _sheetsPanel.SetContainer(_container);
        }

        // ── Main horizontal area (browser | content) ───────────────────────────────

        private void BuildMainArea()
        {
            _mainArea = new VisualElement();
            _mainArea.AddToClassList("main-area");
            _mainArea.style.flexDirection = FlexDirection.Row;
            _mainArea.style.flexGrow = 1;

            // Browser sidebar
            _browserPanel = new GameDataBrowserPanel();
            _browserPanel.OnContainerSelected += so =>
            {
                _selectionBar.SelectContainer(so);
                _browserPanel.SetActiveContainer(so);
            };
            _browserPanel.style.display = _browserOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _mainArea.Add(_browserPanel);

            // Browser resize handle — draggable right edge of the sidebar
            _browserResizeHandle = BuildBrowserResizeHandle();
            _browserResizeHandle.style.display = _browserOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _mainArea.Add(_browserResizeHandle);

            // Content area wrapper
            _mainContent = new VisualElement();
            _mainContent.style.flexGrow = 1;
            _mainContent.style.flexDirection = FlexDirection.Column;

            BuildEmptyState();
            BuildContentArea();

            _mainArea.Add(_mainContent);
            rootVisualElement.Add(_mainArea);
        }

        private VisualElement BuildBrowserResizeHandle()
        {
            var handle = new VisualElement();
            handle.AddToClassList("browser-resize-handle");

            float dragStartX = 0;
            float dragStartW = 0;
            var dragging = false;

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;

                dragging = true;
                dragStartX = evt.position.x;
                dragStartW = _browserPanel.resolvedStyle.width;
                handle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging) return;

                var newW = Mathf.Clamp(dragStartW + (evt.position.x - dragStartX), 120f, 500f);
                _browserPanel.style.width = newW;
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

        // ── Empty state ────────────────────────────────────────────────────────────

        private void BuildEmptyState()
        {
            _emptyState = new VisualElement();
            _emptyState.AddToClassList("empty-state");

            var label = new Label(
                "No data asset loaded.\n" +
                "Use ☰ Browse to open an existing asset, or create a new one below.");
            label.AddToClassList("empty-state-label");

            var createBtn = new Button(() => _selectionBar.TriggerCreateNew())
                { text = "＋ Create New Data Asset" };
            createBtn.AddToClassList("create-btn");

            _emptyState.Add(label);
            _emptyState.Add(createBtn);
            _mainContent.Add(_emptyState);
        }

        // ── Content area ───────────────────────────────────────────────────────────

        private void BuildContentArea()
        {
            _contentArea = new VisualElement();
            _contentArea.AddToClassList("content-area");
            _contentArea.style.flexGrow = 1;
            _contentArea.style.flexDirection = FlexDirection.Column;

            // Subtitle showing the entry type's friendly display name.
            _dataTypeLabel = new Label(string.Empty);
            _dataTypeLabel.AddToClassList("data-type-label");
            _contentArea.Add(_dataTypeLabel);

            _tableView = new GameDataTableView(
                OnEntryChanged,
                OnAddEntry,
                OnRemoveEntries,
                OnDuplicateEntries,
                OnMoveEntry);
            _tableView.OnPopulateProgress += OnTablePopulateProgress;
            _tableView.OnPopulateComplete += OnTablePopulateComplete;

            _contentArea.Add(_tableView);
            _mainContent.Add(_contentArea);
        }

        // ── View management ────────────────────────────────────────────────────────

        private void RefreshView()
        {
            CancelValidation();

            var has = _container != null;
            _emptyState.style.display = has ? DisplayStyle.None : DisplayStyle.Flex;
            _contentArea.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;

            if (!has)
            {
                _selectionBar.UpdateInfo(null);
                if (_sheetsPanel != null) _sheetsPanel.SetContainer(null);

                return;
            }

            _dataTypeLabel.text = GameDataTypeRegistry.GetEntryDisplayName(_container.EntryType);

            _tableView.SetFilter(_searchText, _enabledOnly, false);
            _tableView.Populate(_container);

            _selectionBar.UpdateInfo(_container);

            // Keep the sheets panel in sync with the current container even when it's hidden,
            // so it's ready to use as soon as the designer opens it.
            if (_sheetsPanel != null) _sheetsPanel.SetContainer(_container);
        }

        private void OnTablePopulateProgress(int loaded, int total)
        {
            if (_container == null || total <= 0) return;

            var displayName = GameDataTypeRegistry.GetEntryDisplayName(_container.EntryType);
            _dataTypeLabel.text = $"{displayName} - Loading {loaded}/{total} rows";
        }

        private void OnTablePopulateComplete()
        {
            if (_container == null) return;

            StartValidation();
        }

        private void StartValidation()
        {
            if (_container == null) return;

            var displayName = GameDataTypeRegistry.GetEntryDisplayName(_container.EntryType);
            var entries = _container.GetEntries();
            var cacheKey = GetContainerCacheKey(_container);
            if (_validationEntries != null &&
                _validationInProgressKey == cacheKey &&
                _validationInProgressRowCount == entries.Count)
                return;

            var generation = ++_validationGeneration;
            _dataTypeLabel.text = $"{displayName} - Validating...";

            if (TryApplyCachedValidation(_container, entries.Count))
            {
                _dataTypeLabel.text = displayName;
                _validationEntries = null;
                _validationColumns = null;
                _validationResults = null;
                _validationRowIndex = 0;
                _validationInProgressKey = int.MinValue;
                _validationInProgressRowCount = -1;
                return;
            }

            _validationEntries = entries.Cast<IGameData>().ToList();
            _validationColumns = GameDataColumnDefinition.FromType(_container.EntryType);
            _collectionValidationResults = GameDataValidationService.RunAll(_validationEntries);
            _fieldValidationResults = new Dictionary<int, List<ValidationResult>>();
            _validationResults = CloneValidationResults(_collectionValidationResults);
            _validationRowIndex = 0;
            _validationRows = null;
            _validationRowsIndex = 0;
            _validationApplyAllRows = true;
            _validationInProgressKey = cacheKey;
            _validationInProgressRowCount = entries.Count;

            _contentArea.schedule.Execute(() => ValidateBatch(generation)).ExecuteLater(0);
        }

        private void CancelValidation()
        {
            _validationGeneration++;
            ClearValidationWorkState();
        }

        private void ClearValidationWorkState()
        {
            _validationEntries = null;
            _validationColumns = null;
            _validationResults = null;
            _validationRowIndex = 0;
            _validationRows = null;
            _validationRowsIndex = 0;
            _validationApplyAllRows = false;
            _validationInProgressKey = int.MinValue;
            _validationInProgressRowCount = -1;
        }

        private void ValidateBatch(int generation)
        {
            if (generation != _validationGeneration ||
                _container == null ||
                _tableView == null ||
                _tableView.IsLoading ||
                _validationEntries == null ||
                _validationColumns == null ||
                _validationResults == null)
                return;

            var total = _validationEntries.Count;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var rowsChecked = 0;

            while (HasMoreValidationRows(total) &&
                   (rowsChecked < MinValidationRowsPerBatch ||
                    (rowsChecked < MaxValidationRowsPerBatch &&
                     stopwatch.ElapsedMilliseconds < MaxValidationBatchMilliseconds)))
            {
                ValidateRow(GetNextValidationRow());
                rowsChecked++;
            }

            var displayName = GameDataTypeRegistry.GetEntryDisplayName(_container.EntryType);
            if (HasMoreValidationRows(total))
            {
                _dataTypeLabel.text = $"{displayName} - Validating {GetValidatedRowCount()}/{total} rows";
                _contentArea.schedule.Execute(() => ValidateBatch(generation)).ExecuteLater(0);
                return;
            }

            _validationResults = CombineValidationResults(_collectionValidationResults, _fieldValidationResults);
            if (_validationApplyAllRows || _validationRows == null)
                _tableView.ApplyValidation(_validationResults);
            else
                _tableView.ApplyValidationRows(_validationResults, _validationRows);

            _tableView.UpdateStats();
            _dataTypeLabel.text = displayName;
            StoreValidationCache(
                _container,
                total,
                _collectionValidationResults,
                _fieldValidationResults,
                _validationResults);

            _validationEntries = null;
            _validationColumns = null;
            _validationResults = null;
            _validationRowIndex = 0;
            _validationRows = null;
            _validationRowsIndex = 0;
            _validationApplyAllRows = false;
            _validationInProgressKey = int.MinValue;
            _validationInProgressRowCount = -1;
        }

        private bool HasMoreValidationRows(int total)
        {
            return _validationRows == null
                ? _validationRowIndex < total
                : _validationRowsIndex < _validationRows.Count;
        }

        private int GetNextValidationRow()
        {
            if (_validationRows == null) return _validationRowIndex++;
            return _validationRows[_validationRowsIndex++];
        }

        private int GetValidatedRowCount()
        {
            return _validationRows == null ? _validationRowIndex : _validationRowsIndex;
        }

        private void ValidateRow(int rowIndex)
        {
            var entry = _validationEntries[rowIndex];
            foreach (var column in _validationColumns)
            {
                var context = new TableValidationContext(
                    _container,
                    _validationEntries,
                    _validationColumns,
                    entry,
                    rowIndex,
                    column,
                    column.Field.GetValue(entry));

                foreach (var validator in TableFieldValidationService.Validators)
                {
                    if (!validator.CanValidate(context)) continue;

                    AddValidationResults(_fieldValidationResults, validator.Validate(context));
                }
            }
        }

        private static void AddValidationResults(
            Dictionary<int, List<ValidationResult>> target,
            IEnumerable<ValidationResult> results)
        {
            foreach (var result in results)
            {
                if (!target.TryGetValue(result.RowIndex, out var rowResults))
                {
                    rowResults = new List<ValidationResult>();
                    target[result.RowIndex] = rowResults;
                }

                rowResults.Add(result);
            }
        }

        private void StartAffectedRowsValidation(
            IEnumerable<int> affectedRows,
            bool validateCollection,
            bool applyAllRows)
        {
            if (_container == null) return;

            if (_validationEntries != null)
            {
                _validationGeneration++;
                ClearValidationWorkState();
                _collectionValidationResults = null;
                _fieldValidationResults = null;
                StartValidation();
                return;
            }

            if (_collectionValidationResults == null || _fieldValidationResults == null)
            {
                StartValidation();
                return;
            }

            var entries = _container.GetEntries();
            var rows = GetValidUniqueRows(affectedRows, entries.Count);
            var generation = ++_validationGeneration;
            var displayName = GameDataTypeRegistry.GetEntryDisplayName(_container.EntryType);

            _validationEntries = entries.Cast<IGameData>().ToList();
            _validationColumns = GameDataColumnDefinition.FromType(_container.EntryType);
            if (validateCollection)
                _collectionValidationResults = GameDataValidationService.RunAll(_validationEntries);

            foreach (var row in rows) _fieldValidationResults.Remove(row);

            _validationResults = CombineValidationResults(_collectionValidationResults, _fieldValidationResults);
            _validationRows = rows;
            _validationRowsIndex = 0;
            _validationRowIndex = 0;
            _validationApplyAllRows = applyAllRows || validateCollection;
            _validationInProgressKey = GetContainerCacheKey(_container);
            _validationInProgressRowCount = entries.Count;

            if (rows.Count == 0)
            {
                FinishIncrementalValidation(displayName, entries.Count);
                return;
            }

            _dataTypeLabel.text = $"{displayName} - Validating {rows.Count} changed row{(rows.Count == 1 ? "" : "s")}";
            _contentArea.schedule.Execute(() => ValidateBatch(generation)).ExecuteLater(0);
        }

        private void FinishIncrementalValidation(string displayName, int rowCount)
        {
            _validationResults = CombineValidationResults(_collectionValidationResults, _fieldValidationResults);

            if (_validationApplyAllRows || _validationRows == null)
                _tableView.ApplyValidation(_validationResults);
            else
                _tableView.ApplyValidationRows(_validationResults, _validationRows);

            _tableView.UpdateStats();
            _dataTypeLabel.text = displayName;
            StoreValidationCache(
                _container,
                rowCount,
                _collectionValidationResults,
                _fieldValidationResults,
                _validationResults);

            ClearValidationWorkState();
        }

        private static List<int> GetValidUniqueRows(IEnumerable<int> rows, int rowCount)
        {
            var unique = new List<int>();
            if (rows == null) return unique;

            foreach (var row in rows)
            {
                if (row < 0 || row >= rowCount || unique.Contains(row)) continue;
                unique.Add(row);
            }

            unique.Sort();
            return unique;
        }

        private void RemapValidationAfterInsertions(List<int> insertionRows)
        {
            if (_fieldValidationResults == null || insertionRows == null || insertionRows.Count == 0) return;

            insertionRows.Sort();
            foreach (var insertionRow in insertionRows)
            {
                _fieldValidationResults = RemapValidationRows(
                    _fieldValidationResults,
                    row => row >= insertionRow ? row + 1 : row);
                _fieldValidationResults.Remove(insertionRow);
            }
        }

        private void RemapValidationAfterRemovals(List<int> removedRows)
        {
            if (_fieldValidationResults == null || removedRows == null || removedRows.Count == 0) return;

            removedRows.Sort();
            _fieldValidationResults = RemapValidationRows(
                _fieldValidationResults,
                row =>
                {
                    if (removedRows.Contains(row)) return -1;

                    var removedBefore = 0;
                    foreach (var removed in removedRows)
                    {
                        if (removed < row) removedBefore++;
                    }

                    return row - removedBefore;
                });
        }

        private void RemapValidationAfterMove(int fromIndex, int insertBefore)
        {
            if (_fieldValidationResults == null) return;

            var entries = _container?.GetEntries();
            if (entries == null || entries.Count == 0) return;

            insertBefore = Mathf.Clamp(insertBefore, 0, entries.Count + 1);
            var finalIndex = insertBefore > fromIndex ? insertBefore - 1 : insertBefore;
            finalIndex = Mathf.Clamp(finalIndex, 0, entries.Count - 1);
            if (fromIndex == finalIndex) return;

            _fieldValidationResults = RemapValidationRows(
                _fieldValidationResults,
                row =>
                {
                    if (row == fromIndex) return finalIndex;
                    if (fromIndex < finalIndex && row > fromIndex && row <= finalIndex) return row - 1;
                    if (finalIndex < fromIndex && row >= finalIndex && row < fromIndex) return row + 1;
                    return row;
                });
        }

        private static Dictionary<int, List<ValidationResult>> RemapValidationRows(
            Dictionary<int, List<ValidationResult>> source,
            System.Func<int, int> mapRow)
        {
            var remapped = new Dictionary<int, List<ValidationResult>>();
            foreach (var pair in source)
            {
                var newRow = mapRow(pair.Key);
                if (newRow < 0) continue;

                if (!remapped.TryGetValue(newRow, out var list))
                {
                    list = new List<ValidationResult>();
                    remapped[newRow] = list;
                }

                foreach (var result in pair.Value)
                {
                    list.Add(new ValidationResult(
                        newRow,
                        result.FieldName,
                        result.Message,
                        result.Severity));
                }
            }

            return remapped;
        }

        private bool TryApplyCachedValidation(IGameDataContainer container, int rowCount)
        {
            var key = GetContainerCacheKey(container);
            if (!_validationCache.TryGetValue(key, out var cache)) return false;
            if (cache.RowCount != rowCount || cache.Results == null) return false;

            _collectionValidationResults = CloneValidationResults(cache.CollectionResults);
            _fieldValidationResults = CloneValidationResults(cache.FieldResults);
            _tableView.ApplyValidation(cache.Results);
            _tableView.UpdateStats();
            return true;
        }

        private void StoreValidationCache(
            IGameDataContainer container,
            int rowCount,
            Dictionary<int, List<ValidationResult>> collectionResults,
            Dictionary<int, List<ValidationResult>> fieldResults,
            Dictionary<int, List<ValidationResult>> results)
        {
            if (container == null || results == null) return;

            _validationCache[GetContainerCacheKey(container)] = new ValidationCacheEntry
            {
                RowCount = rowCount,
                CollectionResults = CloneValidationResults(collectionResults),
                FieldResults = CloneValidationResults(fieldResults),
                Results = CloneValidationResults(results)
            };
        }

        private void InvalidateCachedData(IGameDataContainer container)
        {
            InvalidateValidationCache(container);
            _tableView?.InvalidateCachedRows(container);
        }

        private void InvalidateValidationCache(IGameDataContainer container)
        {
            if (container == null)
            {
                _validationCache.Clear();
                _collectionValidationResults = null;
                _fieldValidationResults = null;
                return;
            }

            _validationCache.Remove(GetContainerCacheKey(container));
        }

        private void OnUndoRedoPerformed()
        {
            InvalidateCachedData(null);
            RefreshView();
        }

        private static Dictionary<int, List<ValidationResult>> CloneValidationResults(
            Dictionary<int, List<ValidationResult>> source)
        {
            var clone = new Dictionary<int, List<ValidationResult>>();
            if (source == null) return clone;

            foreach (var pair in source) clone[pair.Key] = new List<ValidationResult>(pair.Value);
            return clone;
        }

        private static Dictionary<int, List<ValidationResult>> CombineValidationResults(
            Dictionary<int, List<ValidationResult>> collectionResults,
            Dictionary<int, List<ValidationResult>> fieldResults)
        {
            var combined = CloneValidationResults(collectionResults);
            foreach (var pair in fieldResults ?? new Dictionary<int, List<ValidationResult>>())
            {
                if (!combined.TryGetValue(pair.Key, out var list))
                {
                    list = new List<ValidationResult>();
                    combined[pair.Key] = list;
                }

                list.AddRange(pair.Value);
            }

            return combined;
        }

        private static int GetContainerCacheKey(IGameDataContainer container)
        {
            return container is UnityEngine.Object unityObject
                ? unityObject.GetInstanceID()
                : container.GetHashCode();
        }

        /// <summary>
        ///     Polls the loaded SO's dirty state each editor frame and reflects it in the
        ///     window title as a bullet dot when unsaved changes exist.
        /// </summary>
        private void UpdateDirtyIndicator()
        {
            var so = _container as ScriptableObject;
            var dirty = so != null && EditorUtility.IsDirty(so);
            var title = dirty ? "● Game Data Spreadsheet Editor" : "Game Data Spreadsheet Editor";
            if (titleContent.text != title) titleContent.text = title;
        }

        private void ToggleBrowser()
        {
            _browserOpen = !_browserOpen;
            var display = _browserOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _browserPanel.style.display = display;
            _browserResizeHandle.style.display = display;
            if (_browserOpen)
            {
                _browserPanel.Refresh();
                _browserPanel.SetActiveContainer(_container as ScriptableObject);
            }
        }

        // ── Toolbar callbacks ──────────────────────────────────────────────────────

        private void ImportJsonAndRefresh()
        {
            GameDataService.ImportFromJson(_container);
            InvalidateCachedData(_container);
            RefreshView();
        }

        private void ImportCsvAndRefresh()
        {
            GameDataService.ImportFromCsv(_container);
            InvalidateCachedData(_container);
            RefreshView();
        }

        // ── Table callbacks ────────────────────────────────────────────────────────

        private void OnEntryChanged(int index, IGameData updated)
        {
            GameDataService.UpdateEntry(_container, index, updated);
            InvalidateValidationCache(_container);
            StartAffectedRowsValidation(new List<int> { index }, true, true);
            _tableView.UpdateStats();
        }

        private void OnAddEntry()
        {
            GameDataService.AddEntry(_container);
            InvalidateValidationCache(_container);

            if (!_tableView.TryAppendLatestEntry())
            {
                InvalidateCachedData(_container);
                RefreshView();
                return;
            }

            _selectionBar.UpdateInfo(_container);
            if (_sheetsPanel != null) _sheetsPanel.SetContainer(_container);
            StartAffectedRowsValidation(new List<int> { _container.GetEntries().Count - 1 }, true, true);
        }

        private void OnRemoveEntries(List<int> indices)
        {
            var removedRows = GetValidUniqueRows(indices, _container?.GetEntries().Count ?? 0);
            GameDataService.RemoveEntries(_container, indices);
            InvalidateValidationCache(_container);

            if (!_tableView.TryRemoveEntries(indices))
            {
                InvalidateCachedData(_container);
                RefreshView();
                return;
            }

            _selectionBar.UpdateInfo(_container);
            if (_sheetsPanel != null) _sheetsPanel.SetContainer(_container);
            RemapValidationAfterRemovals(removedRows);
            StartAffectedRowsValidation(null, true, true);
        }

        private void OnDuplicateEntries(List<int> indices)
        {
            var sourceRows = GetValidUniqueRows(indices, _container?.GetEntries().Count ?? 0);
            var insertionRows = new List<int>();
            for (var i = 0; i < sourceRows.Count; i++) insertionRows.Add(sourceRows[i] + 1 + i);

            GameDataService.DuplicateEntries(_container, indices);
            InvalidateValidationCache(_container);

            if (!_tableView.TryDuplicateEntries(indices))
            {
                InvalidateCachedData(_container);
                RefreshView();
                return;
            }

            _selectionBar.UpdateInfo(_container);
            if (_sheetsPanel != null) _sheetsPanel.SetContainer(_container);
            RemapValidationAfterInsertions(insertionRows);
            StartAffectedRowsValidation(insertionRows, true, true);
        }

        private void OnMoveEntry(int fromIndex, int insertBefore)
        {
            GameDataService.MoveEntry(_container, fromIndex, insertBefore);
            InvalidateValidationCache(_container);

            if (!_tableView.TryMoveEntry(fromIndex, insertBefore))
            {
                InvalidateCachedData(_container);
                RefreshView();
                return;
            }

            _selectionBar.UpdateInfo(_container);
            if (_sheetsPanel != null) _sheetsPanel.SetContainer(_container);
            RemapValidationAfterMove(fromIndex, insertBefore);
            StartAffectedRowsValidation(null, true, true);
        }
    }
}
