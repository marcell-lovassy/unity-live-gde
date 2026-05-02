using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    ///     Sidebar panel that lists every <see cref="IGameDataContainer" /> asset found in the
    ///     project, grouped by entry-type display name.
    ///     Assets are discovered via <see cref="TypeCache" /> + <see cref="AssetDatabase" /> and
    ///     cached until the user presses Refresh or the panel is rebuilt.
    ///     Clicking a row fires <see cref="OnContainerSelected" /> with the selected
    ///     <see cref="ScriptableObject" /> so the EditorWindow can load it.
    /// </summary>
    public class GameDataBrowserPanel : VisualElement
    {
        private ScriptableObject _activeContainer;

        private VisualElement _list;

        public GameDataBrowserPanel()
        {
            AddToClassList("browser-panel");
            Build();
            Refresh();
        }

        /// <summary>Fired when the user clicks a container row.</summary>
        public event Action<ScriptableObject> OnContainerSelected;

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        ///     Highlights the given container as the active selection.
        ///     Does not fire <see cref="OnContainerSelected" />.
        /// </summary>
        public void SetActiveContainer(ScriptableObject container)
        {
            _activeContainer = container;
            RefreshActiveHighlight();
        }

        /// <summary>Re-scans the project for data assets.</summary>
        public void Refresh()
        {
            _list.Clear();
            var groups = FindAllContainers();

            if (groups.Count == 0)
            {
                var empty = new Label("No data assets found.\nCreate one with the picker above.");
                empty.AddToClassList("browser-empty-label");
                _list.Add(empty);
                return;
            }

            foreach (var (groupName, assets) in groups)
            {
                var groupHeader = new Label(groupName);
                groupHeader.AddToClassList("browser-group-header");
                _list.Add(groupHeader);

                foreach (var so in assets)
                {
                    var so2 = so; // capture
                    var btn = new Button(() => OnContainerSelected?.Invoke(so2));
                    btn.userData = so2;
                    btn.AddToClassList("browser-item");
                    if (so == _activeContainer) btn.AddToClassList("browser-item--active");

                    var container = so as IGameDataContainer;
                    var count = container?.GetEntries()?.Count ?? 0;
                    btn.text = $"{so.name}  ({count})";
                    btn.tooltip = AssetDatabase.GetAssetPath(so);
                    _list.Add(btn);
                }
            }
        }

        // ── UI construction ────────────────────────────────────────────────────────

        private void Build()
        {
            // Title bar
            var titleBar = new VisualElement();
            titleBar.AddToClassList("browser-titlebar");

            var title = new Label("Data Browser");
            title.AddToClassList("browser-title");
            titleBar.Add(title);

            var refreshBtn = new Button(Refresh) { text = "↺" };
            refreshBtn.AddToClassList("browser-refresh-btn");
            titleBar.Add(refreshBtn);
            Add(titleBar);

            // Scrollable list
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("browser-scroll");
            scroll.style.flexGrow = 1;
            _list = scroll.contentContainer;
            Add(scroll);
        }

        // ── Asset discovery ────────────────────────────────────────────────────────

        /// <summary>
        ///     Returns all IGameDataContainer ScriptableObject assets in the project,
        ///     grouped by entry-type display name (sorted alphabetically).
        /// </summary>
        private List<(string groupName, List<ScriptableObject> assets)> FindAllContainers()
        {
            // Collect concrete container types via TypeCache (fast, no assembly scanning).
            var containerTypes = TypeCache.GetTypesDerivedFrom<IGameDataContainer>();
            var grouped = new Dictionary<string, List<ScriptableObject>>(StringComparer.Ordinal);

            foreach (var type in containerTypes)
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition) continue;

                // FindAssets by type name is fast and doesn't load the assets until path is resolved.
                var guids = AssetDatabase.FindAssets($"t:{type.Name}");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var so = AssetDatabase.LoadAssetAtPath(path, type) as ScriptableObject;
                    if (so == null || so is not IGameDataContainer container) continue;

                    var group = GameDataTypeRegistry.GetEntryDisplayName(container.EntryType);
                    if (!grouped.TryGetValue(group, out var list))
                    {
                        list = new List<ScriptableObject>();
                        grouped[group] = list;
                    }

                    list.Add(so);
                }
            }

            // Sort groups and items inside each group by asset name.
            var result = new List<(string, List<ScriptableObject>)>(grouped.Count);
            var keys = new List<string>(grouped.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var key in keys)
            {
                grouped[key].Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                result.Add((key, grouped[key]));
            }

            return result;
        }

        private void RefreshActiveHighlight()
        {
            foreach (var child in _list.Children())
            {
                if (child is not Button btn) continue;
                var isActive = btn.userData as ScriptableObject == _activeContainer;
                btn.EnableInClassList("browser-item--active", isActive);
            }
        }
    }
}