using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;
using ApplicationTaskScheduler = Carves.Runtime.Application.TaskGraph.TaskScheduler;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeHotspotBacklogDrainServiceTests
{
    private static readonly JsonSerializerOptions QueueJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    [Fact]
    public void Build_ProjectsQueueGovernedDrainFromBacklogAndTaskTruth()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave8");

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
                            Title = "Host bootstrap, dispatch, and composition hotspot queue",
                            Summary = "bounded host queue",
                            PlanningTaskId = "T-CARD-429-002",
                            SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                            ProofTarget = "host proof target",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs"],
                            BacklogItemIds = ["RB-host-1"]
                        },
                        new RefactoringHotspotQueue
                        {
                            QueueId = "code_understanding_and_artifact_governance",
                            FamilyId = "code_understanding_and_artifact_governance",
                            Title = "Code-understanding and artifact-governance hotspot queue",
                            Summary = "bounded code-understanding queue",
                            PlanningTaskId = "T-CARD-429-006",
                            SuggestedTaskId = "T-REFQ-code-understanding-and-artifact-governance-b3ea7e6c",
                            ProofTarget = "code-understanding proof target",
                            ScopeRoots = ["src/CARVES.Runtime.Application/Platform/"],
                            ValidationSurface = ["tests/Carves.Runtime.Application.Tests/RuntimeCodeUnderstandingEngineServiceTests.cs"],
                            BacklogItemIds = ["RB-code-1"]
                        }
                    ]
                },
                QueueJsonOptions));

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
                    Priority = "P1",
                    Status = RefactoringBacklogStatus.Suggested
                },
                new RefactoringBacklogItem
                {
                    ItemId = "RB-code-1",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Application/Platform/RuntimeArtifactCatalogService.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Application/Platform/RuntimeArtifactCatalogService.cs",
                    Priority = "P2",
                    Status = RefactoringBacklogStatus.Resolved
                }
            ]
        };

        var graph = new DomainTaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                Title = "Host queue task",
                Status = DomainTaskStatus.Suggested,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Host/"],
                Acceptance = ["accepted"]
            },
            new TaskNode
            {
                TaskId = "T-REFQ-code-understanding-and-artifact-governance-b3ea7e6c",
                Title = "Code-understanding queue task",
                Status = DomainTaskStatus.Completed,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Application/Platform/"],
                Acceptance = ["accepted"]
            }
        ]);

        var service = new RuntimeHotspotBacklogDrainService(
            workspace.RootPath,
            workspace.Paths,
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-hotspot-backlog-drain", surface.SurfaceId);
        Assert.Equal(2, surface.Counts.TotalBacklogItems);
        Assert.Equal(2, surface.Counts.QueueFamilyCount);
        Assert.Equal(1, surface.Counts.CompletedQueueCount);
        Assert.Equal(0, surface.Counts.AcceptedResidualQueueCount);
        Assert.Equal(1, surface.Counts.GovernedCompletedQueueCount);
        Assert.Equal(0, surface.Counts.CompletedWithRemainingBacklogCount);
        Assert.Equal(1, surface.Counts.ClosureBlockingBacklogItemCount);
        Assert.Equal(0, surface.Counts.NonBlockingBacklogItemCount);
        Assert.Equal(0, surface.Counts.UnselectedBacklogItemCount);

        var hostQueue = Assert.Single(surface.Queues, queue => queue.QueueId == "host_bootstrap_dispatch_and_composition");
        Assert.Equal("suggested", hostQueue.SuggestedTaskStatus);
        Assert.Equal("materialized", hostQueue.DrainState);
        Assert.Equal("residual_open", hostQueue.ClosureState);
        Assert.Equal(1, hostQueue.SuggestedBacklogItemCount);

        var codeQueue = Assert.Single(surface.Queues, queue => queue.QueueId == "code_understanding_and_artifact_governance");
        Assert.Equal("completed", codeQueue.SuggestedTaskStatus);
        Assert.Equal("completed_for_selected_items", codeQueue.DrainState);
        Assert.Equal("cleared", codeQueue.ClosureState);
        Assert.Equal(1, codeQueue.ResolvedBacklogItemCount);
    }

    [Fact]
    public void Build_SeparatesResidualCompletedQueuesFromClearedQueues()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave8");

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
                            Title = "Host bootstrap, dispatch, and composition hotspot queue",
                            Summary = "bounded host queue",
                            PlanningTaskId = "T-CARD-429-002",
                            SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                            ProofTarget = "host proof target",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs"],
                            BacklogItemIds = ["RB-host-1"]
                        }
                    ]
                },
                QueueJsonOptions));

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
                    Priority = "P1",
                    Status = RefactoringBacklogStatus.Suggested
                }
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
            }
        ]);

        var service = new RuntimeHotspotBacklogDrainService(
            workspace.RootPath,
            workspace.Paths,
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.Equal(0, surface.Counts.CompletedQueueCount);
        Assert.Equal(0, surface.Counts.AcceptedResidualQueueCount);
        Assert.Equal(1, surface.Counts.GovernedCompletedQueueCount);
        Assert.Equal(1, surface.Counts.CompletedWithRemainingBacklogCount);
        Assert.Equal(0, surface.Counts.UnselectedBacklogItemCount);
        var hostQueue = Assert.Single(surface.Queues);
        Assert.Equal("completed_with_remaining_backlog", hostQueue.DrainState);
        Assert.Equal("residual_open", hostQueue.ClosureState);
    }

    [Fact]
    public void Build_ProjectsContinuationPassWhenResidualQueueRotatesForward()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave10");

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
                            Summary = "Pass 2 bounded host queue",
                            PlanningTaskId = "T-CARD-429-002",
                            SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002",
                            PreviousSuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                            ProofTarget = "host proof target",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs"],
                            BacklogItemIds = ["RB-host-1"]
                        }
                    ]
                },
                QueueJsonOptions));

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
                    Priority = "P1",
                    Status = RefactoringBacklogStatus.Suggested,
                    SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002"
                }
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

        var service = new RuntimeHotspotBacklogDrainService(
            workspace.RootPath,
            workspace.Paths,
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.Equal(1, surface.Counts.MaterializedTaskCount);
        Assert.Equal(0, surface.Counts.AcceptedResidualQueueCount);
        Assert.Equal(1, surface.Counts.GovernedCompletedQueueCount);
        Assert.Equal(1, surface.Counts.CompletedWithRemainingBacklogCount);
        Assert.Equal(1, surface.Counts.ResidualOpenQueueCount);
        Assert.Equal(1, surface.Counts.ContinuedQueueCount);
        Assert.Equal(0, surface.Counts.UnselectedBacklogItemCount);
        var hostQueue = Assert.Single(surface.Queues);
        Assert.Equal(2, hostQueue.QueuePass);
        Assert.Equal("continuation_materialized", hostQueue.DrainState);
        Assert.Equal("residual_open", hostQueue.ClosureState);
        Assert.Equal("completed", hostQueue.PreviousSuggestedTaskStatus);
        Assert.Equal("T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92", hostQueue.PreviousSuggestedTaskId);
    }

    [Fact]
    public void Build_KeepsHistoricalContinuationVisibleWithoutTreatingClearedPassAsResidualOpen()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave69");

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
                            Summary = "Pass 2 bounded host queue",
                            PlanningTaskId = "T-CARD-429-002",
                            SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002",
                            PreviousSuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                            ProofTarget = "host proof target",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs"],
                            BacklogItemIds = ["RB-host-1"]
                        }
                    ]
                },
                QueueJsonOptions));

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
                    Priority = "P1",
                    Status = RefactoringBacklogStatus.Resolved,
                    SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002"
                }
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

        var service = new RuntimeHotspotBacklogDrainService(
            workspace.RootPath,
            workspace.Paths,
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal(1, surface.Counts.CompletedQueueCount);
        Assert.Equal(0, surface.Counts.CompletedWithRemainingBacklogCount);
        Assert.Equal(0, surface.Counts.ResidualOpenQueueCount);
        Assert.Equal(1, surface.Counts.ContinuedQueueCount);
        var hostQueue = Assert.Single(surface.Queues);
        Assert.Equal("completed_for_selected_items", hostQueue.DrainState);
        Assert.Equal("cleared", hostQueue.ClosureState);
        Assert.Equal("completed", hostQueue.PreviousSuggestedTaskStatus);
    }

    [Fact]
    public void Build_ProjectsAcceptedResidualConcentrationWhenPolicyAcceptsFamilyAndOnlyNonBlockingBacklogRemains()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave66");
        workspace.WriteFile(
            ".carves-platform/policies/governance-continuation-gate.policy.json",
            """
            {
              "version": "1.0",
              "hold_continuation_without_qualifying_delta": true,
              "accepted_residual_concentration_families": [
                "host_bootstrap_dispatch_and_composition"
              ],
              "closure_blocking_backlog_kinds": [
                "file_too_large",
                "function_too_large"
              ]
            }
            """);

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
                            Title = "Host bootstrap, dispatch, and composition hotspot queue",
                            Summary = "bounded host queue",
                            PlanningTaskId = "T-CARD-429-002",
                            SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                            ProofTarget = "host proof target",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs"],
                            BacklogItemIds = ["RB-host-1"]
                        }
                    ]
                },
                QueueJsonOptions));

        var backlog = new RefactoringBacklogSnapshot
        {
            Items =
            [
                new RefactoringBacklogItem
                {
                    ItemId = "RB-host-1",
                    Fingerprint = "documentation_cleanup|src/CARVES.Runtime.Host/Program.cs",
                    Kind = "documentation_cleanup",
                    Path = "src/CARVES.Runtime.Host/Program.cs",
                    Priority = "P3",
                    Status = RefactoringBacklogStatus.Suggested
                }
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
            }
        ]);

        var service = new RuntimeHotspotBacklogDrainService(
            workspace.RootPath,
            workspace.Paths,
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal(0, surface.Counts.CompletedQueueCount);
        Assert.Equal(1, surface.Counts.AcceptedResidualQueueCount);
        Assert.Equal(0, surface.Counts.CompletedWithRemainingBacklogCount);
        Assert.Equal(0, surface.Counts.ClosureBlockingBacklogItemCount);
        Assert.Equal(1, surface.Counts.NonBlockingBacklogItemCount);
        Assert.Equal(0, surface.Counts.UnselectedBacklogItemCount);
        var hostQueue = Assert.Single(surface.Queues);
        Assert.Equal("completed_with_remaining_backlog", hostQueue.DrainState);
        Assert.Equal("accepted_residual_concentration", hostQueue.ClosureState);
        Assert.Equal(0, hostQueue.ClosureBlockingBacklogItemCount);
        Assert.Equal(1, hostQueue.NonBlockingBacklogItemCount);
    }

    [Fact]
    public void Build_TreatsSelectedLowPriorityClosureKindAsNonBlockingMaintenanceNoise()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave71");

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
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostContractTests.cs"],
                            BacklogItemIds = ["RB-maintenance-noise"]
                        }
                    ]
                },
                QueueJsonOptions));

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

        var service = new RuntimeHotspotBacklogDrainService(
            workspace.RootPath,
            workspace.Paths,
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal(1, surface.Counts.CompletedQueueCount);
        Assert.Equal(0, surface.Counts.CompletedWithRemainingBacklogCount);
        Assert.Equal(0, surface.Counts.ClosureBlockingBacklogItemCount);
        Assert.Equal(1, surface.Counts.NonBlockingBacklogItemCount);
        var queue = Assert.Single(surface.Queues);
        Assert.Equal("cleared", queue.ClosureState);
        Assert.Equal(0, queue.ClosureBlockingBacklogItemCount);
        Assert.Equal(1, queue.NonBlockingBacklogItemCount);
    }

    [Fact]
    public void Build_KeepsUnselectedActiveBacklogVisibleWithoutCountingItAsResidualProgramBlocking()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave67");

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
                            Title = "Host bootstrap, dispatch, and composition hotspot queue",
                            Summary = "bounded host queue",
                            PlanningTaskId = "T-CARD-429-002",
                            SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                            ProofTarget = "host proof target",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs"],
                            BacklogItemIds = ["RB-host-1"]
                        }
                    ]
                },
                QueueJsonOptions));

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
                    Priority = "P1",
                    Status = RefactoringBacklogStatus.Suggested
                },
                new RefactoringBacklogItem
                {
                    ItemId = "RB-outside-1",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Application/ControlPlane/MarkdownProjector.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Application/ControlPlane/MarkdownProjector.cs",
                    Priority = "P2",
                    Status = RefactoringBacklogStatus.Open
                }
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
            }
        ]);

        var service = new RuntimeHotspotBacklogDrainService(
            workspace.RootPath,
            workspace.Paths,
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal(2, surface.Counts.TotalBacklogItems);
        Assert.Equal(1, surface.Counts.ClosureBlockingBacklogItemCount);
        Assert.Equal(0, surface.Counts.NonBlockingBacklogItemCount);
        Assert.Equal(1, surface.Counts.UnselectedBacklogItemCount);
        Assert.Equal(1, surface.Counts.UnselectedClosureRelevantBacklogItemCount);
        Assert.Equal(0, surface.Counts.UnselectedMaintenanceNoiseBacklogItemCount);
        var hostQueue = Assert.Single(surface.Queues);
        Assert.Equal(1, hostQueue.ClosureBlockingBacklogItemCount);
        Assert.Equal(0, hostQueue.NonBlockingBacklogItemCount);
    }

    [Fact]
    public void Build_ClassifiesLowPriorityUnselectedBacklogAsMaintenanceNoise()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave70");

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
                            Title = "Host bootstrap, dispatch, and composition hotspot queue",
                            Summary = "bounded host queue",
                            PlanningTaskId = "T-CARD-429-002",
                            SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                            ProofTarget = "host proof target",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs"],
                            BacklogItemIds = ["RB-host-1"]
                        }
                    ]
                },
                QueueJsonOptions));

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
                    Priority = "P1",
                    Status = RefactoringBacklogStatus.Suggested
                },
                new RefactoringBacklogItem
                {
                    ItemId = "RB-outside-1",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Application/ControlPlane/MarkdownProjector.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Application/ControlPlane/MarkdownProjector.cs",
                    Priority = "P3",
                    Status = RefactoringBacklogStatus.Open
                }
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
            }
        ]);

        var service = new RuntimeHotspotBacklogDrainService(
            workspace.RootPath,
            workspace.Paths,
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal(1, surface.Counts.UnselectedBacklogItemCount);
        Assert.Equal(0, surface.Counts.UnselectedClosureRelevantBacklogItemCount);
        Assert.Equal(1, surface.Counts.UnselectedMaintenanceNoiseBacklogItemCount);
    }

    [Fact]
    public void Build_IsInvalidWhenBoundaryDocumentOrQueueIndexIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeHotspotBacklogDrainService(
            workspace.RootPath,
            workspace.Paths,
            new StubRefactoringService(new RefactoringBacklogSnapshot()),
            new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.False(surface.IsValid);
        Assert.Contains(surface.Errors, error => error.Contains("runtime-hotspot-backlog-drain-governance.md", StringComparison.Ordinal));
        Assert.Contains(surface.Errors, error => error.Contains(".ai/refactoring/queues/index.json", StringComparison.Ordinal));
    }

    private sealed class StubRefactoringService(RefactoringBacklogSnapshot backlog) : IRefactoringService
    {
        public RefactoringBacklogSnapshot LoadBacklog() => backlog;

        public RefactoringBacklogSnapshot DetectAndStore() => backlog;

        public RefactoringTaskMaterializationResult MaterializeSuggestedTasks() =>
            new([], [], false, [], []);
    }
}
