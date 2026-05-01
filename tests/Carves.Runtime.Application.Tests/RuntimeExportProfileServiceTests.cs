using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeExportProfileServiceTests
{
    [Fact]
    public void BuildSurface_ResolvesDefaultProfilesAndPointerFirstFamilies()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new RuntimeExportProfileService(workspace.RootPath, workspace.Paths, TestSystemConfigFactory.Create());

        var surface = service.BuildSurface();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-export-profiles", surface.SurfaceId);
        Assert.Equal(3, surface.Profiles.Count);

        var sourceReview = Assert.Single(surface.Profiles, profile => profile.ProfileId == "source_review");
        Assert.Contains("src/", sourceReview.IncludedPathRoots);
        Assert.Contains(sourceReview.IncludedFamilies, family => family.FamilyId == "task_truth" && family.PackagingMode == RuntimeExportPackagingMode.Full);
        Assert.True(sourceReview.Discipline.IsValid);
        Assert.Contains("task_truth", sourceReview.Discipline.FullFamilyIds);

        var proofBundle = Assert.Single(surface.Profiles, profile => profile.ProfileId == "proof_bundle");
        Assert.Contains(proofBundle.IncludedFamilies, family => family.FamilyId == "worker_execution_artifact_history" && family.PackagingMode == RuntimeExportPackagingMode.PointerOnly);
        Assert.Contains("worker_execution_artifact_history", proofBundle.Discipline.PointerOnlyFamilyIds);

        var runtimeState = Assert.Single(surface.Profiles, profile => profile.ProfileId == "runtime_state_package");
        Assert.Contains(runtimeState.IncludedFamilies, family => family.FamilyId == "runtime_live_state" && family.PackagingMode == RuntimeExportPackagingMode.Full);
        Assert.Contains(runtimeState.IncludedFamilies, family => family.FamilyId == "execution_packet_mirror" && family.PackagingMode == RuntimeExportPackagingMode.PointerOnly);
        Assert.Contains("platform_provider_live_state", runtimeState.Discipline.ManifestOnlyFamilyIds);
    }

    [Fact]
    public void Validate_RejectsUnknownArtifactFamily()
    {
        using var workspace = new TemporaryWorkspace();
        var policyPath = RuntimeExportProfileService.GetPolicyPath(workspace.Paths);
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);
        File.WriteAllText(policyPath, """
{
  "schema_version": "runtime-export-profiles-policy.v1",
  "policy_id": "runtime-export-profiles",
  "profiles": [
    {
      "profile_id": "broken",
      "display_name": "Broken",
      "summary": "Broken profile",
      "family_rules": [
        {
          "family_id": "missing_family",
          "packaging_mode": "full",
          "reason": "bad ref"
        }
      ],
      "included_path_roots": [],
      "excluded_family_ids": [],
      "excluded_path_roots": [],
      "notes": []
    }
  ]
}
""");

        var service = new RuntimeExportProfileService(workspace.RootPath, workspace.Paths, TestSystemConfigFactory.Create());

        var validation = service.Validate();

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("missing_family", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsDriftedProofBundlePackagingDiscipline()
    {
        using var workspace = new TemporaryWorkspace();
        var policyPath = RuntimeExportProfileService.GetPolicyPath(workspace.Paths);
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);
        File.WriteAllText(policyPath, """
{
  "schema_version": "runtime-export-profiles-policy.v1",
  "policy_id": "runtime-export-profiles",
  "profiles": [
    {
      "profile_id": "source_review",
      "display_name": "Source review pack",
      "summary": "ok",
      "family_rules": [
        { "family_id": "task_truth", "packaging_mode": "full", "reason": "ok" },
        { "family_id": "governed_markdown_mirror", "packaging_mode": "full", "reason": "ok" },
        { "family_id": "platform_definition_truth", "packaging_mode": "manifest_only", "reason": "ok" }
      ],
      "included_path_roots": ["README.md", "AGENTS.md", "src/", "tests/", "docs/"],
      "excluded_family_ids": [
        "execution_memory_truth",
        "planning_runtime_history",
        "execution_surface_history",
        "validation_trace_history",
        "execution_run_detail_history",
        "execution_run_report_history",
        "runtime_failure_detail_history",
        "worker_execution_artifact_history",
        "runtime_live_state",
        "platform_live_state",
        "platform_provider_live_state",
        "ephemeral_runtime_residue"
      ],
      "excluded_path_roots": [],
      "notes": []
    },
    {
      "profile_id": "proof_bundle",
      "display_name": "Proof bundle",
      "summary": "drifted",
      "family_rules": [
        { "family_id": "task_truth", "packaging_mode": "full", "reason": "ok" },
        { "family_id": "execution_memory_truth", "packaging_mode": "full", "reason": "ok" },
        { "family_id": "governed_markdown_mirror", "packaging_mode": "full", "reason": "ok" },
        { "family_id": "validation_suite_truth", "packaging_mode": "full", "reason": "ok" },
        { "family_id": "worker_execution_artifact_history", "packaging_mode": "full", "reason": "bad drift" },
        { "family_id": "runtime_failure_detail_history", "packaging_mode": "pointer_only", "reason": "ok" },
        { "family_id": "runtime_pack_policy_audit_evidence", "packaging_mode": "pointer_only", "reason": "ok" },
        { "family_id": "runtime_pack_selection_audit_evidence", "packaging_mode": "pointer_only", "reason": "ok" }
      ],
      "included_path_roots": ["docs/runtime/", "docs/contracts/"],
      "excluded_family_ids": [
        "runtime_live_state",
        "platform_live_state",
        "platform_provider_live_state",
        "ephemeral_runtime_residue"
      ],
      "excluded_path_roots": [],
      "notes": []
    },
    {
      "profile_id": "runtime_state_package",
      "display_name": "Runtime-state package",
      "summary": "ok",
      "family_rules": [
        { "family_id": "routing_truth", "packaging_mode": "full", "reason": "ok" },
        { "family_id": "platform_definition_truth", "packaging_mode": "full", "reason": "ok" },
        { "family_id": "runtime_live_state", "packaging_mode": "full", "reason": "ok" },
        { "family_id": "platform_live_state", "packaging_mode": "full", "reason": "ok" },
        { "family_id": "platform_provider_live_state", "packaging_mode": "manifest_only", "reason": "ok" },
        { "family_id": "governed_markdown_mirror", "packaging_mode": "manifest_only", "reason": "ok" },
        { "family_id": "context_pack_projection", "packaging_mode": "pointer_only", "reason": "ok" },
        { "family_id": "execution_packet_mirror", "packaging_mode": "pointer_only", "reason": "ok" }
      ],
      "included_path_roots": [],
      "excluded_family_ids": [
        "task_truth",
        "execution_memory_truth",
        "worker_execution_artifact_history",
        "runtime_failure_detail_history",
        "planning_runtime_history",
        "execution_surface_history",
        "validation_trace_history",
        "execution_run_detail_history",
        "execution_run_report_history",
        "ephemeral_runtime_residue"
      ],
      "excluded_path_roots": [],
      "notes": []
    }
  ]
}
""");

        var service = new RuntimeExportProfileService(workspace.RootPath, workspace.Paths, TestSystemConfigFactory.Create());

        var validation = service.Validate();
        var surface = service.BuildSurface("proof_bundle");

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("worker_execution_artifact_history", StringComparison.Ordinal));
        var profile = Assert.Single(surface.Profiles);
        Assert.False(profile.Discipline.IsValid);
        Assert.Contains(profile.Discipline.Errors, error => error.Contains("worker_execution_artifact_history", StringComparison.Ordinal));
    }
}
