using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Main editor window for the Live Game Data Editor.
    /// Open via: Tools > Game Data Editor
    ///
    /// Layout:
    ///   SelectionBar
    ///   Toolbar (search / filter / Browse toggle / JSON / CSV)
    ///   MainArea [horizontal]
    ///     BrowserPanel (220 px, toggleable) | MainContent (EmptyState or ContentArea)
    /// </summary>
    public class LiveGameDataEditorWindow : EditorWindow
    {
        private IGameDataContainer    _container;
        private GameDataSelectionBar  _selectionBar;
        private GameDataTableView     _tableView;
        private GameDataBrowserPanel  _browserPanel;
        private VisualElement         _emptyState;
        private VisualElement         _contentArea;
        private VisualElement         _mainArea;
        private VisualElement         _mainContent;
        private VisualElement         _browserResizeHandle;

        // Toolbar filter state
        private string _searchText  = string.Empty;
        private bool   _enabledOnly = false;
        private bool   _browserOpen = false;

        private void OnEnable()
        {
            Undo.undoRedoPerformed  += RefreshView;
            EditorApplication.update += UpdateDirtyIndicator;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed  -= RefreshView;
            EditorApplication.update -= UpdateDirtyIndicator;
        }

        [MenuItem("Tools/Game Data Editor", priority = 100)]
        public static void OpenWindow()
        {
            var window = GetWindow<LiveGameDataEditorWindow>();
            window.titleContent = new GUIContent(
                "Game Data Editor",
                EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
            window.minSize = new Vector2(700, 420);
        }

        public void CreateGUI()
        {
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Editor/LiveGameDataEditor/LiveGameDataEditor.uss");
            if (uss != null)
                rootVisualElement.styleSheets.Add(uss);

            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.flexGrow      = 1;

            BuildSelectionBar();
            BuildToolbar();
            BuildMainArea();
            RefreshView();
        }

        // ── Selection bar ──────────────────────────────────────────────────────────

        private void BuildSelectionBar()
        {
            _selectionBar = new GameDataSelectionBar();
            _selectionBar.OnContainerSelected += container =>
            {
                _container   = container;
                _searchText  = string.Empty;
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
            var searchField = new TextField { value = _searchText };
            searchField.AddToClassList("search-field");
            searchField.textEdition.placeholder = "Search by Id…";
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
            rootVisualElement.Add(toolbar);
        }

        // ── Main horizontal area (browser | content) ───────────────────────────────

        private void BuildMainArea()
        {
            _mainArea = new VisualElement();
            _mainArea.AddToClassList("main-area");
            _mainArea.style.flexDirection = FlexDirection.Row;
            _mainArea.style.flexGrow      = 1;

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
            _mainContent.style.flexGrow      = 1;
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
            bool  dragging   = false;

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                dragging   = true;
                dragStartX = evt.position.x;
                dragStartW = _browserPanel.resolvedStyle.width;
                handle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });

            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging) return;
                float newW = Mathf.Clamp(dragStartW + (evt.position.x - dragStartX), 120f, 500f);
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

        // Data type subtitle label — populated from GameDataAttribute when a container is loaded.
        private Label _dataTypeLabel;

        // ── Content area ───────────────────────────────────────────────────────────

        private void BuildContentArea()
        {
            _contentArea = new VisualElement();
            _contentArea.AddToClassList("content-area");
            _contentArea.style.flexGrow      = 1;
            _contentArea.style.flexDirection = FlexDirection.Column;

            // Subtitle showing the entry type's friendly display name.
            _dataTypeLabel = new Label(string.Empty);
            _dataTypeLabel.AddToClassList("data-type-label");
            _contentArea.Add(_dataTypeLabel);

            _tableView = new GameDataTableView(
                onEntryChanged:     OnEntryChanged,
                onAddEntry:         OnAddEntry,
                onRemoveEntries:    OnRemoveEntries,
                onDuplicateEntries: OnDuplicateEntries,
                onMoveEntry:        OnMoveEntry);

            _contentArea.Add(_tableView);
            _mainContent.Add(_contentArea);
        }

        // ── View management ────────────────────────────────────────────────────────

        private void RefreshView()
        {
            bool has = _container != null;
            _emptyState.style.display  = has ? DisplayStyle.None : DisplayStyle.Flex;
            _contentArea.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;

            if (!has)
            {
                _selectionBar.UpdateInfo(null);
                return;
            }

            _dataTypeLabel.text = GameDataTypeRegistry.GetEntryDisplayName(_container.EntryType);

            _tableView.Populate(_container);
            _tableView.SetFilter(_searchText, _enabledOnly);
            RunValidation();

            _selectionBar.UpdateInfo(_container);
        }

        private void RunValidation()
        {
            if (_container == null) return;
            var results = GameDataValidationService.RunAll(_container);
            _tableView.ApplyValidation(results);
        }

        /// <summary>
        /// Polls the loaded SO's dirty state each editor frame and reflects it in the
        /// window title as a bullet dot: "● Game Data Editor" when unsaved changes exist.
        /// </summary>
        private void UpdateDirtyIndicator()
        {
            var so    = _container as ScriptableObject;
            bool dirty = so != null && EditorUtility.IsDirty(so);
            string title = dirty ? "● Game Data Editor" : "Game Data Editor";
            if (titleContent.text != title)
                titleContent.text = title;
        }

        private void ToggleBrowser()
        {
            _browserOpen = !_browserOpen;
            var display = _browserOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _browserPanel.style.display          = display;
            _browserResizeHandle.style.display   = display;
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
            RefreshView();
        }

        private void ImportCsvAndRefresh()
        {
            GameDataService.ImportFromCsv(_container);
            RefreshView();
        }

        // ── Table callbacks ────────────────────────────────────────────────────────

        private void OnEntryChanged(int index, IGameDataEntry updated)
        {
            GameDataService.UpdateEntry(_container, index, updated);
            RunValidation();
            _tableView.UpdateStats();
        }

        private void OnAddEntry()
        {
            GameDataService.AddEntry(_container);
            RefreshView();
        }

        private void OnRemoveEntries(List<int> indices)
        {
            GameDataService.RemoveEntries(_container, indices);
            RefreshView();
        }

        private void OnDuplicateEntries(List<int> indices)
        {
            GameDataService.DuplicateEntries(_container, indices);
            RefreshView();
        }

        private void OnMoveEntry(int fromIndex, int insertBefore)
        {
            GameDataService.MoveEntry(_container, fromIndex, insertBefore);
            RefreshView();
        }
    }
}
