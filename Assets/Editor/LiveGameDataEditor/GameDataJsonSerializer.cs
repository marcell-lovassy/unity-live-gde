using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// JSON serializer for any <see cref="IGameDataContainer"/> using
    /// <c>Newtonsoft.Json</c> (<c>com.unity.nuget.newtonsoft-json</c>).
    ///
    /// Public fields are serialized by default — no custom contract resolver required.
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
        /// <summary>Shared default instance.</summary>
        public static GameDataJsonSerializer Default { get; } = new GameDataJsonSerializer();

        // ── IGameDataSerializer ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public string Serialize(IGameDataContainer container, bool indented = true)
        {
            if (container == null) return "{}";

            var entries = container.GetEntries();
            var entriesArray = new JArray();
            foreach (var entry in entries)
                entriesArray.Add(JToken.FromObject(entry));

            var root = new JObject
            {
                ["entryType"] = container.EntryType.FullName,
                ["entries"]   = entriesArray,
            };

            return root.ToString(indented ? Formatting.Indented : Formatting.None);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The caller must call <c>Undo.RecordObject</c> and <c>EditorUtility.SetDirty</c>
        /// around this method — this class does not depend on UnityEditor APIs.
        /// </remarks>
        public void Deserialize(string json, IGameDataContainer container)
        {
            if (string.IsNullOrWhiteSpace(json) || container == null) return;

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                Debug.LogError($"[LiveGameDataEditor] JSON parse error: {ex.Message}");
                return;
            }

            // Warn (don't block) if the file's entry type doesn't match the target container.
            string storedType = root["entryType"]?.Value<string>();
            if (!string.IsNullOrEmpty(storedType) && storedType != container.EntryType.FullName)
            {
                Debug.LogWarning(
                    $"[LiveGameDataEditor] Import type mismatch: file contains '{storedType}', " +
                    $"container expects '{container.EntryType.FullName}'. " +
                    "Attempting import anyway — unrecognised fields will be ignored.");
            }

            // Support both the current "entries" key and the legacy "Entries" key.
            var entriesNode = (root["entries"] ?? root["Entries"]) as JArray;
            if (entriesNode == null)
            {
                Debug.LogError("[LiveGameDataEditor] JSON does not contain an 'entries' or 'Entries' array.");
                return;
            }

            var targetList = container.GetEntries();
            targetList.Clear();

            foreach (var token in entriesNode)
            {
                try
                {
                    var entry = (IGameDataEntry)token.ToObject(container.EntryType);
                    if (entry != null)
                        targetList.Add(entry);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LiveGameDataEditor] Failed to deserialize entry: {ex.Message}");
                }
            }
        }
    }
}

