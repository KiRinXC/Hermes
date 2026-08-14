using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Serialization;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Services;

internal static class CodexToml
{
    private const string ChatGptLoginOverlay = """
        model_provider = "openai"
        cli_auth_credentials_store = "file"
        """;

    private static readonly IReadOnlySet<string> AuthenticationRoutingKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "openai_base_url",
            "chatgpt_base_url",
            "forced_login_method",
            "forced_chatgpt_workspace_id",
            "experimental_realtime_ws_base_url",
            "apps_mcp_product_sku",
            "oss_provider"
        };

    public static string Provider(string configText) => ReadRootString(configText, "model_provider") ?? "openai";

    public static string BuildChatGptLoginConfig(string currentConfig) =>
        MergeForAuthSwitch(currentConfig, ChatGptLoginOverlay);

    public static string MergeForAuthSwitch(string currentConfig, string targetConfig)
    {
        var metadata = new TomlMetadataStore();
        var options = new TomlSerializerOptions
        {
            MetadataStore = metadata
        };
        var current = ParseTable(currentConfig, "当前 config.toml", options);
        var target = ParseTable(targetConfig, "目标 config.toml", options);
        var targetProvider = ReadString(target, "model_provider") ?? "openai";

        // These values redirect authentication or provider traffic. A value left behind by
        // the other login mode must not win merely because the target profile omits it.
        foreach (var key in AuthenticationRoutingKeys)
        {
            if (!target.ContainsKey(key))
            {
                current.Remove(key);
            }
        }

        MergeTables(current, target, metadata);
        current["model_provider"] = targetProvider;
        var merged = TomlSerializer.Serialize(current, options);
        return NormalizeNewlines(merged, currentConfig);
    }

    public static string RequireExplicitProvider(string configText)
    {
        if (string.IsNullOrWhiteSpace(configText))
        {
            throw new CodexSwitchException("config.toml 不能为空。");
        }

        var provider = ReadRootString(configText, "model_provider");
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new CodexSwitchException("config.toml 根配置中缺少 model_provider。");
        }

        return provider;
    }

    public static string? ApiBaseUrl(string configText)
    {
        var provider = Provider(configText);
        var root = ParseTable(configText, "config.toml");
        return root.TryGetValue("model_providers", out var providersValue)
            && providersValue is TomlTable providers
            && providers.TryGetValue(provider, out var providerValue)
            && providerValue is TomlTable providerTable
            ? ReadString(providerTable, "base_url")
            : null;
    }

    public static string? ReadRootString(string text, string key)
    {
        return ReadString(ParseTable(text, "config.toml"), key);
    }

    private static TomlTable ParseTable(
        string text,
        string description,
        TomlSerializerOptions? options = null)
    {
        try
        {
            return TomlSerializer.Deserialize<TomlTable>(
                text.TrimStart('\uFEFF'),
                options ?? new TomlSerializerOptions()) ?? new TomlTable();
        }
        catch (Exception ex)
        {
            throw new CodexSwitchException($"{description} 不是有效的 TOML。", ex);
        }
    }

    private static string? ReadString(TomlTable table, string key) =>
        table.TryGetValue(key, out var value) && value is string text ? text : null;

    private static void MergeTables(
        TomlTable current,
        TomlTable target,
        ITomlMetadataStore metadata)
    {
        foreach (var pair in target)
        {
            if (current.TryGetValue(pair.Key, out var existing)
                && existing is TomlTable currentTable
                && pair.Value is TomlTable targetTable)
            {
                MergeTables(currentTable, targetTable, metadata);
                continue;
            }

            var isNew = !current.ContainsKey(pair.Key);
            current[pair.Key] = pair.Value;
            if (isNew)
            {
                CopyPropertyMetadata(target, current, pair.Key, metadata);
            }
        }
    }

    private static void CopyPropertyMetadata(
        TomlTable source,
        TomlTable destination,
        string key,
        ITomlMetadataStore metadata)
    {
        if (!metadata.TryGetProperties(source, out var sourceProperties)
            || sourceProperties is null
            || !sourceProperties.TryGetProperty(key, out var property))
        {
            return;
        }

        if (!metadata.TryGetProperties(destination, out var destinationProperties)
            || destinationProperties is null)
        {
            destinationProperties = new TomlPropertiesMetadata();
            metadata.SetProperties(destination, destinationProperties);
        }

        destinationProperties.SetProperty(key, property);
    }

    private static string NormalizeNewlines(string text, string reference)
    {
        var newline = reference.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\r', '\n');
        normalized = newline == "\n"
            ? normalized
            : normalized.Replace("\n", newline, StringComparison.Ordinal);
        return normalized + newline;
    }

}
