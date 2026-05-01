using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class SpecificationValidationServiceTests
{
    [Fact]
    public void ValidateTask_PassesForCanonicalTaskNode()
    {
        using var workspace = new TemporaryWorkspace();
        var taskPath = workspace.WriteFile(
            ".ai/tasks/nodes/T-VALIDATE-001.json",
            """
            {
              "schema_version": 1,
              "task_id": "T-VALIDATE-001",
              "title": "Validate task truth",
              "description": "Ensure the validator accepts current task-node shape.",
              "status": "pending",
              "task_type": "execution",
              "priority": "P1",
              "source": "HUMAN",
              "card_id": "CARD-VAL",
              "dependencies": [],
              "scope": ["src/"],
              "acceptance": ["validation passes"],
              "constraints": [],
              "validation": {
                "commands": [],
                "checks": [],
                "expected_evidence": []
              },
              "retry_count": 0,
              "capabilities": [],
              "metadata": {},
              "planner_review": {
                "verdict": "continue",
                "reason": "",
                "acceptance_met": false,
                "boundary_preserved": true,
                "scope_drift_detected": false,
                "follow_up_suggestions": []
              },
              "created_at": "2026-03-19T00:00:00+00:00",
              "updated_at": "2026-03-19T00:00:00+00:00"
            }
            """);
        var service = CreateService(workspace.RootPath);

        var result = service.ValidateTask(taskPath);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Issues, issue => string.Equals(issue.Severity, "error", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateTask_FailsWhenStatusIsNotCanonical()
    {
        using var workspace = new TemporaryWorkspace();
        var taskPath = workspace.WriteFile(
            ".ai/tasks/nodes/T-VALIDATE-002.json",
            """
            {
              "schema_version": 1,
              "task_id": "T-VALIDATE-002",
              "title": "Validate task truth",
              "description": "Ensure the validator rejects non-canonical status casing.",
              "status": "Completed",
              "task_type": "execution",
              "priority": "P1",
              "source": "HUMAN",
              "card_id": "CARD-VAL",
              "dependencies": [],
              "scope": ["src/"],
              "acceptance": ["validation fails"],
              "constraints": [],
              "validation": {
                "commands": [],
                "checks": [],
                "expected_evidence": []
              },
              "retry_count": 0,
              "capabilities": [],
              "metadata": {},
              "planner_review": {
                "verdict": "continue",
                "reason": "",
                "acceptance_met": false,
                "boundary_preserved": true,
                "scope_drift_detected": false,
                "follow_up_suggestions": []
              },
              "created_at": "2026-03-19T00:00:00+00:00",
              "updated_at": "2026-03-19T00:00:00+00:00"
            }
            """);
        var service = CreateService(workspace.RootPath);

        var result = service.ValidateTask(taskPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "task_status_invalid");
    }

    [Fact]
    public void ValidateSafety_DeniesWorkerWriteToManagedControlPlanePath()
    {
        using var workspace = new TemporaryWorkspace();
        var service = CreateService(workspace.RootPath);

        var allowed = service.ValidateSafety("worker", "write", ["src/Foo.cs"]);
        var denied = service.ValidateSafety("worker", "write", [".ai/tasks/graph.json"]);

        Assert.True(allowed.IsValid);
        Assert.False(denied.IsValid);
        Assert.Contains(denied.Issues, issue => issue.Code == "managed_control_plane_write_forbidden");
    }

    [Fact]
    public void ValidatePackArtifact_PassesForCanonicalPackArtifact()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactPath = workspace.WriteFile(
            "artifacts/pack-artifact.json",
            """
            {
              "schemaVersion": "1.0",
              "packId": "carves.runtime.core",
              "packVersion": "1.2.3",
              "packType": "runtime_pack",
              "channel": "stable",
              "runtimeCompatibility": {
                "minVersion": "0.4.0",
                "maxVersion": "0.4.x"
              },
              "kernelCompatibility": {
                "minVersion": "0.1.0",
                "maxVersion": null
              },
              "executionProfiles": {
                "policyPreset": "core-default",
                "gatePreset": "strict",
                "validatorProfile": "default-validator",
                "environmentProfile": "workspace",
                "routingProfile": "connected-lanes",
                "providerAllowlist": ["codex", "openai"]
              },
              "operatorChecklistRefs": ["docs/checklists/core-release.md"],
              "signature": {
                "scheme": "sha256-rsa",
                "keyId": "core-signing-key",
                "digest": "sha256:abc123"
              },
              "provenance": {
                "publishedAtUtc": "2026-03-31T00:00:00+00:00",
                "publishedBy": "operator@carves",
                "sourcePackLine": "core-stable",
                "sourceGenerationId": "gen-001"
              },
              "releaseNoteRef": "docs/releases/core-1.2.3.md",
              "parentPackVersion": null,
              "approvalRef": "APP-001",
              "supersedes": ["1.2.2"]
            }
            """);
        var service = CreateService(workspace.RootPath);

        var result = service.ValidatePackArtifact(artifactPath);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Issues, issue => string.Equals(issue.Severity, "error", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatePackArtifact_FailsWhenRequiredProvenanceIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        var artifactPath = workspace.WriteFile(
            "artifacts/pack-artifact-invalid.json",
            """
            {
              "schemaVersion": "1.0",
              "packId": "carves.runtime.core",
              "packVersion": "1.2.3",
              "packType": "runtime_pack",
              "channel": "stable",
              "runtimeCompatibility": {
                "minVersion": "0.4.0"
              },
              "kernelCompatibility": {
                "minVersion": "0.1.0"
              },
              "executionProfiles": {
                "policyPreset": "core-default",
                "gatePreset": "strict",
                "validatorProfile": "default-validator",
                "environmentProfile": "workspace"
              },
              "operatorChecklistRefs": ["docs/checklists/core-release.md"],
              "signature": {
                "scheme": "sha256-rsa",
                "keyId": "core-signing-key",
                "digest": "sha256:abc123"
              },
              "provenance": {
                "publishedAtUtc": "2026-03-31T00:00:00+00:00",
                "publishedBy": "operator@carves",
                "sourcePackLine": "",
                "sourceGenerationId": "gen-001"
              }
            }
            """);
        var service = CreateService(workspace.RootPath);

        var result = service.ValidatePackArtifact(artifactPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "sourcePackLine_missing");
    }

    [Fact]
    public void ValidateRuntimePackV1_PassesForCanonicalManifest()
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.WriteFile(
            "artifacts/runtime-pack-v1.json",
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
                    "includeGlobs": ["src/**/*.cs", "tests/**/*.cs"],
                    "excludeGlobs": ["**/bin/**", "**/obj/**"],
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
                      }
                    ]
                  }
                ],
                "reviewRubrics": []
              }
            }
            """);
        var service = CreateService(workspace.RootPath);

        var result = service.ValidateRuntimePackV1(manifestPath);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Issues, issue => string.Equals(issue.Severity, "error", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRuntimePackV1_FailsWhenUnknownTopLevelPropertyExists()
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.WriteFile(
            "artifacts/runtime-pack-v1-invalid.json",
            """
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.firstparty.security-review",
              "packVersion": "0.1.0",
              "name": "CARVES First-Party Security Review",
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
                    "id": "security-review-rubric",
                    "description": "Bounded review rubric for security-sensitive changes.",
                    "checklistItems": [
                      {
                        "id": "security-protected-root-boundary",
                        "severity": "block",
                        "text": "Check whether the change widens protected-root mutation."
                      }
                    ]
                  }
                ]
              },
              "unknownField": true
            }
            """);
        var service = CreateService(workspace.RootPath);

        var result = service.ValidateRuntimePackV1(manifestPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "unknown_property");
    }

    [Fact]
    public void ValidateRuntimePackV1_FailsWhenCapabilityKindIsOutsideAllowlist()
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.WriteFile(
            "artifacts/runtime-pack-v1-unknown-capability.json",
            """
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.community.invalid-capability",
              "packVersion": "0.1.0",
              "name": "Invalid Capability Pack",
              "publisher": {
                "name": "community",
                "trustLevel": "community"
              },
              "license": {
                "expression": "MIT"
              },
              "compatibility": {
                "carvesRuntime": ">=0.4.0"
              },
              "capabilityKinds": [
                "tool_adapter"
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
                "reviewRubrics": []
              }
            }
            """);
        var service = CreateService(workspace.RootPath);

        var result = service.ValidateRuntimePackV1(manifestPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "runtime_pack_v1_capability_kind_invalid");
    }

    [Theory]
    [InlineData("network", "requestedPermissions.network")]
    [InlineData("env", "requestedPermissions.env")]
    [InlineData("secrets", "requestedPermissions.secrets")]
    public void ValidateRuntimePackV1_FailsWhenDeniedPermissionIsRequested(string permissionName, string expectedField)
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.WriteFile(
            $"artifacts/runtime-pack-v1-{permissionName}-requested.json",
            $$"""
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.community.denied-permission",
              "packVersion": "0.1.0",
              "name": "Denied Permission Pack",
              "publisher": {
                "name": "community",
                "trustLevel": "community"
              },
              "license": {
                "expression": "MIT"
              },
              "compatibility": {
                "carvesRuntime": ">=0.4.0"
              },
              "capabilityKinds": [
                "review_rubric"
              ],
              "requestedPermissions": {
                "readPaths": ["src/**"],
                "network": {{(permissionName == "network" ? "true" : "false")}},
                "env": {{(permissionName == "env" ? "true" : "false")}},
                "secrets": {{(permissionName == "secrets" ? "true" : "false")}},
                "truthWrite": false
              },
              "recipes": {
                "projectUnderstandingRecipes": [],
                "verificationRecipes": [],
                "reviewRubrics": [
                  {
                    "id": "security-review-rubric",
                    "description": "Bounded review rubric for security-sensitive changes.",
                    "checklistItems": [
                      {
                        "id": "security-protected-root-boundary",
                        "severity": "block",
                        "text": "Check whether the change widens protected-root mutation."
                      }
                    ]
                  }
                ]
              }
            }
            """);
        var service = CreateService(workspace.RootPath);

        var result = service.ValidateRuntimePackV1(manifestPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == $"{permissionName}_invalid" && issue.Field == expectedField);
    }

    [Fact]
    public void ValidateRuntimePackV1_FailsWhenManifestTriesToSelfCertifyHash()
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.WriteFile(
            "artifacts/runtime-pack-v1-self-certified-hash.json",
            """
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.community.self-certified-hash",
              "packVersion": "0.1.0",
              "name": "Self Certified Hash Pack",
              "publisher": {
                "name": "community",
                "trustLevel": "community"
              },
              "license": {
                "expression": "MIT"
              },
              "compatibility": {
                "carvesRuntime": ">=0.4.0"
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
              "artifactHash": "sha256:self-certified",
              "recipes": {
                "projectUnderstandingRecipes": [],
                "verificationRecipes": [],
                "reviewRubrics": [
                  {
                    "id": "security-review-rubric",
                    "description": "Bounded review rubric for security-sensitive changes.",
                    "checklistItems": [
                      {
                        "id": "security-protected-root-boundary",
                        "severity": "block",
                        "text": "Check whether the change widens protected-root mutation."
                      }
                    ]
                  }
                ]
              }
            }
            """);
        var service = CreateService(workspace.RootPath);

        var result = service.ValidateRuntimePackV1(manifestPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "unknown_property" && issue.Message.Contains("artifactHash", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRuntimePackV1_FailsWhenProjectUnderstandingRecipeIdIsDuplicated()
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.WriteFile(
            "artifacts/runtime-pack-v1-duplicate-project-understanding.json",
            """
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.firstparty.dotnet-webapi",
              "packVersion": "0.1.0",
              "name": "CARVES First-Party .NET Web API",
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
                "project_understanding_recipe"
              ],
              "requestedPermissions": {
                "readPaths": ["src/**"],
                "network": false,
                "env": false,
                "secrets": false,
                "truthWrite": false
              },
              "recipes": {
                "projectUnderstandingRecipes": [
                  {
                    "id": "dup-project-understanding",
                    "description": "First declaration.",
                    "includeGlobs": ["src/**/*.cs"]
                  },
                  {
                    "id": "dup-project-understanding",
                    "description": "Second declaration.",
                    "includeGlobs": ["tests/**/*.cs"]
                  }
                ],
                "verificationRecipes": [],
                "reviewRubrics": []
              }
            }
            """);
        var service = CreateService(workspace.RootPath);

        var result = service.ValidateRuntimePackV1(manifestPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "project_understanding_recipe_id_duplicate");
    }

    [Fact]
    public void ValidateRuntimePackV1_FailsWhenVerificationCommandStableIdIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        var manifestPath = workspace.WriteFile(
            "artifacts/runtime-pack-v1-missing-command-id.json",
            """
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.firstparty.dotnet-webapi",
              "packVersion": "0.1.0",
              "name": "CARVES First-Party .NET Web API",
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
                    "id": "dotnet-build-and-test",
                    "description": "Bounded build and test commands for a .NET Web API repository.",
                    "commands": [
                      {
                        "kind": "known_tool_command",
                        "executable": "dotnet",
                        "args": ["build"],
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
        var service = CreateService(workspace.RootPath);

        var result = service.ValidateRuntimePackV1(manifestPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "id_missing" && issue.Field == "recipes.verificationRecipes[0].commands[0].id");
    }

    [Fact]
    public void ValidateRuntimePackAttribution_PassesForCanonicalAttribution()
    {
        using var workspace = new TemporaryWorkspace();
        var attributionPath = workspace.WriteFile(
            "artifacts/runtime-pack-attribution.json",
            """
            {
              "schemaVersion": "1.0",
              "packId": "carves.runtime.core",
              "packVersion": "1.2.3",
              "channel": "stable",
              "artifactRef": ".ai/artifacts/packs/core-1.2.3.json",
              "executionProfiles": {
                "policyPreset": "core-default",
                "gatePreset": "strict",
                "validatorProfile": "default-validator",
                "environmentProfile": "workspace",
                "routingProfile": "connected-lanes"
              },
              "source": {
                "assignmentMode": "overlay_assignment",
                "assignmentRef": "overlay-assignment-001"
              },
              "attributedAtUtc": "2026-03-31T00:00:00+00:00"
            }
            """);
        var service = CreateService(workspace.RootPath);

        var result = service.ValidateRuntimePackAttribution(attributionPath);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Issues, issue => string.Equals(issue.Severity, "error", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRuntimePackAttribution_FailsWhenAssignmentModeIsUnknown()
    {
        using var workspace = new TemporaryWorkspace();
        var attributionPath = workspace.WriteFile(
            "artifacts/runtime-pack-attribution-invalid.json",
            """
            {
              "schemaVersion": "1.0",
              "packId": "carves.runtime.core",
              "packVersion": "1.2.3",
              "channel": "stable",
              "executionProfiles": {
                "policyPreset": "core-default",
                "gatePreset": "strict",
                "validatorProfile": "default-validator",
                "environmentProfile": "workspace"
              },
              "source": {
                "assignmentMode": "unknown_mode"
              },
              "attributedAtUtc": "2026-03-31T00:00:00+00:00"
            }
            """);
        var service = CreateService(workspace.RootPath);

        var result = service.ValidateRuntimePackAttribution(attributionPath);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "runtime_pack_attribution_assignment_mode_invalid");
    }

    private static SpecificationValidationService CreateService(string rootPath)
    {
        var paths = ControlPlanePaths.FromRepoRoot(rootPath);
        var configRepository = new FileControlPlaneConfigRepository(paths);
        return new SpecificationValidationService(rootPath, paths, configRepository);
    }
}
