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
    /// </summary>
    public class LiveGameDataEditorWindow : EditorWindow
    {
        private GameDataContainer _container;
        private GameDataTableView _tableView;
        private VisualElement _emptyState;
        private VisualElement _contentArea;
        private ObjectField _containerField;

        [MenuItem("Tools/Game Data Editor", priority = 100)]
        public static void OpenWindow()
        {
            var window = GetWindow<LiveGameDataEditorWindow>();
            window.titleContent = new GUIContent("Game Data Editor", EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
            window.minSize = new Vector2(620, 400);
        }

        public void CreateGUI()
        {
            // Apply stylesheet if available
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Editor/LiveGameDataEditor/LiveGameDataEditor.uss");
            if (uss != null)
                rootVisualElement.styleSheets.Add(uss);

            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.flexGrow = 1;

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
                objectType = typeof(GameDataContainer),
                allowSceneObjects = false,
                value = _container
            };
            _containerField.style.flexGrow = 1;
            _containerField.RegisterValueChangedCallback(evt =>
            {
                _container = evt.newValue as GameDataContainer;
                RefreshView();
            });
            toolbar.Add(_containerField);

            var exportBtn = new Button(() => GameDataService.ExportToJson(_container))
                { text = "Export JSON" };
            exportBtn.AddToClassList("toolbar-button");

            var importBtn = new Button(ImportAndRefresh)
                { text = "Import JSON" };
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

        // ── Content area ───────────────────────────────────────────────────────────

        private void BuildContentArea()
        {
            _contentArea = new VisualElement();
            _contentArea.AddToClassList("content-area");
            _contentArea.style.flexGrow = 1;

            _tableView = new GameDataTableView(
                onEntryChanged: OnEntryChanged,
                onAddEntry: OnAddEntry,
                onRemoveEntries: OnRemoveEntries);

            _contentArea.Add(_tableView);
            rootVisualElement.Add(_contentArea);
        }

        // ── View refresh ───────────────────────────────────────────────────────────

        private void RefreshView()
        {
            bool hasContainer = _container != null;
            _emptyState.style.display = hasContainer ? DisplayStyle.None : DisplayStyle.Flex;
            _contentArea.style.display = hasContainer ? DisplayStyle.Flex : DisplayStyle.None;

            if (hasContainer)
                _tableView.Populate(_container);
        }

        // ── Callbacks ──────────────────────────────────────────────────────────────

        private void CreateNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Game Data Container",
                "NewGameData",
                "asset",
                "Choose a location for the new GameDataContainer asset.");

            if (string.IsNullOrEmpty(path)) return;

            _container = GameDataService.CreateNewContainer(path);
            _containerField.SetValueWithoutNotify(_container);
            RefreshView();
        }

        private void ImportAndRefresh()
        {
            GameDataService.ImportFromJson(_container);
            _tableView.Populate(_container);
        }

        private void OnEntryChanged(int index, GameDataEntry updatedEntry)
        {
            // GameDataService handles Undo.RecordObject before committing the change
            GameDataService.UpdateEntry(_container, index, updatedEntry);
        }

        private void OnAddEntry()
        {
            GameDataService.AddEntry(_container);
            _tableView.Populate(_container);
        }

        private void OnRemoveEntries(List<int> indices)
        {
            GameDataService.RemoveEntries(_container, indices);
            _tableView.Populate(_container);
        }
    }
}
