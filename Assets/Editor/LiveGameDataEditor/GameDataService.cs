using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Service layer: handles asset creation, Undo-aware mutations, and JSON import/export.
    /// All methods that modify data call Undo.RecordObject before making changes.
    /// </summary>
    public static class GameDataService
    {
        /// <summary>
        /// Future hook: raised after data is imported (e.g., from Google Sheets sync).
        /// Subscribe to react to bulk data replacements.
        /// </summary>
        public static event Action<GameDataContainer> OnDataImported;

        // ── Asset creation ─────────────────────────────────────────────────────────

        /// <summary>Creates a new GameDataContainer asset at <paramref name="assetPath"/>.</summary>
        public static GameDataContainer CreateNewContainer(string assetPath)
        {
            var container = ScriptableObject.CreateInstance<GameDataContainer>();
            AssetDatabase.CreateAsset(container, assetPath);
            AssetDatabase.SaveAssets();
            return container;
        }

        // ── Dirty tracking ─────────────────────────────────────────────────────────

        /// <summary>Marks the container dirty so Unity saves it at the next save operation.</summary>
        public static void MarkDirty(GameDataContainer container)
        {
            if (container != null)
                EditorUtility.SetDirty(container);
        }

        // ── Undo-aware mutations ────────────────────────────────────────────────────

        /// <summary>Adds a default entry, recording an Undo operation first.</summary>
        public static void AddEntry(GameDataContainer container)
        {
            if (container == null) return;
            Undo.RecordObject(container, "Add Game Data Entry");
            container.Entries.Add(new GameDataEntry());
            MarkDirty(container);
        }

        /// <summary>
        /// Removes entries at the given indices with a single grouped Undo operation.
        /// Indices are sorted in reverse so earlier removals don't shift later ones.
        /// </summary>
        public static void RemoveEntries(GameDataContainer container, List<int> indices)
        {
            if (container == null || indices == null || indices.Count == 0) return;

            Undo.RecordObject(container, indices.Count == 1
                ? "Remove Game Data Entry"
                : $"Remove {indices.Count} Game Data Entries");

            var sorted = new List<int>(indices);
            sorted.Sort();
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                int idx = sorted[i];
                if (idx >= 0 && idx < container.Entries.Count)
                    container.Entries.RemoveAt(idx);
            }

            MarkDirty(container);
        }

        /// <summary>
        /// Records Undo and replaces the entry at <paramref name="index"/> with
        /// <paramref name="updated"/>. Called by the window on every field change.
        /// </summary>
        public static void UpdateEntry(GameDataContainer container, int index, GameDataEntry updated)
        {
            if (container == null || index < 0 || index >= container.Entries.Count) return;
            Undo.RecordObject(container, "Edit Game Data Entry");
            container.Entries[index] = updated;
            MarkDirty(container);
        }

        // ── JSON import / export ───────────────────────────────────────────────────

        /// <summary>Exports all entries to a user-chosen JSON file.</summary>
        public static void ExportToJson(GameDataContainer container)
            => ExportToJson((IGameDataContainer)container);

        /// <summary>
        /// Imports entries from a user-chosen JSON file, overwriting current data.
        /// The replacement is wrapped in a single Undo operation.
        /// </summary>
        public static void ImportFromJson(GameDataContainer container)
            => ImportFromJson((IGameDataContainer)container);

        // ── Future validation hook ─────────────────────────────────────────────────

        /// <summary>
        /// Stub: validate a single entry. Pass an <see cref="IDataValidator"/> implementation
        /// when per-entry runtime validation is needed; currently a no-op.
        /// For collection-level / cross-entry validation, use <see cref="GameDataValidationService"/>.
        /// </summary>
        public static bool OnValidateEntry(IGameDataEntry entry, IDataValidator validator = null)
        {
            if (validator == null) return true;
            return validator.Validate(entry, out _);
        }

        // ── IGameDataContainer generic overloads ───────────────────────────────────
        // These overloads accept any IGameDataContainer and use IList for data access,
        // making the service compatible with user-defined container types.

        /// <summary>
        /// Adds a default entry to <paramref name="container"/> using
        /// <see cref="Activator.CreateInstance"/> on its declared entry type.
        /// </summary>
        public static void AddEntry(IGameDataContainer container)
        {
            var so = GetScriptableObject(container);
            if (so == null) return;

            Undo.RecordObject(so, "Add Game Data Entry");
            var entry = (IGameDataEntry)Activator.CreateInstance(container.EntryType);
            container.GetEntries().Add(entry);
            EditorUtility.SetDirty(so);
        }

        /// <summary>Removes entries at the given indices with a single Undo operation.</summary>
        public static void RemoveEntries(IGameDataContainer container, List<int> indices)
        {
            var so = GetScriptableObject(container);
            if (so == null || indices == null || indices.Count == 0) return;

            Undo.RecordObject(so, indices.Count == 1
                ? "Remove Game Data Entry"
                : $"Remove {indices.Count} Game Data Entries");

            IList entries = container.GetEntries();
            var sorted = new List<int>(indices);
            sorted.Sort();
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                int idx = sorted[i];
                if (idx >= 0 && idx < entries.Count)
                    entries.RemoveAt(idx);
            }
            EditorUtility.SetDirty(so);
        }

        /// <summary>Records Undo and replaces the entry at <paramref name="index"/>.</summary>
        public static void UpdateEntry(IGameDataContainer container, int index, IGameDataEntry updated)
        {
            var so = GetScriptableObject(container);
            if (so == null) return;

            IList entries = container.GetEntries();
            if (index < 0 || index >= entries.Count) return;

            Undo.RecordObject(so, "Edit Game Data Entry");
            entries[index] = updated;
            EditorUtility.SetDirty(so);
        }

        /// <summary>Marks the container dirty. No-op if not a ScriptableObject.</summary>
        public static void MarkDirty(IGameDataContainer container)
        {
            if (container is ScriptableObject so)
                EditorUtility.SetDirty(so);
        }

        // ── Entry duplication ──────────────────────────────────────────────────────

        /// <summary>
        /// Moves the entry at <paramref name="fromIndex"/> so that it appears before the
        /// entry currently at <paramref name="insertBefore"/> (pass <c>entries.Count</c>
        /// to move it to the end). Wrapped in a single Undo operation.
        /// </summary>
        public static void MoveEntry(IGameDataContainer container, int fromIndex, int insertBefore)
        {
            var so = GetScriptableObject(container);
            if (so == null) return;

            IList entries = container.GetEntries();
            if (fromIndex < 0 || fromIndex >= entries.Count) return;

            // Clamp insertBefore to [0, entries.Count]
            insertBefore = Mathf.Clamp(insertBefore, 0, entries.Count);

            // No-op: dropping immediately before or after itself
            if (insertBefore == fromIndex || insertBefore == fromIndex + 1) return;

            Undo.RecordObject(so, "Reorder Game Data Entry");

            var entry = entries[fromIndex];
            entries.RemoveAt(fromIndex);

            // After removal, indices above fromIndex shift down by 1
            int finalIdx = insertBefore > fromIndex ? insertBefore - 1 : insertBefore;
            finalIdx = Mathf.Clamp(finalIdx, 0, entries.Count);
            entries.Insert(finalIdx, entry);

            EditorUtility.SetDirty(so);
        }

        /// <summary>
        /// Creates a deep copy of each entry at the given indices and inserts the clones
        /// immediately after their originals. Wrapped in a single Undo operation.
        /// Handles <c>List&lt;T&gt;</c> and array fields by cloning the collection.
        /// </summary>
        public static void DuplicateEntries(IGameDataContainer container, List<int> indices)
        {
            var so = GetScriptableObject(container);
            if (so == null || indices == null || indices.Count == 0) return;

            Undo.RecordObject(so, indices.Count == 1
                ? "Duplicate Entry"
                : $"Duplicate {indices.Count} Entries");

            IList entries = container.GetEntries();
            var sorted = new List<int>(indices);
            sorted.Sort();

            // Insert in reverse order so earlier insertions don't shift later indices.
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                int idx = sorted[i];
                if (idx < 0 || idx >= entries.Count) continue;
                var clone = CloneEntry((IGameDataEntry)entries[idx], container.EntryType);
                entries.Insert(idx + 1, clone);
            }

            EditorUtility.SetDirty(so);
        }

        private static IGameDataEntry CloneEntry(IGameDataEntry source, Type entryType)
        {
            var clone = (IGameDataEntry)Activator.CreateInstance(entryType);
            foreach (var field in entryType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var val = field.GetValue(source);
                // Deep copy list/array fields so the clone doesn't share references.
                if (val is IList list)
                {
                    var listClone = (IList)Activator.CreateInstance(val.GetType());
                    foreach (var item in list) listClone.Add(item);
                    field.SetValue(clone, listClone);
                }
                else
                {
                    field.SetValue(clone, val);
                }
            }
            return clone;
        }

        /// <summary>
        /// Exports data to a user-chosen JSON file. Works for any <see cref="IGameDataContainer"/>.
        /// </summary>
        public static void ExportToJson(IGameDataContainer container)        {
            if (container == null) return;

            var so = container as ScriptableObject;
            string defaultName = so != null ? so.name : container.EntryType.Name;
            string path = EditorUtility.SaveFilePanel(
                "Export Game Data to JSON",
                Application.dataPath,
                defaultName + ".json",
                "json");

            if (string.IsNullOrEmpty(path)) return;

            string json = GameDataJsonSerializer.Default.Serialize(container, indented: true);
            File.WriteAllText(path, json);

            Debug.Log($"[LiveGameDataEditor] Exported {container.GetEntries().Count} entries to: {path}");
        }

        /// <summary>
        /// Imports entries from a user-chosen JSON file, overwriting current data.
        /// Works for any <see cref="IGameDataContainer"/>. Wrapped in a single Undo operation.
        /// </summary>
        public static void ImportFromJson(IGameDataContainer container)
        {
            var so = GetScriptableObject(container);
            if (so == null) return;

            string path = EditorUtility.OpenFilePanel(
                "Import Game Data from JSON",
                Application.dataPath,
                "json");

            if (string.IsNullOrEmpty(path)) return;

            string json = File.ReadAllText(path);

            Undo.RecordObject(so, "Import Game Data from JSON");
            GameDataJsonSerializer.Default.Deserialize(json, container);
            EditorUtility.SetDirty(so);

            if (container is GameDataContainer gc)
                OnDataImported?.Invoke(gc);

            Debug.Log($"[LiveGameDataEditor] Imported {container.GetEntries().Count} entries from: {path}");
        }

        // ── CSV import / export ────────────────────────────────────────────────────

        /// <summary>Exports data to a user-chosen CSV file.</summary>
        public static void ExportToCsv(IGameDataContainer container)
        {
            if (container == null) return;

            var so = container as ScriptableObject;
            string defaultName = so != null ? so.name : container.EntryType.Name;
            string path = EditorUtility.SaveFilePanel(
                "Export Game Data to CSV",
                Application.dataPath,
                defaultName + ".csv",
                "csv");

            if (string.IsNullOrEmpty(path)) return;

            string csv = GameDataCsvSerializer.Serialize(container);
            File.WriteAllText(path, csv, System.Text.Encoding.UTF8);

            Debug.Log($"[LiveGameDataEditor] Exported {container.GetEntries().Count} rows to CSV: {path}");
        }

        /// <summary>
        /// Imports entries from a user-chosen CSV file, overwriting current data.
        /// Wrapped in a single Undo operation.
        /// </summary>
        public static void ImportFromCsv(IGameDataContainer container)
        {
            var so = GetScriptableObject(container);
            if (so == null) return;

            string path = EditorUtility.OpenFilePanel(
                "Import Game Data from CSV",
                Application.dataPath,
                "csv");

            if (string.IsNullOrEmpty(path)) return;

            string csv = File.ReadAllText(path, System.Text.Encoding.UTF8);

            Undo.RecordObject(so, "Import Game Data from CSV");
            if (!GameDataCsvSerializer.Deserialize(csv, container))
            {
                Debug.LogError("[LiveGameDataEditor] CSV import failed — see previous warnings.");
                return;
            }
            EditorUtility.SetDirty(so);

            Debug.Log($"[LiveGameDataEditor] Imported {container.GetEntries().Count} rows from CSV: {path}");
        }

        // Returns the container cast to ScriptableObject, logging a warning if the cast fails.
        private static ScriptableObject GetScriptableObject(IGameDataContainer container)
        {
            if (container == null) return null;
            var so = container as ScriptableObject;
            if (so == null)
                Debug.LogWarning("[LiveGameDataEditor] Container does not inherit ScriptableObject; changes will not be saved.");
            return so;
        }
    }
}
