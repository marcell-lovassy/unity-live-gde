using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// JSON serializer for any <see cref="IGameDataContainer"/> using
    /// <c>Newtonsoft.Json</c> (<c>com.unity.nuget.newtonsoft-json</c>).
    ///
    /// Public fields are serialized by default — no custom contract resolver required.
    /// <see cref="UnityEngine.Object"/> subtype fields (Sprite, AudioClip, etc.) are
    /// serialized as <c>{ "__assetPath": "Assets/..." }</c> and restored via
    /// <see cref="AssetDatabase.LoadAssetAtPath"/>.
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
    /// Backward compat: also imports the legacy <c>{"Entries":[...]}</c> format.
    /// </summary>
    public class GameDataJsonSerializer : IGameDataSerializer
    {
        /// <summary>Shared default instance (includes UnityObject converter).</summary>
        public static GameDataJsonSerializer Default { get; } = new GameDataJsonSerializer();

        // Shared Newtonsoft serializer with Unity asset-reference support.
        private static readonly JsonSerializer _serializer = JsonSerializer.Create(
            new JsonSerializerSettings
            {
                Converters = { new UnityObjectJsonConverter() },
            });

        // ── IGameDataSerializer ────────────────────────────────────────────────────

        /// <inheritdoc/>
        public string Serialize(IGameDataContainer container, bool indented = true)
        {
            if (container == null) return "{}";

            var entries      = container.GetEntries();
            var entriesArray = new JArray();
            foreach (var entry in entries)
                entriesArray.Add(JToken.FromObject(entry, _serializer));

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
        /// around this method — this class does not depend on UnityEditor APIs directly.
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

            // Warn (don't block) if the file's entry type doesn't match.
            string storedType = root["entryType"]?.Value<string>();
            if (!string.IsNullOrEmpty(storedType) && storedType != container.EntryType.FullName)
            {
                Debug.LogWarning(
                    $"[LiveGameDataEditor] Import type mismatch: file contains '{storedType}', " +
                    $"container expects '{container.EntryType.FullName}'. " +
                    "Attempting import anyway — unrecognised fields will be ignored.");
            }

            // Support both "entries" (current) and "Entries" (legacy) keys.
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
                    var entry = (IGameDataEntry)token.ToObject(container.EntryType, _serializer);
                    if (entry != null)
                        targetList.Add(entry);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[LiveGameDataEditor] Failed to deserialize entry: {ex.Message}");
                }
            }
        }

        // ── UnityObjectJsonConverter ───────────────────────────────────────────────

        /// <summary>
        /// Converts <see cref="UnityEngine.Object"/> subtype fields to/from a simple
        /// asset-path token so they survive JSON round-trips without losing the reference.
        ///
        /// Serialized form: <c>{ "__assetPath": "Assets/Art/hero.png" }</c>
        /// </summary>
        private sealed class UnityObjectJsonConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
                => typeof(UnityEngine.Object).IsAssignableFrom(objectType);

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                string path = AssetDatabase.GetAssetPath((UnityEngine.Object)value);
                writer.WriteStartObject();
                writer.WritePropertyName("__assetPath");
                writer.WriteValue(path);
                writer.WriteEndObject();
            }

            public override object ReadJson(
                JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    reader.Read();
                    return null;
                }

                var jo   = JObject.Load(reader);
                string path = jo["__assetPath"]?.Value<string>();
                if (string.IsNullOrEmpty(path)) return null;

                return AssetDatabase.LoadAssetAtPath(path, objectType);
            }
        }
    }
}

