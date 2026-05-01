using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Host;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimePackHostContractTests
{
    [Fact]
    public void PackAlias_DelegatesValidationAndInspectToRuntimeOwnedPackSurfaces()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var referencePackPath = Path.Combine(sandbox.RootPath, "docs", "product", "reference-packs", "runtime-pack-v1-dotnet-webapi.json");

        var validate = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "validate", referencePackPath);
        var inspectAdmissionPolicy = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "inspect", "admission-policy");
        var inspectBoundary = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "inspect", "distribution-boundary");
        var help = RunProgram("--repo-root", sandbox.RootPath, "help");

        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(0, inspectAdmissionPolicy.ExitCode);
        Assert.Equal(0, inspectBoundary.ExitCode);
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("Validator: runtime_pack_v1", validate.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime pack admission policy", inspectAdmissionPolicy.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime pack distribution boundary", inspectBoundary.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pack validate <json-path>", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pack admit <runtime-pack-v1-manifest-path>", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pack explain --task <task-id>", help.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void PackAdmit_ManifestBridgeDelegatesToRuntimeOwnedAdmissionTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var referencePackPath = Path.Combine(sandbox.RootPath, "docs", "product", "reference-packs", "runtime-pack-v1-dotnet-webapi.json");

        var admit = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "admit", referencePackPath);
        var inspectAdmission = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "inspect", "admission");
        var inspectSelection = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "inspect", "selection");
        var apiAdmission = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-admission");

        Assert.Equal(0, admit.ExitCode);
        Assert.Equal(0, inspectAdmission.ExitCode);
        Assert.Equal(0, inspectSelection.ExitCode);
        Assert.Equal(0, apiAdmission.ExitCode);
        Assert.Contains("Runtime Pack v1 manifest admission bridge", admit.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Pack: carves.firstparty.dotnet-webapi@0.1.0 (stable)", inspectAdmission.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("No runtime-local pack selection has been recorded yet.", inspectSelection.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(apiAdmission.StandardOutput);
        var currentAdmission = document.RootElement.GetProperty("current_admission");
        var packArtifactPath = currentAdmission.GetProperty("pack_artifact_path").GetString();
        var attributionPath = currentAdmission.GetProperty("runtime_pack_attribution_path").GetString();
        Assert.Equal("carves.firstparty.dotnet-webapi", currentAdmission.GetProperty("pack_id").GetString());
        Assert.Equal("0.1.0", currentAdmission.GetProperty("pack_version").GetString());
        Assert.Equal("stable", currentAdmission.GetProperty("channel").GetString());
        Assert.NotNull(packArtifactPath);
        Assert.NotNull(attributionPath);
        Assert.Contains(".ai/artifacts/packs/", packArtifactPath!, StringComparison.Ordinal);
        Assert.Contains(".ai/artifacts/packs/", attributionPath!, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, packArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(sandbox.RootPath, attributionPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void PackDogfood_ValidatesAllFirstPartyReferencePacksThroughAliasSurface()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var dotnetPackPath = Path.Combine(sandbox.RootPath, "docs", "product", "reference-packs", "runtime-pack-v1-dotnet-webapi.json");
        var nodePackPath = Path.Combine(sandbox.RootPath, "docs", "product", "reference-packs", "runtime-pack-v1-node-typescript.json");
        var securityPackPath = Path.Combine(sandbox.RootPath, "docs", "product", "reference-packs", "runtime-pack-v1-security-review.json");

        var validateDotnet = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "validate", dotnetPackPath);
        var validateNode = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "validate", nodePackPath);
        var validateSecurity = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "validate", securityPackPath);

        Assert.Equal(0, validateDotnet.ExitCode);
        Assert.Equal(0, validateNode.ExitCode);
        Assert.Equal(0, validateSecurity.ExitCode);
        Assert.Contains("Validator: runtime_pack_v1", validateDotnet.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Validator: runtime_pack_v1", validateNode.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Validator: runtime_pack_v1", validateSecurity.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void PackContextConsumption_ProjectUnderstandingRecipeShapesContextPackThroughRuntimeOwnedSelection()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.AddSyntheticPendingTask(
            "T-INTEGRATION-PACK-CONTEXT",
            scope: ["src/CARVES.Runtime.Application/Platform/RuntimePackAdmissionService.cs"]);

        var referencePackPath = Path.Combine(sandbox.RootPath, "docs", "product", "reference-packs", "runtime-pack-v1-dotnet-webapi.json");

        var admit = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "admit", referencePackPath);
        var assign = RunProgram(
            "--repo-root",
            sandbox.RootPath,
            "--cold",
            "pack",
            "assign",
            "carves.firstparty.dotnet-webapi",
            "--pack-version",
            "0.1.0",
            "--channel",
            "stable",
            "--reason",
            "select declarative pack for context shaping");
        var inspectContextPack = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "context-pack", "T-INTEGRATION-PACK-CONTEXT");

        Assert.Equal(0, admit.ExitCode);
        Assert.Equal(0, assign.ExitCode);
        Assert.Equal(0, inspectContextPack.ExitCode);
        Assert.Contains("runtime_pack:carves.firstparty.dotnet-webapi@0.1.0(stable)", inspectContextPack.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime pack current: carves.firstparty.dotnet-webapi@0.1.0 (stable)", inspectContextPack.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime_pack_project_understanding", inspectContextPack.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime_pack_manifest: docs/product/reference-packs/runtime-pack-v1-dotnet-webapi.json", inspectContextPack.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Program.cs", inspectContextPack.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".csproj", inspectContextPack.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void PackVerificationRecipe_RuntimeAdmissionProjectsCommandsAndDecisionRecordsIntoExecutionTrace()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-PACK-VERIFY", scope: ["src/CARVES.Runtime.Application/Platform/RuntimePackAdmissionService.cs"]);
        using var host = StartedResidentHost.Start(sandbox.RootPath, 200);

        var referencePackPath = Path.Combine(sandbox.RootPath, "docs", "product", "reference-packs", "runtime-pack-v1-dotnet-webapi.json");

        var admit = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "admit", referencePackPath);
        var assign = RunProgram(
            "--repo-root",
            sandbox.RootPath,
            "--cold",
            "pack",
            "assign",
            "carves.firstparty.dotnet-webapi",
            "--pack-version",
            "0.1.0",
            "--channel",
            "stable",
            "--reason",
            "select declarative pack for verification admission");
        var run = RunProgram("--repo-root", sandbox.RootPath, "task", "run", "T-INTEGRATION-PACK-VERIFY", "--dry-run");
        var inspectTrace = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "execution-trace", "T-INTEGRATION-PACK-VERIFY");

        Assert.Equal(0, admit.ExitCode);
        Assert.Equal(0, assign.ExitCode);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(0, inspectTrace.ExitCode);

        using var traceDocument = JsonDocument.Parse(inspectTrace.StandardOutput);
        var root = traceDocument.RootElement;
        var packCommandAdmission = root.GetProperty("pack_command_admission");

        Assert.Contains("carves.firstparty.dotnet-webapi@0.1.0", packCommandAdmission.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.Equal(2, packCommandAdmission.GetProperty("admitted_count").GetInt32());
        Assert.Equal(0, packCommandAdmission.GetProperty("elevated_count").GetInt32());
        Assert.Equal(0, packCommandAdmission.GetProperty("blocked_count").GetInt32());
        Assert.Equal(0, packCommandAdmission.GetProperty("rejected_count").GetInt32());
        Assert.Contains(
            packCommandAdmission.GetProperty("recipe_ids").EnumerateArray().Select(item => item.GetString()),
            value => string.Equals(value, "dotnet-build-and-test", StringComparison.Ordinal));

        var commandIds = packCommandAdmission.GetProperty("decisions")
            .EnumerateArray()
            .Select(item => item.GetProperty("command_id").GetString())
            .ToArray();
        Assert.Contains("dotnet-build", commandIds);
        Assert.Contains("dotnet-test", commandIds);
        Assert.All(
            packCommandAdmission.GetProperty("decisions").EnumerateArray(),
            item => Assert.Equal("admitted", item.GetProperty("verdict").GetString()));

        var validationCommands = root.GetProperty("validation_trace")
            .EnumerateArray()
            .Select(item => item.GetProperty("command").GetString())
            .ToArray();
        Assert.Contains("dotnet build", validationCommands);
        Assert.Contains("dotnet test", validationCommands);

        var runDecisionRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "runs", "T-INTEGRATION-PACK-VERIFY");
        Assert.True(Directory.Exists(runDecisionRoot));
        Assert.Equal(
            2,
            Directory.GetFiles(runDecisionRoot, "*.pack-command-admission.json", SearchOption.TopDirectoryOnly).Length);
    }

    [Fact]
    public void PackReviewRubric_RuntimeProjectionAppearsInTaskInspectAndPackExplainability()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.AddSyntheticPendingTask(
            "T-INTEGRATION-PACK-REVIEW",
            scope: ["src/CARVES.Runtime.Application/ControlPlane/ReviewEvidenceGateService.cs"]);

        var referencePackPath = Path.Combine(sandbox.RootPath, "docs", "product", "reference-packs", "runtime-pack-v1-security-review.json");

        var admit = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "admit", referencePackPath);
        var assign = RunProgram(
            "--repo-root",
            sandbox.RootPath,
            "--cold",
            "pack",
            "assign",
            "carves.firstparty.security-review",
            "--pack-version",
            "0.1.0",
            "--channel",
            "stable",
            "--reason",
            "select declarative pack for review rubric projection");
        var inspectTask = RunProgram("--repo-root", sandbox.RootPath, "--cold", "task", "inspect", "T-INTEGRATION-PACK-REVIEW");
        var explain = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "explain", "--task", "T-INTEGRATION-PACK-REVIEW");

        Assert.Equal(0, admit.ExitCode);
        Assert.Equal(0, assign.ExitCode);
        Assert.Equal(0, inspectTask.ExitCode);
        Assert.Equal(0, explain.ExitCode);

        using var inspectDocument = JsonDocument.Parse(inspectTask.StandardOutput);
        var root = inspectDocument.RootElement;
        var projection = root.GetProperty("runtime_pack_review_rubric");

        Assert.Contains("carves.firstparty.security-review@0.1.0", projection.GetProperty("summary").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, projection.GetProperty("rubric_count").GetInt32());
        Assert.Equal(3, projection.GetProperty("checklist_item_count").GetInt32());
        Assert.Equal(
            "docs/product/reference-packs/runtime-pack-v1-security-review.json",
            projection.GetProperty("manifest_path").GetString());

        var rubric = projection.GetProperty("rubrics")[0];
        Assert.Equal("security-review-rubric", rubric.GetProperty("rubric_id").GetString());
        var checklistIds = rubric.GetProperty("checklist_items")
            .EnumerateArray()
            .Select(item => item.GetProperty("checklist_item_id").GetString())
            .ToArray();
        Assert.Contains("security-input-validation", checklistIds);
        Assert.Contains("security-secret-handling", checklistIds);
        Assert.Contains("security-protected-root-boundary", checklistIds);

        Assert.Contains("Review rubric projection: carves.firstparty.security-review@0.1.0 (stable); rubrics=1; checklist_items=3", explain.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("security-review-rubric", explain.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("security-protected-root-boundary", explain.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void PackAlias_ValidationAndInspectDoNotCreateSecondRuntimePackTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var invalidManifestPath = Path.Combine(payloadRoot, "runtime-pack-v1-invalid.json");
        File.WriteAllText(
            invalidManifestPath,
            """
            {
              "schemaVersion": "carves.pack.v1",
              "packId": "carves.community.invalid-surface-check",
              "packVersion": "0.1.0",
              "name": "Invalid Surface Check",
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

        var before = SnapshotRuntimePackTruth(sandbox.RootPath);

        var validate = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "validate", invalidManifestPath);
        var inspectSelection = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "inspect", "selection");
        var inspectAudit = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "audit");
        var inspectMismatch = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "mismatch");

        var after = SnapshotRuntimePackTruth(sandbox.RootPath);

        Assert.Equal(1, validate.ExitCode);
        Assert.Equal(0, inspectSelection.ExitCode);
        Assert.Equal(0, inspectAudit.ExitCode);
        Assert.Equal(0, inspectMismatch.ExitCode);
        Assert.Contains("truthWrite_invalid", validate.StandardError, StringComparison.Ordinal);
        Assert.Equal(before, after);
    }

    [Fact]
    public void RuntimePackAdmissionPolicy_ProjectsLocalConstraintsAndRejectsPreviewPack()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var previewPackArtifactPath = Path.Combine(payloadRoot, "preview-pack-artifact.json");
        File.WriteAllText(previewPackArtifactPath, CreatePackArtifactJson("1.2.3", "preview"));
        var previewAttributionPath = Path.Combine(payloadRoot, "preview-pack-attribution.json");
        File.WriteAllText(previewAttributionPath, CreateAttributionJson("1.2.3", "preview"));

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-admission-policy");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-admission-policy");
        var rejectPreview = RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "admit-pack", previewPackArtifactPath, "--attribution", previewAttributionPath);

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Equal(1, rejectPreview.ExitCode);
        Assert.Contains("Allowed channels: stable, candidate", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime_pack_admission_channel_disallowed", string.Concat(rejectPreview.StandardOutput, rejectPreview.StandardError), StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var policy = document.RootElement.GetProperty("current_policy");
        Assert.Equal("runtime-pack-admission-policy", document.RootElement.GetProperty("surface_id").GetString());
        Assert.Equal(2, policy.GetProperty("allowed_channels").GetArrayLength());
        Assert.True(policy.GetProperty("require_signature").GetBoolean());
        Assert.True(policy.GetProperty("require_provenance").GetBoolean());
    }

    [Fact]
    public void RuntimePackTaskExplainability_ProjectsTaskScopedCoverageAndEvidence()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var artifactRepository = new JsonRuntimeArtifactRepository(paths);
        var runService = new ExecutionRunService(paths, artifactRepository);
        var reportService = new ExecutionRunReportService(paths);

        artifactRepository.SaveRuntimePackSelectionArtifact(CreateSelection("packsel-001", "1.2.3"));
        PersistCompletedRun(runService, reportService, CreateTask("T-INTEGRATION-PACK-A"));
        PersistCompletedRun(runService, reportService, CreateTask("T-INTEGRATION-PACK-B"));

        artifactRepository.SaveRuntimePackSelectionArtifact(CreateSelection("packsel-002", "1.2.4"));
        PersistCompletedRun(runService, reportService, CreateTask("T-INTEGRATION-PACK-A"));

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-task-explainability", "T-INTEGRATION-PACK-A");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-task-explainability", "T-INTEGRATION-PACK-A");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime pack task explainability", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Task: T-INTEGRATION-PACK-A", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("T-INTEGRATION-PACK-B", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-pack-task-explainability", root.GetProperty("surface_id").GetString());
        Assert.Equal("T-INTEGRATION-PACK-A", root.GetProperty("task_id").GetString());
        Assert.Equal(2, root.GetProperty("recent_runs").GetArrayLength());
        Assert.Equal(2, root.GetProperty("recent_reports").GetArrayLength());
        Assert.All(root.GetProperty("recent_runs").EnumerateArray(), item => Assert.Equal("T-INTEGRATION-PACK-A", item.GetProperty("task_id").GetString()));
        Assert.All(root.GetProperty("recent_reports").EnumerateArray(), item => Assert.Equal("T-INTEGRATION-PACK-A", item.GetProperty("task_id").GetString()));
        Assert.Equal(1, root.GetProperty("coverage").GetProperty("current_selection_run_count").GetInt32());
        Assert.Equal(1, root.GetProperty("coverage").GetProperty("divergent_run_count").GetInt32());
    }

    [Fact]
    public void RuntimePackSwitchPolicy_BlocksDivergentAssignUntilPinClears()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var packOneArtifactPath = Path.Combine(payloadRoot, "pack-one-artifact.json");
        File.WriteAllText(packOneArtifactPath, CreatePackArtifactJson("1.2.3", "stable"));
        var packOneAttributionPath = Path.Combine(payloadRoot, "pack-one-attribution.json");
        File.WriteAllText(packOneAttributionPath, CreateAttributionJson("1.2.3", "stable"));

        var packTwoArtifactPath = Path.Combine(payloadRoot, "pack-two-artifact.json");
        File.WriteAllText(packTwoArtifactPath, CreatePackArtifactJson("1.2.4", "stable"));
        var packTwoAttributionPath = Path.Combine(payloadRoot, "pack-two-attribution.json");
        File.WriteAllText(packTwoAttributionPath, CreateAttributionJson("1.2.4", "stable"));

        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "admit-pack", packOneArtifactPath, "--attribution", packOneAttributionPath).ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "assign-pack", "carves.runtime.core", "--pack-version", "1.2.3", "--channel", "stable", "--reason", "select pack one").ExitCode);

        var pin = RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "pin-current-pack", "--reason", "lock current selection");
        var inspectPinned = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-switch-policy");
        var apiPinned = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-switch-policy");

        Assert.Equal(0, pin.ExitCode);
        Assert.Equal(0, inspectPinned.ExitCode);
        Assert.Equal(0, apiPinned.ExitCode);
        Assert.Contains("Pin active: True", inspectPinned.StandardOutput, StringComparison.Ordinal);

        using (var pinnedDocument = JsonDocument.Parse(apiPinned.StandardOutput))
        {
            var policy = pinnedDocument.RootElement.GetProperty("current_policy");
            Assert.True(policy.GetProperty("pin_active").GetBoolean());
            Assert.Equal("1.2.3", policy.GetProperty("pack_version").GetString());
        }

        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "admit-pack", packTwoArtifactPath, "--attribution", packTwoAttributionPath).ExitCode);
        var blockedAssign = RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "assign-pack", "carves.runtime.core", "--pack-version", "1.2.4", "--channel", "stable", "--reason", "attempt divergent switch");

        Assert.Equal(1, blockedAssign.ExitCode);
        Assert.Contains("runtime_pack_selection_blocked_by_local_pin", string.Concat(blockedAssign.StandardOutput, blockedAssign.StandardError), StringComparison.Ordinal);

        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "clear-pack-pin", "--reason", "unlock selection").ExitCode);
        var assignAfterClear = RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "assign-pack", "carves.runtime.core", "--pack-version", "1.2.4", "--channel", "stable", "--reason", "switch after clear");

        Assert.Equal(0, assignAfterClear.ExitCode);
        Assert.Contains("Runtime-local pack switch policy is unpinned", RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-switch-policy").StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePackMismatchDiagnostics_ProjectsBoundedCategoriesAndNextActions()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var artifactRepository = new JsonRuntimeArtifactRepository(paths);
        var runService = new ExecutionRunService(paths, artifactRepository);
        var reportService = new ExecutionRunReportService(paths);

        artifactRepository.SaveRuntimePackAdmissionArtifact(CreateAdmission("1.2.3"));
        artifactRepository.SaveRuntimePackSelectionArtifact(CreateSelection("packsel-old", "1.2.3"));
        PersistCompletedRun(runService, reportService, CreateTask("T-INTEGRATION-PACK-MISMATCH"));
        artifactRepository.SaveRuntimePackSelectionArtifact(CreateSelection("packsel-new", "1.2.4"));

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-mismatch-diagnostics");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-mismatch-diagnostics");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("selection_not_currently_admitted", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("recent_execution_diverges_from_current_selection", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-pack-task-explainability T-INTEGRATION-PACK-MISMATCH", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var diagnostics = document.RootElement.GetProperty("diagnostics").EnumerateArray().ToArray();
        Assert.Contains(diagnostics, item => item.GetProperty("diagnostic_code").GetString() == "selection_not_currently_admitted");
        Assert.Contains(diagnostics, item => item.GetProperty("diagnostic_code").GetString() == "recent_execution_diverges_from_current_selection");
    }

    [Fact]
    public void PackAlias_DelegatesLifecycleActionsToRuntimeSelectionSurfaces()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var packOneArtifactPath = Path.Combine(payloadRoot, "pack-one-artifact.json");
        File.WriteAllText(packOneArtifactPath, CreatePackArtifactJson("1.2.3", "stable"));
        var packOneAttributionPath = Path.Combine(payloadRoot, "pack-one-attribution.json");
        File.WriteAllText(packOneAttributionPath, CreateAttributionJson("1.2.3", "stable"));

        var packTwoArtifactPath = Path.Combine(payloadRoot, "pack-two-artifact.json");
        File.WriteAllText(packTwoArtifactPath, CreatePackArtifactJson("1.2.4", "stable"));
        var packTwoAttributionPath = Path.Combine(payloadRoot, "pack-two-attribution.json");
        File.WriteAllText(packTwoAttributionPath, CreateAttributionJson("1.2.4", "stable"));

        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "admit", packOneArtifactPath, "--attribution", packOneAttributionPath).ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "assign", "carves.runtime.core", "--pack-version", "1.2.3", "--channel", "stable", "--reason", "select pack one through alias").ExitCode);

        var firstSelectionApi = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-selection");
        Assert.Equal(0, firstSelectionApi.ExitCode);
        using var firstSelectionDocument = JsonDocument.Parse(firstSelectionApi.StandardOutput);
        var firstSelectionId = firstSelectionDocument.RootElement.GetProperty("current_selection").GetProperty("selection_id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(firstSelectionId));

        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "pin", "--reason", "pin alias selection").ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "admit", packTwoArtifactPath, "--attribution", packTwoAttributionPath).ExitCode);

        var blockedAssign = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "assign", "carves.runtime.core", "--pack-version", "1.2.4", "--channel", "stable", "--reason", "blocked while pinned");
        Assert.Equal(1, blockedAssign.ExitCode);
        Assert.Contains("runtime_pack_selection_blocked_by_local_pin", string.Concat(blockedAssign.StandardOutput, blockedAssign.StandardError), StringComparison.Ordinal);

        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "unpin", "--reason", "clear alias pin").ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "assign", "carves.runtime.core", "--pack-version", "1.2.4", "--channel", "stable", "--reason", "select pack two through alias").ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "admit", packOneArtifactPath, "--attribution", packOneAttributionPath).ExitCode);

        var rollback = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "rollback", firstSelectionId!, "--reason", "rollback through alias");
        var inspectSelection = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "inspect", "selection");

        Assert.Equal(0, rollback.ExitCode);
        Assert.Equal(0, inspectSelection.ExitCode);
        Assert.Contains("Runtime pack selection", inspectSelection.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Current selection: carves.runtime.core@1.2.3 (stable)", inspectSelection.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void PackAlias_DelegatesExplainAuditAndMismatchToRuntimeOwnedSurfaces()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var artifactRepository = new JsonRuntimeArtifactRepository(paths);
        var runService = new ExecutionRunService(paths, artifactRepository);
        var reportService = new ExecutionRunReportService(paths);

        artifactRepository.SaveRuntimePackAdmissionArtifact(CreateAdmission("1.2.3"));
        artifactRepository.SaveRuntimePackSelectionArtifact(CreateSelection("packsel-old", "1.2.3"));
        PersistCompletedRun(runService, reportService, CreateTask("T-INTEGRATION-PACK-ALIAS"));
        artifactRepository.SaveRuntimePackSelectionArtifact(CreateSelection("packsel-new", "1.2.4"));

        var explain = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "explain", "--task", "T-INTEGRATION-PACK-ALIAS");
        var audit = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "audit");
        var mismatch = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "mismatch");

        Assert.Equal(0, explain.ExitCode);
        Assert.Equal(0, audit.ExitCode);
        Assert.Equal(0, mismatch.ExitCode);
        Assert.Contains("Runtime pack task explainability", explain.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Task: T-INTEGRATION-PACK-ALIAS", explain.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime pack execution audit", audit.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime pack mismatch diagnostics", mismatch.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("recent_execution_diverges_from_current_selection", mismatch.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void PackAuditAndMismatch_ProjectDeclarativeContributionSnapshotsAndDrift()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var paths = ControlPlanePaths.FromRepoRoot(sandbox.RootPath);
        var artifactRepository = new JsonRuntimeArtifactRepository(paths);
        var runService = new ExecutionRunService(paths, artifactRepository);
        var reportService = new ExecutionRunReportService(paths);
        var referencePackPath = Path.Combine(sandbox.RootPath, "docs", "product", "reference-packs", "runtime-pack-v1-security-review.json");

        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "admit", referencePackPath).ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "assign", "carves.firstparty.security-review", "--pack-version", "0.1.0", "--channel", "stable", "--reason", "select declarative review pack").ExitCode);
        PersistCompletedRun(runService, reportService, CreateTask("T-INTEGRATION-PACK-DECLARATIVE-DRIFT"));

        var mutatedPackNode = JsonNode.Parse(File.ReadAllText(referencePackPath))!.AsObject();
        var checklistItems = mutatedPackNode["recipes"]!["reviewRubrics"]![0]!["checklistItems"]!.AsArray();
        checklistItems.Add(new JsonObject
        {
            ["id"] = "security-new-drift-check",
            ["severity"] = "warn",
            ["text"] = "Check newly added drift-sensitive review guidance."
        });
        File.WriteAllText(referencePackPath, mutatedPackNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var audit = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "audit");
        var mismatch = RunProgram("--repo-root", sandbox.RootPath, "--cold", "pack", "mismatch");

        Assert.Equal(0, audit.ExitCode);
        Assert.Equal(0, mismatch.ExitCode);
        Assert.Contains("Declarative contribution:", audit.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("review_rubric", audit.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("recent_declarative_contributions_diverge_from_current_selection", mismatch.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("T-INTEGRATION-PACK-DECLARATIVE-DRIFT", mismatch.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePackPolicyTransfer_ExportsAndImportsLocalPolicyTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var exportPath = Path.Combine(payloadRoot, "runtime-pack-policy.json");
        var previewPackArtifactPath = Path.Combine(payloadRoot, "preview-pack-artifact.json");
        File.WriteAllText(previewPackArtifactPath, CreatePackArtifactJson("1.2.5", "preview"));
        var previewAttributionPath = Path.Combine(payloadRoot, "preview-pack-attribution.json");
        File.WriteAllText(previewAttributionPath, CreateAttributionJson("1.2.5", "preview"));

        var export = RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "export-pack-policy", exportPath);
        Assert.Equal(0, export.ExitCode);
        Assert.True(File.Exists(exportPath));

        var packageNode = JsonNode.Parse(File.ReadAllText(exportPath))!.AsObject();
        var admissionPolicy = packageNode["admission_policy"]!.AsObject();
        var allowedChannels = admissionPolicy["allowed_channels"]!.AsArray();
        allowedChannels.Add("preview");
        admissionPolicy["policy_id"] = "admission-policy-imported-preview";
        var switchPolicy = packageNode["switch_policy"]!.AsObject();
        switchPolicy["policy_id"] = "switch-policy-imported-preview";
        File.WriteAllText(exportPath, packageNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var import = RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "import-pack-policy", exportPath);
        var inspectTransfer = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-policy-transfer");
        var apiAdmissionPolicy = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-admission-policy");
        var admitPreview = RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "admit-pack", previewPackArtifactPath, "--attribution", previewAttributionPath);

        Assert.Equal(0, import.ExitCode);
        Assert.Equal(0, inspectTransfer.ExitCode);
        Assert.Equal(0, apiAdmissionPolicy.ExitCode);
        Assert.Equal(0, admitPreview.ExitCode);
        Assert.Contains("runtime-pack-policy-transfer", inspectTransfer.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("preview", inspectTransfer.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(apiAdmissionPolicy.StandardOutput);
        var policy = document.RootElement.GetProperty("current_policy");
        Assert.Equal("admission-policy-imported-preview", policy.GetProperty("policy_id").GetString());
        Assert.Contains(policy.GetProperty("allowed_channels").EnumerateArray().Select(item => item.GetString()), value => value == "preview");
    }

    [Fact]
    public void RuntimePackPolicyPreview_ProjectsDiffBeforeImportAndDoesNotMutateCurrentTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var policyPackagePath = Path.Combine(payloadRoot, "runtime-pack-policy.json");
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "export-pack-policy", policyPackagePath).ExitCode);

        var packageNode = JsonNode.Parse(File.ReadAllText(policyPackagePath))!.AsObject();
        var admissionPolicy = packageNode["admission_policy"]!.AsObject();
        admissionPolicy["policy_id"] = "admission-policy-preview";
        admissionPolicy["allowed_channels"] = new JsonArray("stable", "candidate", "preview");
        var switchPolicy = packageNode["switch_policy"]!.AsObject();
        switchPolicy["policy_id"] = "switch-policy-preview";
        switchPolicy["pin_active"] = true;
        switchPolicy["pack_id"] = "carves.runtime.core";
        switchPolicy["pack_version"] = "1.2.5";
        switchPolicy["channel"] = "candidate";
        File.WriteAllText(policyPackagePath, packageNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var preview = RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "preview-pack-policy", policyPackagePath);
        var inspectPreview = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-policy-preview");
        var apiPreview = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-policy-preview");
        var apiAdmissionPolicy = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-admission-policy");

        Assert.Equal(0, preview.ExitCode);
        Assert.Equal(0, inspectPreview.ExitCode);
        Assert.Equal(0, apiPreview.ExitCode);
        Assert.Equal(0, apiAdmissionPolicy.ExitCode);
        Assert.Contains("admission_allowed_channels_changed", inspectPreview.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("switch_pin_state_changed", inspectPreview.StandardOutput, StringComparison.Ordinal);

        using (var previewDocument = JsonDocument.Parse(apiPreview.StandardOutput))
        {
            var root = previewDocument.RootElement;
            Assert.Equal("runtime-pack-policy-preview", root.GetProperty("surface_id").GetString());
            var currentPreview = root.GetProperty("current_preview");
            Assert.Equal("admission-policy-preview", currentPreview.GetProperty("incoming_admission_policy").GetProperty("policy_id").GetString());
            var differenceCodes = currentPreview.GetProperty("differences").EnumerateArray()
                .Select(item => item.GetProperty("diff_code").GetString())
                .ToArray();
            Assert.Contains("switch_target_changed", differenceCodes);
        }

        using var admissionPolicyDocument = JsonDocument.Parse(apiAdmissionPolicy.StandardOutput);
        var currentPolicy = admissionPolicyDocument.RootElement.GetProperty("current_policy");
        Assert.Equal("runtime-pack-admission-policy-default", currentPolicy.GetProperty("policy_id").GetString());
        Assert.DoesNotContain(currentPolicy.GetProperty("allowed_channels").EnumerateArray().Select(item => item.GetString()), value => value == "preview");
    }

    [Fact]
    public void RuntimePackPolicyAudit_ProjectsRecentPolicyChanges()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var payloadRoot = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadRoot);

        var packArtifactPath = Path.Combine(payloadRoot, "stable-pack-artifact.json");
        File.WriteAllText(packArtifactPath, CreatePackArtifactJson("1.2.3", "stable"));
        var attributionPath = Path.Combine(payloadRoot, "stable-pack-attribution.json");
        File.WriteAllText(attributionPath, CreateAttributionJson("1.2.3", "stable"));
        var exportPath = Path.Combine(payloadRoot, "runtime-pack-policy.json");

        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "admit-pack", packArtifactPath, "--attribution", attributionPath).ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "assign-pack", "carves.runtime.core", "--pack-version", "1.2.3", "--channel", "stable", "--reason", "select for policy audit").ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "pin-current-pack", "--reason", "pin for policy audit").ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "clear-pack-pin", "--reason", "clear for policy audit").ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "export-pack-policy", exportPath).ExitCode);
        Assert.Equal(0, RunProgram("--repo-root", sandbox.RootPath, "--cold", "runtime", "import-pack-policy", exportPath).ExitCode);

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-pack-policy-audit");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-pack-policy-audit");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime pack policy audit", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("policy_imported", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("policy_exported", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("clear_pack_pin", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("pin_current_selection", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var entries = document.RootElement.GetProperty("entries").EnumerateArray().Select(item => item.GetProperty("event_kind").GetString()).ToArray();
        Assert.Contains("policy_imported", entries);
        Assert.Contains("policy_exported", entries);
        Assert.Contains("clear_pack_pin", entries);
        Assert.Contains("pin_current_selection", entries);
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
            Title = "Runtime pack host contract test",
            Description = "Exercise task-scoped runtime pack explainability through host surfaces.",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Execution,
            Priority = "P1",
            Scope = ["src/CARVES.Runtime.Application/Platform/"],
            Acceptance = ["task-scoped explainability stays bounded"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    private static RuntimePackSelectionArtifact CreateSelection(string selectionId, string packVersion, string channel = "stable")
    {
        return new RuntimePackSelectionArtifact
        {
            SelectionId = selectionId,
            PackId = "carves.runtime.core",
            PackVersion = packVersion,
            Channel = channel,
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
            AdmissionSource = new RuntimePackAdmissionSource
            {
                AssignmentMode = "overlay_assignment",
                AssignmentRef = $"selection-{selectionId}",
            },
            AdmissionCapturedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            SelectionMode = "manual_local_assignment",
            SelectionReason = "Synthetic integration selection fixture.",
            Summary = $"Selected carves.runtime.core@{packVersion} ({channel}).",
            ChecksPassed =
            [
                "selection remains local-runtime scoped",
                "selection is derived from admitted current evidence"
            ],
        };
    }

    private static RuntimePackAdmissionArtifact CreateAdmission(string packVersion, string channel = "stable")
    {
        return new RuntimePackAdmissionArtifact
        {
            PackId = "carves.runtime.core",
            PackVersion = packVersion,
            Channel = channel,
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
            Summary = $"Admitted carves.runtime.core@{packVersion} ({channel}).",
            ChecksPassed =
            [
                "runtime compatibility accepts local CARVES standard"
            ],
        };
    }

    private static string CreatePackArtifactJson(string packVersion, string channel)
    {
        return $$"""
        {
          "schemaVersion": "1.0",
          "packId": "carves.runtime.core",
          "packVersion": "{{packVersion}}",
          "packType": "runtime_pack",
          "channel": "{{channel}}",
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
          "releaseNoteRef": "docs/releases/core-{{packVersion}}.md",
          "parentPackVersion": null,
          "approvalRef": "APP-001",
          "supersedes": ["1.2.2"]
        }
        """;
    }

    private static string CreateAttributionJson(string packVersion, string channel)
    {
        return $$"""
        {
          "schemaVersion": "1.0",
          "packId": "carves.runtime.core",
          "packVersion": "{{packVersion}}",
          "channel": "{{channel}}",
          "artifactRef": ".ai/artifacts/packs/core-{{packVersion}}.json",
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
        """;
    }

    private static string SnapshotRuntimePackTruth(string repoRoot)
    {
        var roots = new[]
        {
            Path.Combine(repoRoot, ".ai", "artifacts", "runtime-pack-admission"),
            Path.Combine(repoRoot, ".ai", "artifacts", "runtime-pack-selection"),
            Path.Combine(repoRoot, ".ai", "artifacts", "runtime-pack-selection-audit"),
            Path.Combine(repoRoot, ".ai", "artifacts", "runtime-pack-admission-policy"),
        };

        return string.Join(
            "\n",
            roots
                .Where(Directory.Exists)
                .SelectMany(root => Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                .Select(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal));
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

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError);
}
