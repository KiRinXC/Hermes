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
│     ├─ Program.cs / App.xaml / App.xaml.cs
│     ├─ Apps/                       # 可扩展内置小应用注册表、应用中心和独立实现
│     │  └─ CodexAuthSwitchSync/     # Codex 认证档案切换与本地会话同步
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

- 启用单实例守卫，重复启动时激活已有实例。
- 首实例创建 `%LOCALAPPDATA%\KiRinXC\Hermes\` 数据目录，并把旧 `%LOCALAPPDATA%\Hermes\` 程序目录中的设置、密钥、历史与内置应用档案迁移出去。
- 加载设置并应用主题。
- 初始化 DPAPI 密钥存储、日志、历史、翻译服务、选区服务、悬浮层服务。
- 创建 `AppRegistry` 并注册内置 `codex-auth-switch-sync` 应用；应用服务仍由组合根显式装配。
- 由 `Program.cs` 在 WPF 初始化前执行 Velopack 启动钩子，但不自动套用待处理更新；组合根随后启动只负责检查与提醒的后台更新监视器。
- 注册托盘菜单，并在启动通知显示后延迟注册全局快捷键、键盘 hook 和鼠标 hook，降低应用刚启动时 UI 线程忙碌造成的鼠标卡顿。
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
ProviderRoutingTranslationService 按设置路由到 Transmart 或 OpenAI
  ↓
OverlayManager 显示翻译卡片并逐段追加译文
```

快捷键翻译是当前最稳定的主路径，也是后续真实使用测试的第一优先级。

### 鼠标划词悬浮按钮

```text
用户先按住 Ctrl 或 Alt，再拖选文本
  ↓
MouseHookService 捕捉选择手势完成
  ↓
SelectionCandidateService 评估手势并尝试预读候选选区
  ↓
OverlayManager 显示悬浮按钮
  ↓
用户点击按钮
  ↓
TranslationCoordinator
  ├─ Ctrl 手势: 翻译候选文本
  └─ Alt 手势: 解释候选术语
  ↓
TranslationPopupWindow 显示结果
```

被动鼠标路径不执行剪贴板复制，且普通拖选不会进入候选判断或触发诊断记录；只有在鼠标按下前已经长按 Ctrl 或 Alt，并且鼠标按下那一刻仍处于按住状态的拖选，才会继续评估悬浮按钮。鼠标按下后再按 Ctrl/Alt 不会补判为有效手势；一旦起手合法，鼠标和键盘的松开顺序不影响结果。实现上以低级鼠标消息的按下时间为准，同时要求 `GetAsyncKeyState` 确认修饰键当前真实按下，并由低级键盘 hook 的按下-释放时间线确认该状态；仅有遗留的 hook 缓存不能触发普通划词。鼠标释放后只做短暂选区稳定等待，随后候选评估阶段会对敏感控件 UI Automation 检查使用短时间盒，并用更短时间盒预读取选区；如果快速读到文本，则校验并缓存到候选对象，点击按钮后直接使用预读文本；如果 UI Automation 没有暴露选区或预读超时，则仍按手势置信度显示按钮，等用户点击后再走显式触发路径（UI Automation + 受控剪贴板兜底）读取文本。若 UI Automation 明确读到文本但文本不满足校验，则不显示按钮。这样既避免被动路径污染剪贴板，也防止 Zotero、PDF、Electron 或自绘控件等 UI Automation 覆盖较弱的应用拖慢悬浮按钮显示。

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

显式选区读取的受控复制不会直接信任剪贴板当前文本。Hermes 会先记住候选产生时的前台窗口，在复制前再次核对窗口句柄；随后暂存原剪贴板、写入 Hermes 私有探针、发送 `Ctrl+C`，并在最多 350ms 内等待剪贴板序列号变化且探针被新数据替换。只有满足这些条件的新文本才会进入翻译，超时、窗口变化或复制无文本都会明确失败，最后统一恢复原剪贴板。该协议避免悬浮按钮误读其他窗口或把此前复制的残留文本当成本次选区。

### Codex 认证切换与本地会话同步

```text
用户在最右侧“应用 → Codex 认证管理”选择目标认证
  ↓
检查 Codex / ChatGPT / CLI / IDE Codex 进程已退出
  ↓
只刷新当前档案的 auth（保留 OAuth token 更新，不反存合并后的共享 config）
  ↓
DPAPI 加密备份当前 config + auth、rollout 首行和权威 SQLite
  ↓
以当前 config.toml 为基础递归合并目标配置，再原子写入合并结果；auth.json 整体替换
  ↓
把 sessions / archived_sessions 的 session_meta.model_provider
和权威 state_5.sqlite 的 threads.model_provider 对齐到目标 Provider
  ↓
读回 rollout 与 SQLite 计数；零错配才提交，否则回滚
```

当电脑尚未建立 ChatGPT 档案时，用户也可选择“浏览器登录”：Hermes 先刷新当前 auth，备份活动 config/auth 与原 ChatGPT Profile，写入不含凭据的事务记录，然后以当前 config 为基础合并 `model_provider = "openai"` 与 `cli_auth_credentials_store = "file"`，清除不属于 ChatGPT 的旧认证路由，并暂时移除活动 auth。随后在同一 `%CODEX_HOME%` 下异步启动官方 `codex login`，由 Codex 打开系统浏览器完成 ChatGPT 认证。登录进程加入启用 `KILL_ON_JOB_CLOSE` 的 Windows Job Object；等待层使用透明命中层拦截底层操作，不压暗页面，只以带轻阴影的主题卡片、环形状态标识和当前步骤文案表达进行中状态，不显示原生横向进度条，并始终保留“取消登录”与 Esc；Windows 关闭界面动画时环形标识保持静态。取消、关闭设置页或退出 Hermes 都会终止进程树并恢复活动文件和旧 Profile。若 Hermes 或系统异常退出，下一次启动会先回收死进程遗留锁，再按事务记录从 DPAPI 备份恢复。进程成功结束后，Hermes 必须确认新 `auth.json` 可识别为 ChatGPT，才加密保存 ChatGPT 档案并执行同一套 rollout/SQLite Provider 同步；登录超时、认证类型错误或后续同步失败同样回滚。CLI 查找顺序为 PATH 中的原生可执行文件、npm 包携带的原生可执行文件、常见编辑器中的官方 Codex 扩展，最后回退到 PATH 中的 `codex.cmd`；登录进程输出不进入 Hermes 日志。进程守卫按可执行路径区分 VS Code、VS Code Insiders、Cursor、Windsurf 扩展、CLI、App Server、Code Mode Host 与 Hermes 登录任务，并显示 PID。

### 自动更新

```text
Velopack 启动钩子识别安装上下文，但不自动应用待处理更新
  ↓
Hermes 启动 8 秒后从 GitHub Releases 检查 stable win feed
  ↓
发现新版本后更新“设置 → 常规”状态并显示托盘提醒
  ↓
用户点击常规页“检查更新”按钮，就地显示检查结果
  ↓
发现新版时按钮变为唯一主操作“更新”；点击后先保存当前设置，再下载并校验 full/delta 包
  ↓
Hermes 自动退出，应用新版本并自动重新打开
```

正式发行版启动后自动检查，之后每六小时检查一次；后台流程只改变可见状态并最多对同一版本提醒一次，绝不自行下载、安装或重启。常规页的软件更新卡右侧使用与“测试连接”一致的次级按钮：默认“检查更新”，检查中禁用并显示环形状态，最新版在左侧显示“当前已是最新版本”，失败时按钮变为“重新检查”。发现版本后，左侧显示目标版本，右侧同一位置切换为唯一蓝色主按钮“更新”；点击该按钮即为最终确认，不再打开弹窗或第二层确认。随后先走统一设置保存流程，保存失败则终止更新；下载期间按钮显示百分比，不使用横向进度条，并尊重 Windows 减少动画设置。下载完成后 Velopack 退出 Hermes、应用更新并自动启动新版本。Debug 和普通 `dotnet publish` 的 manual-test 文件夹不属于 Velopack 安装上下文，界面只显示当前版本，不暴露安装实现限制。更新源固定为公开仓库 `KiRinXC/Hermes` 的稳定 GitHub Releases。

ChatGPT 与 API 档案都以 DPAPI 保存各自的 `config.toml` 模板和完整 `auth.json`。API 配置采用完整档案编辑：用户直接粘贴或修改中转站提供的两份文件内容，Hermes 要求 `config.toml` 根配置显式包含 `model_provider`，除此之外不改写模型、推理等级、地址、Provider 定义或其他字段；`auth.json` 接受任意非空 API 凭据对象，但拒绝 ChatGPT token 档案。切换时遵循 Codex 配置层的递归合并语义：当前用户配置作为基础，目标模板中的同路径标量/数组整体替换、表递归合并、新键加入，目标没有的 MCP、插件、项目、Feature 和其他配置继续保留。`model_provider` 始终以目标模板为准；目标未声明的旧认证路由项（例如 `forced_login_method`、`openai_base_url`）会清除，防止跨登录方式串用。当前档案只刷新 auth，不再把合并后的完整 config 反存成 Profile，避免旧 MCP/插件快照在下一轮切换时回灌。再次编辑时回填档案 config，并将 DPAPI 档案中的 auth 仅在本次编辑会话内解密回显；编辑框禁用撤销历史，并在统一保存、返回应用列表或关闭设置窗口时清空。配置编辑通过 `ISettingsSaveParticipant` 接入设置窗口右下角统一保存：若当前为 API，保存前要求 Codex 客户端退出，然后先备份活动 config/auth 和旧 API Profile，以活动 config 为基础合并新模板、整体更新 auth、对齐会话 Provider，任一步失败都恢复活动文件和旧 Profile；若当前不是 API，则只更新加密档案，留待下次切换。Provider 标识在 rollout、状态统计和 SQLite 同步中始终按大小写严格匹配；SQLite 查询显式使用 `BINARY`，不依赖数据库列的默认排序规则。同步不复制或上传完整会话内容，而是修正同一 `%CODEX_HOME%` 中的本地 Provider 元数据。权威状态库只从配置的 `sqlite_home/state_5.sqlite`、`sqlite/state_5.sqlite`、根目录 `state_5.sqlite` 中择优选择，明确不修改 `codex-dev.db` 一类辅助目录。状态页在同一次 rollout 首行扫描中按常规会话、内部子代理与已归档三类展示会话构成；无法解析的活动记录计入本地总数但不额外显示加法说明，不读取会话正文。

## 模块说明

### Shell

`Shell/SettingsWindow` 是设置入口，负责 API、翻译、触发、UI、隐私、内置应用、开机启动等配置的展示和保存。设置窗口由托盘菜单或翻译卡片中的设置动作打开。

当前设置窗口默认 `800 × 600`，采用无边框 WPF 壳，窗口内部按 Header、Body、Footer 三段式组织。Header 包含紧凑品牌区、可点击录制的快捷键键帽和五个文字页签，固定顺序为“常规、翻译、外观、高级、应用”；隐私设置作为常规行为的一部分并入“常规”，应用中心位于最右侧。页签与应用图标保持更舒展的垂直间距，外层壳体不再使用会被透明窗口裁切成黑框的外边距阴影。Body 使用圆角分组卡片承载常规与隐私、翻译、外观、高级诊断和应用；页签已明确主题且页面只有单一卡片时，不再重复设置同义卡片标题。辅助小字仅用于动态状态、错误恢复、隐私边界和非直观行为，不复述已经清晰的控件名称；OpenAI 翻译通道改用可见开关标签表达，必要状态继续在字段附近动态反馈。Footer 固定放置保存和状态反馈。设置窗口文字层级以 Regular/Medium 为主，不使用 Bold/SemiBold 作为常规 UI 字重。窗口打开时执行淡入与缩放动效；为保证透明无边框窗口四角干净，设置窗不再启用矩形 DWM/Mica 背景，而由本地壳体背景和运行时圆角裁剪承载视觉外观。虽然窗口视觉上保持无边框，但边缘和四角通过 `WM_NCHITTEST` 恢复原生拖拽缩放手感，用户调整后的宽高会自动写入设置并作为下次默认尺寸。

设置页控件已从传统表单升级为更轻量的交互形态：布尔项使用设置页本地 ToggleSwitch，外观规格使用 Slider，主题使用分段选择器，API Key 支持显示/隐藏，右上角键帽按钮支持录制组合键。主题分段选择器的轨道、选中胶囊和描边都使用本地动态主题资源，浅色模式下以灰色轨道、白色选中胶囊和细描边明确当前选项。鼠标点击页签切换设置分区后，会在内容加载完成时清掉 WPF 自动落到第一个开关上的焦点，避免页面首个 ToggleSwitch 出现误导性的蓝色焦点框；键盘导航路径仍保留可见焦点。键帽按钮整体背景和代码生成的单个键帽都使用动态主题资源，浅色模式下会立即切换为浅灰外壳和浅色键帽；进入录制后再次点击按钮、点击窗口其它区域或按 Esc 会取消录制并清掉蓝色焦点框，录制过程不再写入 Footer 状态提示。测试连接作为 API 凭据上下文动作放在 API Key 行右侧，清空历史作为高级诊断上下文动作放在高级页内。翻译页的模型字段保持为手动输入框，避免模型选择控件在紧凑布局中截断；目标语言固定为中文，不再在设置页展示。翻译页还提供可编辑的系统 Prompt，空白时回退到默认英文到简体中文翻译提示词。外观页提供“气球样式”下拉项、“图标大小”五点横向选择器和“浮窗字号”五点横向选择器：图标大小从左到右对应超小到超大，两端使用透明背景的真实悬浮按钮图标预览尺度，并随当前浅色/深色主题切换 light/dark 图标；浮窗字号从左到右对应五档真实字号，两端用固定画布的矢量 `A` 图标直接显示最小和最大字号，避免字体基线影响端点对齐；两个五点控件共用对齐后的轨道几何，不再显示额外档位文字。外观页不再提供翻译卡片默认宽高控件，卡片尺寸由用户直接拉伸卡片后自动记忆。设置窗口内置本地 TextBox、PasswordBox、ComboBox、ComboBoxItem、FooterButton、Tab、ToggleSwitch、Slider 和滚动条样式，避免设置页回落到原生控件质感。`SettingsWindowOptions` 用于分离设置项显示文案和持久化值，避免中文高级文案写入配置文件。`SettingsWindowThemePalettes` 负责设置窗口自身的浅色/深色调色板，外观页切换主题时会替换本地 brush 资源；设置窗口样式使用 DynamicResource 引用这些 brush，因此浅色/深色/跟随系统会立即作用于设置窗口自身。浅色主题下开关关闭轨道、滑块未选轨道、滚动条滑块和快捷键键帽使用可读灰阶，避免黑色控件在浅色面板中过重或不可见。

浮窗字号当前提供 `12 / 14 / 16 / 18 / 20` 五档，默认值为 `16`；`TranslationPopupWindow` 会将设置值限制在 12 到 20 之间，并同时应用到译文正文和原文预览。

### Apps

`Apps/AppRegistry` 是 Hermes 内置小应用的注册入口，`Apps/AppsCenterView` 在设置页中提供列表与详情两级导航。每个应用实现 `IHermesApp`，拥有稳定 ID、矢量图标、名称、说明和可复用视图；应用自己的文件、服务和 UI 放在 `Apps/<AppName>/` 内，避免未来多个工具继续堆进 Shell。

`Apps/CodexAuthSwitchSync` 是首个内置应用，ID 固定为 `codex-auth-switch-sync`。它负责：识别 ChatGPT/API auth；通过官方 `codex login` 启动系统浏览器初始化或刷新 ChatGPT 认证；以当前 Windows 用户 DPAPI 加密两套配置模板与完整凭据；导入并校验任意中转站提供的 API config/auth；按 Codex 的递归配置层语义合并目标模板并保留当前共享配置；切换、浏览器登录以及活动 API 配置应用前拦截仍运行的 Codex 客户端；以原子文件写和 SQLite 事务同步本地会话；失败时恢复 config、auth、Profile、rollout 首行和数据库；解释 rollout 总数与用户可见历史数之间的常规会话、内部子代理、已归档构成。浏览器登录额外由可取消异步任务、Windows Job Object 和持久事务记录托管，保证用户取消、应用退出与异常中断都能清理进程并恢复。认证卡片的操作区按活动认证和档案存在状态动态裁剪，每张卡最多展示两个有效动作，隐藏“切换到当前认证”等无效入口，并动态指定唯一主操作，避免窄卡片自动换行。切换、登录和同步统一通过 `IHermesConfirmationHost` 使用设置窗口内确认层，不创建系统 MessageBox。备份仅保留最近五次。

### Tray

`TrayService` 维护系统托盘图标、菜单和启动通知。左键单击托盘图标会直接打开设置窗口；右键菜单保留暂停/恢复、翻译剪贴板、设置和退出，不再显示历史入口；启动时右下角通知支持点击打开设置窗口。通知点击后的设置窗会执行一次显式抬前流程：必要时恢复窗口、临时置顶、激活并聚焦，再恢复普通层级，避免被其他应用窗口盖住。托盘是用户无需打开主窗口即可控制应用的主要入口。

### Input

`HotkeyService` 负责注册全局快捷键。`KeyboardHookService` 和 `MouseHookService` 负责低级输入监听，用于关闭被动 UI、捕捉 Esc、识别鼠标选择手势。`KeyboardHookService` 会缓存 Ctrl/Alt 的按下与释放状态及低级 hook 消息时间，供鼠标起手判定读取；`MouseHookService` 在鼠标左键按下时同时检查物理键状态与 hook 时间线，二者都确认修饰键已按下才跟踪手势，避免 hook 遗留状态让普通划词误触。普通拖选不会进入后续移动/释放阶段的补判。单独按下或松开 Ctrl/Alt 不再作为关闭被动 UI 的用户活动。启动阶段会等通知窗口显示后再注册触发器，避免低级 hook 在 UI 线程初始化繁忙时影响鼠标流畅度。Hook 内不做重计算，只转发事件给协调层。

### Selection

选区模块负责“从哪里拿到文本”和“文本是否值得翻译”。

- `UiAutomationSelectionProvider` 通过 Windows UI Automation 读取当前选区，读取工作运行在后台线程，避免点击悬浮翻译按钮时卡住 WPF UI 线程。
- `ClipboardSelectionProvider` 在显式触发时使用受控复制或读取剪贴板文本；受控复制通过私有探针和 `GetClipboardSequenceNumber` 验证本次复制确实写入新内容，最多等待 350ms，并始终尝试恢复原剪贴板。剪贴板操作运行在专用 STA 线程，不通过主 Dispatcher 执行 `Ctrl+C` 和剪贴板读写。
- `ForegroundWindowService` 判断前台窗口、排除应用和敏感控件，并校验候选窗口在显式复制前没有变化；被动划词按钮路径中的敏感控件检查使用短时间盒，避免慢 UI Automation 控件拖住按钮显示。
- `SelectionTextValidator` 根据语言、长度和设置校验文本。
- `SelectionOrchestrator` 决定显式触发、被动鼠标和剪贴板翻译时的读取策略。

### Overlay

悬浮层模块负责按钮、翻译卡片和位置计算。

- `FloatingButtonWindow` 显示在按住 Ctrl/Alt 后开始拖选的轻量触发按钮。
- `FloatingButtonWindow` 的浅色/深色图标内容同步自 `src/Hermes.Windows/Resources/Icons/FloatingButtonLight.svg` 和 `FloatingButtonDark.svg`，不再额外叠加实底边框；仓库根目录不再保留同名副本图标。按钮尺寸由 `UiSettings.FloatingButtonSize` 控制，五档分别映射命中区和图标层大小：超小 32/18、小 38/22、中 44/25、大 52/30、超大 60/36，点击热区会随图标尺寸一起缩放。
- `FloatingButtonWindow` 支持手动高对比图标样式：`DarkBorderLightFill`（黑框白底图标）和 `LightBorderDarkFill`（白框黑底图标），并通过设置页外观项持久化，避免深色网页与浅色主题叠加时按钮不可辨。
- `TranslationPopupWindow` 显示加载、流式译文、长耗时、成功、错误、复制、重试、固定和关闭状态；加载标题会显示实际运行通道（`Tencent` 或 OpenAI 模型名），并在流式 delta 与长耗时状态中继续保留该通道文案。成功完成且未固定时，鼠标点击浮窗外部会关闭卡片。翻译卡片同样保持无边框外观，并通过 `WM_NCHITTEST` 支持边缘和四角原生缩放；用户调整后的宽高会自动保存为下一张卡片默认尺寸。卡片正文滚动条使用与设置窗口一致的细轨道/圆角滑块样式，并通过 `Brush.ScrollThumb` / `Brush.ScrollThumbHover` 随浅色、深色主题切换颜色。
- `TranslationPopupWindow` 的正文渲染统一由 `PopupMarkdownRenderer` 处理，翻译和解释（含流式 delta）共用同一条 Markdown 渲染链路，支持标题、列表、引用、代码块、行内代码、强调和链接文本，并对未闭合标记按普通文本降级显示，避免流式阶段卡死或错乱。
- `TranslationPopupWindow` 顶部使用应用图标作为品牌标识；用户可以从卡片背景、正文和原文区域等非交互表面拖动卡片，按钮、开关、滚动条等交互控件不会触发拖拽。
- `OverlayPositionService` 负责多屏幕边界内的位置约束。
- `OverlayManager` 对外提供显示与关闭悬浮 UI 的统一入口，并区分普通被动 UI 关闭与已完成未固定浮窗的外部点击关闭。显示新悬浮按钮前会清理当前进程中残留的 `FloatingButtonWindow`，`TranslationCoordinator` 也会取消旧的被动划词候选评估，避免快速划词或旧异步评估完成后出现重复按钮。它会跟踪多张浮窗并把重试/关闭事件按浮窗实例回传，避免固定旧卡片影响当前请求。

### Translation

翻译模块封装多提供方翻译能力。

- `TranslationPromptBuilder` 构造翻译/解释指令，并提供默认翻译 Prompt 与解释个性化偏好。
- `TransmartTranslationService` 调用 Tencent Transmart `.../imt` 接口，作为默认翻译提供方。
- `OpenAiTranslationService` 读取设置和密钥，发送 Responses API 请求。普通路径解析 `output_text` 或 `output` 内容；OpenAI 路径使用 `stream = true` 读取 SSE 事件，按 `response.output_text.delta` 逐段输出，并在完成时汇总最终译文。
- `ProviderRoutingTranslationService` 根据设置路由到 Transmart 或 OpenAI；解释模式固定走 OpenAI，翻译模式由 `UseOpenAiForTranslation` 开关决定。
- `TranslationStreamEvent` 描述流式翻译的增量、完成和失败事件。
- `TranslationCoordinator` 串联选区、流式翻译、历史和 UI，是翻译工作流协调层。它会在手势条件不满足时直接跳过被动鼠标候选流程，并对流式 delta 做轻量批处理后再刷新 UI；用户关闭翻译卡片时会取消对应请求，成功完成后再保存最终译文。固定卡片共存时，每张卡片的重试与状态更新按实例隔离。

错误处理覆盖缺少 API Key、鉴权失败、余额或额度不足、限流、无效请求、网络错误、超时、取消和空响应。

### Settings

设置模块负责本地配置和密钥存储。

- `SettingsService` 读写 `%LOCALAPPDATA%\KiRinXC\Hermes\settings.json`。
- `DpapiSecretStorageService` 使用 Windows DPAPI 加密保存 API Key 到 `secrets.dat`。
- `StartupRegistrationService` 管理开机启动注册。
- `AppSettings` 定义 API、翻译、触发、UI、隐私和启动设置。默认 Provider 为 `Transmart`（`https://transmart.qq.com/api`，`normal`），翻译设置包含可编辑系统 Prompt 和“解释个性化偏好”。

### History

历史模块已具备本地存储服务，当前默认关闭保存历史。托盘菜单不展示历史入口；清空历史放在设置窗口高级页中，并会同步清空该页展示的触发诊断队列。

### Infrastructure

基础设施模块包括日志、路径、Win32 方法、应用身份、单实例守卫、日志脱敏、用户数据迁移和 `AutomaticUpdateService`。Velopack 程序目录保持 `%LOCALAPPDATA%\Hermes\`，用户数据独立保存到 `%LOCALAPPDATA%\KiRinXC\Hermes\`；首实例启动时只迁移 `settings.json`、`secrets.dat`、`history.json`、`app.log` 和 `apps\`，不会复制或删除 `current\`、`packages\`、`Update.exe` 等安装文件。内置应用的数据使用 `%LOCALAPPDATA%\KiRinXC\Hermes\apps\<app-id>\` 子目录隔离。自动更新服务维护检查中、最新、发现版本、下载中、待重启、不受支持和失败等状态；组合根只定时检查与提醒，设置窗口订阅状态，并只在用户点击软件更新卡右侧的“更新”后调用下载、应用和重启。

### UI/Themes

主题模块维护浅色、深色、设计 token 和组件样式。运行时根据设置应用 `System`、`Light` 或 `Dark` 主题。深色主题主背景已调整为 `#0A0A0C`。设置窗口拥有独立浅色/深色调色板，ToggleSwitch 开启态使用蓝紫渐变，浅色模式的关闭态轨道和 Slider 未选轨道使用 Apple 风格中性灰，确保控件在白天模式下仍清晰可读。整体 UI 字重控制在 Regular/Medium，标题、按钮、页签和状态文字用 Medium 建立层级，避免大面积加粗造成粗糙感。

### Tests

`tests/Hermes.Tests` 是轻量控制台测试套件，覆盖设置、脱敏、选区校验、选择候选、UI Automation 预读失败/超时手势兜底、受控剪贴板新鲜度与目标窗口约束、被动路径敏感控件检查时间盒、快捷键解析、鼠标 Ctrl/Alt 起手物理键与 hook 双重门控、启动触发器延迟注册、启动通知点击设置、设置窗口选项文案和值映射、设置/弹窗边缘缩放与尺寸持久化约束、外观页悬浮按钮五点尺寸选择器对齐与主题预览、外观页浮窗字号五点选择器、设置/弹窗滚动条主题样式、通知点击后的设置窗抬前逻辑、历史/诊断清理、OpenAI 与 Transmart 响应解析、加载态通道显示、悬浮按钮清晰度和去重约束、翻译卡片拖拽/外部点击关闭入口、多卡片事件隔离约束、弹窗 Markdown 渲染回归、UI 字重约束，以及 Apps 注册表、Codex TOML/auth 识别、配置递归合并、浏览器登录配置与 CLI 定位、登录取消/Job Object/事务恢复入口、进程来源分类、认证路由清理、rollout + 权威 SQLite 同步、Velopack 启动顺序与发行 feed 等逻辑。WPF 可视交互、真实浏览器 OAuth、强制终止恢复和跨版本自动更新仍需要真实发行包试用补充验证。

## 打包策略

项目环境由仓库自身固定：

- `global.json` 指定 .NET SDK `10.0.300`。
- `NuGet.Config` 使用 `.nuget\offline` 作为优先包源，并保留 `nuget.org` 作为在线包源。
- `scripts\Use-HermesEnv.ps1` 统一设置 `DOTNET_CLI_HOME`、NuGet 缓存、scratch/cache 目录和可选代理，并确保 `.nuget\offline` 本地源目录存在；SDK 根目录可由 `HERMES_DOTNET_ROOT` 指定，未指定时依次探测 `C:\Code\Env\dotnet`、`D:\Code\Env\dotnet` 和 `PATH`。构建中间目录默认落在系统临时目录，并按 `MSBuildProjectName` 隔离，避免主项目与测试项目互相编译生成文件。
- `scripts\Restore-Hermes.ps1`、`scripts\Test-Hermes.ps1`、`scripts\Publish-Hermes.ps1` 和 `scripts\Package-HermesRelease.ps1` 是标准入口。
- 普通构建只依赖 .NET SDK；正式安装器额外使用 w64devkit 的 `g++` 与 `windres` 构建无托管运行时依赖的安装引导层，脚本依次从 `PATH`、`C:\Code\Env\C\w64devkit\bin` 和 `D:\Code\Env\C\w64devkit\bin` 查找。

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

开发 agent 每次完成代码、文档或配置修改后，交付前都必须按 `AGENTS.md` 执行一次发行/打包。默认发行方式是运行 `scripts\Publish-Hermes.ps1`，直接覆盖上述 `manual-test\win-x64-self-contained\` 目录，并且必须保持 `win-x64 self-contained`，随包携带 .NET runtime，不能要求客户额外下载安装 .NET。对外分发 zip 只作为覆盖 self-contained 目录后的附加步骤。

推荐命令：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Publish-Hermes.ps1
```

对外 GitHub Release 从 v0.4.0 起采用 Velopack 1.2.0 生成核心 Setup、full/delta nupkg 与 win feed。仓库通过 `.config\dotnet-tools.json` 固定 `vpk` 版本；脚本仍先覆盖固定 self-contained manual-test 目录，再把 Velopack 的完整中间资产写入持久 feed 目录以便后续生成 delta。公开的 `Hermes-win-Setup.exe` 是一个原生轻量引导层：先把旧 `%LOCALAPPDATA%\Hermes\` 中明确列出的用户数据复制到独立目录，再解出并启动内嵌的 Velopack Setup；这可保护同版本覆盖安装时尚未来得及由新程序迁移的数据，正常安装交互保持不变。对外版本目录只包含该安装器、full/delta nupkg、`releases.win.json` 与 SHA256 清单。GitHub Release 以 Setup 作为唯一面向用户的安装入口，不发布 portable ZIP；nupkg 和 release feed 仅供应用内更新使用：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Package-HermesRelease.ps1 -Version 0.4.0
```

输出目录：

```text
artifacts\release\v0.4.0\
├─ Hermes-win-Setup.exe
├─ Hermes-0.4.0-full.nupkg
├─ releases.win.json
└─ checksums.txt
```

对应的 GitHub Release 说明草稿保存在 `docs\release-notes\`。README 面向最终用户介绍下载、运行、隐私和使用边界，内部实现细节继续放在 `Design.md`。

如果本机缺少自包含发布所需的 .NET runtime packs，且 NuGet 无法访问，可以临时发布 framework-dependent 包用于本机试用：

```powershell
C:\Code\Env\dotnet\dotnet.exe publish src\Hermes.Windows\Hermes.Windows.csproj -c Release -r win-x64 --no-self-contained -p:NuGetAudit=false -p:RestoreIgnoreFailedSources=true --artifacts-path artifacts\dotnet -o artifacts\publish\Hermes.Windows\manual-test\win-x64-framework-dependent
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
- manual-test 文件夹继续便于直接检查；GitHub 正式发行由 Velopack portable/Setup 承担安装与更新。

`artifacts/` 是本地构建产物目录，不进入 Git。

GitHub 发布前的仓库边界：

- `docs/`、`README.md`、`UI.md`、`Design.md`、`scripts/`、`src/` 和 `tests/` 应进入 Git。
- `.codex/`、`openspec/`、`.dotnet-home/`、`.nuget/`、`artifacts/`、`bin/`、`obj/`、测试结果、日志、密钥、签名证书和压缩包由 `.gitignore` 排除。
- README 头图 `docs/assets/Info.png`、文生图提示词和 release notes 属于对外发布资产，需要保留。

## 用户数据和隐私

Hermes 的程序文件和用户数据严格分离：

```text
程序：%LOCALAPPDATA%\Hermes\
数据：%LOCALAPPDATA%\KiRinXC\Hermes\
```

Windows 卸载由 Velopack 删除完整程序目录，默认不触碰独立数据目录。常规页“存储位置”展示两个实际路径并提供打开目录操作；“删除数据”使用 Hermes 主题确认层，确认后删除设置、密钥、历史和内置应用档案并立即退出，用户随后可继续从 Windows 设置卸载程序。正式安装器会在替换旧程序前先复制旧版数据；首实例启动再完成兼容迁移、写入迁移标记并移除旧副本，避免覆盖安装或卸载时误删。

主要文件：

- `settings.json`：普通设置。
- `secrets.dat`：DPAPI 加密后的 API Key。
- `app.log`：本地日志。
- `history.json`：翻译历史，默认不保存。
- `apps\codex-auth-switch-sync\profiles\*.profile`：当前 Windows 用户 DPAPI 加密的 Codex 完整认证档案。
- `apps\codex-auth-switch-sync\backups\`：切换/同步前的加密恢复材料，最多保留五份。
- `apps\codex-auth-switch-sync\browser-login.transaction.json`：仅在浏览器登录事务进行中存在的不含凭据恢复指针，成功、取消或恢复后删除。

设计原则：

- 不自动上传未被用户明确触发的文本。
- 被动鼠标路径不执行剪贴板复制。
- API Key 不写入普通设置文件。
- Codex auth、OAuth token、API Key 和备份清单不写入普通设置或日志；DPAPI 档案只能由创建它的 Windows 用户解密，每台电脑需单独初始化。
- 日志默认不记录完整原文和译文。
- 支持排除应用和敏感应用。

## 已知限制

- UI Automation 在浏览器、PDF、Electron、自绘编辑器中的行为不完全一致。
- 自动悬浮按钮在 UI Automation 预读失败时会使用手势兜底显示，但仍无法保证所有应用都能出现按钮或复制到选区文本。
- 当前没有完整历史列表 UI，仅保留高级页清空历史动作。
- 设置窗口的 Mica 背景依赖 Windows 11 DWM 能力；在不支持的系统或透明窗口组合受限时会退回内置深色背景。
- 真实多显示器、高 DPI、不同应用兼容性需要持续人工试用。
- manual-test 自包含目录不受自动更新管理；只有从 Velopack portable 或 Setup 启动的正式发行版能够完成应用内下载、替换与自动重启。
- Codex 会话同步只改变本机 rollout 与 `state_5.sqlite` 中的 Provider 元数据，不等于 ChatGPT 网页云同步；跨认证会话中的加密内容可能仍无法继续。
- 为避免 Codex 同时写入造成竞争，浏览器登录、切换与同步要求先退出 Codex 桌面端、CLI、ChatGPT 和 IDE Codex 扩展。
- ChatGPT 浏览器登录依赖本机可用的 Codex CLI 或官方 Codex 编辑器扩展；OAuth 页面和服务端可用性无法由离线自动测试覆盖。

## 设计变更记录

| 日期 | 变更 | 影响范围 |
| --- | --- | --- |
| 2026-08-15 | 保留 v0.4.0 一键安装，不再引入自定义安装目录：将用户数据从 Velopack 程序目录迁移到 `%LOCALAPPDATA%\KiRinXC\Hermes\`，正常卸载只删除程序；常规页新增程序/数据路径、打开目录和经二次确认的“删除数据并退出”；公开 Setup 增加原生数据保护引导层，在同版本覆盖旧目录前复制设置、密钥、历史及 Codex 档案，首实例再完成迁移清理。 | Infrastructure / Settings / Shell / Uninstall / Migration / Setup / UI / Tests / Documentation |
| 2026-08-14 | 将 v0.4.0 对外发行入口统一为 `Hermes-win-Setup.exe`：发行目录和 GitHub Release 不再包含 portable ZIP，只保留安装程序、应用内更新所需的 full/delta nupkg、`releases.win.json` 与校验文件。 | Release / Packaging / Documentation / Tests |
| 2026-08-14 | 将软件更新交互收敛为卡片右侧单按钮：按钮沿用“测试连接”样式执行检查，最新版与错误在卡片内就地反馈；发现新版后同一按钮切换为蓝色“更新”，点击即开始保存、下载、退出安装与自动重启，并移除更新弹窗。 | Release / Shell / UI / Accessibility / Tests / 文档维护 |
| 2026-08-14 | 将常规页“软件更新”改为可点击入口：打开不压暗背景的 Hermes 主题弹窗后检查并展示版本，“安装”作为唯一最终确认；下载完成后由 Velopack 自动退出、安装并重新打开 Hermes，同时移除界面中的运行方式限制文案。 | Release / Infrastructure / Shell / UI / Accessibility / Tests / 文档维护 |
| 2026-08-14 | 优化 Codex 认证切换、档案保存和浏览器登录等待态：移除默认 WPF 横向进度条和整页黑色遮罩，改用透明命中层、带轻阴影的主题卡片、环形状态标识、当前步骤文案及 180ms 进入动效；尊重 Windows 界面动画开关，并继续保留浏览器登录的按钮/Esc 取消入口。 | Apps / Shell / UI / Accessibility / Tests / 文档维护 |
| 2026-08-14 | 将软件更新调整为微信式“自动检查、手动确认”：后台只更新状态和托盘提醒，不下载、不自动重启；常规页新增紧凑更新卡片，用户确认后先保存设置，再下载、安装和重启；待处理更新也不在启动时静默套用。 | Release / Infrastructure / Shell / UI / Accessibility / Tests / 文档维护 |
| 2026-08-14 | 准备 v0.4.0：接入 Velopack GitHub Releases 更新能力；ChatGPT 浏览器登录改为可取消异步任务，以 Windows Job Object 清理进程树，并以持久事务记录恢复取消、失败或崩溃前的 config/auth/Profile；Codex 进程提示新增 IDE/CLI/Hermes 来源和 PID，等待层沿用 Hermes 遮罩与按钮体系。 | Release / Infrastructure / Apps / Process / Recovery / UI / Tests / 文档维护 |
| 2026-08-14 | 准备并发布 v0.3.0：补充程序集版本和 Release Notes，将打包脚本、README 与发布示例统一到 0.3.0；发布前删除已废弃的固定 API 配置生成路径及其私网开发地址，并将 `Microsoft.Data.Sqlite` 升级到 10.0.11 以消除 SQLite 原生依赖的高危公告。 | Release / Security / Dependencies / Apps / Tests / 文档维护 |
| 2026-08-14 | 将隐私设置并入常规页，移除独立“隐私”页签，在保留历史记录开关与隐私边界提示的同时把设置一级导航由六项缩减为五项。 | Shell / UI / Privacy / Tests / 文档维护 |
| 2026-08-14 | 全面精简设置、应用中心与 Codex 页面微文案：删除复述控件含义的 6 处说明，缩短隐私、诊断与同步边界提示，将 OpenAI 翻译开关改为可见标签，合并“全部 Rollout”指标，并移除常规、外观、隐私及应用中心与页签重复的标题；动态状态、错误、认证档案和会话分类解释继续保留。 | Shell / Apps / UI / Accessibility / Tests / 文档维护 |
| 2026-08-14 | 移除已有 ChatGPT 档案时冗余的“更新当前登录”入口；“保存当前登录”仅用于首次捕获现有 ChatGPT 认证，后续凭据更新由切换离开 ChatGPT 时的自动刷新负责，当前卡片仅保留真正重新认证的“重新登录”。 | Apps / UI / Tests / 文档维护 |
| 2026-08-14 | 修复活动 API 档案编辑只更新加密 Profile、未更新 `.codex` 导致 `base_url` 仍使用旧值的问题：统一保存现在会事务式合并并应用 config、整体更新 auth、同步会话且失败回滚；API 编辑器回显解密 auth、移除重复标题/帮助/局部保存按钮，会话文案改为“常规会话”并移除加法行；系统 MessageBox 替换为 Hermes 内嵌确认层。 | Apps / Shell / UI / Config / Privacy / Tests / 文档维护 |
| 2026-08-14 | Codex 认证卡片操作区改为状态化布局：隐藏当前认证的无效切换按钮，根据档案状态切换“浏览器登录/重新登录”“保存/更新当前登录”和 API 配置文案，每张卡最多显示两个操作并动态确定唯一主按钮，消除 ChatGPT 三按钮在窄卡片中的孤立换行。 | Apps / UI / Tests / 文档维护 |
| 2026-08-14 | 设置页将“应用”移动到“高级”右侧并固定为最右页签；Codex 认证管理新增官方浏览器登录，可在仅有 API 认证的新电脑上自动备份、调用 `codex login`、校验并加密保存 ChatGPT 档案，再切换认证并同步本地会话，失败时恢复原状态。 | Apps / Shell / UI / Privacy / Tests / 文档维护 |
| 2026-08-14 | Codex 认证切换由整份 `config.toml` 覆盖改为 TOML 语义递归合并：目标同路径值覆盖、新键加入、目标缺少的 MCP/插件/项目等共享树保留；`auth.json` 仍整体替换，旧认证路由项按目标清理，当前档案仅刷新 auth，避免合并后的旧共享配置回灌。 | Apps / Config / Privacy / Tests / 文档维护 |
| 2026-08-14 | API 档案配置升级为完整 `config.toml + auth.json` 粘贴导入：支持任意中转站和区分大小写的自定义 Provider，保留配置原文；auth 默认不回显、可留空沿用、编辑时禁用撤销历史并在退出编辑器时清空。 | Apps / UI / Privacy / Tests / 文档维护 |
| 2026-08-14 | 明确 Codex Provider 标识严格区分大小写；SQLite Provider 分组与更新条件显式使用 `BINARY`，并以 `NOCASE` 测试表验证 `OpenAI` 与 `openai` 不会被合并。 | Apps / Tests / 文档维护 |
| 2026-08-14 | Codex 认证管理新增会话构成统计，在不读取正文的前提下将 rollout 分为普通活动、内部子代理、已归档和无法分类，并直接解释总数与 Codex 历史列表数量的差异。 | Apps / UI / Tests / 文档维护 |
| 2026-08-14 | 新增可扩展 `Apps/` 目录与应用中心，内置 `codex-auth-switch-sync`：DPAPI 加密管理 ChatGPT/API 完整档案，切换认证时原子更新 `.codex` 并同步 rollout 与权威 `state_5.sqlite`，提供进程守卫、加密备份、失败回滚和读回校验；同步补充一致 UI、测试、用户说明，修复多项目共享中间目录造成的重复生成文件，并在发布前检查运行实例以避免生成混合版本目录。 | Apps / Shell / Infrastructure / Privacy / Tests / Build / 文档维护 |
| 2026-07-13 | 新增 `docs\release-notes\v0.2.4.md`，将 README、打包脚本默认版本和打包策略示例更新为 v0.2.4；本次发布聚焦划词文本读取、旧剪贴板隔离和普通划词误触三项稳定性修复。 | Input / Selection / Tests / 文档维护 / 打包发布 |
| 2026-07-13 | 修复划词触发与读取协议：鼠标起手同时校验 Ctrl/Alt 物理按下状态和 hook 时间线，普通划词不再因遗留键状态显示按钮；候选点击绑定原前台窗口；受控剪贴板以私有探针和序列号验证本次 `Ctrl+C` 的新内容，等待上限提升到 350ms，失败时不再翻译旧剪贴板文本。同步增加回归测试，并让构建脚本自动探测本机 .NET SDK。 | Input / Selection / Translation / Tests / 开发环境 |
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
| 2026-05-29 | 默认翻译提供方切换为 Tencent Transmart（`https://transmart.qq.com/api` / `normal`），OpenAI 改为可选；新增 `ProviderRoutingTranslationService` 与 `TransmartTranslationService`，并在设置页放宽 Transmart 的 API Key 校验。 | Translation / Settings |
| 2026-05-29 | 新增 Alt 划词术语解释模式：`Ctrl` 划词翻译，`Alt` 划词解释；翻译设置新增“解释个性化偏好”，解释 Prompt 按该偏好生成并与普通翻译共享悬浮按钮和结果卡片。 | Input / Selection / Translation / Shell |
| 2026-05-29 | 修复多固定卡片串扰：Overlay 改为多浮窗跟踪，重试/关闭事件按浮窗实例回传；Coordinator 对每张卡片维护请求上下文并直接更新对应浮窗，避免旧卡片影响当前请求；同时移除卡片宽度 420→360 的隐藏映射并为开机启动注册增加异常保护。 | Overlay / Translation / Settings |
| 2026-05-29 | 更新本地构建脚本：环境默认 `C:\Code\Env\dotnet`，将中间编译目录迁移到系统临时目录并按运行批次输出到 `artifacts\dotnet-verify\bin\<runId>\`，修复当前环境下 `obj` 删除受限导致的构建失败。 | 开发环境 / 打包发布 |
| 2026-05-29 | 翻译设置改为双通道：`AI翻译` 使用 Transmart，`AI解释` 使用 OpenAI，并新增 `UseOpenAiForTranslation` 开关控制翻译通道；解释模式固定走 OpenAI，翻译模式按开关在 OpenAI/Transmart 之间路由；保留旧版单 Provider 配置的自动迁移兼容。 | Shell / Translation / Settings |
| 2026-05-30 | 全仓执行中文文案乱码审计并修复弹窗遗留乱码：`CopyLabel` 复位文案改回“复制译文”，统一翻译卡片加载/长耗时中文提示文案；新增覆盖弹窗中文可读性的回归测试，防止再次引入乱码。 | Overlay / Tests / 文档维护 |
| 2026-05-30 | 设置窗口标题栏改为仅保留关闭按钮（移除最小化）；`AI解释` 区域“测试连接”改用专用按钮样式，提升到与 API Key 输入框同高，增加与输入框的水平间距，并收窄按钮宽度以减少拥挤感。 | Shell / UI-Themes / Tests |
| 2026-05-30 | 根据验收反馈将 `AI解释` 区域“测试连接”按钮改为列内右对齐，保持窄宽度同时让按钮边界更贴合右侧布局。 | Shell / UI-Themes / Tests |
| 2026-05-30 | 继续优化浅色主题按钮可见性：`测试连接`、`清空记录`、`刷新列表` 等按钮改为使用 keycap 背景与边框资源，避免浅色下“仅文字无按钮底”；`确定保存` 主按钮文字固定白色，确保浅色主题下对比度。 | Shell / UI-Themes / Tests |
| 2026-05-30 | 翻译卡片正文改为统一 Markdown 渲染链路：新增 `PopupMarkdownRenderer`，让翻译与解释（含流式输出）共享同一渲染器，补齐标题/列表/引用/代码块/行内样式显示，并修复未闭合 Markdown 标记导致的流式渲染卡死。 | Overlay / Translation / Tests |
| 2026-05-30 | 悬浮按钮新增手动高对比气球样式：在外观页增加“气球样式”可选项（黑框白底 / 白框黑底），并将选项写入 `UiSettings.FloatingButtonStyle`，由 Overlay 在显示按钮时应用。 | Shell / Overlay / Settings / Tests |
| 2026-05-30 | 根据验收反馈回退悬浮按钮实底边框方案：按钮恢复透明圆形命中区与 25×25 图标尺寸，图标内容改为同步 `src/Hermes.Windows/Resources/Icons/FloatingButtonLight.svg` / `FloatingButtonDark.svg` 的矢量形状，并继续沿用外观页“气球样式”切换。 | Overlay / Shell / Tests |
| 2026-05-30 | 根据新验收要求，删除仓库根目录 `think-light.svg` / `think-dark.svg`，并将悬浮按钮图标源固定为 `src/Hermes.Windows/Resources/Icons/` 下的图标文件后再写死到 XAML。 | Overlay / Resources / Tests |
| 2026-05-30 | 修复 Ctrl/Alt 划词悬浮按钮过度依赖 UI Automation 预读的问题：预读失败时不再 suppress 按钮，而是按手势置信度显示按钮，点击后走显式读取和受控剪贴板兜底，提升 Zotero、PDF、Electron 和自绘控件兼容性。 | Selection / Overlay / Tests |
| 2026-05-30 | 将“每次修改后必须发行/打包一次”的交付要求写入 `AGENTS.md`，并同步到打包策略，确保用户每轮修改后都能拿到可试用产物或明确的发行阻塞说明。 | 文档维护 / 打包发布 |
| 2026-05-30 | 明确发行默认直接覆盖 `manual-test\win-x64-self-contained\`，且必须发布 self-contained 产物随包携带 .NET runtime，release zip 仅作为附加分发步骤。 | 文档维护 / 打包发布 |
| 2026-05-30 | 优化启动和被动划词体验：缩短鼠标释放后的固定等待，为 UI Automation 预读增加时间盒，避免 Zotero 等应用拖慢悬浮按钮显示；启动通知显示后再延迟注册全局触发器，降低启动时鼠标卡顿；点击启动通知可打开设置窗口。 | App / Selection / Translation / Tray / Tests |
| 2026-05-30 | 继续收紧 Zotero 被动划词显示延迟：将按钮显示前的敏感控件 UI Automation 检查改为短时间盒，并把被动预读预算缩短到 35ms；预读或敏感检查超时不再阻塞按钮显示，点击按钮后仍走显式读取和兜底。 | Selection / Tests |
| 2026-05-30 | 翻译卡片和设置窗口在无边框外观下恢复边缘/四角原生缩放命中测试，并自动保存用户调整后的尺寸；外观页新增悬浮按钮五档图标大小设置，按钮点击热区随图标大小同步缩放，同时移除弹窗默认尺寸控制。 | Overlay / Shell / Settings / Tests |
| 2026-05-30 | 将外观页悬浮按钮大小设置从下拉框改为五点横向选择器，左/右两端以白底小/大图标提示尺度且不显示档位文字；翻译卡片正文滚动条改为与设置窗口一致的细圆角样式，并通过主题资源在浅色/深色下切换颜色。 | Shell / Overlay / UI-Themes / Tests |
| 2026-05-30 | 继续打磨外观与提醒交互：图标大小选择器两端预览改为真实悬浮按钮图标；浮窗字号从连续滑块改为五点横向选择器，并让正文和原文预览都实际应用该字号；点击右下角启动提醒后，设置窗口会执行显式抬前流程，避免被其他应用遮挡。 | Shell / Overlay / App / Tray / Tests |
| 2026-05-31 | 修复外观页五点控件对齐：图标大小与浮窗字号轨道统一列宽和中段边距；图标预览去掉圆形实底/边框并随浅色/深色主题切换 light/dark 真实悬浮按钮图标；翻译加载态在流式输出和长耗时状态中持续显示 `Tencent` 或 OpenAI 模型名；被动划词候选增加取消与遗留按钮清理，避免同一次划词后出现重复悬浮按钮。 | Shell / Overlay / Translation / Tests / Docs |
| 2026-05-31 | 调整浮窗字号档位为 `12 / 14 / 16 / 18 / 20`，默认字号改为 `16`，并将弹窗字号上限同步放宽到 `20`。 | Settings / Shell / Overlay / Tests |
| 2026-05-31 | 修正外观页图标大小和浮窗字号端点预览的居中方式：两行左右端点预览都固定在 34px 槽位中心，图标 Viewbox 与 `A` 字样不再分别左/右贴边。 | Shell / Tests |
| 2026-06-24 | 修正被动划词触发门槛：只有在鼠标按下前已经按住 Ctrl 或 Alt 的拖选才会进入候选评估；鼠标和键盘的松开顺序不再要求同时，避免拖选中途才按下修饰键也触发按钮。 | Input / Selection / Translation / Tests / Docs |
| 2026-06-24 | 强化键盘修饰键采样：`KeyboardHookService` 在低级 hook 中缓存 Ctrl/Alt 的按下与释放状态，鼠标手势不再只依赖 `GetAsyncKeyState` 的瞬时采样，降低起手时机丢失导致的“完全无法触发”。 | Input / Tests / Docs |
| 2026-06-24 | 修复 Ctrl/Alt 划词仍依赖同步松开的问题：键盘 hook 记录低级消息时间，鼠标手势按左键按下时刻回看修饰键按下-释放区间；Ctrl/Alt 单独按下或松开也不再关闭刚出现的被动按钮。 | Input / Tests / Docs |
| 2026-07-05 | 收紧 Ctrl/Alt 被动划词起手门槛：只有鼠标左键按下时修饰键已经处于长按状态才跟踪并触发悬浮按钮；鼠标按下后再按 Ctrl/Alt 不再补判，起手合法后仍允许键盘或鼠标任意先松开。 | Input / Tests / Docs |
| 2026-07-06 | 新增 `docs\release-notes\v0.2.3.md`，并将 README、打包脚本默认版本与打包策略中的对外发布示例更新为 v0.2.3；本次发布将实际测试确认的 Ctrl/Alt 起手门槛修复同步到 GitHub Release。 | Input / Tests / 文档维护 / 打包发布 |
| 2026-05-31 | 将外观页浮窗字号端点预览从 `TextBlock` 字母改为固定 24x24 画布的描边矢量 `A` 图标，消除字体基线导致的视觉错位，并让变化在界面上可见。 | Shell / Tests |
| 2026-05-31 | 新增 `docs\release-notes\v0.2.0.md`，并将 README 与打包策略中的对外发布示例更新为 v0.2.0。 | 文档维护 / 打包发布 |
| 2026-06-05 | 新增 `docs\release-notes\v0.2.1.md`，并将 README 与打包策略中的对外发布示例更新为 v0.2.1；本次小更新修复高 DPI / 200% 缩放下悬浮按钮、翻译卡片、托盘通知和托盘菜单的物理坐标定位，并强化默认翻译走 Tencent Transmart、不自动启用 OpenAI 翻译。 | Overlay / Tray / Settings / 文档维护 / 打包发布 |
| 2026-06-24 | 新增 `docs\release-notes\v0.2.2.md`，并将 README、打包脚本默认版本与打包策略中的对外发布示例更新为 v0.2.2；本次发布聚焦 Ctrl/Alt 划词触发修复，保证先按住修饰键再拖选后无论松开顺序如何都能显示悬浮按钮。 | Input / Selection / Translation / 文档维护 / 打包发布 |

### 2026-05-29 Transmart Verification Notes

- Manual smoke tests against `https://transmart.qq.com/api/imt` were executed on 2026-05-29.
- Result: HTTP `200` with `header.ret_code = "succ"` and non-empty `auto_translation` output.
- Repeated requests with current payload structure also returned successful responses.
- Conclusion: current endpoint/payload is valid; prior user issues were likely caused by stale OpenAI-style settings leaking into Transmart mode.

### 2026-05-29 Candidate Flow Hotfix

- Fixed a regression where `Ctrl` selection -> floating-button translation could stay in loading forever.
- Root cause: candidate flow created a request CTS, then called `TranslateSelectionAsync`, which immediately canceled that same token and linked a new CTS to an already canceled token.
- Fix: candidate flow no longer creates/reuses `_currentRequestCts` before `TranslateSelectionAsync`; it now passes the outer `cancellationToken` so request CTS ownership stays inside `TranslateSelectionAsync`.
- Added regression test: `translation coordinator candidate flow does not cancel its own request token`.

### 2026-05-30 Translation Channel Toggle and Loading Indicator Fix

- Fixed a routing regression where translation could still go through OpenAI after users turned off `UseOpenAiForTranslation`.
- Settings compatibility migration now only applies legacy provider migration when `UseOpenAiForTranslation` is missing from `settings.json` (legacy single-provider schema).
- Save flow explicitly skips legacy provider migration, preventing stale legacy `Provider=OpenAI` data from overriding the current toggle state.
- Loading state now shows the resolved runtime channel and preserves it during streaming and long-running popup states:
  - `正在翻译 (Tencent)...` when translation routes to Transmart.
  - `正在翻译 (<OpenAI model>)...` when translation routes to OpenAI.
  - `正在解释 (<OpenAI model>)...` for explanation mode (always OpenAI).
- Added regression tests for migration skip behavior and loading-state channel visibility.
