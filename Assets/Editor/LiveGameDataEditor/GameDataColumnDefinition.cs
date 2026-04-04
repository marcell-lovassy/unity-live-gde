using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Describes a single column in the data table: the reflected field it maps to,
    /// its display label, widget type, and default sizing values.
    ///
    /// Supports <see cref="ColumnHeaderAttribute"/> for custom labels and
    /// <see cref="ListFieldAttribute"/> for <c>List&lt;T&gt;</c> / <c>T[]</c> fields.
    /// Instances are generated via <see cref="FromType"/> and cached per entry type.
    /// </summary>
    public sealed class GameDataColumnDefinition
    {
        // ── Core ───────────────────────────────────────────────────────────────────

        /// <summary>The reflected public instance field this column maps to.</summary>
        public FieldInfo Field { get; }

        /// <summary>
        /// Display label shown in the column header.
        /// Sourced from <see cref="ColumnHeaderAttribute"/> when present; otherwise the field name.
        /// </summary>
        public string Label { get; }

        /// <summary>The field's declared CLR type.</summary>
        public Type FieldType { get; }

        /// <summary>CSS flex-grow value used for proportional column sizing.</summary>
        public float FlexGrow { get; }

        /// <summary>Minimum pixel width of the column.</summary>
        public float MinWidth { get; }

        // ── List field support ─────────────────────────────────────────────────────

        /// <summary>
        /// True when the field is a <c>List&lt;T&gt;</c> or <c>T[]</c> decorated with
        /// <see cref="ListFieldAttribute"/>. The row renders it as a single text cell.
        /// </summary>
        public bool IsList { get; }

        /// <summary>
        /// Separator used to join list items for display and to split edited text back
        /// into items. Comes from <see cref="ListFieldAttribute.Separator"/>.
        /// </summary>
        public string ListSeparator { get; }

        /// <summary>The element type of the list (e.g. <c>string</c>, <c>int</c>).</summary>
        public Type ElementType { get; }

        // ── Type shorthand helpers (pick the right input widget) ───────────────────

        public bool IsString      => FieldType == typeof(string);
        public bool IsInt         => FieldType == typeof(int);
        public bool IsFloat       => FieldType == typeof(float);
        public bool IsBool        => FieldType == typeof(bool);
        public bool IsEnum        => FieldType.IsEnum;

        /// <summary>True for any <c>UnityEngine.Object</c> subtype (Sprite, AudioClip, etc.).</summary>
        public bool IsUnityObject => typeof(UnityEngine.Object).IsAssignableFrom(FieldType);

        // ── Constructor ────────────────────────────────────────────────────────────

        private GameDataColumnDefinition(
            FieldInfo field,
            string    label,
            float     flexGrow,
            float     minWidth,
            bool      isList,
            string    listSeparator,
            Type      elementType)
        {
            Field         = field;
            Label         = label;
            FieldType     = field.FieldType;
            FlexGrow      = flexGrow;
            MinWidth      = minWidth;
            IsList        = isList;
            ListSeparator = listSeparator;
            ElementType   = elementType;
        }

        // ── Static factory ─────────────────────────────────────────────────────────

        private static readonly Dictionary<Type, List<GameDataColumnDefinition>> _cache = new();

        /// <summary>
        /// Reflects all public instance fields of <typeparamref name="T"/> and returns
        /// a <see cref="GameDataColumnDefinition"/> for each. Result is cached per type.
        /// </summary>
        public static List<GameDataColumnDefinition> FromType<T>() => FromType(typeof(T));

        /// <inheritdoc cref="FromType{T}"/>
        public static List<GameDataColumnDefinition> FromType(Type type)
        {
            if (_cache.TryGetValue(type, out var cached)) return cached;

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var defs = new List<GameDataColumnDefinition>(fields.Length);
            foreach (var f in fields)
                defs.Add(BuildDef(f));

            _cache[type] = defs;
            return defs;
        }

        private static GameDataColumnDefinition BuildDef(FieldInfo f)
        {
            // Label: prefer [ColumnHeader] attribute, fall back to field name.
            var headerAttr = f.GetCustomAttribute<ColumnHeaderAttribute>();
            string label = headerAttr?.Label ?? f.Name;

            // List detection: [ListField] is required; auto-detects List<T> and T[].
            bool   isList    = false;
            string separator = ", ";
            Type   elemType  = null;

            var listAttr = f.GetCustomAttribute<ListFieldAttribute>();
            if (listAttr != null)
            {
                separator = listAttr.Separator;
                if (f.FieldType.IsGenericType &&
                    f.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    isList   = true;
                    elemType = f.FieldType.GetGenericArguments()[0];
                }
                else if (f.FieldType.IsArray)
                {
                    isList   = true;
                    elemType = f.FieldType.GetElementType();
                }
            }

            // Sizing — most specific type wins.
            float flexGrow, minWidth;
            if      (isList)                                            { flexGrow = 3f;   minWidth = 150f; }
            else if (f.FieldType == typeof(string))                     { flexGrow = 3f;   minWidth = 120f; }
            else if (f.FieldType == typeof(bool))                       { flexGrow = 0f;   minWidth = 64f;  }
            else if (f.FieldType.IsEnum)                                { flexGrow = 1.5f; minWidth = 110f; }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))     { flexGrow = 1.5f; minWidth = 130f; }
            else                                                        { flexGrow = 1f;   minWidth = 60f;  }

            return new GameDataColumnDefinition(
                f, label, flexGrow, minWidth, isList, separator, elemType);
        }
        // ── List field helpers (shared by GameDataRowView and GameDataCsvSerializer) ──

        /// <summary>
        /// Converts a list/array field value to its separator-joined string representation.
        /// Returns an empty string for null values.
        /// </summary>
        public static string ListFieldToString(object value, GameDataColumnDefinition col)
        {
            if (value == null) return string.Empty;
            if (value is IEnumerable<string> strSeq)
                return string.Join(col.ListSeparator, strSeq);
            if (value is IEnumerable items)
                return string.Join(col.ListSeparator, items.Cast<object>().Select(o => o?.ToString() ?? ""));
            return value.ToString();
        }

        /// <summary>
        /// Parses a separator-joined string back into a list or array matching
        /// <see cref="Field"/>.<see cref="FieldInfo.FieldType"/>. Leading/trailing
        /// whitespace on each item is trimmed.
        /// Supported element types: <c>string</c>, <c>int</c>, <c>float</c>.
        /// </summary>
        public object ParseListField(string text)
        {
            if (text == null) text = string.Empty;
            var parts = text
                .Split(new[] { ListSeparator }, StringSplitOptions.None)
                .Select(p => p.Trim())
                .ToArray();

            if (ElementType == typeof(string))
            {
                if (Field.FieldType.IsArray) return parts;
                return new List<string>(parts);
            }
            if (ElementType == typeof(int))
            {
                var ints = parts.Select(p => int.TryParse(p, out int v) ? v : 0).ToList();
                if (Field.FieldType.IsArray) return ints.ToArray();
                return ints;
            }
            if (ElementType == typeof(float))
            {
                var floats = parts.Select(p => float.TryParse(p, out float v) ? v : 0f).ToList();
                if (Field.FieldType.IsArray) return floats.ToArray();
                return floats;
            }
            // Fallback
            return new List<string>(parts);
        }
    }
}

