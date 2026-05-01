using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackMismatchDiagnosticsServiceTests
{
    [Fact]
    public void Build_AlignedStateProjectsNoDiagnostics()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveRuntimePackAdmissionArtifact(CreateAdmission("1.2.3"));
        artifactRepository.SaveRuntimePackSelectionArtifact(RuntimePackSelectionTestData.CreateSelection("packsel-001", "1.2.3"));
        var executionRunService = new ExecutionRunService(workspace.Paths, artifactRepository);
        var service = new RuntimePackMismatchDiagnosticsService(workspace.Paths, artifactRepository, executionRunService);

        var surface = service.Build();

        Assert.Equal("runtime-pack-mismatch-diagnostics", surface.SurfaceId);
        Assert.Empty(surface.Diagnostics);
        Assert.Contains("aligned", surface.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_DivergentSelectionAndRecentEvidenceProjectBoundedNextActions()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveRuntimePackAdmissionArtifact(CreateAdmission("1.2.3"));
        artifactRepository.SaveRuntimePackSelectionArtifact(RuntimePackSelectionTestData.CreateSelection("packsel-old", "1.2.3"));

        var executionRunService = new ExecutionRunService(workspace.Paths, artifactRepository);
        var reportService = new ExecutionRunReportService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(CreateTask("T-CARD-334-DIAG"));
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
        reportService.Persist(completedRun);

        artifactRepository.SaveRuntimePackSelectionArtifact(RuntimePackSelectionTestData.CreateSelection("packsel-new", "1.2.4"));
        var service = new RuntimePackMismatchDiagnosticsService(workspace.Paths, artifactRepository, executionRunService);

        var surface = service.Build();

        Assert.Contains(surface.Diagnostics, item => item.DiagnosticCode == "selection_not_currently_admitted");
        Assert.Contains(surface.Diagnostics, item => item.DiagnosticCode == "recent_execution_diverges_from_current_selection");
        var divergence = Assert.Single(surface.Diagnostics, item => item.DiagnosticCode == "recent_execution_diverges_from_current_selection");
        Assert.Contains("T-CARD-334-DIAG", divergence.RelatedTaskIds);
        Assert.Contains(divergence.RecommendedActions, action => action == "inspect runtime-pack-execution-audit");
        Assert.Contains(divergence.RecommendedActions, action => action == "inspect runtime-pack-task-explainability T-CARD-334-DIAG");
    }

    [Fact]
    public void Build_DeclarativeContributionDriftProjectsDedicatedMismatchDiagnostic()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        workspace.WriteFile(
            "docs/product/reference-packs/runtime-pack-v1-security-review.json",
            """
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.firstparty.security-review",
              "packVersion": "0.1.0",
              "name": "CARVES First-Party Security Review",
              "description": "Bounded review rubric for security-sensitive changes without verdict authority.",
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
                "review_rubric"
              ],
              "requestedPermissions": {
                "readPaths": ["src/**", "tests/**", "docs/**"],
                "network": false,
                "env": false,
                "secrets": false,
                "truthWrite": false
              },
              "recipes": {
                "projectUnderstandingRecipes": [],
                "verificationRecipes": [],
                "reviewRubrics": [
                  {
                    "id": "security-review-rubric",
                    "description": "Review checklist.",
                    "checklistItems": [
                      {
                        "id": "security-input-validation",
                        "severity": "review",
                        "text": "Check input validation."
                      }
                    ]
                  }
                ]
              }
            }
            """);

        artifactRepository.SaveRuntimePackAdmissionArtifact(new RuntimePackAdmissionArtifact
        {
            PackId = "carves.firstparty.security-review",
            PackVersion = "0.1.0",
            Channel = "stable",
            RuntimeStandardVersion = "0.4.0",
            PackArtifactPath = ".ai/artifacts/packs/security-review.json",
            RuntimePackAttributionPath = ".ai/artifacts/packs/security-review.attribution.json",
            ArtifactRef = ".ai/artifacts/packs/security-review.json",
            ExecutionProfiles = new RuntimePackAdmissionProfileSelection
            {
                PolicyPreset = "core-default",
                GatePreset = "strict",
                ValidatorProfile = "default-validator",
                EnvironmentProfile = "workspace",
                RoutingProfile = "connected-lanes",
            },
            Source = new RuntimePackAdmissionSource
            {
                AssignmentMode = "overlay_assignment",
                AssignmentRef = "docs/product/reference-packs/runtime-pack-v1-security-review.json",
            },
            Summary = "Admitted review rubric pack.",
            ChecksPassed = ["runtime compatibility accepts local CARVES standard"],
        });
        artifactRepository.SaveRuntimePackSelectionArtifact(new RuntimePackSelectionArtifact
        {
            SelectionId = "packsel-review-old",
            PackId = "carves.firstparty.security-review",
            PackVersion = "0.1.0",
            Channel = "stable",
            RuntimeStandardVersion = "0.4.0",
            PackArtifactPath = ".ai/artifacts/packs/security-review.json",
            RuntimePackAttributionPath = ".ai/artifacts/packs/security-review.attribution.json",
            ArtifactRef = ".ai/artifacts/packs/security-review.json",
            ExecutionProfiles = new RuntimePackAdmissionProfileSelection
            {
                PolicyPreset = "core-default",
                GatePreset = "strict",
                ValidatorProfile = "default-validator",
                EnvironmentProfile = "workspace",
                RoutingProfile = "connected-lanes",
            },
            AdmissionSource = new RuntimePackAdmissionSource
            {
                AssignmentMode = "overlay_assignment",
                AssignmentRef = "docs/product/reference-packs/runtime-pack-v1-security-review.json",
            },
            AdmissionCapturedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            SelectionMode = "manual_local_assignment",
            SelectionReason = "select original declarative review pack",
            Summary = "Selected review rubric pack.",
            ChecksPassed = ["selection remains local-runtime scoped"],
        });

        var executionRunService = new ExecutionRunService(workspace.Paths, artifactRepository);
        var reportService = new ExecutionRunReportService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(CreateTask("T-CARD-334-RUBRIC-DRIFT"));
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
        reportService.Persist(completedRun);

        workspace.WriteFile(
            "docs/product/reference-packs/runtime-pack-v1-security-review.json",
            """
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.firstparty.security-review",
              "packVersion": "0.1.0",
              "name": "CARVES First-Party Security Review",
              "description": "Bounded review rubric for security-sensitive changes without verdict authority.",
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
                "review_rubric"
              ],
              "requestedPermissions": {
                "readPaths": ["src/**", "tests/**", "docs/**"],
                "network": false,
                "env": false,
                "secrets": false,
                "truthWrite": false
              },
              "recipes": {
                "projectUnderstandingRecipes": [],
                "verificationRecipes": [],
                "reviewRubrics": [
                  {
                    "id": "security-review-rubric",
                    "description": "Review checklist.",
                    "checklistItems": [
                      {
                        "id": "security-input-validation",
                        "severity": "review",
                        "text": "Check input validation."
                      },
                      {
                        "id": "security-protected-root-boundary",
                        "severity": "block",
                        "text": "Check protected-root mutation."
                      }
                    ]
                  }
                ]
              }
            }
            """);

        var service = new RuntimePackMismatchDiagnosticsService(workspace.Paths, artifactRepository, executionRunService);

        var surface = service.Build();

        var divergence = Assert.Single(surface.Diagnostics, item => item.DiagnosticCode == "recent_declarative_contributions_diverge_from_current_selection");
        Assert.Contains("T-CARD-334-RUBRIC-DRIFT", divergence.RelatedTaskIds);
        Assert.Contains(divergence.RecommendedActions, action => action == "inspect runtime-pack-execution-audit");
        Assert.Contains(divergence.RecommendedActions, action => action == "inspect runtime-pack-task-explainability T-CARD-334-RUBRIC-DRIFT");
    }

    private static RuntimePackAdmissionArtifact CreateAdmission(string packVersion)
    {
        return new RuntimePackAdmissionArtifact
        {
            PackId = "carves.runtime.core",
            PackVersion = packVersion,
            Channel = "stable",
            RuntimeStandardVersion = "0.4.0",
            PackArtifactPath = $".ai/artifacts/packs/core-{packVersion}.json",
            RuntimePackAttributionPath = $".ai/artifacts/packs/core-{packVersion}.attribution.json",
            ArtifactRef = $".ai/artifacts/packs/core-{packVersion}.json",
            ExecutionProfiles = new RuntimePackAdmissionProfileSelection
            {
                PolicyPreset = "core-default",
                GatePreset = "strict",
                ValidatorProfile = "default-validator",
                EnvironmentProfile = "workspace",
                RoutingProfile = "connected-lanes",
            },
            Source = new RuntimePackAdmissionSource
            {
                AssignmentMode = "overlay_assignment",
                AssignmentRef = $"admission-{packVersion}",
            },
            ChecksPassed =
            [
                "policy preset matches",
                "runtime compatibility accepts local CARVES standard",
            ],
            Summary = $"Admitted carves.runtime.core@{packVersion} (stable).",
        };
    }

    private static TaskNode CreateTask(string taskId)
    {
        return new TaskNode
        {
            TaskId = taskId,
            CardId = "CARD-334",
            Title = "Runtime pack mismatch test",
            Description = "Exercise bounded mismatch diagnostics over recent execution evidence.",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Execution,
            Priority = "P1",
            Scope = ["src/CARVES.Runtime.Application/Platform/"],
            Acceptance = ["bounded mismatch diagnostics exist"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }
}
