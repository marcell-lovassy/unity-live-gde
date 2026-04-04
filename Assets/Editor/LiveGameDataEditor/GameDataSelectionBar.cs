using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Top-of-window bar that handles data asset selection and creation.
    ///
    /// Layout (left → right):
    ///   [ObjectField — selects any ScriptableObject, validated as IGameDataContainer]
    ///   [Info label  — "EntryTypeName (N rows)"]
    ///   [Create New… — opens a GenericMenu listing all discovered IGameDataEntry types]
    ///
    /// The <see cref="ObjectField"/> is typed to <see cref="ScriptableObject"/> because
    /// Unity's ObjectField does not support interface types. Validation is performed in
    /// the change callback; invalid assignments are rejected with an error dialog.
    /// </summary>
    public class GameDataSelectionBar : VisualElement
    {
        /// <summary>Raised when a valid IGameDataContainer is selected or newly created.</summary>
        public event Action<IGameDataContainer> OnContainerSelected;

        /// <summary>Raised when the ObjectField is cleared (value set to null).</summary>
        public event Action OnContainerCleared;

        private ObjectField _objectField;
        private Label       _infoLabel;

        public GameDataSelectionBar()
        {
            AddToClassList("selection-bar");
            Build();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Updates the info label row count. Call this after the table is populated
        /// so the count reflects the current data, not a stale snapshot.
        /// </summary>
        public void UpdateInfo(IGameDataContainer container)
        {
            if (container == null)
            {
                _infoLabel.text = string.Empty;
                return;
            }
            int count       = container.GetEntries()?.Count ?? 0;
            string typeName = GameDataTypeRegistry.GetEntryDisplayName(container.EntryType);
            _infoLabel.text = $"{typeName} ({count} row{(count == 1 ? "" : "s")})";
        }

        /// <summary>
        /// Silently sets the ObjectField to <paramref name="obj"/> without triggering
        /// the change callback. Use this when loading a container programmatically.
        /// </summary>
        public void SetValueWithoutNotify(UnityEngine.Object obj)
            => _objectField.SetValueWithoutNotify(obj);

        /// <summary>
        /// Programmatically opens the "Create New…" context menu.
        /// Useful for wiring to other UI elements (e.g. the empty-state button).
        /// </summary>
        public void TriggerCreateNew() => OnCreateNewClicked();

        // ── UI construction ────────────────────────────────────────────────────────

        private void Build()
        {
            // Asset picker — typed to ScriptableObject; validated in the callback.
            _objectField = new ObjectField("Data Asset")
            {
                objectType        = typeof(ScriptableObject),
                allowSceneObjects = false,
            };
            _objectField.style.flexGrow = 1;
            _objectField.RegisterValueChangedCallback(OnObjectFieldChanged);
            Add(_objectField);

            // Compact info label: "EnemyData (12 rows)"
            _infoLabel = new Label();
            _infoLabel.AddToClassList("data-info-label");
            Add(_infoLabel);

            var createBtn = new Button(OnCreateNewClicked) { text = "Create New…" };
            createBtn.AddToClassList("create-new-btn");
            Add(createBtn);
        }

        // ── Callbacks ──────────────────────────────────────────────────────────────

        private void OnObjectFieldChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            var newObj = evt.newValue;

            if (newObj == null)
            {
                _infoLabel.text = string.Empty;
                OnContainerCleared?.Invoke();
                return;
            }

            if (newObj is not IGameDataContainer container)
            {
                // Silently restore the previous value and inform the user.
                _objectField.SetValueWithoutNotify(evt.previousValue);
                EditorUtility.DisplayDialog(
                    "Invalid Data Asset",
                    $"'{newObj.name}' does not implement IGameDataContainer.\n\n" +
                    "Containers must extend GameDataContainerBase<T>.",
                    "OK");
                return;
            }

            UpdateInfo(container);
            OnContainerSelected?.Invoke(container);
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
                var type  = entryType; // capture for lambda closure
                string label = GameDataTypeRegistry.GetEntryDisplayName(type);
                menu.AddItem(new GUIContent(label), false, () =>
                {
                    var container = GameDataAssetFactory.CreateForEntryType(type);
                    if (container == null) return;

                    _objectField.SetValueWithoutNotify(container as UnityEngine.Object);
                    UpdateInfo(container);
                    OnContainerSelected?.Invoke(container);
                });
            }
            menu.ShowAsContext();
        }
    }
}
