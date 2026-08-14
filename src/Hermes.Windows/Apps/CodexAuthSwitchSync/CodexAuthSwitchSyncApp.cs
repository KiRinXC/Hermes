using System.Windows;
using System.Windows.Media;
using Hermes.Windows.Apps.CodexAuthSwitchSync.Services;
using Hermes.Windows.Apps.CodexAuthSwitchSync.UI;
using Hermes.Windows.Apps.Contracts;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync;

public sealed class CodexAuthSwitchSyncApp : IHermesApp, ISettingsSaveParticipant
{
    private static readonly Geometry AppIcon = Geometry.Parse(
        "M3,4 L13,4 L13,12 L3,12 Z M6,1 L10,1 L10,4 M6,7 L10,7 M8,5 L8,10 M1,8 L3,8 M13,8 L15,8");

    private readonly CodexAuthSwitchService _service;
    private CodexAuthSwitchSyncView? _view;

    public CodexAuthSwitchSyncApp(CodexAuthSwitchService service)
    {
        _service = service;
    }

    public string Id => CodexLocations.AppId;

    public string Name => "Codex 认证管理";

    public string Description => "切换 ChatGPT / API 认证，并让本地会话索引保持一致";

    public Geometry Icon => AppIcon;

    public FrameworkElement CreateView() => _view ??= new CodexAuthSwitchSyncView(_service);

    public bool HasPendingChanges => _view?.HasPendingChanges == true;

    public Task<SettingsSaveResult> SavePendingChangesAsync() => _view is null
        ? Task.FromResult(SettingsSaveResult.Completed())
        : _view.SavePendingChangesAsync();

    public void DiscardPendingChanges() => _view?.DiscardPendingChanges();
}
