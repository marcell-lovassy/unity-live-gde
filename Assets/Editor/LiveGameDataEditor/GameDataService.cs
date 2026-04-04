using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        {
            if (container == null) return;

            string path = EditorUtility.SaveFilePanel(
                "Export Game Data to JSON",
                Application.dataPath,
                container.name + ".json",
                "json");

            if (string.IsNullOrEmpty(path)) return;

            var wrapper = new GameDataJsonWrapper { Entries = container.Entries };
            string json = JsonUtility.ToJson(wrapper, prettyPrint: true);
            File.WriteAllText(path, json);

            Debug.Log($"[LiveGameDataEditor] Exported {container.Entries.Count} entries to: {path}");
        }

        /// <summary>
        /// Imports entries from a user-chosen JSON file, overwriting current data.
        /// The replacement is wrapped in a single Undo operation.
        /// </summary>
        public static void ImportFromJson(GameDataContainer container)
        {
            if (container == null) return;

            string path = EditorUtility.OpenFilePanel(
                "Import Game Data from JSON",
                Application.dataPath,
                "json");

            if (string.IsNullOrEmpty(path)) return;

            string json = File.ReadAllText(path);
            var wrapper = JsonUtility.FromJson<GameDataJsonWrapper>(json);

            if (wrapper == null)
            {
                EditorUtility.DisplayDialog(
                    "Import Failed",
                    "Could not parse the selected JSON file. Make sure it was exported from this tool.",
                    "OK");
                return;
            }

            Undo.RecordObject(container, "Import Game Data from JSON");
            container.Entries = wrapper.Entries ?? new List<GameDataEntry>();
            MarkDirty(container);

            OnDataImported?.Invoke(container);
            Debug.Log($"[LiveGameDataEditor] Imported {container.Entries.Count} entries from: {path}");
        }

        // ── Bulk mutations ─────────────────────────────────────────────────────────

        /// <summary>
        /// Applies <paramref name="applyAction"/> to every entry at the given indices
        /// using a single Undo operation, then marks the container dirty.
        /// </summary>
        public static void BulkUpdateEntries(
            GameDataContainer container,
            List<int> indices,
            Action<GameDataEntry> applyAction,
            string undoName)
        {
            if (container == null || indices == null || indices.Count == 0 || applyAction == null) return;

            Undo.RecordObject(container, undoName);
            foreach (int i in indices)
            {
                if (i >= 0 && i < container.Entries.Count)
                    applyAction(container.Entries[i]);
            }
            MarkDirty(container);
        }

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

        /// <summary>
        /// Applies <paramref name="applyAction"/> to every entry at <paramref name="indices"/>
        /// under a single Undo operation.
        /// </summary>
        public static void BulkUpdateEntries(
            IGameDataContainer container,
            List<int> indices,
            Action<IGameDataEntry> applyAction,
            string undoName)
        {
            var so = GetScriptableObject(container);
            if (so == null || indices == null || indices.Count == 0 || applyAction == null) return;

            Undo.RecordObject(so, undoName);
            IList entries = container.GetEntries();
            foreach (int i in indices)
            {
                if (i >= 0 && i < entries.Count)
                    applyAction((IGameDataEntry)entries[i]);
            }
            EditorUtility.SetDirty(so);
        }

        /// <summary>Marks the container dirty. No-op if not a ScriptableObject.</summary>
        public static void MarkDirty(IGameDataContainer container)
        {
            if (container is ScriptableObject so)
                EditorUtility.SetDirty(so);
        }

        /// <summary>
        /// Exports data to JSON. Currently only supports <see cref="GameDataContainer"/>;
        /// shows an informational dialog for custom container types.
        /// </summary>
        public static void ExportToJson(IGameDataContainer container)
        {
            if (container is GameDataContainer gc) { ExportToJson(gc); return; }
            if (container == null) return;
            EditorUtility.DisplayDialog(
                "Export Not Supported",
                "JSON export is only supported for the built-in GameDataContainer type.\n\n" +
                "Implement a custom export method for your container type.",
                "OK");
        }

        /// <summary>
        /// Imports JSON data. Currently only supports <see cref="GameDataContainer"/>;
        /// shows an informational dialog for custom container types.
        /// </summary>
        public static void ImportFromJson(IGameDataContainer container)
        {
            if (container is GameDataContainer gc) { ImportFromJson(gc); return; }
            if (container == null) return;
            EditorUtility.DisplayDialog(
                "Import Not Supported",
                "JSON import is only supported for the built-in GameDataContainer type.\n\n" +
                "Implement a custom import method for your container type.",
                "OK");
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

        // ── Private helpers ────────────────────────────────────────────────────────

        /// <summary>Wrapper used by JsonUtility to serialize/deserialize the list.</summary>
        [Serializable]
        private class GameDataJsonWrapper
        {
            public List<GameDataEntry> Entries;
        }
    }
}
