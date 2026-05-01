using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeGovernanceProgramReauditService
{
    private const int SupportingReportFreshnessMaxAgeDays = 7;
    private const string ProgramClosureReviewRelativePath = ".ai/runtime/governance/program-closure-review.json";
    private static readonly JsonSerializerOptions ClosureReviewJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;
    private readonly RoleGovernanceRuntimePolicy roleGovernancePolicy;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly ICodeGraphQueryService codeGraphQueryService;
    private readonly IRefactoringService refactoringService;
    private readonly TaskGraphService taskGraphService;

    public RuntimeGovernanceProgramReauditService(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        RoleGovernanceRuntimePolicy roleGovernancePolicy,
        IRuntimeArtifactRepository artifactRepository,
        ICodeGraphQueryService codeGraphQueryService,
        IRefactoringService refactoringService,
        TaskGraphService taskGraphService)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.paths = paths;
        this.systemConfig = systemConfig;
        this.roleGovernancePolicy = roleGovernancePolicy;
        this.artifactRepository = artifactRepository;
        this.codeGraphQueryService = codeGraphQueryService;
        this.refactoringService = refactoringService;
        this.taskGraphService = taskGraphService;
    }

    public RuntimeGovernanceProgramReauditSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var now = DateTimeOffset.UtcNow;

        var boundaryDocumentPath = ToRepoRelative(Path.Combine(repoRoot, "docs", "runtime", "runtime-governance-program-reaudit.md"));
        if (!File.Exists(Path.Combine(repoRoot, boundaryDocumentPath.Replace('/', Path.DirectorySeparatorChar))))
        {
            errors.Add($"Boundary document '{boundaryDocumentPath}' is missing.");
        }

        var drain = new RuntimeHotspotBacklogDrainService(repoRoot, paths, refactoringService, taskGraphService).Build();
        errors.AddRange(drain.Errors.Select(error => $"Hotspot drain surface: {error}"));
        warnings.AddRange(drain.Warnings.Select(warning => $"Hotspot drain surface: {warning}"));

        var crossFamily = new RuntimeHotspotCrossFamilyPatternService(repoRoot, paths, refactoringService, taskGraphService).Build();
        errors.AddRange(crossFamily.Errors.Select(error => $"Cross-family pattern surface: {error}"));
        warnings.AddRange(crossFamily.Warnings.Select(warning => $"Cross-family pattern surface: {warning}"));

        var proof = new RuntimeControlledGovernanceProofService(repoRoot, paths, systemConfig, roleGovernancePolicy).Build();
        errors.AddRange(proof.Errors.Select(error => $"Controlled governance proof surface: {error}"));
        warnings.AddRange(proof.Warnings.Select(warning => $"Controlled governance proof surface: {warning}"));

        var packaging = new RuntimePackagingProofFederationMaturityService(
            repoRoot,
            paths,
            systemConfig,
            roleGovernancePolicy,
            artifactRepository).Build();
        errors.AddRange(packaging.Errors.Select(error => $"Packaging proof federation maturity surface: {error}"));
        warnings.AddRange(packaging.Warnings.Select(warning => $"Packaging proof federation maturity surface: {warning}"));

        var sustainabilityPath = ToRepoRelative(SustainabilityAuditService.GetAuditPath(paths));
        var archiveReadinessPath = ToRepoRelative(OperationalHistoryArchiveReadinessService.GetLatestReportPath(paths));
        var closureReviewPath = ProgramClosureReviewRelativePath;

        var sustainability = new SustainabilityAuditService(repoRoot, paths, systemConfig, codeGraphQueryService).TryLoadLatest();
        if (sustainability is null)
        {
            warnings.Add($"Latest sustainability audit '{sustainabilityPath}' is missing.");
        }

        var archiveReadiness = new OperationalHistoryArchiveReadinessService(repoRoot, paths, systemConfig).TryLoadLatest();
        if (archiveReadiness is null)
        {
            warnings.Add($"Latest archive-readiness report '{archiveReadinessPath}' is missing.");
        }

        var closureReview = TryLoadClosureReview(errors);
        var closureReviewApproved = IsApprovedClosureReview(closureReview);

        var continuationGatePolicyService = new RuntimeGovernanceContinuationGatePolicyService(paths);
        var continuationGatePolicy = continuationGatePolicyService.LoadPolicy();
        var continuationGateValidation = continuationGatePolicyService.Validate();
        errors.AddRange(continuationGateValidation.Errors);
        warnings.AddRange(continuationGateValidation.Warnings);

        var counts = BuildCounts(drain, crossFamily, proof, packaging, sustainability, archiveReadiness, now);
        var closureDeltaPosture = DetermineClosureDeltaPosture(counts);
        var continuationGateOutcome = DetermineContinuationGateOutcome(
            counts,
            proof,
            packaging,
            sustainability,
            archiveReadiness,
            now,
            closureDeltaPosture,
            continuationGatePolicy.HoldContinuationWithoutQualifyingDelta,
            closureReviewApproved);
        var criteria = BuildCriteria(drain, crossFamily, proof, packaging, sustainability, archiveReadiness, now, closureDeltaPosture, continuationGateOutcome, closureReviewApproved);
        var overallVerdict = DetermineOverallVerdict(criteria, closureReviewApproved);
        var recommendedNextAction = DetermineRecommendedNextAction(drain, packaging, proof, sustainability, archiveReadiness, counts, overallVerdict, closureDeltaPosture, continuationGateOutcome);

        return new RuntimeGovernanceProgramReauditSurface
        {
            BoundaryDocumentPath = boundaryDocumentPath,
            SustainabilityAuditPath = sustainabilityPath,
            ArchiveReadinessPath = archiveReadinessPath,
            ClosureReviewPath = closureReviewPath,
            Summary = closureReviewApproved
                ? "Runtime governance re-audit now composes hotspot drain truth, cross-family pattern truth, controlled-governance proof, packaging maturity, supporting sustainability/archive-readiness reports, and a governed closure-review record into final program-exit truth."
                : "Wave 12 and Wave 13 keep program exit posture read-only and evidence-driven by composing hotspot drain truth, cross-family pattern truth, controlled-governance proof, packaging maturity, and latest sustainability/archive-readiness reports into one bounded re-audit surface.",
            OverallVerdict = overallVerdict,
            ContinuationGateOutcome = continuationGateOutcome,
            ClosureDeltaPosture = closureDeltaPosture,
            ClosureReviewOutcome = closureReview?.Outcome ?? "missing",
            RecommendedNextAction = recommendedNextAction,
            QueueSnapshotGeneratedAt = drain.QueueSnapshotGeneratedAt,
            SustainabilityGeneratedAt = sustainability?.GeneratedAt,
            ArchiveReadinessGeneratedAt = archiveReadiness?.GeneratedAt,
            ClosureReviewRecordedAt = closureReview?.RecordedAt,
            IsValid = errors.Count == 0 && drain.IsValid && crossFamily.IsValid && proof.IsValid && packaging.IsValid,
            Errors = errors,
            Warnings = warnings,
            Counts = counts,
            Criteria = criteria,
            NonClaims =
            [
                "Program re-audit does not become a second planner, scheduler, or execution authority.",
                "Program re-audit does not auto-open a new wave or auto-promote suggested tasks.",
                "Program re-audit does not treat stale or missing supporting reports as synthetic completion evidence."
            ],
        };
    }

    private static RuntimeGovernanceProgramReauditCountsSurface BuildCounts(
        RuntimeHotspotBacklogDrainSurface drain,
        RuntimeHotspotCrossFamilyPatternSurface crossFamily,
        RuntimeControlledGovernanceProofSurface proof,
        RuntimePackagingProofFederationMaturitySurface packaging,
        SustainabilityAuditReport? sustainability,
        OperationalHistoryArchiveReadinessReport? archiveReadiness,
        DateTimeOffset now)
    {
        var sustainabilityAgeDays = CalculateAgeDays(sustainability?.GeneratedAt, now);
        var archiveReadinessAgeDays = CalculateAgeDays(archiveReadiness?.GeneratedAt, now);
        return new RuntimeGovernanceProgramReauditCountsSurface
        {
            QueueFamilyCount = drain.Counts.QueueFamilyCount,
            ClearedQueueCount = drain.Counts.CompletedQueueCount,
            AcceptedResidualQueueCount = drain.Counts.AcceptedResidualQueueCount,
            GovernedCompletedQueueCount = drain.Counts.GovernedCompletedQueueCount,
            ResidualCompletedQueueCount = drain.Counts.CompletedWithRemainingBacklogCount,
            ResidualOpenQueueCount = drain.Counts.ResidualOpenQueueCount,
            ContinuedQueueCount = drain.Counts.ContinuedQueueCount,
            OpenBacklogItems = drain.Counts.OpenBacklogItems,
            SuggestedBacklogItems = drain.Counts.SuggestedBacklogItems,
            ResolvedBacklogItems = drain.Counts.ResolvedBacklogItems,
            ClosureBlockingBacklogItems = drain.Counts.ClosureBlockingBacklogItemCount,
            NonBlockingBacklogItems = drain.Counts.NonBlockingBacklogItemCount,
            UnselectedBacklogItems = drain.Counts.UnselectedBacklogItemCount,
            UnselectedClosureRelevantBacklogItems = drain.Counts.UnselectedClosureRelevantBacklogItemCount,
            UnselectedMaintenanceNoiseBacklogItems = drain.Counts.UnselectedMaintenanceNoiseBacklogItemCount,
            PatternCount = crossFamily.Counts.PatternCount,
            ResidualPatternCount = crossFamily.Counts.ResidualPatternCount,
            RepeatedBacklogKindPatternCount = crossFamily.Counts.RepeatedBacklogKindPatternCount,
            ValidationOverlapPatternCount = crossFamily.Counts.ValidationOverlapPatternCount,
            SharedBoundaryCategoryCount = crossFamily.Counts.SharedBoundaryCategoryCount,
            ProofLaneCount = proof.Lanes.Count,
            PackagingProfileCount = packaging.PackagingProfiles.Count,
            ClosedCapabilityCount = packaging.ClosedCapabilities.Count,
            SustainabilityAuditAvailable = sustainability is not null,
            SustainabilityAuditAgeDays = sustainabilityAgeDays,
            SustainabilityAuditFreshness = ResolveFreshness(sustainability?.GeneratedAt, now),
            SustainabilityStrictPassed = sustainability?.StrictPassed ?? false,
            SustainabilityFindingCount = sustainability?.Findings.Length ?? 0,
            SustainabilityErrorCount = sustainability?.Findings.Count(finding => string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase)) ?? 0,
            SustainabilityWarningCount = sustainability?.Findings.Count(finding => !string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase)) ?? 0,
            ArchiveReadinessAvailable = archiveReadiness is not null,
            ArchiveReadinessAgeDays = archiveReadinessAgeDays,
            ArchiveReadinessFreshness = ResolveFreshness(archiveReadiness?.GeneratedAt, now),
            ArchiveFamilyCount = archiveReadiness?.Families.Length ?? 0,
            PromotionRelevantArchivedEntryCount = archiveReadiness?.PromotionRelevantEntries.Length ?? 0,
        };
    }

    private static RuntimeGovernanceProgramReauditCriterionSurface[] BuildCriteria(
        RuntimeHotspotBacklogDrainSurface drain,
        RuntimeHotspotCrossFamilyPatternSurface crossFamily,
        RuntimeControlledGovernanceProofSurface proof,
        RuntimePackagingProofFederationMaturitySurface packaging,
        SustainabilityAuditReport? sustainability,
        OperationalHistoryArchiveReadinessReport? archiveReadiness,
        DateTimeOffset now,
        string closureDeltaPosture,
        string continuationGateOutcome,
        bool closureReviewApproved)
    {
        var sustainabilityFreshness = ResolveFreshness(sustainability?.GeneratedAt, now);
        var archiveReadinessFreshness = ResolveFreshness(archiveReadiness?.GeneratedAt, now);
        var sustainabilityAgeDays = CalculateAgeDays(sustainability?.GeneratedAt, now);
        var archiveReadinessAgeDays = CalculateAgeDays(archiveReadiness?.GeneratedAt, now);
        var queueClosureSatisfied = drain.Counts.QueueFamilyCount > 0
            && drain.Counts.CompletedQueueCount + drain.Counts.AcceptedResidualQueueCount == drain.Counts.QueueFamilyCount
            && drain.Counts.CompletedWithRemainingBacklogCount == 0
            && drain.Counts.ResidualOpenQueueCount == 0
            && drain.Queues.All(queue => !string.Equals(queue.ClosureState, "residual_open", StringComparison.Ordinal));

        var backlogPressurePresent = drain.Counts.ClosureBlockingBacklogItemCount > 0
            || drain.Counts.UnselectedClosureRelevantBacklogItemCount > 0
            || crossFamily.Counts.ResidualPatternCount > 0
            || crossFamily.Counts.RepeatedBacklogKindPatternCount > 0;

        var surfaceStabilitySatisfied =
            proof.IsValid
            && packaging.IsValid
            && sustainability is not null
            && sustainability.StrictPassed
            && archiveReadiness is not null
            && string.Equals(sustainabilityFreshness, "fresh", StringComparison.Ordinal)
            && string.Equals(archiveReadinessFreshness, "fresh", StringComparison.Ordinal);

        return
        [
            new RuntimeGovernanceProgramReauditCriterionSurface
            {
                CriterionId = "queue_family_closure",
                DisplayName = "Queue family closure",
                Status = queueClosureSatisfied ? "satisfied" : "continue_program",
                Summary = queueClosureSatisfied
                    ? $"All {drain.Counts.QueueFamilyCount} hotspot queue families are currently projected as cleared or accepted residual concentration; residual_open={drain.Counts.ResidualOpenQueueCount}, residual_completed={drain.Counts.CompletedWithRemainingBacklogCount}, and historical_continued={drain.Counts.ContinuedQueueCount} remain queue-pass history only."
                    : $"Queue closure is not satisfied: queue_families={drain.Counts.QueueFamilyCount}, cleared={drain.Counts.CompletedQueueCount}, accepted_residual={drain.Counts.AcceptedResidualQueueCount}, residual_open={drain.Counts.ResidualOpenQueueCount}, residual_completed={drain.Counts.CompletedWithRemainingBacklogCount}, historical_continued={drain.Counts.ContinuedQueueCount}.",
                EvidenceRefs =
                [
                    "inspect runtime-hotspot-backlog-drain",
                    "docs/runtime/runtime-hotspot-backlog-drain-governance.md",
                ],
                NonClaims =
                [
                    "Governed completion with residual backlog is not treated as cleared queue closure unless repo-local truth explicitly accepts that family as residual concentration.",
                    "Historical continuation passes remain visible as queue-pass provenance and are not themselves treated as uncleared families."
                ],
            },
            new RuntimeGovernanceProgramReauditCriterionSurface
            {
                CriterionId = "backlog_structural_pressure",
                DisplayName = "Backlog structural pressure",
                Status = backlogPressurePresent ? "continue_program" : "satisfied",
                Summary = backlogPressurePresent
                    ? BuildBacklogPressureSummary(drain, crossFamily)
                    : $"Current backlog and cross-family pattern truth no longer project closure-blocking structural pressure; non_blocking_backlog={drain.Counts.NonBlockingBacklogItemCount}; unselected_closure_relevant={drain.Counts.UnselectedClosureRelevantBacklogItemCount}; unselected_maintenance_noise={drain.Counts.UnselectedMaintenanceNoiseBacklogItemCount}.",
                EvidenceRefs =
                [
                    "inspect runtime-hotspot-backlog-drain",
                    "inspect runtime-hotspot-cross-family-patterns",
                    "docs/runtime/runtime-hotspot-cross-family-patterns.md",
                ],
                NonClaims =
                [
                    "One resolved backlog item does not by itself prove the hotspot family is drained.",
                    "Active backlog residue outside the current selected residual program stays visible and does not become synthetic closure evidence."
                ],
            },
            new RuntimeGovernanceProgramReauditCriterionSurface
            {
                CriterionId = "continuation_gate",
                DisplayName = "Continuation gate",
                Status = string.Equals(continuationGateOutcome, "closure_review_ready", StringComparison.Ordinal)
                         || string.Equals(continuationGateOutcome, "closure_review_completed", StringComparison.Ordinal)
                    ? "satisfied"
                    : continuationGateOutcome,
                Summary = continuationGateOutcome switch
                {
                    "closure_review_completed" => "Governed closure review has been recorded; no further continuation is needed and the convergence program can remain closed.",
                    "hold_continuation" => $"No qualifying closure delta is currently projected: cleared={drain.Counts.CompletedQueueCount}, accepted_residual={drain.Counts.AcceptedResidualQueueCount}, closure_blocking_backlog={drain.Counts.ClosureBlockingBacklogItemCount}, non_blocking_backlog={drain.Counts.NonBlockingBacklogItemCount}, unselected_closure_relevant={drain.Counts.UnselectedClosureRelevantBacklogItemCount}, unselected_maintenance_noise={drain.Counts.UnselectedMaintenanceNoiseBacklogItemCount}.",
                    "continuation_allowed" => $"A qualifying closure delta is projected as {closureDeltaPosture}; later continuation may be considered, but only through a new governed card.",
                    "closure_review_ready" => "All current closure gates are satisfied; program closure review can proceed without opening another continuation.",
                    _ => "Program closure review can proceed without opening another continuation.",
                },
                EvidenceRefs =
                [
                    "inspect runtime-hotspot-backlog-drain",
                    "inspect runtime-governance-program-reaudit",
                    "docs/runtime/runtime-governance-program-reaudit.md",
                ],
                NonClaims =
                [
                    "A continuation_allowed result does not auto-open the next wave.",
                    "A hold_continuation result does not close the program by itself."
                ],
            },
            new RuntimeGovernanceProgramReauditCriterionSurface
            {
                CriterionId = "supporting_report_freshness",
                DisplayName = "Supporting report freshness",
                Status = string.Equals(sustainabilityFreshness, "fresh", StringComparison.Ordinal)
                         && string.Equals(archiveReadinessFreshness, "fresh", StringComparison.Ordinal)
                    ? "satisfied"
                    : sustainability is null || archiveReadiness is null
                        ? "warning"
                        : "continue_program",
                Summary = string.Equals(sustainabilityFreshness, "fresh", StringComparison.Ordinal)
                          && string.Equals(archiveReadinessFreshness, "fresh", StringComparison.Ordinal)
                    ? $"Supporting reports are fresh enough for closure review: sustainability_age_days={FormatAgeDays(sustainabilityAgeDays)}, archive_readiness_age_days={FormatAgeDays(archiveReadinessAgeDays)}, freshness_window_days={SupportingReportFreshnessMaxAgeDays}."
                    : sustainability is null || archiveReadiness is null
                        ? $"Supporting report freshness is incomplete: sustainability_present={(sustainability is not null).ToString().ToLowerInvariant()}, archive_readiness_present={(archiveReadiness is not null).ToString().ToLowerInvariant()}, freshness_window_days={SupportingReportFreshnessMaxAgeDays}."
                        : $"Supporting report freshness is not closure-ready: sustainability_age_days={FormatAgeDays(sustainabilityAgeDays)} ({sustainabilityFreshness}), archive_readiness_age_days={FormatAgeDays(archiveReadinessAgeDays)} ({archiveReadinessFreshness}), freshness_window_days={SupportingReportFreshnessMaxAgeDays}.",
                EvidenceRefs =
                [
                    "inspect sustainability",
                    "inspect archive-readiness",
                    "docs/runtime/runtime-governance-program-reaudit.md",
                ],
                NonClaims =
                [
                    "Fresh supporting reports do not by themselves satisfy sustainability strictness, queue closure, or backlog pressure."
                ],
            },
            new RuntimeGovernanceProgramReauditCriterionSurface
            {
                CriterionId = "program_wave_necessity",
                DisplayName = "Program-wave necessity",
                Status = queueClosureSatisfied && !backlogPressurePresent && surfaceStabilitySatisfied && closureReviewApproved
                    ? "satisfied"
                    : queueClosureSatisfied && !backlogPressurePresent && surfaceStabilitySatisfied
                    ? "closure_candidate"
                    : string.Equals(continuationGateOutcome, "hold_continuation", StringComparison.Ordinal)
                        ? "hold_continuation"
                        : "continue_program",
                Summary = queueClosureSatisfied && !backlogPressurePresent && surfaceStabilitySatisfied && closureReviewApproved
                    ? "Current repo-local truth closes the convergence program; later work must re-enter through normal bounded maintenance instead of another continuation wave."
                    : queueClosureSatisfied && !backlogPressurePresent && surfaceStabilitySatisfied
                    ? "Current repo-local truth supports operator review of program closure rather than opening another governed continuation wave."
                    : queueClosureSatisfied
                        ? BuildProgramWaveNecessitySummary(drain, crossFamily)
                    : string.Equals(continuationGateOutcome, "hold_continuation", StringComparison.Ordinal)
                        ? $"The program still remains open, but later continuation should stay held until qualifying closure delta evidence changes from the current '{closureDeltaPosture}' posture."
                        : "Another bounded governed continuation remains necessary because queue closure, backlog pressure, or supporting surface stability is not yet satisfied.",
                EvidenceRefs =
                [
                    "docs/runtime/runtime-governance-follow-on-workmap.md",
                    "inspect runtime-hotspot-backlog-drain",
                    "inspect runtime-hotspot-cross-family-patterns",
                ],
                NonClaims =
                [
                    "Re-audit does not open the next wave automatically."
                ],
            },
            new RuntimeGovernanceProgramReauditCriterionSurface
            {
                CriterionId = "surface_stability",
                DisplayName = "Surface stability",
                Status = surfaceStabilitySatisfied
                    ? "satisfied"
                    : proof.IsValid && packaging.IsValid
                        ? "partial"
                        : "warning",
                Summary = surfaceStabilitySatisfied
                    ? $"Supporting read-only surfaces are valid, sustainability strict_passed={sustainability!.StrictPassed.ToString().ToLowerInvariant()}, sustainability_freshness={sustainabilityFreshness}, archive_readiness_freshness={archiveReadinessFreshness}, and archive-readiness is present with promotion_relevant_entries={archiveReadiness!.PromotionRelevantEntries.Length}."
                    : proof.IsValid && packaging.IsValid
                        ? $"Proof and packaging surfaces are valid, but sustainability/archive posture is not closure-ready: sustainability_present={(sustainability is not null).ToString().ToLowerInvariant()}, sustainability_freshness={sustainabilityFreshness}, strict_passed={(sustainability?.StrictPassed ?? false).ToString().ToLowerInvariant()}, archive_readiness_present={(archiveReadiness is not null).ToString().ToLowerInvariant()}, archive_readiness_freshness={archiveReadinessFreshness}."
                        : $"Supporting surfaces are not fully stable: proof_valid={proof.IsValid.ToString().ToLowerInvariant()}, packaging_valid={packaging.IsValid.ToString().ToLowerInvariant()}, sustainability_present={(sustainability is not null).ToString().ToLowerInvariant()}, archive_readiness_present={(archiveReadiness is not null).ToString().ToLowerInvariant()}.",
                EvidenceRefs =
                [
                    "inspect runtime-controlled-governance-proof",
                    "inspect runtime-packaging-proof-federation-maturity",
                    "inspect sustainability",
                    "inspect archive-readiness",
                ],
                NonClaims =
                [
                    "Stable read-only surfaces do not override sustainability drift or missing archive-readiness evidence."
                ],
            },
        ];
    }

    private static string DetermineOverallVerdict(IReadOnlyList<RuntimeGovernanceProgramReauditCriterionSurface> criteria, bool closureReviewApproved)
    {
        if (closureReviewApproved && criteria.All(criterion => string.Equals(criterion.Status, "satisfied", StringComparison.Ordinal)))
        {
            return "program_closure_complete";
        }

        if (criteria.All(criterion => string.Equals(criterion.Status, "satisfied", StringComparison.Ordinal)
                                      || string.Equals(criterion.Status, "closure_candidate", StringComparison.Ordinal)))
        {
            return "program_closure_candidate";
        }

        return "continue_program";
    }

    private static string DetermineClosureDeltaPosture(RuntimeGovernanceProgramReauditCountsSurface counts)
    {
        var signals = new List<string>();
        if (counts.ClearedQueueCount > 0)
        {
            signals.Add("queue_cleared");
        }

        if (counts.AcceptedResidualQueueCount > 0)
        {
            signals.Add("accepted_residual_concentration");
        }

        if (counts.NonBlockingBacklogItems > 0)
        {
            signals.Add("non_blocking_backlog_relief");
        }

        return signals.Count switch
        {
            0 => "none",
            1 => signals[0],
            _ => "mixed",
        };
    }

    private static string DetermineContinuationGateOutcome(
        RuntimeGovernanceProgramReauditCountsSurface counts,
        RuntimeControlledGovernanceProofSurface proof,
        RuntimePackagingProofFederationMaturitySurface packaging,
        SustainabilityAuditReport? sustainability,
        OperationalHistoryArchiveReadinessReport? archiveReadiness,
        DateTimeOffset now,
        string closureDeltaPosture,
        bool holdContinuationWithoutQualifyingDelta,
        bool closureReviewApproved)
    {
        var queueClosureSatisfied = counts.QueueFamilyCount > 0
            && counts.ClearedQueueCount + counts.AcceptedResidualQueueCount == counts.QueueFamilyCount
            && counts.ResidualCompletedQueueCount == 0
            && counts.ResidualOpenQueueCount == 0;
        var backlogPressurePresent = counts.ClosureBlockingBacklogItems > 0
            || counts.UnselectedClosureRelevantBacklogItems > 0
            || counts.ResidualPatternCount > 0
            || counts.RepeatedBacklogKindPatternCount > 0;
        var surfaceStabilitySatisfied =
            proof.IsValid
            && packaging.IsValid
            && sustainability is not null
            && sustainability.StrictPassed
            && archiveReadiness is not null
            && string.Equals(ResolveFreshness(sustainability.GeneratedAt, now), "fresh", StringComparison.Ordinal)
            && string.Equals(ResolveFreshness(archiveReadiness.GeneratedAt, now), "fresh", StringComparison.Ordinal);

        if (queueClosureSatisfied && !backlogPressurePresent && surfaceStabilitySatisfied && closureReviewApproved)
        {
            return "closure_review_completed";
        }

        if (queueClosureSatisfied && !backlogPressurePresent && surfaceStabilitySatisfied)
        {
            return "closure_review_ready";
        }

        if (string.Equals(closureDeltaPosture, "none", StringComparison.Ordinal) && holdContinuationWithoutQualifyingDelta)
        {
            return "hold_continuation";
        }

        return string.Equals(closureDeltaPosture, "none", StringComparison.Ordinal)
            ? "continue_program"
            : "continuation_allowed";
    }

    private static string DetermineRecommendedNextAction(
        RuntimeHotspotBacklogDrainSurface drain,
        RuntimePackagingProofFederationMaturitySurface packaging,
        RuntimeControlledGovernanceProofSurface proof,
        SustainabilityAuditReport? sustainability,
        OperationalHistoryArchiveReadinessReport? archiveReadiness,
        RuntimeGovernanceProgramReauditCountsSurface counts,
        string overallVerdict,
        string closureDeltaPosture,
        string continuationGateOutcome)
    {
        if (string.Equals(overallVerdict, "program_closure_complete", StringComparison.Ordinal))
        {
            return "Treat the governed closure review as terminal convergence-program truth; later changes must re-enter through normal bounded maintenance cards instead of continuation waves.";
        }

        if (string.Equals(overallVerdict, "program_closure_candidate", StringComparison.Ordinal))
        {
            return "Use the re-audit output as operator-facing evidence for a governed closure review instead of auto-opening another continuation wave.";
        }

        if (sustainability is null || archiveReadiness is null)
        {
            return "Refresh sustainability and archive-readiness reports before making program closure claims.";
        }

        if (!string.Equals(counts.SustainabilityAuditFreshness, "fresh", StringComparison.Ordinal)
            || !string.Equals(counts.ArchiveReadinessFreshness, "fresh", StringComparison.Ordinal))
        {
            return $"Refresh stale supporting reports before closure review: sustainability_age_days={FormatAgeDays(counts.SustainabilityAuditAgeDays)}, archive_readiness_age_days={FormatAgeDays(counts.ArchiveReadinessAgeDays)}, freshness_window_days={SupportingReportFreshnessMaxAgeDays}.";
        }

        if (!sustainability.StrictPassed)
        {
            return "Prune overdue ephemeral runtime residue and re-run sustainability audit before claiming closure.";
        }

        if (!proof.IsValid || !packaging.IsValid)
        {
            return "Repair invalid supporting proof or packaging surfaces before treating the current convergence program as closure-ready.";
        }

        if (string.Equals(continuationGateOutcome, "hold_continuation", StringComparison.Ordinal))
        {
            return $"Hold later continuation until a qualifying closure delta is recorded. Current delta posture is '{closureDeltaPosture}', with cleared_queues={counts.ClearedQueueCount}, accepted_residual_queues={counts.AcceptedResidualQueueCount}, closure_blocking_backlog={counts.ClosureBlockingBacklogItems}, non_blocking_backlog={counts.NonBlockingBacklogItems}, unselected_closure_relevant={counts.UnselectedClosureRelevantBacklogItems}, and unselected_maintenance_noise={counts.UnselectedMaintenanceNoiseBacklogItems}.";
        }

        var queueClosureSatisfied = counts.QueueFamilyCount > 0
            && counts.ClearedQueueCount + counts.AcceptedResidualQueueCount == counts.QueueFamilyCount
            && counts.ResidualCompletedQueueCount == 0
            && counts.ResidualOpenQueueCount == 0;

        if (queueClosureSatisfied
            && (counts.UnselectedClosureRelevantBacklogItems > 0
                || counts.ResidualPatternCount > 0
                || counts.RepeatedBacklogKindPatternCount > 0))
        {
            if (counts.UnselectedClosureRelevantBacklogItems > 0)
            {
                return $"Queue-family closure is satisfied; open a bounded unselected-backlog disposition card instead of another technical continuation: unselected_closure_relevant={counts.UnselectedClosureRelevantBacklogItems}, unselected_maintenance_noise={counts.UnselectedMaintenanceNoiseBacklogItems}, residual_patterns={counts.ResidualPatternCount}, repeated_backlog_kinds={counts.RepeatedBacklogKindPatternCount}.";
            }

            return $"Queue-family closure and unselected backlog disposition are satisfied for the current residual program; open a bounded cross-family pattern disposition card instead of another technical continuation: residual_patterns={counts.ResidualPatternCount}, repeated_backlog_kinds={counts.RepeatedBacklogKindPatternCount}, unselected_maintenance_noise={counts.UnselectedMaintenanceNoiseBacklogItems}.";
        }

        if (!queueClosureSatisfied && (drain.Counts.ContinuedQueueCount > 0 || drain.Counts.CompletedWithRemainingBacklogCount > 0))
        {
            return $"Open the next bounded governed continuation only if it is explicitly anchored to the current '{closureDeltaPosture}' closure-delta evidence instead of wave cadence alone.";
        }

        if (counts.ClosureBlockingBacklogItems > 0)
        {
            return "Drain the remaining closure-blocking backlog through bounded governed cards before treating the program as closed.";
        }

        return "Continue the current governed drain program until queue closure, backlog pressure, and supporting surface stability are all satisfied.";
    }

    private static string BuildBacklogPressureSummary(
        RuntimeHotspotBacklogDrainSurface drain,
        RuntimeHotspotCrossFamilyPatternSurface crossFamily)
    {
        var blockers = new List<string>();
        if (drain.Counts.ClosureBlockingBacklogItemCount > 0)
        {
            blockers.Add($"closure_blocking={drain.Counts.ClosureBlockingBacklogItemCount}");
        }

        if (drain.Counts.UnselectedClosureRelevantBacklogItemCount > 0)
        {
            blockers.Add($"unselected_closure_relevant={drain.Counts.UnselectedClosureRelevantBacklogItemCount}");
        }

        if (crossFamily.Counts.ResidualPatternCount > 0)
        {
            blockers.Add($"residual_patterns={crossFamily.Counts.ResidualPatternCount}");
        }

        if (crossFamily.Counts.RepeatedBacklogKindPatternCount > 0)
        {
            blockers.Add($"repeated_backlog_kinds={crossFamily.Counts.RepeatedBacklogKindPatternCount}");
        }

        var context = new List<string>();
        if (drain.Counts.NonBlockingBacklogItemCount > 0)
        {
            context.Add($"non_blocking_backlog={drain.Counts.NonBlockingBacklogItemCount}");
        }

        if (drain.Counts.UnselectedMaintenanceNoiseBacklogItemCount > 0)
        {
            context.Add($"unselected_maintenance_noise={drain.Counts.UnselectedMaintenanceNoiseBacklogItemCount}");
        }

        return context.Count == 0
            ? $"Structural backlog pressure remains: {string.Join(", ", blockers)}."
            : $"Structural backlog pressure remains: {string.Join(", ", blockers)}. Non-blocking residue stays visible as {string.Join(", ", context)}.";
    }

    private static string BuildProgramWaveNecessitySummary(
        RuntimeHotspotBacklogDrainSurface drain,
        RuntimeHotspotCrossFamilyPatternSurface crossFamily)
    {
        if (drain.Counts.UnselectedClosureRelevantBacklogItemCount > 0)
        {
            return $"Queue-family closure is satisfied, but a bounded unselected-backlog disposition card remains necessary because closure-relevant residue is still projected: unselected_closure_relevant={drain.Counts.UnselectedClosureRelevantBacklogItemCount}, unselected_maintenance_noise={drain.Counts.UnselectedMaintenanceNoiseBacklogItemCount}, residual_patterns={crossFamily.Counts.ResidualPatternCount}, repeated_backlog_kinds={crossFamily.Counts.RepeatedBacklogKindPatternCount}.";
        }

        return $"Queue-family closure and unselected backlog disposition are satisfied for the current residual program, but a bounded cross-family pattern disposition card remains necessary: residual_patterns={crossFamily.Counts.ResidualPatternCount}, repeated_backlog_kinds={crossFamily.Counts.RepeatedBacklogKindPatternCount}, unselected_maintenance_noise={drain.Counts.UnselectedMaintenanceNoiseBacklogItemCount}.";
    }

    private ProgramClosureReviewRecord? TryLoadClosureReview(List<string> errors)
    {
        var fullPath = Path.Combine(repoRoot, ProgramClosureReviewRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(fullPath);
            var record = JsonSerializer.Deserialize<ProgramClosureReviewRecord>(json, ClosureReviewJsonOptions);
            if (record is null || string.IsNullOrWhiteSpace(record.Outcome))
            {
                errors.Add($"Closure review record '{ProgramClosureReviewRelativePath}' is invalid.");
                return null;
            }

            return record;
        }
        catch (Exception ex)
        {
            errors.Add($"Closure review record '{ProgramClosureReviewRelativePath}' could not be loaded: {ex.Message}");
            return null;
        }
    }

    private static bool IsApprovedClosureReview(ProgramClosureReviewRecord? closureReview)
    {
        return closureReview is not null
            && string.Equals(closureReview.Outcome, "approved_for_closure", StringComparison.Ordinal);
    }

    private static int? CalculateAgeDays(DateTimeOffset? generatedAt, DateTimeOffset now)
    {
        if (generatedAt is null)
        {
            return null;
        }

        return Math.Max(0, (int)Math.Floor((now - generatedAt.Value).TotalDays));
    }

    private static string ResolveFreshness(DateTimeOffset? generatedAt, DateTimeOffset now)
    {
        var ageDays = CalculateAgeDays(generatedAt, now);
        if (ageDays is null)
        {
            return "missing";
        }

        return ageDays.Value <= SupportingReportFreshnessMaxAgeDays
            ? "fresh"
            : "stale";
    }

    private static string FormatAgeDays(int? ageDays)
    {
        return ageDays?.ToString() ?? "n/a";
    }

    private string ToRepoRelative(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Path.GetRelativePath(repoRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private sealed class ProgramClosureReviewRecord
    {
        public DateTimeOffset RecordedAt { get; init; }

        public string Outcome { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;

        public string SourceSurfaceId { get; init; } = string.Empty;

        public string SourceOverallVerdict { get; init; } = string.Empty;

        public string SourceContinuationGateOutcome { get; init; } = string.Empty;
    }
}
