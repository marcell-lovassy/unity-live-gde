using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Renders a single <see cref="GameDataEntry"/> as an inline-editable table row.
    /// Does NOT mutate the entry directly; instead it fires <see cref="OnEntryChanged"/>
    /// with a cloned entry so the parent can call Undo.RecordObject before committing.
    /// </summary>
    public class GameDataRowView : VisualElement
    {
        /// <summary>
        /// Raised when any field is edited. The argument is a cloned entry with the new values.
        /// The parent is responsible for Undo.RecordObject + assigning to the container.
        /// </summary>
        public event Action<GameDataEntry> OnEntryChanged;

        /// <summary>
        /// Raised when the row is clicked. Bool = whether Ctrl/Shift/Cmd was held (multi-select).
        /// </summary>
        public event Action<bool> OnSelectionToggled;

        // Local copies of field values — the row never modifies the original entry reference.
        private string _id;
        private int _value;
        private float _multiplier;
        private bool _enabled;

        public GameDataRowView(GameDataEntry entry, bool isAlternateRow)
        {
            // Capture initial values
            _id = entry.Id;
            _value = entry.Value;
            _multiplier = entry.Multiplier;
            _enabled = entry.Enabled;

            AddToClassList("table-row");
            if (isAlternateRow)
                AddToClassList("table-row--alternate");

            // Click anywhere on the row to (de)select it
            RegisterCallback<ClickEvent>(evt =>
                OnSelectionToggled?.Invoke(evt.ctrlKey || evt.shiftKey || evt.commandKey));

            BuildFields();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        public void SetSelected(bool selected)
        {
            EnableInClassList("table-row--selected", selected);
        }

        // ── UI construction ────────────────────────────────────────────────────────

        private void BuildFields()
        {
            // Narrow left gutter used for visual alignment with the header
            var gutter = new VisualElement();
            gutter.AddToClassList("col-gutter");
            Add(gutter);

            // Id ─ TextField
            var idField = new TextField { value = _id };
            idField.AddToClassList("col-id");
            idField.RegisterValueChangedCallback(evt =>
            {
                _id = evt.newValue;
                OnEntryChanged?.Invoke(MakeEntry());
            });
            Add(idField);

            // Value ─ IntegerField
            var valueField = new IntegerField { value = _value };
            valueField.AddToClassList("col-value");
            valueField.RegisterValueChangedCallback(evt =>
            {
                _value = evt.newValue;
                OnEntryChanged?.Invoke(MakeEntry());
            });
            Add(valueField);

            // Multiplier ─ FloatField
            var multiplierField = new FloatField { value = _multiplier };
            multiplierField.AddToClassList("col-multiplier");
            multiplierField.RegisterValueChangedCallback(evt =>
            {
                _multiplier = evt.newValue;
                OnEntryChanged?.Invoke(MakeEntry());
            });
            Add(multiplierField);

            // Enabled ─ Toggle
            var enabledToggle = new Toggle { value = _enabled };
            enabledToggle.AddToClassList("col-enabled");
            enabledToggle.RegisterValueChangedCallback(evt =>
            {
                _enabled = evt.newValue;
                OnEntryChanged?.Invoke(MakeEntry());
            });
            Add(enabledToggle);
        }

        /// <summary>Constructs a new entry from the row's current local field values.</summary>
        private GameDataEntry MakeEntry() => new GameDataEntry
        {
            Id = _id,
            Value = _value,
            Multiplier = _multiplier,
            Enabled = _enabled
        };
    }
}
