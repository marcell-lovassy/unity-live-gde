using System;
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
    ///   Sheets Setup  — OAuth walkthrough (buyer creates their own Google Cloud credentials)
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

        private Page          _activePage = Page.Welcome;
        private VisualElement _contentArea;
        private VisualElement[] _tabButtons;

        // ── Menu item ─────────────────────────────────────────────────────────

        [MenuItem("Tools/GDE/Welcome", priority = 200)]
        public static void ShowWindow()
        {
            var window = GetWindow<WelcomeWindow>(utility: true, title: "Live Game Data Editor");
            window.minSize = new Vector2(640, 520);
            window.maxSize = new Vector2(640, 520);
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
            var scroll = new ScrollView();
            scroll.AddToClassList("welcome-page-scroll");

            scroll.Add(Heading("Google Sheets Setup"));
            scroll.Add(Para(
                "Each project uses its own Google Cloud OAuth credentials — " +
                "your data stays entirely within your own Google account. " +
                "The setup takes about 5 minutes."));

            // Step 1
            scroll.Add(Step(1, "Go to Google Cloud Console",
                "Open the Google Cloud Console and sign in with your Google account."));
            scroll.Add(LinkButton("Open Google Cloud Console →", CloudConsoleUrl));

            // Step 2
            scroll.Add(Step(2, "Create a project & enable the Sheets API",
                "Create a new project (or select an existing one).\n" +
                "Then navigate to APIs & Services → Library and enable the Google Sheets API."));
            scroll.Add(LinkButton("Enable Google Sheets API →", CloudSheetsApiUrl));

            // Step 3
            scroll.Add(Step(3, "Create OAuth 2.0 credentials",
                "APIs & Services → Credentials → Create Credentials → OAuth 2.0 Client ID\n" +
                "Application type: Desktop app\n" +
                "Give it any name (e.g. \"Unity Game Data Editor\").\n" +
                "Copy the Client ID and Client Secret shown on screen."));
            scroll.Add(LinkButton("Open Credentials page →", CloudCredentialsUrl));

            // Step 4
            scroll.Add(Step(4, "Add a test user",
                "Your app starts in 'Testing' mode — you must add your Google email address.\n" +
                "APIs & Services → OAuth consent screen → Test users → Add users."));
            scroll.Add(LinkButton("Open OAuth Consent Screen →", CloudConsentUrl));

            // Step 5
            scroll.Add(Step(5, "Configure in Unity",
                "In the Project window:\n" +
                "Assets → Create → Live Game Data / Google Sheets Config\n\n" +
                "Fill in:\n" +
                "• Spreadsheet ID  — the segment after /d/ in your sheet URL\n" +
                "• OAuth Client ID — from Step 3\n" +
                "• OAuth Client Secret — from Step 3\n" +
                "• Auth Mode — OAuth (default)"));

            // Step 6
            scroll.Add(Step(6, "Declare the tab name on your container",
                "Add [GoogleSheetsTab(\"MyTabName\")] to your container class:\n\n" +
                "[GoogleSheetsTab(\"EnemyData\")]\n" +
                "public class EnemyDataContainer : GameDataContainerBase<EnemyDataEntry> { }"));

            // Step 7
            scroll.Add(Step(7, "Sign in and sync",
                "Open the Game Data Editor → load your container → click ☁ Sheets.\n" +
                "Assign your GoogleSheetsConfig, then click 'Sign in with Google'.\n" +
                "After sign-in, use ↑ Push to write data or ↓ Pull to read it."));

            scroll.Add(Tip(
                "The GoogleSheetsConfig is shared across all open containers — " +
                "one config per spreadsheet, each container maps to its own tab."));

            _contentArea.Add(scroll);
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
