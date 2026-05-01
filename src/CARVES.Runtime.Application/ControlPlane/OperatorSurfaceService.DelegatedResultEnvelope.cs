using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    private static readonly JsonSerializerOptions DelegatedResultEnvelopeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private DelegatedResultSubmissionOutcome SubmitDelegatedResultEnvelope(
        TaskRunReport? report,
        ExecutionRun executionRun,
        bool dryRun)
    {
        var resultEnvelopePath = WriteDelegatedResultEnvelope(report, executionRun, dryRun);
        var safetyGate = ReadDelegatedSafetyGate(report);
        if (dryRun || report is null || string.IsNullOrWhiteSpace(resultEnvelopePath))
        {
            return new DelegatedResultSubmissionOutcome(
                resultEnvelopePath,
                IngestionOutcome: null,
                Status: dryRun ? "dry_run_not_submitted" : "result_envelope_not_written",
                SafetyGate: safetyGate);
        }

        if (!CanAutoSubmitDelegatedResult(report, safetyGate))
        {
            return new DelegatedResultSubmissionOutcome(
                resultEnvelopePath,
                IngestionOutcome: null,
                Status: ResolveNotSubmittedStatus(safetyGate),
                SafetyGate: safetyGate);
        }

        var ingestionOutcome = resultIngestionService.Ingest(report.TaskId);
        return new DelegatedResultSubmissionOutcome(
            resultEnvelopePath,
            ingestionOutcome,
            ingestionOutcome.AlreadyApplied ? "already_applied" : "ingested",
            safetyGate);
    }

    private string? WriteDelegatedResultEnvelope(TaskRunReport? report, ExecutionRun executionRun, bool dryRun)
    {
        if (dryRun || report is null || !report.WorkerExecution.Succeeded)
        {
            return null;
        }

        var workerArtifact = artifactRepository.TryLoadWorkerExecutionArtifact(report.TaskId);
        if (workerArtifact is null || workerArtifact.Evidence.EvidenceCompleteness == ExecutionEvidenceCompleteness.Missing)
        {
            return null;
        }

        var resultPath = ResolveCandidateResultPath(report);
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);

        var changedFiles = ResolveChangedFiles(report).ToArray();
        var commandsRun = ResolveCommandsRun(report).ToArray();
        var durationSeconds = Math.Max(1, (int)Math.Ceiling((report.WorkerExecution.CompletedAt - report.WorkerExecution.StartedAt).TotalSeconds));
        var envelope = new ResultEnvelope
        {
            TaskId = report.TaskId,
            ExecutionRunId = executionRun.RunId,
            ExecutionEvidencePath = workerArtifact.Evidence.EvidencePath,
            CompletedStepCount = executionRun.Steps.Count,
            TotalStepCount = executionRun.Steps.Count,
            Status = "success",
            Changes = new ResultEnvelopeChanges
            {
                FilesModified = changedFiles,
                LinesChanged = report.Patch.TotalLinesChanged,
            },
            Validation = new ResultEnvelopeValidation
            {
                CommandsRun = commandsRun,
                Build = ResolveValidationStatus(commandsRun, "build", report),
                Tests = ResolveValidationStatus(commandsRun, "test", report),
            },
            Result = new ResultEnvelopeOutcome
            {
                StopReason = "acceptance_satisfied",
            },
            Next = new ResultEnvelopeNextAction
            {
                Suggested = "submit_result",
            },
            Telemetry = new ExecutionTelemetry
            {
                FilesChanged = changedFiles.Length,
                LinesChanged = report.Patch.TotalLinesChanged,
                RetryCount = 1,
                FailureCount = 0,
                DurationSeconds = durationSeconds,
                ObservedPaths = changedFiles,
                ChangeKinds = new ExecutionPathClassifier().ClassifyMany(changedFiles),
                BudgetExceeded = false,
                Summary = "Host generated delegated result envelope from worker execution packet and evidence.",
            },
        };

        File.WriteAllText(resultPath, JsonSerializer.Serialize(envelope, DelegatedResultEnvelopeJsonOptions));
        return ToRepoRelativePath(resultPath);
    }

    private DelegatedSafetyGateReadback ReadDelegatedSafetyGate(TaskRunReport? report)
    {
        if (report is null)
        {
            return DelegatedSafetyGateReadback.NotEvaluated;
        }

        var safetyArtifact = artifactRepository.TryLoadSafetyArtifact(report.TaskId);
        if (safetyArtifact is null)
        {
            return new DelegatedSafetyGateReadback(
                ArtifactPresent: false,
                ConsistentWithReport: false,
                Status: "safety_artifact_missing",
                Allowed: false,
                Issues: Array.Empty<string>());
        }

        var issues = safetyArtifact.Decision.Issues
            .Select(issue => $"{issue.Code}: {issue.Message}")
            .ToArray();
        if (!string.Equals(safetyArtifact.Decision.TaskId, report.TaskId, StringComparison.Ordinal))
        {
            return new DelegatedSafetyGateReadback(
                ArtifactPresent: true,
                ConsistentWithReport: false,
                Status: "safety_artifact_task_mismatch",
                Allowed: false,
                Issues: issues);
        }

        if (safetyArtifact.Decision.Outcome != report.SafetyDecision.Outcome)
        {
            return new DelegatedSafetyGateReadback(
                ArtifactPresent: true,
                ConsistentWithReport: false,
                Status: "safety_artifact_mismatch",
                Allowed: false,
                Issues: issues);
        }

        return new DelegatedSafetyGateReadback(
            ArtifactPresent: true,
            ConsistentWithReport: true,
            Status: FormatSafetyStatus(safetyArtifact.Decision.Outcome),
            Allowed: safetyArtifact.Decision.Allowed,
            Issues: issues);
    }

    private static bool CanAutoSubmitDelegatedResult(TaskRunReport report, DelegatedSafetyGateReadback safetyGate)
    {
        return report.WorkerExecution.Succeeded
               && report.Validation.Passed
               && safetyGate.ArtifactPresent
               && safetyGate.ConsistentWithReport
               && safetyGate.Allowed;
    }

    private static string ResolveNotSubmittedStatus(DelegatedSafetyGateReadback safetyGate)
    {
        if (!safetyGate.ArtifactPresent || !safetyGate.ConsistentWithReport || !safetyGate.Allowed)
        {
            return safetyGate.Status;
        }

        return "not_auto_submitted";
    }

    private static string FormatSafetyStatus(SafetyOutcome outcome)
    {
        return outcome switch
        {
            SafetyOutcome.Allow => "allow",
            SafetyOutcome.NeedsReview => "needs_review",
            SafetyOutcome.Blocked => "blocked",
            _ => outcome.ToString().ToLowerInvariant(),
        };
    }

    private string ResolveCandidateResultPath(TaskRunReport report)
    {
        var channel = report.Request.ExecutionRequest?.WorkerExecutionPacket.ResultSubmission.CandidateResultChannel;
        if (IsExpectedCandidateResultChannel(report.TaskId, channel))
        {
            return Path.Combine(paths.RepoRoot, channel!.Replace('/', Path.DirectorySeparatorChar));
        }

        return Path.Combine(paths.AiRoot, "execution", report.TaskId, "result.json");
    }

    private static bool IsExpectedCandidateResultChannel(string taskId, string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel))
        {
            return false;
        }

        var normalized = channel.Trim().Trim('`').Replace('\\', '/');
        return string.Equals(normalized, $".ai/execution/{taskId}/result.json", StringComparison.Ordinal);
    }

    private static IEnumerable<string> ResolveChangedFiles(TaskRunReport report)
    {
        return report.Patch.Paths
            .Concat(report.WorkerExecution.ChangedFiles)
            .Concat(report.WorkerExecution.ObservedChangedFiles)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static IEnumerable<string> ResolveCommandsRun(TaskRunReport report)
    {
        return report.WorkerExecution.CommandTrace
            .Concat(report.Validation.CommandResults)
            .Select(item => string.Join(' ', item.Command))
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.Ordinal);
    }

    private static string ResolveValidationStatus(IReadOnlyCollection<string> commandsRun, string keyword, TaskRunReport report)
    {
        if (!commandsRun.Any(command => command.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        {
            return "not_run";
        }

        var matchingResults = report.WorkerExecution.CommandTrace
            .Concat(report.Validation.CommandResults)
            .Where(item =>
                item.Command.Any(part => part.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || item.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matchingResults.Length > 0 && matchingResults.All(item => item.ExitCode == 0)
            ? "success"
            : "failed";
    }

    private string ToRepoRelativePath(string absolutePath)
    {
        return Path.GetRelativePath(paths.RepoRoot, absolutePath)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private sealed record DelegatedResultSubmissionOutcome(
        string? ResultEnvelopePath,
        ResultIngestionOutcome? IngestionOutcome,
        string Status,
        DelegatedSafetyGateReadback SafetyGate);

    private sealed record DelegatedSafetyGateReadback(
        bool ArtifactPresent,
        bool ConsistentWithReport,
        string Status,
        bool Allowed,
        IReadOnlyList<string> Issues)
    {
        public static DelegatedSafetyGateReadback NotEvaluated { get; } = new(
            ArtifactPresent: false,
            ConsistentWithReport: false,
            Status: "not_evaluated",
            Allowed: false,
            Issues: Array.Empty<string>());
    }
}
