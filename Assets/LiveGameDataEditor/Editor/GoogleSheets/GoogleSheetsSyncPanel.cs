using System;
using System.Threading;
using System.Threading.Tasks;
using LiveGameDataEditor.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.GoogleSheets
{
    /// <summary>
    ///     Collapsible panel that sits below the main toolbar in <see cref="LiveGameDataEditorWindow" />.
    ///     Lets the designer pick a <see cref="GoogleSheetsConfig" />, sign in (OAuth), then push or pull
    ///     with one click.
    ///     The <see cref="GoogleSheetsConfig" /> is shared across all containers in the editor window —
    ///     switching containers (browser tabs) does not change or clear the config.
    ///     The association is stored in a single EditorPrefs key per project.
    /// </summary>
    public sealed class GoogleSheetsSyncPanel : VisualElement
    {
        // Single shared EditorPrefs key — config is project-wide, not per-container.
        private const string SharedConfigPrefKey = "LiveGameDataEditor.SheetsConfig";
        private Label _authBadge;
        private bool _busy;

        private GoogleSheetsConfig _config;

        // ── UI references ──────────────────────────────────────────────────────

        private ObjectField _configField;

        // ── State ──────────────────────────────────────────────────────────────

        private IGameDataContainer _container;
        private string _containerGuid = "";
        private Label _lastSyncLabel;
        private VisualElement _oauthRow;
        private Label _oauthStatusLabel;
        private Button _pullBtn;
        private Button _pushBtn;
        private Button _signInBtn;
        private Button _signOutBtn;
        private Label _statusLabel;
        private VisualElement _statusRow;
        private Label _tabInfoLabel;

        // ── Constructor ────────────────────────────────────────────────────────

        public GoogleSheetsSyncPanel()
        {
            AddToClassList("sheets-panel");
            Build();
        }

        // ── Last-sync persistence ──────────────────────────────────────────────

        private string LastSyncPrefKey => "LiveGameDataEditor.SheetsLastSync." + _containerGuid;
        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Fired after a successful pull so the window can refresh the table.</summary>
        public event Action OnPullComplete;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        ///     Called by the window whenever the loaded container changes (including null).
        ///     The config is shared — it is kept when switching containers and only restored
        ///     from EditorPrefs on the very first call (cold start with no config loaded yet).
        /// </summary>
        public void SetContainer(IGameDataContainer container)
        {
            if (!string.IsNullOrEmpty(_containerGuid)) GoogleSheetsAutoSaveMonitor.Unregister(_containerGuid);

            _container = container;

            var so = container as ScriptableObject;
            _containerGuid = so != null
                ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(so))
                : "";

            if (_config == null)
            {
                // Cold start — restore the shared config from EditorPrefs.
                GoogleSheetsConfig restoredConfig = null;
                var configGuid = EditorPrefs.GetString(SharedConfigPrefKey, "");
                if (!string.IsNullOrEmpty(configGuid))
                {
                    var configPath = AssetDatabase.GUIDToAssetPath(configGuid);
                    restoredConfig = AssetDatabase.LoadAssetAtPath<GoogleSheetsConfig>(configPath);
                }

                SetConfig(restoredConfig, false);
            }
            else
            {
                // Container switch — keep the existing config; just re-register auto-push.
                GoogleSheetsAutoSaveMonitor.Register(_containerGuid, _container, _config);
            }

            RefreshButtons();
            SetStatus("", true);
            UpdateLastSyncLabel();
            UpdateTabInfoLabel();
        }

        // ── UI construction ────────────────────────────────────────────────────

        private void Build()
        {
            // ── Header row ─────────────────────────────────────────────────────
            var headerRow = new VisualElement();
            headerRow.AddToClassList("sheets-panel-header");

            var titleLabel = new Label("☁  Google Sheets Sync");
            titleLabel.AddToClassList("sheets-panel-title");
            headerRow.Add(titleLabel);

            _tabInfoLabel = new Label("");
            _tabInfoLabel.AddToClassList("sheets-tab-info");
            headerRow.Add(_tabInfoLabel);

            Add(headerRow);

            // ── Config row ─────────────────────────────────────────────────────
            var configRow = new VisualElement();
            configRow.AddToClassList("sheets-row");

            var configLabel = new Label("Config");
            configLabel.AddToClassList("sheets-row-label");
            configRow.Add(configLabel);

            _configField = new ObjectField
            {
                objectType = typeof(GoogleSheetsConfig),
                allowSceneObjects = false
            };
            _configField.AddToClassList("sheets-config-field");
            _configField.RegisterValueChangedCallback(evt =>
            {
                SetConfig(evt.newValue as GoogleSheetsConfig, true);
                RefreshButtons();
            });
            configRow.Add(_configField);

            _authBadge = new Label();
            _authBadge.AddToClassList("sheets-auth-badge");
            configRow.Add(_authBadge);

            var createConfigBtn = new Button(CreateNewConfig) { text = "New…" };
            createConfigBtn.AddToClassList("sheets-new-btn");
            configRow.Add(createConfigBtn);

            Add(configRow);

            // ── OAuth auth-state row (hidden for non-OAuth modes) ──────────────
            _oauthRow = new VisualElement();
            _oauthRow.AddToClassList("sheets-row");
            _oauthRow.AddToClassList("sheets-oauth-row");
            _oauthRow.style.display = DisplayStyle.None;

            _oauthStatusLabel = new Label("");
            _oauthStatusLabel.AddToClassList("sheets-oauth-status");
            _oauthRow.Add(_oauthStatusLabel);

            _signInBtn = new Button(OnSignInClicked) { text = "Sign in with Google ▶" };
            _signInBtn.AddToClassList("sheets-sign-in-btn");
            _oauthRow.Add(_signInBtn);

            _signOutBtn = new Button(OnSignOutClicked) { text = "Sign out" };
            _signOutBtn.AddToClassList("sheets-sign-out-btn");
            _oauthRow.Add(_signOutBtn);

            Add(_oauthRow);

            // ── Action row ─────────────────────────────────────────────────────
            var actionRow = new VisualElement();
            actionRow.AddToClassList("sheets-row");

            _pushBtn = new Button(OnPushClicked) { text = "↑ Push to Sheet" };
            _pushBtn.AddToClassList("sheets-action-btn");
            _pushBtn.AddToClassList("sheets-push-btn");
            actionRow.Add(_pushBtn);

            _pullBtn = new Button(OnPullClicked) { text = "↓ Pull from Sheet" };
            _pullBtn.AddToClassList("sheets-action-btn");
            _pullBtn.AddToClassList("sheets-pull-btn");
            actionRow.Add(_pullBtn);

            _lastSyncLabel = new Label("");
            _lastSyncLabel.AddToClassList("sheets-last-sync");
            actionRow.Add(_lastSyncLabel);

            Add(actionRow);

            // ── Status row ─────────────────────────────────────────────────────
            _statusRow = new VisualElement();
            _statusRow.AddToClassList("sheets-status-row");
            _statusRow.style.display = DisplayStyle.None;

            _statusLabel = new Label("");
            _statusLabel.AddToClassList("sheets-status-label");
            _statusRow.Add(_statusLabel);

            Add(_statusRow);

            RefreshButtons();
            UpdateAuthBadge();
        }

        // ── Config management ──────────────────────────────────────────────────

        private void SetConfig(GoogleSheetsConfig config, bool saveToPrefs)
        {
            _config = config;
            _configField.SetValueWithoutNotify(config);
            UpdateAuthBadge();

            GoogleSheetsAutoSaveMonitor.Register(_containerGuid, _container, _config);

            if (saveToPrefs)
            {
                var configGuid = config != null
                    ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config))
                    : "";
                EditorPrefs.SetString(SharedConfigPrefKey, configGuid);
            }
        }

        private void CreateNewConfig()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Google Sheets Config",
                "NewGoogleSheetsConfig",
                "asset",
                "Choose a location for the new GoogleSheetsConfig asset.");

            if (string.IsNullOrEmpty(path)) return;

            var newConfig = ScriptableObject.CreateInstance<GoogleSheetsConfig>();
            AssetDatabase.CreateAsset(newConfig, path);
            AssetDatabase.SaveAssets();
            SetConfig(newConfig, true);
            Selection.activeObject = newConfig;
        }

        // ── OAuth sign-in / sign-out ───────────────────────────────────────────

        private async void OnSignInClicked()
        {
            if (_config == null || _busy) return;

            _busy = true;
            SetBusy(true);
            SetStatus("Opening browser for Google sign-in…", true);

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                await GoogleSheetsAuthService.StartOAuthFlowAsync(_config, cts.Token);
                SetStatus("Signed in successfully.", true);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Sign-in timed out (5 min). Please try again.", false);
            }
            catch (Exception ex)
            {
                SetStatus("Sign-in failed: " + ex.Message, false);
                Debug.LogError("[LiveGameDataEditor] Google OAuth: " + ex.Message);
            }
            finally
            {
                _busy = false;
                SetBusy(false);
                UpdateAuthBadge();
                RefreshButtons();
            }
        }

        private void OnSignOutClicked()
        {
            if (_config == null) return;
            GoogleSheetsAuthService.SignOut(_config);
            UpdateAuthBadge();
            RefreshButtons();
            SetStatus("Signed out.", true);
        }

        // ── Push / Pull ────────────────────────────────────────────────────────

        private async void OnPushClicked()
        {
            if (_busy || _container == null || _config == null) return;
            await RunSync(true);
        }

        private async void OnPullClicked()
        {
            if (_busy || _container == null || _config == null) return;
            await RunSync(false);
        }

        private async Task RunSync(bool push)
        {
            _busy = true;
            SetBusy(true);
            SetStatus(push ? "Pushing…" : "Pulling…", true);

            SyncResult result;
            try
            {
                result = push
                    ? await GoogleSheetsService.PushAsync(_container, _config)
                    : await GoogleSheetsService.PullAsync(_container, _config);
            }
            catch (Exception ex)
            {
                result = SyncResult.Fail("Unexpected error: " + ex.Message);
            }

            _busy = false;
            SetBusy(false);
            SetStatus(result.Message, result.Success);
            SaveLastSyncTime(push ? "↑" : "↓");
            UpdateLastSyncLabel();

            if (!push && result.Success) OnPullComplete?.Invoke();
        }

        // ── UI helpers ─────────────────────────────────────────────────────────

        private void RefreshButtons()
        {
            if (_config == null || _container == null)
            {
                _pushBtn.SetEnabled(false);
                _pullBtn.SetEnabled(false);
                return;
            }

            var isConfigured = _config.IsConfigured();
            var isOAuthReady = _config.AuthMode != GoogleSheetsAuthMode.OAuth
                               || GoogleSheetsAuthService.IsOAuthAuthenticated(_config);
            var canSync = isConfigured && isOAuthReady && !_busy;

            // API Key is Pull-only; OAuth and Service Account support Push.
            _pushBtn.SetEnabled(canSync && _config.AuthMode != GoogleSheetsAuthMode.ApiKey);
            _pullBtn.SetEnabled(canSync);
        }

        private void SetBusy(bool busy)
        {
            var canSync = _container != null && _config != null && _config.IsConfigured() && !busy;
            _pushBtn.SetEnabled(canSync && _config?.AuthMode != GoogleSheetsAuthMode.ApiKey);
            _pullBtn.SetEnabled(canSync);
        }

        private void SetStatus(string message, bool success)
        {
            if (string.IsNullOrEmpty(message))
            {
                _statusRow.style.display = DisplayStyle.None;
                return;
            }

            _statusRow.style.display = DisplayStyle.Flex;
            var prefix = success ? "✓  " : "✗  ";
            _statusLabel.text = prefix + message;
            _statusLabel.RemoveFromClassList("sheets-status--ok");
            _statusLabel.RemoveFromClassList("sheets-status--error");
            _statusLabel.AddToClassList(success ? "sheets-status--ok" : "sheets-status--error");
        }

        private void UpdateAuthBadge()
        {
            if (_config == null)
            {
                _authBadge.text = "";
                _authBadge.style.display = DisplayStyle.None;
                UpdateOAuthRow();
                return;
            }

            _authBadge.style.display = DisplayStyle.Flex;
            _authBadge.text = _config.AuthModeLabel;
            _authBadge.RemoveFromClassList("sheets-badge--apikey");
            _authBadge.RemoveFromClassList("sheets-badge--oauth");
            _authBadge.RemoveFromClassList("sheets-badge--sa");
            _authBadge.AddToClassList(_config.AuthMode switch
            {
                GoogleSheetsAuthMode.ApiKey => "sheets-badge--apikey",
                GoogleSheetsAuthMode.OAuth => "sheets-badge--oauth",
                GoogleSheetsAuthMode.ServiceAccount => "sheets-badge--sa",
                _ => "sheets-badge--apikey"
            });

            UpdateOAuthRow();
        }

        private void UpdateOAuthRow()
        {
            if (_config == null || _config.AuthMode != GoogleSheetsAuthMode.OAuth)
            {
                _oauthRow.style.display = DisplayStyle.None;
                return;
            }

            _oauthRow.style.display = DisplayStyle.Flex;
            var isAuthed = GoogleSheetsAuthService.IsOAuthAuthenticated(_config);

            _oauthStatusLabel.text = isAuthed ? "✓  Signed in to Google" : "⚠  Not signed in";
            _oauthStatusLabel.RemoveFromClassList("sheets-oauth--ok");
            _oauthStatusLabel.RemoveFromClassList("sheets-oauth--warn");
            _oauthStatusLabel.AddToClassList(isAuthed ? "sheets-oauth--ok" : "sheets-oauth--warn");

            _signInBtn.style.display = isAuthed ? DisplayStyle.None : DisplayStyle.Flex;
            _signOutBtn.style.display = isAuthed ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateTabInfoLabel()
        {
            if (_tabInfoLabel == null) return;
            if (_container == null)
            {
                _tabInfoLabel.text = "";
                _tabInfoLabel.style.display = DisplayStyle.None;
                return;
            }

            var tabName = GoogleSheetsService.ResolveTabName(_container);
            _tabInfoLabel.text = $"→ tab: {tabName}";
            _tabInfoLabel.style.display = DisplayStyle.Flex;
        }

        private void SaveLastSyncTime(string direction)
        {
            if (string.IsNullOrEmpty(_containerGuid)) return;
            EditorPrefs.SetString(LastSyncPrefKey, $"{direction} {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        private void UpdateLastSyncLabel()
        {
            if (string.IsNullOrEmpty(_containerGuid))
            {
                _lastSyncLabel.text = "";
                return;
            }

            var stored = EditorPrefs.GetString(LastSyncPrefKey, "");
            _lastSyncLabel.text = string.IsNullOrEmpty(stored) ? "Never synced" : "Last: " + stored;
        }
    }
}