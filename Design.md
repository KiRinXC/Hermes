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
├─ UI.md                           # 当前 UI 设计原则、视觉系统和组件规范
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
├─ docs/
│  ├─ assets/                       # README 和发布页视觉素材，当前头图为 Info.png
│  ├─ prompts/                      # 文生图提示词等可复用创意素材
│  └─ release-notes/                # GitHub Release 文案草稿
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
OpenAiTranslationService 以流式 Responses API 请求翻译
  ↓
OverlayManager 显示翻译卡片并逐段追加译文
```

快捷键翻译是当前最稳定的主路径，也是后续真实使用测试的第一优先级。

### 鼠标划词悬浮按钮

```text
用户按住 Ctrl 拖选文本
  ↓
MouseHookService 捕捉 Ctrl 选择手势完成
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

被动鼠标路径不执行剪贴板复制，且普通拖选不会进入候选判断或触发诊断记录；只有从按下到释放都保持 Ctrl 的拖选才会继续评估悬浮按钮。这样既减少鼠标 hook 后续工作量，也避免用户未明确触发时污染剪贴板或上传误判文本。

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

当前设置窗口采用固定 `800 × 600` 的无边框 WPF 壳，窗口内部按 Header、Body、Footer 三段式组织。Header 包含紧凑品牌区、可点击录制的快捷键键帽和五个文字页签；页签与应用图标保持更舒展的垂直间距，外层壳体不再使用会被透明窗口裁切成黑框的外边距阴影。Body 使用圆角分组卡片承载常规、翻译、外观、隐私和高级诊断；Footer 固定放置保存和状态反馈。设置窗口文字层级以 Regular/Medium 为主，不使用 Bold/SemiBold 作为常规 UI 字重。窗口打开时执行淡入与缩放动效；为保证透明无边框窗口四角干净，设置窗不再启用矩形 DWM/Mica 背景，而由本地壳体背景和运行时圆角裁剪承载视觉外观。

设置页控件已从传统表单升级为更轻量的交互形态：布尔项使用设置页本地 ToggleSwitch，外观规格使用 Slider，主题使用分段选择器，API Key 支持显示/隐藏，右上角键帽按钮支持录制组合键。主题分段选择器的轨道、选中胶囊和描边都使用本地动态主题资源，浅色模式下以灰色轨道、白色选中胶囊和细描边明确当前选项。鼠标点击页签切换设置分区后，会在内容加载完成时清掉 WPF 自动落到第一个开关上的焦点，避免隐私页“保存翻译历史”等 ToggleSwitch 出现误导性的蓝色焦点框；键盘导航路径仍保留可见焦点。键帽按钮整体背景和代码生成的单个键帽都使用动态主题资源，浅色模式下会立即切换为浅灰外壳和浅色键帽；进入录制后再次点击按钮、点击窗口其它区域或按 Esc 会取消录制并清掉蓝色焦点框，录制过程不再写入 Footer 状态提示。测试连接作为 API 凭据上下文动作放在 API Key 行右侧，清空历史作为高级诊断上下文动作放在高级页内。翻译页的模型字段保持为手动输入框，避免模型选择控件在紧凑布局中截断；目标语言固定为中文，不再在设置页展示。翻译页还提供可编辑的系统 Prompt，空白时回退到默认英文到简体中文翻译提示词。设置窗口内置本地 TextBox、PasswordBox、ComboBox、ComboBoxItem、FooterButton、Tab、ToggleSwitch、Slider 和滚动条样式，避免设置页回落到原生控件质感。`SettingsWindowOptions` 用于分离设置项显示文案和持久化值，避免中文高级文案写入配置文件。`SettingsWindowThemePalettes` 负责设置窗口自身的浅色/深色调色板，外观页切换主题时会替换本地 brush 资源；设置窗口样式使用 DynamicResource 引用这些 brush，因此浅色/深色/跟随系统会立即作用于设置窗口自身。浅色主题下开关关闭轨道、滑块未选轨道和快捷键键帽使用可读灰阶，避免黑色控件在浅色面板中过重或不可见。

### Tray

`TrayService` 维护系统托盘图标和菜单。左键单击托盘图标会直接打开设置窗口；右键菜单保留暂停/恢复、翻译剪贴板、设置和退出，不再显示历史入口。托盘是用户无需打开主窗口即可控制应用的主要入口。

### Input

`HotkeyService` 负责注册全局快捷键。`KeyboardHookService` 和 `MouseHookService` 负责低级输入监听，用于关闭被动 UI、捕捉 Esc、识别鼠标选择手势。Hook 内不做重计算，只转发事件给协调层。

### Selection

选区模块负责“从哪里拿到文本”和“文本是否值得翻译”。

- `UiAutomationSelectionProvider` 通过 Windows UI Automation 读取当前选区，读取工作运行在后台线程，避免点击悬浮翻译按钮时卡住 WPF UI 线程。
- `ClipboardSelectionProvider` 在显式触发时使用受控复制或读取剪贴板文本；剪贴板操作运行在专用 STA 线程，不再通过主 Dispatcher 执行 `Ctrl+C` 和剪贴板读写。
- `ForegroundWindowService` 判断前台窗口、排除应用和敏感控件。
- `SelectionTextValidator` 根据语言、长度和设置校验文本。
- `SelectionOrchestrator` 决定显式触发、被动鼠标和剪贴板翻译时的读取策略。

### Overlay

悬浮层模块负责按钮、翻译卡片和位置计算。

- `FloatingButtonWindow` 显示按住 Ctrl 划词后的轻量翻译按钮。
- `FloatingButtonWindow` 的浅色/深色图标基于 `src\Hermes.Windows\Resources\Icons\FloatingButtonLight.svg` 和 `FloatingButtonDark.svg` 的路径和填充色绘制，未点击和未选中状态不再对图标本体施加模糊或变淡效果。按钮外层固定为 44×44 透明圆角命中区，内部 25×25 图标不参与命中测试，用户点击热区任意位置即可触发翻译。
- `TranslationPopupWindow` 显示加载、流式译文、长耗时、成功、错误、复制、重试、固定和关闭状态；成功完成且未固定时，鼠标点击浮窗外部会关闭卡片。
- `TranslationPopupWindow` 顶部使用应用图标作为品牌标识；用户可以从卡片背景、正文和原文区域等非交互表面拖动卡片，按钮、开关、滚动条等交互控件不会触发拖拽。
- `OverlayPositionService` 负责多屏幕边界内的位置约束。
- `OverlayManager` 对外提供显示、追加流式译文、完成翻译和关闭悬浮 UI 的统一入口，并区分普通被动 UI 关闭与已完成未固定浮窗的外部点击关闭。

### Translation

翻译模块封装 OpenAI 兼容 Responses API。

- `TranslationPromptBuilder` 构造英文到简体中文翻译指令，并提供可配置 Prompt 的默认值。
- `OpenAiTranslationService` 读取设置和密钥，发送 Responses API 请求。普通路径解析 `output_text` 或 `output` 内容；翻译主路径使用 `stream = true` 读取 SSE 事件，按 `response.output_text.delta` 逐段输出，并在完成时汇总最终译文。
- `TranslationStreamEvent` 描述流式翻译的增量、完成和失败事件。
- `TranslationCoordinator` 串联选区、流式翻译、历史和 UI，是翻译工作流协调层。它会在 Ctrl 条件不满足时直接跳过被动鼠标候选流程，并对流式 delta 做轻量批处理后再刷新 UI；用户关闭翻译卡片时会取消当前请求，成功完成后再保存最终译文。

错误处理覆盖缺少 API Key、鉴权失败、余额或额度不足、限流、无效请求、网络错误、超时、取消和空响应。

### Settings

设置模块负责本地配置和密钥存储。

- `SettingsService` 读写 `%LOCALAPPDATA%\Hermes\settings.json`。
- `DpapiSecretStorageService` 使用 Windows DPAPI 加密保存 API Key 到 `secrets.dat`。
- `StartupRegistrationService` 管理开机启动注册。
- `AppSettings` 定义 API、翻译、触发、UI、隐私和启动设置，其中翻译设置包含可编辑系统 Prompt。

### History

历史模块已具备本地存储服务，当前默认关闭保存历史。托盘菜单不展示历史入口；清空历史放在设置窗口高级页中，并会同步清空该页展示的触发诊断队列。

### Infrastructure

基础设施模块包括日志、路径、Win32 方法、应用身份、单实例守卫和日志脱敏。用户数据目录统一为 `%LOCALAPPDATA%\Hermes\`，并保留从旧目录迁移数据的兼容逻辑。

### UI/Themes

主题模块维护浅色、深色、设计 token 和组件样式。运行时根据设置应用 `System`、`Light` 或 `Dark` 主题。深色主题主背景已调整为 `#0A0A0C`。设置窗口拥有独立浅色/深色调色板，ToggleSwitch 开启态使用蓝紫渐变，浅色模式的关闭态轨道和 Slider 未选轨道使用 Apple 风格中性灰，确保控件在白天模式下仍清晰可读。整体 UI 字重控制在 Regular/Medium，标题、按钮、页签和状态文字用 Medium 建立层级，避免大面积加粗造成粗糙感。

### Tests

`tests/Hermes.Tests` 是轻量控制台测试套件，覆盖设置、脱敏、选区校验、选择候选、快捷键解析、鼠标 Ctrl 触发门控、设置窗口选项文案和值映射、历史/诊断清理、OpenAI 普通/流式响应解析、悬浮按钮清晰度约束、翻译卡片拖拽/外部点击关闭入口和 UI 字重约束等逻辑。WPF 可视交互仍需要真实应用试用补充验证。

## 打包策略

项目环境由仓库自身固定：

- `global.json` 指定 .NET SDK `10.0.300`。
- `NuGet.Config` 使用 `.nuget\offline` 作为优先包源，并保留 `nuget.org` 作为在线包源。
- `scripts\Use-HermesEnv.ps1` 统一设置 `DOTNET_CLI_HOME`、NuGet 缓存、scratch/cache 目录和可选代理，并确保 `.nuget\offline` 本地源目录存在。
- `scripts\Restore-Hermes.ps1`、`scripts\Test-Hermes.ps1`、`scripts\Publish-Hermes.ps1` 和 `scripts\Package-HermesRelease.ps1` 是标准入口。

自包含发布需要以下 runtime packs 放在 `.nuget\offline`：

```text
microsoft.aspnetcore.app.runtime.win-x64.10.0.8.nupkg
microsoft.netcore.app.runtime.win-x64.10.0.8.nupkg
microsoft.windowsdesktop.app.runtime.win-x64.10.0.8.nupkg
```

如果 `.nuget\offline` 为空，`NuGet.Config` 仍会回退到 `nuget.org`；环境脚本会创建空目录，避免 NuGet 因本地源路径不存在而中断发布。

真实使用测试优先采用完整自包含 portable 包：

```text
artifacts\publish\Hermes.Windows\manual-test\win-x64-self-contained\
```

日常修复发布固定覆盖上述 `win-x64-self-contained` 目录，不再为每次 UI 或小修复新增带后缀的发布目录；如果目录被正在运行的 Hermes 锁定，应先提示用户退出应用再覆盖，避免继续产生废弃包。

推荐命令：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Publish-Hermes.ps1
```

对外 GitHub Release 采用 zip 包分发，脚本会先生成固定 self-contained portable 目录，再压缩为版本化 zip 并生成 SHA256 校验文件：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Package-HermesRelease.ps1 -Version 0.1.0
```

输出目录：

```text
artifacts\release\v0.1.0\
├─ Hermes-v0.1.0-win-x64-portable.zip
└─ checksums.txt
```

对应的 GitHub Release 说明草稿保存在 `docs\release-notes\`。README 面向最终用户介绍下载、运行、隐私和使用边界，内部实现细节继续放在 `Design.md`。

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

GitHub 发布前的仓库边界：

- `docs/`、`README.md`、`UI.md`、`Design.md`、`scripts/`、`src/` 和 `tests/` 应进入 Git。
- `.codex/`、`openspec/`、`.dotnet-home/`、`.nuget/`、`artifacts/`、`bin/`、`obj/`、测试结果、日志、密钥、签名证书和压缩包由 `.gitignore` 排除。
- README 头图 `docs/assets/Info.png`、文生图提示词和 release notes 属于对外发布资产，需要保留。

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
- 当前没有完整历史列表 UI，仅保留高级页清空历史动作。
- 设置窗口的 Mica 背景依赖 Windows 11 DWM 能力；在不支持的系统或透明窗口组合受限时会退回内置深色背景。
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
| 2026-05-26 | 设置主窗口重构为固定 800×600 无边框深色 Mica 风格面板，新增三段式布局、品牌 Header、键帽快捷键、卡片化六页设置、Footer 动作栏、API Key 显示/隐藏、主题分段选择器、外观滑块、快捷键录制和测试连接 loading/success 状态。 | Shell / UI/Themes |
| 2026-05-26 | 修复设置窗口 XAML 入口动效挂载到 `Window.RenderTransform` 导致托盘设置无法弹出的问题；修复 ComboBox 显示 `SettingsOption` 默认字符串、输入框文字垂直裁切，并让外观页主题切换立即作用于设置窗口自身。 | Shell / UI/Themes |
| 2026-05-26 | 精修设置主窗口为“果系极简面板”：移除松散装饰副标题，补齐设置窗口本地 TextBox、PasswordBox、ComboBox 和 ComboBoxItem 样式，让输入框、密钥框、下拉框与控制中心卡片体系统一。 | Shell / UI/Themes |
| 2026-05-26 | 修复设置窗口打开失败：本地主题切换不再直接修改可能被冻结的 WPF Brush 资源，改为替换新的 SolidColorBrush，避免点击托盘“设置”时因只读刷子异常导致窗口不弹出。 | Shell / UI/Themes |
| 2026-05-26 | 根据视觉验收反馈调整设置主窗口：移除标题旁“全局控制中心”，下移页签导航，将右上角快捷键键帽改为可点击录制按钮，移除独立“快捷键”页签；测试连接移入翻译页 API Key 行，清空历史移入高级页，Footer 只保留状态与保存。 | Shell / UI/Themes |
| 2026-05-26 | 调整托盘交互：左键单击托盘图标直接打开设置，右键菜单移除历史入口；设置窗口本地主题资源改为 DynamicResource，使浅色、深色和跟随系统主题立即作用于设置界面。 | Tray / Shell / UI-Themes |
| 2026-05-26 | 根据浅色模式和翻译页验收反馈继续精修设置窗口：拉大品牌区与页签垂直间距；设置页使用本地 ToggleSwitch 和 Slider 资源，修正白天模式开关/滑块灰阶可读性；API Key 输入框缩短并将测试连接放在右侧；模型字段改为可刷新列表，调用 `/models` 解析可用模型；目标语言固定中文不再展示；新增可编辑系统 Prompt 并将其传入 Responses API 请求。 | Shell / Translation / Settings / UI-Themes |
| 2026-05-26 | 根据模型字段显示验收反馈回退模型自动解析入口：移除设置页模型刷新按钮和 OpenAI 兼容 `/models` 调用，Model 恢复为普通手动输入框，避免紧凑宽度下模型名截断。 | Shell / Translation / Settings |
| 2026-05-27 | 优化设置与翻译 UI：拉开设置页图标和文字距离；翻译卡片左上角改用应用图标；卡片支持从非交互区域整体拖动；划词浮动按钮在不同主题和未选中状态下保持清晰。 | Shell / Overlay / UI-Themes |
| 2026-05-27 | 翻译主流程改为 Responses API 流式请求，按 `response.output_text.delta` 增量更新翻译卡片，完成后保存最终译文；保留非流式路径用于测试连接和兼容测试。 | Translation / Overlay / History |
| 2026-05-27 | 发布脚本验证通过 `D:\Code\Env\dotnet` 生成 `win-x64 self-contained` portable 包，并修复 `.nuget\offline` 本地源目录缺失导致 publish restore 中断的问题。 | 打包发布 / 开发环境 |
| 2026-05-27 | 修复设置窗口外层黑框：移除透明无边框窗口外层被裁切的黑色阴影和外边距，并进一步拉开应用图标与页签菜单的垂直距离。 | Shell / UI-Themes |
| 2026-05-27 | 划词浮动翻译按钮改用独立浅色/深色 SVG 主题图标，并新增 44×44 透明圆角命中区，解决只能点击图标局部区域才能触发翻译的问题。 | Overlay / UI-Themes |
| 2026-05-27 | 收敛全局 UI 字重：设置页、托盘、翻译卡片和通用组件从 Bold/SemiBold 调整为 Medium/Regular，新增测试防止常规 UI 回退到重字重。 | Shell / Tray / Overlay / UI-Themes |
| 2026-05-27 | 收紧被动鼠标触发：只有按住 Ctrl 完成拖选才会进入候选评估，普通拖选不再记录触发诊断；清空历史同步清空高级页诊断记录；已完成且未固定的翻译浮窗支持点击外部关闭；流式 UI 更新改为小批量刷新以降低卡顿。 | Input / Selection / Shell / Overlay / Translation |
| 2026-05-27 | 优化点击悬浮翻译图标后的响应：UI Automation 选区读取改到后台线程，剪贴板兜底改到专用 STA 线程，避免读取选区和受控复制阻塞 WPF 主线程。 | Selection / Translation |
| 2026-05-27 | 消除设置窗口左上角残留直角阴影：关闭透明窗口上的矩形 DWM/Mica 背景，并为 RootShell 增加运行时圆角裁剪，避免子内容或系统背景越过圆角。 | Shell / UI-Themes |
| 2026-05-27 | 优化设置窗口快捷键键帽：浅色模式下键帽外壳和单键即时恢复浅色搭配；快捷键录制支持再次点击、点击其它区域或按 Esc 取消，取消或完成后清除蓝色焦点框，录制过程不再占用左下角状态提示。 | Shell / UI-Themes |
| 2026-05-27 | 修正设置窗口主题分段选择器浅色模式选中态：选中胶囊改为动态调色板资源，浅色下使用白色胶囊和中性描边，避免“浅色模式”当前选项融进背景。 | Shell / UI-Themes |
| 2026-05-27 | 修正设置页鼠标切换页签后的自动焦点：点击“隐私”等菜单栏时清除 WPF 自动落到第一个 ToggleSwitch 的焦点，避免保存翻译历史开关出现误导性蓝框，同时保留键盘导航焦点。 | Shell / UI-Themes |
| 2026-05-27 | 将 README 改为面向最终用户的项目入口，新增章鱼主题 README 插图、文生图提示词、v0.1.0 Release Notes 草稿和 `Package-HermesRelease.ps1`，用于生成 GitHub Release portable zip 与校验文件。 | 文档维护 / 打包发布 |
| 2026-05-27 | 新增根目录 `UI.md` 记录 Hermes UI 设计规范；README 头图改为 `docs/assets/Info.png`；整合 `.gitignore` 以保留 `docs/`、脚本和应用资源并忽略本地缓存、构建产物、日志、密钥和压缩包；浮动按钮浅色/深色 SVG 从根目录迁移到 `src\Hermes.Windows\Resources\Icons\`，保持根目录整洁。 | 文档维护 / UI-Themes / 仓库结构 |
