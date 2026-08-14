using System.IO;
using System.Text.Json.Nodes;
using Hermes.Windows.Apps;
using Hermes.Windows.Apps.CodexAuthSwitchSync.Domain;
using Hermes.Windows.Apps.CodexAuthSwitchSync.Services;
using Hermes.Windows.Apps.Contracts;
using Microsoft.Data.Sqlite;

namespace Hermes.Tests.Apps;

public static class CodexAuthSwitchSyncTests
{
    public static void Register(TestSuite suite)
    {
        suite.Add("app registry rejects duplicate ids", AppRegistryRejectsDuplicateIds);
        suite.Add("codex API config requires explicit provider", ApiConfigRequiresExplicitProvider);
        suite.Add("codex config switch recursively merges target values and preserves shared trees", ConfigSwitchRecursivelyMergesAndPreservesSharedTrees);
        suite.Add("codex browser login config preserves shared values and clears API routing", BrowserLoginConfigPreservesSharedValuesAndClearsApiRouting);
        suite.Add("codex config switch rejects malformed TOML", ConfigSwitchRejectsMalformedToml);
        suite.Add("codex auth mode detection recognizes ChatGPT and API files", AuthModeDetectionRecognizesSupportedFiles);
        suite.Add("codex profile store imports arbitrary provider config and auth", ProfileStoreImportsArbitraryProviderFiles);
        suite.Add("codex API editor decrypts saved auth for the active edit session", ApiEditorDraftReturnsDecryptedAuth);
        suite.Add("codex active API edit applies new base URL and preserves shared config", ActiveApiEditAppliesNewBaseUrl);
        suite.Add("codex session sync updates rollout and authoritative state database", SessionSyncUpdatesRolloutAndStateDatabase);
        suite.Add("codex provider alignment is case sensitive", ProviderAlignmentIsCaseSensitive);
        suite.Add("codex session summary separates visible internal and archived rollouts", SessionSummarySeparatesRolloutKinds);
        suite.Add("codex browser login locates native CLI from npm installation", BrowserLoginLocatesNativeCliFromNpmInstallation);
        suite.Add("codex process display distinguishes editor extension cli and owned login", ProcessDisplayDistinguishesSources);
        suite.Add("codex browser login is cancelable and crash recoverable", BrowserLoginIsCancelableAndRecoverable);
        suite.Add("Hermes update checks automatically and installs only after explicit update action", AutomaticUpdateUsesGithubVelopackReleases);
        suite.Add("codex app UI uses Hermes styles and explicit secret labels", AppUiUsesHermesStylesAndSecretLabels);
    }

    private static void AppRegistryRejectsDuplicateIds()
    {
        var registry = new AppRegistry();
        registry.Register(new FakeApp("test"));
        var threw = false;
        try
        {
            registry.Register(new FakeApp("TEST"));
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        TestAssert.True(threw);
    }

    private static void ApiConfigRequiresExplicitProvider()
    {
        TestAssert.Equal("Relay-X", CodexToml.RequireExplicitProvider("model_provider = \"Relay-X\"\n"));

        var threw = false;
        try
        {
            CodexToml.RequireExplicitProvider("model = \"relay-model\"\n");
        }
        catch (CodexSwitchException)
        {
            threw = true;
        }

        TestAssert.True(threw);
    }

    private static void ConfigSwitchRecursivelyMergesAndPreservesSharedTrees()
    {
        const string current = """
            # Keep this root comment
            notify = ["pwsh", "notify.ps1"]
            model_provider = "openai"
            model = "old-model"
            forced_login_method = "chatgpt"

            [features]
            apps = true

            [mcp_servers.local]
            # Keep this MCP comment
            command = "npx"
            args = [
              "-y",
              "@example/mcp",
            ]

            [model_providers.Relay-X]
            name = "Old relay"
            base_url = "https://old.example/v1"
            """;
        const string target = """
            model_provider = "Relay-X"
            model = "relay-model"
            model_reasoning_effort = "xhigh"

            [features]
            multi_agent = false

            [mcp_servers.remote]
            url = "https://mcp.example"

            [model_providers.Relay-X]
            name = "Relay X"
            base_url = "https://relay.example/v1"
            wire_api = "responses"
            """;

        var result = CodexToml.MergeForAuthSwitch(current, target);

        TestAssert.Equal("Relay-X", CodexToml.Provider(result));
        TestAssert.Equal("relay-model", CodexToml.ReadRootString(result, "model"));
        TestAssert.Equal("https://relay.example/v1", CodexToml.ApiBaseUrl(result));
        TestAssert.Contains("notify", result);
        TestAssert.Contains("[mcp_servers.local]", result);
        TestAssert.Contains("[mcp_servers.remote]", result);
        TestAssert.Contains("apps = true", result);
        TestAssert.Contains("multi_agent = false", result);
        TestAssert.Contains("wire_api = \"responses\"", result);
        TestAssert.False(result.Contains("forced_login_method", StringComparison.Ordinal));
    }

    private static void ConfigSwitchRejectsMalformedToml()
    {
        var threw = false;
        try
        {
            CodexToml.MergeForAuthSwitch("model = \"valid\"\n", "model_provider = [\n");
        }
        catch (CodexSwitchException)
        {
            threw = true;
        }

        TestAssert.True(threw);
    }

    private static void BrowserLoginConfigPreservesSharedValuesAndClearsApiRouting()
    {
        const string current = """
            model_provider = "Relay-X"
            model = "gpt-5.6-sol"
            model_reasoning_effort = "xhigh"
            forced_login_method = "api"
            openai_base_url = "https://relay.example/v1"

            [mcp_servers.local]
            command = "npx"

            [model_providers.Relay-X]
            name = "Relay X"
            base_url = "https://relay.example/v1"
            """;

        var result = CodexToml.BuildChatGptLoginConfig(current);

        TestAssert.Equal("openai", CodexToml.Provider(result));
        TestAssert.Equal("gpt-5.6-sol", CodexToml.ReadRootString(result, "model"));
        TestAssert.Equal("xhigh", CodexToml.ReadRootString(result, "model_reasoning_effort"));
        TestAssert.Equal("file", CodexToml.ReadRootString(result, "cli_auth_credentials_store"));
        TestAssert.Contains("[mcp_servers.local]", result);
        TestAssert.Contains("[model_providers.Relay-X]", result);
        TestAssert.False(result.Contains("forced_login_method", StringComparison.Ordinal));
        TestAssert.False(result.Contains("openai_base_url", StringComparison.Ordinal));
    }

    private static void AuthModeDetectionRecognizesSupportedFiles()
    {
        TestAssert.Equal(CodexAuthMode.Api, CodexProfileStore.InferMode("{\"OPENAI_API_KEY\":\"sk-test\"}"));
        TestAssert.Equal(CodexAuthMode.Api, CodexProfileStore.InferMode("{\"RELAY_ACCESS_TOKEN\":\"relay-secret\"}"));
        TestAssert.Equal(CodexAuthMode.ChatGpt, CodexProfileStore.InferMode("{\"tokens\":{\"access_token\":\"secret\"}}"));
        TestAssert.Equal(CodexAuthMode.Unknown, CodexProfileStore.InferMode("{}"));
    }

    private static void ProfileStoreImportsArbitraryProviderFiles()
    {
        using var workspace = new TemporaryDirectory();
        var locations = new CodexLocations(Path.Combine(workspace.Path, "codex"), Path.Combine(workspace.Path, "app"));
        var store = new CodexProfileStore(locations);
        const string config = "model_provider = \"Relay-X\"\nmodel = \"relay-model\"\nmodel_reasoning_effort = \"medium\"\n\n[model_providers.Relay-X]\nname = \"Relay X\"\nbase_url = \"https://relay.example/v1\"\nwire_api = \"responses\"\n";
        const string auth = "{\"RELAY_ACCESS_TOKEN\":\"relay-profile-secret\"}\n";
        var summary = store.ConfigureApiFiles(config, auth);
        var bytes = File.ReadAllBytes(locations.GetProfilePath(CodexAuthMode.Api));
        TestAssert.True(summary.Saved);
        TestAssert.Equal("Relay-X", summary.Provider);
        TestAssert.Equal("relay-model", summary.Model);
        TestAssert.Equal("https://relay.example/v1", summary.BaseUrl);
        TestAssert.False(System.Text.Encoding.UTF8.GetString(bytes).Contains("relay-profile-secret", StringComparison.Ordinal));
        var loaded = store.Load(CodexAuthMode.Api);
        TestAssert.Equal(config, loaded.ConfigText);
        TestAssert.Equal(auth, loaded.AuthText);

        const string replacementConfig = "model_provider = \"AnotherRelay\"\nmodel = \"another-model\"\n";
        store.ConfigureApiFiles(replacementConfig, string.Empty);
        var replaced = store.Load(CodexAuthMode.Api);
        TestAssert.Equal(replacementConfig, replaced.ConfigText);
        TestAssert.Equal(auth, replaced.AuthText);

        const string refreshedAuth = "{\"RELAY_ACCESS_TOKEN\":\"refreshed-secret\"}\n";
        store.RefreshAuth(
            CodexAuthMode.Api,
            replacementConfig + "\n[mcp_servers.local]\ncommand = \"npx\"\n",
            refreshedAuth);
        var refreshed = store.Load(CodexAuthMode.Api);
        TestAssert.Equal(replacementConfig, refreshed.ConfigText);
        TestAssert.Equal(refreshedAuth, refreshed.AuthText);

        var rejectedMissingProvider = false;
        try
        {
            store.ConfigureApiFiles("model = \"missing-provider\"\n", auth);
        }
        catch (CodexSwitchException)
        {
            rejectedMissingProvider = true;
        }

        TestAssert.True(rejectedMissingProvider);

        var rejectedChatGptAuth = false;
        try
        {
            store.ConfigureApiFiles(config, "{\"tokens\":{\"access_token\":\"chatgpt-secret\"}}\n");
        }
        catch (CodexSwitchException)
        {
            rejectedChatGptAuth = true;
        }

        TestAssert.True(rejectedChatGptAuth);
    }

    private static void ApiEditorDraftReturnsDecryptedAuth()
    {
        using var workspace = new TemporaryDirectory();
        var locations = new CodexLocations(Path.Combine(workspace.Path, "codex"), Path.Combine(workspace.Path, "app"));
        var store = new CodexProfileStore(locations);
        const string config = "model_provider = \"Relay-X\"\n\n[model_providers.Relay-X]\nbase_url = \"https://relay.example/v1\"\n";
        const string auth = "{\"RELAY_ACCESS_TOKEN\":\"visible-only-while-editing\"}\n";
        store.ConfigureApiFiles(config, auth);

        var draft = store.GetApiDraft();

        TestAssert.Equal(config, draft.ConfigText);
        TestAssert.Equal(auth, draft.AuthText);
        TestAssert.False(
            System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(locations.GetProfilePath(CodexAuthMode.Api)))
                .Contains("visible-only-while-editing", StringComparison.Ordinal));
    }

    private static void ActiveApiEditAppliesNewBaseUrl()
    {
        using var workspace = new TemporaryDirectory();
        var codexHome = Path.Combine(workspace.Path, "codex");
        var appData = Path.Combine(workspace.Path, "app");
        Directory.CreateDirectory(codexHome);
        const string activeConfig = """
            model_provider = "Relay-X"
            model = "old-model"

            [model_providers.Relay-X]
            name = "Relay X"
            base_url = "https://old.example/v1"
            wire_api = "responses"

            [mcp_servers.local]
            command = "npx"
            """;
        const string newConfig = """
            model_provider = "Relay-X"
            model = "new-model"

            [model_providers.Relay-X]
            name = "Relay X"
            base_url = "https://new.example/v1"
            wire_api = "responses"
            """;
        const string oldAuth = "{\"RELAY_ACCESS_TOKEN\":\"old-secret\"}\n";
        const string newAuth = "{\"RELAY_ACCESS_TOKEN\":\"new-secret\"}\n";
        File.WriteAllText(Path.Combine(codexHome, "config.toml"), activeConfig);
        File.WriteAllText(Path.Combine(codexHome, "auth.json"), oldAuth);

        var locations = new CodexLocations(codexHome, appData);
        var store = new CodexProfileStore(locations);
        var service = new CodexAuthSwitchService(
            locations,
            store,
            new CodexSessionSyncService(locations),
            new NoRunningCodexProcessGuard(),
            new Hermes.Windows.Infrastructure.AppLogger());

        service.ConfigureApiFiles(newConfig, newAuth);

        var appliedConfig = File.ReadAllText(locations.ConfigPath);
        TestAssert.Equal("https://new.example/v1", CodexToml.ApiBaseUrl(appliedConfig));
        TestAssert.Equal("new-model", CodexToml.ReadRootString(appliedConfig, "model"));
        TestAssert.Contains("[mcp_servers.local]", appliedConfig);
        TestAssert.Contains("command = \"npx\"", appliedConfig);
        TestAssert.Equal(newAuth, File.ReadAllText(locations.AuthPath));
        TestAssert.Equal(newConfig, store.Load(CodexAuthMode.Api).ConfigText);
        TestAssert.Equal(newAuth, store.Load(CodexAuthMode.Api).AuthText);
    }

    private static void SessionSyncUpdatesRolloutAndStateDatabase()
    {
        using var workspace = new TemporaryDirectory();
        var codexHome = Path.Combine(workspace.Path, "codex");
        var appData = Path.Combine(workspace.Path, "app");
        var sessionDirectory = Path.Combine(codexHome, "sessions", "2026", "08", "14");
        Directory.CreateDirectory(sessionDirectory);
        var rollout = Path.Combine(sessionDirectory, "rollout-test.jsonl");
        File.WriteAllText(
            rollout,
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"thread-1\",\"model_provider\":\"OpenAI\"}}\n{\"type\":\"response_item\",\"payload\":{}}\n");
        Directory.CreateDirectory(Path.Combine(codexHome, "sqlite"));
        var database = Path.Combine(codexHome, "sqlite", "state_5.sqlite");
        using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE threads (id TEXT PRIMARY KEY, model_provider TEXT); INSERT INTO threads VALUES ('thread-1', 'OpenAI');";
            command.ExecuteNonQuery();
        }

        var locations = new CodexLocations(codexHome, appData);
        var service = new CodexSessionSyncService(locations);
        var backup = Path.Combine(appData, "backups", "test");
        var result = service.Sync("model_provider = \"openai\"\n", "openai", backup);

        TestAssert.Equal(1, result.RolloutFilesUpdated);
        TestAssert.Equal(1, result.SqliteRowsUpdated);
        TestAssert.True(result.Verification.IsAligned);
        var firstLine = File.ReadLines(rollout).First();
        var provider = JsonNode.Parse(firstLine)?["payload"]?["model_provider"]?.GetValue<string>();
        TestAssert.Equal("openai", provider);
        using var verify = new SqliteConnection($"Data Source={database};Mode=ReadOnly;Pooling=False");
        verify.Open();
        using var verifyCommand = verify.CreateCommand();
        verifyCommand.CommandText = "SELECT model_provider FROM threads WHERE id = 'thread-1'";
        TestAssert.Equal("openai", Convert.ToString(verifyCommand.ExecuteScalar()));
        TestAssert.True(File.Exists(Path.Combine(backup, "rollouts.dat")));
        var protectedDatabase = Path.Combine(backup, "sqlite", "state_5.sqlite.dat");
        TestAssert.True(File.Exists(protectedDatabase));
        TestAssert.False(File.ReadAllBytes(protectedDatabase).AsSpan().StartsWith("SQLite format 3"u8));
        verify.Close();

        service.Restore(backup);
        var restoredLine = File.ReadLines(rollout).First();
        TestAssert.Equal(
            "OpenAI",
            JsonNode.Parse(restoredLine)?["payload"]?["model_provider"]?.GetValue<string>());
        using var restored = new SqliteConnection($"Data Source={database};Mode=ReadOnly;Pooling=False");
        restored.Open();
        using var restoredCommand = restored.CreateCommand();
        restoredCommand.CommandText = "SELECT model_provider FROM threads WHERE id = 'thread-1'";
        TestAssert.Equal("OpenAI", Convert.ToString(restoredCommand.ExecuteScalar()));
    }

    private static void SessionSummarySeparatesRolloutKinds()
    {
        using var workspace = new TemporaryDirectory();
        var codexHome = Path.Combine(workspace.Path, "codex");
        var activeDirectory = Path.Combine(codexHome, "sessions", "2026", "08", "14");
        var archivedDirectory = Path.Combine(codexHome, "archived_sessions");
        Directory.CreateDirectory(activeDirectory);
        Directory.CreateDirectory(archivedDirectory);
        File.WriteAllText(
            Path.Combine(activeDirectory, "rollout-visible.jsonl"),
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"visible\",\"model_provider\":\"openai\",\"source\":\"vscode\"}}\n");
        File.WriteAllText(
            Path.Combine(activeDirectory, "rollout-internal.jsonl"),
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"internal\",\"model_provider\":\"openai\",\"source\":{\"subagent\":{\"other\":\"guardian\"}}}}\n");
        File.WriteAllText(
            Path.Combine(archivedDirectory, "rollout-archived.jsonl"),
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"archived\",\"model_provider\":\"openai\",\"source\":\"vscode\"}}\n");

        var service = new CodexSessionSyncService(
            new CodexLocations(codexHome, Path.Combine(workspace.Path, "app")));
        var composition = service.GetAlignment("model_provider = \"openai\"\n").SessionComposition;

        TestAssert.Equal(3, composition.Total);
        TestAssert.Equal(1, composition.RegularActive);
        TestAssert.Equal(1, composition.InternalActive);
        TestAssert.Equal(1, composition.Archived);
        TestAssert.Equal(0, composition.Unclassified);
    }

    private static void ProviderAlignmentIsCaseSensitive()
    {
        using var workspace = new TemporaryDirectory();
        var codexHome = Path.Combine(workspace.Path, "codex");
        var sessionDirectory = Path.Combine(codexHome, "sessions", "2026", "08", "14");
        Directory.CreateDirectory(sessionDirectory);
        File.WriteAllText(
            Path.Combine(sessionDirectory, "rollout-chatgpt.jsonl"),
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"chatgpt\",\"model_provider\":\"openai\",\"source\":\"vscode\"}}\n");
        File.WriteAllText(
            Path.Combine(sessionDirectory, "rollout-api.jsonl"),
            "{\"type\":\"session_meta\",\"payload\":{\"id\":\"api\",\"model_provider\":\"OpenAI\",\"source\":\"vscode\"}}\n");

        var sqliteDirectory = Path.Combine(codexHome, "sqlite");
        Directory.CreateDirectory(sqliteDirectory);
        var database = Path.Combine(sqliteDirectory, "state_5.sqlite");
        using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE threads (id TEXT PRIMARY KEY, model_provider TEXT COLLATE NOCASE);
                INSERT INTO threads VALUES ('chatgpt', 'openai');
                INSERT INTO threads VALUES ('api', 'OpenAI');
                """;
            command.ExecuteNonQuery();
        }

        var service = new CodexSessionSyncService(
            new CodexLocations(codexHome, Path.Combine(workspace.Path, "app")));
        var alignment = service.GetAlignment("model_provider = \"openai\"\n");

        TestAssert.Equal(2, alignment.RolloutProviderCounts.Count);
        TestAssert.Equal(1, alignment.RolloutProviderCounts["openai"]);
        TestAssert.Equal(1, alignment.RolloutProviderCounts["OpenAI"]);
        TestAssert.Equal(2, alignment.SqliteProviderCounts.Count);
        TestAssert.Equal(1, alignment.SqliteProviderCounts["openai"]);
        TestAssert.Equal(1, alignment.SqliteProviderCounts["OpenAI"]);
        TestAssert.Equal(1, alignment.RolloutMismatched);
        TestAssert.Equal(1, alignment.SqliteMismatched);
    }

    private static void BrowserLoginLocatesNativeCliFromNpmInstallation()
    {
        using var workspace = new TemporaryDirectory();
        var bin = Path.Combine(workspace.Path, "bin");
        var executable = Path.Combine(
            bin,
            "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-x64",
            "vendor", "x86_64-pc-windows-msvc", "bin", "codex.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllText(executable, string.Empty);

        var result = CodexBrowserLoginLauncher.FindExecutable(bin, Path.Combine(workspace.Path, "profile"));

        TestAssert.Equal(executable, result);
    }

    private static void ProcessDisplayDistinguishesSources()
    {
        TestAssert.Equal(
            "VS Code Codex 扩展（PID 42）",
            CodexProcessGuard.DescribeProcess(
                "codex",
                42,
                @"C:\Users\test\.vscode\extensions\openai.chatgpt-1.0\bin\windows-x86_64\codex.exe",
                ownedLogin: false));
        TestAssert.Equal(
            "Codex CLI（PID 43）",
            CodexProcessGuard.DescribeProcess("codex", 43, @"C:\Tools\codex.exe", ownedLogin: false));
        TestAssert.Equal(
            "Hermes 浏览器登录（PID 44）",
            CodexProcessGuard.DescribeProcess("codex", 44, @"C:\Tools\codex.exe", ownedLogin: true));
    }

    private static void BrowserLoginIsCancelableAndRecoverable()
    {
        var launcher = File.ReadAllText(FindRepoFile(
            "src/Hermes.Windows/Apps/CodexAuthSwitchSync/Services/CodexBrowserLoginLauncher.cs"));
        var service = File.ReadAllText(FindRepoFile(
            "src/Hermes.Windows/Apps/CodexAuthSwitchSync/Services/CodexAuthSwitchService.cs"));
        var job = File.ReadAllText(FindRepoFile(
            "src/Hermes.Windows/Apps/CodexAuthSwitchSync/Services/WindowsProcessJob.cs"));
        TestAssert.Contains("LoginAsync(string codexHome, CancellationToken cancellationToken)", launcher);
        TestAssert.Contains("WaitForExitAsync(linked.Token)", launcher);
        TestAssert.Contains("Kill(entireProcessTree: true)", launcher);
        TestAssert.Contains("JobObjectLimitKillOnJobClose", job);
        TestAssert.Contains("BrowserLoginJournalPath", service);
        TestAssert.Contains("TryRecoverInterruptedBrowserLogin", service);
        TestAssert.Contains("RestoreProfile(CodexAuthMode.ChatGpt", service);
    }

    private static void AutomaticUpdateUsesGithubVelopackReleases()
    {
        var program = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Program.cs"));
        var updater = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Infrastructure/AutomaticUpdateService.cs"));
        var app = File.ReadAllText(FindRepoFile("src/Hermes.Windows/App.xaml.cs"));
        var settings = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));
        var packageScript = File.ReadAllText(FindRepoFile("scripts/Package-HermesRelease.ps1"));
        TestAssert.True(
            program.IndexOf("VelopackApp.Build()", StringComparison.Ordinal)
            < program.IndexOf("new App()", StringComparison.Ordinal));
        TestAssert.Contains("SetAutoApplyOnStartup(false)", program);
        TestAssert.Contains("https://github.com/KiRinXC/Hermes", updater);
        TestAssert.Contains("CheckForUpdatesAsync", updater);
        TestAssert.Contains("DownloadUpdatesAsync", updater);
        TestAssert.Contains("ApplyUpdatesAndRestart", updater);
        TestAssert.Contains("awaits user confirmation", updater);
        TestAssert.False(app.Contains("InstallAndRestartAsync", StringComparison.Ordinal));
        TestAssert.Contains("ShowUpdateSettingsWindow", app);
        TestAssert.Contains("UpdateAction_Click", settings);
        TestAssert.Contains("ShowGeneralPage", settings);
        var installHandlerStart = settings.IndexOf("private async void UpdateAction_Click", StringComparison.Ordinal);
        var installHandlerEnd = settings.IndexOf("private async Task<bool> SaveSettingsAsync", installHandlerStart, StringComparison.Ordinal);
        TestAssert.True(installHandlerStart >= 0 && installHandlerEnd > installHandlerStart);
        var installHandler = settings[installHandlerStart..installHandlerEnd];
        TestAssert.Contains("InstallAndRestartAsync", installHandler);
        TestAssert.False(installHandler.Contains("ConfirmAsync(", StringComparison.Ordinal));
        TestAssert.Contains("vpk pack", packageScript);
        TestAssert.Contains("releases.win.json", packageScript);
        var releaseAssetsStart = packageScript.IndexOf("$releaseAssetNames = @(", StringComparison.Ordinal);
        var releaseAssetsEnd = packageScript.IndexOf(")", releaseAssetsStart, StringComparison.Ordinal);
        TestAssert.True(releaseAssetsStart >= 0 && releaseAssetsEnd > releaseAssetsStart);
        var releaseAssets = packageScript[releaseAssetsStart..releaseAssetsEnd];
        TestAssert.Contains("Hermes-win-Setup.exe", releaseAssets);
        TestAssert.Contains("full.nupkg", releaseAssets);
        TestAssert.Contains("releases.win.json", releaseAssets);
        TestAssert.False(releaseAssets.Contains("Portable.zip", StringComparison.Ordinal));
        TestAssert.False(releaseAssets.Contains("assets.win.json", StringComparison.Ordinal));
    }

    private static void AppUiUsesHermesStylesAndSecretLabels()
    {
        var settings = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml"));
        var center = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Apps/AppsCenterView.xaml"));
        var app = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Apps/CodexAuthSwitchSync/UI/CodexAuthSwitchSyncView.xaml"));
        var settingsCode = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Shell/SettingsWindow.xaml.cs"));
        TestAssert.Contains("<TabItem Header=\"应用\">", settings);
        TestAssert.True(
            settings.IndexOf("<TabItem Header=\"应用\">", StringComparison.Ordinal)
            > settings.IndexOf("<TabItem Header=\"高级\">", StringComparison.Ordinal));
        TestAssert.Contains("Settings.CardBrush", center);
        TestAssert.False(center.Contains("把常用的小工具集中在 Hermes 中", StringComparison.Ordinal));
        TestAssert.Contains("{Binding Description}", center);
        TestAssert.Contains("Style=\"{DynamicResource Settings.PrimaryButton}\"", app);
        TestAssert.Contains("Text=\"config.toml\"", app);
        TestAssert.Contains("Text=\"auth.json\"", app);
        TestAssert.False(app.Contains("（敏感）", StringComparison.Ordinal));
        TestAssert.False(app.Contains("导入 API 完整档案", StringComparison.Ordinal));
        TestAssert.False(app.Contains("加密保存完整档案", StringComparison.Ordinal));
        TestAssert.False(app.Contains("必须包含根级 model_provider", StringComparison.Ordinal));
        TestAssert.Contains("x:Name=\"ApiConfigTomlText\"", app);
        TestAssert.Contains("x:Name=\"ApiAuthJsonText\"", app);
        TestAssert.Contains("x:Name=\"LoginChatGptButton\"", app);
        TestAssert.Contains("x:Name=\"CaptureChatGptButtonText\"", app);
        TestAssert.Contains("x:Name=\"LoginChatGptButtonText\"", app);
        TestAssert.Contains("AutomationProperties.Name=\"在浏览器中登录 ChatGPT\"", app);
        TestAssert.False(app.Contains("<WrapPanel>", StringComparison.Ordinal));
        TestAssert.True(
            app.IndexOf("x:Name=\"CaptureChatGptButton\"", StringComparison.Ordinal)
            < app.IndexOf("x:Name=\"LoginChatGptButton\"", StringComparison.Ordinal));
        TestAssert.Contains("IsUndoEnabled=\"False\"", app);
        var codeBehind = File.ReadAllText(FindRepoFile("src/Hermes.Windows/Apps/CodexAuthSwitchSync/UI/CodexAuthSwitchSyncView.xaml.cs"));
        TestAssert.Contains("IsVisibleChanged", codeBehind);
        TestAssert.Contains("ResetApiEditor", codeBehind);
        TestAssert.Contains("_service.LoginChatGptWithBrowserAsync", codeBehind);
        TestAssert.Contains("x:Name=\"BusyCancelButton\"", app);
        TestAssert.Contains("x:Name=\"BusySpinnerRotateTransform\"", app);
        TestAssert.False(app.Contains("<ProgressBar", StringComparison.Ordinal));
        TestAssert.Contains("Background=\"Transparent\"", app);
        TestAssert.False(app.Contains("Background=\"#8A000000\"", StringComparison.Ordinal));
        TestAssert.Contains("CancelBrowserLogin", codeBehind);
        TestAssert.Contains("SystemParameters.ClientAreaAnimation", codeBehind);
        TestAssert.Contains("StartBusyVisuals", codeBehind);
        TestAssert.Contains("ContentScrollViewer.IsHitTestVisible", codeBehind);
        TestAssert.False(codeBehind.Contains("\n        IsHitTestVisible = !busy;", StringComparison.Ordinal));
        TestAssert.Contains("RenderChatGptActions", codeBehind);
        TestAssert.Contains("isCurrent && !hasProfile", codeBehind);
        TestAssert.False(codeBehind.Contains("更新当前登录", StringComparison.Ordinal));
        TestAssert.Contains("RenderApiActions", codeBehind);
        TestAssert.Contains("SetButtonStyle", codeBehind);
        TestAssert.Contains("SavePendingChangesAsync", codeBehind);
        TestAssert.False(codeBehind.Contains("MessageBox.Show", StringComparison.Ordinal));
        TestAssert.Contains("Text=\"会话构成\"", app);
        TestAssert.Contains("Text=\"全部 Rollout\"", app);
        TestAssert.Contains("只同步本机 Provider 元数据，不上传会话。", app);
        TestAssert.False(app.Contains("切换时递归合并 config", StringComparison.Ordinal));
        TestAssert.False(app.Contains("全部 rollout 记录", StringComparison.Ordinal));
        TestAssert.Contains("Text=\"常规会话\"", app);
        TestAssert.False(app.Contains("Text=\"普通活动\"", StringComparison.Ordinal));
        TestAssert.False(app.Contains("SessionCompositionNote", StringComparison.Ordinal));
        TestAssert.Contains("x:Name=\"InternalSessionCountText\"", app);
        TestAssert.Contains("x:Name=\"ConfirmationOverlay\"", settings);
        TestAssert.Contains("ISettingsSaveParticipant", settingsCode);
        TestAssert.False(app.Contains("<svg", StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }

    private sealed class FakeApp(string id) : IHermesApp
    {
        public string Id => id;
        public string Name => id;
        public string Description => id;
        public System.Windows.Media.Geometry Icon => System.Windows.Media.Geometry.Empty;
        public System.Windows.FrameworkElement CreateView() => new System.Windows.Controls.Grid();
    }

    private sealed class NoRunningCodexProcessGuard : CodexProcessGuard
    {
        public override IReadOnlyList<string> GetRunningClients() => [];

        public override void EnsureClientsClosed()
        {
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Hermes.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
