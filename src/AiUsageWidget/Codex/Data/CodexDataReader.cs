using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using CodexUsageWidget.Models;

namespace CodexUsageWidget.Data;

public sealed record DataReadResult(
    IReadOnlyList<UsageRecord> Records,
    int MalformedLineCount,
    string? Warning,
    LocalRateLimitSnapshot? LatestRateLimit = null,
    LocalRateLimitSnapshot? LatestFiveHourRateLimit = null,
    LocalRateLimitSnapshot? LatestWeeklyRateLimit = null);

public sealed class CodexDataReader
{
    private readonly Dictionary<string, CachedFileData> fileCache = new(StringComparer.OrdinalIgnoreCase);

    internal int FilesReadForLastCall { get; private set; }

    internal long BytesReadForLastCall { get; private set; }

    public DataReadResult Read(CodexDataPaths paths)
    {
        FilesReadForLastCall = 0;
        BytesReadForLastCall = 0;
        var warnings = new List<string>();
        var files = LocateRolloutFiles(paths, warnings);
        var records = new List<UsageRecord>();
        var malformedLineCount = 0;
        LocalRateLimitSnapshot? latestRateLimit = null;
        LocalRateLimitSnapshot? latestFiveHourRateLimit = null;
        LocalRateLimitSnapshot? latestWeeklyRateLimit = null;
        var activeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in files)
        {
            var file = Path.GetFullPath(filePath);
            activeFiles.Add(file);
            var cached = GetFileData(file);
            records.AddRange(cached.Records);
            malformedLineCount += cached.MalformedLineCount;
            if (cached.Warning is not null)
            {
                warnings.Add(cached.Warning);
            }

            latestRateLimit = SelectLatest(latestRateLimit, cached.LatestRateLimit);
            latestFiveHourRateLimit = SelectLatest(latestFiveHourRateLimit, cached.LatestFiveHourRateLimit);
            latestWeeklyRateLimit = SelectLatest(latestWeeklyRateLimit, cached.LatestWeeklyRateLimit);
        }

        foreach (var staleFile in fileCache.Keys.Where(file => !activeFiles.Contains(file)).ToArray())
        {
            fileCache.Remove(staleFile);
        }

        return new DataReadResult(
            records,
            malformedLineCount,
            warnings.Count == 0 ? null : string.Join("; ", warnings),
            latestRateLimit,
            latestFiveHourRateLimit,
            latestWeeklyRateLimit);
    }

    private CachedFileData GetFileData(string file)
    {
        var fileInfo = new FileInfo(file);
        var signature = new FileSignature(
            fileInfo.Exists ? fileInfo.Length : -1,
            fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.MinValue);
        if (fileCache.TryGetValue(file, out var cached) && cached.Signature == signature)
        {
            return cached;
        }

        if (fileCache.TryGetValue(file, out cached) &&
            cached.Warning is null &&
            signature.Length >= cached.ParsedLength &&
            signature.Length >= cached.Signature.Length &&
            (signature.Length > cached.Signature.Length ||
                cached.ParsedLength < cached.Signature.Length))
        {
            var appended = ReadFileChunk(file, cached.ParsedLength);
            BytesReadForLastCall += appended.BytesRead;
            if (appended.Warning is null)
            {
                return Merge(cached, appended, signature);
            }
        }

        FilesReadForLastCall++;
        var fresh = ReadFile(file, signature);
        fileCache[file] = fresh;
        return fresh;
    }

    private CachedFileData ReadFile(string file, FileSignature signature)
    {
        var chunk = ReadFileChunk(file, startOffset: 0);
        BytesReadForLastCall += chunk.BytesRead;
        return Merge(previous: null, chunk, signature);
    }

    private static FileChunk ReadFileChunk(string file, long startOffset)
    {
        try
        {
            using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (startOffset < 0 || startOffset > stream.Length)
            {
                return new FileChunk([], 0, $"无法读取 {Path.GetFileName(file)}: 文件长度发生变化");
            }

            stream.Position = startOffset;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            var lines = DecodeCompleteLines(bytes, startOffset, out var parsedLength);
            var parsed = ParseLines(lines, file, parsedLength, bytes.LongLength);
            return parsed;
        }
        catch (IOException exception)
        {
            return new FileChunk([], startOffset, $"无法读取 {Path.GetFileName(file)}: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return new FileChunk([], startOffset, $"无法访问 {Path.GetFileName(file)}: {exception.Message}");
        }
    }

    private static IReadOnlyList<string> DecodeCompleteLines(
        byte[] bytes,
        long startOffset,
        out long parsedLength)
    {
        if (bytes.Length == 0)
        {
            parsedLength = startOffset;
            return [];
        }

        var lastNewLine = Array.LastIndexOf(bytes, (byte)'\n');
        var completeLength = lastNewLine >= 0 ? lastNewLine + 1 : 0;
        var lines = new List<string>();
        if (completeLength > 0)
        {
            AddDecodedLines(bytes, 0, completeLength, lines);
        }

        parsedLength = startOffset + completeLength;
        if (completeLength < bytes.Length)
        {
            var finalLine = Encoding.UTF8.GetString(
                bytes,
                completeLength,
                bytes.Length - completeLength).TrimEnd('\r');
            if (IsCompleteJson(finalLine))
            {
                lines.Add(finalLine);
                parsedLength = startOffset + bytes.Length;
            }
        }

        return lines;
    }

    private static void AddDecodedLines(
        byte[] bytes,
        int offset,
        int count,
        ICollection<string> lines)
    {
        var text = Encoding.UTF8.GetString(bytes, offset, count);
        foreach (var line in text.Split('\n'))
        {
            lines.Add(line);
        }
    }

    private static bool IsCompleteJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(text.TrimStart('\uFEFF'));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static FileChunk ParseLines(
        IReadOnlyList<string> lines,
        string file,
        long parsedLength,
        long bytesRead)
    {
        var records = new List<UsageRecord>();
        var malformedLineCount = 0;
        LocalRateLimitSnapshot? latestRateLimit = null;
        LocalRateLimitSnapshot? latestFiveHourRateLimit = null;
        LocalRateLimitSnapshot? latestWeeklyRateLimit = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimStart('\uFEFF').TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                if (UsageJsonParser.TryParseRateLimits(line, out var rateLimits))
                {
                    foreach (var rateLimit in rateLimits)
                    {
                        latestRateLimit = SelectLatest(latestRateLimit, rateLimit);
                        if (rateLimit.IsFiveHour)
                        {
                            latestFiveHourRateLimit = SelectLatest(latestFiveHourRateLimit, rateLimit);
                        }

                        if (rateLimit.IsWeekly)
                        {
                            latestWeeklyRateLimit = SelectLatest(latestWeeklyRateLimit, rateLimit);
                        }
                    }
                }

                if (UsageJsonParser.TryParse(line, file, out var record) && record is not null)
                {
                    records.Add(record);
                }
            }
            catch (JsonException)
            {
                malformedLineCount++;
            }
        }

        return new FileChunk(
            records,
            parsedLength,
            null,
            malformedLineCount,
            latestRateLimit,
            latestFiveHourRateLimit,
            latestWeeklyRateLimit,
            bytesRead);
    }

    private static CachedFileData Merge(
        CachedFileData? previous,
        FileChunk chunk,
        FileSignature signature)
    {
        var records = previous is null
            ? new List<UsageRecord>()
            : new List<UsageRecord>(previous.Records);
        records.AddRange(chunk.Records);

        return new CachedFileData(
            signature,
            chunk.ParsedLength,
            records,
            (previous?.MalformedLineCount ?? 0) + chunk.MalformedLineCount,
            chunk.Warning,
            SelectLatest(previous?.LatestRateLimit, chunk.LatestRateLimit),
            SelectLatest(previous?.LatestFiveHourRateLimit, chunk.LatestFiveHourRateLimit),
            SelectLatest(previous?.LatestWeeklyRateLimit, chunk.LatestWeeklyRateLimit));
    }

    private static LocalRateLimitSnapshot? SelectLatest(
        LocalRateLimitSnapshot? current,
        LocalRateLimitSnapshot? candidate)
    {
        return candidate is not null &&
            (current is null || candidate.RecordedAt > current.RecordedAt)
            ? candidate
            : current;
    }

    private static IReadOnlyList<string> LocateRolloutFiles(CodexDataPaths paths, List<string> warnings)
    {
        var indexedPaths = ReadIndexedRolloutPaths(paths.StateDatabasePath);
        if (indexedPaths.Count > 0)
        {
            return indexedPaths
                .Select(NormalizePath)
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (!Directory.Exists(paths.SessionsDirectory))
        {
            warnings.Add("未找到 Codex sessions 目录");
            return [];
        }

        try
        {
            return Directory
                .EnumerateFiles(paths.SessionsDirectory, "*.jsonl", SearchOption.AllDirectories)
                .ToArray();
        }
        catch (IOException exception)
        {
            warnings.Add($"无法枚举 Codex sessions 目录: {exception.Message}");
            return [];
        }
        catch (UnauthorizedAccessException exception)
        {
            warnings.Add($"无法访问 Codex sessions 目录: {exception.Message}");
            return [];
        }
    }

    private static IReadOnlyList<string> ReadIndexedRolloutPaths(string databasePath)
    {
        if (!File.Exists(databasePath))
        {
            return [];
        }

        try
        {
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Cache = SqliteCacheMode.Shared,
                    Pooling = false
                }.ToString());
            connection.Open();

            try
            {
                return ReadIndexedRolloutPaths(connection, includeSource: true);
            }
            catch (SqliteException)
            {
                // Older state databases may not have the source column.
                return ReadIndexedRolloutPaths(connection, includeSource: false);
            }
        }
        catch (SqliteException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> ReadIndexedRolloutPaths(
        SqliteConnection connection,
        bool includeSource)
    {
        using var command = connection.CreateCommand();
        command.CommandText = includeSource
            ? "SELECT rollout_path, source FROM threads WHERE rollout_path IS NOT NULL AND rollout_path <> ''"
            : "SELECT rollout_path FROM threads WHERE rollout_path IS NOT NULL AND rollout_path <> ''";
        using var reader = command.ExecuteReader();
        var paths = new List<string>();
        while (reader.Read())
        {
            if (reader.IsDBNull(0))
            {
                continue;
            }

            if (includeSource && !reader.IsDBNull(1) && IsSubagentSource(reader.GetString(1)))
            {
                continue;
            }

            paths.Add(reader.GetString(0));
        }

        return paths;
    }

    private static bool IsSubagentSource(string source)
    {
        return source.Contains("subagent", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Trim();
        if (normalized.StartsWith(@"\\?\", StringComparison.Ordinal))
        {
            normalized = normalized[4..];
        }

        return normalized;
    }

    private readonly record struct FileSignature(long Length, DateTime LastWriteTimeUtc);

    private sealed record FileChunk(
        IReadOnlyList<UsageRecord> Records,
        long ParsedLength,
        string? Warning,
        int MalformedLineCount = 0,
        LocalRateLimitSnapshot? LatestRateLimit = null,
        LocalRateLimitSnapshot? LatestFiveHourRateLimit = null,
        LocalRateLimitSnapshot? LatestWeeklyRateLimit = null,
        long BytesRead = 0);

    private sealed record CachedFileData(
        FileSignature Signature,
        long ParsedLength,
        IReadOnlyList<UsageRecord> Records,
        int MalformedLineCount,
        string? Warning,
        LocalRateLimitSnapshot? LatestRateLimit,
        LocalRateLimitSnapshot? LatestFiveHourRateLimit,
        LocalRateLimitSnapshot? LatestWeeklyRateLimit);
}
