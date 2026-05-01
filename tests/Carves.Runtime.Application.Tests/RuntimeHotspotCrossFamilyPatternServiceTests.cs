using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;
using ApplicationTaskScheduler = Carves.Runtime.Application.TaskGraph.TaskScheduler;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeHotspotCrossFamilyPatternServiceTests
{
    private static readonly JsonSerializerOptions QueueJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    [Fact]
    public void Build_ProjectsResidualKindsValidationOverlapAndBoundaryCategories()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave8");
        workspace.WriteFile("docs/runtime/runtime-hotspot-cross-family-patterns.md", "# wave11");

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
                            PlanningTaskId = "T-CARD-429-002",
                            SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002",
                            PreviousSuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                            ProofTarget = "Host bootstrap, dispatch, and composition accumulation points are decomposed through bounded routing and composition surfaces without reopening control-plane ownership.",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            PreservationConstraints =
                            [
                                "keep the resident-host command surface and current truth ownership stable",
                                "do not introduce a second host shell, second control plane, or unmanaged bootstrap lane",
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
                        },
                        new RefactoringHotspotQueue
                        {
                            QueueId = "operator_projection_and_control_plane",
                            FamilyId = "operator_projection_and_control_plane",
                            QueuePass = 2,
                            Title = "Operator projection and control-plane hotspot queue (pass 2)",
                            Summary = "Pass 2 operator queue",
                            PlanningTaskId = "T-CARD-429-003",
                            SuggestedTaskId = "T-REFQ-operator-projection-and-control-plane-c5293c04-P002",
                            PreviousSuggestedTaskId = "T-REFQ-operator-projection-and-control-plane-c5293c04",
                            ProofTarget = "Operator projection and control-plane accumulation points are reduced without widening control-plane writes or collapsing projection/read boundaries.",
                            ScopeRoots = ["src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceFormatter"],
                            PreservationConstraints =
                            [
                                "preserve projection-versus-writeback separation for operator surfaces",
                                "keep scope bounded to the declared control-plane family and coupled formatter/service slices",
                            ],
                            ValidationSurface =
                            [
                                "tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs",
                            ],
                            BacklogItemIds = ["RB-operator-1", "RB-operator-2"],
                            HotspotPaths =
                            [
                                "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceFormatter.cs",
                                "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.cs",
                            ],
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
                    Status = RefactoringBacklogStatus.Suggested,
                },
                new RefactoringBacklogItem
                {
                    ItemId = "RB-operator-1",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceFormatter.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceFormatter.cs",
                    Priority = "P2",
                    Status = RefactoringBacklogStatus.Suggested,
                },
                new RefactoringBacklogItem
                {
                    ItemId = "RB-operator-2",
                    Fingerprint = "function_too_large|src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.cs",
                    Kind = "function_too_large",
                    Path = "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.cs",
                    Priority = "P2",
                    Status = RefactoringBacklogStatus.Suggested,
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
            },
            new TaskNode
            {
                TaskId = "T-REFQ-operator-projection-and-control-plane-c5293c04",
                Title = "Operator queue task",
                Status = DomainTaskStatus.Completed,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Application/ControlPlane/"],
                Acceptance = ["accepted"]
            },
            new TaskNode
            {
                TaskId = "T-REFQ-operator-projection-and-control-plane-c5293c04-P002",
                Title = "Operator queue task (pass 2)",
                Status = DomainTaskStatus.Suggested,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Application/ControlPlane/"],
                Acceptance = ["accepted"]
            }
        ]);

        var service = new RuntimeHotspotCrossFamilyPatternService(
            workspace.RootPath,
            workspace.Paths,
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-hotspot-cross-family-patterns", surface.SurfaceId);
        Assert.Equal(2, surface.Counts.QueueFamilyCount);
        Assert.Equal(1, surface.Counts.RepeatedBacklogKindPatternCount);
        Assert.Equal(1, surface.Counts.ValidationOverlapPatternCount);
        Assert.True(surface.Counts.SharedBoundaryCategoryCount >= 1);

        var residual = Assert.Single(surface.Patterns, pattern => pattern.PatternId == "residual_continuation_pressure");
        Assert.Equal("residual_continuation", residual.PatternType);
        Assert.Equal(2, residual.QueueCount);

        var repeatedKind = Assert.Single(surface.Patterns, pattern => pattern.PatternId == "backlog_kind_file_too_large");
        Assert.Equal("repeated_backlog_kind", repeatedKind.PatternType);
        Assert.Contains("file_too_large", repeatedKind.BacklogKinds);

        var validationOverlap = Assert.Single(surface.Patterns, pattern => pattern.PatternType == "validation_overlap");
        Assert.Contains("tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs", validationOverlap.ValidationSurfaces);

        Assert.Contains(surface.BoundaryCategories, category => category.CategoryId == "truth_ownership_boundary");
        Assert.Contains(surface.BoundaryCategories, category => category.CategoryId == "projection_read_boundary");
    }

    [Fact]
    public void Build_IsInvalidWhenBoundaryDocumentIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave8");

        var service = new RuntimeHotspotCrossFamilyPatternService(
            workspace.RootPath,
            workspace.Paths,
            new StubRefactoringService(new RefactoringBacklogSnapshot()),
            new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.False(surface.IsValid);
        Assert.Contains(surface.Errors, error => error.Contains("runtime-hotspot-cross-family-patterns.md", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_DoesNotProjectHistoricalContinuationOrResolvedKindsAsActiveResidualPressure()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-hotspot-backlog-drain-governance.md", "# wave71");
        workspace.WriteFile("docs/runtime/runtime-hotspot-cross-family-patterns.md", "# wave71");

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
                            Title = "Host bootstrap queue",
                            Summary = "Pass 2 host queue",
                            PlanningTaskId = "T-CARD-568-001",
                            SuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92-P002",
                            PreviousSuggestedTaskId = "T-REFQ-host-bootstrap-dispatch-and-composition-f33a9c92",
                            ProofTarget = "host proof target",
                            ScopeRoots = ["src/CARVES.Runtime.Host/"],
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs"],
                            BacklogItemIds = ["RB-host-1"],
                            HotspotPaths = ["src/CARVES.Runtime.Host/Program.cs"],
                        },
                        new RefactoringHotspotQueue
                        {
                            QueueId = "operator_projection_and_control_plane",
                            FamilyId = "operator_projection_and_control_plane",
                            QueuePass = 2,
                            Title = "Operator queue",
                            Summary = "Pass 2 operator queue",
                            PlanningTaskId = "T-CARD-567-001",
                            SuggestedTaskId = "T-REFQ-operator-projection-and-control-plane-c5293c04-P002",
                            PreviousSuggestedTaskId = "T-REFQ-operator-projection-and-control-plane-c5293c04",
                            ProofTarget = "operator proof target",
                            ScopeRoots = ["src/CARVES.Runtime.Application/ControlPlane/"],
                            ValidationSurface = ["tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs"],
                            BacklogItemIds = ["RB-operator-1"],
                            HotspotPaths = ["src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.cs"],
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
                    Priority = "P2",
                    Status = RefactoringBacklogStatus.Resolved,
                },
                new RefactoringBacklogItem
                {
                    ItemId = "RB-operator-1",
                    Fingerprint = "file_too_large|src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.cs",
                    Kind = "file_too_large",
                    Path = "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.cs",
                    Priority = "P2",
                    Status = RefactoringBacklogStatus.Resolved,
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
            },
            new TaskNode
            {
                TaskId = "T-REFQ-operator-projection-and-control-plane-c5293c04",
                Title = "Operator queue task",
                Status = DomainTaskStatus.Completed,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Application/ControlPlane/"],
                Acceptance = ["accepted"]
            },
            new TaskNode
            {
                TaskId = "T-REFQ-operator-projection-and-control-plane-c5293c04-P002",
                Title = "Operator queue task (pass 2)",
                Status = DomainTaskStatus.Completed,
                Priority = "P2",
                Scope = ["src/CARVES.Runtime.Application/ControlPlane/"],
                Acceptance = ["accepted"]
            }
        ]);

        var service = new RuntimeHotspotCrossFamilyPatternService(
            workspace.RootPath,
            workspace.Paths,
            new StubRefactoringService(backlog),
            new TaskGraphService(new InMemoryTaskGraphRepository(graph), new ApplicationTaskScheduler()));

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal(2, surface.Counts.QueueFamilyCount);
        Assert.Equal(2, surface.Counts.ContinuedQueueCount);
        Assert.Equal(0, surface.Counts.ResidualPatternCount);
        Assert.Equal(0, surface.Counts.RepeatedBacklogKindPatternCount);
        Assert.DoesNotContain(surface.Patterns, pattern => pattern.PatternId == "residual_continuation_pressure");
        Assert.DoesNotContain(surface.Patterns, pattern => pattern.PatternType == "repeated_backlog_kind");
        Assert.Contains(surface.Patterns, pattern => pattern.PatternType == "validation_overlap");
    }

    private sealed class StubRefactoringService(RefactoringBacklogSnapshot backlog) : IRefactoringService
    {
        public RefactoringBacklogSnapshot LoadBacklog() => backlog;

        public RefactoringBacklogSnapshot DetectAndStore() => backlog;

        public RefactoringTaskMaterializationResult MaterializeSuggestedTasks() => new([], [], false, [], []);
    }
}
