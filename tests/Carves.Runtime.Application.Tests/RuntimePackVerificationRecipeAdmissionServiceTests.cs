using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Platform.SurfaceModels;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackVerificationRecipeAdmissionServiceTests
{
    [Fact]
    public void Resolve_SelectedDeclarativePackPersistsDecisionRecordsAndFiltersCommands()
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.WriteFile(
            "artifacts/runtime-pack-v1-verification.json",
            """
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.firstparty.verification-suite",
              "packVersion": "0.1.0",
              "name": "Verification Suite",
              "description": "Exercise all Pack v1 verification command kinds.",
              "publisher": {
                "name": "CARVES",
                "trustLevel": "first_party"
              },
              "license": {
                "expression": "MIT"
              },
              "compatibility": {
                "carvesRuntime": ">=0.4.0"
              },
              "capabilityKinds": [
                "verification_recipe"
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
                "verificationRecipes": [
                  {
                    "id": "verification-suite",
                    "description": "Exercise known tool, package script, repo script, and shell command handling.",
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
                        "id": "npm-test",
                        "kind": "package_manager_script",
                        "executable": "npm",
                        "args": ["test"],
                        "cwd": ".",
                        "required": true
                      },
                      {
                        "id": "repo-verify",
                        "kind": "repo_script",
                        "executable": "scripts/verify.sh",
                        "args": [],
                        "cwd": ".",
                        "required": true
                      },
                      {
                        "id": "shell-wrapper",
                        "kind": "shell_command",
                        "executable": "bash",
                        "args": ["-lc", "echo unsafe"],
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

        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveRuntimePackSelectionArtifact(new RuntimePackSelectionArtifact
        {
            SelectionId = "packsel-verification-suite",
            PackId = "carves.firstparty.verification-suite",
            PackVersion = "0.1.0",
            Channel = "stable",
            RuntimeStandardVersion = "0.4.0",
            PackArtifactPath = ".ai/artifacts/packs/verification-suite.json",
            RuntimePackAttributionPath = ".ai/artifacts/packs/verification-suite.attribution.json",
            ArtifactRef = ".ai/artifacts/packs/verification-suite.json",
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
                AssignmentRef = "artifacts/runtime-pack-v1-verification.json",
            },
            AdmissionCapturedAt = DateTimeOffset.UtcNow,
            SelectionMode = "manual_local_assignment",
            SelectionReason = "Selected for Phase 2B verification recipe admission coverage.",
            Summary = "Selected declarative pack for verification recipe admission coverage.",
            ChecksPassed = ["selection is derived from admitted current evidence"],
        });

        var task = new TaskNode
        {
            TaskId = "T-PACK-VERIFY-001",
            Title = "Admit Pack verification recipes",
            Description = "Exercise Pack v1 verification recipe admission.",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Validation = new ValidationPlan
            {
                Commands = [["dotnet", "restore"]],
            },
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["execution_run_active_id"] = "RUN-T-PACK-VERIFY-001",
            },
        };

        var service = new RuntimePackVerificationRecipeAdmissionService(workspace.RootPath, workspace.Paths, artifactRepository);

        var result = service.Resolve(task, task.Validation.Commands);

        Assert.True(result.HasRuntimePackContribution);
        Assert.Equal(1, result.AdmittedCommandCount);
        Assert.Equal(1, result.ElevatedRiskCommandCount);
        Assert.Equal(1, result.BlockedCommandCount);
        Assert.Equal(1, result.RejectedCommandCount);
        Assert.Equal(["verification-suite"], result.RecipeIds);
        Assert.Equal(4, result.DecisionIds.Count);
        Assert.Equal(4, result.DecisionPaths.Count);

        var effectiveCommands = result.EffectiveValidationCommands
            .Select(command => string.Join(' ', command))
            .ToArray();
        Assert.Contains("dotnet restore", effectiveCommands);
        Assert.Contains("dotnet build", effectiveCommands);
        Assert.Contains("npm test", effectiveCommands);
        Assert.DoesNotContain("scripts/verify.sh", effectiveCommands);
        Assert.DoesNotContain("bash -lc echo unsafe", effectiveCommands);

        foreach (var decisionPath in result.DecisionPaths)
        {
            var fullPath = Path.Combine(workspace.RootPath, decisionPath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath), $"Expected decision artifact to exist: {decisionPath}");
        }

        var decisionsByCommand = result.DecisionPaths
            .Select(path =>
            {
                var fullPath = Path.Combine(workspace.RootPath, path.Replace('/', Path.DirectorySeparatorChar));
                using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
                return new
                {
                    CommandId = document.RootElement.GetProperty("commandRef").GetProperty("commandId").GetString(),
                    Verdict = document.RootElement.GetProperty("decision").GetProperty("verdict").GetString(),
                };
            })
            .ToDictionary(item => item.CommandId!, item => item.Verdict!, StringComparer.Ordinal);

        Assert.Equal("admitted", decisionsByCommand["dotnet-build"]);
        Assert.Equal("admitted_with_elevated_risk", decisionsByCommand["npm-test"]);
        Assert.Equal("blocked", decisionsByCommand["repo-verify"]);
        Assert.Equal("rejected", decisionsByCommand["shell-wrapper"]);
    }

    [Fact]
    public void Resolve_WithoutActiveRunIdLeavesValidationCommandsUnchanged()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var task = new TaskNode
        {
            TaskId = "T-PACK-VERIFY-002",
            Title = "Skip Pack verification recipes",
            Description = "No active run id should keep base validation commands unchanged.",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Validation = new ValidationPlan
            {
                Commands = [["dotnet", "restore"]],
            },
        };
        var service = new RuntimePackVerificationRecipeAdmissionService(workspace.RootPath, workspace.Paths, artifactRepository);

        var result = service.Resolve(task, task.Validation.Commands);

        Assert.False(result.HasRuntimePackContribution);
        Assert.Equal(["dotnet restore"], result.EffectiveValidationCommands.Select(command => string.Join(' ', command)).ToArray());
        Assert.Empty(result.DecisionIds);
        Assert.Empty(result.DecisionPaths);
    }
}
