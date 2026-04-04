using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Serializes and deserializes any <see cref="IGameDataContainer"/> to/from CSV.
    ///
    /// Format:
    ///   - First row = column headers (uses <see cref="GameDataColumnDefinition.Label"/>,
    ///     which respects <see cref="ColumnHeaderAttribute"/>).
    ///   - One data row per entry; fields separated by commas.
    ///   - Fields that contain commas, quotes, or line breaks are RFC 4180 quoted.
    ///   - Enum values → their name string.
    ///   - Bool → <c>true</c> / <c>false</c>.
    ///   - <see cref="UnityEngine.Object"/> → project-relative asset path.
    ///   - <see cref="ListFieldAttribute"/> fields → items joined by their separator
    ///     (the whole field is quoted if the separator contains a comma).
    ///
    /// Import column matching:
    ///   CSV header is matched first against <see cref="GameDataColumnDefinition.Label"/>
    ///   then against the raw field name (both case-insensitive). Unknown CSV headers are
    ///   skipped; missing headers leave the field at its default value.
    /// </summary>
    public static class GameDataCsvSerializer
    {
        // ── Serialize ──────────────────────────────────────────────────────────────

        public static string Serialize(IGameDataContainer container)
        {
            if (container == null) return string.Empty;

            var columns = GameDataColumnDefinition.FromType(container.EntryType);
            var sb      = new StringBuilder();

            // Header row
            for (int i = 0; i < columns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(EscapeField(columns[i].Label));
            }
            sb.AppendLine();

            // Data rows
            var entries = container.GetEntries();
            foreach (var obj in entries)
            {
                var entry = (IGameDataEntry)obj;
                for (int i = 0; i < columns.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(EscapeField(FieldToString(columns[i].Field.GetValue(entry), columns[i])));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ── Deserialize ────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses <paramref name="csv"/> and populates <paramref name="container"/>.
        /// Returns <c>true</c> on success. The caller is responsible for Undo wrapping.
        /// </summary>
        public static bool Deserialize(string csv, IGameDataContainer container)
        {
            if (string.IsNullOrWhiteSpace(csv) || container == null) return false;

            var lines = SplitLines(csv);
            if (lines.Count < 1) return false;

            var columns = GameDataColumnDefinition.FromType(container.EntryType);

            // Build header → column index map (label first, then field name, case-insensitive)
            var headerRow    = ParseCsvLine(lines[0]);
            var colMap       = BuildColumnMap(headerRow, columns);

            var entries = container.GetEntries();
            entries.Clear();

            for (int lineIdx = 1; lineIdx < lines.Count; lineIdx++)
            {
                string line = lines[lineIdx].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var fields = ParseCsvLine(line);
                var entry  = (IGameDataEntry)Activator.CreateInstance(container.EntryType);

                for (int csvCol = 0; csvCol < fields.Count && csvCol < colMap.Length; csvCol++)
                {
                    var colDef = colMap[csvCol];
                    if (colDef == null) continue;

                    try
                    {
                        var value = ParseField(fields[csvCol], colDef);
                        colDef.Field.SetValue(entry, value);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"[LiveGameDataEditor] CSV: could not parse column '{colDef.Label}' " +
                            $"at row {lineIdx}: {ex.Message}");
                    }
                }

                entries.Add(entry);
            }

            return true;
        }

        // ── Field conversion ───────────────────────────────────────────────────────

        private static string FieldToString(object value, GameDataColumnDefinition col)
        {
            if (value == null) return string.Empty;
            if (col.IsList)        return GameDataColumnDefinition.ListFieldToString(value, col);
            if (col.IsUnityObject) return AssetDatabase.GetAssetPath((UnityEngine.Object)value);
            if (col.IsBool)        return value.ToString().ToLowerInvariant();
            return value.ToString();
        }

        private static object ParseField(string text, GameDataColumnDefinition col)
        {
            if (col.IsList)   return col.ParseListField(text);
            if (col.IsString) return text;
            if (col.IsInt)    return int.TryParse(text.Trim(), out int i) ? i : 0;
            if (col.IsFloat)  return float.TryParse(text.Trim(), out float f) ? f : 0f;
            if (col.IsBool)   return bool.TryParse(text.Trim(), out bool b) && b;
            if (col.IsEnum)
            {
                return Enum.TryParse(col.FieldType, text.Trim(), ignoreCase: true, out object result)
                    ? result
                    : Activator.CreateInstance(col.FieldType);
            }
            if (col.IsUnityObject)
            {
                string path = text.Trim();
                return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath(path, col.FieldType);
            }
            return text;
        }

        // ── CSV escaping (RFC 4180) ────────────────────────────────────────────────

        private static string EscapeField(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            bool needsQuoting = value.Contains(',')  ||
                                value.Contains('"')  ||
                                value.Contains('\n') ||
                                value.Contains('\r');
            if (!needsQuoting) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        // ── CSV parsing (RFC 4180) ─────────────────────────────────────────────────

        private static List<string> SplitLines(string text)
        {
            var lines  = new List<string>();
            var sb     = new StringBuilder();
            bool inQ   = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQ)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQ = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') { inQ = true; sb.Append(c); }
                    else if (c == '\n') { lines.Add(sb.ToString()); sb.Clear(); }
                    else if (c == '\r') { /* skip */ }
                    else sb.Append(c);
                }
            }
            if (sb.Length > 0) lines.Add(sb.ToString());
            return lines;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var fields  = new List<string>();
            var current = new StringBuilder();
            bool inQ    = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQ)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                        else inQ = false;
                    }
                    else current.Append(c);
                }
                else
                {
                    if (c == '"') inQ = true;
                    else if (c == ',') { fields.Add(current.ToString()); current.Clear(); }
                    else current.Append(c);
                }
            }
            fields.Add(current.ToString());
            return fields;
        }

        // ── Column map builder ─────────────────────────────────────────────────────

        private static GameDataColumnDefinition[] BuildColumnMap(
            List<string> headers,
            List<GameDataColumnDefinition> columns)
        {
            var map = new GameDataColumnDefinition[headers.Count];
            for (int h = 0; h < headers.Count; h++)
            {
                string header = headers[h].Trim();
                foreach (var col in columns)
                {
                    if (string.Equals(col.Label, header, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(col.Field.Name, header, StringComparison.OrdinalIgnoreCase))
                    {
                        map[h] = col;
                        break;
                    }
                }
            }
            return map;
        }
    }
}
