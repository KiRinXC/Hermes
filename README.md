# Hermes

Hermes 是一个面向 Windows 10/11 的系统级 AI 翻译助手。它常驻托盘，监听用户的快捷键和文本选择操作，在不打断当前工作的前提下，把选中的英文内容翻译成简体中文。

这个项目不是浏览器插件，而是 Windows 桌面应用。它的目标是在 Chrome、Edge、VS Code、记事本、PDF 阅读器、办公软件、聊天工具等主流应用中提供轻量的全局划词翻译体验。

## 当前能力

- 常驻系统托盘，支持暂停/恢复、设置、翻译剪贴板和退出。
- 默认快捷键为 `Ctrl+Alt+E`，可翻译当前选中文本。
- 鼠标选中文本后，在可识别选区时显示悬浮翻译按钮。
- 优先通过 Windows UI Automation 读取选中文本。
- 在显式触发翻译时支持受控剪贴板兜底。
- 使用 OpenAI 兼容的 Responses API 调用翻译模型。
- API Key 使用 Windows DPAPI 本地加密保存。
- 翻译卡片支持加载态、复制译文、重新翻译、固定、关闭和错误提示。
- 支持浅色、深色和跟随系统主题。
- 翻译历史能力已接入本地存储，默认关闭。

## 现实边界

Windows 上不同应用暴露选区的方式不一致，所以“任何应用都能精准在选区右上角弹按钮”不是一个稳定承诺。Hermes 当前采用三层策略：

1. 优先使用 UI Automation 读取支持的文本控件选区。
2. 鼠标选区识别失败时，快捷键仍可作为稳定入口。
3. 显式触发翻译时，可以通过受控复制读取当前选区或剪贴板文本。

因此，真实测试时需要重点观察：哪些应用能自动显示按钮，哪些应用需要快捷键，哪些应用需要剪贴板兜底。

## 项目结构

```text
src/Hermes.Windows/
├─ App.xaml / App.xaml.cs          # 应用启动、服务组装、生命周期
├─ Shell/                          # 设置窗口
├─ Tray/                           # 托盘菜单和托盘通知
├─ Input/                          # 全局快捷键、键盘 hook、鼠标 hook
├─ Selection/                      # 选区读取、剪贴板兜底、文本校验
├─ Overlay/                        # 悬浮按钮、翻译卡片、定位服务
├─ Translation/                    # OpenAI Responses API 翻译封装
├─ Settings/                       # 设置读写、DPAPI 密钥存储、开机启动
├─ History/                        # 本地翻译历史
├─ Infrastructure/                 # 日志、路径、Win32、单实例等基础设施
├─ Resources/                      # 应用图标和托盘图标资源
└─ UI/Themes/                      # 主题资源和组件样式

tests/Hermes.Tests/                # 轻量级控制台测试套件
Design.md                          # 项目设计、模块说明和变更同步记录
```

## 开发环境

项目目标框架为 `net10.0-windows`，本机开发 SDK 位于：

```powershell
D:\Code\Env\dotnet\dotnet.exe
```

仓库已内置项目级环境配置：

- `global.json` 固定开发 SDK 为 `10.0.300`。
- `NuGet.Config` 优先使用 `.nuget\offline` 离线包源，再使用 `nuget.org`。
- `scripts\Use-HermesEnv.ps1` 会把 .NET/NuGet 缓存导向仓库内目录，避免污染用户全局环境。
- `.nuget\offline` 用于存放自包含发布所需 runtime packs，不进入 Git。

初始化当前 PowerShell 环境：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Use-HermesEnv.ps1
```

常用命令：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Restore-Hermes.ps1
powershell -ExecutionPolicy Bypass -File scripts\Test-Hermes.ps1
powershell -ExecutionPolicy Bypass -File scripts\Publish-Hermes.ps1
```

如果需要走本地代理，可给脚本加 `-UseLocalProxy`，默认代理地址为 `http://127.0.0.1:7890`。

## 打包和真实使用测试

日常真实使用测试优先发布为完整自包含 portable 包，避免被目标机器是否安装 .NET Runtime 影响。

推荐发布目录：

```text
artifacts\publish\Hermes.Windows\manual-test\win-x64-self-contained\
```

推荐发布命令：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Publish-Hermes.ps1
```

脚本会使用 `NuGet.Config` 中的 `.nuget\offline` 离线源。当前需要的离线 runtime packs 是：

```text
microsoft.aspnetcore.app.runtime.win-x64.10.0.8.nupkg
microsoft.netcore.app.runtime.win-x64.10.0.8.nupkg
microsoft.windowsdesktop.app.runtime.win-x64.10.0.8.nupkg
```

如果离线包缺失且 NuGet 无法访问，可以临时发布 framework-dependent 包用于本机试用：

```powershell
D:\Code\Env\dotnet\dotnet.exe publish src\Hermes.Windows\Hermes.Windows.csproj -c Release -r win-x64 --no-self-contained -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true --artifacts-path artifacts\dotnet -o artifacts\publish\Hermes.Windows\manual-test\win-x64-framework-dependent
```

这个临时包适合当前开发机测试，但换到没有 .NET Desktop Runtime 10 的机器可能无法启动。

真实试用时可以直接运行：

```powershell
artifacts\publish\Hermes.Windows\manual-test\win-x64-self-contained\Hermes.Windows.exe
```

如果本轮只生成了临时 framework-dependent 包，则运行：

```powershell
artifacts\publish\Hermes.Windows\manual-test\win-x64-framework-dependent\Hermes.Windows.exe
```

如果当前机器没有全局安装 .NET Desktop Runtime 10，可以使用本轮生成的本地运行时测试包：

```text
artifacts\publish\Hermes.Windows\manual-test\win-x64-local-runtime\Run-Hermes.cmd
```

双击 `Run-Hermes.cmd` 会使用包内 `dotnet/` 运行时启动 `app/Hermes.Windows.dll`。

如果需要更像日常软件使用，可以把整个发布目录复制到：

```text
D:\Apps\Hermes\
```

应用设置、API Key、日志和历史记录写入 `%LOCALAPPDATA%\Hermes\`，替换发布目录不会清空这些用户数据。

## API 配置

从托盘图标打开设置窗口，配置：

- Provider：OpenAI 或 OpenAI-compatible
- Base URL：默认 `https://api.openai.com/v1`
- API Key：使用 Windows DPAPI 加密存储
- Model：默认 `gpt-4.1-mini`，可在设置中修改

请求会发送到 `{Base URL}/responses`。

## 隐私说明

- 只有用户按快捷键、点击悬浮按钮或选择翻译剪贴板时才会发送文本。
- API Key 不写入 `settings.json`。
- 翻译历史默认关闭。
- 日志会隐藏 API Key 形态的内容，默认不记录完整原文和译文。
- 排除应用和敏感应用列表由设置维护，受保护输入控件会被跳过。

## 设计文档同步

`Design.md` 是项目结构、运行流程、模块职责和功能变更的同步文档。以后每次新增、修改或删减功能，都需要同步更新 `Design.md` 中对应模块和变更记录。
