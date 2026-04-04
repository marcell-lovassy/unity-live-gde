using System;
using System.Collections;
using System.Collections.Generic;
using LiveGameDataEditor.Editor;
using UnityEngine;

namespace LiveGameDataEditor.GoogleSheets
{
    /// <summary>
    /// Converts between sheet column values (strings) and typed C# field values,
    /// and builds the header-to-column-index mapping used during pull.
    /// </summary>
    public static class GoogleSheetsColumnMapper
    {
        // ── Header mapping ─────────────────────────────────────────────────────

        /// <summary>
        /// Builds a mapping of <c>fieldName → columnIndex</c> by matching the sheet header
        /// row against the container's column definitions.  Matching is case-insensitive.
        /// Returns an empty dict if the header row is null or empty.
        /// </summary>
        public static Dictionary<string, int> BuildMapping(
            IReadOnlyList<GameDataColumnDefinition> columns,
            IList<string>                           headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (headerRow == null)
            {
                return map;
            }

            for (int ci = 0; ci < headerRow.Count; ci++)
            {
                string header = headerRow[ci]?.Trim();
                if (string.IsNullOrEmpty(header))
                {
                    continue;
                }
                map[header] = ci;
            }

            return map;
        }

        // ── Value → string ─────────────────────────────────────────────────────

        /// <summary>
        /// Converts a field value to its sheet-cell string representation.
        /// List fields are joined with their declared separator.
        /// </summary>
        public static string ValueToString(GameDataColumnDefinition col, object value)
        {
            if (value == null)
            {
                return "";
            }

            if (col.IsList)
            {
                string sep = col.ListSeparator ?? ",";
                var list   = value as IList;
                if (list == null)
                {
                    return "";
                }

                var parts = new List<string>(list.Count);
                foreach (var item in list)
                {
                    parts.Add(item?.ToString() ?? "");
                }
                return string.Join(sep, parts);
            }

            if (col.IsBool)
            {
                return ((bool)value) ? "TRUE" : "FALSE";
            }

            // float: use invariant culture to avoid locale-specific decimal separators
            if (col.IsFloat)
            {
                return ((float)value).ToString("G", System.Globalization.CultureInfo.InvariantCulture);
            }

            return value.ToString();
        }

        // ── string → value ─────────────────────────────────────────────────────

        /// <summary>
        /// Parses a sheet cell string into the typed value expected by the field.
        /// Returns the field's default value and logs a warning on parse failure.
        /// </summary>
        public static object StringToValue(GameDataColumnDefinition col, string raw)
        {
            if (raw == null)
            {
                raw = "";
            }
            raw = raw.Trim();

            try
            {
                if (col.IsList)
                {
                    return ParseListValue(col, raw);
                }

                if (col.IsBool)
                {
                    return ParseBool(raw, col.Field.Name);
                }

                if (col.IsInt)
                {
                    if (int.TryParse(raw, out int intVal))
                    {
                        return intVal;
                    }
                    LogParseWarning(col.Field.Name, raw, "int");
                    return 0;
                }

                if (col.IsFloat)
                {
                    // Accept both '.' and ',' as decimal separator for robustness.
                    string normalised = raw.Replace(',', '.');
                    if (float.TryParse(normalised,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float floatVal))
                    {
                        return floatVal;
                    }
                    LogParseWarning(col.Field.Name, raw, "float");
                    return 0f;
                }

                if (col.IsEnum)
                {
                    Type enumType = col.Field.FieldType;
                    if (Enum.TryParse(enumType, raw, ignoreCase: true, out object enumVal))
                    {
                        return enumVal;
                    }
                    LogParseWarning(col.Field.Name, raw, enumType.Name);
                    return Enum.GetValues(enumType).GetValue(0);
                }

                // string and fallback
                return raw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[LiveGameDataEditor] GoogleSheets: unexpected error parsing field " +
                    $"'{col.Field.Name}' value '{raw}': {ex.Message}. Using default.");
                return GetFieldDefault(col.Field);
            }
        }

        // ── Private helpers ────────────────────────────────────────────────────

        private static object ParseListValue(GameDataColumnDefinition col, string raw)
        {
            string sep      = col.ListSeparator ?? ",";
            Type   listType = col.Field.FieldType;

            // Determine the element type (e.g. List<string> → string)
            Type elementType = typeof(string);
            if (listType.IsGenericType)
            {
                elementType = listType.GetGenericArguments()[0];
            }

            var list = (IList)Activator.CreateInstance(listType);
            if (string.IsNullOrEmpty(raw))
            {
                return list;
            }

            foreach (string part in raw.Split(new[] { sep }, StringSplitOptions.None))
            {
                string trimmed = part.Trim();
                object element = ConvertToElementType(elementType, trimmed, col.Field.Name);
                list.Add(element);
            }
            return list;
        }

        private static object ConvertToElementType(Type elementType, string raw, string fieldName)
        {
            if (elementType == typeof(string))
            {
                return raw;
            }

            if (elementType == typeof(int))
            {
                if (int.TryParse(raw, out int v))
                {
                    return v;
                }
                LogParseWarning(fieldName, raw, "int list element");
                return 0;
            }

            if (elementType == typeof(float))
            {
                string normalised = raw.Replace(',', '.');
                if (float.TryParse(normalised,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out float v))
                {
                    return v;
                }
                LogParseWarning(fieldName, raw, "float list element");
                return 0f;
            }

            if (elementType == typeof(bool))
            {
                return ParseBool(raw, fieldName);
            }

            // Fallback: try Convert
            try
            {
                return Convert.ChangeType(raw, elementType);
            }
            catch
            {
                return Activator.CreateInstance(elementType);
            }
        }

        private static bool ParseBool(string raw, string fieldName)
        {
            switch (raw.ToUpperInvariant())
            {
                case "TRUE":
                case "1":
                case "YES":
                    return true;
                case "FALSE":
                case "0":
                case "NO":
                case "":
                    return false;
            }
            LogParseWarning(fieldName, raw, "bool");
            return false;
        }

        private static object GetFieldDefault(FieldInfo field)
        {
            return field.FieldType.IsValueType ? Activator.CreateInstance(field.FieldType) : null;
        }

        private static void LogParseWarning(string fieldName, string raw, string typeName)
        {
            Debug.LogWarning(
                $"[LiveGameDataEditor] GoogleSheets: could not parse '{raw}' as {typeName} " +
                $"for field '{fieldName}'. Using default value.");
        }
    }
}
