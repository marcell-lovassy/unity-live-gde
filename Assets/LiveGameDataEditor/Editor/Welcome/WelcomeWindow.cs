using System;
using System.IO;
using LiveGameDataEditor.GoogleSheets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace LiveGameDataEditor.Editor
{
    /// <summary>
    /// Welcome / onboarding window shown on first install and reopenable from the menu.
    ///
    /// Four tabs:
    ///   Welcome       — elevator pitch + feature overview
    ///   Quick Start   — step-by-step guide to first use
    ///   Sheets Setup  — interactive wizard: creates GoogleSheetsConfig and fills it in inline
    ///   About         — version info + external links
    ///
    /// Auto-shown via <see cref="WelcomeWindowInitializer"/> on the first editor launch
    /// after installation. Suppressed when the "Don't show on startup" checkbox is ticked.
    /// </summary>
    public sealed class WelcomeWindow : EditorWindow
    {
        private const string Version       = "1.0.0";
        private const string SuppressPref  = "LiveGameDataEditor.WelcomeSuppressed";
        private const string ShownPref     = "LiveGameDataEditor.WelcomeShown";

        private const string CloudConsoleUrl     = "https://console.cloud.google.com";
        private const string CloudCredentialsUrl = "https://console.cloud.google.com/apis/credentials";
        private const string CloudConsentUrl     = "https://console.cloud.google.com/apis/credentials/consent";
        private const string CloudSheetsApiUrl   = "https://console.cloud.google.com/apis/library/sheets.googleapis.com";

        // ── Page identifiers ───────────────────────────────────────────────────

        private enum Page { Welcome, QuickStart, SheetsSetup, About }

        private Page             _activePage = Page.Welcome;
        private VisualElement    _contentArea;
        private VisualElement[]  _tabButtons;

        // ── Sheets wizard state ────────────────────────────────────────────────

        private GoogleSheetsConfig _wizardConfig;
        private string             _wizardConfigPath;
        private string             _wizardFolderPath = "Assets";
        private VisualElement      _wizardAuthCredentials; // swapped on auth-mode change

        // ── Menu item ─────────────────────────────────────────────────────────

        [MenuItem("Tools/GDE/Welcome", priority = 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<WelcomeWindow>(utility: true, title: "Live Game Data Editor");
            window.minSize = new Vector2(660, 580);
            window.maxSize = new Vector2(660, 580);
            window.ShowUtility();
        }

        /// <summary>Called by <see cref="WelcomeWindowInitializer"/> on first install.</summary>
        public static void ShowOnStartup()
        {
            if (EditorPrefs.GetBool(SuppressPref, false))
            {
                return;
            }
            EditorPrefs.SetBool(ShownPref, true);
            ShowWindow();
        }

        // ── Window lifecycle ───────────────────────────────────────────────────

        private void CreateGUI()
        {
            // Load the shared stylesheet.
            string ussPath = AssetDatabase.FindAssets("LiveGameDataEditor t:StyleSheet") is { Length: > 0 } guids
                ? AssetDatabase.GUIDToAssetPath(guids[0])
                : null;

            if (!string.IsNullOrEmpty(ussPath))
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(ussPath);
                if (sheet != null)
                {
                    rootVisualElement.styleSheets.Add(sheet);
                }
            }

            rootVisualElement.AddToClassList("welcome-root");
            rootVisualElement.style.flexDirection = FlexDirection.Column;

            BuildBanner();
            BuildBody();
            BuildFooter();

            NavigateTo(Page.Welcome);
        }

        // ── Banner ─────────────────────────────────────────────────────────────

        private void BuildBanner()
        {
            var banner = new VisualElement();
            banner.AddToClassList("welcome-banner");

            var title = new Label("Live Game Data Editor");
            title.AddToClassList("welcome-banner-title");
            banner.Add(title);

            var subtitle = new Label("Spreadsheet-style game data editing — right inside Unity");
            subtitle.AddToClassList("welcome-banner-subtitle");
            banner.Add(subtitle);

            rootVisualElement.Add(banner);
        }

        // ── Body (sidebar + content) ───────────────────────────────────────────

        private void BuildBody()
        {
            var body = new VisualElement();
            body.AddToClassList("welcome-body");
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow      = 1;

            BuildSidebar(body);
            BuildContentArea(body);

            rootVisualElement.Add(body);
        }

        private void BuildSidebar(VisualElement parent)
        {
            var sidebar = new VisualElement();
            sidebar.AddToClassList("welcome-sidebar");

            var pages = new[]
            {
                (Page.Welcome,     "🏠  Welcome"),
                (Page.QuickStart,  "⚡  Quick Start"),
                (Page.SheetsSetup, "☁  Sheets Setup"),
                (Page.About,       "ℹ  About"),
            };

            _tabButtons = new VisualElement[pages.Length];

            for (int i = 0; i < pages.Length; i++)
            {
                var (page, label) = pages[i];
                int captured = i;
                Page capturedPage = page;

                var btn = new Button(() => NavigateTo(capturedPage));
                btn.text = label;
                btn.AddToClassList("welcome-tab-btn");
                sidebar.Add(btn);

                _tabButtons[i] = btn;
            }

            parent.Add(sidebar);
        }

        private void BuildContentArea(VisualElement parent)
        {
            _contentArea = new VisualElement();
            _contentArea.AddToClassList("welcome-content");
            parent.Add(_contentArea);
        }

        // ── Footer ─────────────────────────────────────────────────────────────

        private void BuildFooter()
        {
            var footer = new VisualElement();
            footer.AddToClassList("welcome-footer");

            bool suppressed = EditorPrefs.GetBool(SuppressPref, false);
            var toggle = new Toggle("Don't show on startup")
            {
                value = suppressed
            };
            toggle.AddToClassList("welcome-suppress-toggle");
            toggle.RegisterValueChangedCallback(evt =>
            {
                EditorPrefs.SetBool(SuppressPref, evt.newValue);
            });
            footer.Add(toggle);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            footer.Add(spacer);

            var closeBtn = new Button(Close) { text = "Close" };
            closeBtn.AddToClassList("welcome-close-btn");
            footer.Add(closeBtn);

            rootVisualElement.Add(footer);
        }

        // ── Navigation ─────────────────────────────────────────────────────────

        private void NavigateTo(Page page)
        {
            _activePage = page;
            _contentArea.Clear();

            switch (page)
            {
                case Page.Welcome:     BuildWelcomePage();     break;
                case Page.QuickStart:  BuildQuickStartPage();  break;
                case Page.SheetsSetup: BuildSheetsSetupPage(); break;
                case Page.About:       BuildAboutPage();       break;
            }

            // Update tab button active states.
            var pageValues = (Page[])Enum.GetValues(typeof(Page));
            for (int i = 0; i < _tabButtons.Length && i < pageValues.Length; i++)
            {
                _tabButtons[i].RemoveFromClassList("welcome-tab-btn--active");
                if (pageValues[i] == page)
                {
                    _tabButtons[i].AddToClassList("welcome-tab-btn--active");
                }
            }
        }

        // ── Welcome page ───────────────────────────────────────────────────────

        private void BuildWelcomePage()
        {
            var scroll = new ScrollView();
            scroll.AddToClassList("welcome-page-scroll");

            scroll.Add(Heading("Welcome!"));
            scroll.Add(Para(
                "Live Game Data Editor brings a spreadsheet-style workflow to Unity. " +
                "Edit your ScriptableObject data in a resizable, sortable, filterable table — " +
                "No custom tools or coding required."));

            scroll.Add(Heading("Features"));

            var features = new[]
            {
                ("📋", "Table editor",       "Edit game data like Excel, directly in Unity, sort, filter and search"),
                ("✅", "Validation",         "Prevent broken data (duplicate IDs, invalid values)"),
                ("🔄", "Multi-container",    "Switch between data assets with a browser panel"),
                ("📊", "CSV & JSON",         "Import and export in one click"),
                ("☁",  "Google Sheets Sync", "Sign in with Google and sync instantly"),
                ("↕",  "Drag to reorder",   "Reorder rows with a handle — undo supported"),
            };

            foreach (var (icon, title, desc) in features)
            {
                scroll.Add(FeatureRow(icon, title, desc));
            }

            var openBtn = new Button(() =>
            {
                Close();
                EditorApplication.ExecuteMenuItem("Tools/GDE/Open Editor");
            })
            { text = "Open Game Data Editor  →" };
            openBtn.AddToClassList("welcome-cta-btn");
            scroll.Add(openBtn);

            _contentArea.Add(scroll);
        }

        // ── Quick Start page ───────────────────────────────────────────────────

        private void BuildQuickStartPage()
        {
            var scroll = new ScrollView();
            scroll.AddToClassList("welcome-page-scroll");

            scroll.Add(Heading("Quick Start"));
            scroll.Add(Para("Get up and running in under two minutes."));

            scroll.Add(Step(1, "Create a data container",
                "In the Project window:\n" +
                "Assets → Create → Live Game Data / Enemy Data Container\n" +
                "(or any container type you've defined)"));

            scroll.Add(Step(2, "Open the editor",
                "Tools → GDE → Open Editor \n" +
                "The editor opens as a dockable window."));

            scroll.Add(Step(3, "Load your container",
                "Click the folder icon or drag your container asset into the editor.\n" +
                "The table populates automatically from the container's entry type."));

            scroll.Add(Step(4, "Edit your data",
                "Click any cell to edit inline. Add rows with the + button.\n" +
                "Changes are saved to the ScriptableObject immediately (Ctrl+Z to undo)."));

            scroll.Add(Step(5, "Export (optional)",
                "Use the toolbar buttons to:\n" +
                "• Export → JSON  or  Export → CSV\n" +
                "• Import ← JSON  or  Import ← CSV\n" +
                "• ☁ Sheets → Push ↑ / Pull ↓  (after Sheets Setup)"));

            scroll.Add(Tip("Add your own data types by creating a class that inherits " +
                           "GameDataContainerBase<YourEntryType>."));

            _contentArea.Add(scroll);
        }

        // ── Google Sheets Setup page ───────────────────────────────────────────

        private void BuildSheetsSetupPage()
        {
            FindOrLoadWizardConfig();

            var scroll = new ScrollView();
            scroll.AddToClassList("welcome-page-scroll");

            scroll.Add(Heading("Google Sheets Setup"));
            scroll.Add(Para(
                "Connect your data to Google Sheets in a few steps. " +
                "Your credentials stay in your own Google account — nothing is shared."));

            if (_wizardConfig == null)
            {
                scroll.Add(BuildConfigCreatePanel());
            }
            else
            {
                scroll.Add(BuildConfigBar());
                scroll.Add(BuildConfigForm());
            }

            _contentArea.Add(scroll);
        }

        // ── Wizard: config creation ────────────────────────────────────────────

        private VisualElement BuildConfigCreatePanel()
        {
            var box = new VisualElement();
            box.AddToClassList("welcome-wizard-create-box");

            var title = new Label("Step 1 of 1 — Create your config asset");
            title.AddToClassList("welcome-wizard-section-title");
            box.Add(title);

            box.Add(Para("This file stores your Spreadsheet ID and credentials. " +
                         "One config is shared across all your data containers."));

            // Folder picker row
            var folderRow = new VisualElement();
            folderRow.style.flexDirection = FlexDirection.Row;
            folderRow.style.alignItems    = Align.Center;
            folderRow.style.marginTop     = 8;
            folderRow.style.marginBottom  = 8;

            var folderLabel = new Label("Save to:");
            folderLabel.AddToClassList("welcome-wizard-field-label");
            folderLabel.style.marginRight = 8;
            folderLabel.style.minWidth    = 60;
            folderRow.Add(folderLabel);

            var folderDisplay = new Label(_wizardFolderPath);
            folderDisplay.AddToClassList("welcome-wizard-folder-display");
            folderDisplay.style.flexGrow = 1;
            folderRow.Add(folderDisplay);

            var browseBtn = new Button(() =>
            {
                string chosen = EditorUtility.OpenFolderPanel("Choose folder", _wizardFolderPath, "");
                if (!string.IsNullOrEmpty(chosen))
                {
                    // Convert absolute path to project-relative (Assets/...)
                    string projectPath = Application.dataPath.Replace("/Assets", "");
                    if (chosen.StartsWith(projectPath))
                    {
                        _wizardFolderPath = chosen.Substring(projectPath.Length + 1);
                    }
                    else
                    {
                        _wizardFolderPath = "Assets";
                    }
                    folderDisplay.text = _wizardFolderPath;
                }
            })
            { text = "Browse…" };
            browseBtn.AddToClassList("welcome-wizard-browse-btn");
            folderRow.Add(browseBtn);

            box.Add(folderRow);

            var createBtn = new Button(() =>
            {
                CreateWizardConfig(_wizardFolderPath);
                RebuildSheetsPage();
            })
            { text = "✓  Create GoogleSheetsConfig" };
            createBtn.AddToClassList("welcome-cta-btn");
            box.Add(createBtn);

            return box;
        }

        private void CreateWizardConfig(string folder)
        {
            string assetPath = AssetDatabase.GenerateUniqueAssetPath(
                Path.Combine(folder, "GoogleSheetsConfig.asset").Replace("\\", "/"));

            var config = CreateInstance<GoogleSheetsConfig>();
            AssetDatabase.CreateAsset(config, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _wizardConfig     = config;
            _wizardConfigPath = assetPath;

            // Ping the new asset in the Project window
            EditorGUIUtility.PingObject(config);
        }

        private void FindOrLoadWizardConfig()
        {
            if (_wizardConfig != null)
            {
                return;
            }

            var guids = AssetDatabase.FindAssets("t:GoogleSheetsConfig");
            if (guids.Length == 0)
            {
                return;
            }

            _wizardConfigPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            _wizardConfig     = AssetDatabase.LoadAssetAtPath<GoogleSheetsConfig>(_wizardConfigPath);
        }

        private void RebuildSheetsPage()
        {
            _contentArea.Clear();
            BuildSheetsSetupPage();
        }

        // ── Wizard: config info bar ────────────────────────────────────────────

        private VisualElement BuildConfigBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("welcome-wizard-config-bar");

            var label = new Label($"✓  {Path.GetFileName(_wizardConfigPath)}   ·   {_wizardConfigPath}");
            label.AddToClassList("welcome-wizard-config-bar-label");
            bar.Add(label);

            var pingBtn = new Button(() => EditorGUIUtility.PingObject(_wizardConfig))
            { text = "Show in Project" };
            pingBtn.AddToClassList("welcome-wizard-ping-btn");
            bar.Add(pingBtn);

            return bar;
        }

        // ── Wizard: inline config form ─────────────────────────────────────────

        private VisualElement BuildConfigForm()
        {
            var form = new VisualElement();

            // ── Step 1: Spreadsheet ID ─────────────────────────────────────────
            var step1 = WizardSection("1", "Your Google Spreadsheet");
            step1.Add(Para("Open your Google Sheet in a browser. The Spreadsheet ID is the " +
                           "segment between /d/ and /edit in the URL."));
            step1.Add(Para("Example: docs.google.com/spreadsheets/d/\u00AB ID \u00BB/edit"));
            step1.Add(LinkButton("Open Google Sheets →", "https://sheets.google.com"));
            step1.Add(WizardField("Spreadsheet ID", _wizardConfig.SpreadsheetId, v =>
            {
                Undo.RecordObject(_wizardConfig, "Set Spreadsheet ID");
                _wizardConfig.SpreadsheetId = v;
                SaveWizardConfig();
            }));
            form.Add(step1);

            // ── Step 2: Auth mode ──────────────────────────────────────────────
            var step2 = WizardSection("2", "Authentication Method");
            step2.Add(Para("OAuth is recommended for designers: sign in once with your Google account."));

            var authRow = new VisualElement();
            authRow.AddToClassList("welcome-wizard-auth-row");

            _wizardAuthCredentials = new VisualElement();

            foreach (var mode in new[] { GoogleSheetsAuthMode.OAuth, GoogleSheetsAuthMode.ApiKey, GoogleSheetsAuthMode.ServiceAccount })
            {
                GoogleSheetsAuthMode captured = mode;
                bool active = _wizardConfig.AuthMode == mode;

                string label = mode switch
                {
                    GoogleSheetsAuthMode.OAuth          => "● OAuth  (recommended)",
                    GoogleSheetsAuthMode.ApiKey         => "API Key  (read-only)",
                    GoogleSheetsAuthMode.ServiceAccount => "Service Account  (CI/CD)",
                    _                                   => mode.ToString()
                };

                var btn = new Button();
                btn.text = label;
                btn.clicked += () =>
                {
                    Undo.RecordObject(_wizardConfig, "Set Auth Mode");
                    _wizardConfig.AuthMode = captured;
                    SaveWizardConfig();
                    // Rebuild only the auth section
                    RefreshAuthCredentialsSection();
                    // Re-apply button active states
                    foreach (var child in authRow.Children())
                    {
                        child.RemoveFromClassList("welcome-wizard-auth-btn--active");
                    }
                    btn.AddToClassList("welcome-wizard-auth-btn--active");
                };

                btn.AddToClassList("welcome-wizard-auth-btn");
                if (active)
                {
                    btn.AddToClassList("welcome-wizard-auth-btn--active");
                }
                authRow.Add(btn);
            }

            step2.Add(authRow);
            step2.Add(_wizardAuthCredentials);
            RefreshAuthCredentialsSection();
            form.Add(step2);

            // ── Step 3: Container attribute ────────────────────────────────────
            var step3 = WizardSection("3", "Tag Your Container");
            step3.Add(Para("Each container maps to a Google Sheet tab by name. " +
                           "Add this attribute to your container class:"));
            var codeBlock = new Label(
                "[GoogleSheetsTab(\"EnemyData\")]\n" +
                "public class EnemyDataContainer\n" +
                "    : GameDataContainerBase<EnemyDataEntry> { }");
            codeBlock.AddToClassList("welcome-wizard-code-block");
            step3.Add(codeBlock);
            step3.Add(Tip("If you omit the attribute, the tab name falls back to the entry " +
                          "type name (e.g. \"EnemyDataEntry\")."));
            form.Add(step3);

            // ── CTA ────────────────────────────────────────────────────────────
            var openBtn = new Button(() =>
            {
                Close();
                EditorApplication.ExecuteMenuItem("Tools/GDE/Open Editor");
            })
            { text = "Open Game Data Editor  →" };
            openBtn.AddToClassList("welcome-cta-btn");
            form.Add(openBtn);

            return form;
        }

        private void RefreshAuthCredentialsSection()
        {
            _wizardAuthCredentials.Clear();

            switch (_wizardConfig.AuthMode)
            {
                case GoogleSheetsAuthMode.OAuth:
                    BuildOAuthCredentials(_wizardAuthCredentials);
                    break;
                case GoogleSheetsAuthMode.ApiKey:
                    BuildApiKeyCredentials(_wizardAuthCredentials);
                    break;
                case GoogleSheetsAuthMode.ServiceAccount:
                    BuildServiceAccountCredentials(_wizardAuthCredentials);
                    break;
            }
        }

        private void BuildOAuthCredentials(VisualElement parent)
        {
            parent.Add(WizardSubStep("3a", "Create a Google Cloud project",
                "Go to Google Cloud Console and create a new project (or select an existing one)."));
            parent.Add(LinkButton("Open Google Cloud Console →", CloudConsoleUrl));

            parent.Add(WizardSubStep("3b", "Enable the Google Sheets API",
                "APIs & Services → Library → search 'Google Sheets API' → Enable."));
            parent.Add(LinkButton("Enable Google Sheets API →", CloudSheetsApiUrl));

            parent.Add(WizardSubStep("3c", "Create OAuth 2.0 credentials",
                "Credentials → + Create Credentials → OAuth 2.0 Client ID\n" +
                "Application type: Desktop app — give it any name."));
            parent.Add(LinkButton("Open Credentials page →", CloudCredentialsUrl));

            parent.Add(WizardField("Client ID", _wizardConfig.OAuthClientId, v =>
            {
                Undo.RecordObject(_wizardConfig, "Set OAuth Client ID");
                _wizardConfig.OAuthClientId = v;
                SaveWizardConfig();
            }));
            parent.Add(WizardField("Client Secret", _wizardConfig.OAuthClientSecret, v =>
            {
                Undo.RecordObject(_wizardConfig, "Set OAuth Client Secret");
                _wizardConfig.OAuthClientSecret = v;
                SaveWizardConfig();
            }));

            parent.Add(WizardSubStep("3d", "Add yourself as a Test User",
                "While your OAuth app is in 'Testing' mode, only listed emails can authenticate.\n" +
                "OAuth consent screen → Test users → + Add users → enter your Google email."));
            parent.Add(LinkButton("Open OAuth Consent Screen →", CloudConsentUrl));
        }

        private void BuildApiKeyCredentials(VisualElement parent)
        {
            parent.Add(Para("API Key mode is read-only (Pull only). Your sheet must be shared " +
                            "as 'Anyone with the link can view'."));
            parent.Add(WizardSubStep("3a", "Create an API Key",
                "Google Cloud Console → APIs & Services → Credentials\n" +
                "+ Create Credentials → API Key → copy the key."));
            parent.Add(LinkButton("Open Credentials page →", CloudCredentialsUrl));
            parent.Add(WizardField("API Key", _wizardConfig.ApiKey, v =>
            {
                Undo.RecordObject(_wizardConfig, "Set API Key");
                _wizardConfig.ApiKey = v;
                SaveWizardConfig();
            }));
        }

        private void BuildServiceAccountCredentials(VisualElement parent)
        {
            parent.Add(Para("Service Account is suitable for CI/CD pipelines. " +
                            "Keep the JSON key file outside Assets/ and add it to .gitignore."));
            parent.Add(WizardSubStep("3a", "Create a Service Account",
                "Google Cloud Console → IAM & Admin → Service Accounts → + Create\n" +
                "Grant it 'Editor' role on your spreadsheet (share the sheet with the SA email)."));
            parent.Add(LinkButton("Open Google Cloud Console →", CloudConsoleUrl));
            parent.Add(WizardSubStep("3b", "Download the JSON key",
                "Service account → Keys → Add key → Create new key → JSON\n" +
                "Save the file somewhere outside your Assets folder."));
            parent.Add(WizardField("JSON Key File Path", _wizardConfig.ServiceAccountJsonPath, v =>
            {
                Undo.RecordObject(_wizardConfig, "Set SA JSON Path");
                _wizardConfig.ServiceAccountJsonPath = v;
                SaveWizardConfig();
            }, placeholder: "e.g.  C:/credentials/my-project-key.json"));
        }

        private void SaveWizardConfig()
        {
            if (_wizardConfig == null)
            {
                return;
            }
            EditorUtility.SetDirty(_wizardConfig);
            AssetDatabase.SaveAssets();
        }

        // ── Wizard element helpers ─────────────────────────────────────────────

        private static VisualElement WizardSection(string number, string title)
        {
            var section = new VisualElement();
            section.AddToClassList("welcome-wizard-section");

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems   = Align.Center;
            header.style.marginBottom = 8;

            var numLabel = new Label($"STEP {number}");
            numLabel.AddToClassList("welcome-wizard-step-num");
            header.Add(numLabel);

            var titleLabel = new Label(title.ToUpper());
            titleLabel.AddToClassList("welcome-wizard-section-title");
            header.Add(titleLabel);

            section.Add(header);
            return section;
        }

        private static VisualElement WizardSubStep(string number, string title, string body)
        {
            var row = new VisualElement();
            row.AddToClassList("welcome-wizard-substep");

            var numLabel = new Label(number);
            numLabel.AddToClassList("welcome-wizard-substep-num");
            row.Add(numLabel);

            var textCol = new VisualElement();
            textCol.style.flexGrow = 1;

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("welcome-step-title");
            textCol.Add(titleLabel);

            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("welcome-step-body");
            textCol.Add(bodyLabel);

            row.Add(textCol);
            return row;
        }

        private static VisualElement WizardField(string labelText, string currentValue,
            Action<string> onChange, string placeholder = "")
        {
            var container = new VisualElement();
            container.AddToClassList("welcome-wizard-field-row");

            var label = new Label(labelText);
            label.AddToClassList("welcome-wizard-field-label");
            container.Add(label);

            var field = new TextField { value = currentValue };
            if (!string.IsNullOrEmpty(placeholder))
            {
                field.tooltip = placeholder;
            }
            field.AddToClassList("welcome-wizard-field");
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            container.Add(field);

            return container;
        }

        // ── About page ─────────────────────────────────────────────────────────

        private void BuildAboutPage()
        {
            var scroll = new ScrollView();
            scroll.AddToClassList("welcome-page-scroll");

            scroll.Add(Heading("About"));

            var versionLabel = new Label($"Live Game Data Editor  v{Version}");
            versionLabel.AddToClassList("welcome-version-label");
            scroll.Add(versionLabel);

            scroll.Add(Para(
                "Built for Unity 2022.3 and above using UI Toolkit.\n" +
                "Designed for game designers who need fast, reliable data editing workflows."));

            scroll.Add(Separator());

            scroll.Add(SubHeading("Links"));

            scroll.Add(LinkButton("📄  Documentation", "https://github.com"));
            scroll.Add(LinkButton("🐛  Report an Issue", "https://github.com"));
            scroll.Add(LinkButton("⭐  Rate on the Asset Store", "https://assetstore.unity.com"));

            scroll.Add(Separator());
            scroll.Add(SubHeading("License"));
            scroll.Add(Para("Standard Unity Asset Store EULA. See LICENSE in the package root."));

            _contentArea.Add(scroll);
        }

        // ── UI element builders ────────────────────────────────────────────────

        private static Label Heading(string text)
        {
            var l = new Label(text);
            l.AddToClassList("welcome-heading");
            return l;
        }

        private static Label SubHeading(string text)
        {
            var l = new Label(text);
            l.AddToClassList("welcome-subheading");
            return l;
        }

        private static Label Para(string text)
        {
            var l = new Label(text);
            l.AddToClassList("welcome-para");
            return l;
        }

        private static Label Tip(string text)
        {
            var l = new Label("💡  " + text);
            l.AddToClassList("welcome-tip");
            return l;
        }

        private static VisualElement Separator()
        {
            var s = new VisualElement();
            s.AddToClassList("welcome-separator");
            return s;
        }

        private static VisualElement FeatureRow(string icon, string title, string description)
        {
            var row = new VisualElement();
            row.AddToClassList("welcome-feature-row");

            var iconLabel = new Label(icon);
            iconLabel.AddToClassList("welcome-feature-icon");
            row.Add(iconLabel);

            var textCol = new VisualElement();
            textCol.style.flexGrow = 1;

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("welcome-feature-title");
            textCol.Add(titleLabel);

            var descLabel = new Label(description);
            descLabel.AddToClassList("welcome-feature-desc");
            textCol.Add(descLabel);

            row.Add(textCol);
            return row;
        }

        private static VisualElement Step(int number, string title, string body)
        {
            var row = new VisualElement();
            row.AddToClassList("welcome-step");

            var numLabel = new Label(number.ToString());
            numLabel.AddToClassList("welcome-step-num");
            row.Add(numLabel);

            var textCol = new VisualElement();
            textCol.style.flexGrow = 1;

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("welcome-step-title");
            textCol.Add(titleLabel);

            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("welcome-step-body");
            textCol.Add(bodyLabel);

            row.Add(textCol);
            return row;
        }

        private static Button LinkButton(string text, string url)
        {
            var btn = new Button(() => Application.OpenURL(url)) { text = text };
            btn.AddToClassList("welcome-link-btn");
            return btn;
        }
    }
}
