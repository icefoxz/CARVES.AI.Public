using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackTaskExplainabilityServiceTests
{
    [Fact]
    public void Build_FiltersRecentEvidenceToOneTaskAndExplainsCoverageAgainstCurrentSelection()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var runService = new ExecutionRunService(workspace.Paths, artifactRepository);
        var reportService = new ExecutionRunReportService(workspace.Paths);
        var service = new RuntimePackTaskExplainabilityService(workspace.Paths, artifactRepository, runService);

        artifactRepository.SaveRuntimePackSelectionArtifact(RuntimePackSelectionTestData.CreateSelection("packsel-001", "1.2.3"));
        PersistCompletedRun(runService, reportService, CreateTask("T-PACK-A"));
        PersistCompletedRun(runService, reportService, CreateTask("T-PACK-B"));

        artifactRepository.SaveRuntimePackSelectionArtifact(RuntimePackSelectionTestData.CreateSelection("packsel-002", "1.2.4"));
        PersistCompletedRun(runService, reportService, CreateTask("T-PACK-A"));

        var surface = service.Build("T-PACK-A");

        Assert.Equal("runtime-pack-task-explainability", surface.SurfaceId);
        Assert.Equal("T-PACK-A", surface.TaskId);
        Assert.NotNull(surface.CurrentSelection);
        Assert.Equal("1.2.4", surface.CurrentSelection!.PackVersion);
        Assert.Equal(2, surface.RecentRuns.Count);
        Assert.Equal(2, surface.RecentReports.Count);
        Assert.All(surface.RecentRuns, entry => Assert.Equal("T-PACK-A", entry.TaskId));
        Assert.All(surface.RecentReports, entry => Assert.Equal("T-PACK-A", entry.TaskId));
        Assert.Equal(1, surface.Coverage.CurrentSelectionRunCount);
        Assert.Equal(1, surface.Coverage.DivergentRunCount);
        Assert.Equal(1, surface.Coverage.CurrentSelectionReportCount);
        Assert.Equal(1, surface.Coverage.DivergentReportCount);
    }

    [Fact]
    public void Build_ThrowsWhenTaskIdIsBlank()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var runService = new ExecutionRunService(workspace.Paths, artifactRepository);
        var service = new RuntimePackTaskExplainabilityService(workspace.Paths, artifactRepository, runService);

        Assert.Throws<InvalidOperationException>(() => service.Build(" "));
    }

    [Fact]
    public void Build_ProjectsCurrentReviewRubricFromSelectedDeclarativeManifest()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var runService = new ExecutionRunService(workspace.Paths, artifactRepository);
        var service = new RuntimePackTaskExplainabilityService(workspace.Paths, artifactRepository, runService);
        var manifestPath = workspace.WriteFile(
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
                "carvesRuntime": ">=0.4.0",
                "frameworkHints": [
                  "security-review"
                ]
              },
              "capabilityKinds": [
                "review_rubric"
              ],
              "requestedPermissions": {
                "readPaths": [
                  "src/**",
                  "tests/**",
                  "docs/**"
                ],
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
                    "description": "Review checklist for boundary-sensitive or security-sensitive modifications.",
                    "checklistItems": [
                      {
                        "id": "security-input-validation",
                        "severity": "review",
                        "text": "Check whether new or changed input paths preserve validation and fail-closed behavior."
                      }
                    ]
                  }
                ]
              }
            }
            """);

        artifactRepository.SaveRuntimePackSelectionArtifact(new RuntimePackSelectionArtifact
        {
            SelectionId = "packsel-review-002",
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
                AssignmentRef = Path.GetRelativePath(workspace.RootPath, manifestPath).Replace('\\', '/'),
            },
            AdmissionCapturedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            SelectionMode = "manual_local_assignment",
            SelectionReason = "select review rubric pack",
            Summary = "Selected review rubric pack.",
            ChecksPassed = ["selection remains local-runtime scoped"],
        });

        var surface = service.Build("T-PACK-REVIEW");

        Assert.NotNull(surface.CurrentReviewRubricProjection);
        Assert.Equal("carves.firstparty.security-review", surface.CurrentReviewRubricProjection!.PackId);
        Assert.Equal(1, surface.CurrentReviewRubricProjection.RubricCount);
        Assert.Equal(1, surface.CurrentReviewRubricProjection.ChecklistItemCount);
    }

    private static void PersistCompletedRun(ExecutionRunService runService, ExecutionRunReportService reportService, TaskNode task)
    {
        var run = runService.PrepareRunForDispatch(task);
        var completed = runService.CompleteRun(run, null);
        reportService.Persist(completed);
    }

    private static TaskNode CreateTask(string taskId)
    {
        return new TaskNode
        {
            TaskId = taskId,
            CardId = "CARD-331",
            Title = "Runtime pack explainability test",
            Description = "Exercise task-scoped pack explainability truth.",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Execution,
            Priority = "P1",
            Scope = ["src/CARVES.Runtime.Application/Platform/"],
            Acceptance = ["task explainability stays task-scoped"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }
}
