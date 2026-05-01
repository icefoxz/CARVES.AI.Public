using System.Text;
using System.Text.Json;

namespace Carves.Runtime.Application.Guard;

public sealed class GuardDecisionAuditStore
{
    public const int CurrentSchemaVersion = 1;
    public const string RelativeAuditPath = ".ai/runtime/guard/decisions.jsonl";
    public const int MaxStoredLineCount = 1000;
    public const string WriteConcurrencyPolicy = "file_exclusive_append_lock";
    public const int WriteLockRetryCount = 50;
    public const int WriteLockRetryDelayMilliseconds = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public string ResolveAuditPath(string repositoryRoot)
    {
        return Path.Combine(repositoryRoot, ".ai", "runtime", "guard", "decisions.jsonl");
    }

    public bool TryAppend(string repositoryRoot, GuardDecisionAuditRecord record)
    {
        for (var attempt = 0; attempt <= WriteLockRetryCount; attempt++)
        {
            try
            {
                var path = ResolveAuditPath(repositoryRoot);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var stream = new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
                stream.Seek(0, SeekOrigin.End);
                using (var writer = new StreamWriter(stream, Utf8NoBom, bufferSize: 4096, leaveOpen: true))
                {
                    writer.WriteLine(JsonSerializer.Serialize(record, JsonOptions));
                }

                TryTrimToMaxStoredLines(stream);

                return true;
            }
            catch (IOException) when (attempt < WriteLockRetryCount)
            {
                Thread.Sleep(WriteLockRetryDelayMilliseconds);
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    public GuardDecisionReadResult LoadRecent(string repositoryRoot, int limit = 10)
    {
        var path = ResolveAuditPath(repositoryRoot);
        var read = ReadAll(path, limit);
        var records = read.Records
            .OrderByDescending(record => record.RecordedAtUtc)
            .ThenByDescending(record => record.RunId, StringComparer.Ordinal)
            .Take(read.Diagnostics.EffectiveLimit)
            .ToArray();
        return read with
        {
            Records = records,
            Diagnostics = read.Diagnostics with
            {
                ReturnedRecordCount = records.Length,
            },
        };
    }

    public GuardDecisionFindResult Find(string repositoryRoot, string runId)
    {
        var path = ResolveAuditPath(repositoryRoot);
        var read = ReadAll(
            path,
            requestedLimit: MaxStoredLineCount,
            effectiveLimit: MaxStoredLineCount);
        var record = read.Records
            .Where(record => string.Equals(record.RunId, runId, StringComparison.Ordinal))
            .OrderByDescending(record => record.RecordedAtUtc)
            .FirstOrDefault();
        return new GuardDecisionFindResult(
            record,
            read.Diagnostics with
            {
                ReturnedRecordCount = record is null ? 0 : 1,
            });
    }

    private static GuardDecisionReadResult ReadAll(string path, int limit)
    {
        return ReadAll(
            path,
            requestedLimit: limit,
            effectiveLimit: limit <= 0 ? 0 : Math.Clamp(limit, 1, 100));
    }

    private static GuardDecisionReadResult ReadAll(string path, int requestedLimit, int effectiveLimit)
    {
        if (!File.Exists(path) || effectiveLimit <= 0)
        {
            return new GuardDecisionReadResult(
                Array.Empty<GuardDecisionAuditRecord>(),
                new GuardDecisionReadDiagnostics(
                    RequestedLimit: requestedLimit,
                    EffectiveLimit: effectiveLimit,
                    TotalLineCount: 0,
                    EmptyLineCount: 0,
                    LoadedRecordCount: 0,
                    ReturnedRecordCount: 0,
                    SkippedRecordCount: 0,
                    MalformedRecordCount: 0,
                    FutureVersionRecordCount: 0,
                    MaxStoredLineCount));
        }

        var records = new List<GuardDecisionAuditRecord>();
        var totalLineCount = 0;
        var emptyLineCount = 0;
        var malformedRecordCount = 0;
        var futureVersionRecordCount = 0;
        foreach (var line in File.ReadLines(path))
        {
            totalLineCount++;
            if (string.IsNullOrWhiteSpace(line))
            {
                emptyLineCount++;
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                if (!document.RootElement.TryGetProperty("schema_version", out var schemaVersionElement)
                    || schemaVersionElement.ValueKind != JsonValueKind.Number
                    || !schemaVersionElement.TryGetInt32(out var schemaVersion))
                {
                    malformedRecordCount++;
                    continue;
                }

                if (schemaVersion > CurrentSchemaVersion)
                {
                    futureVersionRecordCount++;
                    continue;
                }

                if (schemaVersion != CurrentSchemaVersion)
                {
                    malformedRecordCount++;
                    continue;
                }

                var record = JsonSerializer.Deserialize<GuardDecisionAuditRecord>(line, JsonOptions);
                if (IsUsableRecord(record))
                {
                    records.Add(record!);
                    continue;
                }

                malformedRecordCount++;
            }
            catch (JsonException)
            {
                malformedRecordCount++;
            }
        }

        var skippedRecordCount = malformedRecordCount + futureVersionRecordCount;
        return new GuardDecisionReadResult(
            records,
            new GuardDecisionReadDiagnostics(
                RequestedLimit: requestedLimit,
                EffectiveLimit: effectiveLimit,
                totalLineCount,
                emptyLineCount,
                records.Count,
                ReturnedRecordCount: 0,
                skippedRecordCount,
                malformedRecordCount,
                futureVersionRecordCount,
                MaxStoredLineCount));
    }

    private static bool IsUsableRecord(GuardDecisionAuditRecord? record)
    {
        return record is not null
               && !string.IsNullOrWhiteSpace(record.RunId)
               && !string.IsNullOrWhiteSpace(record.Outcome)
               && !string.IsNullOrWhiteSpace(record.PolicyId)
               && !string.IsNullOrWhiteSpace(record.Source)
               && record.ChangedFiles is not null
               && record.Violations is not null
               && record.Warnings is not null
               && record.EvidenceRefs is not null;
    }

    private static void TryTrimToMaxStoredLines(FileStream stream)
    {
        try
        {
            stream.Seek(0, SeekOrigin.Begin);
            var recentLines = new Queue<string>(MaxStoredLineCount);
            var totalLineCount = 0;
            using (var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true))
            {
                while (reader.ReadLine() is { } line)
                {
                    totalLineCount++;
                    if (recentLines.Count == MaxStoredLineCount)
                    {
                        recentLines.Dequeue();
                    }

                    recentLines.Enqueue(line);
                }
            }

            if (totalLineCount <= MaxStoredLineCount)
            {
                return;
            }

            stream.SetLength(0);
            stream.Seek(0, SeekOrigin.Begin);
            using var writer = new StreamWriter(stream, Utf8NoBom, bufferSize: 4096, leaveOpen: true);
            foreach (var line in recentLines)
            {
                writer.WriteLine(line);
            }
        }
        catch
        {
        }
    }
}
