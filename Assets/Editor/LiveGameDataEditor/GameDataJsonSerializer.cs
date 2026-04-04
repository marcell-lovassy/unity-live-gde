using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UnityEngine;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// JSON serializer for any <see cref="IGameDataContainer"/> using
    /// <c>System.Text.Json</c> (available in Unity 2022.3+ via the .NET 6 runtime).
    ///
    /// Output format:
    /// <code>
    /// {
    ///   "entryType": "MyNamespace.EnemyData",
    ///   "entries": [
    ///     { "Id": "enemy_01", "MaxHealth": 100, "Speed": 3.5, "IsFlying": false },
    ///     ...
    ///   ]
    /// }
    /// </code>
    ///
    /// Backward compat: also imports the legacy <c>{"Entries":[...]}</c> format produced
    /// by the old <c>GameDataJsonWrapper</c>-based export.
    /// </summary>
    public class GameDataJsonSerializer : IGameDataSerializer
    {
        // Shared option instances (allocate once).
        private static readonly JsonSerializerOptions _compact = BuildOptions(indented: false);
        private static readonly JsonSerializerOptions _pretty  = BuildOptions(indented: true);

        // ── IGameDataSerializer ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public string Serialize(IGameDataContainer container, bool indented = true)
        {
            if (container == null) return "{}";

            var entries = container.GetEntries();
            var opts    = indented ? _pretty : _compact;

            // Serialize each entry to a JsonNode so we can embed it in the wrapper object.
            var entriesArray = new JsonArray();
            foreach (var entry in entries)
            {
                var node = JsonSerializer.SerializeToNode(entry, entry.GetType(), opts);
                entriesArray.Add(node);
            }

            var wrapper = new JsonObject
            {
                ["entryType"] = JsonValue.Create(container.EntryType.FullName),
                ["entries"]   = entriesArray,
            };

            return wrapper.ToJsonString(opts);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The caller must call <c>Undo.RecordObject</c> and <c>EditorUtility.SetDirty</c>
        /// around this method — this class does not depend on UnityEditor APIs.
        /// </remarks>
        public void Deserialize(string json, IGameDataContainer container)
        {
            if (string.IsNullOrWhiteSpace(json) || container == null) return;

            // Check whether this is the old format first (has "Entries", no "entryType").
            if (IsLegacyFormat(json))
            {
                DeserializeLegacy(json, container);
                return;
            }

            JsonNode root;
            try
            {
                root = JsonNode.Parse(json);
            }
            catch (JsonException ex)
            {
                Debug.LogError($"[LiveGameDataEditor] JSON parse error: {ex.Message}");
                return;
            }

            // Warn if the stored entry type does not match the target container.
            string storedType = root?["entryType"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(storedType) && storedType != container.EntryType.FullName)
            {
                Debug.LogWarning(
                    $"[LiveGameDataEditor] Import type mismatch: file contains '{storedType}', " +
                    $"container expects '{container.EntryType.FullName}'. " +
                    "Attempting import anyway — unrecognised fields will be ignored.");
            }

            var entriesNode = root?["entries"] as JsonArray;
            if (entriesNode == null)
            {
                Debug.LogError("[LiveGameDataEditor] JSON does not contain an 'entries' array.");
                return;
            }

            var targetList = container.GetEntries();
            targetList.Clear();

            foreach (var element in entriesNode)
            {
                if (element == null) continue;
                try
                {
                    var entry = (IGameDataEntry)JsonSerializer.Deserialize(
                        element.ToJsonString(), container.EntryType, _compact);
                    if (entry != null)
                        targetList.Add(entry);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LiveGameDataEditor] Failed to deserialize entry: {ex.Message}");
                }
            }
        }

        // ── Static convenience accessors ───────────────────────────────────────────

        /// <summary>Shared default instance (singleton pattern for service use).</summary>
        public static GameDataJsonSerializer Default { get; } = new GameDataJsonSerializer();

        // ── Backward compatibility ─────────────────────────────────────────────────

        /// <summary>
        /// Detects the legacy <c>{"Entries":[...]}</c> format written by the old
        /// <c>GameDataJsonWrapper</c>-based export.
        /// </summary>
        private static bool IsLegacyFormat(string json)
        {
            // Quick heuristic: look for a root "Entries" key without an "entryType" key.
            return json.Contains("\"Entries\"") && !json.Contains("\"entryType\"");
        }

        /// <summary>
        /// Imports entries from the legacy wrapper format. Fields that do not exist on
        /// <see cref="IGameDataContainer.EntryType"/> are silently ignored.
        /// </summary>
        private static void DeserializeLegacy(string json, IGameDataContainer container)
        {
            JsonNode root;
            try
            {
                root = JsonNode.Parse(json);
            }
            catch (JsonException ex)
            {
                Debug.LogError($"[LiveGameDataEditor] Legacy JSON parse error: {ex.Message}");
                return;
            }

            var entriesNode = root?["Entries"] as JsonArray;
            if (entriesNode == null)
            {
                Debug.LogError("[LiveGameDataEditor] Legacy JSON does not contain an 'Entries' array.");
                return;
            }

            var targetList = container.GetEntries();
            targetList.Clear();

            foreach (var element in entriesNode)
            {
                if (element == null) continue;
                try
                {
                    var entry = (IGameDataEntry)JsonSerializer.Deserialize(
                        element.ToJsonString(), container.EntryType, _compact);
                    if (entry != null)
                        targetList.Add(entry);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LiveGameDataEditor] Failed to deserialize legacy entry: {ex.Message}");
                }
            }

            Debug.Log($"[LiveGameDataEditor] Imported {targetList.Count} entries from legacy format.");
        }

        // ── Options factory ────────────────────────────────────────────────────────

        private static JsonSerializerOptions BuildOptions(bool indented) => new JsonSerializerOptions
        {
            // Serialize and deserialize public fields (Unity uses fields, not properties).
            IncludeFields = true,

            // Accept "id", "Id", "ID" etc. on import — tolerant for hand-edited files.
            PropertyNameCaseInsensitive = true,

            WriteIndented = indented,

            // Skip null values to keep files clean.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }
}
