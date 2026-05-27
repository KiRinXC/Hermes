# Hermes

Hermes 是一个 Windows 全局 AI 划词翻译助手。选中英文内容后，按快捷键或点击悬浮按钮，就能把结果以轻量浮窗的形式翻译成简体中文。

<p align="center">
  <img src="docs/assets/Info.png" alt="Hermes red octopus AI translation assistant connecting to desktop apps" width="860">
</p>

## 适合谁

- 经常在浏览器、PDF、VS Code、Notion、Slack、Word 等软件里阅读英文内容的人。
- 希望不复制、不切窗口、不打开网页翻译器，就能快速理解一段英文的人。
- 想用自己的 OpenAI 或 OpenAI-compatible API Key 做本地桌面翻译的人。

## 功能亮点

- 全局快捷键翻译，默认 `Ctrl+Alt+E`。
- 按住 `Ctrl` 划词后显示悬浮翻译按钮，普通划词不会触发。
- 翻译结果以悬浮卡片显示，支持复制、重新翻译、固定、关闭和拖动。
- 使用 OpenAI Responses API，支持流式输出，译文会边生成边显示。
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
3. 填入 API Key、Base URL 和 Model。
4. 选中一段英文文本，按 `Ctrl+Alt+E` 翻译。
5. 或按住 `Ctrl` 划选英文文本，点击出现的悬浮翻译图标。

默认 Base URL：

```text
https://api.openai.com/v1
```

默认模型：

```text
gpt-4.1-mini
```

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
D:\Code\Env\dotnet\dotnet.exe
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
