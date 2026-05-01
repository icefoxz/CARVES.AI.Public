using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Host;
using Carves.Runtime.Infrastructure.CodeGraph;
using Carves.Runtime.Infrastructure.Memory;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

public sealed partial class HostContractTests
{
    [Fact]
    public void StatusAndScanCode_SucceedAgainstSandbox()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var statusExitCode = Program.Main(["--repo-root", sandbox.RootPath, "status"]);
        var scanExitCode = Program.Main(["--repo-root", sandbox.RootPath, "scan-code"]);
        var auditExitCode = Program.Main(["--repo-root", sandbox.RootPath, "audit", "codegraph", "--strict"]);
        var indexJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "codegraph", "index.json"));

        Assert.Equal(0, statusExitCode);
        Assert.Equal(0, scanExitCode);
        Assert.Equal(0, auditExitCode);
        Assert.Contains("\"modules\"", indexJson, StringComparison.Ordinal);
        Assert.Contains("\"callables\"", indexJson, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, ".ai", "codegraph", "audit.json")));
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, ".ai", "codegraph", "manifest.json")));
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, ".ai", "codegraph", "search", "index.json")));
        Assert.True(Directory.Exists(Path.Combine(sandbox.RootPath, ".ai", "codegraph", "modules")));

        var status = RunProgram("--repo-root", sandbox.RootPath, "status");
        var safety = RunProgram("--repo-root", sandbox.RootPath, "safety-check");

        Assert.Contains("CARVES standard: v0.4", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("CARVES naming:", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("CARVES dependency:", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("CARVES code standard:", safety.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("dependency one-way:", safety.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void TaskGraphLoad_CoercesLegacyNonStringMetadataValues()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-METADATA-COERCE");
        var taskPath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-METADATA-COERCE.json");
        var taskNode = JsonNode.Parse(File.ReadAllText(taskPath))!.AsObject();
        taskNode["metadata"] = new JsonObject
        {
            ["phase_order"] = 2,
            ["ready"] = true,
            ["note"] = "kept",
            ["ignored_null"] = null,
        };
        File.WriteAllText(taskPath, taskNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var repository = new JsonTaskGraphRepository(ControlPlanePaths.FromRepoRoot(sandbox.RootPath));
        var task = repository.Load().Tasks["T-INTEGRATION-METADATA-COERCE"];

        Assert.Equal("2", task.Metadata["phase_order"]);
        Assert.Equal("true", task.Metadata["ready"]);
        Assert.Equal("kept", task.Metadata["note"]);
        Assert.False(task.Metadata.ContainsKey("ignored_null"));
    }

    [Fact]
    public void ValidateTaskAndSafetyCommands_RespectTaskTruthAndWriteBoundaries()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-VALIDATE");

        var taskPath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-VALIDATE.json");
        var validateTask = RunProgram("--repo-root", sandbox.RootPath, "validate", "task", taskPath);
        var allowWrite = RunProgram("--repo-root", sandbox.RootPath, "validate", "safety", "--actor", "worker", "--op", "write", "--path", "src/CARVES.Runtime.Host/Program.cs");
        var denyWrite = RunProgram("--repo-root", sandbox.RootPath, "validate", "safety", "--actor", "worker", "--op", "write", "--path", ".ai/tasks/graph.json");

        Assert.Equal(0, validateTask.ExitCode);
        Assert.Equal(0, allowWrite.ExitCode);
        Assert.Equal(1, denyWrite.ExitCode);
        Assert.Contains("Validation passed.", validateTask.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Validator: task", validateTask.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Validation failed.", denyWrite.StandardError, StringComparison.Ordinal);
        Assert.Contains("managed_control_plane_write_forbidden", denyWrite.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidatePackContracts_ExposeRuntimePackArtifactAndAttributionChecks()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var packArtifactPath = Path.Combine(payloadRoot, "pack-artifact.json");
        File.WriteAllText(
            packArtifactPath,
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

        var attributionPath = Path.Combine(payloadRoot, "runtime-pack-attribution.json");
        File.WriteAllText(
            attributionPath,
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

        var invalidAttributionPath = Path.Combine(payloadRoot, "runtime-pack-attribution-invalid.json");
        File.WriteAllText(
            invalidAttributionPath,
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

        var validateArtifact = RunProgram("--repo-root", sandbox.RootPath, "--cold", "validate", "pack-artifact", packArtifactPath);
        var validateAttribution = RunProgram("--repo-root", sandbox.RootPath, "--cold", "validate", "runtime-pack-attribution", attributionPath);
        var validateInvalidAttribution = RunProgram("--repo-root", sandbox.RootPath, "--cold", "validate", "runtime-pack-attribution", invalidAttributionPath);
        var inspectContracts = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "execution-contract-surface");

        Assert.Equal(0, validateArtifact.ExitCode);
        Assert.Equal(0, validateAttribution.ExitCode);
        Assert.Equal(1, validateInvalidAttribution.ExitCode);
        Assert.Equal(0, inspectContracts.ExitCode);
        Assert.Contains("Validator: pack_artifact", validateArtifact.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Validator: runtime_pack_attribution", validateAttribution.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime_pack_attribution_assignment_mode_invalid", validateInvalidAttribution.StandardError, StringComparison.Ordinal);
        Assert.Contains("pack_artifact", inspectContracts.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime_pack_attribution", inspectContracts.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimePackV1_UsesPackV1ManifestValidatorAndReferencePack()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var referencePackPath = Path.Combine(sandbox.RootPath, "docs", "product", "reference-packs", "runtime-pack-v1-dotnet-webapi.json");
        var invalidManifestPath = Path.Combine(payloadRoot, "runtime-pack-v1-invalid.json");
        File.WriteAllText(
            invalidManifestPath,
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
                "truthWrite": true
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

        var validateReference = RunProgram("--repo-root", sandbox.RootPath, "--cold", "validate", "runtime-pack-v1", referencePackPath);
        var validateInvalid = RunProgram("--repo-root", sandbox.RootPath, "--cold", "validate", "runtime-pack-v1", invalidManifestPath);
        var inspectContracts = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "execution-contract-surface");

        Assert.Equal(0, validateReference.ExitCode);
        Assert.Equal(1, validateInvalid.ExitCode);
        Assert.Equal(0, inspectContracts.ExitCode);
        Assert.Contains("Validator: runtime_pack_v1", validateReference.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("truthWrite_invalid", validateInvalid.StandardError, StringComparison.Ordinal);
        Assert.Contains("runtime_pack_v1_manifest", inspectContracts.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRuntimePackPolicyPackage_ExplicitlyPassesAndFails()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var policyPackagePath = Path.Combine(payloadRoot, "runtime-pack-policy.json");
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "export-pack-policy", policyPackagePath).ExitCode);

        var invalidPolicyPackagePath = Path.Combine(payloadRoot, "runtime-pack-policy-invalid.json");
        var packageNode = JsonNode.Parse(File.ReadAllText(policyPackagePath))!.AsObject();
        var invalidAdmissionPolicy = packageNode["admission_policy"]!.AsObject();
        invalidAdmissionPolicy["allowed_channels"] = new JsonArray();
        invalidAdmissionPolicy["policy_id"] = string.Empty;
        File.WriteAllText(invalidPolicyPackagePath, packageNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var validateValid = RunProgram("--repo-root", sandbox.RootPath, "--cold", "validate", "runtime-pack-policy-package", policyPackagePath);
        var validateInvalid = RunProgram("--repo-root", sandbox.RootPath, "--cold", "validate", "runtime-pack-policy-package", invalidPolicyPackagePath);
        var inspectContracts = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "execution-contract-surface");

        Assert.Equal(0, validateValid.ExitCode);
        Assert.Equal(1, validateInvalid.ExitCode);
        Assert.Equal(0, inspectContracts.ExitCode);
        Assert.Contains("Validator: runtime_pack_policy_package", validateValid.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime_pack_policy_package_allowed_channels_missing", validateInvalid.StandardError, StringComparison.Ordinal);
        Assert.Contains("runtime_pack_policy_package", inspectContracts.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePackAdmission_AdmitsMatchingPairAndProjectsCurrentEvidence()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var packArtifactPath = Path.Combine(payloadRoot, "pack-artifact.json");
        File.WriteAllText(
            packArtifactPath,
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

        var attributionPath = Path.Combine(payloadRoot, "runtime-pack-attribution.json");
        File.WriteAllText(
            attributionPath,
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

        var admit = RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "admit-pack", packArtifactPath, "--attribution", attributionPath);
        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-admission");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-admission");

        Assert.Equal(0, admit.ExitCode);
        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime pack admission", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves.runtime.core@1.2.3", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("policy=core-default", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-pack-admission", root.GetProperty("surface_id").GetString());
        var admission = root.GetProperty("current_admission");
        Assert.Equal("carves.runtime.core", admission.GetProperty("pack_id").GetString());
        Assert.Equal("1.2.3", admission.GetProperty("pack_version").GetString());
        Assert.Equal("overlay_assignment", admission.GetProperty("source").GetProperty("assignment_mode").GetString());
    }

    [Fact]
    public void RuntimePackSelection_AssignsCurrentPackFromAdmittedEvidence()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var packArtifactPath = Path.Combine(payloadRoot, "pack-artifact.json");
        File.WriteAllText(
            packArtifactPath,
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

        var attributionPath = Path.Combine(payloadRoot, "runtime-pack-attribution.json");
        File.WriteAllText(
            attributionPath,
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

        var admit = RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "admit-pack", packArtifactPath, "--attribution", attributionPath);
        var assign = RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "assign-pack", "carves.runtime.core", "--pack-version", "1.2.3", "--channel", "stable", "--reason", "select current pack");
        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-selection");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-selection");

        Assert.Equal(0, admit.ExitCode);
        Assert.Equal(0, assign.ExitCode);
        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime pack selection", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Current selection: carves.runtime.core@1.2.3 (stable)", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Selection mode: manual_local_assignment", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Audit trail:", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-pack-selection", root.GetProperty("surface_id").GetString());
        var selection = root.GetProperty("current_selection");
        Assert.Equal("carves.runtime.core", selection.GetProperty("pack_id").GetString());
        Assert.StartsWith("packsel-", selection.GetProperty("selection_id").GetString(), StringComparison.Ordinal);
        Assert.Equal("manual_local_assignment", selection.GetProperty("selection_mode").GetString());
        Assert.Single(root.GetProperty("history").EnumerateArray());
        Assert.Single(root.GetProperty("audit_trail").EnumerateArray());
    }

    [Fact]
    public void RuntimePackSelection_RollbackRestoresHistoricalSelectionWhenAdmissionMatches()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var packOneArtifactPath = Path.Combine(payloadRoot, "pack-one-artifact.json");
        File.WriteAllText(
            packOneArtifactPath,
            """
            {
              "schemaVersion": "1.0",
              "packId": "carves.runtime.core",
              "packVersion": "1.2.3",
              "packType": "runtime_pack",
              "channel": "stable",
              "runtimeCompatibility": { "minVersion": "0.4.0", "maxVersion": "0.4.x" },
              "kernelCompatibility": { "minVersion": "0.1.0", "maxVersion": null },
              "executionProfiles": {
                "policyPreset": "core-default",
                "gatePreset": "strict",
                "validatorProfile": "default-validator",
                "environmentProfile": "workspace",
                "routingProfile": "connected-lanes",
                "providerAllowlist": ["codex", "openai"]
              },
              "operatorChecklistRefs": ["docs/checklists/core-release.md"],
              "signature": { "scheme": "sha256-rsa", "keyId": "core-signing-key", "digest": "sha256:abc123" },
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

        var packOneAttributionPath = Path.Combine(payloadRoot, "pack-one-runtime-pack-attribution.json");
        File.WriteAllText(
            packOneAttributionPath,
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
              "source": { "assignmentMode": "overlay_assignment", "assignmentRef": "overlay-assignment-001" },
              "attributedAtUtc": "2026-03-31T00:00:00+00:00"
            }
            """);

        var packTwoArtifactPath = Path.Combine(payloadRoot, "pack-two-artifact.json");
        File.WriteAllText(
            packTwoArtifactPath,
            """
            {
              "schemaVersion": "1.0",
              "packId": "carves.runtime.core",
              "packVersion": "1.2.4",
              "packType": "runtime_pack",
              "channel": "stable",
              "runtimeCompatibility": { "minVersion": "0.4.0", "maxVersion": "0.4.x" },
              "kernelCompatibility": { "minVersion": "0.1.0", "maxVersion": null },
              "executionProfiles": {
                "policyPreset": "core-default",
                "gatePreset": "strict",
                "validatorProfile": "default-validator",
                "environmentProfile": "workspace",
                "routingProfile": "connected-lanes",
                "providerAllowlist": ["codex", "openai"]
              },
              "operatorChecklistRefs": ["docs/checklists/core-release.md"],
              "signature": { "scheme": "sha256-rsa", "keyId": "core-signing-key", "digest": "sha256:def456" },
              "provenance": {
                "publishedAtUtc": "2026-03-31T00:00:00+00:00",
                "publishedBy": "operator@carves",
                "sourcePackLine": "core-stable",
                "sourceGenerationId": "gen-002"
              },
              "releaseNoteRef": "docs/releases/core-1.2.4.md",
              "parentPackVersion": "1.2.3",
              "approvalRef": "APP-002",
              "supersedes": ["1.2.3"]
            }
            """);

        var packTwoAttributionPath = Path.Combine(payloadRoot, "pack-two-runtime-pack-attribution.json");
        File.WriteAllText(
            packTwoAttributionPath,
            """
            {
              "schemaVersion": "1.0",
              "packId": "carves.runtime.core",
              "packVersion": "1.2.4",
              "channel": "stable",
              "artifactRef": ".ai/artifacts/packs/core-1.2.4.json",
              "executionProfiles": {
                "policyPreset": "core-default",
                "gatePreset": "strict",
                "validatorProfile": "default-validator",
                "environmentProfile": "workspace",
                "routingProfile": "connected-lanes"
              },
              "source": { "assignmentMode": "overlay_assignment", "assignmentRef": "overlay-assignment-002" },
              "attributedAtUtc": "2026-03-31T00:00:00+00:00"
            }
            """);

        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "admit-pack", packOneArtifactPath, "--attribution", packOneAttributionPath).ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "assign-pack", "carves.runtime.core", "--pack-version", "1.2.3", "--channel", "stable", "--reason", "select pack one").ExitCode);
        var firstApi = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-selection");
        using var firstDocument = JsonDocument.Parse(firstApi.StandardOutput);
        var firstSelectionId = firstDocument.RootElement.GetProperty("current_selection").GetProperty("selection_id").GetString();

        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "admit-pack", packTwoArtifactPath, "--attribution", packTwoAttributionPath).ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "assign-pack", "carves.runtime.core", "--pack-version", "1.2.4", "--channel", "stable", "--reason", "select pack two").ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "admit-pack", packOneArtifactPath, "--attribution", packOneAttributionPath).ExitCode);

        var rollback = RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "rollback-pack", firstSelectionId!, "--reason", "rollback to pack one");
        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-selection");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-selection");

        Assert.Equal(0, rollback.ExitCode);
        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("manual_local_rollback", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Selection history:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Rollback targets:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Audit trail:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("selection_rolled_back", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        var selection = root.GetProperty("current_selection");
        var auditTrail = root.GetProperty("audit_trail").EnumerateArray().ToArray();
        Assert.Equal("1.2.3", selection.GetProperty("pack_version").GetString());
        Assert.Equal("manual_local_rollback", selection.GetProperty("selection_mode").GetString());
        Assert.Equal(firstSelectionId, selection.GetProperty("rollback_target_selection_id").GetString());
        Assert.True(root.GetProperty("history").GetArrayLength() >= 3);
        Assert.True(auditTrail.Length >= 3);
        Assert.Equal("selection_rolled_back", auditTrail[0].GetProperty("event_kind").GetString());
        Assert.Equal(firstSelectionId, auditTrail[0].GetProperty("rollback_target_selection_id").GetString());
    }

    [Fact]
    public void RuntimePackDistributionBoundary_ProjectsLocalAndClosedFutureCapabilities()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-distribution-boundary");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-distribution-boundary");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime pack distribution boundary", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Local capabilities:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Closed future capabilities:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime_local_pack_selection", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pack_registry_sync", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-pack-distribution-boundary", root.GetProperty("surface_id").GetString());
        Assert.Contains(
            root.GetProperty("local_capabilities").EnumerateArray().Select(item => item.GetProperty("capability_id").GetString()),
            capabilityId => string.Equals(capabilityId, "runtime_local_pack_selection", StringComparison.Ordinal));
        Assert.Contains(
            root.GetProperty("closed_future_capabilities").EnumerateArray().Select(item => item.GetProperty("capability_id").GetString()),
            capabilityId => string.Equals(capabilityId, "pack_registry_sync", StringComparison.Ordinal));
    }

    [Fact]
    public void InspectRun_ProjectsSelectedPackAttributionWhenCurrentSelectionExists()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-PACK-RUN");

        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var packArtifactPath = Path.Combine(payloadRoot, "pack-artifact.json");
        File.WriteAllText(
            packArtifactPath,
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

        var attributionPath = Path.Combine(payloadRoot, "runtime-pack-attribution.json");
        File.WriteAllText(
            attributionPath,
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

        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "admit-pack", packArtifactPath, "--attribution", attributionPath).ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "assign-pack", "carves.runtime.core", "--pack-version", "1.2.3", "--channel", "stable", "--reason", "select current pack").ExitCode);

        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var artifactRepository = new JsonRuntimeArtifactRepository(paths);
        var taskGraphService = new TaskGraphService(new JsonTaskGraphRepository(paths), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var task = taskGraphService.GetTask("T-INTEGRATION-PACK-RUN");
        var run = new ExecutionRunService(paths, artifactRepository).PrepareRunForDispatch(task);

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "run", run.RunId);

        Assert.Equal(0, inspect.ExitCode);
        using var document = JsonDocument.Parse(inspect.StandardOutput);
        var root = document.RootElement;
        var selectedPack = root.GetProperty("selected_pack");
        Assert.Equal("carves.runtime.core", selectedPack.GetProperty("pack_id").GetString());
        Assert.Equal("1.2.3", selectedPack.GetProperty("pack_version").GetString());
        Assert.Equal("connected-lanes", selectedPack.GetProperty("routing_profile").GetString());
    }

    [Fact]
    public void RuntimePackExecutionAudit_ProjectsCurrentSelectionCoverageAndRecentEvidence()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-PACK-AUDIT");

        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var packArtifactPath = Path.Combine(payloadRoot, "pack-artifact.json");
        File.WriteAllText(
            packArtifactPath,
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

        var attributionPath = Path.Combine(payloadRoot, "runtime-pack-attribution.json");
        File.WriteAllText(
            attributionPath,
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

        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "admit-pack", packArtifactPath, "--attribution", attributionPath).ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "assign-pack", "carves.runtime.core", "--pack-version", "1.2.3", "--channel", "stable", "--reason", "select current pack").ExitCode);

        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var artifactRepository = new JsonRuntimeArtifactRepository(paths);
        var taskGraphService = new TaskGraphService(new JsonTaskGraphRepository(paths), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var task = taskGraphService.GetTask("T-INTEGRATION-PACK-AUDIT");
        var runService = new ExecutionRunService(paths, artifactRepository);
        var run = runService.PrepareRunForDispatch(task) with
        {
            Status = ExecutionRunStatus.Completed,
            EndedAtUtc = DateTimeOffset.UtcNow,
        };
        File.WriteAllText(
            runService.GetRunPath(run.TaskId, run.RunId),
            JsonSerializer.Serialize(
                run,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }));
        new ExecutionRunReportService(paths).Persist(
            run,
            taskRunReport: new TaskRunReport
            {
                TaskId = run.TaskId,
                WorkerExecution = new WorkerExecutionResult
                {
                    TaskId = run.TaskId,
                    ChangedFiles =
                    [
                        "src/CARVES.Runtime.Application/Platform/RuntimePackExecutionAuditService.cs"
                    ],
                },
            });

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-execution-audit");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-execution-audit");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime pack execution audit", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Current selection: carves.runtime.core@1.2.3 (stable)", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Recent attributed runs:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Recent attributed reports:", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-pack-execution-audit", root.GetProperty("surface_id").GetString());
        Assert.Equal("carves.runtime.core", root.GetProperty("current_selection").GetProperty("pack_id").GetString());
        Assert.Equal(1, root.GetProperty("coverage").GetProperty("current_selection_run_count").GetInt32());
        Assert.Equal(1, root.GetProperty("coverage").GetProperty("current_selection_report_count").GetInt32());
        Assert.Single(root.GetProperty("recent_runs").EnumerateArray());
        Assert.Single(root.GetProperty("recent_reports").EnumerateArray());
    }

    [Fact]
    public void InspectAndApiCodexToolSurface_ExposeMinimalRegistry()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "codex-tool-surface");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "codex-tool-surface");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Codex tool surface", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Planner-only actions:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Worker-allowed actions:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("get_execution_packet", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("review_task", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("codex-tool-surface", root.GetProperty("surface_id").GetString());
        var tools = root.GetProperty("tools");
        Assert.True(tools.GetArrayLength() >= 8);
        Assert.Contains(tools.EnumerateArray(), tool =>
            tool.GetProperty("tool_id").GetString() == "get_task"
            && tool.GetProperty("action_class").GetString() == "worker_allowed");
        Assert.Contains(tools.EnumerateArray(), tool =>
            tool.GetProperty("tool_id").GetString() == "review_task"
            && tool.GetProperty("action_class").GetString() == "planner_only");
    }

    [Fact]
    public void InspectAndApiExecutionContractSurface_ExposeContractsAndVerdicts()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "execution-contract-surface");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "execution-contract-surface");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Execution contract surface", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("execution_packet", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("task_result_envelope", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("quarantined", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("execution-contract-surface", root.GetProperty("surface_id").GetString());
        var contracts = root.GetProperty("contracts");
        Assert.Contains(contracts.EnumerateArray(), contract =>
            contract.GetProperty("contract_id").GetString() == "execution_packet"
            && contract.GetProperty("schema_path").GetString() == "docs/contracts/execution-packet.schema.json");
        var verdicts = root.GetProperty("planner_verdicts");
        Assert.Contains(verdicts.EnumerateArray(), verdict =>
            verdict.GetProperty("contract_id").GetString() == "quarantined"
            && verdict.GetProperty("indicates_quarantine").GetBoolean());
    }

    [Fact]
    public void InspectAndApiExecutionPacket_ExposeCompiledPacketAndPlannerIntent()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask(
            "T-INTEGRATION-PACKET",
            scope: ["src/CARVES.Runtime.Host/Program.cs"]);

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "execution-packet", "T-INTEGRATION-PACKET");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "execution-packet", "T-INTEGRATION-PACKET");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Execution packet", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Planner intent: Execution", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Context order: Architecture -> RelevantModules -> CurrentTaskFiles", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Context compaction:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("predicted_patch_exceeds_budget", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("requires_split_to_stay_within_patch_budget", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("execution-packet", root.GetProperty("surface_id").GetString());
        Assert.Equal("T-INTEGRATION-PACKET", root.GetProperty("task_id").GetString());
        Assert.Equal("execution", root.GetProperty("planner_intent").GetString());
        var packet = root.GetProperty("packet");
        Assert.Equal("execution", packet.GetProperty("planner_intent").GetString());
        Assert.Contains(packet.GetProperty("context").GetProperty("assembly_order").EnumerateArray(), item => item.GetString() == "Architecture");
        Assert.Equal(".ai/runtime/context-packs/tasks/T-INTEGRATION-PACKET.json", packet.GetProperty("context").GetProperty("context_pack_ref").GetString());
        Assert.True(packet.GetProperty("context").GetProperty("compaction").GetProperty("candidate_file_count").GetInt32() >= 1);
        Assert.Contains(packet.GetProperty("stop_conditions").EnumerateArray(), item => item.GetString() == "predicted_patch_exceeds_budget");
        Assert.Contains(packet.GetProperty("stop_conditions").EnumerateArray(), item => item.GetString() == "requires_split_to_stay_within_patch_budget");
    }

    [Fact]
    public void InspectAndApiPacketEnforcement_ExposeVerdictAndPlannerOnlyActionAttempt()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask(
            "T-INTEGRATION-PACKET-ENFORCEMENT",
            scope: ["src/CARVES.Runtime.Host/Program.cs"]);

        var taskId = "T-INTEGRATION-PACKET-ENFORCEMENT";
        var runId = $"RUN-{taskId}-001";
        var packet = new ExecutionPacket
        {
            PacketId = $"EP-{taskId}-v1",
            TaskRef = new ExecutionPacketTaskRef
            {
                CardId = "CARD-INTEGRATION",
                TaskId = taskId,
                TaskRevision = 1,
            },
            Goal = "Synthetic packet enforcement fixture.",
            PlannerIntent = Carves.Runtime.Domain.Planning.PlannerIntent.Execution,
            Permissions = new ExecutionPacketPermissions
            {
                EditableRoots = ["src/"],
                ReadOnlyRoots = ["docs/"],
                TruthRoots = ["carves://truth/tasks", "carves://truth/runtime"],
                RepoMirrorRoots = [".ai/"],
            },
            WorkerAllowedActions = ["read", "edit", "build", "test", "carves.submit_result", "carves.request_replan"],
            PlannerOnlyActions = ["carves.review_task", "carves.sync_state"],
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        var packetPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "execution-packets", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(packetPath)!);
        File.WriteAllText(packetPath, JsonSerializer.Serialize(packet, jsonOptions));

        var resultPath = Path.Combine(sandbox.RootPath, ".ai", "execution", taskId, "result.json");
        Directory.CreateDirectory(Path.GetDirectoryName(resultPath)!);
        File.WriteAllText(
            resultPath,
            JsonSerializer.Serialize(
                new ResultEnvelope
                {
                    TaskId = taskId,
                    ExecutionRunId = runId,
                    ExecutionEvidencePath = $".ai/artifacts/worker-executions/{runId}/evidence.json",
                    Status = "success",
                    Changes = new ResultEnvelopeChanges
                    {
                        FilesModified = ["src/CARVES.Runtime.Host/Program.cs"],
                    },
                    Validation = new ResultEnvelopeValidation
                    {
                        Build = "success",
                        Tests = "success",
                    },
                    Next = new ResultEnvelopeNextAction
                    {
                        Suggested = "Run review-task and sync-state after submit_result.",
                    },
                },
                jsonOptions));

        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var artifactRepository = new JsonRuntimeArtifactRepository(paths);
        var evidenceRoot = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker-executions", runId);
        var evidencePath = Path.Combine(evidenceRoot, "evidence.json");
        var commandLogPath = Path.Combine(evidenceRoot, "command.log");
        var buildLogPath = Path.Combine(evidenceRoot, "build.log");
        var testLogPath = Path.Combine(evidenceRoot, "test.log");
        var patchPath = Path.Combine(evidenceRoot, "patch.diff");
        Directory.CreateDirectory(evidenceRoot);
        File.WriteAllText(evidencePath, "{\"taskId\":\"" + taskId + "\"}");
        File.WriteAllText(commandLogPath, "dotnet build\r\ndotnet test");
        File.WriteAllText(buildLogPath, "Build succeeded.");
        File.WriteAllText(testLogPath, "Test run completed.");
        File.WriteAllText(patchPath, "diff --git a/src/CARVES.Runtime.Host/Program.cs b/src/CARVES.Runtime.Host/Program.cs\n--- a/src/CARVES.Runtime.Host/Program.cs\n+++ b/src/CARVES.Runtime.Host/Program.cs\n@@\n+ synthetic change");
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = runId,
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = runId,
                WorkerId = "CodexCliWorkerAdapter",
                EvidenceSource = ExecutionEvidenceSource.Host,
                CommandsExecuted = ["dotnet build", "dotnet test"],
                FilesWritten = ["src/CARVES.Runtime.Host/Program.cs"],
                EvidencePath = ".ai/artifacts/worker-executions/" + runId + "/evidence.json",
                CommandLogRef = ".ai/artifacts/worker-executions/" + runId + "/command.log",
                BuildOutputRef = ".ai/artifacts/worker-executions/" + runId + "/build.log",
                TestOutputRef = ".ai/artifacts/worker-executions/" + runId + "/test.log",
                PatchRef = ".ai/artifacts/worker-executions/" + runId + "/patch.diff",
                EvidenceCompleteness = ExecutionEvidenceCompleteness.Complete,
                EvidenceStrength = ExecutionEvidenceStrength.Replayable,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                EndedAt = DateTimeOffset.UtcNow,
                ExitStatus = 0,
            },
        });
        artifactRepository.SaveSafetyArtifact(new SafetyArtifact
        {
            Decision = SafetyDecision.Allow(taskId),
        });

        var ingest = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "ingest-result", taskId);
        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "packet-enforcement", taskId);
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "packet-enforcement", taskId);
        var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", $"{taskId}.json"));

        Assert.Equal(0, ingest.ExitCode);
        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("\"execution_packet_enforcement_verdict\": \"reject\"", taskNodeJson, StringComparison.Ordinal);
        Assert.Contains("Packet enforcement", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Verdict: reject", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Planner-only action attempted: True", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Result-envelope changed files: src/CARVES.Runtime.Host/Program.cs", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Evidence changed files: src/CARVES.Runtime.Host/Program.cs", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("packet-enforcement", root.GetProperty("surface_id").GetString());
        var record = root.GetProperty("record");
        Assert.Equal("reject", record.GetProperty("verdict").GetString());
        Assert.True(record.GetProperty("planner_only_action_attempted").GetBoolean());
        Assert.Contains(
            record.GetProperty("result_envelope_changed_files").EnumerateArray(),
            item => item.GetString() == "src/CARVES.Runtime.Host/Program.cs");
        Assert.Contains(
            record.GetProperty("evidence_changed_files").EnumerateArray(),
            item => item.GetString() == "src/CARVES.Runtime.Host/Program.cs");
    }

    [Fact]
    public void InspectAndApiAuthoritativeTruthStore_ExposeExternalRootAndMirrorFamilies()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-AUTHORITY", scope: ["src/CARVES.Runtime.Host/Program.cs"]);

        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var truthStore = new AuthoritativeTruthStoreService(paths);
        var repository = new JsonTaskGraphRepository(paths);
        repository.Save(repository.Load());
        var writerLeasePath = ControlPlaneLockLeasePaths.GetLeasePath(sandbox.RootPath, AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope);
        Directory.CreateDirectory(Path.GetDirectoryName(writerLeasePath)!);
        var now = DateTimeOffset.UtcNow;
        File.WriteAllText(
            writerLeasePath,
            $$"""
            {
              "scope": "{{AuthoritativeTruthStoreService.AuthoritativeTruthWriterScope}}",
              "resource": "{{truthStore.TaskGraphFile.Replace('\\', '/')}}",
              "operation": "sync-state",
              "mode": "write",
              "owner_id": "host:test",
              "owner_process_id": {{Environment.ProcessId}},
              "owner_process_name": "dotnet",
              "acquired_at": "{{now:O}}",
              "last_heartbeat": "{{now:O}}",
              "ttl_seconds": 120
            }
            """);

        var manifestService = new RuntimeManifestService(paths);
        manifestService.Save(new Carves.Runtime.Domain.Platform.RepoRuntimeManifest
        {
            RepoId = "repo-authority",
            RepoPath = sandbox.RootPath,
            GitRoot = sandbox.RootPath,
            ActiveBranch = "main",
            RuntimeVersion = "v-test",
            ClientVersion = "cli-test",
            HostSessionId = "host-test",
            RuntimeStatus = "healthy",
            RepoSummary = "authority test",
            State = Carves.Runtime.Domain.Platform.RepoRuntimeManifestState.Healthy,
        });

        var task = repository.Load().Tasks["T-INTEGRATION-AUTHORITY"];
        var packetService = new ExecutionPacketCompilerService(
            paths,
            new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler()),
            new FileCodeGraphQueryService(
                paths,
                new FileCodeGraphBuilder(
                    sandbox.RootPath,
                    paths,
                    new Carves.Runtime.Application.Configuration.SystemConfig(
                        "TestRepo",
                        "../.carves-worktrees/TestRepo",
                        1,
                        ["dotnet", "test", "CARVES.Runtime.sln"],
                        ["src"],
                        [".git", ".nuget", "bin", "obj", "TestResults", "coverage", ".ai/worktrees"],
                        true,
                        true))),
            new MemoryService(new FileMemoryRepository(paths), new ExecutionContextBuilder()),
            new PlannerIntentRoutingService());
        packetService.CompileAndPersist(task);

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "authoritative-truth-store");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "authoritative-truth-store");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Authoritative truth store", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("External to repo: True", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Writer lock: active", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Mirror sync outcome: in_sync", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("task_graph", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime_manifest", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("authoritative-truth-store", root.GetProperty("surface_id").GetString());
        Assert.Equal("active", root.GetProperty("writer_lock").GetProperty("state").GetString());
        Assert.Equal("sync-state", root.GetProperty("writer_lock").GetProperty("operation").GetString());
        var authoritativeRoot = root.GetProperty("authoritative_root").GetString();
        Assert.False(string.IsNullOrWhiteSpace(authoritativeRoot));
        Assert.DoesNotContain(sandbox.RootPath.Replace('\\', '/'), authoritativeRoot!, StringComparison.OrdinalIgnoreCase);
        var families = root.GetProperty("families");
        Assert.Contains(families.EnumerateArray(), family =>
            family.GetProperty("family_id").GetString() == "task_graph"
            && family.GetProperty("authoritative_exists").GetBoolean()
            && family.GetProperty("mirror_sync").GetProperty("outcome").GetString() == "in_sync");
        Assert.Contains(families.EnumerateArray(), family =>
            family.GetProperty("family_id").GetString() == "runtime_manifest"
            && family.GetProperty("authoritative_exists").GetBoolean()
            && family.GetProperty("mirror_sync").GetProperty("outcome").GetString() == "in_sync");
        Assert.Contains(families.EnumerateArray(), family =>
            family.GetProperty("family_id").GetString() == "execution_packets"
            && family.GetProperty("authoritative_exists").GetBoolean()
            && family.GetProperty("mirror_sync").GetProperty("outcome").GetString() == "in_sync");
        Assert.True(File.Exists(truthStore.TaskGraphFile));
        Assert.True(File.Exists(truthStore.RuntimeManifestFile));
        Assert.True(File.Exists(truthStore.GetExecutionPacketPath("T-INTEGRATION-AUTHORITY")));
    }

    [Fact]
    public void InspectAndApiExecutionHardening_ExposeGovernancePacketEnforcementAndAuthoritativeTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-HARDENING", scope: ["src/CARVES.Runtime.Host/Program.cs"]);

        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var truthStore = new AuthoritativeTruthStoreService(paths);
        try
        {
            var repository = new JsonTaskGraphRepository(paths);
            var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
            var packetCompiler = new ExecutionPacketCompilerService(
                paths,
                taskGraphService,
                new FileCodeGraphQueryService(
                    paths,
                    new FileCodeGraphBuilder(
                        sandbox.RootPath,
                        paths,
                        new Carves.Runtime.Application.Configuration.SystemConfig(
                            "TestRepo",
                            "../.carves-worktrees/TestRepo",
                            1,
                            ["dotnet", "test", "CARVES.Runtime.sln"],
                            ["src"],
                            [".git", ".nuget", "bin", "obj", "TestResults", "coverage", ".ai/worktrees"],
                            true,
                            true))),
                new MemoryService(new FileMemoryRepository(paths), new ExecutionContextBuilder()),
                new PlannerIntentRoutingService());
            var task = repository.Load().Tasks["T-INTEGRATION-HARDENING"];
            packetCompiler.CompileAndPersist(task);

            var packetMirrorPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "execution-packets", "T-INTEGRATION-HARDENING.json");
            var driftedPacket = JsonNode.Parse(File.ReadAllText(packetMirrorPath))!.AsObject();
            driftedPacket["goal"] = "mirror drifted goal";
            File.WriteAllText(packetMirrorPath, driftedPacket.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            var artifactRepository = new JsonRuntimeArtifactRepository(paths);
            var runId = "RUN-T-INTEGRATION-HARDENING-001";
            var evidenceRoot = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker-executions", runId);
            Directory.CreateDirectory(evidenceRoot);
            File.WriteAllText(Path.Combine(evidenceRoot, "evidence.json"), "{\"task_id\":\"T-INTEGRATION-HARDENING\"}");

            var resultRoot = Path.Combine(sandbox.RootPath, ".ai", "execution", "T-INTEGRATION-HARDENING");
            Directory.CreateDirectory(resultRoot);
            File.WriteAllText(
                Path.Combine(resultRoot, "result.json"),
                JsonSerializer.Serialize(
                    new ResultEnvelope
                    {
                        TaskId = "T-INTEGRATION-HARDENING",
                        ExecutionRunId = runId,
                        ExecutionEvidencePath = ".ai/artifacts/worker-executions/" + runId + "/evidence.json",
                        Status = "success",
                        Changes = new ResultEnvelopeChanges
                        {
                            FilesModified = ["src/CARVES.Runtime.Host/Program.cs"],
                        },
                        Validation = new ResultEnvelopeValidation
                        {
                            Build = "success",
                            Tests = "success",
                        },
                        Next = new ResultEnvelopeNextAction
                        {
                            Suggested = "review_task",
                        },
                    },
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true,
                    }));

            artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
            {
                TaskId = "T-INTEGRATION-HARDENING",
                Result = new WorkerExecutionResult
                {
                    TaskId = "T-INTEGRATION-HARDENING",
                    RunId = runId,
                    BackendId = "codex_cli",
                    ProviderId = "codex",
                    AdapterId = "CodexCliWorkerAdapter",
                    Status = WorkerExecutionStatus.Succeeded,
                    FailureKind = WorkerFailureKind.None,
                    FailureLayer = WorkerFailureLayer.None,
                },
                Evidence = new ExecutionEvidence
                {
                    TaskId = "T-INTEGRATION-HARDENING",
                    RunId = runId,
                    WorkerId = "CodexCliWorkerAdapter",
                    EvidenceSource = ExecutionEvidenceSource.Host,
                    CommandsExecuted = ["dotnet build", "dotnet test"],
                    FilesWritten = ["src/CARVES.Runtime.Host/Program.cs"],
                    EvidencePath = ".ai/artifacts/worker-executions/" + runId + "/evidence.json",
                    EvidenceCompleteness = ExecutionEvidenceCompleteness.Complete,
                    EvidenceStrength = ExecutionEvidenceStrength.Replayable,
                    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                    EndedAt = DateTimeOffset.UtcNow,
                    ExitStatus = 0,
                },
            });

            var packetEnforcementService = new PacketEnforcementService(paths, taskGraphService, artifactRepository);
            packetEnforcementService.Persist("T-INTEGRATION-HARDENING");

            var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "execution-hardening", "T-INTEGRATION-HARDENING");
            var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "execution-hardening", "T-INTEGRATION-HARDENING");

            Assert.Equal(0, inspect.ExitCode);
            Assert.Equal(0, api.ExitCode);
            Assert.Contains("Execution hardening", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Governance bootstrap:", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("get_execution_packet", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Packet enforcement verdict: reject", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Authoritative drift:", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("execution_packets", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("- off_packet_mutation: supported=True; current=reject", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("- missing_validation:", inspect.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("- mirror_drift: supported=True; current=drifted", inspect.StandardOutput, StringComparison.Ordinal);

            using var document = JsonDocument.Parse(api.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("execution-hardening", root.GetProperty("surface_id").GetString());
            Assert.Equal("T-INTEGRATION-HARDENING", root.GetProperty("task_id").GetString());
            Assert.True(root.GetProperty("governance").GetProperty("bootstrap_ready").GetBoolean());
            Assert.Equal("reject", root.GetProperty("packet_enforcement").GetProperty("record").GetProperty("verdict").GetString());
            Assert.Contains(root.GetProperty("relevant_tools").EnumerateArray(), tool => tool.GetProperty("tool_id").GetString() == "get_execution_packet");
            Assert.Contains(root.GetProperty("negative_paths").EnumerateArray(), path =>
                path.GetProperty("path_id").GetString() == "missing_validation");
            Assert.Contains(root.GetProperty("negative_paths").EnumerateArray(), path =>
                path.GetProperty("path_id").GetString() == "mirror_drift"
                && path.GetProperty("current_status").GetString() == "drifted");
            Assert.Contains(root.GetProperty("authoritative_truth").GetProperty("families").EnumerateArray(), family =>
                family.GetProperty("family_id").GetString() == "execution_packets"
                && family.GetProperty("mirror_drift_detected").GetBoolean()
                && family.GetProperty("mirror_state").GetString() == "drifted");
        }
        finally
        {
            if (Directory.Exists(truthStore.AuthoritativeRoot))
            {
                Directory.Delete(truthStore.AuthoritativeRoot, true);
            }
        }
    }

    [Fact]
    public void SyncStateAndRunNextDryRun_WriteArtifacts()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-DRYRUN");

        var sync = RunProgram("--repo-root", sandbox.RootPath, "--cold", "sync-state");
        var dryRunExitCode = Program.Main(["--repo-root", sandbox.RootPath, "run-next", "--dry-run"]);

        Assert.True(sync.ExitCode == 0, sync.CombinedOutput);
        Assert.Equal(0, dryRunExitCode);
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, ".ai", "CURRENT_TASK.md")));
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")));
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker", "T-INTEGRATION-DRYRUN.json")));
        Assert.Contains(
            "\"schema_version\": 1",
            File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "worker", "T-INTEGRATION-DRYRUN.json")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void SessionCommands_PersistLifecycleAndTick()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-SESSION");

        var start = RunProgram("--repo-root", sandbox.RootPath, "session", "start", "--dry-run");
        var status = RunProgram("--repo-root", sandbox.RootPath, "session", "status");
        var tick = RunProgram("--repo-root", sandbox.RootPath, "session", "tick", "--dry-run");
        var pause = RunProgram("--repo-root", sandbox.RootPath, "session", "pause", "Waiting", "for", "review");
        var resume = RunProgram("--repo-root", sandbox.RootPath, "session", "resume", "Resume", "work");
        var stop = RunProgram("--repo-root", sandbox.RootPath, "session", "stop", "End", "session");
        var sessionJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json"));

        Assert.Equal(0, start.ExitCode);
        Assert.Equal(0, status.ExitCode);
        Assert.Equal(0, tick.ExitCode);
        Assert.Equal(0, pause.ExitCode);
        Assert.Equal(0, resume.ExitCode);
        Assert.Equal(0, stop.ExitCode);
        Assert.Contains("Status: Idle", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Current actionability:", status.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"stopped\"", sessionJson, StringComparison.Ordinal);
        Assert.Contains("\"tick_count\":", sessionJson, StringComparison.Ordinal);
    }

    [Fact]
    public void DetectAndMaterializeRefactors_CreateSuggestedTaskWhenPriorityGateClears()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.ClearRefactoringSuggestions();
        sandbox.AddSyntheticRefactoringSmell();

        var detectExitCode = Program.Main(["--repo-root", sandbox.RootPath, "detect-refactors"]);
        var materializeExitCode = Program.Main(["--repo-root", sandbox.RootPath, "materialize-refactors"]);
        var suggestedTaskFiles = Directory.EnumerateFiles(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes"), "T-REFQ-*.json").ToArray();
        var queueIndexPath = Path.Combine(sandbox.RootPath, ".ai", "refactoring", "queues", "index.json");

        Assert.Equal(0, detectExitCode);
        Assert.Equal(0, materializeExitCode);
        Assert.NotEmpty(suggestedTaskFiles);
        Assert.True(File.Exists(queueIndexPath));
        Assert.Contains("\"status\": \"suggested\"", File.ReadAllText(suggestedTaskFiles[0]), StringComparison.Ordinal);
        Assert.Contains("\"source\": \"REFACTORING_BACKLOG\"", File.ReadAllText(suggestedTaskFiles[0]), StringComparison.Ordinal);
        Assert.Contains("\"refactoring_queue_id\"", File.ReadAllText(suggestedTaskFiles[0]), StringComparison.Ordinal);
        Assert.Contains("\"queues\"", File.ReadAllText(queueIndexPath), StringComparison.Ordinal);
    }

    [Fact]
    public void DetectAndShowOpportunities_CreateOpportunityTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.ClearRefactoringSuggestions();
        sandbox.ClearOpportunities();
        sandbox.AddSyntheticRefactoringSmell();

        var detect = RunProgram("--repo-root", sandbox.RootPath, "detect-opportunities");
        var show = RunProgram("--repo-root", sandbox.RootPath, "show-opportunities");
        var opportunitiesPath = Path.Combine(sandbox.RootPath, ".ai", "opportunities", "index.json");

        Assert.Equal(0, detect.ExitCode);
        Assert.Equal(0, show.ExitCode);
        Assert.True(File.Exists(opportunitiesPath));
        Assert.Contains("\"schema_version\": 1", File.ReadAllText(opportunitiesPath), StringComparison.Ordinal);
        Assert.Contains("Opportunities:", show.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void SessionLoop_MaterializesOpportunityPipelineWhenNoExecutionTaskExists()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.ClearRefactoringSuggestions();
        sandbox.ClearOpportunities();
        sandbox.AddSyntheticRefactoringSmell();

        var start = RunProgram("--repo-root", sandbox.RootPath, "session", "start", "--dry-run");
        var loop = RunProgram("--repo-root", sandbox.RootPath, "session", "loop", "--dry-run", "--iterations", "1");
        var sessionJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json"));
        var planningTaskFiles = Directory.EnumerateFiles(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes"), "T-PLAN-*.json").ToArray();

        Assert.Equal(0, start.ExitCode);
        Assert.Equal(0, loop.ExitCode);
        Assert.NotEmpty(planningTaskFiles);
        Assert.Contains("\"planner_round\": 1", sessionJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenAiProvider_CanRunThroughRuntimeLoop()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticTask("T-INTEGRATION-PROVIDER", "pending", "Synthetic provider task");
        await using var server = new StubResponsesApiServer("""
{
  "id": "resp_test_123",
  "model": "gpt-5-mini",
  "output": [
    {
      "type": "message",
      "content": [
        {
          "type": "output_text",
          "text": "scope: tests only\nrisks: none\nvalidation: dotnet test"
        }
      ]
    }
  ],
  "usage": {
    "input_tokens": 12,
    "output_tokens": 9
  }
}
""");
        sandbox.WriteAiProviderConfig($$"""
{
  "provider": "openai",
  "enabled": true,
  "model": "gpt-5-mini",
  "base_url": "{{server.Url}}",
  "api_key_environment_variable": "OPENAI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 10,
  "max_output_tokens": 300,
  "reasoning_effort": "low",
  "organization": null,
  "project": null
}
""");

        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-key");
        try
        {
            var exitCode = Program.Main(["--repo-root", sandbox.RootPath, "run-next"]);
            var providerArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "provider", "T-INTEGRATION-PROVIDER.json");
            var reviewArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", "T-INTEGRATION-PROVIDER.json");
            var taskNodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-PROVIDER.json");
            var reviewArtifactBeforeApproval = File.ReadAllText(reviewArtifactPath);
            var approveExitCode = Program.Main(["--repo-root", sandbox.RootPath, "approve-review", "T-INTEGRATION-PROVIDER", "Human", "accepted", "the", "review"]);
            var mergeCandidatePath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "merge-candidates", "T-INTEGRATION-PROVIDER.json");

            Assert.Equal(0, exitCode);
            Assert.Equal(0, approveExitCode);
            Assert.True(File.Exists(providerArtifactPath));
            Assert.True(File.Exists(reviewArtifactPath));
            Assert.True(File.Exists(mergeCandidatePath));
            Assert.Contains("\"provider\": \"openai\"", File.ReadAllText(providerArtifactPath), StringComparison.Ordinal);
            Assert.Contains("\"worker_adapter\": \"OpenAiWorkerAdapter\"", File.ReadAllText(providerArtifactPath), StringComparison.Ordinal);
            Assert.Contains("\"used_fallback\": false", File.ReadAllText(providerArtifactPath), StringComparison.Ordinal);
            Assert.Contains("\"decision_status\": \"pending_review\"", reviewArtifactBeforeApproval, StringComparison.Ordinal);
            Assert.Contains("\"decision_status\": \"approved\"", File.ReadAllText(reviewArtifactPath), StringComparison.Ordinal);
            Assert.Contains("\"status\": \"completed\"", File.ReadAllText(taskNodePath), StringComparison.Ordinal);
            Assert.Contains("\"schema_version\": 1", File.ReadAllText(mergeCandidatePath), StringComparison.Ordinal);
            Assert.Contains("\"model\":\"gpt-5-mini\"", server.LastRequestBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    private static ProgramRunResult RunProgram(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = Program.Main(args);
            return new ProgramRunResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => string.Concat(StandardOutput, StandardError);
    }
}
