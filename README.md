# Hermes

Hermes 是一个 Windows 全局 AI 划词翻译助手。选中英文内容后，按快捷键或点击悬浮按钮，就能把结果以轻量浮窗的形式翻译成简体中文。

<p align="center">
  <img src="docs/assets/Info.png" alt="Hermes red octopus AI translation assistant connecting to desktop apps" width="860">
</p>

## 适合谁

- 经常在浏览器、PDF、VS Code、Notion、Slack、Word 等软件里阅读英文内容的人。
- 希望不复制、不切窗口、不打开网页翻译器，就能快速理解一段英文的人。
- 希望默认即用腾讯交互翻译（Transmart），并按需切换到 OpenAI 的人。

## 功能亮点

- 全局快捷键翻译，默认 `Ctrl+Alt+E`。
- 按住 `Ctrl` 划词后显示悬浮翻译按钮并执行翻译，普通划词不会触发。
- 按住 `Alt` 划词后使用同一悬浮按钮触发术语解释（可配置“解释个性化偏好”）。
- 翻译结果以悬浮卡片显示，支持复制、重新翻译、固定、关闭和拖动。
- 翻译卡片和设置窗口支持从边缘/四角调整大小并自动记忆；悬浮按钮图标大小和浮窗字号都可在外观页通过五点横向控件选择，图标预览会随浅色/深色主题切换且保持透明背景。
- 默认使用腾讯 Transmart 翻译；可切换 OpenAI / OpenAI-compatible。
- OpenAI 模式支持 Responses API 流式输出，译文会边生成边显示。
- 支持 OpenAI-compatible Base URL 和自定义模型名。
- API Key 使用 Windows DPAPI 加密保存在本机。
- 支持浅色、深色和跟随系统主题。
- 默认不保存翻译历史，隐私优先。

## 下载和运行

正式对外发布时，请在 GitHub Releases 中下载：

```text
Hermes-v0.1.0-win-x64-portable.zip
```

解压后运行：

```text
Hermes.Windows.exe
```

这是 `win-x64 self-contained` portable 包，目标机器不需要额外安装 .NET Runtime。

如果你是从源码构建，当前本机验证包位于：

```text
artifacts\publish\Hermes.Windows\manual-test\win-x64-self-contained\
```

## 快速开始

1. 启动 `Hermes.Windows.exe`。
2. 在系统托盘中打开 Hermes 设置。
3. 默认 Provider 为 Transmart，可直接使用；如需 OpenAI 再填写 API Key / Base URL / Model。
4. 选中一段英文文本，按 `Ctrl+Alt+E` 翻译。
5. 或按住 `Ctrl` 划选英文文本，点击出现的悬浮翻译图标。
6. 按住 `Alt` 划选术语，点击悬浮按钮查看解释。

默认 Base URL：

```text
https://transmart.qq.com/api
```

默认模型：

```text
normal
```

说明：术语解释（`Alt` 划词）固定走 OpenAI 通道，请在“AI解释”区域配置 OpenAI API。

## 使用边界

Windows 上不同应用暴露选区的方式并不一致，所以 Hermes 采用多层策略：

1. 优先通过 Windows UI Automation 读取当前选区。
2. 支持用快捷键稳定触发翻译。
3. 在显式触发时，必要情况下使用受控剪贴板兜底。

这意味着：Chrome、Edge、VS Code、记事本、PDF 阅读器、办公软件等主流应用会尽量提供顺滑体验；少数应用可能需要使用快捷键或剪贴板兜底。

## 隐私说明

- 只有用户主动按快捷键、点击悬浮按钮或选择翻译剪贴板时，Hermes 才会发送文本。
- API Key 不写入 `settings.json`，而是使用 Windows DPAPI 加密保存。
- 翻译历史默认关闭。
- 日志默认不记录完整原文和译文，也会脱敏 API Key 形态的内容。
- 可在设置中维护排除应用和敏感应用列表。

## 从源码构建

项目目标框架是 `net10.0-windows`。当前开发环境使用：

```powershell
C:\Code\Env\dotnet\dotnet.exe
```

常用命令：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Restore-Hermes.ps1
powershell -ExecutionPolicy Bypass -File scripts\Test-Hermes.ps1
powershell -ExecutionPolicy Bypass -File scripts\Publish-Hermes.ps1
```

生成 GitHub Release portable zip：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Package-HermesRelease.ps1 -Version 0.1.0
```

输出位置：

```text
artifacts\release\v0.1.0\
```

其中包含：

- `Hermes-v0.1.0-win-x64-portable.zip`
- `checksums.txt`

## GitHub Release 流程

1. 运行测试：`scripts\Test-Hermes.ps1`
2. 生成 zip：`scripts\Package-HermesRelease.ps1 -Version 0.1.0`
3. 创建 tag：`v0.1.0`
4. 在 GitHub Releases 上传 zip 和 `checksums.txt`
5. 把 `docs/release-notes/v0.1.0.md` 的内容作为 Release Notes

> 目前 Hermes 还没有代码签名证书。Windows SmartScreen 可能会提示未知发布者，这是独立 Windows 应用早期发布时常见的情况。

## 项目文档

- `UI.md`：Hermes 的界面设计原则、视觉系统和组件规范。
- `Design.md`：项目结构、核心流程、模块职责、打包策略和变更记录。
- `docs/release-notes/`：GitHub Release 文案草稿。
- `docs/prompts/`：README 头图等视觉素材提示词。

## Provider Notes (2026-05-29)

- Translation settings now use a dual-channel layout: `AI翻译` configures Tencent Transmart, and `AI解释` keeps OpenAI configuration with a `翻译走 OpenAI` toggle.
- Explanation mode always uses OpenAI.
- Translation mode uses OpenAI only when `翻译走 OpenAI` is enabled; otherwise it uses Transmart.
- Legacy single-provider settings are migrated automatically on load.
- Legacy provider migration is only applied when `UseOpenAiForTranslation` is missing from saved settings (old schema), so manual toggle changes are not overwritten.
- Popup loading text now exposes the active channel at runtime and keeps that channel visible during streaming or long-running states:
  - `正在翻译 (Tencent)...` for Transmart translation.
  - `正在翻译 (<OpenAI model>)...` for OpenAI translation.
