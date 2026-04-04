using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Main editor window for the Live Game Data Editor.
    /// Open via: Tools > Game Data Editor
    ///
    /// Orchestrates:
    ///   - Asset picker and creation
    ///   - Search / filter toolbar
    ///   - Table view (<see cref="GameDataTableView"/>)
    ///   - Bulk edit panel (<see cref="GameDataBulkEditPanel"/>)
    ///   - Validation feedback via <see cref="GameDataValidationService"/>
    ///   - JSON import / export
    /// </summary>
    public class LiveGameDataEditorWindow : EditorWindow
    {
        private GameDataContainer     _container;
        private GameDataTableView     _tableView;
        private GameDataBulkEditPanel _bulkEditPanel;
        private VisualElement         _emptyState;
        private VisualElement         _contentArea;
        private ObjectField           _containerField;

        // Toolbar filter state
        private string _searchText  = string.Empty;
        private bool   _enabledOnly = false;

        // Latest selection from the table (kept in sync via event)
        private List<int> _currentSelection = new();

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

            BuildToolbar();
            BuildEmptyState();
            BuildContentArea();
            RefreshView();
        }

        // ── Toolbar ────────────────────────────────────────────────────────────────

        private void BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");

            // Asset picker
            _containerField = new ObjectField("Data Asset")
            {
                objectType        = typeof(GameDataContainer),
                allowSceneObjects = false,
                value             = _container
            };
            _containerField.style.flexGrow = 1;
            _containerField.RegisterValueChangedCallback(evt =>
            {
                _container   = evt.newValue as GameDataContainer;
                _searchText  = string.Empty;
                _enabledOnly = false;
                RefreshView();
            });
            toolbar.Add(_containerField);

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

            var exportBtn = new Button(() => GameDataService.ExportToJson(_container))
                { text = "Export JSON" };
            exportBtn.AddToClassList("toolbar-button");

            var importBtn = new Button(ImportAndRefresh) { text = "Import JSON" };
            importBtn.AddToClassList("toolbar-button");

            toolbar.Add(exportBtn);
            toolbar.Add(importBtn);
            rootVisualElement.Add(toolbar);
        }

        // ── Empty state ────────────────────────────────────────────────────────────

        private void BuildEmptyState()
        {
            _emptyState = new VisualElement();
            _emptyState.AddToClassList("empty-state");

            var label = new Label(
                "No Game Data Container selected.\n" +
                "Select an existing asset above, or create a new one.");
            label.AddToClassList("empty-state-label");

            var createBtn = new Button(CreateNewAsset) { text = "＋ Create New Data Asset" };
            createBtn.AddToClassList("create-btn");

            _emptyState.Add(label);
            _emptyState.Add(createBtn);
            rootVisualElement.Add(_emptyState);
        }

        // Data type subtitle label — populated from GameDataAttribute when a container is loaded
        private Label _dataTypeLabel;

        // ── Content area ───────────────────────────────────────────────────────────

        private void BuildContentArea()
        {
            _contentArea = new VisualElement();
            _contentArea.AddToClassList("content-area");
            _contentArea.style.flexGrow      = 1;
            _contentArea.style.flexDirection = FlexDirection.Column;

            // Subtitle showing the entry type's display name (from GameDataAttribute)
            _dataTypeLabel = new Label(string.Empty);
            _dataTypeLabel.AddToClassList("data-type-label");
            _contentArea.Add(_dataTypeLabel);

            _tableView = new GameDataTableView(
                onEntryChanged:  OnEntryChanged,
                onAddEntry:      OnAddEntry,
                onRemoveEntries: OnRemoveEntries);
            _tableView.OnSelectionChanged += HandleSelectionChanged;

            _bulkEditPanel = new GameDataBulkEditPanel();
            _bulkEditPanel.OnBulkApply  += HandleBulkApply;
            _bulkEditPanel.style.display = DisplayStyle.None;

            _contentArea.Add(_tableView);
            _contentArea.Add(_bulkEditPanel);
            rootVisualElement.Add(_contentArea);
        }

        // ── View management ────────────────────────────────────────────────────────

        private void RefreshView()
        {
            bool has = _container != null;
            _emptyState.style.display  = has ? DisplayStyle.None : DisplayStyle.Flex;
            _contentArea.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;

            if (!has) return;

            // Resolve the display name from GameDataAttribute, falling back to the type name
            var entryType = _container.EntryType;
            var attr = entryType.GetCustomAttributes(typeof(GameDataAttribute), inherit: false);
            string displayName = attr.Length > 0 && !string.IsNullOrEmpty(((GameDataAttribute)attr[0]).DisplayName)
                ? ((GameDataAttribute)attr[0]).DisplayName
                : entryType.Name;
            _dataTypeLabel.text = displayName;

            _tableView.Populate(_container);
            _tableView.SetFilter(_searchText, _enabledOnly);
            _bulkEditPanel.style.display = DisplayStyle.None;
            _currentSelection.Clear();
            RunValidation();
        }

        private void RunValidation()
        {
            if (_container == null) return;
            var results = GameDataValidationService.RunAll(_container);
            _tableView.ApplyValidation(results);
        }

        // ── Toolbar callbacks ──────────────────────────────────────────────────────

        private void CreateNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Game Data Container",
                "NewGameData", "asset",
                "Choose a location for the new GameDataContainer asset.");
            if (string.IsNullOrEmpty(path)) return;

            _container = GameDataService.CreateNewContainer(path);
            _containerField.SetValueWithoutNotify(_container);
            RefreshView();
        }

        private void ImportAndRefresh()
        {
            GameDataService.ImportFromJson(_container);
            RefreshView();
        }

        // ── Table callbacks ────────────────────────────────────────────────────────

        private void OnEntryChanged(int index, IGameDataEntry updated)
        {
            GameDataService.UpdateEntry(_container, index, (GameDataEntry)updated);
            RunValidation();
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

        private void HandleSelectionChanged(List<int> indices)
        {
            _currentSelection = indices;

            bool showBulk = indices.Count >= 2;
            _bulkEditPanel.style.display = showBulk ? DisplayStyle.Flex : DisplayStyle.None;
            if (showBulk)
                _bulkEditPanel.SetSelectionCount(indices.Count);
        }

        // ── Bulk edit callback ─────────────────────────────────────────────────────

        private void HandleBulkApply(Action<GameDataEntry> applyAction, string undoName)
        {
            if (_container == null || _currentSelection.Count == 0) return;
            GameDataService.BulkUpdateEntries(_container, _currentSelection, applyAction, undoName);
            RefreshView();
        }
    }
}
