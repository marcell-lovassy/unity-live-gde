using System;
using System.Collections.Generic;
using System.Reflection;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Describes a single column in the data table: the reflected field it maps to,
    /// its display label, and default sizing values.
    /// Instances are generated via <see cref="FromType{T}"/> and cached.
    /// </summary>
    public sealed class GameDataColumnDefinition
    {
        /// <summary>The reflected public instance field this column maps to.</summary>
        public FieldInfo Field { get; }

        /// <summary>Display label shown in the column header.</summary>
        public string Label { get; }

        /// <summary>The field's declared CLR type.</summary>
        public Type FieldType { get; }

        /// <summary>CSS flex-grow value used for proportional column sizing.</summary>
        public float FlexGrow { get; }

        /// <summary>Minimum pixel width of the column.</summary>
        public float MinWidth { get; }

        // Type shorthand helpers used by row views to pick the right input widget.
        public bool IsString => FieldType == typeof(string);
        public bool IsInt    => FieldType == typeof(int);
        public bool IsFloat  => FieldType == typeof(float);
        public bool IsBool   => FieldType == typeof(bool);

        public GameDataColumnDefinition(FieldInfo field, float flexGrow, float minWidth)
        {
            Field     = field;
            Label     = field.Name;
            FieldType = field.FieldType;
            FlexGrow  = flexGrow;
            MinWidth  = minWidth;
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

        /// <summary>Assigns sensible default sizing based on field type.</summary>
        private static GameDataColumnDefinition BuildDef(FieldInfo f)
        {
            if (f.FieldType == typeof(string)) return new GameDataColumnDefinition(f, flexGrow: 3f, minWidth: 120f);
            if (f.FieldType == typeof(bool))   return new GameDataColumnDefinition(f, flexGrow: 0f, minWidth: 64f);
            return new GameDataColumnDefinition(f, flexGrow: 1f, minWidth: 60f);
        }
    }
}
