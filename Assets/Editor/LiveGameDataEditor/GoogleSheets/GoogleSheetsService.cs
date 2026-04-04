using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LiveGameDataEditor;
using LiveGameDataEditor.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace LiveGameDataEditor.GoogleSheets
{
    /// <summary>
    /// Pushes and pulls data between an <see cref="IGameDataContainer"/> and a Google Sheet
    /// via the Google Sheets REST API v4.
    ///
    /// <b>Push</b> writes a header row + data rows to the configured tab, replacing all
    /// existing content (PUT with valueInputOption=USER_ENTERED).
    ///
    /// <b>Pull</b> reads the sheet, matches columns by header name, and overwrites the
    /// container's entries (wrapped in Undo.RecordObject).
    /// </summary>
    public static class GoogleSheetsService
    {
        private const string ApiBase = "https://sheets.googleapis.com/v4/spreadsheets";

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Pushes all entries from <paramref name="container"/> to the configured Google Sheet.
        /// Returns a result indicating success or failure with a human-readable message.
        /// </summary>
        public static async Task<SyncResult> PushAsync(IGameDataContainer container, GoogleSheetsConfig config)
        {
            try
            {
                ValidateConfig(config);

                // API Key is read-only — Push is not supported.
                if (config.AuthMode == GoogleSheetsAuthMode.ApiKey)
                {
                    return SyncResult.Fail(
                        "API Key mode is read-only. " +
                        "Switch to OAuth or Service Account auth to push data.");
                }

                var columns = GameDataColumnDefinition.FromType(container.EntryType);
                IList entries = container.GetEntries();

                // Build the 2D values array: [header row, ...data rows]
                var values = new List<IList<object>>();
                var header = BuildHeaderRow(columns);
                values.Add(header);

                foreach (var entry in entries)
                {
                    values.Add(BuildDataRow(columns, (IGameDataEntry)entry));
                }

                string range  = ResolveTabName(container);
                string url    = $"{ApiBase}/{Uri.EscapeDataString(config.SpreadsheetId)}/values/{Uri.EscapeDataString(range)}?valueInputOption=USER_ENTERED";
                string authHeader = await GoogleSheetsAuthService.GetAuthHeaderAsync(config);
                if (config.AuthMode == GoogleSheetsAuthMode.ApiKey)
                {
                    url += "&key=" + Uri.EscapeDataString(config.ApiKey);
                }

                var body = new JObject
                {
                    ["range"]          = range,
                    ["majorDimension"] = "ROWS",
                    ["values"]         = JArray.FromObject(values)
                };
                string bodyJson = body.ToString(Formatting.None);

                string response = await SendRequestAsync("PUT", url, authHeader, bodyJson);
                var responseObj = JObject.Parse(response);
                int updated = (int?)responseObj["updatedCells"] ?? 0;

                string msg = $"Pushed {entries.Count} rows ({updated} cells updated) " +
                             $"to tab '{range}' at {DateTime.Now:HH:mm:ss}.";
                Debug.Log($"[LiveGameDataEditor] GoogleSheets Push: {msg}");
                return SyncResult.Ok(msg);
            }
            catch (GoogleSheetsAuthException ex)
            {
                string msg = "Authentication failed: " + ex.Message;
                Debug.LogError($"[LiveGameDataEditor] GoogleSheets Push: {msg}");
                return SyncResult.Fail(msg);
            }
            catch (GoogleSheetsApiException ex)
            {
                string msg = $"API error ({ex.StatusCode}): " + ex.Message;
                Debug.LogError($"[LiveGameDataEditor] GoogleSheets Push: {msg}");
                return SyncResult.Fail(msg);
            }
            catch (Exception ex)
            {
                string msg = "Unexpected error: " + ex.Message;
                Debug.LogException(ex);
                return SyncResult.Fail(msg);
            }
        }

        /// <summary>
        /// Pulls data from the configured Google Sheet and overwrites the container's entries.
        /// The operation is wrapped in <c>Undo.RecordObject</c> so it can be undone.
        /// Returns a result indicating success or failure with a human-readable message.
        /// </summary>
        public static async Task<SyncResult> PullAsync(IGameDataContainer container, GoogleSheetsConfig config)
        {
            var so = container as UnityEngine.Object;
            try
            {
                ValidateConfig(config);

                string range  = ResolveTabName(container);
                string url    = $"{ApiBase}/{Uri.EscapeDataString(config.SpreadsheetId)}/values/{Uri.EscapeDataString(range)}";
                string authHeader = await GoogleSheetsAuthService.GetAuthHeaderAsync(config);
                if (config.AuthMode == GoogleSheetsAuthMode.ApiKey)
                {
                    url += "?key=" + Uri.EscapeDataString(config.ApiKey);
                }

                string response = await SendRequestAsync("GET", url, authHeader, null);
                var responseObj = JObject.Parse(response);
                var rawValues   = (JArray)responseObj["values"];

                if (rawValues == null || rawValues.Count == 0)
                {
                    return SyncResult.Ok("Sheet is empty — no data imported.");
                }

                var columns  = GameDataColumnDefinition.FromType(container.EntryType);
                var allRows  = ParseRawValues(rawValues);

                // First row is the header when HasHeaderRow is true.
                IList<string> headerRow = config.HasHeaderRow && allRows.Count > 0
                    ? allRows[0]
                    : BuildDefaultHeader(columns);

                var mapping   = GoogleSheetsColumnMapper.BuildMapping(columns, headerRow);
                int dataStart = config.HasHeaderRow ? 1 : 0;

                if (so != null)
                {
                    Undo.RecordObject(so, "Pull from Google Sheets");
                }

                IList entries = container.GetEntries();
                entries.Clear();

                int importedCount = 0;
                for (int ri = dataStart; ri < allRows.Count; ri++)
                {
                    var row   = allRows[ri];
                    var entry = (IGameDataEntry)Activator.CreateInstance(container.EntryType);

                    foreach (var col in columns)
                    {
                        if (!mapping.TryGetValue(col.Field.Name, out int ci))
                        {
                            continue; // column not in sheet; keep default
                        }
                        string raw = ci < row.Count ? row[ci] : "";
                        object val = GoogleSheetsColumnMapper.StringToValue(col, raw);
                        col.Field.SetValue(entry, val);
                    }

                    entries.Add(entry);
                    importedCount++;
                }

                if (so != null)
                {
                    EditorUtility.SetDirty(so);
                }

                string msg = $"Pulled {importedCount} rows from tab '{range}' at {DateTime.Now:HH:mm:ss}.";
                Debug.Log($"[LiveGameDataEditor] GoogleSheets Pull: {msg}");
                return SyncResult.Ok(msg);
            }
            catch (GoogleSheetsAuthException ex)
            {
                string msg = "Authentication failed: " + ex.Message;
                Debug.LogError($"[LiveGameDataEditor] GoogleSheets Pull: {msg}");
                return SyncResult.Fail(msg);
            }
            catch (GoogleSheetsApiException ex)
            {
                string msg = $"API error ({ex.StatusCode}): " + ex.Message;
                Debug.LogError($"[LiveGameDataEditor] GoogleSheets Pull: {msg}");
                return SyncResult.Fail(msg);
            }
            catch (Exception ex)
            {
                string msg = "Unexpected error: " + ex.Message;
                Debug.LogException(ex);
                return SyncResult.Fail(msg);
            }
        }

        // ── Row building ───────────────────────────────────────────────────────

        private static IList<object> BuildHeaderRow(IReadOnlyList<GameDataColumnDefinition> columns)
        {
            var row = new List<object>(columns.Count);
            foreach (var col in columns)
            {
                row.Add(col.Field.Name);
            }
            return row;
        }

        private static IList<object> BuildDataRow(
            IReadOnlyList<GameDataColumnDefinition> columns,
            IGameDataEntry entry)
        {
            var row = new List<object>(columns.Count);
            foreach (var col in columns)
            {
                object val = col.Field.GetValue(entry);
                row.Add(GoogleSheetsColumnMapper.ValueToString(col, val));
            }
            return row;
        }

        private static IList<string> BuildDefaultHeader(IReadOnlyList<GameDataColumnDefinition> columns)
        {
            var header = new List<string>(columns.Count);
            foreach (var col in columns)
            {
                header.Add(col.Field.Name);
            }
            return header;
        }

        // ── Parsing ────────────────────────────────────────────────────────────

        private static List<List<string>> ParseRawValues(JArray rawValues)
        {
            var result = new List<List<string>>(rawValues.Count);
            foreach (JArray rowArr in rawValues)
            {
                var row = new List<string>(rowArr.Count);
                foreach (var cell in rowArr)
                {
                    row.Add(cell.Type == JTokenType.Null ? "" : (string)cell);
                }
                result.Add(row);
            }
            return result;
        }

        // ── HTTP ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends an HTTP request using <see cref="UnityWebRequest"/> and returns the response body.
        /// Must be called from the Unity main thread (editor context).
        /// Throws <see cref="GoogleSheetsApiException"/> on HTTP errors.
        /// </summary>
        private static Task<string> SendRequestAsync(
            string method,
            string url,
            string authHeader,
            string jsonBody)
        {
            var tcs = new TaskCompletionSource<string>();

            EditorApplication.delayCall += () =>
            {
                UnityWebRequest req;
                if (method == "GET")
                {
                    req = UnityWebRequest.Get(url);
                }
                else
                {
                    byte[] bodyBytes = jsonBody != null ? Encoding.UTF8.GetBytes(jsonBody) : Array.Empty<byte>();
                    req = new UnityWebRequest(url, method)
                    {
                        uploadHandler   = new UploadHandlerRaw(bodyBytes),
                        downloadHandler = new DownloadHandlerBuffer()
                    };
                    req.SetRequestHeader("Content-Type", "application/json");
                }

                if (!string.IsNullOrEmpty(authHeader))
                {
                    req.SetRequestHeader("Authorization", authHeader);
                }

                var op = req.SendWebRequest();
                op.completed += _ =>
                {
                    long code = req.responseCode;
                    string body = req.downloadHandler?.text ?? "";

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        // Try to extract a friendly message from the error response JSON.
                        string apiMsg = TryExtractApiErrorMessage(body);
                        string display = string.IsNullOrEmpty(apiMsg)
                            ? $"{req.error} — {body}"
                            : apiMsg;
                        tcs.TrySetException(new GoogleSheetsApiException((int)code, display));
                        return;
                    }

                    tcs.TrySetResult(body);
                };
            };

            return tcs.Task;
        }

        private static string TryExtractApiErrorMessage(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            try
            {
                var obj = JObject.Parse(json);
                return (string)obj["error"]?["message"];
            }
            catch
            {
                return null;
            }
        }

        // ── Utilities ──────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the Google Sheet tab name for the given container.
        /// Reads <see cref="GoogleSheetsTabAttribute"/> from the container's concrete type;
        /// falls back to the entry type name if the attribute is absent.
        /// </summary>
        public static string ResolveTabName(IGameDataContainer container)
        {
            var attr = container.GetType().GetCustomAttribute<GoogleSheetsTabAttribute>(inherit: true);
            if (attr != null && !string.IsNullOrWhiteSpace(attr.TabName))
            {
                return attr.TabName;
            }
            return container.EntryType.Name;
        }

        private static void ValidateConfig(GoogleSheetsConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config), "GoogleSheetsConfig is null.");
            }
            if (!config.IsConfigured())
            {
                throw new InvalidOperationException(
                    "GoogleSheetsConfig is not fully configured.\n" +
                    "Make sure SpreadsheetId and the relevant credential fields are filled in.");
            }
        }
    }

    // ── Result type ─────────────────────────────────────────────────────────────

    /// <summary>Result returned by <see cref="GoogleSheetsService"/> push/pull operations.</summary>
    public sealed class SyncResult
    {
        public bool   Success { get; }
        public string Message { get; }

        private SyncResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static SyncResult Ok(string message)   => new SyncResult(true, message);
        public static SyncResult Fail(string message) => new SyncResult(false, message);
    }

    /// <summary>Thrown when the Google Sheets REST API returns a non-success status code.</summary>
    public sealed class GoogleSheetsApiException : Exception
    {
        public int StatusCode { get; }
        public GoogleSheetsApiException(int statusCode, string message)
            : base(message) => StatusCode = statusCode;
    }
}
