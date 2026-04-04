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
    /// Orchestrates:
    ///   - Asset selection / creation (<see cref="GameDataSelectionBar"/>)
    ///   - Search / filter toolbar
    ///   - Table view (<see cref="GameDataTableView"/>)
    ///   - Validation feedback via <see cref="GameDataValidationService"/>
    ///   - JSON import / export
    /// </summary>
    public class LiveGameDataEditorWindow : EditorWindow
    {
        private IGameDataContainer   _container;
        private GameDataSelectionBar _selectionBar;
        private GameDataTableView    _tableView;
        private VisualElement        _emptyState;
        private VisualElement        _contentArea;

        // Toolbar filter state
        private string _searchText  = string.Empty;
        private bool   _enabledOnly = false;

        private void OnEnable()
        {
            Undo.undoRedoPerformed += RefreshView;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= RefreshView;
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
            BuildEmptyState();
            BuildContentArea();
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
                RefreshView();
            };
            _selectionBar.OnContainerCleared += () =>
            {
                _container = null;
                RefreshView();
            };
            rootVisualElement.Add(_selectionBar);
        }

        // ── Toolbar (search / filter / JSON) ───────────────────────────────────────

        private void BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("toolbar");

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
                "No data asset selected.\n" +
                "Use the picker above to select an existing asset, or create a new one.");
            label.AddToClassList("empty-state-label");

            var createBtn = new Button(() => _selectionBar.TriggerCreateNew())
                { text = "＋ Create New Data Asset" };
            createBtn.AddToClassList("create-btn");

            _emptyState.Add(label);
            _emptyState.Add(createBtn);
            rootVisualElement.Add(_emptyState);
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
                onEntryChanged:  OnEntryChanged,
                onAddEntry:      OnAddEntry,
                onRemoveEntries: OnRemoveEntries);

            _contentArea.Add(_tableView);
            rootVisualElement.Add(_contentArea);
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

        // ── Toolbar callbacks ──────────────────────────────────────────────────────

        private void ImportAndRefresh()
        {
            GameDataService.ImportFromJson(_container);
            RefreshView();
        }

        // ── Table callbacks ────────────────────────────────────────────────────────

        private void OnEntryChanged(int index, IGameDataEntry updated)
        {
            GameDataService.UpdateEntry(_container, index, updated);
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
    }
}
