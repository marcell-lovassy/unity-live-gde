using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    ///     Discovers and caches game data types at edit-time using Unity's <see cref="TypeCache" />.
    ///     Provides lookup from entry type → container type by walking the generic inheritance chain.
    ///     All results are cached on first access; safe to call every frame if needed.
    /// </summary>
    public static class GameDataTypeRegistry
    {
        // Cached reference to the open generic base — avoids re-allocating per lookup.
        private static readonly Type _containerBaseDef = typeof(GameDataContainerBase<>);

        private static List<Type> _cachedEntryTypes;
        private static readonly Dictionary<Type, Type> _containerTypeCache = new();

        // ── Entry type discovery ───────────────────────────────────────────────────

        /// <summary>
        ///     Returns all non-abstract, <c>[Serializable]</c> types implementing
        ///     <see cref="IGameData" />. Results are cached after the first call.
        /// </summary>
        public static IReadOnlyList<Type> GetEntryTypes()
        {
            if (_cachedEntryTypes != null) return _cachedEntryTypes;

            _cachedEntryTypes = new List<Type>();
            foreach (var t in TypeCache.GetTypesDerivedFrom<IGameData>())
            {
                if (t.IsAbstract || t.IsInterface) continue;
                if (t.GetCustomAttribute<SerializableAttribute>() == null) continue;
                _cachedEntryTypes.Add(t);
            }

            return _cachedEntryTypes;
        }

        // ── Container type lookup ──────────────────────────────────────────────────

        /// <summary>
        ///     Finds the concrete non-abstract <see cref="ScriptableObject" /> container type
        ///     that extends <c>GameDataContainerBase&lt;<paramref name="entryType" />&gt;</c>.
        ///     Returns <c>null</c> if no match exists. Results are cached.
        /// </summary>
        public static Type FindContainerTypeForEntry(Type entryType)
        {
            if (entryType == null) return null;
            if (_containerTypeCache.TryGetValue(entryType, out var cached)) return cached;

            Type found = null;
            foreach (var t in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (t.IsAbstract) continue;
                if (!typeof(IGameDataContainer).IsAssignableFrom(t)) continue;
                if (GetGenericEntryType(t) == entryType)
                {
                    found = t;
                    break;
                }
            }

            // Cache both positive and negative results to avoid repeated full scans.
            _containerTypeCache[entryType] = found;
            return found;
        }

        // ── Display name helper ────────────────────────────────────────────────────

        /// <summary>
        ///     Returns the friendly display name for <paramref name="entryType" />.
        ///     Reads <see cref="GameDataAttribute.DisplayName" /> if present; falls back to
        ///     <c>Type.Name</c>.
        /// </summary>
        public static string GetEntryDisplayName(Type entryType)
        {
            if (entryType == null) return "Unknown";
            var attr = entryType.GetCustomAttribute<GameDataAttribute>();
            return attr != null && !string.IsNullOrEmpty(attr.DisplayName)
                ? attr.DisplayName
                : entryType.Name;
        }

        // ── Private helpers ────────────────────────────────────────────────────────

        /// <summary>
        ///     Walks <paramref name="containerType" />'s inheritance chain to find
        ///     <c>GameDataContainerBase&lt;T&gt;</c> and returns T.
        ///     Returns <c>null</c> if the chain does not contain the base.
        /// </summary>
        private static Type GetGenericEntryType(Type containerType)
        {
            var t = containerType;
            while (t != null && t != typeof(object))
            {
                if (t.IsGenericType && t.GetGenericTypeDefinition() == _containerBaseDef)
                    return t.GetGenericArguments()[0];
                t = t.BaseType;
            }

            return null;
        }
    }
}