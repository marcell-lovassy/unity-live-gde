using UnityEngine;

namespace LiveGameDataEditor.GoogleSheets
{
    /// <summary>
    /// Stores the connection settings needed to reach a Google Spreadsheet.
    /// Create via <c>Assets / Create / Live Game Data / Google Sheets Config</c>.
    ///
    /// One config asset is shared across all data containers in your project.
    /// Each container declares its own tab name via <see cref="GoogleSheetsTabAttribute"/>:
    /// <code>
    /// [GoogleSheetsTab("Enemies")]
    /// public class EnemyDataContainer : GameDataContainerBase&lt;EnemyDataEntry&gt; { }
    /// </code>
    /// If the attribute is absent the service falls back to the entry type name.
    ///
    /// Credentials (API key or service account JSON path) are stored in this asset.
    /// If you use a Service Account, keep the <c>.json</c> key file outside <c>Assets/</c>
    /// and add it to <c>.gitignore</c> so it is never committed to source control.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Live Game Data/Google Sheets Config",
        fileName = "NewGoogleSheetsConfig",
        order    = 51)]
    public class GoogleSheetsConfig : ScriptableObject
    {
        // ── Spreadsheet ────────────────────────────────────────────────────────

        [Tooltip("The ID from the spreadsheet URL:\nhttps://docs.google.com/spreadsheets/d/<ID>/edit\n\n" +
                 "This config is shared across all containers. Each container maps to its own tab " +
                 "via [GoogleSheetsTab(\"TabName\")] on the container class.")]
        public string SpreadsheetId = "";

        [Tooltip("When true, the first row of each tab is treated as a header row " +
                 "containing field names. Always keep this on.")]
        public bool HasHeaderRow = true;

        // ── Authentication ─────────────────────────────────────────────────────

        [Tooltip("API Key: read-only access; the sheet must be shared as 'Anyone with the link can view'.\n" +
                 "Service Account: read + write access to private sheets. " +
                 "Share the sheet with the service account e-mail address.")]
        public GoogleSheetsAuthMode AuthMode = GoogleSheetsAuthMode.ApiKey;

        [Tooltip("Google Cloud API Key. Only used in API Key mode.\n" +
                 "Create one at console.cloud.google.com → APIs & Services → Credentials.")]
        public string ApiKey = "";

        [Tooltip("Absolute or project-relative path to the Service Account JSON key file " +
                 "(e.g. '.google/my-project-key.json').\n" +
                 "Download it from console.cloud.google.com → IAM → Service Accounts → Keys.\n" +
                 "Add the file to .gitignore — do NOT commit credentials to source control.")]
        public string ServiceAccountJsonPath = "";

        // ── Sync behaviour ────────────────────────────────────────────────────

        [Tooltip("Automatically push to the sheet every time a container asset is saved (Ctrl+S).")]
        public bool AutoPushOnSave = false;

        // ── Internal ──────────────────────────────────────────────────────────

        /// <summary>Returns true when the minimum required settings are filled in.</summary>
        public bool IsConfigured()
        {
            if (string.IsNullOrWhiteSpace(SpreadsheetId))
            {
                return false;
            }
            if (AuthMode == GoogleSheetsAuthMode.ApiKey && string.IsNullOrWhiteSpace(ApiKey))
            {
                return false;
            }
            if (AuthMode == GoogleSheetsAuthMode.ServiceAccount && string.IsNullOrWhiteSpace(ServiceAccountJsonPath))
            {
                return false;
            }
            return true;
        }

        /// <summary>Human-readable label for the current auth mode used in the UI.</summary>
        public string AuthModeLabel => AuthMode switch
        {
            GoogleSheetsAuthMode.ApiKey         => "API Key",
            GoogleSheetsAuthMode.ServiceAccount => "Service Account",
            _                                   => "Unknown"
        };
    }

    public enum GoogleSheetsAuthMode
    {
        /// <summary>Read-only access using a Google Cloud API Key. The sheet must be public.</summary>
        ApiKey,

        /// <summary>Read + write access using a Service Account JSON key file.</summary>
        ServiceAccount,
    }
}

