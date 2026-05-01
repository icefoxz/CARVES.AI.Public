using System.Text.Json;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackV1ManifestAdmissionBridgeServiceTests
{
    [Fact]
    public void Admit_PersistsRuntimeAdmissionTruthForDeclarativeManifest()
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.WriteFile(
            "artifacts/runtime-pack-v1-dotnet-webapi.json",
            """
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.firstparty.dotnet-webapi",
              "packVersion": "0.1.0",
              "name": "CARVES First-Party .NET Web API",
              "description": "Bounded project-understanding and verification recipes for ASP.NET Core Web API repositories.",
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
                "languages": ["csharp"],
                "frameworkHints": ["aspnetcore", "dotnet"],
                "repoSignals": ["*.sln", "*.csproj", "Program.cs"]
              },
              "capabilityKinds": [
                "project_understanding_recipe",
                "verification_recipe"
              ],
              "requestedPermissions": {
                "readPaths": ["src/**", "tests/**", "*.sln", "*.csproj", "Program.cs"],
                "network": false,
                "env": false,
                "secrets": false,
                "truthWrite": false
              },
              "recipes": {
                "projectUnderstandingRecipes": [
                  {
                    "id": "dotnet-webapi-project-understanding",
                    "description": "Prioritize ASP.NET Core source, tests, and project files for bounded context shaping.",
                    "repoSignals": ["*.sln", "*.csproj", "Program.cs"],
                    "frameworkHints": ["aspnetcore", "dotnet"],
                    "includeGlobs": ["src/**/*.cs", "tests/**/*.cs", "*.sln", "*.csproj", "Program.cs"],
                    "excludeGlobs": ["**/bin/**", "**/obj/**", ".git/**"],
                    "priorityRules": [
                      {
                        "id": "dotnet-src-priority",
                        "glob": "src/**/*.cs",
                        "weight": 90
                      }
                    ]
                  }
                ],
                "verificationRecipes": [
                  {
                    "id": "dotnet-build-and-test",
                    "description": "Bounded build and test commands for a .NET Web API repository.",
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
        var service = CreateService(workspace.RootPath, out var artifactRepository);

        var result = service.Admit(manifestPath);

        Assert.True(result.Admitted);
        Assert.NotNull(result.GeneratedPackArtifactPath);
        Assert.NotNull(result.GeneratedAttributionPath);
        var artifact = artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact();
        Assert.NotNull(artifact);
        Assert.Equal("carves.firstparty.dotnet-webapi", artifact!.PackId);
        Assert.Equal("0.1.0", artifact.PackVersion);
        Assert.Equal("stable", artifact.Channel);
        Assert.Equal(result.GeneratedPackArtifactPath, artifact.PackArtifactPath);
        Assert.Equal(result.GeneratedAttributionPath, artifact.RuntimePackAttributionPath);

        var generatedArtifactPath = Path.Combine(workspace.RootPath, result.GeneratedPackArtifactPath!.Replace('/', Path.DirectorySeparatorChar));
        var generatedAttributionPath = Path.Combine(workspace.RootPath, result.GeneratedAttributionPath!.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(generatedArtifactPath));
        Assert.True(File.Exists(generatedAttributionPath));

        using var generatedArtifact = JsonDocument.Parse(File.ReadAllText(generatedArtifactPath));
        Assert.Equal("runtime_pack", generatedArtifact.RootElement.GetProperty("packType").GetString());
        Assert.Equal("stable", generatedArtifact.RootElement.GetProperty("channel").GetString());
        Assert.Equal("runtime-pack-v1-bridge-sha256", generatedArtifact.RootElement.GetProperty("signature").GetProperty("scheme").GetString());
        Assert.Equal("runtime-pack-v1-manifest-bridge", generatedArtifact.RootElement.GetProperty("provenance").GetProperty("sourcePackLine").GetString());

        using var generatedAttribution = JsonDocument.Parse(File.ReadAllText(generatedAttributionPath));
        Assert.Equal("overlay_assignment", generatedAttribution.RootElement.GetProperty("source").GetProperty("assignmentMode").GetString());
        Assert.Equal("artifacts/runtime-pack-v1-dotnet-webapi.json", generatedAttribution.RootElement.GetProperty("source").GetProperty("assignmentRef").GetString());
        Assert.Equal(result.GeneratedPackArtifactPath, generatedAttribution.RootElement.GetProperty("artifactRef").GetString());
    }

    [Fact]
    public void Admit_RejectsUnsupportedCompatibilityExpressionBeforeWritingAdmissionTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.WriteFile(
            "artifacts/runtime-pack-v1-unsupported-compat.json",
            """
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.community.unsupported-compat",
              "packVersion": "0.1.0",
              "name": "Unsupported Runtime Compatibility",
              "publisher": {
                "name": "community",
                "trustLevel": "community"
              },
              "license": {
                "expression": "MIT"
              },
              "compatibility": {
                "carvesRuntime": "^0.4.0"
              },
              "capabilityKinds": [
                "review_rubric"
              ],
              "requestedPermissions": {
                "readPaths": ["src/**"],
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
                    "id": "unsupported-compat-review",
                    "description": "Review rubric with unsupported runtime compatibility expression.",
                    "checklistItems": [
                      {
                        "id": "check-unsupported-compat",
                        "severity": "review",
                        "text": "Ensure the manifest bridge rejects unsupported compatibility expressions."
                      }
                    ]
                  }
                ]
              }
            }
            """);
        var service = CreateService(workspace.RootPath, out var artifactRepository);

        var result = service.Admit(manifestPath);

        Assert.False(result.Admitted);
        Assert.Contains("runtime_pack_v1_runtime_compatibility_expression_unsupported", result.FailureCodes);
        Assert.Null(artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact());
        Assert.Null(result.GeneratedPackArtifactPath);
        Assert.Null(result.GeneratedAttributionPath);
    }

    private static RuntimePackV1ManifestAdmissionBridgeService CreateService(string rootPath, out JsonRuntimeArtifactRepository artifactRepository)
    {
        var paths = ControlPlanePaths.FromRepoRoot(rootPath);
        var configRepository = new FileControlPlaneConfigRepository(paths);
        artifactRepository = new JsonRuntimeArtifactRepository(paths);
        var validationService = new SpecificationValidationService(rootPath, paths, configRepository);
        return new RuntimePackV1ManifestAdmissionBridgeService(rootPath, paths, configRepository, artifactRepository, validationService);
    }
}
