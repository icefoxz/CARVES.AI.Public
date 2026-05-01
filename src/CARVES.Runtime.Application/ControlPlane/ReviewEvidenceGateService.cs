using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class ReviewEvidenceGateService
{
    public ReviewEvidenceAssessment EvaluateBeforeWriteback(
        TaskNode task,
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact)
    {
        return Evaluate(task, reviewArtifact, workerArtifact, writeback: null, writebackProjection: null, includeWritebackDerivedRequirements: false);
    }

    public ReviewEvidenceAssessment EvaluateProjectedAfterWriteback(
        TaskNode task,
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact,
        ReviewWritebackEvidenceProjection writebackProjection)
    {
        return Evaluate(task, reviewArtifact, workerArtifact, writeback: null, writebackProjection, includeWritebackDerivedRequirements: true);
    }

    public ReviewEvidenceAssessment EvaluateAfterWriteback(
        TaskNode task,
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact,
        ReviewWritebackRecord? writeback)
    {
        return Evaluate(task, reviewArtifact, workerArtifact, writeback, writebackProjection: null, includeWritebackDerivedRequirements: true);
    }

    private static ReviewEvidenceAssessment Evaluate(
        TaskNode task,
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact,
        ReviewWritebackRecord? writeback,
        ReviewWritebackEvidenceProjection? writebackProjection,
        bool includeWritebackDerivedRequirements)
    {
        var requirements = task.AcceptanceContract?.EvidenceRequired ?? Array.Empty<AcceptanceContractEvidenceRequirement>();
        var missing = new List<ReviewEvidenceGap>();

        if (IsNullWorkerDiagnostic(workerArtifact))
        {
            missing.Add(new ReviewEvidenceGap(
                "null_worker_diagnostic_only",
                "null_worker result is diagnostic-only and cannot satisfy final review approval.",
                "Re-run the task with a real worker backend or route the intended truth mutation through a separate governed host action instead of approving completion from null_worker output."));
        }

        if (requirements.Count == 0 && !HasConcreteWorkerEvidence(workerArtifact))
        {
            missing.Add(new ReviewEvidenceGap(
                "worker_execution_evidence",
                "No non-diagnostic worker execution evidence is recorded for this review.",
                "Capture concrete worker execution evidence before final approval, or keep the task in review as a provisional/manual decision."));
        }

        foreach (var requirement in requirements)
        {
            var normalizedType = Normalize(requirement.Type);
            if (!includeWritebackDerivedRequirements && IsWritebackDerivedRequirement(normalizedType))
            {
                continue;
            }

            if (IsSatisfied(normalizedType, reviewArtifact, workerArtifact, writeback, writebackProjection))
            {
                continue;
            }

            missing.Add(new ReviewEvidenceGap(
                requirement.Type,
                requirement.Description,
                BuildFollowUpAction(normalizedType, requirement.Description)));
        }

        return missing.Count == 0
            ? ReviewEvidenceAssessment.Satisfied
            : new ReviewEvidenceAssessment(missing);
    }

    private static bool IsSatisfied(
        string normalizedType,
        PlannerReviewArtifact reviewArtifact,
        WorkerExecutionArtifact? workerArtifact,
        ReviewWritebackRecord? writeback,
        ReviewWritebackEvidenceProjection? writebackProjection)
    {
        var evidence = workerArtifact?.Evidence;
        return normalizedType switch
        {
            "validation" or "validation_passed" => reviewArtifact.ValidationPassed,
            "validation_evidence" => reviewArtifact.ValidationEvidence.Count > 0,
            "manual_review_note" or "review_note" or "review_reason" => HasManualReviewNote(reviewArtifact),
            "worker_execution_evidence" or "worker_output" => HasConcreteWorkerEvidence(workerArtifact),
            "memory_write" => HasMemoryTruthWrite(writeback, writebackProjection),
            "host_routed_truth_write_record" or "authoritative_truth_record" => HasHostRoutedTruthWriteRecord(writeback, writebackProjection),
            "patch" => evidence is not null
                       && (!string.IsNullOrWhiteSpace(evidence.PatchRef)
                           || !string.IsNullOrWhiteSpace(evidence.PatchHash)
                           || evidence.FilesWritten.Count > 0),
            "command_log" => evidence is not null
                             && (!string.IsNullOrWhiteSpace(evidence.CommandLogRef)
                                 || evidence.CommandsExecuted.Count > 0),
            "command_trace" => evidence is not null
                               && (!string.IsNullOrWhiteSpace(evidence.CommandTraceHash)
                                   || evidence.CommandsExecuted.Count > 0),
            "build_output" => evidence is not null && !string.IsNullOrWhiteSpace(evidence.BuildOutputRef),
            "test_output" => evidence is not null && !string.IsNullOrWhiteSpace(evidence.TestOutputRef),
            "files_written" or "changed_files" => evidence is not null && evidence.FilesWritten.Count > 0,
            "worktree" => evidence is not null && !string.IsNullOrWhiteSpace(evidence.WorktreePath),
            "result_commit" => !string.IsNullOrWhiteSpace(writeback?.ResultCommit)
                               || writebackProjection?.WillCaptureResultCommit == true,
            "writeback" => writeback?.Applied == true
                           || writebackProjection?.WillApply == true,
            _ => false,
        };
    }

    private static bool IsWritebackDerivedRequirement(string normalizedType)
    {
        return normalizedType is "memory_write" or "host_routed_truth_write_record" or "authoritative_truth_record" or "result_commit" or "writeback";
    }

    private static string BuildFollowUpAction(string normalizedType, string? description)
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            return $"Capture evidence for '{description.Trim()}' before final acceptance.";
        }

        return normalizedType switch
        {
            "validation" or "validation_passed" => "Re-run validation and capture a passing review artifact before final acceptance.",
            "validation_evidence" => "Persist explicit validation evidence before final acceptance.",
            "manual_review_note" or "review_note" or "review_reason" => "Record a concrete review note on the planner review artifact before final acceptance.",
            "worker_execution_evidence" or "worker_output" => "Capture non-diagnostic worker execution evidence before final acceptance; host-routed writeback cannot substitute for worker output proof.",
            "memory_write" => "Route the memory update through host-owned review writeback and capture the resulting truth-write record before final acceptance.",
            "host_routed_truth_write_record" or "authoritative_truth_record" => "Capture the host-routed truth-write record before final acceptance.",
            "patch" => "Persist patch evidence before final acceptance.",
            "command_log" or "command_trace" => "Persist command execution evidence before final acceptance.",
            "build_output" => "Persist build output evidence before final acceptance.",
            "test_output" => "Persist test output evidence before final acceptance.",
            "files_written" or "changed_files" => "Persist changed-file evidence before final acceptance.",
            "worktree" => "Persist delegated worktree evidence before final acceptance.",
            "result_commit" => "Capture a bounded result commit before final acceptance.",
            "writeback" => "Complete delegated repo writeback before final acceptance.",
            "null_worker_diagnostic_only" => "Keep null_worker results diagnostic-only; do not finalize completion until a real worker result or separate governed truth mutation exists.",
            _ => $"Record how the acceptance contract evidence type '{normalizedType}' is proven before final acceptance.",
        };
    }

    private static bool HasMemoryTruthWrite(
        ReviewWritebackRecord? writeback,
        ReviewWritebackEvidenceProjection? writebackProjection)
    {
        return (writeback?.Applied == true && writeback.Files.Any(IsMemoryPath))
               || (writebackProjection?.WillApply == true && writebackProjection.Files.Any(IsMemoryPath));
    }

    private static bool HasManualReviewNote(PlannerReviewArtifact reviewArtifact)
    {
        return !string.IsNullOrWhiteSpace(reviewArtifact.Review.Reason)
               || !string.IsNullOrWhiteSpace(reviewArtifact.PlannerComment)
               || !string.IsNullOrWhiteSpace(reviewArtifact.DecisionReason);
    }

    private static bool HasHostRoutedTruthWriteRecord(
        ReviewWritebackRecord? writeback,
        ReviewWritebackEvidenceProjection? writebackProjection)
    {
        return (writeback?.Applied == true && writeback.Files.Count > 0)
               || (writebackProjection?.WillApply == true && writebackProjection.Files.Count > 0);
    }

    private static bool HasConcreteWorkerEvidence(WorkerExecutionArtifact? workerArtifact)
    {
        if (workerArtifact is null || IsNullWorkerDiagnostic(workerArtifact) || !workerArtifact.Result.Succeeded)
        {
            return false;
        }

        var evidence = workerArtifact.Evidence;
        return !string.IsNullOrWhiteSpace(evidence.PatchRef)
               || !string.IsNullOrWhiteSpace(evidence.PatchHash)
               || !string.IsNullOrWhiteSpace(evidence.CommandLogRef)
               || !string.IsNullOrWhiteSpace(evidence.CommandTraceHash)
               || !string.IsNullOrWhiteSpace(evidence.BuildOutputRef)
               || !string.IsNullOrWhiteSpace(evidence.TestOutputRef)
               || !string.IsNullOrWhiteSpace(evidence.EvidencePath)
               || !string.IsNullOrWhiteSpace(evidence.WorktreePath)
               || evidence.FilesWritten.Count > 0
               || evidence.CommandsExecuted.Count > 0
               || evidence.Artifacts.Count > 0
               || evidence.ArtifactHashes.Count > 0
               || evidence.EvidenceStrength > ExecutionEvidenceStrength.Missing
               || evidence.EvidenceCompleteness > ExecutionEvidenceCompleteness.Missing;
    }

    private static bool IsNullWorkerDiagnostic(WorkerExecutionArtifact? workerArtifact)
    {
        return string.Equals(workerArtifact?.Result.BackendId, "null_worker", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMemoryPath(string path)
    {
        var normalized = Normalize(path).TrimStart('/');
        return normalized.StartsWith(".ai/memory/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, ".ai/memory", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? requirementType)
    {
        return (requirementType ?? string.Empty)
            .Trim()
            .Replace('-', '_')
            .Replace(' ', '_')
            .ToLowerInvariant();
    }
}

public sealed record ReviewEvidenceAssessment(IReadOnlyList<ReviewEvidenceGap> MissingRequirements)
{
    public static ReviewEvidenceAssessment Satisfied { get; } = new(Array.Empty<ReviewEvidenceGap>());

    public bool IsSatisfied => MissingRequirements.Count == 0;

    public string BuildFailureMessage(string taskId)
    {
        return $"Cannot approve review for {taskId}: acceptance contract evidence is missing: {SummarizeMissingRequirements()}.";
    }

    public string SummarizeMissingRequirements()
    {
        return string.Join(
            "; ",
            MissingRequirements.Select(static gap => gap.DisplayLabel));
    }

    public IReadOnlyList<string> BuildFollowUpActions()
    {
        return MissingRequirements
            .Select(static gap => gap.FollowUpAction)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed record ReviewEvidenceGap(string RequirementType, string? Description, string FollowUpAction)
{
    public string DisplayLabel => string.IsNullOrWhiteSpace(Description)
        ? RequirementType.Trim()
        : $"{RequirementType.Trim()} ({Description.Trim()})";
}
