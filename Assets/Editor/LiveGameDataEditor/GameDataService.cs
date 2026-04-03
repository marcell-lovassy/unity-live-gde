using System;
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
        /// Stub: validate a single entry. Pass an IDataValidator implementation
        /// when the validation system is added; currently a no-op.
        /// </summary>
        public static bool OnValidateEntry(GameDataEntry entry, IDataValidator validator = null)
        {
            if (validator == null) return true;
            return validator.Validate(entry, out _);
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
