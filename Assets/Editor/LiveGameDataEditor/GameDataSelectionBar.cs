using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Top-of-window bar that shows the currently loaded data asset and provides
    /// "Create New…" and "✕ Close" controls.
    ///
    /// Asset selection is handled by the <see cref="GameDataBrowserPanel"/> sidebar.
    /// This bar is purely informational — it shows what is loaded and lets you close it.
    ///
    /// Layout (left → right):
    ///   [Asset name label]  [Asset path label (muted)]  [✕]  [Create New…]
    /// </summary>
    public class GameDataSelectionBar : VisualElement
    {
        /// <summary>Raised when a valid IGameDataContainer is selected or newly created.</summary>
        public event Action<IGameDataContainer> OnContainerSelected;

        /// <summary>Raised when the user clicks ✕ to unload the current container.</summary>
        public event Action OnContainerCleared;

        private Label _nameLabel;
        private Label _pathLabel;
        private Button _clearBtn;

        // The currently loaded SO (null when nothing is selected)
        private ScriptableObject _current;

        public GameDataSelectionBar()
        {
            AddToClassList("selection-bar");
            Build();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Updates the info label row count. Call after the table is populated.
        /// </summary>
        public void UpdateInfo(IGameDataContainer container)
        {
            if (container == null)
            {
                _nameLabel.text = "No asset loaded";
                _pathLabel.text = "Use ☰ Browse or Create New to get started";
                _clearBtn.style.display = DisplayStyle.None;
                return;
            }
            int count       = container.GetEntries()?.Count ?? 0;
            string typeName = GameDataTypeRegistry.GetEntryDisplayName(container.EntryType);
            _nameLabel.text = $"{(container as ScriptableObject)?.name ?? typeName}";
            _pathLabel.text = $"{typeName}  ·  {count} row{(count == 1 ? "" : "s")}  ·  " +
                              AssetDatabase.GetAssetPath(container as UnityEngine.Object);
            _clearBtn.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// Loads <paramref name="so"/> as the current container, firing
        /// <see cref="OnContainerSelected"/> if it is a valid <see cref="IGameDataContainer"/>.
        /// </summary>
        public void SelectContainer(ScriptableObject so)
        {
            if (so == null)
            {
                _current = null;
                UpdateInfo(null);
                OnContainerCleared?.Invoke();
                return;
            }

            if (so is not IGameDataContainer container)
            {
                EditorUtility.DisplayDialog(
                    "Invalid Data Asset",
                    $"'{so.name}' does not implement IGameDataContainer.\n\n" +
                    "Containers must extend GameDataContainerBase<T>.",
                    "OK");
                return;
            }

            _current = so;
            UpdateInfo(container);
            OnContainerSelected?.Invoke(container);
        }

        /// <summary>Programmatically opens the "Create New…" context menu.</summary>
        public void TriggerCreateNew() => OnCreateNewClicked();

        // ── UI construction ────────────────────────────────────────────────────────

        private void Build()
        {
            // Asset name — shown bold and large
            _nameLabel = new Label("No asset loaded");
            _nameLabel.AddToClassList("asset-name-label");
            Add(_nameLabel);

            // Path / type info — muted subtitle
            _pathLabel = new Label("Use ☰ Browse or Create New to get started");
            _pathLabel.AddToClassList("asset-path-label");
            Add(_pathLabel);

            // Spacer to push the buttons to the right
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            Add(spacer);

            // Clear button — only visible when an asset is loaded
            _clearBtn = new Button(OnClearClicked) { text = "✕" };
            _clearBtn.AddToClassList("clear-btn");
            _clearBtn.tooltip = "Close this asset";
            _clearBtn.style.display = DisplayStyle.None;
            Add(_clearBtn);

            var createBtn = new Button(OnCreateNewClicked) { text = "Create New…" };
            createBtn.AddToClassList("create-new-btn");
            Add(createBtn);
        }

        // ── Callbacks ──────────────────────────────────────────────────────────────

        private void OnClearClicked()
        {
            _current = null;
            UpdateInfo(null);
            OnContainerCleared?.Invoke();
        }

        private void OnCreateNewClicked()
        {
            var entryTypes = GameDataTypeRegistry.GetEntryTypes();
            if (entryTypes.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Entry Types Found",
                    "No [Serializable] classes implementing IGameDataEntry were found.\n\n" +
                    "Create your entry type class before using this tool.",
                    "OK");
                return;
            }

            var menu = new GenericMenu();
            foreach (var entryType in entryTypes)
            {
                var type  = entryType;
                string label = GameDataTypeRegistry.GetEntryDisplayName(type);
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    var container = GameDataAssetFactory.CreateForEntryType(type);
                    if (container == null) return;

                    _current = container as ScriptableObject;
                    UpdateInfo(container);
                    OnContainerSelected?.Invoke(container);
                });
            }
            menu.ShowAsContext();
        }
    }
}
