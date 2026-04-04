using System;
using System.Threading.Tasks;
using LiveGameDataEditor;
using LiveGameDataEditor.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.GoogleSheets
{
    /// <summary>
    /// Collapsible panel that sits below the main toolbar in <see cref="LiveGameDataEditorWindow"/>.
    /// Lets the designer pick a <see cref="GoogleSheetsConfig"/>, then push or pull with one click.
    ///
    /// The association between a container and a config is stored in EditorPrefs
    /// keyed by the container asset's GUID so it survives domain reloads.
    /// </summary>
    public sealed class GoogleSheetsSyncPanel : VisualElement
    {
        // ── Events ─────────────────────────────────────────────────────────────

        /// <summary>Fired after a successful pull so the window can refresh the table.</summary>
        public event Action OnPullComplete;

        // ── State ──────────────────────────────────────────────────────────────

        private IGameDataContainer _container;
        private string             _containerGuid = "";

        private GoogleSheetsConfig _config;
        private bool               _busy = false;

        // ── UI references ──────────────────────────────────────────────────────

        private ObjectField   _configField;
        private Label         _authBadge;
        private Label         _tabInfoLabel;   // read-only label showing the resolved tab name
        private Button        _pushBtn;
        private Button        _pullBtn;
        private Label         _statusLabel;
        private VisualElement _statusRow;
        private Label         _lastSyncLabel;

        // EditorPrefs key prefix for config-container association
        private const string PrefKeyPrefix = "LiveGameDataEditor.SheetsConfig.";

        // ── Constructor ────────────────────────────────────────────────────────

        public GoogleSheetsSyncPanel()
        {
            AddToClassList("sheets-panel");
            Build();
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called by the window whenever the loaded container changes (including null).
        /// Restores the previously associated config from EditorPrefs.
        /// </summary>
        public void SetContainer(IGameDataContainer container)
        {
            // Unregister auto-push for the old container before switching.
            if (!string.IsNullOrEmpty(_containerGuid))
            {
                GoogleSheetsAutoSaveMonitor.Unregister(_containerGuid);
            }

            _container = container;

            var so = container as ScriptableObject;
            _containerGuid = so != null
                ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(so))
                : "";

            // Restore the config that was last associated with this container.
            GoogleSheetsConfig restoredConfig = null;
            if (!string.IsNullOrEmpty(_containerGuid))
            {
                string configGuid = EditorPrefs.GetString(PrefKeyPrefix + _containerGuid, "");
                if (!string.IsNullOrEmpty(configGuid))
                {
                    string configPath = AssetDatabase.GUIDToAssetPath(configGuid);
                    restoredConfig = AssetDatabase.LoadAssetAtPath<GoogleSheetsConfig>(configPath);
                }
            }

            SetConfig(restoredConfig, saveToPrefs: false);
            RefreshButtons();
            SetStatus("", success: true);
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

            // Read-only label showing which tab this container maps to.
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
                SetConfig(evt.newValue as GoogleSheetsConfig, saveToPrefs: true);
                RefreshButtons();
            });
            configRow.Add(_configField);

            _authBadge = new Label();
            _authBadge.AddToClassList("sheets-auth-badge");
            configRow.Add(_authBadge);

            // "Create new config" convenience button
            var createConfigBtn = new Button(CreateNewConfig) { text = "New…" };
            createConfigBtn.AddToClassList("sheets-new-btn");
            configRow.Add(createConfigBtn);

            Add(configRow);

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

            // Update auto-push registration whenever the config changes.
            GoogleSheetsAutoSaveMonitor.Register(_containerGuid, _container, _config);

            if (saveToPrefs && !string.IsNullOrEmpty(_containerGuid))
            {
                string configGuid = config != null
                    ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config))
                    : "";
                EditorPrefs.SetString(PrefKeyPrefix + _containerGuid, configGuid);
            }
        }

        private void CreateNewConfig()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Google Sheets Config",
                "NewGoogleSheetsConfig",
                "asset",
                "Choose a location for the new GoogleSheetsConfig asset.");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var newConfig = ScriptableObject.CreateInstance<GoogleSheetsConfig>();
            AssetDatabase.CreateAsset(newConfig, path);
            AssetDatabase.SaveAssets();
            SetConfig(newConfig, saveToPrefs: true);
            Selection.activeObject = newConfig;
        }

        // ── Button actions ─────────────────────────────────────────────────────

        private async void OnPushClicked()
        {
            if (_busy || _container == null || _config == null)
            {
                return;
            }
            await RunSync(push: true);
        }

        private async void OnPullClicked()
        {
            if (_busy || _container == null || _config == null)
            {
                return;
            }
            await RunSync(push: false);
        }

        private async Task RunSync(bool push)
        {
            _busy = true;
            SetBusy(true);
            SetStatus(push ? "Pushing…" : "Pulling…", success: true);

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

            if (!push && result.Success)
            {
                OnPullComplete?.Invoke();
            }
        }

        // ── UI helpers ─────────────────────────────────────────────────────────

        private void RefreshButtons()
        {
            bool canSync = _container != null && _config != null && _config.IsConfigured() && !_busy;
            _pushBtn.SetEnabled(canSync);
            _pullBtn.SetEnabled(canSync);
        }

        private void SetBusy(bool busy)
        {
            _pushBtn.SetEnabled(!busy && _container != null && _config != null && _config.IsConfigured());
            _pullBtn.SetEnabled(!busy && _container != null && _config != null && _config.IsConfigured());
        }

        private void SetStatus(string message, bool success)
        {
            if (string.IsNullOrEmpty(message))
            {
                _statusRow.style.display = DisplayStyle.None;
                return;
            }

            _statusRow.style.display = DisplayStyle.Flex;
            string prefix = success ? "✓  " : "✗  ";
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
                return;
            }

            _authBadge.style.display = DisplayStyle.Flex;
            _authBadge.text = _config.AuthModeLabel;
            _authBadge.RemoveFromClassList("sheets-badge--apikey");
            _authBadge.RemoveFromClassList("sheets-badge--sa");
            _authBadge.AddToClassList(_config.AuthMode == GoogleSheetsAuthMode.ApiKey
                ? "sheets-badge--apikey"
                : "sheets-badge--sa");
        }

        private void UpdateTabInfoLabel()
        {
            if (_tabInfoLabel == null)
            {
                return;
            }
            if (_container == null)
            {
                _tabInfoLabel.text = "";
                _tabInfoLabel.style.display = DisplayStyle.None;
                return;
            }
            string tabName = GoogleSheetsService.ResolveTabName(_container);
            _tabInfoLabel.text = $"→ tab: {tabName}";
            _tabInfoLabel.style.display = DisplayStyle.Flex;
        }

        // ── Last-sync persistence ──────────────────────────────────────────────

        private string LastSyncPrefKey => "LiveGameDataEditor.SheetsLastSync." + _containerGuid;

        private void SaveLastSyncTime(string direction)
        {
            if (string.IsNullOrEmpty(_containerGuid))
            {
                return;
            }
            string value = $"{direction} {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            EditorPrefs.SetString(LastSyncPrefKey, value);
        }

        private void UpdateLastSyncLabel()
        {
            if (string.IsNullOrEmpty(_containerGuid))
            {
                _lastSyncLabel.text = "";
                return;
            }
            string stored = EditorPrefs.GetString(LastSyncPrefKey, "");
            _lastSyncLabel.text = string.IsNullOrEmpty(stored) ? "Never synced" : "Last: " + stored;
        }
    }
}
