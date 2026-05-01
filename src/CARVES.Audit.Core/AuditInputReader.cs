using System.Text;
using System.Text.Json;

namespace Carves.Audit.Core;

public sealed class AuditInputReader
{
    public const int CurrentGuardDecisionSchemaVersion = 1;
    public const int MaxGuardLineCount = 1000;
    public const int MaxGuardRecordByteCount = 128 * 1024;
    public const string DefaultGuardDecisionPath = ".ai/runtime/guard/decisions.jsonl";
    public const string DefaultGuardPolicyPath = ".ai/guard-policy.json";
    public const string DefaultHandoffPacketPath = ".ai/handoff/handoff.json";
    public const string HandoffPacketSchemaVersion = "carves-continuity-handoff.v1";

    public AuditInputSnapshot Read(AuditInputOptions options)
    {
        IReadOnlyList<string> handoffPaths = options.HandoffPacketPaths.Count == 0 && !options.HandoffPacketPathsExplicit
            ? [DefaultHandoffPacketPath]
            : options.HandoffPacketPaths;

        return new AuditInputSnapshot(
            ReadGuardInput(options),
            handoffPaths
                .Select(path => ReadHandoffInput(options.WorkingDirectory, path, options.HandoffPacketPathsExplicit))
                .ToArray());
    }

    private static GuardAuditInput ReadGuardInput(AuditInputOptions options)
    {
        var path = ResolvePath(options.WorkingDirectory, options.GuardDecisionPath);
        if (!File.Exists(path))
        {
            var fatal = options.GuardDecisionPathExplicit;
            return new GuardAuditInput(
                path,
                fatal ? "input_error" : "missing",
                fatal,
                [],
                new GuardDecisionDiagnostics(
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    MaxGuardLineCount,
                    MaxGuardRecordByteCount,
                    IsDegraded: false));
        }

        try
        {
            var recentLines = ReadRecentLines(path, MaxGuardLineCount);
            var effectiveLines = recentLines.Lines;
            var records = new List<GuardDecisionRecord>();
            var emptyLineCount = 0;
            var malformedRecordCount = 0;
            var futureVersionRecordCount = 0;
            var oversizedRecordCount = 0;

            foreach (var line in effectiveLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    emptyLineCount++;
                    continue;
                }

                if (Encoding.UTF8.GetByteCount(line) > MaxGuardRecordByteCount)
                {
                    oversizedRecordCount++;
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(line);
                    if (!TryReadGuardSchemaVersion(document.RootElement, out var schemaVersion))
                    {
                        malformedRecordCount++;
                        continue;
                    }

                    if (schemaVersion > CurrentGuardDecisionSchemaVersion)
                    {
                        futureVersionRecordCount++;
                        continue;
                    }

                    if (schemaVersion != CurrentGuardDecisionSchemaVersion)
                    {
                        malformedRecordCount++;
                        continue;
                    }

                    var record = JsonSerializer.Deserialize<GuardDecisionRecord>(line, AuditJsonContracts.JsonOptions);
                    if (IsUsableGuardRecord(record))
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

            var skippedRecordCount = malformedRecordCount + futureVersionRecordCount + oversizedRecordCount;
            var isDegraded = skippedRecordCount > 0 || recentLines.TotalLineCount > MaxGuardLineCount;
            var fatal = options.GuardDecisionPathExplicit && records.Count == 0 && skippedRecordCount > 0;
            return new GuardAuditInput(
                path,
                fatal ? "input_error" : isDegraded ? "degraded" : "loaded",
                fatal,
                records,
                new GuardDecisionDiagnostics(
                    recentLines.TotalLineCount,
                    effectiveLines.Length,
                    emptyLineCount,
                    records.Count,
                    skippedRecordCount,
                    malformedRecordCount,
                    futureVersionRecordCount,
                    oversizedRecordCount,
                    MaxGuardLineCount,
                    MaxGuardRecordByteCount,
                    isDegraded));
        }
        catch (IOException exception)
        {
            return GuardInputError(path, exception.Message, options.GuardDecisionPathExplicit);
        }
        catch (UnauthorizedAccessException exception)
        {
            return GuardInputError(path, exception.Message, options.GuardDecisionPathExplicit);
        }
    }

    private static HandoffAuditInput ReadHandoffInput(string workingDirectory, string packetPath, bool explicitPath)
    {
        var path = ResolvePath(workingDirectory, packetPath);
        if (!File.Exists(path))
        {
            return explicitPath
                ? HandoffInputError(path, "handoff_packet_missing", "Explicit Handoff packet path does not exist.", fatal: true)
                : new HandoffAuditInput(
                    path,
                    "missing",
                    IsFatal: false,
                    Packet: null,
                    Diagnostics:
                    [
                        new AuditInputDiagnostic(
                            "warning",
                            "handoff_default_missing",
                            $"Default Handoff packet was not found at {DefaultHandoffPacketPath}.",
                            path),
                    ]);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var schemaVersion = ReadString(root, "schema_version");
            if (!string.Equals(schemaVersion, HandoffPacketSchemaVersion, StringComparison.Ordinal))
            {
                return HandoffInputError(path, "handoff_unsupported_schema", "Handoff packet schema is not supported.", explicitPath);
            }

            var handoffId = ReadString(root, "handoff_id");
            if (string.IsNullOrWhiteSpace(handoffId))
            {
                return HandoffInputError(path, "handoff_missing_id", "Handoff packet is missing handoff_id.", explicitPath);
            }

            if (!DateTimeOffset.TryParse(ReadString(root, "created_at_utc"), out var createdAtUtc))
            {
                return HandoffInputError(path, "handoff_invalid_created_at", "Handoff packet has an invalid created_at_utc.", explicitPath);
            }

            var packet = new HandoffPacketRecord(
                schemaVersion!,
                handoffId!,
                createdAtUtc,
                ReadString(root, "resume_status"),
                ReadString(root, "current_objective"),
                ReadString(root, "confidence"),
                ReadArrayCount(root, "remaining_work"),
                CountCompletedFactsWithEvidence(root),
                ReadArrayCount(root, "blocked_reasons"),
                ReadArrayCount(root, "must_not_repeat"),
                ReadArrayCount(root, "decision_refs"),
                ReadArrayCount(root, "context_refs"),
                ReadArrayCount(root, "evidence_refs"),
                ReadRefs(root, "evidence_refs"),
                ReadRefs(root, "context_refs"));

            return new HandoffAuditInput(path, "loaded", IsFatal: false, packet, []);
        }
        catch (JsonException)
        {
            return HandoffInputError(path, "handoff_malformed_json", "Handoff packet is not valid JSON.", explicitPath);
        }
        catch (IOException exception)
        {
            return HandoffInputError(path, "handoff_read_failed", exception.Message, explicitPath);
        }
        catch (UnauthorizedAccessException exception)
        {
            return HandoffInputError(path, "handoff_read_failed", exception.Message, explicitPath);
        }
    }

    private static RecentLines ReadRecentLines(string path, int maxLineCount)
    {
        var recentLines = new Queue<string>(maxLineCount);
        var totalLineCount = 0;
        foreach (var line in File.ReadLines(path))
        {
            totalLineCount++;
            if (recentLines.Count == maxLineCount)
            {
                recentLines.Dequeue();
            }

            recentLines.Enqueue(line);
        }

        return new RecentLines(totalLineCount, recentLines.ToArray());
    }

    private static GuardAuditInput GuardInputError(string path, string message, bool fatal)
    {
        return new GuardAuditInput(
            path,
            fatal ? "input_error" : "missing",
            fatal,
            [],
            new GuardDecisionDiagnostics(
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                MaxGuardLineCount,
                MaxGuardRecordByteCount,
                IsDegraded: false));
    }

    private static HandoffAuditInput HandoffInputError(string path, string code, string message, bool fatal)
    {
        return new HandoffAuditInput(
            path,
            fatal ? "input_error" : "degraded",
            IsFatal: fatal,
            Packet: null,
            Diagnostics: [new AuditInputDiagnostic(fatal ? "error" : "warning", code, message, path)]);
    }

    private static string ResolvePath(string workingDirectory, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(workingDirectory, path));
    }

    private static bool TryReadGuardSchemaVersion(JsonElement root, out int schemaVersion)
    {
        schemaVersion = 0;
        return root.TryGetProperty("schema_version", out var schemaVersionElement)
               && schemaVersionElement.ValueKind == JsonValueKind.Number
               && schemaVersionElement.TryGetInt32(out schemaVersion);
    }

    private static bool IsUsableGuardRecord(GuardDecisionRecord? record)
    {
        return record is not null
               && record.SchemaVersion == CurrentGuardDecisionSchemaVersion
               && !string.IsNullOrWhiteSpace(record.RunId)
               && record.RecordedAtUtc != default
               && !string.IsNullOrWhiteSpace(record.Outcome)
               && !string.IsNullOrWhiteSpace(record.PolicyId)
               && !string.IsNullOrWhiteSpace(record.Source)
               && record.ChangedFiles is not null
               && record.Violations is not null
               && record.Warnings is not null
               && record.EvidenceRefs is not null;
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int ReadArrayCount(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array
            ? property.GetArrayLength()
            : 0;
    }

    private static int CountCompletedFactsWithEvidence(JsonElement root)
    {
        if (!root.TryGetProperty("completed_facts", out var facts) || facts.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var count = 0;
        foreach (var fact in facts.EnumerateArray())
        {
            if (fact.ValueKind == JsonValueKind.Object
                && fact.TryGetProperty("evidence_refs", out var evidenceRefs)
                && evidenceRefs.ValueKind == JsonValueKind.Array
                && evidenceRefs.GetArrayLength() > 0)
            {
                count++;
            }
        }

        return count;
    }

    private static IReadOnlyList<string> ReadRefs(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var refs = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                refs.Add(item.GetString()!);
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("ref", out var refElement)
                && refElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(refElement.GetString()))
            {
                refs.Add(refElement.GetString()!);
            }
        }

        return refs;
    }

    private sealed record RecentLines(int TotalLineCount, string[] Lines);
}
