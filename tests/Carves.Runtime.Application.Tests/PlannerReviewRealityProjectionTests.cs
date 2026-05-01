using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class PlannerReviewRealityProjectionTests
{
    [Fact]
    public void ReviewArtifactFactory_RecordsProtoRealityProjectionForValidatedReviewBoundary()
    {
        var factory = new PlannerReviewArtifactFactory();
        var task = new TaskNode
        {
            TaskId = "T-REALITY-001",
            Title = "Add proof-target guard",
            Scope = ["src/CARVES.Runtime.Application/Planning/PlannerProposalValidator.cs"],
            Metadata = PlanningProofTargetMetadata.Merge(
                new Dictionary<string, string>(StringComparer.Ordinal),
                new RealityProofTarget
                {
                    Kind = ProofTargetKind.Boundary,
                    Description = "Admission explicitly records bounded proof for scoped execution work.",
                }),
        };
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            Validation = new ValidationResult
            {
                Passed = true,
                Evidence = ["targeted planning validation passed"],
            },
            SafetyDecision = SafetyDecision.Allow(task.TaskId),
        };

        var artifact = factory.Create(
            task,
            report,
            new PlannerReview
            {
                Verdict = PlannerVerdict.Continue,
                Reason = "Validated and ready for review.",
                AcceptanceMet = true,
            },
            new TaskTransitionDecision(DomainTaskStatus.Review, false, "Successful execution must stop at the review boundary."));

        Assert.Equal(SolidityClass.Proto, artifact.RealityProjection.SolidityClass);
        Assert.Equal("review_ready", artifact.RealityProjection.PromotionResult);
        Assert.Equal("boundary", System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(artifact.RealityProjection.ProofTarget!.Kind.ToString()));
        Assert.Contains("targeted planning validation passed", artifact.RealityProjection.VerifiedOutcome, StringComparison.Ordinal);
        Assert.Equal("worker", artifact.ClosureBundle.CandidateResultSource);
        Assert.Equal("review_pending", artifact.ClosureBundle.WorkerResultVerdict);
        Assert.Equal("none", artifact.ClosureBundle.AcceptedPatchSource);
        Assert.Equal("worker_review_pending", artifact.ClosureBundle.CompletionMode);
        Assert.Equal("pending_review", artifact.ClosureBundle.ReviewerDecision);
        Assert.Equal("passed", artifact.ClosureBundle.Validation.RequiredGateStatus);
        Assert.Equal(1, artifact.ClosureBundle.Validation.EvidenceCount);
        Assert.Equal("review_required_before_writeback", artifact.ClosureBundle.WritebackRecommendation);
    }

    [Fact]
    public void ReviewArtifactFactory_BuildsResidueContractMatrixForCard972()
    {
        var factory = new PlannerReviewArtifactFactory();
        var task = new TaskNode
        {
            TaskId = "T-CARD-972-001",
            CardId = "CARD-972",
            Title = "Close recoverable residue contract",
            Description = "Make lock and residue fields persist, read back, and project through review surfaces.",
            Scope =
            [
                "src/CARVES.Runtime.Application/ControlPlane/ControlPlaneResidueContract.cs",
                "src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs",
                "src/CARVES.Runtime.Infrastructure/ControlPlane/ControlPlaneLockService.cs",
                "src/CARVES.Runtime.Application/Planning/ManagedWorkspaceLeaseService.cs",
                "src/CARVES.Runtime.Application/Platform/RuntimeProductClosurePilotStatusService.cs",
            ],
            Acceptance =
            [
                "Residue severities stay in the warning/error vocabulary.",
                "A write/read roundtrip proves the persisted fields are visible on readback surfaces.",
            ],
        };
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            Patch = new PatchSummary(
                FilesChanged: 5,
                LinesAdded: 96,
                LinesRemoved: 4,
                Estimated: false,
                Paths:
                [
                    "src/CARVES.Runtime.Application/ControlPlane/ControlPlaneResidueContract.cs",
                    "src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs",
                    "src/CARVES.Runtime.Infrastructure/ControlPlane/ControlPlaneLockService.cs",
                    "src/CARVES.Runtime.Application/Planning/ManagedWorkspaceLeaseService.cs",
                    "src/CARVES.Runtime.Application/Platform/RuntimeProductClosurePilotStatusService.cs",
                ]),
            Validation = new ValidationResult
            {
                Passed = true,
                Evidence =
                [
                    "ControlPlaneContentionTests roundtrip validation passed",
                    "ManagedWorkspaceLeaseServiceTests persist -> read validation passed",
                ],
            },
            SafetyDecision = SafetyDecision.Allow(task.TaskId),
        };

        var artifact = factory.Create(
            task,
            report,
            new PlannerReview
            {
                Verdict = PlannerVerdict.Continue,
                Reason = "Residue contract candidate is ready for review.",
                AcceptanceMet = true,
            },
            new TaskTransitionDecision(DomainTaskStatus.Review, false, "Successful execution must stop at the review boundary."));

        var matrix = artifact.ClosureBundle.ContractMatrix;
        Assert.Equal("runtime_recoverable_residue_contract_v1", matrix.ProfileId);
        Assert.Equal("passed", matrix.Status);
        Assert.Empty(matrix.Blockers);
        Assert.Contains(matrix.Checks, check => check.CheckId == "residue_contract_schema_presence" && check.Status == "passed");
        Assert.Contains(matrix.Checks, check => check.CheckId == "persistence_readback_wiring" && check.Status == "passed");
        Assert.Contains(matrix.Checks, check => check.CheckId == "review_surface_projection" && check.Status == "passed");
        Assert.Contains(matrix.Checks, check => check.CheckId == "severity_vocabulary_invariant" && check.Status == "passed");
        Assert.Contains(matrix.Checks, check => check.CheckId == "roundtrip_validation_evidence" && check.Status == "passed");
        Assert.Contains(matrix.Checks, check => check.CheckId == "scope_hygiene" && check.Status == "passed");
        Assert.False(artifact.ClosureBundle.ClosureDecision.WritebackAllowed);
        Assert.Contains("reviewer_decision_not_approved:pending_review", artifact.ClosureBundle.ClosureDecision.Blockers);
    }

    [Fact]
    public void ReviewArtifactFactory_SeparatesRequiredValidationFromKnownRedAdvisoryFullSuite()
    {
        var factory = new PlannerReviewArtifactFactory();
        var task = new TaskNode
        {
            TaskId = "T-CARD-972-001",
            CardId = "CARD-972",
            Title = "Close recoverable residue contract",
            Scope =
            [
                "src/CARVES.Runtime.Application/ControlPlane/ControlPlaneResidueContract.cs",
                "src/CARVES.Runtime.Infrastructure/ControlPlane/ControlPlaneLockService.cs",
            ],
        };
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            Patch = new PatchSummary(
                FilesChanged: 2,
                LinesAdded: 24,
                LinesRemoved: 0,
                Estimated: false,
                Paths:
                [
                    "src/CARVES.Runtime.Application/ControlPlane/ControlPlaneResidueContract.cs",
                    "src/CARVES.Runtime.Infrastructure/ControlPlane/ControlPlaneLockService.cs",
                ]),
            Validation = new ValidationResult
            {
                Passed = true,
                Evidence =
                [
                    "focused tests passed: PlannerReviewRealityProjectionTests",
                    "ControlPlaneContentionTests roundtrip validation passed",
                    "full dotnet test advisory failed: known red baseline unrelated",
                    "known-red baseline checked: existing failures are not caused by this patch",
                ],
                CommandResults =
                [
                    new CommandExecutionRecord(
                        ["dotnet", "test", "tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj", "--filter", "FullyQualifiedName~PlannerReviewRealityProjectionTests"],
                        0,
                        "passed",
                        string.Empty,
                        false,
                        "/tmp/carves-runtime",
                        "validation",
                        DateTimeOffset.UtcNow),
                    new CommandExecutionRecord(
                        ["dotnet", "test"],
                        1,
                        string.Empty,
                        "known red baseline: existing failures outside touched scope",
                        false,
                        "/tmp/carves-runtime",
                        "advisory-validation",
                        DateTimeOffset.UtcNow),
                ],
            },
            SafetyDecision = SafetyDecision.Allow(task.TaskId),
        };

        var artifact = factory.Create(
            task,
            report,
            new PlannerReview
            {
                Verdict = PlannerVerdict.Continue,
                Reason = "Focused validation passed; full suite is advisory known-red noise.",
                AcceptanceMet = true,
            },
            new TaskTransitionDecision(DomainTaskStatus.Review, false, "Successful execution must stop at the review boundary."));

        var validation = artifact.ClosureBundle.Validation;
        Assert.Equal("passed", validation.RequiredGateStatus);
        Assert.Equal("known_red_advisory_failure", validation.AdvisoryGateStatus);
        Assert.Equal("recorded", validation.KnownRedBaselineStatus);
        Assert.Equal("runtime_recoverable_residue_validation_profile_v1", validation.Profile.ProfileId);
        Assert.Equal("passed", validation.Profile.Status);
        Assert.Equal("known_red_advisory_only", validation.Profile.FailureAttribution);
        Assert.Empty(validation.Profile.Blockers);
        Assert.Contains(validation.Profile.RequiredGates, gate => gate.GateId == "focused_required_validation" && gate.Status == "passed" && gate.Blocking);
        Assert.Contains(validation.Profile.RequiredGates, gate => gate.GateId == "contract_roundtrip_required" && gate.Status == "passed" && gate.Blocking);
        Assert.Contains(validation.Profile.AdvisoryGates, gate => gate.GateId == "full_suite_advisory" && gate.Status == "known_red_advisory_failure" && !gate.Blocking);
    }

    [Fact]
    public void ReviewArtifactFactory_ProjectsWorkerCompletionClaimIntoClosureBundle()
    {
        var factory = new PlannerReviewArtifactFactory();
        var task = new TaskNode
        {
            TaskId = "T-CLOSURE-CLAIM-001",
            Title = "Record worker completion claim",
            Scope = ["src/CARVES.Runtime.Application/Workers/WorkerCompletionClaimExtractor.cs"],
        };
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            Patch = new PatchSummary(
                FilesChanged: 1,
                LinesAdded: 24,
                LinesRemoved: 0,
                Estimated: false,
                Paths: ["src/CARVES.Runtime.Application/Workers/WorkerCompletionClaimExtractor.cs"]),
            Validation = new ValidationResult
            {
                Passed = true,
                Evidence = ["focused tests passed: WorkerCompletionClaimExtractorTests"],
            },
            SafetyDecision = SafetyDecision.Allow(task.TaskId),
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = task.TaskId,
                Status = WorkerExecutionStatus.Succeeded,
                CompletionClaim = new WorkerCompletionClaim
                {
                    Required = true,
                    Status = "present",
                    PacketId = "WEP-T-CLOSURE-CLAIM-001-v1",
                    SourceExecutionPacketId = "EP-T-CLOSURE-CLAIM-001-v1",
                    PacketValidationStatus = "passed",
                    PresentFields =
                    [
                        "changed_files",
                        "contract_items_satisfied",
                        "tests_run",
                        "evidence_paths",
                        "known_limitations",
                        "next_recommendation",
                    ],
                    ChangedFiles = ["src/CARVES.Runtime.Application/Workers/WorkerCompletionClaimExtractor.cs"],
                    ContractItemsSatisfied = ["patch_scope_recorded", "validation_recorded"],
                    TestsRun = ["dotnet test tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj --filter FullyQualifiedName~WorkerCompletionClaimExtractorTests"],
                    EvidencePaths = [".ai/artifacts/worker-executions/T-CLOSURE-CLAIM-001.json"],
                    KnownLimitations = ["none"],
                    NextRecommendation = "submit for Host review",
                    RawClaimHash = "claim-hash",
                },
            },
        };

        var artifact = factory.Create(
            task,
            report,
            new PlannerReview
            {
                Verdict = PlannerVerdict.Continue,
                Reason = "Completion claim is projected as review evidence.",
                AcceptanceMet = true,
            },
            new TaskTransitionDecision(DomainTaskStatus.Review, false, "Successful execution must stop at the review boundary."));

        var claim = artifact.ClosureBundle.CompletionClaim;
        Assert.Equal("present", claim.Status);
        Assert.True(claim.Required);
        Assert.Empty(claim.MissingFields);
        Assert.Contains("contract_items_satisfied", claim.PresentFields);
        Assert.Equal(["src/CARVES.Runtime.Application/Workers/WorkerCompletionClaimExtractor.cs"], claim.ChangedFiles);
        Assert.Contains("patch_scope_recorded", claim.ContractItemsSatisfied);
        Assert.Equal("claim-hash", claim.RawClaimHash);
        Assert.Equal("passed", artifact.ClosureBundle.HostValidation.Status);
        Assert.True(artifact.ClosureBundle.HostValidation.Required);
        Assert.Equal("valid", artifact.ClosureBundle.HostValidation.ReasonCode);
        Assert.Equal("WEP-T-CLOSURE-CLAIM-001-v1", artifact.ClosureBundle.HostValidation.WorkerPacketId);
        Assert.Contains("reviewer_decision_not_approved:pending_review", artifact.ClosureBundle.ClosureDecision.Blockers);
    }

    [Fact]
    public void ReviewArtifactFactory_BlocksApprovedWritebackWhenCompletionClaimFailsPacketValidation()
    {
        var factory = new PlannerReviewArtifactFactory();
        var task = new TaskNode
        {
            TaskId = "T-CLOSURE-CLAIM-INVALID",
            Title = "Block invalid worker completion claim",
            Scope = ["src/CARVES.Runtime.Application/Workers/WorkerCompletionClaimExtractor.cs"],
        };
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            Patch = new PatchSummary(
                FilesChanged: 1,
                LinesAdded: 12,
                LinesRemoved: 0,
                Estimated: false,
                Paths: ["src/CARVES.Runtime.Application/Workers/WorkerCompletionClaimExtractor.cs"]),
            Validation = new ValidationResult
            {
                Passed = true,
                Evidence = ["focused tests passed: WorkerCompletionClaimExtractorTests"],
            },
            SafetyDecision = SafetyDecision.Allow(task.TaskId),
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = task.TaskId,
                Status = WorkerExecutionStatus.Succeeded,
                CompletionClaim = new WorkerCompletionClaim
                {
                    Required = true,
                    Status = "invalid",
                    PacketId = "WEP-T-CLOSURE-CLAIM-INVALID-v1",
                    PacketValidationStatus = "failed",
                    PacketValidationBlockers =
                    [
                        "completion_claim_missing_contract_item:scope_hygiene",
                    ],
                    PresentFields =
                    [
                        "changed_files",
                        "contract_items_satisfied",
                        "tests_run",
                        "evidence_paths",
                        "known_limitations",
                        "next_recommendation",
                    ],
                    ChangedFiles = ["src/CARVES.Runtime.Application/Workers/WorkerCompletionClaimExtractor.cs"],
                    ContractItemsSatisfied = ["patch_scope_recorded"],
                    RequiredContractItems = ["patch_scope_recorded", "scope_hygiene"],
                    MissingContractItems = ["scope_hygiene"],
                    TestsRun = ["dotnet test tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj --filter FullyQualifiedName~WorkerCompletionClaimExtractorTests"],
                    EvidencePaths = [".ai/artifacts/worker-executions/T-CLOSURE-CLAIM-INVALID.json"],
                    KnownLimitations = ["none"],
                    NextRecommendation = "resubmit completion claim with scope_hygiene",
                },
            },
        };
        var reviewArtifact = factory.Create(
            task,
            report,
            new PlannerReview
            {
                Verdict = PlannerVerdict.Continue,
                Reason = "Invalid claim should be visible at review.",
                AcceptanceMet = true,
            },
            new TaskTransitionDecision(DomainTaskStatus.Review, false, "Successful execution must stop at the review boundary."));

        var approved = factory.RecordDecision(
            reviewArtifact,
            task,
            new PlannerReview
            {
                Verdict = PlannerVerdict.Complete,
                Reason = "Reviewer approves the patch, but claim packet validation must still block writeback.",
                DecisionStatus = ReviewDecisionStatus.Approved,
                AcceptanceMet = true,
            },
            DomainTaskStatus.Completed,
            "Approved after focused validation.");

        var closureDecision = approved.ClosureBundle.ClosureDecision;
        var hostValidation = approved.ClosureBundle.HostValidation;
        Assert.Equal("failed", hostValidation.Status);
        Assert.True(hostValidation.Required);
        Assert.Equal("completion_claim_not_present", hostValidation.ReasonCode);
        Assert.Contains("completion_claim_not_present:invalid", hostValidation.Blockers);
        Assert.Contains("completion_claim_source_execution_packet_id_missing", hostValidation.Blockers);
        Assert.Contains("completion_claim_packet_validation_not_passed:failed", hostValidation.Blockers);
        Assert.Contains("completion_claim_packet:completion_claim_missing_contract_item:scope_hygiene", hostValidation.Blockers);
        Assert.False(closureDecision.WritebackAllowed);
        Assert.Equal("writeback_blocked", closureDecision.Status);
        Assert.Contains("completion_claim_not_present:invalid", closureDecision.Blockers);
        Assert.Contains("completion_claim_packet_validation_failed", closureDecision.Blockers);
        Assert.Contains("completion_claim_packet:completion_claim_missing_contract_item:scope_hygiene", closureDecision.Blockers);
        Assert.Contains("host_validation_not_passed:failed", closureDecision.Blockers);
        Assert.Contains("host_validation:completion_claim_source_execution_packet_id_missing", closureDecision.Blockers);
    }

    [Fact]
    public void ReviewArtifactFactory_AllowsWritebackOnlyAfterApprovedClosureDecision()
    {
        var factory = new PlannerReviewArtifactFactory();
        var task = new TaskNode
        {
            TaskId = "T-CARD-972-001",
            CardId = "CARD-972",
            Title = "Close recoverable residue contract",
            Description = "Make lock and residue fields persist, read back, and project through review surfaces.",
            Scope =
            [
                "src/CARVES.Runtime.Application/ControlPlane/ControlPlaneResidueContract.cs",
                "src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs",
                "src/CARVES.Runtime.Infrastructure/ControlPlane/ControlPlaneLockService.cs",
                "src/CARVES.Runtime.Application/Planning/ManagedWorkspaceLeaseService.cs",
                "src/CARVES.Runtime.Application/Platform/RuntimeProductClosurePilotStatusService.cs",
            ],
        };
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            Patch = new PatchSummary(
                FilesChanged: 5,
                LinesAdded: 96,
                LinesRemoved: 4,
                Estimated: false,
                Paths:
                [
                    "src/CARVES.Runtime.Application/ControlPlane/ControlPlaneResidueContract.cs",
                    "src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockLeaseSnapshot.cs",
                    "src/CARVES.Runtime.Infrastructure/ControlPlane/ControlPlaneLockService.cs",
                    "src/CARVES.Runtime.Application/Planning/ManagedWorkspaceLeaseService.cs",
                    "src/CARVES.Runtime.Application/Platform/RuntimeProductClosurePilotStatusService.cs",
                ]),
            Validation = new ValidationResult
            {
                Passed = true,
                Evidence =
                [
                    "focused tests passed: PlannerReviewRealityProjectionTests",
                    "ControlPlaneContentionTests roundtrip validation passed",
                    "ManagedWorkspaceLeaseServiceTests persist -> read validation passed",
                ],
            },
            SafetyDecision = SafetyDecision.Allow(task.TaskId),
        };
        var reviewArtifact = factory.Create(
            task,
            report,
            new PlannerReview
            {
                Verdict = PlannerVerdict.Continue,
                Reason = "Residue contract candidate is ready for review.",
                AcceptanceMet = true,
            },
            new TaskTransitionDecision(DomainTaskStatus.Review, false, "Successful execution must stop at the review boundary."));

        var approved = factory.RecordDecision(
            reviewArtifact,
            task,
            new PlannerReview
            {
                Verdict = PlannerVerdict.Complete,
                Reason = "Approved after focused validation and contract matrix passed.",
                DecisionStatus = ReviewDecisionStatus.Approved,
                AcceptanceMet = true,
            },
            DomainTaskStatus.Completed,
            "Approved after focused validation and contract matrix passed.");

        var closureDecision = approved.ClosureBundle.ClosureDecision;
        Assert.True(closureDecision.WritebackAllowed);
        Assert.Equal("writeback_allowed", closureDecision.Status);
        Assert.Equal("allow_writeback", closureDecision.Decision);
        Assert.Equal("worker_patch", closureDecision.AcceptedPatchSource);
        Assert.Equal("accepted", closureDecision.WorkerResultVerdict);
        Assert.Equal("approved", closureDecision.ReviewerDecision);
        Assert.Equal("passed", closureDecision.RequiredGateStatus);
        Assert.Equal("passed", closureDecision.ContractMatrixStatus);
        Assert.Equal("passed", closureDecision.SafetyStatus);
        Assert.Equal("not_required", closureDecision.HostValidationStatus);
        Assert.Equal("not_required", approved.ClosureBundle.HostValidation.Status);
        Assert.Empty(closureDecision.Blockers);
    }

    [Fact]
    public void WorkbenchTaskSurface_ProjectsRealityFromReviewArtifactWithoutSecondAuthority()
    {
        using var workspace = new TemporaryWorkspace();
        var task = new TaskNode
        {
            TaskId = "T-REALITY-002",
            CardId = "CARD-REALITY",
            Title = "Project reality through workbench",
            Description = "Expose ghost proto solid on read-only task surfaces.",
            Status = DomainTaskStatus.Review,
            Scope = ["src/CARVES.Runtime.Application/Platform/WorkbenchSurfaceService.cs"],
            Acceptance = ["reality projection is visible"],
        };
        var taskGraph = new DomainTaskGraph([task]);
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(taskGraph), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var planningDraftService = new PlanningDraftService(
            workspace.Paths,
            taskGraphService,
            new JsonCardDraftRepository(workspace.Paths),
            new JsonTaskGraphDraftRepository(workspace.Paths));
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SavePlannerReviewArtifact(new PlannerReviewArtifact
        {
            TaskId = task.TaskId,
            Review = new PlannerReview
            {
                Verdict = PlannerVerdict.Continue,
                Reason = "Validated and queued for review.",
                AcceptanceMet = true,
            },
            ResultingStatus = DomainTaskStatus.Review,
            TransitionReason = "Successful execution must stop at the review boundary.",
            PlannerComment = "Validated and queued for review.",
            PatchSummary = "files=1; added=12; removed=0; estimated=False; paths=src/CARVES.Runtime.Application/Platform/WorkbenchSurfaceService.cs",
            ValidationPassed = true,
            ValidationEvidence = ["targeted workbench projection validation passed"],
            SafetyOutcome = SafetyOutcome.Allow,
            SafetyIssues = [],
            DecisionStatus = ReviewDecisionStatus.PendingReview,
            RealityProjection = new ReviewRealityProjection
            {
                SolidityClass = SolidityClass.Proto,
                PromotionResult = "review_ready",
                PlannedScope = "src/CARVES.Runtime.Application/Platform/WorkbenchSurfaceService.cs",
                VerifiedOutcome = "targeted workbench projection validation passed",
                ProofTarget = new RealityProofTarget
                {
                    Kind = ProofTargetKind.Boundary,
                    Description = "Workbench task read model exposes reality gradient without becoming a second authority.",
                },
            },
        });

        var service = new WorkbenchSurfaceService(
            workspace.RootPath,
            workspace.Paths,
            taskGraphService,
            planningDraftService,
            null!,
            null!,
            null!,
            new ExecutionRunService(workspace.Paths),
            artifactRepository,
            null!,
            null!,
            new StubGitClient(),
            maxParallelTasks: 1);

        var surface = service.BuildTask(task.TaskId);

        Assert.Equal("proto", surface.Reality.Status);
        Assert.Contains("planned=src/CARVES.Runtime.Application/Platform/WorkbenchSurfaceService.cs", surface.Reality.Summary, StringComparison.Ordinal);
        Assert.Contains("promotion=review_ready", surface.Reality.Summary, StringComparison.Ordinal);
        Assert.Contains("verified=targeted workbench projection validation passed", surface.Reality.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewArtifactFactory_ProjectsProvisionalAcceptanceWithoutPromotingToSolid()
    {
        var factory = new PlannerReviewArtifactFactory();
        var artifact = factory.RecordDecision(
            new PlannerReviewArtifact
            {
                TaskId = "T-REALITY-003",
                Review = new PlannerReview
                {
                    Verdict = PlannerVerdict.PauseForReview,
                    Reason = "Waiting for bounded debt closure.",
                    DecisionStatus = ReviewDecisionStatus.PendingReview,
                    AcceptanceMet = true,
                },
                ResultingStatus = DomainTaskStatus.Review,
                TransitionReason = "Validated work stopped at review boundary.",
                PlannerComment = "Validated work stopped at review boundary.",
                PatchSummary = "files=1; added=3; removed=0; estimated=False; paths=src/CARVES.Runtime.Application/Platform/WorkbenchSurfaceService.cs",
                ValidationPassed = true,
                ValidationEvidence = ["targeted validation passed"],
                SafetyOutcome = SafetyOutcome.Allow,
                DecisionStatus = ReviewDecisionStatus.PendingReview,
                RealityProjection = new ReviewRealityProjection
                {
                    SolidityClass = SolidityClass.Proto,
                    PromotionResult = "review_ready",
                    PlannedScope = "src/CARVES.Runtime.Application/Platform/WorkbenchSurfaceService.cs",
                    VerifiedOutcome = "targeted validation passed",
                    ProofTarget = new RealityProofTarget
                    {
                        Kind = ProofTargetKind.Boundary,
                        Description = "Reality remains proto while provisional debt is open.",
                    },
                },
            },
            new PlannerReview
            {
                Verdict = PlannerVerdict.PauseForReview,
                Reason = "Ship the slice, then close one bounded follow-up.",
                DecisionStatus = ReviewDecisionStatus.ProvisionalAccepted,
                AcceptanceMet = true,
                DecisionDebt = new ReviewDecisionDebt
                {
                    Summary = "Close the remaining follow-up before final acceptance.",
                    FollowUpActions = ["Run the narrowed follow-up review."],
                    RequiresFollowUpReview = true,
                },
            },
            DomainTaskStatus.Review,
            "Ship the slice, then close one bounded follow-up.");

        Assert.Equal(ReviewDecisionStatus.ProvisionalAccepted, artifact.DecisionStatus);
        Assert.Equal("provisional_accepted", artifact.RealityProjection.PromotionResult);
        Assert.Equal(SolidityClass.Proto, artifact.RealityProjection.SolidityClass);
        Assert.Equal("Close the remaining follow-up before final acceptance.", artifact.DecisionDebt?.Summary);
    }

    [Fact]
    public void ReviewArtifactFactory_ProjectsReopenedDecisionBackToProtoReality()
    {
        var factory = new PlannerReviewArtifactFactory();
        var artifact = factory.RecordDecision(
            new PlannerReviewArtifact
            {
                TaskId = "T-REALITY-004",
                Review = new PlannerReview
                {
                    Verdict = PlannerVerdict.Complete,
                    Reason = "Previously accepted.",
                    DecisionStatus = ReviewDecisionStatus.Approved,
                    AcceptanceMet = true,
                },
                ResultingStatus = DomainTaskStatus.Completed,
                TransitionReason = "Human approval previously promoted this slice.",
                PlannerComment = "Human approval previously promoted this slice.",
                PatchSummary = "files=1; added=4; removed=0; estimated=False; paths=src/CARVES.Runtime.Application/Platform/WorkbenchSurfaceService.cs",
                ValidationPassed = true,
                ValidationEvidence = ["targeted validation passed"],
                SafetyOutcome = SafetyOutcome.Allow,
                DecisionStatus = ReviewDecisionStatus.Approved,
                RealityProjection = new ReviewRealityProjection
                {
                    SolidityClass = SolidityClass.Solid,
                    PromotionResult = "promoted",
                    PlannedScope = "src/CARVES.Runtime.Application/Platform/WorkbenchSurfaceService.cs",
                    VerifiedOutcome = "targeted validation passed",
                    ProofTarget = new RealityProofTarget
                    {
                        Kind = ProofTargetKind.Boundary,
                        Description = "Reality should drop back to proto when reopened.",
                    },
                },
            },
            new PlannerReview
            {
                Verdict = PlannerVerdict.PauseForReview,
                Reason = "Acceptance needs one more human pass.",
                DecisionStatus = ReviewDecisionStatus.Reopened,
                AcceptanceMet = false,
            },
            DomainTaskStatus.Review,
            "Acceptance needs one more human pass.");

        Assert.Equal(ReviewDecisionStatus.Reopened, artifact.DecisionStatus);
        Assert.Equal("reopened", artifact.RealityProjection.PromotionResult);
        Assert.Equal(SolidityClass.Proto, artifact.RealityProjection.SolidityClass);
        Assert.Contains("Reopened", artifact.RealityProjection.VerifiedOutcome, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewArtifactFactory_RecordsManualFallbackClosureBundleWithoutCreditingWorkerPatch()
    {
        var factory = new PlannerReviewArtifactFactory();
        var task = new TaskNode
        {
            TaskId = "T-REALITY-005-MANUAL",
            Status = DomainTaskStatus.Completed,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["completion_provenance"] = "manual_fallback",
                ["completion_historical_worker_failure_kind"] = WorkerFailureKind.TaskLogicFailed.ToString(),
                ["completion_historical_worker_backend"] = "codex_cli",
                ["fallback_run_packet_role_switch_receipt"] = "manual_fallback_review_boundary_receipt",
                ["fallback_run_packet_context_receipt"] = "worker-run-history-001",
                ["fallback_run_packet_execution_claim"] = "manual_fallback_execution_claim:T-REALITY-005-MANUAL",
                ["fallback_run_packet_review_bundle"] = ".ai/artifacts/reviews/T-REALITY-005-MANUAL.json#closure_bundle",
            },
        };

        var artifact = factory.RecordDecision(
            new PlannerReviewArtifact
            {
                TaskId = task.TaskId,
                Review = new PlannerReview
                {
                    Verdict = PlannerVerdict.Continue,
                    Reason = "Worker patch reached review but did not close the task.",
                    DecisionStatus = ReviewDecisionStatus.PendingReview,
                    AcceptanceMet = false,
                },
                ResultingStatus = DomainTaskStatus.Review,
                TransitionReason = "Semantic worker failure requires review.",
                PlannerComment = "Worker patch reached review but did not close the task.",
                PatchSummary = "files=4; added=18; removed=0; estimated=False; paths=src/CARVES.Runtime.Application/ControlPlane/ControlPlaneLockHandle.cs",
                ValidationPassed = false,
                ValidationEvidence = ["dotnet build passed", "dotnet test failed"],
                SafetyOutcome = SafetyOutcome.Allow,
                DecisionStatus = ReviewDecisionStatus.PendingReview,
                ClosureBundle = new ReviewClosureBundle
                {
                    CandidateResultSource = "worker",
                    WorkerResultVerdict = "rejected_validation_failed",
                    AcceptedPatchSource = "none",
                    CompletionMode = "worker_review_pending",
                    ReviewerDecision = "pending_review",
                    Validation = new ReviewClosureValidationSummary
                    {
                        RequiredGateStatus = "failed",
                        EvidenceCount = 2,
                    },
                    WritebackRecommendation = "review_required_before_writeback",
                },
            },
            task,
            new PlannerReview
            {
                Verdict = PlannerVerdict.Complete,
                Reason = "Accepted the bounded manual fallback patch after worker review.",
                DecisionStatus = ReviewDecisionStatus.Approved,
                AcceptanceMet = true,
            },
            DomainTaskStatus.Completed,
            "Accepted the bounded manual fallback patch after worker review.");

        Assert.Equal("worker", artifact.ClosureBundle.CandidateResultSource);
        Assert.Equal("rejected_semantic_incomplete", artifact.ClosureBundle.WorkerResultVerdict);
        Assert.Equal("manual_fallback_patch", artifact.ClosureBundle.AcceptedPatchSource);
        Assert.Equal("manual_fallback_after_worker_review", artifact.ClosureBundle.CompletionMode);
        Assert.Equal("approved", artifact.ClosureBundle.ReviewerDecision);
        Assert.Equal("failed", artifact.ClosureBundle.Validation.RequiredGateStatus);
        Assert.Equal(2, artifact.ClosureBundle.Validation.EvidenceCount);
        Assert.True(artifact.ClosureBundle.FallbackRunPacket.Required);
        Assert.Equal("complete", artifact.ClosureBundle.FallbackRunPacket.Status);
        Assert.True(artifact.ClosureBundle.FallbackRunPacket.StrictlyRequired);
        Assert.True(artifact.ClosureBundle.FallbackRunPacket.ClosureBlockerWhenIncomplete);
        Assert.Empty(artifact.ClosureBundle.FallbackRunPacket.MissingReceipts);
        Assert.Equal("manual_fallback_recorded", artifact.ClosureBundle.WritebackRecommendation);
    }

    [Fact]
    public void ReviewArtifactFactory_BlocksManualFallbackClosureWhenFallbackRunPacketIsIncomplete()
    {
        var factory = new PlannerReviewArtifactFactory();
        var task = new TaskNode
        {
            TaskId = "T-REALITY-005-MANUAL-INCOMPLETE",
            Status = DomainTaskStatus.Completed,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["completion_provenance"] = "manual_fallback",
                ["completion_historical_worker_failure_kind"] = WorkerFailureKind.TaskLogicFailed.ToString(),
            },
        };

        var artifact = factory.RecordDecision(
            new PlannerReviewArtifact
            {
                TaskId = task.TaskId,
                ValidationPassed = true,
                ValidationEvidence = ["focused tests passed"],
                SafetyOutcome = SafetyOutcome.Allow,
            },
            task,
            new PlannerReview
            {
                Verdict = PlannerVerdict.Complete,
                Reason = "Accepted manual fallback without packet receipts.",
                DecisionStatus = ReviewDecisionStatus.Approved,
                AcceptanceMet = true,
            },
            DomainTaskStatus.Completed,
            "Accepted manual fallback without packet receipts.");

        Assert.True(artifact.ClosureBundle.FallbackRunPacket.Required);
        Assert.Equal("incomplete", artifact.ClosureBundle.FallbackRunPacket.Status);
        Assert.True(artifact.ClosureBundle.FallbackRunPacket.StrictlyRequired);
        Assert.True(artifact.ClosureBundle.FallbackRunPacket.ClosureBlockerWhenIncomplete);
        Assert.Contains("role_switch_receipt", artifact.ClosureBundle.FallbackRunPacket.MissingReceipts);
        Assert.Contains("context_receipt", artifact.ClosureBundle.FallbackRunPacket.MissingReceipts);
        Assert.Contains("execution_claim", artifact.ClosureBundle.FallbackRunPacket.MissingReceipts);
        Assert.False(artifact.ClosureBundle.ClosureDecision.WritebackAllowed);
        Assert.Contains("fallback_run_packet_incomplete", artifact.ClosureBundle.ClosureDecision.Blockers);
        Assert.Contains("fallback_run_packet_missing:execution_claim", artifact.ClosureBundle.ClosureDecision.Blockers);
    }

    [Fact]
    public void WorkbenchTaskSurface_ProjectsReviewEvidenceGateBeforeApproval()
    {
        using var workspace = new TemporaryWorkspace();
        var task = new TaskNode
        {
            TaskId = "T-REALITY-005",
            CardId = "CARD-REALITY",
            Title = "Project review evidence into workbench",
            Description = "Show predicted missing evidence before human approval.",
            Status = DomainTaskStatus.Review,
            Scope = ["src/CARVES.Runtime.Application/ControlPlane/ReviewEvidenceProjectionService.cs"],
            Acceptance = ["review evidence is visible"],
            AcceptanceContract = new AcceptanceContract
            {
                ContractId = "AC-T-REALITY-005",
                Title = "Acceptance contract for workbench evidence",
                Status = AcceptanceContractLifecycleStatus.HumanReview,
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement
                    {
                        Type = "result_commit",
                        Description = "Workbench should expose a missing result commit before approval.",
                    },
                ],
            },
        };
        var taskGraph = new DomainTaskGraph([task]);
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(taskGraph), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var planningDraftService = new PlanningDraftService(
            workspace.Paths,
            taskGraphService,
            new JsonCardDraftRepository(workspace.Paths),
            new JsonTaskGraphDraftRepository(workspace.Paths));
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SavePlannerReviewArtifact(new PlannerReviewArtifact
        {
            TaskId = task.TaskId,
            Review = new PlannerReview
            {
                Verdict = PlannerVerdict.Continue,
                Reason = "Validated and queued for review.",
                AcceptanceMet = true,
            },
            ResultingStatus = DomainTaskStatus.Review,
            TransitionReason = "Successful execution must stop at the review boundary.",
            PlannerComment = "Validated and queued for review.",
            PatchSummary = "files=1; added=12; removed=0; estimated=False; paths=src/CARVES.Runtime.Application/ControlPlane/ReviewEvidenceProjectionService.cs",
            ValidationPassed = true,
            ValidationEvidence = ["targeted workbench projection validation passed"],
            SafetyOutcome = SafetyOutcome.Allow,
            SafetyIssues = [],
            DecisionStatus = ReviewDecisionStatus.PendingReview,
            ClosureBundle = new ReviewClosureBundle
            {
                CompletionClaim = new ReviewClosureCompletionClaimSummary
                {
                    Required = true,
                    Status = "partial",
                    PresentFields = ["changed_files", "evidence_paths"],
                    MissingFields = ["tests_run", "next_recommendation"],
                    EvidencePaths = [".ai/artifacts/worker-executions/T-REALITY-005.json"],
                    NextRecommendation = "ask worker to resubmit missing claim fields",
                },
            },
            RealityProjection = new ReviewRealityProjection
            {
                SolidityClass = SolidityClass.Proto,
                PromotionResult = "review_ready",
                PlannedScope = "src/CARVES.Runtime.Application/ControlPlane/ReviewEvidenceProjectionService.cs",
                VerifiedOutcome = "targeted workbench projection validation passed",
            },
        });
        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", task.TaskId);
        Directory.CreateDirectory(worktreePath);
        var sourcePath = Path.Combine(worktreePath, "src", "CARVES.Runtime.Application", "ControlPlane", "ReviewEvidenceProjectionService.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "namespace Synthetic; public sealed class ReviewEvidenceProjectionSurface {}");
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = task.TaskId,
            Result = new WorkerExecutionResult
            {
                TaskId = task.TaskId,
                RunId = $"RUN-{task.TaskId}-001",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = task.TaskId,
                RunId = $"RUN-{task.TaskId}-001",
                WorktreePath = worktreePath,
                FilesWritten = ["src/CARVES.Runtime.Application/ControlPlane/ReviewEvidenceProjectionService.cs"],
            },
        });

        var service = new WorkbenchSurfaceService(
            workspace.RootPath,
            workspace.Paths,
            taskGraphService,
            planningDraftService,
            null!,
            null!,
            null!,
            new ExecutionRunService(workspace.Paths),
            artifactRepository,
            null!,
            null!,
            new StubGitClient(),
            maxParallelTasks: 1);

        var surface = service.BuildTask(task.TaskId);

        Assert.Equal("post_writeback_gap", surface.ReviewEvidence.Status);
        Assert.False(surface.ReviewEvidence.CanFinalApprove);
        Assert.Contains("result_commit", surface.ReviewEvidence.Summary, StringComparison.Ordinal);
        Assert.Contains(surface.ReviewEvidence.MissingEvidence, item => item.Contains("result_commit", StringComparison.Ordinal));
        Assert.Equal("partial", surface.ReviewEvidence.CompletionClaimStatus);
        Assert.True(surface.ReviewEvidence.CompletionClaimRequired);
        Assert.Equal(["tests_run", "next_recommendation"], surface.ReviewEvidence.CompletionClaimMissingFields);
        Assert.Equal([".ai/artifacts/worker-executions/T-REALITY-005.json"], surface.ReviewEvidence.CompletionClaimEvidencePaths);
        Assert.Contains("not lifecycle truth", surface.ReviewEvidence.CompletionClaimSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkbenchTaskSurface_ProjectsBoundaryStoppedManagedWorkspaceReason()
    {
        using var workspace = new TemporaryWorkspace();
        var task = new TaskNode
        {
            TaskId = "T-REALITY-BOUNDARY-001",
            CardId = "CARD-REALITY",
            Title = "Project managed workspace boundary stop",
            Description = "Expose scoped workspace policy stops through workbench task surfaces.",
            Status = DomainTaskStatus.Review,
            Scope = ["src/CARVES.Runtime.Application/Planning/ManagedWorkspaceLeaseService.cs"],
            Acceptance = ["managed workspace stop reason is visible"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["boundary_stopped"] = "true",
                ["managed_workspace_path_policy_status"] = "host_only",
                ["managed_workspace_path_policy_next_action"] = "route governed truth mutations through host-routed review/writeback instead of editing them in the workspace",
            },
            PlannerReview = new PlannerReview
            {
                Verdict = PlannerVerdict.PauseForReview,
                Reason = "Execution stopped by the boundary gate: Managed workspace path policy reserved host-only truth roots: .ai/tasks/graph.json.",
                DecisionStatus = ReviewDecisionStatus.PendingReview,
                AcceptanceMet = false,
                FollowUpSuggestions =
                [
                    "Route governed truth mutations back through host-routed review/writeback instead of editing them in the workspace.",
                ],
            },
        };
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var planningDraftService = new PlanningDraftService(
            workspace.Paths,
            taskGraphService,
            new JsonCardDraftRepository(workspace.Paths),
            new JsonTaskGraphDraftRepository(workspace.Paths));
        var service = new WorkbenchSurfaceService(
            workspace.RootPath,
            workspace.Paths,
            taskGraphService,
            planningDraftService,
            null!,
            null!,
            null!,
            new ExecutionRunService(workspace.Paths),
            new JsonRuntimeArtifactRepository(workspace.Paths),
            null!,
            null!,
            new StubGitClient(),
            maxParallelTasks: 1);

        var surface = service.BuildTask(task.TaskId);

        Assert.Contains("host-only truth roots", surface.BlockedReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("host-routed review/writeback", surface.NextAction, StringComparison.OrdinalIgnoreCase);
    }
}
