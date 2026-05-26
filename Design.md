# Hermes 项目设计文档

本文件记录 Hermes 当前怎么做、为什么这样做，以及后续功能变化需要同步更新的位置。它面向项目维护和回顾，不替代 README 的项目入口说明，也不替代 OpenSpec 的变更提案。

## 文档维护规则

- 每次新增、修改或删减功能，都必须同步更新本文件。
- 如果功能影响用户使用路径，需要更新“核心流程”和对应模块说明。
- 如果功能影响项目结构、配置、隐私策略、打包方式、测试策略，需要更新对应章节。
- 纯格式调整、注释修正、无行为变化的内部整理，可以不写入变更记录。
- 完成实现前，开发 agent 需要检查本文件是否仍与代码一致。

## 产品定位

Hermes 是 Windows 10/11 上的全局 AI 划词翻译助手。它常驻后台，通过托盘、快捷键、鼠标选区、悬浮按钮和翻译卡片，为用户提供低打扰的英文到简体中文翻译体验。

项目优先保证快捷键翻译和剪贴板兜底可用，再逐步增强自动划词按钮和选区定位。全局选区识别在 Windows 上无法做到 100% 精准，因此系统设计中保留多级兜底路径。

## 当前总体结构

```text
Hermes
├─ README.md                       # 给读者的项目入口
├─ Design.md                       # 当前设计、模块职责、变更同步记录
├─ AGENTS.md                       # 给开发 agent 的产品和工作约束
├─ Hermes.sln
├─ src/
│  └─ Hermes.Windows/
│     ├─ App.xaml / App.xaml.cs
│     ├─ Shell/
│     ├─ Tray/
│     ├─ Input/
│     ├─ Selection/
│     ├─ Overlay/
│     ├─ Translation/
│     ├─ Settings/
│     ├─ History/
│     ├─ Infrastructure/
│     ├─ Resources/
│     └─ UI/Themes/
├─ tests/
│  └─ Hermes.Tests/
└─ artifacts/                      # 本地发布产物，忽略 Git
```

## 运行时组装

`App.xaml.cs` 是当前应用的组合根。启动时它负责：

- 创建 `%LOCALAPPDATA%\Hermes\` 数据目录。
- 启用单实例守卫，重复启动时激活已有实例。
- 加载设置并应用主题。
- 初始化 DPAPI 密钥存储、日志、历史、翻译服务、选区服务、悬浮层服务。
- 注册托盘菜单、全局快捷键、键盘 hook、鼠标 hook。
- 根据暂停状态和设置控制触发器启动或停止。

当前服务之间以构造函数直接组装为主，没有引入依赖注入容器。这个选择符合 MVP 体量，后续如果服务数量继续增长，可以再评估是否引入轻量 DI。

## 核心流程

### 快捷键翻译

```text
用户选中文本
  ↓
按 Ctrl+Alt+E
  ↓
HotkeyService 触发
  ↓
TranslationCoordinator
  ↓
SelectionOrchestrator
  ├─ 先读 UI Automation 选区
  └─ 失败后使用受控剪贴板兜底
  ↓
OpenAiTranslationService 调用 Responses API
  ↓
OverlayManager 显示翻译卡片
```

快捷键翻译是当前最稳定的主路径，也是后续真实使用测试的第一优先级。

### 鼠标划词悬浮按钮

```text
用户拖选文本
  ↓
MouseHookService 捕捉选择手势完成
  ↓
SelectionCandidateService 尝试读取候选选区
  ↓
OverlayManager 显示悬浮按钮
  ↓
用户点击按钮
  ↓
TranslationCoordinator 翻译候选文本
  ↓
TranslationPopupWindow 显示结果
```

被动鼠标路径不执行剪贴板复制，优先避免用户未明确触发时污染剪贴板或上传误判文本。

### 剪贴板翻译

```text
用户打开托盘菜单
  ↓
选择翻译剪贴板
  ↓
ClipboardSelectionProvider 读取当前剪贴板文本
  ↓
文本校验通过后翻译
  ↓
显示翻译卡片
```

这是 UI Automation 和选区识别失败时的兜底路径。

## 模块说明

### Shell

`Shell/SettingsWindow` 是设置入口，负责 API、翻译、触发、UI、隐私、开机启动等配置的展示和保存。设置窗口由托盘菜单或翻译卡片中的设置动作打开。

### Tray

`TrayService` 维护系统托盘图标和菜单。当前入口包括暂停/恢复、翻译剪贴板、设置、历史提示和退出。托盘是用户无需打开主窗口即可控制应用的主要入口。

### Input

`HotkeyService` 负责注册全局快捷键。`KeyboardHookService` 和 `MouseHookService` 负责低级输入监听，用于关闭被动 UI、捕捉 Esc、识别鼠标选择手势。Hook 内不做重计算，只转发事件给协调层。

### Selection

选区模块负责“从哪里拿到文本”和“文本是否值得翻译”。

- `UiAutomationSelectionProvider` 通过 Windows UI Automation 读取当前选区。
- `ClipboardSelectionProvider` 在显式触发时使用受控复制或读取剪贴板文本。
- `ForegroundWindowService` 判断前台窗口、排除应用和敏感控件。
- `SelectionTextValidator` 根据语言、长度和设置校验文本。
- `SelectionOrchestrator` 决定显式触发、被动鼠标和剪贴板翻译时的读取策略。

### Overlay

悬浮层模块负责按钮、翻译卡片和位置计算。

- `FloatingButtonWindow` 显示划词后的轻量翻译按钮。
- `TranslationPopupWindow` 显示加载、长耗时、成功、错误、复制、重试、固定和关闭状态。
- `OverlayPositionService` 负责多屏幕边界内的位置约束。
- `OverlayManager` 对外提供显示、更新和关闭悬浮 UI 的统一入口。

### Translation

翻译模块封装 OpenAI 兼容 Responses API。

- `TranslationPromptBuilder` 构造英文到简体中文翻译指令。
- `OpenAiTranslationService` 读取设置和密钥，发送请求，解析 `output_text` 或 `output` 内容。
- `TranslationCoordinator` 串联选区、翻译、历史和 UI，是翻译工作流协调层。

错误处理覆盖缺少 API Key、鉴权失败、余额或额度不足、限流、无效请求、网络错误、超时、取消和空响应。

### Settings

设置模块负责本地配置和密钥存储。

- `SettingsService` 读写 `%LOCALAPPDATA%\Hermes\settings.json`。
- `DpapiSecretStorageService` 使用 Windows DPAPI 加密保存 API Key 到 `secrets.dat`。
- `StartupRegistrationService` 管理开机启动注册。
- `AppSettings` 定义 API、翻译、触发、UI、隐私和启动设置。

### History

历史模块已具备本地存储服务，当前默认关闭保存历史。托盘历史入口目前只提示“已接入本地存储，详细列表后续 UI 展示”。

### Infrastructure

基础设施模块包括日志、路径、Win32 方法、应用身份、单实例守卫和日志脱敏。用户数据目录统一为 `%LOCALAPPDATA%\Hermes\`，并保留从旧目录迁移数据的兼容逻辑。

### UI/Themes

主题模块维护浅色、深色、设计 token 和组件样式。运行时根据设置应用 `System`、`Light` 或 `Dark` 主题。

### Tests

`tests/Hermes.Tests` 是轻量控制台测试套件，覆盖设置、脱敏、选区校验、选择候选、快捷键解析和 OpenAI 响应解析等逻辑。WPF 可视交互仍需要真实应用试用补充验证。

## 打包策略

项目环境由仓库自身固定：

- `global.json` 指定 .NET SDK `10.0.300`。
- `NuGet.Config` 使用 `.nuget\offline` 作为优先包源，并保留 `nuget.org` 作为在线包源。
- `scripts\Use-HermesEnv.ps1` 统一设置 `DOTNET_CLI_HOME`、NuGet 缓存、scratch/cache 目录和可选代理。
- `scripts\Restore-Hermes.ps1`、`scripts\Test-Hermes.ps1`、`scripts\Publish-Hermes.ps1` 是标准入口。

自包含发布需要以下 runtime packs 放在 `.nuget\offline`：

```text
microsoft.aspnetcore.app.runtime.win-x64.10.0.8.nupkg
microsoft.netcore.app.runtime.win-x64.10.0.8.nupkg
microsoft.windowsdesktop.app.runtime.win-x64.10.0.8.nupkg
```

真实使用测试优先采用完整自包含 portable 包：

```text
artifacts\publish\Hermes.Windows\manual-test\win-x64-self-contained\
```

推荐命令：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Publish-Hermes.ps1
```

如果本机缺少自包含发布所需的 .NET runtime packs，且 NuGet 无法访问，可以临时发布 framework-dependent 包用于本机试用：

```powershell
D:\Code\Env\dotnet\dotnet.exe publish src\Hermes.Windows\Hermes.Windows.csproj -c Release -r win-x64 --no-self-contained -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true --artifacts-path artifacts\dotnet -o artifacts\publish\Hermes.Windows\manual-test\win-x64-framework-dependent
```

临时 framework-dependent 包不作为正式测试分发目标，只用于当前开发机或已安装 .NET Desktop Runtime 10 的机器。

如果当前开发机没有全局安装 .NET Desktop Runtime 10，可以从 framework-dependent 输出组装本地运行时测试包：

```text
artifacts\publish\Hermes.Windows\manual-test\win-x64-local-runtime\
├─ app\                         # Hermes framework-dependent 输出
├─ dotnet\                      # 本地 .NET 运行时
└─ Run-Hermes.cmd               # 使用包内 dotnet 启动 Hermes
```

这个包用于人工试用，不替代正式 self-contained publish。后续 NuGet runtime packs 可用后，仍应优先回到 `win-x64-self-contained` 包。

选择自包含文件夹包的原因：

- 不要求测试机器额外安装 .NET Desktop Runtime。
- 比单文件包更容易检查依赖和日志问题。
- 便于直接替换整个目录进行日常试用。
- 后续正式发布前仍可追加单文件包、MSIX 或安装器。

`artifacts/` 是本地构建产物目录，不进入 Git。

## 用户数据和隐私

Hermes 的用户数据保存在：

```text
%LOCALAPPDATA%\Hermes\
```

主要文件：

- `settings.json`：普通设置。
- `secrets.dat`：DPAPI 加密后的 API Key。
- `app.log`：本地日志。
- `history.json`：翻译历史，默认不保存。

设计原则：

- 不自动上传未被用户明确触发的文本。
- 被动鼠标路径不执行剪贴板复制。
- API Key 不写入普通设置文件。
- 日志默认不记录完整原文和译文。
- 支持排除应用和敏感应用。

## 已知限制

- UI Automation 在浏览器、PDF、Electron、自绘编辑器中的行为不完全一致。
- 自动悬浮按钮无法保证所有应用都出现。
- 当前历史入口没有完整列表 UI。
- 真实多显示器、高 DPI、不同应用兼容性需要持续人工试用。
- 当前打包是 portable 测试包，不是正式安装器。

## 设计变更记录

| 日期 | 变更 | 影响范围 |
| --- | --- | --- |
| 2026-05-26 | 建立根目录 `Design.md`，明确项目结构、核心流程、模块职责和文档同步规则。 | 文档维护 |
| 2026-05-26 | 确定真实使用测试优先采用 `win-x64 self-contained` portable 文件夹包，输出到 `artifacts\publish\Hermes.Windows\manual-test\win-x64-self-contained\`。 | 打包发布 |
| 2026-05-26 | 记录自包含 runtime packs 不可用时的临时 `win-x64 framework-dependent` 发布路径，用于当前开发机试用。 | 打包发布 |
| 2026-05-26 | 新增 `win-x64-local-runtime` 测试包约定，通过包内 `dotnet/` 和 `Run-Hermes.cmd` 支持当前机器直接试用。 | 打包发布 |
| 2026-05-26 | 新增 `global.json`、项目级 `NuGet.Config` 和 `scripts\*.ps1` 环境脚本，固定 SDK、离线包源、项目内缓存和标准 restore/test/publish 流程。 | 开发环境 |
