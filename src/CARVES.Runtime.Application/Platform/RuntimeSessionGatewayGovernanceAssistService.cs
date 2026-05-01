using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeSessionGatewayGovernanceAssistService
{
    private readonly string repoRoot;
    private readonly Func<RuntimeSessionGatewayRepeatabilitySurface> repeatabilityFactory;

    public RuntimeSessionGatewayGovernanceAssistService(
        string repoRoot,
        Func<RuntimeSessionGatewayRepeatabilitySurface> repeatabilityFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.repeatabilityFactory = repeatabilityFactory;
    }

    public RuntimeSessionGatewayGovernanceAssistSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        const string executionPlanPath = "docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md";
        const string releaseSurfacePath = "docs/session-gateway/release-surface.md";
        const string repeatabilityReadinessPath = "docs/session-gateway/repeatability-readiness.md";
        const string governanceAssistPath = "docs/session-gateway/governance-assist.md";

        ValidateDocument(executionPlanPath, "Execution plan", errors);
        ValidateDocument(releaseSurfacePath, "Release surface", errors);
        ValidateDocument(repeatabilityReadinessPath, "Repeatability readiness", errors);
        ValidateDocument(governanceAssistPath, "Governance assist", errors);

        var repeatability = repeatabilityFactory();
        errors.AddRange(repeatability.Errors.Select(error => $"Repeatability surface: {error}"));
        warnings.AddRange(repeatability.Warnings.Select(warning => $"Repeatability surface: {warning}"));

        var changePressures = BuildChangePressures(repeatability);
        var reviewEvidencePlaybook = BuildReviewEvidencePlaybook(repeatability);
        var decompositionCandidates = BuildDecompositionCandidates(repeatability, reviewEvidencePlaybook);
        var weightLedger = BuildArtifactWeightLedger(repeatability);
        var recentReviewTasks = repeatability.RecentGatewayTasks
            .Where(IsReviewTask)
            .ToArray();
        var acceptanceContractProjectedCount = repeatability.RecentGatewayTasks.Count(IsAcceptanceContractProjectedTask);
        var acceptanceContractBindingGapTasks = repeatability.RecentGatewayTasks
            .Where(IsAcceptanceContractBindingGapTask)
            .ToArray();
        var workerCompletionClaimGapTasks = repeatability.RecentGatewayTasks
            .Where(HasWorkerCompletionClaimGap)
            .ToArray();
        var reviewFinalReadyCount = recentReviewTasks.Count(static task => task.ReviewCanFinalApprove);
        var reviewEvidenceBlockedCount = recentReviewTasks.Count(task =>
            !task.ReviewCanFinalApprove
            && !string.Equals(task.ReviewEvidenceStatus, "unavailable", StringComparison.Ordinal));
        var reviewEvidenceUnavailableCount = recentReviewTasks.Count(task =>
            string.Equals(task.ReviewEvidenceStatus, "unavailable", StringComparison.Ordinal));

        var postureReady =
            repeatability.IsValid
            && string.Equals(repeatability.OverallPosture, "repeatable_private_alpha_ready", StringComparison.Ordinal)
            && errors.Count == 0;

        return new RuntimeSessionGatewayGovernanceAssistSurface
        {
            ExecutionPlanPath = executionPlanPath,
            ReleaseSurfacePath = releaseSurfacePath,
            RepeatabilityReadinessPath = repeatabilityReadinessPath,
            GovernanceAssistPath = governanceAssistPath,
            OverallPosture = postureReady ? "governance_assist_observe_ready" : "blocked_by_governance_assist_gaps",
            RepeatabilityPosture = repeatability.OverallPosture,
            ProgramClosureVerdict = repeatability.ProgramClosureVerdict,
            ContinuationGateOutcome = repeatability.ContinuationGateOutcome,
            ProviderVisibilitySummary = repeatability.ProviderVisibilitySummary,
            SupportedIntents = repeatability.SupportedIntents,
            ArtifactWeightTotal = weightLedger.Sum(entry => entry.WeightScore),
            HighPressureCount = changePressures.Count(entry => string.Equals(entry.Level, "high", StringComparison.Ordinal)),
            RecentReviewTaskCount = recentReviewTasks.Length,
            ReviewFinalReadyCount = reviewFinalReadyCount,
            ReviewEvidenceBlockedCount = reviewEvidenceBlockedCount,
            ReviewEvidenceUnavailableCount = reviewEvidenceUnavailableCount,
            WorkerCompletionClaimGapCount = workerCompletionClaimGapTasks.Length,
            AcceptanceContractProjectedCount = acceptanceContractProjectedCount,
            AcceptanceContractBindingGapCount = acceptanceContractBindingGapTasks.Length,
            ArtifactWeightLedger = weightLedger,
            ChangePressures = changePressures,
            DecompositionCandidates = decompositionCandidates,
            ReviewEvidencePlaybook = reviewEvidencePlaybook,
            RecentGatewayTasks = repeatability.RecentGatewayTasks,
            OperatorProofContract = repeatability.OperatorProofContract,
            RecommendedNextAction = ResolveRecommendedNextAction(postureReady, changePressures, decompositionCandidates, reviewEvidencePlaybook),
            IsValid = errors.Count == 0 && repeatability.IsValid,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "Governance assist does not auto-block execution, mutate approval truth, or promote suggestions into enforced runtime gates.",
                "Dynamic gate mode remains observe_assist only; this slice does not enable govern or protect semantics.",
                "Decomposition candidates stay Runtime-owned read models and do not create a second planner, scheduler, or client-owned governance state.",
                "Worker completion claims stay worker declarations; Host validation and Review closure remain authoritative.",
            ],
        };
    }

    private static IReadOnlyList<SessionGatewayArtifactWeightEntrySurface> BuildArtifactWeightLedger(RuntimeSessionGatewayRepeatabilitySurface repeatability)
    {
        var recentTasks = repeatability.RecentGatewayTasks;
        var taskCount = recentTasks.Count;
        var reviewArtifacts = recentTasks.Count(task => task.ReviewArtifactAvailable);
        var workerArtifacts = recentTasks.Count(task => task.WorkerExecutionArtifactAvailable);
        var providerArtifacts = recentTasks.Count(task => task.ProviderArtifactAvailable);
        var recentReviewTasks = recentTasks.Where(IsReviewTask).ToArray();
        var acceptanceContractProjectedCount = recentTasks.Count(IsAcceptanceContractProjectedTask);
        var acceptanceContractBindingGapTasks = recentTasks.Where(IsAcceptanceContractBindingGapTask).ToArray();
        var workerCompletionClaimGapTasks = recentTasks.Where(HasWorkerCompletionClaimGap).ToArray();
        var reviewFinalReadyCount = recentReviewTasks.Count(static task => task.ReviewCanFinalApprove);
        var reviewEvidenceBlockedCount = recentReviewTasks.Count(task =>
            !task.ReviewCanFinalApprove
            && !string.Equals(task.ReviewEvidenceStatus, "unavailable", StringComparison.Ordinal));
        var reviewEvidenceUnavailableCount = recentReviewTasks.Count(task =>
            string.Equals(task.ReviewEvidenceStatus, "unavailable", StringComparison.Ordinal));

        return
        [
            new SessionGatewayArtifactWeightEntrySurface
            {
                ArtifactKind = "operator_proof_contract",
                WeightClass = repeatability.OperatorProofContract.RealWorldProofMissing ? "high" : "medium",
                WeightScore = repeatability.OperatorProofContract.RealWorldProofMissing ? 4 : 2,
                Summary = repeatability.OperatorProofContract.BlockingSummary,
                EvidenceReferences = ["docs/session-gateway/operator-proof-contract.md", repeatability.RepeatabilityReadinessPath],
            },
            new SessionGatewayArtifactWeightEntrySurface
            {
                ArtifactKind = "recent_gateway_task_history",
                WeightClass = taskCount >= 4 ? "medium" : "low",
                WeightScore = taskCount >= 4 ? 3 : 1,
                Summary = $"Recent bounded gateway task history projects {taskCount} task(s), with review artifacts on {reviewArtifacts}, worker artifacts on {workerArtifacts}, and provider artifacts on {providerArtifacts}. Recent review queue projects {recentReviewTasks.Length} task(s): {reviewFinalReadyCount} final-ready, {reviewEvidenceBlockedCount} evidence-blocked, {reviewEvidenceUnavailableCount} projection-unavailable, {workerCompletionClaimGapTasks.Length} worker-claim gap(s).",
                EvidenceReferences = [repeatability.RepeatabilityReadinessPath],
            },
            new SessionGatewayArtifactWeightEntrySurface
            {
                ArtifactKind = "acceptance_contract_binding",
                WeightClass = acceptanceContractBindingGapTasks.Length switch
                {
                    >= 2 => "high",
                    1 => "medium",
                    _ => "low",
                },
                WeightScore = acceptanceContractBindingGapTasks.Length switch
                {
                    >= 2 => 3,
                    1 => 2,
                    _ => 1,
                },
                Summary = acceptanceContractBindingGapTasks.Length == 0
                    ? $"Delegated-run acceptance contract binding is projected on {acceptanceContractProjectedCount} recent Session Gateway task(s), with no current projection gaps."
                    : $"Delegated-run acceptance contract binding is projected on {acceptanceContractProjectedCount} recent Session Gateway task(s), with {acceptanceContractBindingGapTasks.Length} recent projection gap(s): {string.Join("; ", acceptanceContractBindingGapTasks.Select(FormatAcceptanceContractBindingTaskSummary))}.",
                EvidenceReferences = acceptanceContractBindingGapTasks.Length == 0
                    ? [repeatability.RepeatabilityReadinessPath]
                    :
                    [
                        repeatability.RepeatabilityReadinessPath,
                        .. acceptanceContractBindingGapTasks.SelectMany(BuildAcceptanceContractBindingReferences),
                    ],
            },
            new SessionGatewayArtifactWeightEntrySurface
            {
                ArtifactKind = "operator_event_timeline",
                WeightClass = repeatability.RecentTimelineEntries.Count == 0 ? "medium" : "low",
                WeightScore = repeatability.RecentTimelineEntries.Count == 0 ? 3 : 1,
                Summary = repeatability.RecentTimelineEntries.Count == 0
                    ? "No live Session Gateway operator-event timeline is currently recorded, so governance assist treats rerun-timeline capture as a bounded assist target."
                    : $"Recent Session Gateway operator-event truth projects {repeatability.RecentTimelineEntries.Count} timeline entr{(repeatability.RecentTimelineEntries.Count == 1 ? "y" : "ies")}.",
                EvidenceReferences = [repeatability.RepeatabilityReadinessPath],
            },
            new SessionGatewayArtifactWeightEntrySurface
            {
                ArtifactKind = "provider_visibility",
                WeightClass = "low",
                WeightScore = 1,
                Summary = $"Provider visibility remains Runtime-owned and advisory: {repeatability.ProviderVisibilitySummary}.",
                EvidenceReferences = [repeatability.ReleaseSurfacePath],
            },
        ];
    }

    private static IReadOnlyList<SessionGatewayChangePressureSurface> BuildChangePressures(RuntimeSessionGatewayRepeatabilitySurface repeatability)
    {
        var recentBlockedReviewTasks = repeatability.RecentGatewayTasks
            .Where(IsBlockedReviewTask)
            .ToArray();
        var acceptanceContractBindingGapTasks = repeatability.RecentGatewayTasks
            .Where(IsAcceptanceContractBindingGapTask)
            .ToArray();
        var workerCompletionClaimGapTasks = repeatability.RecentGatewayTasks
            .Where(HasWorkerCompletionClaimGap)
            .ToArray();
        var blockedTaskList = recentBlockedReviewTasks
            .Select(static task => $"{task.TaskId} ({FormatMissingReviewEvidence(task.MissingReviewEvidence)})")
            .ToArray();
        var acceptanceContractBindingTaskList = acceptanceContractBindingGapTasks
            .Select(FormatAcceptanceContractBindingTaskSummary)
            .ToArray();
        var workerCompletionClaimTaskList = workerCompletionClaimGapTasks
            .Select(FormatWorkerCompletionClaimTaskSummary)
            .ToArray();

        SessionGatewayChangePressureSurface[] pressures =
        [
            new SessionGatewayChangePressureSurface
            {
                PressureKind = "real_world_proof_gap",
                Level = repeatability.OperatorProofContract.RealWorldProofMissing ? "high" : "low",
                Summary = repeatability.OperatorProofContract.RealWorldProofMissing
                    ? "Real-world proof is still missing, so the assist layer keeps operator-facing proof completion as a high-priority assist slice."
                    : "Real-world proof is already attached; the assist layer is no longer prioritizing proof completion.",
                EvidenceReferences = ["docs/session-gateway/operator-proof-contract.md", repeatability.RepeatabilityReadinessPath],
            },
            new SessionGatewayChangePressureSurface
            {
                PressureKind = "timeline_evidence_gap",
                Level = repeatability.RecentTimelineEntries.Count == 0 ? "medium" : "low",
                Summary = repeatability.RecentTimelineEntries.Count == 0
                    ? "No live operator-event timeline is present yet, so a bounded rerun that produces fresh timeline evidence remains a useful assist candidate."
                    : "Timeline evidence already exists, so governance assist keeps timeline capture at low pressure.",
                EvidenceReferences = [repeatability.RepeatabilityReadinessPath],
            },
            new SessionGatewayChangePressureSurface
            {
                PressureKind = "review_evidence_blockers",
                Level = recentBlockedReviewTasks.Length switch
                {
                    >= 2 => "high",
                    1 => "medium",
                    _ => "low",
                },
                Summary = recentBlockedReviewTasks.Length == 0
                    ? "Recent Session Gateway review tasks are either final-ready or absent, so governance assist does not currently project a bounded review-evidence blocker slice."
                    : $"Recent Session Gateway review evidence remains blocked on {recentBlockedReviewTasks.Length} task(s): {string.Join("; ", blockedTaskList)}.",
                EvidenceReferences = recentBlockedReviewTasks.Length == 0
                    ? [repeatability.RepeatabilityReadinessPath]
                    :
                    [
                        repeatability.RepeatabilityReadinessPath,
                        .. recentBlockedReviewTasks.SelectMany(BuildReviewEvidenceReferences),
                    ],
            },
            new SessionGatewayChangePressureSurface
            {
                PressureKind = "acceptance_contract_binding_gaps",
                Level = acceptanceContractBindingGapTasks.Length switch
                {
                    >= 2 => "high",
                    1 => "medium",
                    _ => "low",
                },
                Summary = acceptanceContractBindingGapTasks.Length == 0
                    ? "Recent Session Gateway delegated runs either project acceptance contract binding correctly or have not yet produced delegated-run artifacts, so governance assist keeps binding repair at low pressure."
                    : $"Recent Session Gateway delegated-run acceptance contract binding remains incomplete on {acceptanceContractBindingGapTasks.Length} task(s): {string.Join("; ", acceptanceContractBindingTaskList)}.",
                EvidenceReferences = acceptanceContractBindingGapTasks.Length == 0
                    ? [repeatability.RepeatabilityReadinessPath]
                    :
                    [
                        repeatability.RepeatabilityReadinessPath,
                        .. acceptanceContractBindingGapTasks.SelectMany(BuildAcceptanceContractBindingReferences),
                    ],
            },
            new SessionGatewayChangePressureSurface
            {
                PressureKind = "worker_completion_claim_gaps",
                Level = workerCompletionClaimGapTasks.Length switch
                {
                    >= 2 => "medium",
                    1 => "low",
                    _ => "low",
                },
                Summary = workerCompletionClaimGapTasks.Length == 0
                    ? "Recent worker completion claims are either complete, not required, or absent from review-local evidence, so governance assist does not project a worker-claim repair slice."
                    : $"Recent worker completion claims remain incomplete on {workerCompletionClaimGapTasks.Length} task(s): {string.Join("; ", workerCompletionClaimTaskList)}. Claims remain declarations only, not lifecycle truth.",
                EvidenceReferences = workerCompletionClaimGapTasks.Length == 0
                    ? [repeatability.RepeatabilityReadinessPath]
                    :
                    [
                        repeatability.RepeatabilityReadinessPath,
                        .. workerCompletionClaimGapTasks.SelectMany(BuildWorkerCompletionClaimReferences),
                    ],
            },
            new SessionGatewayChangePressureSurface
            {
                PressureKind = "provider_optional_residue",
                Level = "low",
                Summary = $"Provider visibility is advisory-only at this stage: {repeatability.ProviderVisibilitySummary}.",
                EvidenceReferences = [repeatability.ReleaseSurfacePath],
            },
        ];

        return pressures
            .OrderBy(GetChangePressurePriority)
            .ThenBy(GetChangePressureLevelPriority)
            .ThenBy(static entry => entry.PressureKind, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<SessionGatewayDecompositionCandidateSurface> BuildDecompositionCandidates(
        RuntimeSessionGatewayRepeatabilitySurface repeatability,
        IReadOnlyList<SessionGatewayReviewEvidencePlaybookEntrySurface> reviewEvidencePlaybook)
    {
        var reviewEvidencePlaybookByKind = reviewEvidencePlaybook.ToDictionary(
            static entry => entry.EvidenceKind,
            static entry => entry,
            StringComparer.Ordinal);
        var candidates = new List<RankedDecompositionCandidate>();
        var currentStage = repeatability.OperatorProofContract.StageExitContracts
            .FirstOrDefault(stage => string.Equals(stage.BlockingState, repeatability.OperatorProofContract.CurrentOperatorState, StringComparison.Ordinal));

        if (currentStage is not null)
        {
            candidates.Add(new RankedDecompositionCandidate(
                Candidate: new SessionGatewayDecompositionCandidateSurface
                {
                    CandidateId = $"operator-proof-{currentStage.StageId}",
                    Title = $"Close operator proof gap: {currentStage.StageId}",
                    Summary = currentStage.MissingProofSummary,
                    BlockingState = currentStage.BlockingState,
                    PreferredProofSource = currentStage.AcceptedProofSources.FirstOrDefault() ?? SessionGatewayProofSources.OperatorRunProof,
                    SuggestedAction = string.Join(" | ", currentStage.OperatorMustDo),
                    EvidenceReferences = currentStage.RequiredEvidence,
                },
                PressureKind: "real_world_proof_gap",
                TaskUpdatedAt: DateTimeOffset.MinValue,
                PressureSpecificPriority: 0));
        }

        if (repeatability.RecentTimelineEntries.Count == 0)
        {
            candidates.Add(new RankedDecompositionCandidate(
                Candidate: new SessionGatewayDecompositionCandidateSurface
                {
                    CandidateId = "capture-bounded-rerun-timeline",
                    Title = "Capture bounded rerun timeline evidence",
                    Summary = "Produce a fresh Session Gateway operator-event timeline by rerunning the bounded lane through the same Runtime-owned gateway route.",
                    BlockingState = SessionGatewayOperatorWaitStates.WaitingOperatorRun,
                    PreferredProofSource = SessionGatewayProofSources.RepoLocalProof,
                    SuggestedAction = "Use the existing rerun commands, then re-inspect runtime-session-gateway-repeatability so the assist layer can compare fresh timeline evidence against the current operator-proof contract.",
                    EvidenceReferences = ["session_id", "operation_id", "event_stream_capture"],
                },
                PressureKind: "timeline_evidence_gap",
                TaskUpdatedAt: DateTimeOffset.MinValue,
                PressureSpecificPriority: 0));
        }

        candidates.AddRange(
            repeatability.RecentGatewayTasks
                .Where(IsAcceptanceContractBindingGapTask)
                .Select(task => new RankedDecompositionCandidate(
                    Candidate: BuildAcceptanceContractBindingCandidate(task),
                    PressureKind: "acceptance_contract_binding_gaps",
                    TaskUpdatedAt: task.UpdatedAt,
                    PressureSpecificPriority: 0)));

        candidates.AddRange(
            repeatability.RecentGatewayTasks
                .Where(IsBlockedReviewTask)
                .Select(task => new RankedDecompositionCandidate(
                    Candidate: BuildReviewEvidenceCandidate(task),
                    PressureKind: "review_evidence_blockers",
                    TaskUpdatedAt: task.UpdatedAt,
                    PressureSpecificPriority: GetReviewEvidenceCandidatePriority(task, reviewEvidencePlaybookByKind))));

        candidates.AddRange(
            repeatability.RecentGatewayTasks
                .Where(HasWorkerCompletionClaimGap)
                .Select(task => new RankedDecompositionCandidate(
                    Candidate: BuildWorkerCompletionClaimCandidate(task),
                    PressureKind: "worker_completion_claim_gaps",
                    TaskUpdatedAt: task.UpdatedAt,
                    PressureSpecificPriority: task.MissingWorkerCompletionClaimFields.Count == 0 ? 1 : 0)));

        return candidates
            .OrderBy(static candidate => GetDecompositionPressurePriority(candidate.PressureKind))
            .ThenBy(static candidate => candidate.PressureSpecificPriority)
            .ThenByDescending(static candidate => candidate.TaskUpdatedAt)
            .ThenBy(static candidate => candidate.Candidate.CandidateId, StringComparer.Ordinal)
            .Select(static candidate => candidate.Candidate)
            .ToArray();
    }

    private static IReadOnlyList<SessionGatewayReviewEvidencePlaybookEntrySurface> BuildReviewEvidencePlaybook(RuntimeSessionGatewayRepeatabilitySurface repeatability)
    {
        return repeatability.RecentGatewayTasks
            .Where(IsBlockedReviewTask)
            .SelectMany(BuildReviewEvidencePlaybookSeeds)
            .GroupBy(static seed => seed.Key, StringComparer.Ordinal)
            .OrderByDescending(static group => group.Select(seed => seed.Task.TaskId).Distinct(StringComparer.Ordinal).Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Select(BuildReviewEvidencePlaybookEntry)
            .ToArray();
    }

    private static string ResolveRecommendedNextAction(
        bool postureReady,
        IReadOnlyList<SessionGatewayChangePressureSurface> changePressures,
        IReadOnlyList<SessionGatewayDecompositionCandidateSurface> decompositionCandidates,
        IReadOnlyList<SessionGatewayReviewEvidencePlaybookEntrySurface> reviewEvidencePlaybook)
    {
        if (!postureReady)
        {
            return "Restore repeatability readiness before treating governance assist as a valid bounded maintenance slice.";
        }

        var topPressure = changePressures.FirstOrDefault();
        if (topPressure is null)
        {
            return "Keep governance assist in observe_assist mode, then use the bounded decomposition candidates to close operator-proof and rerun-evidence gaps without introducing new blocking authority.";
        }

        var topCandidate = decompositionCandidates.FirstOrDefault(candidate => CandidateMatchesPressure(topPressure.PressureKind, candidate));
        return topPressure.PressureKind switch
        {
            "acceptance_contract_binding_gaps" when topCandidate is not null
                => $"Highest-priority assist slice: {topCandidate.Title}. {topCandidate.SuggestedAction}",
            "review_evidence_blockers" when reviewEvidencePlaybook.Count > 0
                => $"Highest-priority assist slice: clear the repeated review evidence blocker '{reviewEvidencePlaybook[0].DisplayLabel}' across {reviewEvidencePlaybook[0].BlockedTaskCount} recent task(s). {reviewEvidencePlaybook[0].SuggestedAction}",
            "real_world_proof_gap" when topCandidate is not null
                => $"Highest-priority assist slice: {topCandidate.Title}. {topCandidate.SuggestedAction}",
            "timeline_evidence_gap" when topCandidate is not null
                => $"Highest-priority assist slice: {topCandidate.Title}. {topCandidate.SuggestedAction}",
            _ => "Keep governance assist in observe_assist mode, then use the bounded decomposition candidates to close operator-proof and rerun-evidence gaps without introducing new blocking authority.",
        };
    }

    private static bool IsReviewTask(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return string.Equals(task.Status, "review", StringComparison.Ordinal);
    }

    private static bool IsBlockedReviewTask(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return IsReviewTask(task)
               && !task.ReviewCanFinalApprove
               && !string.Equals(task.ReviewEvidenceStatus, "unavailable", StringComparison.Ordinal);
    }

    private static bool IsAcceptanceContractProjectedTask(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return string.Equals(task.AcceptanceContractBindingState, "projected", StringComparison.Ordinal);
    }

    private static bool IsAcceptanceContractBindingGapTask(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return string.Equals(task.AcceptanceContractBindingState, "missing_projection", StringComparison.Ordinal)
               || string.Equals(task.AcceptanceContractBindingState, "projection_drift", StringComparison.Ordinal);
    }

    private static bool HasWorkerCompletionClaimGap(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return task.WorkerCompletionClaimRequired
               && !string.Equals(task.WorkerCompletionClaimStatus, "present", StringComparison.Ordinal);
    }

    private static SessionGatewayDecompositionCandidateSurface BuildAcceptanceContractBindingCandidate(RuntimeSessionGatewayRecentTaskSurface task)
    {
        var contractSummary = FormatAcceptanceContractSummary(task.AcceptanceContractId, task.AcceptanceContractStatus);
        var projectedSummary = FormatAcceptanceContractSummary(task.ProjectedAcceptanceContractId, task.ProjectedAcceptanceContractStatus);
        return new SessionGatewayDecompositionCandidateSurface
        {
            CandidateId = $"project-acceptance-contract-binding-{task.TaskId.ToLowerInvariant()}",
            Title = $"Project acceptance contract binding: {task.TaskId}",
            Summary = task.AcceptanceContractBindingState switch
            {
                "projection_drift" => $"{task.Title} recorded delegated-run acceptance contract binding that drifted from task truth. Task truth projects {contractSummary}; delegated metadata projects {projectedSummary}.",
                _ => $"{task.Title} has an acceptance contract in task truth ({contractSummary}) but the recent delegated-run artifact is missing the projected binding metadata.",
            },
            BlockingState = "acceptance_contract_binding_gap",
            PreferredProofSource = SessionGatewayProofSources.RepoLocalProof,
            SuggestedAction = task.AcceptanceContractBindingState switch
            {
                "projection_drift" => "Recompile the execution packet from current task truth, rerun the bounded delegated lane, and re-inspect runtime-session-gateway-repeatability to confirm the projected binding matches the contract.",
                _ => "Rerun the bounded delegated lane through the Runtime-owned worker route so execution-request metadata projects the task acceptance contract, then re-inspect repeatability and governance assist.",
            },
            EvidenceReferences = BuildAcceptanceContractBindingReferences(task),
        };
    }

    private static SessionGatewayDecompositionCandidateSurface BuildReviewEvidenceCandidate(RuntimeSessionGatewayRecentTaskSurface task)
    {
        var missingEvidence = FormatMissingReviewEvidence(task.MissingReviewEvidence);
        return new SessionGatewayDecompositionCandidateSurface
        {
            CandidateId = $"clear-review-evidence-{task.TaskId.ToLowerInvariant()}",
            Title = $"Clear review evidence blocker: {task.TaskId}",
            Summary = $"{task.Title} remains in review with evidence status '{task.ReviewEvidenceStatus}' and cannot reach final approval until {missingEvidence} is proven.",
            BlockingState = "review_evidence_blocked",
            PreferredProofSource = SessionGatewayProofSources.RepoLocalProof,
            SuggestedAction = $"Capture bounded acceptance evidence for {missingEvidence}, then re-inspect runtime-session-gateway-governance-assist before approving {task.TaskId}.",
            EvidenceReferences = BuildReviewEvidenceReferences(task),
        };
    }

    private static SessionGatewayDecompositionCandidateSurface BuildWorkerCompletionClaimCandidate(RuntimeSessionGatewayRecentTaskSurface task)
    {
        var missingFields = FormatWorkerCompletionClaimMissingFields(task.MissingWorkerCompletionClaimFields);
        var nextRecommendation = string.IsNullOrWhiteSpace(task.WorkerCompletionClaimNextRecommendation)
            ? $"Ask the worker to resubmit its completion claim with {missingFields}, then rerun the review readback."
            : task.WorkerCompletionClaimNextRecommendation;
        return new SessionGatewayDecompositionCandidateSurface
        {
            CandidateId = $"resubmit-worker-completion-claim-{task.TaskId.ToLowerInvariant()}",
            Title = $"Resubmit worker completion claim: {task.TaskId}",
            Summary = $"{task.Title} has worker completion claim status '{task.WorkerCompletionClaimStatus}' with missing field(s): {missingFields}. This is actionability evidence only; it does not replace Host validation or Review closure.",
            BlockingState = "worker_completion_claim_gap",
            PreferredProofSource = SessionGatewayProofSources.RepoLocalProof,
            SuggestedAction = nextRecommendation,
            EvidenceReferences = BuildWorkerCompletionClaimReferences(task),
        };
    }

    private static IEnumerable<ReviewEvidencePlaybookSeed> BuildReviewEvidencePlaybookSeeds(RuntimeSessionGatewayRecentTaskSurface task)
    {
        if (task.MissingReviewEvidence.Count == 0)
        {
            yield return new ReviewEvidencePlaybookSeed(
                Key: task.ReviewEvidenceStatus,
                EvidenceKind: task.ReviewEvidenceStatus,
                DisplayLabel: task.ReviewEvidenceStatus,
                Task: task);
            yield break;
        }

        foreach (var missingEvidence in task.MissingReviewEvidence)
        {
            var evidenceKind = NormalizeEvidenceKind(missingEvidence);
            yield return new ReviewEvidencePlaybookSeed(
                Key: evidenceKind,
                EvidenceKind: evidenceKind,
                DisplayLabel: missingEvidence,
                Task: task);
        }
    }

    private static SessionGatewayReviewEvidencePlaybookEntrySurface BuildReviewEvidencePlaybookEntry(
        IGrouping<string, ReviewEvidencePlaybookSeed> group)
    {
        var seeds = group.ToArray();
        var taskIds = seeds
            .Select(static seed => seed.Task.TaskId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        var displayLabel = seeds
            .Select(static seed => seed.DisplayLabel)
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static label => label, StringComparer.Ordinal)
            .FirstOrDefault() ?? group.Key;

        return new SessionGatewayReviewEvidencePlaybookEntrySurface
        {
            PlaybookId = $"review-evidence-playbook-{Slugify(group.Key)}",
            EvidenceKind = group.Key,
            DisplayLabel = displayLabel,
            BlockedTaskCount = taskIds.Length,
            TaskIds = taskIds,
            Summary = BuildReviewEvidencePlaybookSummary(group.Key, displayLabel, taskIds),
            SuggestedAction = BuildReviewEvidencePlaybookAction(group.Key, displayLabel),
            EvidenceReferences =
            [
                $"review_evidence_playbook:{group.Key}",
                .. taskIds.Select(static id => $"task_id:{id}"),
                .. seeds.Select(static seed => $"review_evidence:{seed.Task.ReviewEvidenceStatus}").Distinct(StringComparer.Ordinal),
                .. seeds.Select(static seed => $"missing:{seed.DisplayLabel}").Distinct(StringComparer.Ordinal),
            ],
        };
    }

    private static IReadOnlyList<string> BuildReviewEvidenceReferences(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return
        [
            $"task_id:{task.TaskId}",
            $"review_evidence:{task.ReviewEvidenceStatus}",
            .. task.MissingReviewEvidence.Select(static item => $"missing:{item}"),
        ];
    }

    private static IReadOnlyList<string> BuildWorkerCompletionClaimReferences(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return
        [
            $"task_id:{task.TaskId}",
            $"completion_claim:{task.WorkerCompletionClaimStatus}",
            .. task.MissingWorkerCompletionClaimFields.Select(static item => $"completion_claim_missing:{item}"),
            .. task.WorkerCompletionClaimEvidencePaths.Select(static item => $"completion_claim_evidence:{item}"),
        ];
    }

    private static IReadOnlyList<string> BuildAcceptanceContractBindingReferences(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return
        [
            $"task_id:{task.TaskId}",
            $"binding_state:{task.AcceptanceContractBindingState}",
            $"contract:{FormatAcceptanceContractSummary(task.AcceptanceContractId, task.AcceptanceContractStatus)}",
            $"projected_contract:{FormatAcceptanceContractSummary(task.ProjectedAcceptanceContractId, task.ProjectedAcceptanceContractStatus)}",
            .. task.AcceptanceContractEvidenceRequired.Select(static requirement => $"contract_evidence:{requirement}"),
        ];
    }

    private static string FormatMissingReviewEvidence(IReadOnlyList<string> missingEvidence)
    {
        return missingEvidence.Count == 0
            ? "the missing review evidence"
            : string.Join(", ", missingEvidence);
    }

    private static string FormatWorkerCompletionClaimMissingFields(IReadOnlyList<string> missingFields)
    {
        return missingFields.Count == 0
            ? "the missing completion claim fields"
            : string.Join(", ", missingFields);
    }

    private static string FormatWorkerCompletionClaimTaskSummary(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return $"{task.TaskId} ({task.WorkerCompletionClaimStatus}; missing={FormatWorkerCompletionClaimMissingFields(task.MissingWorkerCompletionClaimFields)})";
    }

    private static string FormatAcceptanceContractBindingTaskSummary(RuntimeSessionGatewayRecentTaskSurface task)
    {
        return $"{task.TaskId} ({task.AcceptanceContractBindingState}; task={FormatAcceptanceContractSummary(task.AcceptanceContractId, task.AcceptanceContractStatus)}; projected={FormatAcceptanceContractSummary(task.ProjectedAcceptanceContractId, task.ProjectedAcceptanceContractStatus)})";
    }

    private static string FormatAcceptanceContractSummary(string? contractId, string? contractStatus)
    {
        return $"{contractId ?? "(none)"}@{contractStatus ?? "(none)"}";
    }

    private static string NormalizeEvidenceKind(string missingEvidence)
    {
        if (string.IsNullOrWhiteSpace(missingEvidence))
        {
            return "review_evidence_gap";
        }

        var label = missingEvidence.Trim();
        var descriptionIndex = label.IndexOf(" (", StringComparison.Ordinal);
        if (descriptionIndex >= 0)
        {
            label = label[..descriptionIndex];
        }

        return label
            .Replace('-', '_')
            .Replace(' ', '_')
            .ToLowerInvariant();
    }

    private static string BuildReviewEvidencePlaybookSummary(string evidenceKind, string displayLabel, IReadOnlyList<string> taskIds)
    {
        return taskIds.Count == 1
            ? $"Recent Session Gateway review task {taskIds[0]} remains blocked on {displayLabel}."
            : $"Recent Session Gateway review tasks {string.Join(", ", taskIds)} remain blocked on the same evidence kind '{evidenceKind}'.";
    }

    private static string BuildReviewEvidencePlaybookAction(string evidenceKind, string displayLabel)
    {
        return evidenceKind switch
        {
            "result_commit" => "Re-run the bounded task in a delegated git worktree, capture a scoped result commit, then re-inspect runtime-session-gateway-governance-assist before final approval.",
            "writeback" or "writeback_blocked" => "Restore delegated writeback availability and verify the bounded file set before re-inspecting governance assist.",
            "validation" or "validation_passed" or "validation_evidence" => "Capture passing validation evidence, then re-inspect governance assist before final approval.",
            "test_output" => "Persist the targeted test output artifact, then re-inspect governance assist before final approval.",
            "build_output" => "Persist the bounded build output artifact, then re-inspect governance assist before final approval.",
            "command_log" or "command_trace" => "Persist command execution evidence, then re-inspect governance assist before final approval.",
            "files_written" or "changed_files" or "patch" => "Persist bounded patch or file-change evidence, then re-inspect governance assist before final approval.",
            "worktree" => "Restore delegated worktree evidence and verify the bounded file set before re-inspecting governance assist.",
            _ => $"Capture the missing acceptance evidence for {displayLabel}, then re-inspect runtime-session-gateway-governance-assist before final approval.",
        };
    }

    private static string Slugify(string value)
    {
        return value
            .Replace(' ', '-')
            .Replace('_', '-')
            .ToLowerInvariant();
    }

    private static int GetChangePressurePriority(SessionGatewayChangePressureSurface pressure)
    {
        return GetDecompositionPressurePriority(pressure.PressureKind);
    }

    private static int GetChangePressureLevelPriority(SessionGatewayChangePressureSurface pressure)
    {
        return pressure.Level switch
        {
            "high" => 0,
            "medium" => 1,
            _ => 2,
        };
    }

    private static int GetDecompositionPressurePriority(string pressureKind)
    {
        return pressureKind switch
        {
            "acceptance_contract_binding_gaps" => 0,
            "review_evidence_blockers" => 1,
            "real_world_proof_gap" => 2,
            "worker_completion_claim_gaps" => 3,
            "timeline_evidence_gap" => 4,
            "provider_optional_residue" => 5,
            _ => 100,
        };
    }

    private static int GetReviewEvidenceCandidatePriority(
        RuntimeSessionGatewayRecentTaskSurface task,
        IReadOnlyDictionary<string, SessionGatewayReviewEvidencePlaybookEntrySurface> reviewEvidencePlaybookByKind)
    {
        if (task.MissingReviewEvidence.Count == 0)
        {
            return 100;
        }

        var highestPriority = 100;
        foreach (var missingEvidence in task.MissingReviewEvidence)
        {
            var normalizedKind = NormalizeEvidenceKind(missingEvidence);
            if (!reviewEvidencePlaybookByKind.TryGetValue(normalizedKind, out var playbook))
            {
                continue;
            }

            highestPriority = Math.Min(highestPriority, -playbook.BlockedTaskCount);
        }

        return highestPriority;
    }

    private static bool CandidateMatchesPressure(string pressureKind, SessionGatewayDecompositionCandidateSurface candidate)
    {
        return pressureKind switch
        {
            "acceptance_contract_binding_gaps" => string.Equals(candidate.BlockingState, "acceptance_contract_binding_gap", StringComparison.Ordinal),
            "review_evidence_blockers" => string.Equals(candidate.BlockingState, "review_evidence_blocked", StringComparison.Ordinal),
            "real_world_proof_gap" => candidate.CandidateId.StartsWith("operator-proof-", StringComparison.Ordinal),
            "worker_completion_claim_gaps" => string.Equals(candidate.BlockingState, "worker_completion_claim_gap", StringComparison.Ordinal),
            "timeline_evidence_gap" => string.Equals(candidate.CandidateId, "capture-bounded-rerun-timeline", StringComparison.Ordinal),
            _ => false,
        };
    }

    private sealed record RankedDecompositionCandidate(
        SessionGatewayDecompositionCandidateSurface Candidate,
        string PressureKind,
        DateTimeOffset TaskUpdatedAt,
        int PressureSpecificPriority);

    private sealed record ReviewEvidencePlaybookSeed(
        string Key,
        string EvidenceKind,
        string DisplayLabel,
        RuntimeSessionGatewayRecentTaskSurface Task);

    private void ValidateDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
