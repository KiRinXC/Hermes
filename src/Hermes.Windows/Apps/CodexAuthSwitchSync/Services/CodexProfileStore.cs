using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hermes.Windows.Apps.CodexAuthSwitchSync.Domain;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Services;

public sealed class CodexProfileStore
{
    private static readonly byte[] Header = "HCAS1"u8.ToArray();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly CodexLocations _locations;

    public CodexProfileStore(CodexLocations locations)
    {
        _locations = locations;
    }

    public bool Exists(CodexAuthMode mode) => File.Exists(_locations.GetProfilePath(mode));

    public CodexProfileSummary GetSummary(CodexAuthMode mode)
    {
        if (!Exists(mode))
        {
            return new CodexProfileSummary(false, mode);
        }

        var profile = Load(mode);
        return new CodexProfileSummary(
            true,
            mode,
            CodexToml.Provider(profile.ConfigText),
            CodexToml.ReadRootString(profile.ConfigText, "model") ?? "（默认）",
            CodexToml.ReadRootString(profile.ConfigText, "model_reasoning_effort") ?? "（默认）",
            CodexToml.ApiBaseUrl(profile.ConfigText),
            profile.SavedAt);
    }

    public CodexProfileSummary CaptureCurrent(CodexAuthMode? expectedMode = null)
    {
        if (!File.Exists(_locations.AuthPath))
        {
            throw new CodexSwitchException("当前 .codex/auth.json 不存在，请先在 Codex 中完成登录。");
        }

        var config = File.Exists(_locations.ConfigPath)
            ? File.ReadAllText(_locations.ConfigPath, Encoding.UTF8)
            : string.Empty;
        var auth = File.ReadAllText(_locations.AuthPath, Encoding.UTF8);
        var mode = InferMode(auth);
        if (mode == CodexAuthMode.Unknown)
        {
            throw new CodexSwitchException("当前 auth.json 中没有可识别的 ChatGPT 或 API 认证。");
        }

        if (expectedMode is not null && mode != expectedMode)
        {
            throw new CodexSwitchException($"当前实际是 {ModeLabel(mode)} 认证，不能保存为 {ModeLabel(expectedMode.Value)} 档案。");
        }

        return Save(mode, config, auth);
    }

    public CodexApiProfileDraft GetApiDraft()
    {
        if (Exists(CodexAuthMode.Api))
        {
            var existing = Load(CodexAuthMode.Api);
            return new CodexApiProfileDraft(existing.ConfigText, existing.AuthText);
        }

        if (File.Exists(_locations.AuthPath))
        {
            var activeAuth = File.ReadAllText(_locations.AuthPath, Encoding.UTF8);
            if (InferMode(activeAuth) == CodexAuthMode.Api)
            {
                var activeConfig = File.Exists(_locations.ConfigPath)
                    ? File.ReadAllText(_locations.ConfigPath, Encoding.UTF8)
                    : string.Empty;
                return new CodexApiProfileDraft(activeConfig, activeAuth);
            }
        }

        return new CodexApiProfileDraft(string.Empty, string.Empty);
    }

    public CodexProfileSummary ConfigureApiFiles(string configText, string? authText)
    {
        _ = CodexToml.RequireExplicitProvider(configText);
        var existing = Exists(CodexAuthMode.Api) ? Load(CodexAuthMode.Api) : null;
        string resolvedAuth;
        if (!string.IsNullOrWhiteSpace(authText))
        {
            resolvedAuth = authText;
        }
        else if (existing is not null)
        {
            resolvedAuth = existing.AuthText;
        }
        else
        {
            throw new CodexSwitchException("首次配置 API 档案时必须粘贴完整 auth.json。");
        }

        return Save(CodexAuthMode.Api, configText, resolvedAuth);
    }

    internal CodexProfileBundle Load(CodexAuthMode mode)
    {
        var path = _locations.GetProfilePath(mode);
        if (!File.Exists(path))
        {
            throw new CodexSwitchException($"尚未保存 {ModeLabel(mode)} 的完整 config + auth 档案。");
        }

        try
        {
            var stored = File.ReadAllBytes(path);
            if (stored.Length <= Header.Length || !stored.AsSpan(0, Header.Length).SequenceEqual(Header))
            {
                throw new CodexSwitchException($"{ModeLabel(mode)} 档案格式无效。");
            }

            var json = Encoding.UTF8.GetString(CodexDpapi.Unprotect(stored[Header.Length..]));
            var profile = JsonSerializer.Deserialize<CodexProfileBundle>(json, JsonOptions)
                ?? throw new CodexSwitchException($"{ModeLabel(mode)} 档案内容为空。");
            ValidateAuth(profile.AuthText, mode);
            if (profile.Mode != mode)
            {
                throw new CodexSwitchException($"{ModeLabel(mode)} 档案类型不匹配。");
            }

            return profile;
        }
        catch (CodexSwitchException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CodexSwitchException($"无法读取 {ModeLabel(mode)} 档案。", ex);
        }
    }

    internal CodexProfileSummary Save(CodexAuthMode mode, string configText, string authText)
    {
        ValidateAuth(authText, mode);
        var bundle = new CodexProfileBundle(1, mode, DateTimeOffset.Now, configText, authText);
        var clear = JsonSerializer.SerializeToUtf8Bytes(bundle, JsonOptions);
        var protectedBytes = CodexDpapi.Protect(clear);
        var output = new byte[Header.Length + protectedBytes.Length];
        Header.CopyTo(output, 0);
        protectedBytes.CopyTo(output, Header.Length);
        _locations.EnsureCreated();
        AtomicFile.WriteAllBytes(_locations.GetProfilePath(mode), output);
        return GetSummary(mode);
    }

    internal CodexProfileSummary RefreshAuth(
        CodexAuthMode mode,
        string currentConfigText,
        string currentAuthText)
    {
        var profileConfig = Exists(mode)
            ? Load(mode).ConfigText
            : currentConfigText;
        return Save(mode, profileConfig, currentAuthText);
    }

    public static CodexAuthMode InferMode(string authText)
    {
        JsonObject auth;
        try
        {
            auth = JsonNode.Parse(authText.TrimStart('\uFEFF')) as JsonObject
                ?? throw new JsonException();
        }
        catch (JsonException ex)
        {
            throw new CodexSwitchException("auth.json 不是有效的 JSON 对象。", ex);
        }

        var explicitMode = auth["auth_mode"]?.GetValue<string>()?.ToLowerInvariant() ?? string.Empty;
        if (explicitMode.StartsWith("chatgpt", StringComparison.Ordinal)
            || explicitMode is "headers" or "agentidentity" or "personalaccesstoken"
            || HasValue(auth, "tokens")
            || HasValue(auth, "agent_identity")
            || HasValue(auth, "personal_access_token"))
        {
            return CodexAuthMode.ChatGpt;
        }

        if (explicitMode is "apikey" or "api_key" or "api-key" || HasString(auth, "OPENAI_API_KEY"))
        {
            return CodexAuthMode.Api;
        }

        if (auth.Any(pair => !string.Equals(pair.Key, "auth_mode", StringComparison.Ordinal)
            && HasValue(pair.Value)))
        {
            return CodexAuthMode.Api;
        }

        return CodexAuthMode.Unknown;
    }

    private static void ValidateAuth(string authText, CodexAuthMode expected)
    {
        if (InferMode(authText) != expected)
        {
            throw new CodexSwitchException($"这份 auth.json 不包含可用的 {ModeLabel(expected)} 认证。");
        }
    }

    private static bool HasValue(JsonObject value, string name) =>
        HasValue(value[name]);

    private static bool HasValue(JsonNode? node) =>
        node is not null && node.ToJsonString() is not "null" and not "{}" and not "[]" and not "\"\"";

    private static bool HasString(JsonObject value, string name) =>
        value[name] is JsonValue item && item.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text);

    private static string ModeLabel(CodexAuthMode mode) => mode switch
    {
        CodexAuthMode.ChatGpt => "ChatGPT",
        CodexAuthMode.Api => "API",
        _ => "未知"
    };
}
