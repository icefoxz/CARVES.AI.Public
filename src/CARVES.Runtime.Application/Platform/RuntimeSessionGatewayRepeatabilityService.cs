using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeSessionGatewayRepeatabilityService
{
    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<RuntimeSessionGatewayPrivateAlphaHandoffSurface> privateAlphaHandoffFactory;
    private readonly TaskGraphService taskGraphService;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly OperatorOsEventStreamService operatorOsEventStreamService;
    private readonly ReviewEvidenceProjectionService reviewEvidenceProjectionService;

    public RuntimeSessionGatewayRepeatabilityService(
        string repoRoot,
        Func<RuntimeSessionGatewayPrivateAlphaHandoffSurface> privateAlphaHandoffFactory,
        TaskGraphService taskGraphService,
        IRuntimeArtifactRepository artifactRepository,
        OperatorOsEventStreamService operatorOsEventStreamService,
        ReviewEvidenceProjectionService reviewEvidenceProjectionService)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.privateAlphaHandoffFactory = privateAlphaHandoffFactory;
        this.taskGraphService = taskGraphService;
        this.artifactRepository = artifactRepository;
        this.operatorOsEventStreamService = operatorOsEventStreamService;
        this.reviewEvidenceProjectionService = reviewEvidenceProjectionService;
    }

    public RuntimeSessionGatewayRepeatabilitySurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        const string executionPlanPath = "docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md";
        const string releaseSurfacePath = "docs/session-gateway/release-surface.md";
        const string repeatabilityReadinessPath = "docs/session-gateway/repeatability-readiness.md";
        const string dogfoodValidationPath = "docs/session-gateway/dogfood-validation.md";
        const string operatorProofContractPath = "docs/session-gateway/operator-proof-contract.md";
        const string alphaSetupPath = "docs/session-gateway/ALPHA_SETUP.md";
        const string alphaQuickstartPath = "docs/session-gateway/ALPHA_QUICKSTART.md";
        const string knownLimitationsPath = "docs/session-gateway/KNOWN_LIMITATIONS.md";
        const string bugReportBundlePath = "docs/session-gateway/BUG_REPORT_BUNDLE.md";

        ValidateDocument(executionPlanPath, "Execution plan", errors);
        ValidateDocument(releaseSurfacePath, "Release surface", errors);
        ValidateDocument(repeatabilityReadinessPath, "Repeatability readiness", errors);
        ValidateDocument(dogfoodValidationPath, "Dogfood validation", errors);
        ValidateDocument(operatorProofContractPath, "Operator proof contract", errors);
        ValidateDocument(alphaSetupPath, "Alpha setup", errors);
        ValidateDocument(alphaQuickstartPath, "Alpha quickstart", errors);
        ValidateDocument(knownLimitationsPath, "Known limitations", errors);
        ValidateDocument(bugReportBundlePath, "Bug report bundle", errors);

        var handoff = privateAlphaHandoffFactory();
        errors.AddRange(handoff.Errors.Select(error => $"Private alpha handoff surface: {error}"));
        warnings.AddRange(handoff.Warnings.Select(warning => $"Private alpha handoff surface: {warning}"));

        var recentGatewayTasks = taskGraphService.Load().Tasks.Values
            .Where(IsSessionGatewayTask)
            .OrderByDescending(task => task.UpdatedAt)
            .ThenByDescending(task => task.TaskId, StringComparer.Ordinal)
            .Take(6)
            .Select(ProjectTask)
            .ToArray();
        if (recentGatewayTasks.Length == 0)
        {
            warnings.Add("No recent Session Gateway task history is currently projected from task truth.");
        }

        var recentTimelineEntries = operatorOsEventStreamService.Load()
            .Where(record => IsSessionGatewayEvent(record.EventKind))
            .OrderByDescending(record => record.OccurredAt)
            .Take(12)
            .Select(ProjectTimelineEntry)
            .ToArray();
        if (recentTimelineEntries.Length == 0)
        {
            warnings.Add("No Session Gateway operator-event timeline is currently recorded in the live repo; repeatability remains route-first until a bounded rerun produces fresh event evidence.");
        }

        var recoveryCommands = handoff.MaintenanceCommands
            .Append(RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-repeatability"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var rerunCommands = new[]
        {
            RuntimeHostCommandLauncher.Cold("host", "start", "--interval-ms", "200"),
            RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-repeatability"),
            RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-private-alpha-handoff"),
            RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-dogfood-validation"),
        };

        var repeatabilityReady =
            handoff.IsValid
            && string.Equals(handoff.OverallPosture, "private_alpha_deliverable_ready", StringComparison.Ordinal)
            && errors.Count == 0;

        return new RuntimeSessionGatewayRepeatabilitySurface
        {
            ExecutionPlanPath = executionPlanPath,
            ReleaseSurfacePath = releaseSurfacePath,
            RepeatabilityReadinessPath = repeatabilityReadinessPath,
            DogfoodValidationPath = dogfoodValidationPath,
            OperatorProofContractPath = operatorProofContractPath,
            AlphaSetupPath = alphaSetupPath,
            AlphaQuickstartPath = alphaQuickstartPath,
            KnownLimitationsPath = knownLimitationsPath,
            BugReportBundlePath = bugReportBundlePath,
            OverallPosture = repeatabilityReady ? "repeatable_private_alpha_ready" : "blocked_by_repeatability_gaps",
            PrivateAlphaHandoffPosture = handoff.OverallPosture,
            DogfoodValidationPosture = handoff.DogfoodValidationPosture,
            ProgramClosureVerdict = handoff.ProgramClosureVerdict,
            ContinuationGateOutcome = handoff.ContinuationGateOutcome,
            ThinShellRoute = handoff.ThinShellRoute,
            SessionCollectionRoute = handoff.SessionCollectionRoute,
            MessageRouteTemplate = handoff.MessageRouteTemplate,
            EventsRouteTemplate = handoff.EventsRouteTemplate,
            AcceptedOperationRouteTemplate = handoff.AcceptedOperationRouteTemplate,
            ProviderVisibilitySummary = handoff.ProviderVisibilitySummary,
            ProviderStatuses = handoff.ProviderStatuses,
            RecoveryCommands = recoveryCommands,
            ArtifactBundleCommands = handoff.BugReportBundleCommands,
            RerunCommands = rerunCommands,
            SupportedIntents = handoff.SupportedIntents,
            RecentGatewayTasks = recentGatewayTasks,
            RecentTimelineEntries = recentTimelineEntries,
            OperatorProofContract = handoff.OperatorProofContract,
            RecommendedNextAction = repeatabilityReady
                ? "Use the same Runtime-owned gateway lane to restart, recover, inspect recent work, collect the bundle, and rerun bounded alpha scenarios without widening proof claims beyond WAITING_OPERATOR_*."
                : "Restore private-alpha handoff readiness and repeatability docs before treating Session Gateway as repeatable private alpha.",
            IsValid = errors.Count == 0 && handoff.IsValid,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "Repeatability readiness does not create a second control plane, second task truth, or front-end-owned rerun lane.",
                "Repeatability readiness does not claim operator_run_proof or external_user_proof already exists.",
                "Recent gateway task history and bundle access stay Runtime-owned and do not authorize client-owned review or recovery truth.",
            ],
        };
    }

    private RuntimeSessionGatewayRecentTaskSurface ProjectTask(TaskNode task)
    {
        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(task.TaskId);
        var runArtifact = artifactRepository.TryLoadWorkerArtifact(task.TaskId);
        var workerArtifact = artifactRepository.TryLoadWorkerExecutionArtifact(task.TaskId);
        var reviewEvidenceProjection = reviewEvidenceProjectionService.Build(task, reviewArtifact, workerArtifact);
        var missingReviewEvidence = reviewEvidenceProjection.MissingBeforeWriteback
            .Concat(reviewEvidenceProjection.MissingAfterWriteback)
            .Select(static gap => gap.DisplayLabel)
            .Concat(reviewEvidenceProjection.ClosureBlockers.Select(static blocker => $"closure:{blocker}"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var acceptanceContractBinding = BuildAcceptanceContractBindingProjection(task, runArtifact, workerArtifact);

        return new RuntimeSessionGatewayRecentTaskSurface
        {
            TaskId = task.TaskId,
            CardId = task.CardId,
            Title = task.Title,
            Status = task.Status.ToString().ToLowerInvariant(),
            UpdatedAt = task.UpdatedAt,
            RecoveryAction = task.LastRecoveryAction.ToString().ToLowerInvariant(),
            RecoveryReason = task.LastRecoveryReason ?? "(none)",
            ReviewArtifactAvailable = reviewArtifact is not null,
            WorkerExecutionArtifactAvailable = workerArtifact is not null,
            ProviderArtifactAvailable = artifactRepository.TryLoadProviderArtifact(task.TaskId) is not null,
            ReviewEvidenceStatus = reviewEvidenceProjection.Status,
            ReviewCanFinalApprove = reviewEvidenceProjection.CanFinalApprove,
            ReviewEvidenceSummary = reviewEvidenceProjection.Summary,
            MissingReviewEvidence = missingReviewEvidence,
            WorkerCompletionClaimStatus = reviewEvidenceProjection.CompletionClaimStatus,
            WorkerCompletionClaimRequired = reviewEvidenceProjection.CompletionClaimRequired,
            WorkerCompletionClaimSummary = reviewEvidenceProjection.CompletionClaimSummary,
            MissingWorkerCompletionClaimFields = reviewEvidenceProjection.CompletionClaimMissingFields,
            WorkerCompletionClaimEvidencePaths = reviewEvidenceProjection.CompletionClaimEvidencePaths,
            WorkerCompletionClaimNextRecommendation = reviewEvidenceProjection.CompletionClaimNextRecommendation,
            AcceptanceContractBindingState = acceptanceContractBinding.BindingState,
            AcceptanceContractId = acceptanceContractBinding.AcceptanceContractId,
            AcceptanceContractStatus = acceptanceContractBinding.AcceptanceContractStatus,
            ProjectedAcceptanceContractId = acceptanceContractBinding.ProjectedAcceptanceContractId,
            ProjectedAcceptanceContractStatus = acceptanceContractBinding.ProjectedAcceptanceContractStatus,
            AcceptanceContractEvidenceRequired = acceptanceContractBinding.AcceptanceContractEvidenceRequired,
        };
    }

    private static AcceptanceContractBindingProjection BuildAcceptanceContractBindingProjection(
        TaskNode task,
        TaskRunArtifact? runArtifact,
        WorkerExecutionArtifact? workerArtifact)
    {
        var contract = task.AcceptanceContract;
        var contractId = NormalizeValue(contract?.ContractId);
        var contractStatus = contract?.Status.ToString();
        var contractEvidence = contract?.EvidenceRequired
            .Select(static requirement => requirement.Type)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();

        var metadata = runArtifact?.Report.Request.ExecutionRequest?.Metadata;
        var projectedContractId = NormalizeValue(metadata?.GetValueOrDefault("acceptance_contract_id"));
        var projectedContractStatus = NormalizeValue(metadata?.GetValueOrDefault("acceptance_contract_status"));
        var projectedContractEvidence = SplitDelimitedMetadata(metadata?.GetValueOrDefault("acceptance_contract_evidence_required"));
        var hasProjectedBinding = projectedContractId is not null
            || projectedContractStatus is not null
            || projectedContractEvidence.Length > 0;
        var hasDelegatedExecutionArtifacts = runArtifact is not null || workerArtifact is not null;

        var bindingState = contract switch
        {
            null when hasProjectedBinding => "projection_drift",
            null => "none",
            not null when hasProjectedBinding && ContractProjectionMatches(contractId, contractStatus, contractEvidence, projectedContractId, projectedContractStatus, projectedContractEvidence) => "projected",
            not null when hasProjectedBinding => "projection_drift",
            not null when hasDelegatedExecutionArtifacts => "missing_projection",
            _ => "task_only",
        };

        return new AcceptanceContractBindingProjection(
            bindingState,
            contractId,
            contractStatus,
            projectedContractId,
            projectedContractStatus,
            contractEvidence);
    }

    private static RuntimeSessionGatewayTimelineEntrySurface ProjectTimelineEntry(OperatorOsEventRecord record)
    {
        return new RuntimeSessionGatewayTimelineEntrySurface
        {
            EventKind = record.EventKind.ToString(),
            Stage = ResolveTimelineStage(record.EventKind),
            Summary = record.Summary,
            TaskId = record.TaskId,
            OperationId = record.ReferenceId,
            RecordedAt = record.OccurredAt,
        };
    }

    private static string ResolveTimelineStage(OperatorOsEventKind eventKind)
    {
        return eventKind switch
        {
            OperatorOsEventKind.SessionGatewaySessionCreated => "accepted",
            OperatorOsEventKind.SessionGatewayTurnAccepted => "accepted",
            OperatorOsEventKind.SessionGatewayTurnClassified => "classified",
            OperatorOsEventKind.SessionGatewayOperationAccepted => "accepted",
            OperatorOsEventKind.SessionGatewayOperationProgressed => "progressed",
            OperatorOsEventKind.SessionGatewayReviewResolved => "review",
            OperatorOsEventKind.SessionGatewayReplanRequested => "requested",
            OperatorOsEventKind.SessionGatewayReplanProjected => "projected",
            OperatorOsEventKind.SessionGatewayOperationCompleted => "completed",
            OperatorOsEventKind.SessionGatewayOperationFailed => "failed",
            OperatorOsEventKind.SessionGatewayOperatorActionRequired => SessionGatewayOperatorWaitStates.WaitingOperatorSetup,
            OperatorOsEventKind.SessionGatewayOperatorProjectRequired => SessionGatewayOperatorWaitStates.WaitingOperatorSetup,
            OperatorOsEventKind.SessionGatewayOperatorEvidenceRequired => SessionGatewayOperatorWaitStates.WaitingOperatorEvidence,
            OperatorOsEventKind.SessionGatewayRealWorldProofMissing => "blocked",
            _ => "projected",
        };
    }

    private static bool IsSessionGatewayEvent(OperatorOsEventKind eventKind)
    {
        return eventKind is OperatorOsEventKind.SessionGatewaySessionCreated
            or OperatorOsEventKind.SessionGatewayTurnAccepted
            or OperatorOsEventKind.SessionGatewayTurnClassified
            or OperatorOsEventKind.SessionGatewayOperationAccepted
            or OperatorOsEventKind.SessionGatewayOperationProgressed
            or OperatorOsEventKind.SessionGatewayReviewResolved
            or OperatorOsEventKind.SessionGatewayReplanRequested
            or OperatorOsEventKind.SessionGatewayReplanProjected
            or OperatorOsEventKind.SessionGatewayOperationCompleted
            or OperatorOsEventKind.SessionGatewayOperationFailed
            or OperatorOsEventKind.SessionGatewayOperatorActionRequired
            or OperatorOsEventKind.SessionGatewayOperatorProjectRequired
            or OperatorOsEventKind.SessionGatewayOperatorEvidenceRequired
            or OperatorOsEventKind.SessionGatewayRealWorldProofMissing;
    }

    private static bool IsSessionGatewayTask(TaskNode task)
    {
        return ContainsGatewayToken(task.Title)
               || ContainsGatewayToken(task.Description)
               || task.Scope.Any(ContainsGatewayToken)
               || task.Acceptance.Any(ContainsGatewayToken)
               || task.Constraints.Any(ContainsGatewayToken);
    }

    private static bool ContainsGatewayToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("session gateway", StringComparison.OrdinalIgnoreCase)
               || value.Contains("session-gateway", StringComparison.OrdinalIgnoreCase)
               || value.Contains("private alpha", StringComparison.OrdinalIgnoreCase)
               || value.Contains("dogfood", StringComparison.OrdinalIgnoreCase)
               || value.Contains("operator proof", StringComparison.OrdinalIgnoreCase)
               || value.Contains("thin shell", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContractProjectionMatches(
        string? contractId,
        string? contractStatus,
        IReadOnlyList<string> contractEvidence,
        string? projectedContractId,
        string? projectedContractStatus,
        IReadOnlyList<string> projectedContractEvidence)
    {
        return string.Equals(contractId, projectedContractId, StringComparison.Ordinal)
               && string.Equals(contractStatus, projectedContractStatus, StringComparison.Ordinal)
               && contractEvidence.SequenceEqual(projectedContractEvidence, StringComparer.Ordinal);
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string[] SplitDelimitedMetadata(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private void ValidateDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private sealed record AcceptanceContractBindingProjection(
        string BindingState,
        string? AcceptanceContractId,
        string? AcceptanceContractStatus,
        string? ProjectedAcceptanceContractId,
        string? ProjectedAcceptanceContractStatus,
        IReadOnlyList<string> AcceptanceContractEvidenceRequired);
}
