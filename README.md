# Hermes.Windows

Windows 10/11 desktop translation assistant for translating selected English text into Simplified Chinese with a global hotkey, tray menu, clipboard fallback, and lightweight overlay UI.

## Current MVP Shape

- WPF desktop app targeting `net10.0-windows`
- Tray resident lifecycle with pause/resume, settings, clipboard translation, history placeholder, and exit
- Configurable OpenAI-compatible Responses API settings
- API key stored separately with Windows DPAPI
- Global hotkey default: `Ctrl+Alt+E`
- UI Automation selected-text read first, controlled clipboard fallback for explicit user triggers
- Floating icon button after mouse text selection when UI Automation can read selected text
- Translation popup with loading, long-running, success, error, copy, retry, pin, close, and settings action
- Disabled-by-default local history hooks

## Required SDK

This project is intended for .NET 10 and WPF:

```powershell
D:\Code\Env\dotnet\dotnet.exe --info
D:\Code\Env\dotnet\dotnet.exe build Hermes.sln
D:\Code\Env\dotnet\dotnet.exe run --project tests/Hermes.Tests/Hermes.Tests.csproj
```

The development SDK is installed under `D:\Code\Env\dotnet`.

## Installed App

The portable self-contained app build is installed under:

```powershell
D:\Code\Demo\Hermes\Hermes.Windows.exe
```

Publish it with the bundled .NET runtime so the app can be launched by double-clicking without installing .NET globally:

```powershell
D:\Code\Env\dotnet\dotnet.exe publish src\Hermes.Windows\Hermes.Windows.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o D:\Code\Demo\Hermes
```

## API Configuration

Open settings from the tray icon and configure:

- Provider: OpenAI or OpenAI-compatible
- Base URL: defaults to `https://api.openai.com/v1`
- API Key: encrypted locally using Windows-protected storage
- Model: defaults to `gpt-4.1-mini`, but can be changed without code changes

Requests are sent to `{Base URL}/responses`.

## Privacy Notes

- Translation text is only sent when the user explicitly presses the hotkey, clicks the floating icon, or chooses translate clipboard.
- API keys are not stored in `settings.json`.
- Translation history is disabled by default.
- Logs redact API-key shaped content and do not intentionally include full source or translated text.
- Excluded and sensitive application lists are available in settings data; password fields are ignored when UI Automation marks them as protected.

## Known Compatibility Limits

Global selection detection on Windows is not universal. UI Automation works well in some native controls and less consistently in custom, browser, PDF, and Electron text surfaces. The hotkey path uses clipboard fallback as the compatibility-first MVP route.
