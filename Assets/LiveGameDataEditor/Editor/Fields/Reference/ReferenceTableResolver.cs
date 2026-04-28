using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Discovers target table rows for string-backed table references.
    /// </summary>
    public static class ReferenceTableResolver
    {
        public sealed class Option
        {
            public string Key { get; }
            public string Display { get; }

            public Option(string key, string display)
            {
                Key = key;
                Display = display;
            }
        }

        public sealed class Result
        {
            public TableReferenceAttribute Attribute { get; }
            public Type TargetTableType => Attribute?.TargetTableType;
            public List<ScriptableObject> TargetAssets { get; } = new();
            public List<Option> Options { get; } = new();
            public List<string> Errors { get; } = new();
            public bool HasDuplicateKeys { get; private set; }
            public bool HasKeyField => KeyField != null;
            public FieldInfo KeyField { get; private set; }
            public FieldInfo DisplayField { get; private set; }
            public Type TargetEntryType { get; private set; }

            public Result(TableReferenceAttribute attribute)
            {
                Attribute = attribute;
            }

            public bool ContainsKey(string key)
            {
                return Options.Any(option => string.Equals(option.Key, key, StringComparison.Ordinal));
            }

            internal void SetTargetEntryType(Type entryType)
            {
                TargetEntryType = entryType;
            }

            internal void SetFields(FieldInfo keyField, FieldInfo displayField)
            {
                KeyField = keyField;
                DisplayField = displayField;
            }

            internal void SetDuplicateKeys()
            {
                HasDuplicateKeys = true;
            }
        }

        public static bool IsReferenceField(FieldInfo field)
        {
            return field.GetCustomAttribute<TableReferenceAttribute>() != null;
        }

        public static Result Resolve(FieldInfo sourceField)
        {
            var attribute = sourceField.GetCustomAttribute<TableReferenceAttribute>();
            var result = new Result(attribute);

            if (attribute == null)
            {
                result.Errors.Add("Missing TableReferenceAttribute.");
                return result;
            }

            if (sourceField.FieldType != typeof(string))
            {
                result.Errors.Add($"[TableReference] can only be used on string fields: {sourceField.Name}.");
                return result;
            }

            if (attribute.TargetTableType == null)
            {
                result.Errors.Add($"Missing referenced table type on {sourceField.Name}.");
                return result;
            }

            if (!typeof(ScriptableObject).IsAssignableFrom(attribute.TargetTableType) ||
                !typeof(IGameDataContainer).IsAssignableFrom(attribute.TargetTableType))
            {
                result.Errors.Add(
                    $"Referenced table type {attribute.TargetTableType.Name} must be a ScriptableObject implementing IGameDataContainer.");
                return result;
            }

            LoadTargetAssets(result);
            if (result.TargetAssets.Count == 0)
            {
                result.Errors.Add($"Missing referenced table asset: {attribute.TargetTableType.Name}");
                return result;
            }

            var firstContainer = result.TargetAssets[0] as IGameDataContainer;
            result.SetTargetEntryType(firstContainer?.EntryType);
            if (result.TargetEntryType == null)
            {
                result.Errors.Add($"Target table {attribute.TargetTableType.Name} has no row list.");
                return result;
            }

            var keyFields = result.TargetEntryType
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => field.GetCustomAttribute<TableKeyAttribute>() != null)
                .ToList();

            if (keyFields.Count == 0)
            {
                result.Errors.Add($"Target row type {result.TargetEntryType.Name} has no [TableKey] field.");
                return result;
            }

            if (keyFields.Count > 1)
            {
                result.Errors.Add($"Target row type {result.TargetEntryType.Name} has multiple [TableKey] fields.");
                return result;
            }

            var displayField = result.TargetEntryType
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(field => field.GetCustomAttribute<TableDisplayAttribute>() != null);

            result.SetFields(keyFields[0], displayField);
            BuildOptions(result);
            return result;
        }

        public static string GetOptionLabel(Option option)
        {
            if (option == null || string.IsNullOrEmpty(option.Key))
            {
                return "(None)";
            }

            return string.IsNullOrEmpty(option.Display)
                ? option.Key
                : $"{option.Key} - {option.Display}";
        }

        private static void LoadTargetAssets(Result result)
        {
            var guids = AssetDatabase.FindAssets($"t:{result.TargetTableType.Name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, result.TargetTableType) as ScriptableObject;
                if (asset == null || asset is not IGameDataContainer)
                {
                    continue;
                }
                result.TargetAssets.Add(asset);
            }
        }

        private static void BuildOptions(Result result)
        {
            var keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var asset in result.TargetAssets)
            {
                if (asset is not IGameDataContainer container)
                {
                    continue;
                }

                foreach (var row in container.GetEntries())
                {
                    var rawKey = result.KeyField.GetValue(row);
                    var key = rawKey?.ToString() ?? string.Empty;
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    var display = result.DisplayField?.GetValue(row)?.ToString() ?? string.Empty;
                    result.Options.Add(new Option(key, display));

                    keyCounts.TryGetValue(key, out var count);
                    keyCounts[key] = count + 1;
                }
            }

            foreach (var pair in keyCounts)
            {
                if (pair.Value <= 1)
                {
                    continue;
                }

                result.SetDuplicateKeys();
                result.Errors.Add($"Duplicate key '{pair.Key}' in {result.TargetTableType.Name}.");
            }
        }
    }
}
