using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Git;
using Carves.Runtime.Infrastructure.Processes;
using JsonRuntimeArtifactRepository = Carves.Runtime.Infrastructure.Persistence.JsonRuntimeArtifactRepository;
using TaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeSessionGatewayRepeatabilityServiceTests
{
    [Fact]
    public void Build_ProjectsRuntimeOwnedRepeatabilityReadiness()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md", "# plan");
        workspace.WriteFile("docs/session-gateway/release-surface.md", "# release");
        workspace.WriteFile("docs/session-gateway/repeatability-readiness.md", "# repeatability");
        workspace.WriteFile("docs/session-gateway/dogfood-validation.md", "# dogfood");
        workspace.WriteFile("docs/session-gateway/operator-proof-contract.md", "# operator proof");
        workspace.WriteFile("docs/session-gateway/ALPHA_SETUP.md", "# setup");
        workspace.WriteFile("docs/session-gateway/ALPHA_QUICKSTART.md", "# quickstart");
        workspace.WriteFile("docs/session-gateway/KNOWN_LIMITATIONS.md", "# limitations");
        workspace.WriteFile("docs/session-gateway/BUG_REPORT_BUNDLE.md", "# bundle");

        var handoffTask = new TaskNode
        {
            TaskId = "T-CARD-579-001",
            CardId = "CARD-579",
            Title = "Implement Session Gateway private alpha handoff",
            Description = "Session Gateway private alpha handoff readiness.",
            Status = TaskStatus.Completed,
            Scope = ["docs/session-gateway/", "src/CARVES.Runtime.Application/Platform/"],
            UpdatedAt = DateTimeOffset.Parse("2026-04-05T06:00:00+00:00"),
        };
        var repeatabilityTask = new TaskNode
        {
            TaskId = "T-CARD-581-001",
            CardId = "CARD-581",
            Title = "Implement Session Gateway private alpha repeatability readiness",
            Description = "Bounded Session Gateway repeatability readiness.",
            Status = TaskStatus.Pending,
            Scope = ["docs/session-gateway/", "src/CARVES.Runtime.Application/Platform/"],
            AcceptanceContract = new AcceptanceContract
            {
                ContractId = "AC-T-CARD-581-001",
                Status = AcceptanceContractLifecycleStatus.Compiled,
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement { Type = "validation" },
                ],
            },
            UpdatedAt = DateTimeOffset.Parse("2026-04-05T06:30:00+00:00"),
        };

        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph([handoffTask, repeatabilityTask])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SavePlannerReviewArtifact(new PlannerReviewArtifact
        {
            TaskId = repeatabilityTask.TaskId,
            PatchSummary = "files=1; added=3; removed=0; estimated=False; paths=docs/session-gateway/repeatability-readiness.md",
            ValidationPassed = true,
            ValidationEvidence = ["focused validation passed"],
            SafetyOutcome = Carves.Runtime.Domain.Safety.SafetyOutcome.Allow,
            DecisionStatus = ReviewDecisionStatus.PendingReview,
            ClosureBundle = new ReviewClosureBundle
            {
                CompletionClaim = new ReviewClosureCompletionClaimSummary
                {
                    Required = true,
                    Status = "partial",
                    PresentFields = ["changed_files", "evidence_paths"],
                    MissingFields = ["tests_run", "next_recommendation"],
                    EvidencePaths = [".ai/artifacts/worker-executions/T-CARD-581-001.json"],
                    NextRecommendation = "ask worker to resubmit completion claim fields",
                },
            },
        });
        artifactRepository.SaveWorkerArtifact(new TaskRunArtifact
        {
            Report = new TaskRunReport
            {
                TaskId = repeatabilityTask.TaskId,
                Request = new WorkerRequest
                {
                    ExecutionRequest = new WorkerExecutionRequest
                    {
                        TaskId = repeatabilityTask.TaskId,
                        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["acceptance_contract_id"] = "AC-T-CARD-581-001",
                            ["acceptance_contract_status"] = "Compiled",
                            ["acceptance_contract_evidence_required"] = "validation",
                        },
                    },
                },
            },
        });
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = repeatabilityTask.TaskId,
        });
        var eventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository(), workspace.Paths);
        eventStream.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayOperationAccepted,
            ActorSessionId = "session-gateway-alpha",
            ActorIdentity = "session-gateway",
            ReferenceId = "operation-123",
            Summary = "Accepted a Session Gateway operation.",
            OccurredAt = DateTimeOffset.Parse("2026-04-05T06:20:00+00:00"),
        });
        eventStream.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayReviewResolved,
            ActorSessionId = "session-gateway-alpha",
            ActorIdentity = "session-gateway",
            TaskId = "T-CARD-579-001",
            ReferenceId = "operation-123",
            Summary = "Resolved a Session Gateway review.",
            OccurredAt = DateTimeOffset.Parse("2026-04-05T06:25:00+00:00"),
        });

        var service = new RuntimeSessionGatewayRepeatabilityService(
            workspace.RootPath,
            () => new RuntimeSessionGatewayPrivateAlphaHandoffSurface
            {
                OverallPosture = "private_alpha_deliverable_ready",
                DogfoodValidationPosture = "narrow_private_alpha_ready",
                ProgramClosureVerdict = "program_closure_complete",
                ContinuationGateOutcome = "closure_review_completed",
                ThinShellRoute = "/session-gateway/v1/shell",
                SessionCollectionRoute = "/api/session-gateway/v1/sessions",
                MessageRouteTemplate = "/api/session-gateway/v1/sessions/{session_id}/messages",
                EventsRouteTemplate = "/api/session-gateway/v1/sessions/{session_id}/events",
                AcceptedOperationRouteTemplate = "/api/session-gateway/v1/operations/{operation_id}",
                ProviderVisibilitySummary = "actionability_issues=0; optional=0; disabled=0",
                ProviderStatuses = ["codex/codex_cli:healthy; next=observe"],
                MaintenanceCommands = [RuntimeHostCommandLauncher.Cold("repair")],
                BugReportBundleCommands = [RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-private-alpha-handoff")],
                SupportedIntents = ["discuss", "plan", "governed_run"],
                OperatorProofContract = RuntimeSessionGatewayOperatorProofContractCatalog.BuildPrivateAlphaContract(),
                IsValid = true,
            },
            taskGraphService,
            artifactRepository,
            eventStream,
            new ReviewEvidenceProjectionService(workspace.RootPath, new GitClient(new ProcessRunner())));

        var surface = service.Build();

        Assert.Equal("runtime-session-gateway-repeatability", surface.SurfaceId);
        Assert.Equal("repeatable_private_alpha_ready", surface.OverallPosture);
        Assert.Equal("private_alpha_deliverable_ready", surface.PrivateAlphaHandoffPosture);
        Assert.Equal("program_closure_complete", surface.ProgramClosureVerdict);
        Assert.Equal("closure_review_completed", surface.ContinuationGateOutcome);
        Assert.Equal("docs/session-gateway/repeatability-readiness.md", surface.RepeatabilityReadinessPath);
        Assert.Equal("/session-gateway/v1/shell", surface.ThinShellRoute);
        Assert.Single(surface.ProviderStatuses);
        Assert.Equal(2, surface.RecentGatewayTasks.Count);
        Assert.Equal("T-CARD-581-001", surface.RecentGatewayTasks[0].TaskId);
        Assert.Equal("final_ready", surface.RecentGatewayTasks[0].ReviewEvidenceStatus);
        Assert.True(surface.RecentGatewayTasks[0].ReviewCanFinalApprove);
        Assert.Empty(surface.RecentGatewayTasks[0].MissingReviewEvidence);
        Assert.Equal("partial", surface.RecentGatewayTasks[0].WorkerCompletionClaimStatus);
        Assert.True(surface.RecentGatewayTasks[0].WorkerCompletionClaimRequired);
        Assert.Equal(["tests_run", "next_recommendation"], surface.RecentGatewayTasks[0].MissingWorkerCompletionClaimFields);
        Assert.Equal([".ai/artifacts/worker-executions/T-CARD-581-001.json"], surface.RecentGatewayTasks[0].WorkerCompletionClaimEvidencePaths);
        Assert.Contains("not lifecycle truth", surface.RecentGatewayTasks[0].WorkerCompletionClaimSummary, StringComparison.Ordinal);
        Assert.Equal("projected", surface.RecentGatewayTasks[0].AcceptanceContractBindingState);
        Assert.Equal("AC-T-CARD-581-001", surface.RecentGatewayTasks[0].AcceptanceContractId);
        Assert.Equal("Compiled", surface.RecentGatewayTasks[0].AcceptanceContractStatus);
        Assert.Equal("AC-T-CARD-581-001", surface.RecentGatewayTasks[0].ProjectedAcceptanceContractId);
        Assert.Equal("Compiled", surface.RecentGatewayTasks[0].ProjectedAcceptanceContractStatus);
        Assert.Equal(["validation"], surface.RecentGatewayTasks[0].AcceptanceContractEvidenceRequired);
        Assert.Equal(2, surface.RecentTimelineEntries.Count);
        Assert.Contains("inspect runtime-session-gateway-repeatability", string.Join('\n', surface.RecoveryCommands));
        Assert.Equal(SessionGatewayOperatorWaitStates.WaitingOperatorSetup, surface.OperatorProofContract.CurrentOperatorState);
        Assert.True(surface.IsValid);
        Assert.Empty(surface.Errors);
    }
}
