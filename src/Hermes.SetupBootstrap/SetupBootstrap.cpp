#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif
#define WIN32_LEAN_AND_MEAN

#include <windows.h>
#include <shellapi.h>
#include <shlobj.h>

#include <string>
#include <vector>

namespace
{
constexpr int IDR_CORE_SETUP = 201;
constexpr wchar_t MigrationMarkerName[] = L".legacy-data-migrated";
constexpr const wchar_t* DataFileNames[] = {
    L"settings.json",
    L"secrets.dat",
    L"history.json",
    L"app.log"
};
constexpr const wchar_t* DataDirectoryNames[] = { L"apps" };

HINSTANCE g_instance = nullptr;

std::wstring JoinPath(const std::wstring& left, const std::wstring& right)
{
    if (left.empty() || left.back() == L'\\' || left.back() == L'/')
    {
        return left + right;
    }
    return left + L"\\" + right;
}

bool FileExists(const std::wstring& path)
{
    const DWORD attributes = GetFileAttributesW(path.c_str());
    return attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) == 0;
}

bool DirectoryExists(const std::wstring& path)
{
    const DWORD attributes = GetFileAttributesW(path.c_str());
    return attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
}

bool EnsureDirectory(const std::wstring& path)
{
    return DirectoryExists(path)
        || SHCreateDirectoryExW(nullptr, path.c_str(), nullptr) == ERROR_SUCCESS
        || DirectoryExists(path);
}

bool CopyDirectory(const std::wstring& source, const std::wstring& target, bool overwrite)
{
    if (!EnsureDirectory(target))
    {
        return false;
    }

    WIN32_FIND_DATAW data{};
    HANDLE find = FindFirstFileW(JoinPath(source, L"*").c_str(), &data);
    if (find == INVALID_HANDLE_VALUE)
    {
        return GetLastError() == ERROR_FILE_NOT_FOUND;
    }

    bool success = true;
    do
    {
        const std::wstring name(data.cFileName);
        if (name == L"." || name == L".." || (data.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
        {
            continue;
        }

        const auto sourcePath = JoinPath(source, name);
        const auto targetPath = JoinPath(target, name);
        if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
        {
            success = CopyDirectory(sourcePath, targetPath, overwrite);
        }
        else if ((overwrite || !FileExists(targetPath))
            && CopyFileW(sourcePath.c_str(), targetPath.c_str(), overwrite ? FALSE : TRUE) == FALSE)
        {
            success = false;
        }
    } while (success && FindNextFileW(find, &data));

    FindClose(find);
    return success;
}

bool MigrateUserData(const std::wstring& source, const std::wstring& target)
{
    if (!DirectoryExists(source) || FileExists(JoinPath(target, MigrationMarkerName)))
    {
        return true;
    }

    if (!EnsureDirectory(target))
    {
        return false;
    }

    for (const auto* fileName : DataFileNames)
    {
        const auto sourcePath = JoinPath(source, fileName);
        if (FileExists(sourcePath)
            && CopyFileW(sourcePath.c_str(), JoinPath(target, fileName).c_str(), FALSE) == FALSE)
        {
            return false;
        }
    }

    for (const auto* directoryName : DataDirectoryNames)
    {
        const auto sourcePath = JoinPath(source, directoryName);
        if (DirectoryExists(sourcePath)
            && !CopyDirectory(sourcePath, JoinPath(target, directoryName), true))
        {
            return false;
        }
    }

    return true;
}

bool MigrateInstalledUserData()
{
    PWSTR localAppData = nullptr;
    if (FAILED(SHGetKnownFolderPath(FOLDERID_LocalAppData, KF_FLAG_DEFAULT, nullptr, &localAppData)))
    {
        return false;
    }

    const std::wstring root(localAppData);
    CoTaskMemFree(localAppData);
    return MigrateUserData(
        JoinPath(root, L"Hermes"),
        JoinPath(JoinPath(root, L"KiRinXC"), L"Hermes"));
}

bool ExtractCoreSetup(const std::wstring& outputPath)
{
    HRSRC resource = FindResourceW(g_instance, MAKEINTRESOURCEW(IDR_CORE_SETUP), RT_RCDATA);
    if (resource == nullptr)
    {
        return false;
    }

    HGLOBAL loaded = LoadResource(g_instance, resource);
    const DWORD size = SizeofResource(g_instance, resource);
    const void* bytes = loaded == nullptr ? nullptr : LockResource(loaded);
    if (bytes == nullptr || size < 1024 * 1024)
    {
        return false;
    }

    HANDLE file = CreateFileW(outputPath.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS,
        FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE)
    {
        return false;
    }

    DWORD written = 0;
    const bool success = WriteFile(file, bytes, size, &written, nullptr) != FALSE && written == size;
    CloseHandle(file);
    return success;
}

std::wstring QuoteArgument(const std::wstring& argument)
{
    if (argument.find_first_of(L" \t\"") == std::wstring::npos)
    {
        return argument;
    }

    std::wstring result = L"\"";
    size_t slashCount = 0;
    for (const wchar_t value : argument)
    {
        if (value == L'\\')
        {
            ++slashCount;
            continue;
        }

        if (value == L'\"')
        {
            result.append(slashCount * 2 + 1, L'\\');
            result.push_back(L'\"');
            slashCount = 0;
            continue;
        }

        result.append(slashCount, L'\\');
        slashCount = 0;
        result.push_back(value);
    }
    result.append(slashCount * 2, L'\\');
    result.push_back(L'\"');
    return result;
}

DWORD RunCoreSetup(const std::wstring& setupPath, int argumentCount, wchar_t** arguments)
{
    std::wstring commandLine = QuoteArgument(setupPath);
    for (int index = 1; index < argumentCount; ++index)
    {
        commandLine += L" ";
        commandLine += QuoteArgument(arguments[index]);
    }

    std::vector<wchar_t> mutableCommand(commandLine.begin(), commandLine.end());
    mutableCommand.push_back(L'\0');
    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    if (!CreateProcessW(setupPath.c_str(), mutableCommand.data(), nullptr, nullptr, FALSE, 0,
            nullptr, nullptr, &startup, &process))
    {
        return 0xFFFFFFFF;
    }

    WaitForSingleObject(process.hProcess, INFINITE);
    DWORD exitCode = 0xFFFFFFFF;
    GetExitCodeProcess(process.hProcess, &exitCode);
    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    return exitCode;
}

bool VerifyEmbeddedPackage()
{
    HRSRC resource = FindResourceW(g_instance, MAKEINTRESOURCEW(IDR_CORE_SETUP), RT_RCDATA);
    return resource != nullptr && SizeofResource(g_instance, resource) > 1024 * 1024;
}

struct LocalArguments
{
    wchar_t** Value;

    ~LocalArguments()
    {
        LocalFree(Value);
    }
};
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int)
{
    g_instance = instance;
    int argumentCount = 0;
    wchar_t** arguments = CommandLineToArgvW(GetCommandLineW(), &argumentCount);
    if (arguments == nullptr)
    {
        return 1;
    }
    LocalArguments argumentOwner{arguments};

    if (argumentCount == 2
        && CompareStringOrdinal(arguments[1], -1, L"--verify-package", -1, TRUE) == CSTR_EQUAL)
    {
        return VerifyEmbeddedPackage() ? 0 : 2;
    }

    if (argumentCount == 4
        && CompareStringOrdinal(arguments[1], -1, L"--test-migrate", -1, TRUE) == CSTR_EQUAL)
    {
        return MigrateUserData(arguments[2], arguments[3]) ? 0 : 3;
    }

    if (!MigrateInstalledUserData())
    {
        MessageBoxW(nullptr,
            L"无法安全备份现有 Hermes 用户数据，安装已停止。请检查用户数据目录权限后重试。",
            L"Hermes 安装程序",
            MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
        return 4;
    }

    wchar_t tempRoot[MAX_PATH + 1]{};
    const DWORD tempRootLength = GetTempPathW(MAX_PATH, tempRoot);
    if (tempRootLength == 0 || tempRootLength > MAX_PATH)
    {
        return 5;
    }

    const auto workDirectory = JoinPath(tempRoot,
        L"HermesSetupBootstrap-" + std::to_wstring(GetCurrentProcessId()));
    if (!EnsureDirectory(workDirectory))
    {
        return 6;
    }

    const auto coreSetup = JoinPath(workDirectory, L"Hermes-Core-Setup.exe");
    if (!ExtractCoreSetup(coreSetup))
    {
        MessageBoxW(nullptr,
            L"安装包内容不完整，请重新下载 Hermes 安装程序。",
            L"Hermes 安装程序",
            MB_OK | MB_ICONERROR | MB_SETFOREGROUND);
        return 7;
    }

    const DWORD exitCode = RunCoreSetup(coreSetup, argumentCount, arguments);
    if (!DeleteFileW(coreSetup.c_str()))
    {
        MoveFileExW(coreSetup.c_str(), nullptr, MOVEFILE_DELAY_UNTIL_REBOOT);
    }
    RemoveDirectoryW(workDirectory.c_str());
    return static_cast<int>(exitCode);
}
