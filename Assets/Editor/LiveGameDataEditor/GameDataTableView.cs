using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Renders a scrollable table of <see cref="GameDataEntry"/> rows.
    /// Owns: column headers, ScrollView of rows, Add/Remove footer, multi-row selection.
    /// </summary>
    public class GameDataTableView : VisualElement
    {
        // Callbacks wired by the EditorWindow
        private readonly Action<int, GameDataEntry> _onEntryChanged;  // (index, newEntry)
        private readonly Action _onAddEntry;
        private readonly Action<List<int>> _onRemoveEntries;           // (selectedIndices)

        private GameDataContainer _container;
        private ScrollView _scrollView;
        private readonly List<GameDataRowView> _rows = new();
        private readonly List<int> _selectedIndices = new();

        public GameDataTableView(
            Action<int, GameDataEntry> onEntryChanged,
            Action onAddEntry,
            Action<List<int>> onRemoveEntries)
        {
            _onEntryChanged = onEntryChanged;
            _onAddEntry = onAddEntry;
            _onRemoveEntries = onRemoveEntries;

            AddToClassList("table-view");
            style.flexGrow = 1;

            BuildHeader();
            BuildScrollView();
            BuildFooter();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>Rebuilds all rows from the container. Clears selection.</summary>
        public void Populate(GameDataContainer container)
        {
            _container = container;
            _selectedIndices.Clear();
            _rows.Clear();
            _scrollView.Clear();

            if (container == null) return;

            for (int i = 0; i < container.Entries.Count; i++)
            {
                int capturedIndex = i;
                var row = new GameDataRowView(container.Entries[i], i % 2 == 1);

                row.OnEntryChanged += (updatedEntry) =>
                    _onEntryChanged?.Invoke(capturedIndex, updatedEntry);

                row.OnSelectionToggled += (isMultiSelect) =>
                    HandleRowSelection(capturedIndex, isMultiSelect);

                _rows.Add(row);
                _scrollView.Add(row);
            }
        }

        // ── UI construction ────────────────────────────────────────────────────────

        private void BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("table-header");

            // Gutter placeholder so header aligns with row gutters
            var gutter = new VisualElement();
            gutter.AddToClassList("col-gutter");
            header.Add(gutter);

            foreach (string col in new[] { "Id", "Value", "Multiplier", "Enabled" })
            {
                var label = new Label(col);
                label.AddToClassList("col-header");
                label.AddToClassList($"col-{col.ToLower()}");
                header.Add(label);
            }

            Add(header);
        }

        private void BuildScrollView()
        {
            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.AddToClassList("table-scroll");
            _scrollView.style.flexGrow = 1;
            Add(_scrollView);
        }

        private void BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("table-footer");

            var addBtn = new Button(() => _onAddEntry?.Invoke()) { text = "+ Add Row" };
            addBtn.AddToClassList("footer-button");

            var removeBtn = new Button(RemoveSelected) { text = "− Remove Selected" };
            removeBtn.AddToClassList("footer-button");
            removeBtn.AddToClassList("footer-button--danger");

            footer.Add(addBtn);
            footer.Add(removeBtn);
            Add(footer);
        }

        // ── Selection logic ────────────────────────────────────────────────────────

        private void HandleRowSelection(int index, bool isMultiSelect)
        {
            if (!isMultiSelect)
            {
                // Single click: deselect all others
                foreach (int i in _selectedIndices)
                {
                    if (i < _rows.Count) _rows[i].SetSelected(false);
                }
                _selectedIndices.Clear();
            }

            // Toggle clicked row
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
        }

        private void RemoveSelected()
        {
            if (_selectedIndices.Count == 0) return;
            _onRemoveEntries?.Invoke(new List<int>(_selectedIndices));
        }
    }
}
