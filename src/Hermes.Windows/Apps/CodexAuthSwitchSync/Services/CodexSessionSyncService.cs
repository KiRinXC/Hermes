using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hermes.Windows.Apps.CodexAuthSwitchSync.Domain;
using Microsoft.Data.Sqlite;

namespace Hermes.Windows.Apps.CodexAuthSwitchSync.Services;

public sealed class CodexSessionSyncService
{
    private readonly CodexLocations _locations;

    public CodexSessionSyncService(CodexLocations locations)
    {
        _locations = locations;
    }

    public CodexHistoryAlignment GetAlignment(string configText, string? provider = null)
    {
        var activeProvider = provider ?? CodexToml.Provider(configText);
        var rolloutInspection = InspectRollouts();
        var rolloutCounts = rolloutInspection.ProviderCounts;
        var database = DetectStateDatabase(configText, rolloutCounts.Values.Sum());
        var sqliteCounts = CountSqliteProviders(database);
        return new CodexHistoryAlignment(
            rolloutCounts,
            sqliteCounts,
            rolloutCounts.Where(pair => !string.Equals(pair.Key, activeProvider, StringComparison.Ordinal)).Sum(pair => pair.Value),
            sqliteCounts.Where(pair => !string.Equals(pair.Key, activeProvider, StringComparison.Ordinal)).Sum(pair => pair.Value),
            database,
            rolloutInspection.Composition);
    }

    public CodexOperationResult Sync(string configText, string targetProvider, string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetProvider))
        {
            throw new CodexSwitchException("目标 config.toml 中的 model_provider 为空。");
        }

        Directory.CreateDirectory(backupDirectory);
        var changes = PlanRolloutChanges(targetProvider);
        BackupRolloutFirstLines(changes, backupDirectory);

        var database = DetectStateDatabase(configText, EnumerateRolloutFiles().Count());
        string? databaseBackup = null;
        if (database is not null && HasThreadsProviderColumn(database))
        {
            var sqliteDirectory = Path.Combine(backupDirectory, "sqlite");
            Directory.CreateDirectory(sqliteDirectory);
            databaseBackup = Path.Combine(sqliteDirectory, "state_5.sqlite.dat");
            BackupDatabaseProtected(database, databaseBackup);
        }

        var appliedRollouts = new List<RolloutChange>();
        var sqliteRows = 0;
        var databaseTouched = false;
        try
        {
            foreach (var change in changes)
            {
                ReplaceFirstLine(change.Path, change.Original, change.Replacement);
                appliedRollouts.Add(change);
            }

            if (database is not null && databaseBackup is not null)
            {
                databaseTouched = true;
                sqliteRows = UpdateDatabase(database, targetProvider);
            }

            var verification = GetAlignment(configText, targetProvider);
            if (!verification.IsAligned)
            {
                throw new CodexSwitchException(
                    $"同步校验未通过：仍有 {verification.RolloutMismatched} 个 rollout 和 {verification.SqliteMismatched} 条索引使用其他 Provider。");
            }

            return new CodexOperationResult(
                targetProvider,
                changes.Count,
                sqliteRows,
                verification,
                backupDirectory);
        }
        catch
        {
            if (databaseTouched && database is not null && databaseBackup is not null)
            {
                TryRestoreProtectedDatabase(databaseBackup, database);
            }

            for (var index = appliedRollouts.Count - 1; index >= 0; index--)
            {
                var change = appliedRollouts[index];
                try
                {
                    ReplaceFirstLine(change.Path, change.Replacement, change.Original);
                }
                catch
                {
                    // Continue restoring every file; the original error remains primary.
                }
            }

            throw;
        }
    }

    private RolloutInspection InspectRollouts()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var regularActive = 0;
        var internalActive = 0;
        var archived = 0;
        var unclassified = 0;
        foreach (var path in EnumerateRolloutFiles())
        {
            var isArchived = IsArchivedRollout(path);
            try
            {
                var record = JsonNode.Parse(Encoding.UTF8.GetString(ReadFirstLine(path))) as JsonObject;
                var provider = record?["payload"]?["model_provider"]?.GetValue<string>() ?? "(missing)";
                counts[provider] = counts.GetValueOrDefault(provider) + 1;

                if (isArchived)
                {
                    archived++;
                }
                else if (IsInternalSession(record))
                {
                    internalActive++;
                }
                else if (IsSessionMetadata(record))
                {
                    regularActive++;
                }
                else
                {
                    unclassified++;
                }
            }
            catch
            {
                counts["(unreadable)"] = counts.GetValueOrDefault("(unreadable)") + 1;
                if (isArchived)
                {
                    archived++;
                }
                else
                {
                    unclassified++;
                }
            }
        }

        return new RolloutInspection(
            counts,
            new CodexSessionComposition(regularActive, internalActive, archived, unclassified));
    }

    private bool IsArchivedRollout(string path)
    {
        var archiveRoot = Path.GetFullPath(Path.Combine(_locations.CodexHome, "archived_sessions"))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(archiveRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSessionMetadata(JsonObject? record) =>
        string.Equals(record?["type"]?.GetValue<string>(), "session_meta", StringComparison.Ordinal)
        && record?["payload"] is JsonObject;

    private static bool IsInternalSession(JsonObject? record)
    {
        if (!IsSessionMetadata(record) || record?["payload"] is not JsonObject payload)
        {
            return false;
        }

        return payload["source"] switch
        {
            JsonObject source => source.ContainsKey("subagent"),
            JsonValue source when source.TryGetValue<string>(out var value) =>
                value.Contains("subagent", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private IReadOnlyDictionary<string, int> CountSqliteProviders(string? path)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (path is null)
        {
            return counts;
        }

        try
        {
            using var connection = OpenReadOnly(path);
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT model_provider COLLATE BINARY, COUNT(*)
                FROM threads
                GROUP BY model_provider COLLATE BINARY
                """;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var provider = reader.IsDBNull(0) ? "(missing)" : reader.GetString(0);
                counts[provider] = checked((int)reader.GetInt64(1));
            }
        }
        catch (Exception) when (File.Exists(path))
        {
            counts["(unreadable)"] = 1;
        }

        return counts;
    }

    private List<RolloutChange> PlanRolloutChanges(string targetProvider)
    {
        var changes = new List<RolloutChange>();
        foreach (var path in EnumerateRolloutFiles())
        {
            byte[] firstLine;
            try
            {
                firstLine = ReadFirstLine(path);
            }
            catch (IOException ex)
            {
                throw new CodexSwitchException($"无法读取会话文件 {Path.GetFileName(path)}。", ex);
            }

            var newlineLength = firstLine.EndsWith("\r\n"u8) ? 2 : firstLine.EndsWith("\n"u8) ? 1 : 0;
            var body = newlineLength == 0 ? firstLine : firstLine[..^newlineLength];
            JsonObject? record;
            try
            {
                record = JsonNode.Parse(body) as JsonObject;
            }
            catch (JsonException)
            {
                continue;
            }

            if (record?["type"]?.GetValue<string>() != "session_meta" || record["payload"] is not JsonObject payload)
            {
                continue;
            }

            if (string.Equals(payload["model_provider"]?.GetValue<string>(), targetProvider, StringComparison.Ordinal))
            {
                continue;
            }

            payload["model_provider"] = targetProvider;
            var replacementBody = JsonSerializer.SerializeToUtf8Bytes(record, new JsonSerializerOptions { WriteIndented = false });
            var replacement = new byte[replacementBody.Length + newlineLength];
            replacementBody.CopyTo(replacement, 0);
            if (newlineLength == 2)
            {
                replacement[^2] = (byte)'\r';
            }

            if (newlineLength > 0)
            {
                replacement[^1] = (byte)'\n';
            }

            changes.Add(new RolloutChange(path, firstLine, replacement));
        }

        return changes;
    }

    private string? DetectStateDatabase(string configText, int rolloutCount)
    {
        var candidates = new List<string>();
        var configured = CodexToml.ReadRootString(configText, "sqlite_home");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var expanded = Environment.ExpandEnvironmentVariables(configured);
            if (expanded.StartsWith('~'))
            {
                expanded = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    expanded.TrimStart('~', '/', '\\'));
            }

            var configuredDirectory = Path.IsPathRooted(expanded)
                ? expanded
                : Path.Combine(_locations.CodexHome, expanded);
            candidates.Add(Path.Combine(configuredDirectory, "state_5.sqlite"));
        }

        candidates.Add(Path.Combine(_locations.CodexHome, "sqlite", "state_5.sqlite"));
        candidates.Add(Path.Combine(_locations.CodexHome, "state_5.sqlite"));
        var existing = candidates
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToArray();
        if (existing.Length == 0)
        {
            return null;
        }

        var readable = new List<(string Path, long Distance, long NegativeRows, long NegativeTicks, int Priority)>();
        for (var index = 0; index < existing.Length; index++)
        {
            try
            {
                var rows = ThreadCount(existing[index]);
                var distance = rolloutCount == 0 ? 0 : Math.Abs(rows - rolloutCount);
                readable.Add((existing[index], distance, -rows, -File.GetLastWriteTimeUtc(existing[index]).Ticks, index));
            }
            catch
            {
                // Preserve the first known location for diagnostics if all candidates fail.
            }
        }

        return readable
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.NegativeRows)
            .ThenBy(item => item.NegativeTicks)
            .ThenBy(item => item.Priority)
            .Select(item => item.Path)
            .FirstOrDefault() ?? existing[0];
    }

    private static long ThreadCount(string path)
    {
        using var connection = OpenReadOnly(path);
        if (!HasThreadsProviderColumn(connection))
        {
            throw new CodexSwitchException($"{Path.GetFileName(path)} 不包含可同步的 threads.model_provider。");
        }

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM threads";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static bool HasThreadsProviderColumn(string path)
    {
        using var connection = OpenReadOnly(path);
        return HasThreadsProviderColumn(connection);
    }

    private static bool HasThreadsProviderColumn(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(threads)";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), "model_provider", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void BackupDatabaseProtected(string sourcePath, string backupPath)
    {
        var clearPath = backupPath + $".clear-{Guid.NewGuid():N}";
        using var source = OpenReadOnly(sourcePath);
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = clearPath,
            Pooling = false
        }.ToString());
        destination.Open();
        source.BackupDatabase(destination);
        destination.Close();
        source.Close();
        try
        {
            AtomicFile.WriteAllBytes(backupPath, CodexDpapi.Protect(File.ReadAllBytes(clearPath)));
        }
        finally
        {
            if (File.Exists(clearPath))
            {
                File.Delete(clearPath);
            }
        }
    }

    private static void TryRestoreProtectedDatabase(string backupPath, string destinationPath)
    {
        var clearPath = backupPath + $".restore-{Guid.NewGuid():N}";
        try
        {
            AtomicFile.WriteAllBytes(clearPath, CodexDpapi.Unprotect(File.ReadAllBytes(backupPath)));
            using var source = OpenReadOnly(clearPath);
            using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = destinationPath,
                Pooling = false
            }.ToString());
            destination.Open();
            source.BackupDatabase(destination);
        }
        catch
        {
            // Best effort rollback; the caller still receives the original failure.
        }
        finally
        {
            if (File.Exists(clearPath))
            {
                File.Delete(clearPath);
            }
        }
    }

    private static int UpdateDatabase(string path, string targetProvider)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false
        }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE threads
            SET model_provider = $provider
            WHERE (COALESCE(model_provider, '') COLLATE BINARY) <> ($provider COLLATE BINARY)
            """;
        command.Parameters.AddWithValue("$provider", targetProvider);
        var changed = command.ExecuteNonQuery();
        transaction.Commit();
        return changed;
    }

    private IEnumerable<string> EnumerateRolloutFiles()
    {
        foreach (var directoryName in new[] { "sessions", "archived_sessions" })
        {
            var directory = Path.Combine(_locations.CodexHome, directoryName);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "rollout-*.jsonl", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }
    }

    private static byte[] ReadFirstLine(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return ReadFirstLine(stream);
    }

    private static byte[] ReadFirstLine(Stream stream)
    {
        using var line = new MemoryStream();
        while (true)
        {
            var value = stream.ReadByte();
            if (value < 0)
            {
                break;
            }

            line.WriteByte((byte)value);
            if (value == '\n')
            {
                break;
            }

            if (line.Length > 4 * 1024 * 1024)
            {
                throw new CodexSwitchException("会话元数据首行异常过长，已停止同步。");
            }
        }

        return line.ToArray();
    }

    private static void ReplaceFirstLine(string path, byte[] expected, byte[] replacement)
    {
        var directory = Path.GetDirectoryName(path)!;
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.tmp-{Environment.ProcessId}-{Guid.NewGuid():N}");
        try
        {
            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var actual = ReadFirstLine(source);
                if (!actual.AsSpan().SequenceEqual(expected))
                {
                    throw new CodexSwitchException($"会话文件在同步期间发生变化：{path}");
                }

                using var destination = new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.WriteThrough);
                destination.Write(replacement);
                source.CopyTo(destination);
                destination.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void BackupRolloutFirstLines(IReadOnlyCollection<RolloutChange> changes, string backupDirectory)
    {
        var manifest = changes.Select(change => new
        {
            change.Path,
            OriginalFirstLine = Convert.ToBase64String(change.Original)
        });
        var clear = JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions { WriteIndented = true });
        var protectedBytes = CodexDpapi.Protect(clear);
        AtomicFile.WriteAllBytes(Path.Combine(backupDirectory, "rollouts.dat"), protectedBytes);
    }

    private sealed record RolloutInspection(
        IReadOnlyDictionary<string, int> ProviderCounts,
        CodexSessionComposition Composition);

    private sealed record RolloutChange(string Path, byte[] Original, byte[] Replacement);
}
