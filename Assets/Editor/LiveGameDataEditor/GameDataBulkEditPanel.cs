using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Panel that appears when two or more table rows are selected.
    /// Exposes bulk operations (Set Value, Add to Value, Multiply Multiplier, Set Enabled).
    /// Each Apply button fires <see cref="OnBulkApply"/> with a mutation delegate and undo name;
    /// the caller is responsible for applying the mutation to every selected entry via
    /// <see cref="GameDataService.BulkUpdateEntries"/>.
    /// </summary>
    public class GameDataBulkEditPanel : VisualElement
    {
        /// <summary>
        /// Raised when the user clicks an Apply button.
        /// <c>Action&lt;GameDataEntry&gt;</c> is a mutation that should be applied to each selected entry.
        /// <c>string</c> is the Undo operation name.
        /// </summary>
        public event Action<Action<GameDataEntry>, string> OnBulkApply;

        private Label _titleLabel;

        public GameDataBulkEditPanel()
        {
            AddToClassList("bulk-edit-panel");
            Build();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>Updates the selection count shown in the panel title.</summary>
        public void SetSelectionCount(int count)
        {
            _titleLabel.text = $"Bulk Edit — {count} row{(count == 1 ? "" : "s")} selected";
        }

        // ── UI construction ────────────────────────────────────────────────────────

        private void Build()
        {
            _titleLabel = new Label("Bulk Edit");
            _titleLabel.AddToClassList("bulk-edit-title");
            Add(_titleLabel);

            var fieldsRow = new VisualElement();
            fieldsRow.AddToClassList("bulk-edit-fields");
            Add(fieldsRow);

            // ── Set Value ──────────────────────────────────────────────────────────
            var setValueField = new IntegerField { value = 0 };
            setValueField.AddToClassList("bulk-field");
            fieldsRow.Add(BuildSection("Set Value", setValueField, () =>
            {
                int v = setValueField.value;
                OnBulkApply?.Invoke(e => e.Value = v, "Bulk Set Value");
            }));

            // ── Add to Value ───────────────────────────────────────────────────────
            var addValueField = new IntegerField { value = 0 };
            addValueField.AddToClassList("bulk-field");
            fieldsRow.Add(BuildSection("Add to Value", addValueField, () =>
            {
                int v = addValueField.value;
                OnBulkApply?.Invoke(e => e.Value += v, "Bulk Add to Value");
            }));

            // ── Multiply Multiplier ────────────────────────────────────────────────
            var multiplyField = new FloatField { value = 1f };
            multiplyField.AddToClassList("bulk-field");
            fieldsRow.Add(BuildSection("Multiply Multiplier", multiplyField, () =>
            {
                float v = multiplyField.value;
                OnBulkApply?.Invoke(e => e.Multiplier *= v, "Bulk Multiply Multiplier");
            }));

            // ── Set Enabled ────────────────────────────────────────────────────────
            var enabledToggle = new Toggle { value = true };
            enabledToggle.AddToClassList("bulk-field");
            fieldsRow.Add(BuildSection("Set Enabled", enabledToggle, () =>
            {
                bool v = enabledToggle.value;
                OnBulkApply?.Invoke(e => e.Enabled = v, "Bulk Set Enabled");
            }));
        }

        /// <summary>Builds a labelled section: [Label | Field | Apply button].</summary>
        private static VisualElement BuildSection(string label, VisualElement field, Action onApply)
        {
            var section = new VisualElement();
            section.AddToClassList("bulk-edit-section");

            var lbl = new Label(label);
            lbl.AddToClassList("bulk-edit-section-label");

            var applyBtn = new Button(onApply) { text = "Apply" };
            applyBtn.AddToClassList("bulk-apply-btn");

            section.Add(lbl);
            section.Add(field);
            section.Add(applyBtn);
            return section;
        }
    }
}
