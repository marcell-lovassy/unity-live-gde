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

        [Tooltip("API Key: read-only access (Pull only); the sheet must be public.\n" +
                 "OAuth: read + write access via 'Sign in with Google' — recommended for designers.\n" +
                 "Service Account: read + write via a JSON key file — suitable for CI/CD.")]
        public GoogleSheetsAuthMode AuthMode = GoogleSheetsAuthMode.OAuth;

        // ── API Key ────────────────────────────────────────────────────────────

        [Tooltip("Google Cloud API Key. Only used in API Key mode.\n" +
                 "Create one at console.cloud.google.com → APIs & Services → Credentials.\n" +
                 "The sheet must be shared as 'Anyone with the link can view'. Push is not available.")]
        public string ApiKey = "";

        // ── OAuth 2.0 ──────────────────────────────────────────────────────────

        [Tooltip("OAuth 2.0 Client ID.\n" +
                 "In Google Cloud Console: APIs & Services → Credentials → Create → OAuth 2.0 Client ID.\n" +
                 "Application type: Desktop app.")]
        public string OAuthClientId = "";

        [Tooltip("OAuth 2.0 Client Secret paired with the Client ID above.\n" +
                 "Note: for Desktop apps this is not considered truly secret by Google's own documentation.")]
        public string OAuthClientSecret = "";

        // ── Service Account (legacy / CI-CD) ───────────────────────────────────

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
            if (AuthMode == GoogleSheetsAuthMode.OAuth &&
                (string.IsNullOrWhiteSpace(OAuthClientId) || string.IsNullOrWhiteSpace(OAuthClientSecret)))
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
            GoogleSheetsAuthMode.OAuth          => "OAuth",
            GoogleSheetsAuthMode.ServiceAccount => "Service Account",
            _                                   => "Unknown"
        };
    }

    public enum GoogleSheetsAuthMode
    {
        /// <summary>Read-only, Pull only. The sheet must be shared as "Anyone with the link can view".</summary>
        ApiKey,

        /// <summary>Read + Write. Browser-based "Sign in with Google" flow. Recommended for designers.</summary>
        OAuth,

        /// <summary>Read + Write. Service Account JSON key file. Suitable for CI/CD pipelines.</summary>
        ServiceAccount,
    }
}

