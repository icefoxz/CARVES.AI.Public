using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackReviewRubricProjectionServiceTests
{
    [Fact]
    public void TryBuildCurrentProjection_ReturnsChecklistProjectionForSelectedDeclarativeReviewPack()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
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
                      },
                      {
                        "id": "security-protected-root-boundary",
                        "severity": "block",
                        "text": "Check whether the change attempts to widen protected-root mutation, review authority, or truth-write authority."
                      }
                    ]
                  }
                ]
              }
            }
            """);

        artifactRepository.SaveRuntimePackSelectionArtifact(new RuntimePackSelectionArtifact
        {
            SelectionId = "packsel-review-001",
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
            AdmissionCapturedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            SelectionMode = "manual_local_assignment",
            SelectionReason = "select declarative review rubric pack",
            Summary = "Selected review rubric pack.",
            ChecksPassed = ["selection remains local-runtime scoped"],
        });

        var service = new RuntimePackReviewRubricProjectionService(workspace.RootPath, artifactRepository);

        var projection = service.TryBuildCurrentProjection();

        Assert.NotNull(projection);
        Assert.Equal("carves.firstparty.security-review", projection!.PackId);
        Assert.Equal("0.1.0", projection.PackVersion);
        Assert.Equal(1, projection.RubricCount);
        Assert.Equal(2, projection.ChecklistItemCount);
        Assert.Equal("docs/product/reference-packs/runtime-pack-v1-security-review.json", projection.ManifestPath);
        Assert.Contains("without changing review verdict authority", projection.Summary, StringComparison.Ordinal);
        Assert.Collection(
            projection.Rubrics,
            rubric =>
            {
                Assert.Equal("security-review-rubric", rubric.RubricId);
                Assert.Equal(2, rubric.ChecklistItems.Count);
                Assert.Collection(
                    rubric.ChecklistItems,
                    item =>
                    {
                        Assert.Equal("security-input-validation", item.ChecklistItemId);
                        Assert.Equal("review", item.Severity);
                    },
                    item =>
                    {
                        Assert.Equal("security-protected-root-boundary", item.ChecklistItemId);
                        Assert.Equal("block", item.Severity);
                    });
            });
    }

    [Fact]
    public void TryBuildCurrentProjection_ReturnsNullWhenSelectionDoesNotReferenceDeclarativeManifest()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveRuntimePackSelectionArtifact(RuntimePackSelectionTestData.CreateSelection("packsel-001", "1.2.3"));
        var service = new RuntimePackReviewRubricProjectionService(workspace.RootPath, artifactRepository);

        var projection = service.TryBuildCurrentProjection();

        Assert.Null(projection);
    }
}
