using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using ApplicationTaskScheduler = Carves.Runtime.Application.TaskGraph.TaskScheduler;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeGovernanceProgramReauditServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    [Fact]
    public void Build_ProjectsContinueProgramWhenResidualPressureAndSustainabilityDriftRemain()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-validationlab-proof-handoff-boundary.md", "# handoff");
        workspace.WriteFile("docs/runtime/runtime-controlled-governance-proof-integration.md", "# controlled");
        workspace.WriteFile("docs/runtime/runtime-packaging-proof-federation-maturity.md", "# packaging");
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave8");
        workspace.WriteFile("docs/runtime/runtime-hotspot-cross-family-patterns.md", "# wave11");
        workspace.WriteFile("docs/runtime/runtime-governance-program-reaudit.md", "# wave12");

        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai", "refactoring", "queues"));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "refactoring", "queues", "index.json"),
            JsonSerializer.Serialize(
                new RefactoringHotspotQueueSnapshot
                {
                    Queues =
                    [
                        new RefactoringHotspotQueue
                        {
                            QueueId = "host_bootstrap_dispatch_and_composition",
                            FamilyId = "host_bootstrap_dispatch_and_composition",
                            QueuePass = 2,
                            Title = "Host bootstrap, dispatch, and composition hotspot queue (pass 2)",
                            Summary = "Pass 2 host queue",
                            PlanningTaskId = "T-CARD-493-001",
                            SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002",
                            PreviousSuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                            ProofTarget = "Host bootstrap, dispatch, and composition accumulation points are decomposed through bounded routing and composition surfaces without reopening control-plane ownership.",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            PreservationConstraints =
                            [
                                "keep truth ownership stable",
                                "do not introduce a second host shell",
                            ],
                            ValidationSurface =
                            [
                                "tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs",
                            ],
                            BacklogItemIds = ["RB-host-1", "RB-host-2"],
                            HotspotPaths =
                            [
                                "src/CARVES.Runtime.Host/Program.cs",
                                "src/CARVES.Runtime.Host/LocalHostCommandDispatcher.cs",
                            ],
                        }
                    ]
                },
                JsonOptions));

        var backlog = new RefactoringBacklogSnapshot
        {
            Items =
            [
                new RefactoringBacklogItem
                {
                    ItemId = "RB-host-1",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Host/Program.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Host/Program.cs",
                    Priority = "P2",
                    Status = RefactoringBacklogStatus.Suggested,
                },
                new RefactoringBacklogItem
                {
                    ItemId = "RB-host-2",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Host/LocalHostCommandDispatcher.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Host/LocalHostCommandDispatcher.cs",
                    Priority = "P2",
                    Status = RefactoringBacklogStatus.Open,
                },
                new RefactoringBacklogItem
                {
                    ItemId = "RB-outside-1",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Application/ControlPlane/MarkdownProjector.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Application/ControlPlane/MarkdownProjector.cs",
                    Priority = "P2",
                    Status = RefactoringBacklogStatus.Open,
                },
            ]
        };

        var graph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                Title = "Host queue task",
                Status = DomainTaskStatus.Completed,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Host/"],
                Acceptance = ["accepted"]
            },
            new TaskNode
            {
                TaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002",
                Title = "Host queue task (pass 2)",
                Status = DomainTaskStatus.Suggested,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Host/"],
                Acceptance = ["accepted"]
            }
        ]);

        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai", "runtime", "sustainability"));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "runtime", "sustainability", "audit.json"),
            JsonSerializer.Serialize(
                new SustainabilityAuditReport
                {
                    GeneratedAt = DateTimeOffset.UtcNow.AddDays(-10),
                    StrictPassed = false,
                    Findings =
                    [
                        new SustainabilityAuditFinding
                        {
                            Category = "retention_drift",
                            Severity = "error",
                            FamilyId = "ephemeral_runtime_residue",
                            Path = ".ai/runtime/tmp",
                            Message = "ephemeral residue remains overdue",
                            RecommendedAction = RuntimeMaintenanceActionKind.PruneEphemeral,
                        }
                    ],
                },
                JsonOptions));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "runtime", "sustainability", "archive-readiness.json"),
            JsonSerializer.Serialize(
                new OperationalHistoryArchiveReadinessReport
                {
                    GeneratedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    ArchiveRoot = ".ai/runtime/sustainability/archive",
                    Families =
                    [
                        new OperationalHistoryArchiveReadinessFamily
                        {
                            FamilyId = "worker_execution_artifact_history",
                            DisplayName = "Worker execution artifact history",
                            PromotionRelevantCount = 1,
                            Summary = "one relevant archived entry",
                        }
                    ],
                    PromotionRelevantEntries =
                    [
                        new OperationalHistoryArchiveReadinessEntry
                        {
                            EntryId = "archive-entry-1",
                            FamilyId = "worker_execution_artifact_history",
                            OriginalPath = ".ai/artifacts/reviews/T-CARD-001.json",
                            ArchivedPath = ".ai/runtime/sustainability/archive/worker_execution_artifact_history/.ai/artifacts/reviews/T-CARD-001.json",
                            ArchivedAt = DateTimeOffset.UtcNow,
                            WhyArchived = "aged out",
                            ArchiveReadinessState = "archive_ready_after_hot_window_with_followup",
                            PromotionRelevant = true,
                            PromotionReason = "review evidence still matters",
                        }
                    ],
                    Summary = "archive readiness exists",
                },
                JsonOptions));

        var service = new RuntimeGovernanceProgramReauditService(
            workspace.RootPath,
            workspace.Paths,
            TestSystemConfigFactory.Create(),
            RoleGovernanceRuntimePolicy.CreateDefault(),
            new JsonRuntimeArtifactRepository(workspace.Paths),
            new StubCodeGraphQueryService(),
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-governance-program-reaudit", surface.SurfaceId);
        Assert.Equal("continue_program", surface.OverallVerdict);
        Assert.Equal("hold_continuation", surface.ContinuationGateOutcome);
        Assert.Equal("none", surface.ClosureDeltaPosture);
        Assert.Contains("Refresh stale supporting reports", surface.RecommendedNextAction, StringComparison.Ordinal);

        var queueClosure = Assert.Single(surface.Criteria, criterion => criterion.CriterionId == "queue_family_closure");
        Assert.Equal("continue_program", queueClosure.Status);

        var freshness = Assert.Single(surface.Criteria, criterion => criterion.CriterionId == "supporting_report_freshness");
        Assert.Equal("continue_program", freshness.Status);
        Assert.Contains("sustainability_age_days=", freshness.Summary, StringComparison.Ordinal);

        var surfaceStability = Assert.Single(surface.Criteria, criterion => criterion.CriterionId == "surface_stability");
        Assert.Equal("partial", surfaceStability.Status);

        Assert.True(surface.Counts.SustainabilityAuditAvailable);
        Assert.Equal("stale", surface.Counts.SustainabilityAuditFreshness);
        Assert.False(surface.Counts.SustainabilityStrictPassed);
        Assert.Equal("fresh", surface.Counts.ArchiveReadinessFreshness);
        Assert.Equal(1, surface.Counts.PromotionRelevantArchivedEntryCount);
        Assert.Equal(2, surface.Counts.ClosureBlockingBacklogItems);
        Assert.Equal(0, surface.Counts.NonBlockingBacklogItems);
        Assert.Equal(1, surface.Counts.UnselectedBacklogItems);
        Assert.Equal(1, surface.Counts.UnselectedClosureRelevantBacklogItems);
        Assert.Equal(0, surface.Counts.UnselectedMaintenanceNoiseBacklogItems);
    }

    [Fact]
    public void Build_WarnsWhenSupportingReportsAreMissing()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-validationlab-proof-handoff-boundary.md", "# handoff");
        workspace.WriteFile("docs/runtime/runtime-controlled-governance-proof-integration.md", "# controlled");
        workspace.WriteFile("docs/runtime/runtime-packaging-proof-federation-maturity.md", "# packaging");
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave8");
        workspace.WriteFile("docs/runtime/runtime-hotspot-cross-family-patterns.md", "# wave11");
        workspace.WriteFile("docs/runtime/runtime-governance-program-reaudit.md", "# wave12");

        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai", "refactoring", "queues"));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "refactoring", "queues", "index.json"),
            JsonSerializer.Serialize(new RefactoringHotspotQueueSnapshot(), JsonOptions));

        var service = new RuntimeGovernanceProgramReauditService(
            workspace.RootPath,
            workspace.Paths,
            TestSystemConfigFactory.Create(),
            RoleGovernanceRuntimePolicy.CreateDefault(),
            new JsonRuntimeArtifactRepository(workspace.Paths),
            new StubCodeGraphQueryService(),
            new StubRefactoringService(new RefactoringBacklogSnapshot()),
            new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Contains(surface.Warnings, warning => warning.Contains("sustainability audit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(surface.Warnings, warning => warning.Contains("archive-readiness", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("continue_program", surface.OverallVerdict);
        var freshness = Assert.Single(surface.Criteria, criterion => criterion.CriterionId == "supporting_report_freshness");
        Assert.Equal("warning", freshness.Status);
    }

    [Fact]
    public void Build_HoldsContinuationUntilQualifyingClosureDeltaExists()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-validationlab-proof-handoff-boundary.md", "# handoff");
        workspace.WriteFile("docs/runtime/runtime-controlled-governance-proof-integration.md", "# controlled");
        workspace.WriteFile("docs/runtime/runtime-packaging-proof-federation-maturity.md", "# packaging");
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave66");
        workspace.WriteFile("docs/runtime/runtime-hotspot-cross-family-patterns.md", "# wave11");
        workspace.WriteFile("docs/runtime/runtime-governance-program-reaudit.md", "# wave66");

        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai", "refactoring", "queues"));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "refactoring", "queues", "index.json"),
            JsonSerializer.Serialize(
                new RefactoringHotspotQueueSnapshot
                {
                    Queues =
                    [
                        new RefactoringHotspotQueue
                        {
                            QueueId = "host_bootstrap_dispatch_and_composition",
                            FamilyId = "host_bootstrap_dispatch_and_composition",
                            QueuePass = 2,
                            Title = "Host bootstrap, dispatch, and composition hotspot queue (pass 2)",
                            Summary = "Pass 2 host queue",
                            PlanningTaskId = "T-CARD-493-001",
                            SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002",
                            PreviousSuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                            ProofTarget = "Host bootstrap, dispatch, and composition accumulation points are decomposed through bounded routing and composition surfaces without reopening control-plane ownership.",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs"],
                            BacklogItemIds = ["RB-host-1"],
                        }
                    ]
                },
                JsonOptions));

        var backlog = new RefactoringBacklogSnapshot
        {
            Items =
            [
                new RefactoringBacklogItem
                {
                    ItemId = "RB-host-1",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Host/Program.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Host/Program.cs",
                    Priority = "P2",
                    Status = RefactoringBacklogStatus.Open,
                },
            ]
        };

        var graph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                Title = "Host queue task",
                Status = DomainTaskStatus.Completed,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Host/"],
                Acceptance = ["accepted"]
            },
            new TaskNode
            {
                TaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002",
                Title = "Host queue task (pass 2)",
                Status = DomainTaskStatus.Suggested,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Host/"],
                Acceptance = ["accepted"]
            }
        ]);

        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai", "runtime", "sustainability"));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "runtime", "sustainability", "audit.json"),
            JsonSerializer.Serialize(
                new SustainabilityAuditReport
                {
                    GeneratedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    StrictPassed = true,
                    Findings = [],
                },
                JsonOptions));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "runtime", "sustainability", "archive-readiness.json"),
            JsonSerializer.Serialize(
                new OperationalHistoryArchiveReadinessReport
                {
                    GeneratedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    ArchiveRoot = ".ai/runtime/sustainability/archive",
                    Families = [],
                    PromotionRelevantEntries = [],
                    Summary = "archive readiness exists",
                },
                JsonOptions));

        var service = new RuntimeGovernanceProgramReauditService(
            workspace.RootPath,
            workspace.Paths,
            TestSystemConfigFactory.Create(),
            RoleGovernanceRuntimePolicy.CreateDefault(),
            new JsonRuntimeArtifactRepository(workspace.Paths),
            new StubCodeGraphQueryService(),
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.Equal("continue_program", surface.OverallVerdict);
        Assert.Equal("hold_continuation", surface.ContinuationGateOutcome);
        Assert.Equal("none", surface.ClosureDeltaPosture);
        Assert.Contains("Hold later continuation", surface.RecommendedNextAction, StringComparison.Ordinal);
        var continuationGate = Assert.Single(surface.Criteria, criterion => criterion.CriterionId == "continuation_gate");
        Assert.Equal("hold_continuation", continuationGate.Status);
        Assert.Contains("No qualifying closure delta", continuationGate.Summary, StringComparison.Ordinal);
        var programWaveNecessity = Assert.Single(surface.Criteria, criterion => criterion.CriterionId == "program_wave_necessity");
        Assert.Equal("hold_continuation", programWaveNecessity.Status);
    }

    [Fact]
    public void Build_AdvancesToClosureCandidateWhenOnlyHistoricalContinuationAndMaintenanceNoiseRemain()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-validationlab-proof-handoff-boundary.md", "# handoff");
        workspace.WriteFile("docs/runtime/runtime-controlled-governance-proof-integration.md", "# controlled");
        workspace.WriteFile("docs/runtime/runtime-packaging-proof-federation-maturity.md", "# packaging");
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave69");
        workspace.WriteFile("docs/runtime/runtime-hotspot-cross-family-patterns.md", "# wave11");
        workspace.WriteFile("docs/runtime/runtime-governance-program-reaudit.md", "# wave69");

        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai", "refactoring", "queues"));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "refactoring", "queues", "index.json"),
            JsonSerializer.Serialize(
                new RefactoringHotspotQueueSnapshot
                {
                    Queues =
                    [
                        new RefactoringHotspotQueue
                        {
                            QueueId = "host_bootstrap_dispatch_and_composition",
                            FamilyId = "host_bootstrap_dispatch_and_composition",
                            QueuePass = 2,
                            Title = "Host bootstrap, dispatch, and composition hotspot queue (pass 2)",
                            Summary = "Pass 2 host queue",
                            PlanningTaskId = "T-CARD-493-001",
                            SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002",
                            PreviousSuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                            ProofTarget = "Host bootstrap, dispatch, and composition accumulation points are decomposed through bounded routing and composition surfaces without reopening control-plane ownership.",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs"],
                            BacklogItemIds = ["RB-host-1"],
                        }
                    ]
                },
                JsonOptions));

        var backlog = new RefactoringBacklogSnapshot
        {
            Items =
            [
                new RefactoringBacklogItem
                {
                    ItemId = "RB-host-1",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Host/Program.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Host/Program.cs",
                    Priority = "P2",
                    Status = RefactoringBacklogStatus.Resolved,
                },
                new RefactoringBacklogItem
                {
                    ItemId = "RB-outside-1",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Application/ControlPlane/MarkdownProjector.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Application/ControlPlane/MarkdownProjector.cs",
                    Priority = "P3",
                    Status = RefactoringBacklogStatus.Open,
                },
            ]
        };

        var graph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                Title = "Host queue task",
                Status = DomainTaskStatus.Completed,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Host/"],
                Acceptance = ["accepted"]
            },
            new TaskNode
            {
                TaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002",
                Title = "Host queue task (pass 2)",
                Status = DomainTaskStatus.Completed,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Host/"],
                Acceptance = ["accepted"]
            }
        ]);

        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai", "runtime", "sustainability"));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "runtime", "sustainability", "audit.json"),
            JsonSerializer.Serialize(
                new SustainabilityAuditReport
                {
                    GeneratedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    StrictPassed = true,
                    Findings = [],
                },
                JsonOptions));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "runtime", "sustainability", "archive-readiness.json"),
            JsonSerializer.Serialize(
                new OperationalHistoryArchiveReadinessReport
                {
                    GeneratedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    ArchiveRoot = ".ai/runtime/sustainability/archive",
                    Families = [],
                    PromotionRelevantEntries = [],
                    Summary = "archive readiness exists",
                },
                JsonOptions));

        var service = new RuntimeGovernanceProgramReauditService(
            workspace.RootPath,
            workspace.Paths,
            TestSystemConfigFactory.Create(),
            RoleGovernanceRuntimePolicy.CreateDefault(),
            new JsonRuntimeArtifactRepository(workspace.Paths),
            new StubCodeGraphQueryService(),
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("program_closure_candidate", surface.OverallVerdict);
        Assert.Equal("closure_review_ready", surface.ContinuationGateOutcome);
        Assert.Equal("queue_cleared", surface.ClosureDeltaPosture);
        Assert.Equal(0, surface.Counts.ResidualOpenQueueCount);
        Assert.Equal(1, surface.Counts.ContinuedQueueCount);
        Assert.Equal(1, surface.Counts.UnselectedBacklogItems);
        Assert.Equal(0, surface.Counts.UnselectedClosureRelevantBacklogItems);
        Assert.Equal(1, surface.Counts.UnselectedMaintenanceNoiseBacklogItems);
        Assert.Contains("closure review", surface.RecommendedNextAction, StringComparison.OrdinalIgnoreCase);

        var queueClosure = Assert.Single(surface.Criteria, criterion => criterion.CriterionId == "queue_family_closure");
        Assert.Equal("satisfied", queueClosure.Status);
        Assert.Contains("historical_continued=1", queueClosure.Summary, StringComparison.Ordinal);

        var backlogPressure = Assert.Single(surface.Criteria, criterion => criterion.CriterionId == "backlog_structural_pressure");
        Assert.Equal("satisfied", backlogPressure.Status);
        Assert.Contains("unselected_maintenance_noise=1", backlogPressure.Summary, StringComparison.Ordinal);

        var programWaveNecessity = Assert.Single(surface.Criteria, criterion => criterion.CriterionId == "program_wave_necessity");
        Assert.Equal("closure_candidate", programWaveNecessity.Status);
        Assert.Contains("operator review of program closure", programWaveNecessity.Summary, StringComparison.Ordinal);

        var continuationGate = Assert.Single(surface.Criteria, criterion => criterion.CriterionId == "continuation_gate");
        Assert.Equal("satisfied", continuationGate.Status);
        Assert.Contains("closure review can proceed", continuationGate.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_AdvancesToClosureCompleteWhenGovernedClosureReviewIsRecorded()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-validationlab-proof-handoff-boundary.md", "# handoff");
        workspace.WriteFile("docs/runtime/runtime-controlled-governance-proof-integration.md", "# controlled");
        workspace.WriteFile("docs/runtime/runtime-packaging-proof-federation-maturity.md", "# packaging");
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave8");
        workspace.WriteFile("docs/runtime/runtime-hotspot-cross-family-patterns.md", "# wave11");
        workspace.WriteFile("docs/runtime/runtime-governance-program-reaudit.md", "# wave12");

        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai", "refactoring", "queues"));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "refactoring", "queues", "index.json"),
            JsonSerializer.Serialize(
                new RefactoringHotspotQueueSnapshot
                {
                    Queues =
                    [
                        new RefactoringHotspotQueue
                        {
                            QueueId = "cleared_family",
                            FamilyId = "cleared_family",
                            QueuePass = 2,
                            Title = "Cleared family (pass 2)",
                            Summary = "Historical continuation only",
                            PlanningTaskId = "T-CARD-569-001",
                            SuggestedTaskId = "T-REFQ-cleared-family-P002",
                            PreviousSuggestedTaskId = "T-REFQ-cleared-family",
                            ProofTarget = "Cleared family remains historical continuation only.",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            PreservationConstraints = ["keep closure truth downstream only"],
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostContractTests.cs"],
                            BacklogItemIds = ["RB-maintenance-noise"],
                            HotspotPaths = ["src/CARVES.Runtime.Host/Program.cs"],
                        }
                    ]
                },
                JsonOptions));

        var backlog = new RefactoringBacklogSnapshot
        {
            Items =
            [
                new RefactoringBacklogItem
                {
                    ItemId = "RB-maintenance-noise",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Application/ControlPlane/MarkdownProjector.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Application/ControlPlane/MarkdownProjector.cs",
                    Priority = "P3",
                    Status = RefactoringBacklogStatus.Open,
                },
            ]
        };

        var graph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-REFQ-cleared-family",
                Title = "Original queue task",
                Status = DomainTaskStatus.Completed,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Host/"],
                Acceptance = ["accepted"],
            },
            new TaskNode
            {
                TaskId = "T-REFQ-cleared-family-P002",
                Title = "Continuation queue task",
                Status = DomainTaskStatus.Completed,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Host/"],
                Acceptance = ["accepted"],
            }
        ]);

        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai", "runtime", "sustainability"));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "runtime", "sustainability", "audit.json"),
            JsonSerializer.Serialize(
                new SustainabilityAuditReport
                {
                    GeneratedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    StrictPassed = true,
                    Findings = [],
                },
                JsonOptions));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "runtime", "sustainability", "archive-readiness.json"),
            JsonSerializer.Serialize(
                new OperationalHistoryArchiveReadinessReport
                {
                    GeneratedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    ArchiveRoot = ".ai/runtime/sustainability/archive",
                    Families = [],
                    PromotionRelevantEntries = [],
                    Summary = "archive readiness exists",
                },
                JsonOptions));
        Directory.CreateDirectory(Path.Combine(workspace.RootPath, ".ai", "runtime", "governance"));
        File.WriteAllText(
            Path.Combine(workspace.RootPath, ".ai", "runtime", "governance", "program-closure-review.json"),
            JsonSerializer.Serialize(
                new ClosureReviewRecord
                {
                    RecordedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                    Outcome = "approved_for_closure",
                    Summary = "closure review recorded",
                    SourceSurfaceId = "runtime-governance-program-reaudit",
                    SourceOverallVerdict = "program_closure_candidate",
                    SourceContinuationGateOutcome = "closure_review_ready",
                },
                JsonOptions));

        var service = new RuntimeGovernanceProgramReauditService(
            workspace.RootPath,
            workspace.Paths,
            TestSystemConfigFactory.Create(),
            RoleGovernanceRuntimePolicy.CreateDefault(),
            new JsonRuntimeArtifactRepository(workspace.Paths),
            new StubCodeGraphQueryService(),
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("program_closure_complete", surface.OverallVerdict);
        Assert.Equal("closure_review_completed", surface.ContinuationGateOutcome);
        Assert.Equal("approved_for_closure", surface.ClosureReviewOutcome);
        Assert.Equal(".ai/runtime/governance/program-closure-review.json", surface.ClosureReviewPath);
        Assert.NotNull(surface.ClosureReviewRecordedAt);
        Assert.Contains("bounded maintenance", surface.RecommendedNextAction, StringComparison.OrdinalIgnoreCase);

        var programWaveNecessity = Assert.Single(surface.Criteria, criterion => criterion.CriterionId == "program_wave_necessity");
        Assert.Equal("satisfied", programWaveNecessity.Status);
        Assert.Contains("normal bounded maintenance", programWaveNecessity.Summary, StringComparison.OrdinalIgnoreCase);

        var continuationGate = Assert.Single(surface.Criteria, criterion => criterion.CriterionId == "continuation_gate");
        Assert.Equal("satisfied", continuationGate.Status);
        Assert.Contains("closure review has been recorded", continuationGate.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubRefactoringService(RefactoringBacklogSnapshot backlog) : IRefactoringService
    {
        public RefactoringBacklogSnapshot LoadBacklog() => backlog;

        public RefactoringBacklogSnapshot DetectAndStore() => backlog;

        public RefactoringTaskMaterializationResult MaterializeSuggestedTasks() => new([], [], false, [], []);
    }

    private sealed class StubCodeGraphQueryService : ICodeGraphQueryService
    {
        public CodeGraphManifest LoadManifest() => new();

        public IReadOnlyList<CodeGraphModuleEntry> LoadModuleSummaries() => [];

        public CodeGraphIndex LoadIndex() => new();

        public CodeGraphScopeAnalysis AnalyzeScope(IEnumerable<string> scopeEntries) => CodeGraphScopeAnalysis.Empty;

        public CodeGraphImpactAnalysis AnalyzeImpact(IEnumerable<string> scopeEntries) => CodeGraphImpactAnalysis.Empty;
    }

    private sealed class ClosureReviewRecord
    {
        public DateTimeOffset RecordedAt { get; init; }

        public string Outcome { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;

        public string SourceSurfaceId { get; init; } = string.Empty;

        public string SourceOverallVerdict { get; init; } = string.Empty;

        public string SourceContinuationGateOutcome { get; init; } = string.Empty;
    }
}
