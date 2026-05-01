using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackExecutionAuditServiceTests
{
    [Fact]
    public void Build_WithoutCurrentSelection_RemainsExplainable()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths, artifactRepository);
        var service = new RuntimePackExecutionAuditService(workspace.Paths, artifactRepository, executionRunService);

        var surface = service.Build();

        Assert.Equal("runtime-pack-execution-audit", surface.SurfaceId);
        Assert.Null(surface.CurrentSelection);
        Assert.Equal(0, surface.Coverage.RecentAttributedRunCount);
        Assert.Equal(0, surface.Coverage.RecentAttributedReportCount);
        Assert.Contains("no current selection", surface.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_ProjectsCurrentSelectionCoverageAcrossRunsAndReports()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveRuntimePackSelectionArtifact(new RuntimePackSelectionArtifact
        {
            SelectionId = "packsel-current",
            PackId = "carves.runtime.core",
            PackVersion = "1.2.3",
            Channel = "stable",
            ArtifactRef = ".ai/artifacts/packs/core-1.2.3.json",
            ExecutionProfiles = new RuntimePackAdmissionProfileSelection
            {
                PolicyPreset = "core-default",
                GatePreset = "strict",
                ValidatorProfile = "default-validator",
                RoutingProfile = "connected-lanes",
                EnvironmentProfile = "workspace",
            },
            SelectionMode = "manual_local_assignment",
            Summary = "Selected current local pack.",
        });

        var executionRunService = new ExecutionRunService(workspace.Paths, artifactRepository);
        var run = executionRunService.PrepareRunForDispatch(CreateTask("T-CARD-330-APP"));
        var completedRun = run with
        {
            Status = ExecutionRunStatus.Completed,
            EndedAtUtc = DateTimeOffset.UtcNow,
        };
        File.WriteAllText(
            executionRunService.GetRunPath(completedRun.TaskId, completedRun.RunId),
            System.Text.Json.JsonSerializer.Serialize(
                completedRun,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }));

        var reportService = new ExecutionRunReportService(workspace.Paths);
        reportService.Persist(
            completedRun,
            taskRunReport: new TaskRunReport
            {
                TaskId = completedRun.TaskId,
                WorkerExecution = new WorkerExecutionResult
                {
                    TaskId = completedRun.TaskId,
                    ChangedFiles =
                    [
                        "src/CARVES.Runtime.Application/Platform/RuntimePackExecutionAuditService.cs"
                    ],
                },
            });

        var service = new RuntimePackExecutionAuditService(workspace.Paths, artifactRepository, executionRunService);

        var surface = service.Build();

        Assert.NotNull(surface.CurrentSelection);
        Assert.Equal(1, surface.Coverage.RecentAttributedRunCount);
        Assert.Equal(1, surface.Coverage.RecentAttributedReportCount);
        Assert.Equal(1, surface.Coverage.CurrentSelectionRunCount);
        Assert.Equal(1, surface.Coverage.CurrentSelectionReportCount);
        Assert.Single(surface.RecentRuns);
        Assert.Single(surface.RecentReports);
        Assert.True(surface.RecentRuns[0].MatchesCurrentSelection);
        Assert.True(surface.RecentReports[0].MatchesCurrentSelection);
    }

    [Fact]
    public void Build_ProjectsDeclarativeContributionSnapshotAcrossCurrentSelectionRunsAndReports()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        workspace.WriteFile(
            "docs/product/reference-packs/runtime-pack-v1-dotnet-webapi.json",
            """
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.firstparty.dotnet-webapi",
              "packVersion": "0.1.0",
              "name": "CARVES First-Party .NET Web API",
              "description": "Declarative project-understanding and verification pack for ASP.NET Core Web API projects.",
              "publisher": {
                "name": "CARVES",
                "trustLevel": "first_party",
                "url": null
              },
              "license": {
                "expression": "MIT",
                "url": null
              },
              "compatibility": {
                "carvesRuntime": ">=0.4.0"
              },
              "capabilityKinds": [
                "project_understanding_recipe",
                "verification_recipe"
              ],
              "requestedPermissions": {
                "readPaths": ["src/**", "tests/**", "*.sln", "*.csproj"],
                "network": false,
                "env": false,
                "secrets": false,
                "truthWrite": false
              },
              "recipes": {
                "projectUnderstandingRecipes": [
                  {
                    "id": "dotnet-solution-understanding",
                    "description": "Prefer runtime and test files.",
                    "repoSignals": ["*.sln"],
                    "frameworkHints": ["aspnetcore"],
                    "includeGlobs": ["src/**/*.cs", "tests/**/*.cs"],
                    "excludeGlobs": ["**/bin/**", "**/obj/**"],
                    "priorityRules": [
                      {
                        "id": "prefer-csharp",
                        "glob": "src/**/*.cs",
                        "weight": 10
                      }
                    ]
                  }
                ],
                "verificationRecipes": [
                  {
                    "id": "dotnet-build-and-test",
                    "description": "Run dotnet validation.",
                    "commands": [
                      {
                        "id": "dotnet-build",
                        "kind": "known_tool_command",
                        "executable": "dotnet",
                        "args": ["build"],
                        "cwd": ".",
                        "required": true
                      },
                      {
                        "id": "dotnet-test",
                        "kind": "known_tool_command",
                        "executable": "dotnet",
                        "args": ["test"],
                        "cwd": ".",
                        "required": true
                      }
                    ]
                  }
                ],
                "reviewRubrics": []
              }
            }
            """);

        artifactRepository.SaveRuntimePackSelectionArtifact(new RuntimePackSelectionArtifact
        {
            SelectionId = "packsel-current",
            PackId = "carves.firstparty.dotnet-webapi",
            PackVersion = "0.1.0",
            Channel = "stable",
            ArtifactRef = ".ai/artifacts/packs/dotnet-webapi.json",
            ExecutionProfiles = new RuntimePackAdmissionProfileSelection
            {
                PolicyPreset = "core-default",
                GatePreset = "strict",
                ValidatorProfile = "default-validator",
                RoutingProfile = "connected-lanes",
                EnvironmentProfile = "workspace",
            },
            AdmissionSource = new RuntimePackAdmissionSource
            {
                AssignmentMode = "overlay_assignment",
                AssignmentRef = "docs/product/reference-packs/runtime-pack-v1-dotnet-webapi.json",
            },
            SelectionMode = "manual_local_assignment",
            Summary = "Selected declarative current local pack.",
        });

        var executionRunService = new ExecutionRunService(workspace.Paths, artifactRepository);
        var run = executionRunService.PrepareRunForDispatch(CreateTask("T-CARD-330-DECL"));
        var completedRun = run with
        {
            Status = ExecutionRunStatus.Completed,
            EndedAtUtc = DateTimeOffset.UtcNow,
        };
        File.WriteAllText(
            executionRunService.GetRunPath(completedRun.TaskId, completedRun.RunId),
            System.Text.Json.JsonSerializer.Serialize(
                completedRun,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }));

        var reportService = new ExecutionRunReportService(workspace.Paths);
        reportService.Persist(completedRun);
        var service = new RuntimePackExecutionAuditService(workspace.Paths, artifactRepository, executionRunService);

        var surface = service.Build();

        Assert.NotNull(surface.CurrentSelection);
        Assert.NotNull(surface.CurrentSelection!.DeclarativeContribution);
        Assert.Equal(
            "docs/product/reference-packs/runtime-pack-v1-dotnet-webapi.json",
            surface.CurrentSelection.DeclarativeContribution!.ManifestPath);
        Assert.Contains("project_understanding_recipe", surface.CurrentSelection.DeclarativeContribution.CapabilityKinds);
        Assert.Contains("verification_recipe", surface.CurrentSelection.DeclarativeContribution.CapabilityKinds);
        Assert.Equal(1, surface.Coverage.RecentDeclarativeRunCount);
        Assert.Equal(1, surface.Coverage.RecentDeclarativeReportCount);
        Assert.Equal(1, surface.Coverage.CurrentSelectionContributionRunCount);
        Assert.Equal(1, surface.Coverage.CurrentSelectionContributionReportCount);
        Assert.True(surface.RecentRuns[0].MatchesCurrentDeclarativeContribution);
        Assert.True(surface.RecentReports[0].MatchesCurrentDeclarativeContribution);
        Assert.NotNull(surface.RecentRuns[0].DeclarativeContribution);
        Assert.Contains("dotnet-build-and-test", surface.RecentRuns[0].DeclarativeContribution!.VerificationRecipeIds);
        Assert.Contains("dotnet-test", surface.RecentRuns[0].DeclarativeContribution!.VerificationCommandIds);
    }

    private static TaskNode CreateTask(string taskId)
    {
        return new TaskNode
        {
            TaskId = taskId,
            CardId = "CARD-330",
            Title = "Execution audit test",
            Description = "Exercise runtime pack execution audit projection.",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Execution,
            Priority = "P1",
            Scope = ["src/CARVES.Runtime.Application/Platform/"],
            Acceptance = ["pack execution audit exists"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }
}
