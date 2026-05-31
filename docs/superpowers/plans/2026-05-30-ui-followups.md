# Hermes UI Follow-ups Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修正浮窗字号控制、图标大小真实预览，以及通知点击后设置窗置顶这三处 UI/交互问题。

**Architecture:** 保持现有设置与浮窗管线不变，只替换设置页控件形态、复用现有图标资源，并在设置窗激活路径上补一个显式抬前逻辑。测试继续使用当前轻量控制台套件做源码断言回归。

**Tech Stack:** WPF, Win32 window activation, Hermes lightweight source-based tests

---

### Task 1: Add regression tests

**Files:**
- Modify: `tests/Hermes.Tests/Shell/SettingsWindowOptionTests.cs`
- Modify: `tests/Hermes.Tests/Overlay/OverlayExperienceTests.cs`
- Modify: `tests/Hermes.Tests/Tray/TrayServiceTests.cs`

- [ ] **Step 1: Write the failing tests**
- [ ] **Step 2: Run `powershell -ExecutionPolicy Bypass -File scripts\Test-Hermes.ps1` and confirm the new assertions fail**

### Task 2: Implement settings and popup UI changes

**Files:**
- Modify: `src/Hermes.Windows/Shell/SettingsWindow.xaml`
- Modify: `src/Hermes.Windows/Shell/SettingsWindow.xaml.cs`
- Modify: `src/Hermes.Windows/Overlay/TranslationPopupWindow.xaml.cs`

- [ ] **Step 1: Replace appearance font-size slider with a five-dot selector using small/large `A` previews**
- [ ] **Step 2: Switch icon-size preview endpoints to Hermes app icon resources**
- [ ] **Step 3: Make popup theme application update both translated body and source preview font sizes**
- [ ] **Step 4: Run `powershell -ExecutionPolicy Bypass -File scripts\Test-Hermes.ps1` and confirm tests pass**

### Task 3: Implement foreground activation for settings

**Files:**
- Modify: `src/Hermes.Windows/App.xaml.cs`
- Test: `tests/Hermes.Tests/Tray/TrayServiceTests.cs`

- [ ] **Step 1: Add explicit foreground activation logic when showing settings from tray/notification flows**
- [ ] **Step 2: Run `powershell -ExecutionPolicy Bypass -File scripts\Test-Hermes.ps1` and confirm the activation regression stays green**

### Task 4: Sync docs and publish

**Files:**
- Modify: `Design.md`
- Modify: `README.md`

- [ ] **Step 1: Update user-facing docs for the new font-size control and notification foreground behavior**
- [ ] **Step 2: Run `git diff --check`**
- [ ] **Step 3: Run `powershell -ExecutionPolicy Bypass -File scripts\Publish-Hermes.ps1`**
- [ ] **Step 4: Verify `artifacts\publish\Hermes.Windows\manual-test\win-x64-self-contained\` contains `Hermes.Windows.exe`, `coreclr.dll`, `hostfxr.dll`, `hostpolicy.dll`, and `System.Private.CoreLib.dll`**
