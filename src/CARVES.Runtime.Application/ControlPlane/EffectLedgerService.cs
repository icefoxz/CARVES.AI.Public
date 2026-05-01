using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class EffectLedgerService
{
    public const string EventSchema = "carves.effect_ledger_event.v0.98-rc.p5";
    public const string SealSchema = "carves.effect_ledger_seal.v0.98-rc.p5";
    public const string AuditIncompleteStopReason = "SC-AUDIT-INCOMPLETE";
    public const string EffectEscalationStopReason = "SC-EFFECT-ESCALATION";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };

    private readonly ControlPlanePaths paths;

    public EffectLedgerService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public string GetRunLedgerPath(string runId)
    {
        return Path.Combine(paths.WorkerExecutionArtifactsRoot, runId, "effect-ledger.jsonl");
    }

    public string GetWorkOrderLedgerPath(string workOrderId)
    {
        return Path.Combine(paths.RuntimeRoot, "work-orders", workOrderId, "effect-ledger.jsonl");
    }

    public EffectLedgerAppendResult AppendEvent(string ledgerPath, EffectLedgerEventDraft draft)
    {
        var ledgerState = ValidateLedger(ledgerPath, requireSeal: false);
        if (!ledgerState.CanWriteBack)
        {
            throw new InvalidOperationException($"{AuditIncompleteStopReason}: {ledgerState.Summary}");
        }

        if (ledgerState.Sealed)
        {
            throw new InvalidOperationException($"{AuditIncompleteStopReason}: effect ledger is already sealed.");
        }

        var effectEscalation = draft.ObservedEffects
            .Where(effect => !draft.DeclaredEffects.Contains(effect, StringComparer.Ordinal))
            .ToArray();
        if (effectEscalation.Length != 0)
        {
            throw new InvalidOperationException(
                $"{EffectEscalationStopReason}: observed effect was not declared ({string.Join(", ", effectEscalation)}).");
        }

        var outputFailure = ValidateOutputs(draft.Outputs);
        if (outputFailure is not null)
        {
            throw new InvalidOperationException($"{AuditIncompleteStopReason}: {outputFailure}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ledgerPath)!);
        var eventIndex = ledgerState.EventCount + 1;
        var ledgerEvent = new EffectLedgerEventRecord
        {
            Schema = string.IsNullOrWhiteSpace(draft.Schema) ? EventSchema : draft.Schema,
            EventId = BuildEventId(draft.EventIdPrefix, draft.StepId, eventIndex),
            PreviousEventHash = ledgerState.LastEventHash,
            WorkOrderId = NormalizeOptional(draft.WorkOrderId),
            TransactionId = NormalizeOptional(draft.TransactionId),
            LeaseId = NormalizeOptional(draft.LeaseId),
            TaskId = NormalizeOptional(draft.TaskId),
            RunId = NormalizeOptional(draft.RunId),
            StepId = draft.StepId,
            Actor = draft.Actor,
            UtteranceHash = NormalizeOptional(draft.UtteranceHash),
            ObjectBindingHash = NormalizeOptional(draft.ObjectBindingHash),
            AdmissionState = NormalizeOptional(draft.AdmissionState),
            TransactionHash = NormalizeOptional(draft.TransactionHash),
            TerminalState = NormalizeOptional(draft.TerminalState),
            TransactionStepIds = draft.TransactionStepIds,
            DeclaredEffects = draft.DeclaredEffects,
            ObservedEffects = draft.ObservedEffects,
            Outputs = draft.Outputs,
            Facts = draft.Facts,
            Verdict = draft.Verdict,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        var payload = JsonSerializer.Serialize(ledgerEvent, JsonOptions);
        File.AppendAllText(ledgerPath, payload + Environment.NewLine);
        return new EffectLedgerAppendResult(ledgerEvent.EventId, ComputeContentHash(payload), ToRepoRelative(ledgerPath));
    }

    public EffectLedgerAppendResult Seal(string ledgerPath, EffectLedgerSealDraft draft)
    {
        return AppendEvent(
            ledgerPath,
            new EffectLedgerEventDraft(
                draft.EventIdPrefix,
                "final_seal",
                draft.Actor,
                ["seal_effect_ledger"],
                ["seal_effect_ledger"],
                [],
                "sealed")
            {
                Schema = SealSchema,
                WorkOrderId = draft.WorkOrderId,
                TransactionId = draft.TransactionId,
                LeaseId = draft.LeaseId,
                TaskId = draft.TaskId,
                RunId = draft.RunId,
                UtteranceHash = draft.UtteranceHash,
                ObjectBindingHash = draft.ObjectBindingHash,
                AdmissionState = draft.AdmissionState,
                TransactionHash = draft.TransactionHash,
                TerminalState = draft.TerminalState,
                TransactionStepIds = draft.TransactionStepIds,
                Facts = draft.Facts,
            });
    }

    public EffectLedgerReplayResult Replay(string ledgerPath)
    {
        return ValidateLedger(ledgerPath, requireSeal: true);
    }

    public EffectLedgerReplayResult ReplayOpen(string ledgerPath)
    {
        return ValidateLedger(ledgerPath, requireSeal: false);
    }

    public EffectLedgerReplayResult ReplayRun(string runId)
    {
        return Replay(GetRunLedgerPath(runId));
    }

    public EffectLedgerReplayResult ReplayWorkOrder(string workOrderId)
    {
        return Replay(GetWorkOrderLedgerPath(workOrderId));
    }

    public string ToRepoRelative(string path)
    {
        return Path.GetRelativePath(paths.RepoRoot, ResolveGovernedFullPath(path)).Replace('\\', '/');
    }

    public EffectLedgerOutput BuildOutput(string kind, string path, string hash)
    {
        return new EffectLedgerOutput(kind, ToRepoRelative(path), hash);
    }

    public string HashFile(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ResolveGovernedFullPath(path)))).ToLowerInvariant();
    }

    public static string ComputeContentHash(string payload)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private EffectLedgerReplayResult ValidateLedger(string ledgerPath, bool requireSeal)
    {
        if (!File.Exists(ledgerPath))
        {
            if (!requireSeal)
            {
                return new EffectLedgerReplayResult(
                    ToRepoRelative(ledgerPath),
                    "open",
                    CanWriteBack: true,
                    Sealed: false,
                    EventCount: 0,
                    LastEventHash: null,
                    WorkOrderId: null,
                    RunId: null,
                    TaskId: null,
                    UtteranceHash: null,
                    ObjectBindingHash: null,
                    AdmissionState: null,
                    LeaseId: null,
                    TransactionHash: null,
                    TerminalState: null,
                    StepEvents: [],
                    EvidenceHashes: new Dictionary<string, string>(),
                    OutputHashes: new Dictionary<string, string>(),
                    EventIds: [],
                    StopReasons: [],
                    Summary: "Effect ledger is open for its first append.");
            }

            return EffectLedgerReplayResult.Broken(
                ToRepoRelative(ledgerPath),
                "missing",
                [AuditIncompleteStopReason],
                "Effect ledger is missing.");
        }

        var lines = File.ReadAllLines(ledgerPath)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length == 0)
        {
            if (!requireSeal)
            {
                return new EffectLedgerReplayResult(
                    ToRepoRelative(ledgerPath),
                    "open",
                    CanWriteBack: true,
                    Sealed: false,
                    EventCount: 0,
                    LastEventHash: null,
                    WorkOrderId: null,
                    RunId: null,
                    TaskId: null,
                    UtteranceHash: null,
                    ObjectBindingHash: null,
                    AdmissionState: null,
                    LeaseId: null,
                    TransactionHash: null,
                    TerminalState: null,
                    StepEvents: [],
                    EvidenceHashes: new Dictionary<string, string>(),
                    OutputHashes: new Dictionary<string, string>(),
                    EventIds: [],
                    StopReasons: [],
                    Summary: "Effect ledger is open for its first append.");
            }

            return EffectLedgerReplayResult.Broken(
                ToRepoRelative(ledgerPath),
                "empty",
                [AuditIncompleteStopReason],
                "Effect ledger is empty.");
        }

        string? previousHash = null;
        var eventIds = new List<string>();
        var stepEvents = new List<string>();
        var evidenceHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var outputHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        var stopReasons = new List<string>();
        var sealedLedger = false;
        string? workOrderId = null;
        string? runId = null;
        string? taskId = null;
        string? utteranceHash = null;
        string? objectBindingHash = null;
        string? admissionState = null;
        string? leaseId = null;
        string? transactionHash = null;
        string? terminalState = null;

        for (var index = 0; index < lines.Length; index++)
        {
            EffectLedgerEventRecord? ledgerEvent;
            try
            {
                ledgerEvent = JsonSerializer.Deserialize<EffectLedgerEventRecord>(lines[index], JsonOptions);
            }
            catch (JsonException)
            {
                return EffectLedgerReplayResult.Broken(
                    ToRepoRelative(ledgerPath),
                    "broken",
                    [AuditIncompleteStopReason],
                    $"Effect ledger event {index + 1} is not valid JSON.");
            }

            if (ledgerEvent is null || string.IsNullOrWhiteSpace(ledgerEvent.EventId))
            {
                return EffectLedgerReplayResult.Broken(
                    ToRepoRelative(ledgerPath),
                    "broken",
                    [AuditIncompleteStopReason],
                    $"Effect ledger event {index + 1} is missing event_id.");
            }

            if (!string.Equals(ledgerEvent.PreviousEventHash, previousHash, StringComparison.Ordinal))
            {
                return EffectLedgerReplayResult.Broken(
                    ToRepoRelative(ledgerPath),
                    "broken",
                    [AuditIncompleteStopReason],
                    $"Effect ledger hash chain broke at event {ledgerEvent.EventId}.");
            }

            var effectEscalation = ledgerEvent.ObservedEffects
                .Where(effect => !ledgerEvent.DeclaredEffects.Contains(effect, StringComparer.Ordinal))
                .ToArray();
            if (effectEscalation.Length != 0)
            {
                return EffectLedgerReplayResult.Broken(
                    ToRepoRelative(ledgerPath),
                    "broken",
                    [EffectEscalationStopReason],
                    $"Effect ledger observed undeclared effects at event {ledgerEvent.EventId}: {string.Join(", ", effectEscalation)}.");
            }

            var outputFailure = ValidateOutputs(ledgerEvent.Outputs);
            if (outputFailure is not null)
            {
                return EffectLedgerReplayResult.Broken(
                    ToRepoRelative(ledgerPath),
                    "broken",
                    [AuditIncompleteStopReason],
                    outputFailure);
            }

            eventIds.Add(ledgerEvent.EventId);
            if (!string.IsNullOrWhiteSpace(ledgerEvent.StepId))
            {
                stepEvents.Add(ledgerEvent.StepId);
            }

            foreach (var stepId in ledgerEvent.TransactionStepIds)
            {
                if (!stepEvents.Contains(stepId, StringComparer.Ordinal))
                {
                    stepEvents.Add(stepId);
                }
            }

            foreach (var output in ledgerEvent.Outputs)
            {
                outputHashes[output.Kind] = output.Hash;
                if (output.Kind.StartsWith("evidence", StringComparison.Ordinal)
                    || output.Kind.Contains("evidence", StringComparison.Ordinal)
                    || output.Kind.Contains("boundary", StringComparison.Ordinal)
                    || output.Kind.Contains("result", StringComparison.Ordinal))
                {
                    evidenceHashes[output.Kind] = output.Hash;
                }
            }

            workOrderId ??= NormalizeOptional(ledgerEvent.WorkOrderId);
            runId ??= NormalizeOptional(ledgerEvent.RunId);
            taskId ??= NormalizeOptional(ledgerEvent.TaskId);
            utteranceHash ??= NormalizeOptional(ledgerEvent.UtteranceHash);
            objectBindingHash ??= NormalizeOptional(ledgerEvent.ObjectBindingHash);
            admissionState ??= NormalizeOptional(ledgerEvent.AdmissionState);
            leaseId ??= NormalizeOptional(ledgerEvent.LeaseId);
            transactionHash ??= NormalizeOptional(ledgerEvent.TransactionHash);
            terminalState = NormalizeOptional(ledgerEvent.TerminalState) ?? terminalState;
            sealedLedger = sealedLedger
                || string.Equals(ledgerEvent.Schema, SealSchema, StringComparison.Ordinal)
                || string.Equals(ledgerEvent.StepId, "final_seal", StringComparison.Ordinal);
            previousHash = ComputeContentHash(lines[index]);
        }

        if (requireSeal && !sealedLedger)
        {
            stopReasons.Add(AuditIncompleteStopReason);
            return new EffectLedgerReplayResult(
                ToRepoRelative(ledgerPath),
                "broken",
                CanWriteBack: false,
                Sealed: false,
                EventCount: lines.Length,
                LastEventHash: previousHash,
                WorkOrderId: workOrderId,
                RunId: runId,
                TaskId: taskId,
                UtteranceHash: utteranceHash,
                ObjectBindingHash: objectBindingHash,
                AdmissionState: admissionState,
                LeaseId: leaseId,
                TransactionHash: transactionHash,
                TerminalState: terminalState,
                StepEvents: stepEvents,
                EvidenceHashes: evidenceHashes,
                OutputHashes: outputHashes,
                EventIds: eventIds,
                StopReasons: stopReasons,
                Summary: "Effect ledger is missing its final seal event.");
        }

        return new EffectLedgerReplayResult(
            ToRepoRelative(ledgerPath),
            "verified",
            CanWriteBack: true,
            Sealed: sealedLedger,
            EventCount: lines.Length,
            LastEventHash: previousHash,
            WorkOrderId: workOrderId,
            RunId: runId,
            TaskId: taskId,
            UtteranceHash: utteranceHash,
            ObjectBindingHash: objectBindingHash,
            AdmissionState: admissionState,
            LeaseId: leaseId,
            TransactionHash: transactionHash,
            TerminalState: terminalState,
            StepEvents: stepEvents,
            EvidenceHashes: evidenceHashes,
            OutputHashes: outputHashes,
            EventIds: eventIds,
            StopReasons: [],
            Summary: "Effect ledger replay verified.");
    }

    private string? ValidateOutputs(IReadOnlyList<EffectLedgerOutput> outputs)
    {
        foreach (var output in outputs)
        {
            if (string.IsNullOrWhiteSpace(output.Path) || string.IsNullOrWhiteSpace(output.Hash))
            {
                return $"Effect ledger output '{output.Kind}' is missing path or hash.";
            }

            string fullPath;
            try
            {
                fullPath = ResolveGovernedFullPath(output.Path);
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message;
            }

            if (!File.Exists(fullPath))
            {
                return $"Effect ledger output '{output.Kind}' is missing at {output.Path}.";
            }

            var actualHash = HashFile(fullPath);
            if (!string.Equals(actualHash, output.Hash, StringComparison.Ordinal))
            {
                return $"Effect ledger output '{output.Kind}' hash mismatch at {output.Path}.";
            }
        }

        return null;
    }

    private string ResolveGovernedFullPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Effect ledger path is empty.");
        }

        var repoRoot = Path.GetFullPath(paths.RepoRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(paths.RepoRoot, path.Replace('/', Path.DirectorySeparatorChar)));
        var repoPrefix = repoRoot + Path.DirectorySeparatorChar;
        if (!string.Equals(fullPath, repoRoot, StringComparison.Ordinal)
            && !fullPath.StartsWith(repoPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Effect ledger path '{path}' escapes the repository root.");
        }

        var relativePath = Path.GetRelativePath(repoRoot, fullPath).Replace('\\', '/');
        if (!IsWithinGovernedArtifactRoot(relativePath))
        {
            throw new InvalidOperationException(
                $"Effect ledger path '{relativePath}' is outside governed artifact roots.");
        }

        return fullPath;
    }

    private static bool IsWithinGovernedArtifactRoot(string relativePath)
    {
        return relativePath.StartsWith(".ai/artifacts/", StringComparison.Ordinal)
            || relativePath.StartsWith(".ai/runtime/", StringComparison.Ordinal)
            || relativePath.StartsWith(".ai/execution/", StringComparison.Ordinal)
            || relativePath.StartsWith(".ai/tasks/", StringComparison.Ordinal)
            || relativePath.StartsWith(".ai/memory/", StringComparison.Ordinal)
            || relativePath.StartsWith(".ai/evidence/", StringComparison.Ordinal)
            || relativePath.StartsWith(".ai/codegraph/", StringComparison.Ordinal)
            || relativePath.StartsWith(".ai/failures/", StringComparison.Ordinal)
            || relativePath.StartsWith(".carves-platform/", StringComparison.Ordinal);
    }

    private static string BuildEventId(string eventIdPrefix, string stepId, int eventIndex)
    {
        var prefix = string.IsNullOrWhiteSpace(eventIdPrefix) ? "EV" : eventIdPrefix.Trim();
        var normalizedStep = new string(stepId
            .Select(static value => char.IsLetterOrDigit(value) ? char.ToUpperInvariant(value) : '-')
            .ToArray())
            .Trim('-');
        return $"{prefix}-{normalizedStep}-{eventIndex:000}";
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record EffectLedgerEventDraft(
    string EventIdPrefix,
    string StepId,
    string Actor,
    IReadOnlyList<string> DeclaredEffects,
    IReadOnlyList<string> ObservedEffects,
    IReadOnlyList<EffectLedgerOutput> Outputs,
    string Verdict)
{
    public string? Schema { get; init; }

    public string? WorkOrderId { get; init; }

    public string? TransactionId { get; init; }

    public string? LeaseId { get; init; }

    public string? TaskId { get; init; }

    public string? RunId { get; init; }

    public string? UtteranceHash { get; init; }

    public string? ObjectBindingHash { get; init; }

    public string? AdmissionState { get; init; }

    public string? TransactionHash { get; init; }

    public string? TerminalState { get; init; }

    public IReadOnlyList<string> TransactionStepIds { get; init; } = [];

    public IReadOnlyDictionary<string, string?> Facts { get; init; } = new Dictionary<string, string?>();
}

public sealed record EffectLedgerSealDraft(
    string EventIdPrefix,
    string Actor)
{
    public string? WorkOrderId { get; init; }

    public string? TransactionId { get; init; }

    public string? LeaseId { get; init; }

    public string? TaskId { get; init; }

    public string? RunId { get; init; }

    public string? UtteranceHash { get; init; }

    public string? ObjectBindingHash { get; init; }

    public string? AdmissionState { get; init; }

    public string? TransactionHash { get; init; }

    public string? TerminalState { get; init; }

    public IReadOnlyList<string> TransactionStepIds { get; init; } = [];

    public IReadOnlyDictionary<string, string?> Facts { get; init; } = new Dictionary<string, string?>();
}

public sealed class EffectLedgerEventRecord
{
    public string Schema { get; init; } = EffectLedgerService.EventSchema;

    public string EventId { get; init; } = string.Empty;

    public string? PreviousEventHash { get; init; }

    public string? WorkOrderId { get; init; }

    public string? TransactionId { get; init; }

    public string? LeaseId { get; init; }

    public string? TaskId { get; init; }

    public string? RunId { get; init; }

    public string StepId { get; init; } = string.Empty;

    public string Actor { get; init; } = string.Empty;

    public string? UtteranceHash { get; init; }

    public string? ObjectBindingHash { get; init; }

    public string? AdmissionState { get; init; }

    public string? TransactionHash { get; init; }

    public string? TerminalState { get; init; }

    public IReadOnlyList<string> TransactionStepIds { get; init; } = [];

    public IReadOnlyList<string> DeclaredEffects { get; init; } = [];

    public IReadOnlyList<string> ObservedEffects { get; init; } = [];

    public IReadOnlyList<EffectLedgerOutput> Outputs { get; init; } = [];

    public IReadOnlyDictionary<string, string?> Facts { get; init; } = new Dictionary<string, string?>();

    public string Verdict { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record EffectLedgerOutput(string Kind, string Path, string Hash);

public sealed record EffectLedgerAppendResult(string EventId, string EventHash, string LedgerPath);

public sealed record EffectLedgerReplayResult(
    string LedgerPath,
    string ReplayState,
    bool CanWriteBack,
    bool Sealed,
    int EventCount,
    string? LastEventHash,
    string? WorkOrderId,
    string? RunId,
    string? TaskId,
    string? UtteranceHash,
    string? ObjectBindingHash,
    string? AdmissionState,
    string? LeaseId,
    string? TransactionHash,
    string? TerminalState,
    IReadOnlyList<string> StepEvents,
    IReadOnlyDictionary<string, string> EvidenceHashes,
    IReadOnlyDictionary<string, string> OutputHashes,
    IReadOnlyList<string> EventIds,
    IReadOnlyList<string> StopReasons,
    string Summary)
{
    public static EffectLedgerReplayResult Broken(
        string ledgerPath,
        string replayState,
        IReadOnlyList<string> stopReasons,
        string summary)
    {
        return new EffectLedgerReplayResult(
            ledgerPath,
            replayState,
            CanWriteBack: false,
            Sealed: false,
            EventCount: 0,
            LastEventHash: null,
            WorkOrderId: null,
            RunId: null,
            TaskId: null,
            UtteranceHash: null,
            ObjectBindingHash: null,
            AdmissionState: null,
            LeaseId: null,
            TransactionHash: null,
            TerminalState: null,
            StepEvents: [],
            EvidenceHashes: new Dictionary<string, string>(),
            OutputHashes: new Dictionary<string, string>(),
            EventIds: [],
            StopReasons: stopReasons,
            Summary: summary);
    }
}
