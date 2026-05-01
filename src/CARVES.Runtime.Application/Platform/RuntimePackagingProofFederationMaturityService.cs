using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackagingProofFederationMaturityService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;
    private readonly RoleGovernanceRuntimePolicy roleGovernancePolicy;
    private readonly IRuntimeArtifactRepository artifactRepository;

    public RuntimePackagingProofFederationMaturityService(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        RoleGovernanceRuntimePolicy roleGovernancePolicy,
        IRuntimeArtifactRepository artifactRepository)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.paths = paths;
        this.systemConfig = systemConfig;
        this.roleGovernancePolicy = roleGovernancePolicy;
        this.artifactRepository = artifactRepository;
    }

    public RuntimePackagingProofFederationMaturitySurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var boundaryDocumentPath = ToRepoRelative(Path.Combine(repoRoot, "docs", "runtime", "runtime-packaging-proof-federation-maturity.md"));
        var boundaryDocumentFullPath = Path.Combine(repoRoot, boundaryDocumentPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(boundaryDocumentFullPath))
        {
            errors.Add($"Boundary document '{boundaryDocumentPath}' is missing.");
        }

        var exportProfiles = new RuntimeExportProfileService(repoRoot, paths, systemConfig).BuildSurface();
        errors.AddRange(exportProfiles.Errors.Select(error => $"Export profiles: {error}"));
        warnings.AddRange(exportProfiles.Warnings.Select(warning => $"Export profiles: {warning}"));

        var proof = new RuntimeControlledGovernanceProofService(repoRoot, paths, systemConfig, roleGovernancePolicy).Build();
        errors.AddRange(proof.Errors.Select(error => $"Controlled governance proof: {error}"));
        warnings.AddRange(proof.Warnings.Select(warning => $"Controlled governance proof: {warning}"));

        var packBoundary = new RuntimePackDistributionBoundaryService(artifactRepository).BuildSurface();

        return new RuntimePackagingProofFederationMaturitySurface
        {
            BoundaryDocumentPath = boundaryDocumentPath,
            ExportProfilePolicyPath = ToRepoRelative(RuntimeExportProfileService.GetPolicyPath(paths)),
            ControlledGovernanceBoundaryPath = proof.BoundaryDocumentPath,
            ValidationLabHandoffBoundaryPath = proof.HandoffBoundaryDocumentPath,
            PackDistributionSurfaceId = packBoundary.SurfaceId,
            Summary = "Packaging, proof, and bounded federation maturity remain read-only Runtime projections over export-profile policy, controlled-governance proof, local pack-distribution limits, and repo-role freeze truth.",
            IsValid = errors.Count == 0 && exportProfiles.IsValid && proof.IsValid,
            Errors = errors,
            Warnings = warnings,
            PackagingProfiles = exportProfiles.Profiles
                .Select(profile => new RuntimePackagingMaturityProfileSurface
                {
                    ProfileId = profile.ProfileId,
                    DisplayName = profile.DisplayName,
                    DisciplineValid = profile.Discipline.IsValid,
                    FullFamilyIds = profile.Discipline.FullFamilyIds,
                    ManifestOnlyFamilyIds = profile.Discipline.ManifestOnlyFamilyIds,
                    PointerOnlyFamilyIds = profile.Discipline.PointerOnlyFamilyIds,
                    IncludedPathRoots = profile.IncludedPathRoots,
                    Notes = profile.Notes,
                })
                .ToArray(),
            ProofLanes = proof.Lanes
                .Select(lane => new RuntimePackagingMaturityProofLaneSurface
                {
                    LaneId = lane.LaneId,
                    DisplayName = lane.DisplayName,
                    TruthLevel = lane.TruthLevel,
                    SourceHandoffLaneIds = lane.SourceHandoffLaneIds,
                    RuntimeEvidencePaths = lane.RuntimeEvidencePaths,
                    NonClaims = lane.NonClaims,
                })
                .ToArray(),
            FederationLanes = BuildFederationLanes(proof),
            ClosedCapabilities = packBoundary.ClosedFutureCapabilities,
        };
    }

    private RuntimeBoundedFederationLaneSurface[] BuildFederationLanes(RuntimeControlledGovernanceProofSurface proof)
    {
        return
        [
            new RuntimeBoundedFederationLaneSurface
            {
                LaneId = "runtime_operating_repo",
                DisplayName = "Runtime operating repo",
                Status = "authoritative_write_path",
                Summary = "CARVES.Runtime remains the current operating repo and truth owner for task, execution, policy, and operator surfaces.",
                TruthRefs =
                [
                    ".ai/tasks/",
                    ".ai/memory/execution/",
                    ".carves-platform/policies/",
                    "review-task <task-id> <verdict> <reason...>",
                    "approve-review <task-id> <reason...>",
                    "sync-state",
                ],
                NonClaims =
                [
                    "Runtime does not externalize its write gates into sibling repos.",
                ],
            },
            new RuntimeBoundedFederationLaneSurface
            {
                LaneId = "kernel_sibling_boundary",
                DisplayName = "Kernel sibling boundary",
                Status = "bounded_sibling_repo",
                Summary = "CARVES.Kernel remains the sibling bounded-portability and kernel-positioning line rather than a Runtime write extension.",
                TruthRefs =
                [
                    "docs/implementation/RUNTIME_TO_KERNEL_MIGRATION_BOUNDARY.md",
                    "docs/runtime/runtime-governance-convergence-program.md",
                ],
                NonClaims =
                [
                    "Wave 7 does not pull kernel-only proof back into Runtime.",
                ],
            },
            new RuntimeBoundedFederationLaneSurface
            {
                LaneId = "operator_sibling_boundary",
                DisplayName = "Operator sibling boundary",
                Status = "bounded_sibling_repo",
                Summary = "CARVES.Operator remains the sibling consumer-project line and does not become a second Runtime truth owner.",
                TruthRefs =
                [
                    "README.md",
                    "AGENTS.md",
                    "docs/runtime/runtime-governance-convergence-program.md",
                ],
                NonClaims =
                [
                    "Wave 7 does not pull consumer-project entry back into Runtime.",
                ],
            },
            new RuntimeBoundedFederationLaneSurface
            {
                LaneId = "validationlab_sibling_boundary",
                DisplayName = "ValidationLab sibling boundary",
                Status = "downstream_validation_boundary",
                Summary = "CARVES.ValidationLab.ApprovalRecovery remains the downstream validation asset repo that consumes bounded proof lanes without becoming a Runtime write path.",
                TruthRefs =
                [
                    proof.HandoffBoundaryDocumentPath,
                    proof.BoundaryDocumentPath,
                    "inspect runtime-validationlab-proof-handoff",
                    "inspect runtime-controlled-governance-proof",
                ],
                NonClaims =
                [
                    "ValidationLab does not become a Runtime approval, recovery, or packaging authority.",
                ],
            },
            new RuntimeBoundedFederationLaneSurface
            {
                LaneId = "projection_only_surfaces",
                DisplayName = "Projection-only surfaces",
                Status = "projection_only",
                Summary = "Projection-only surfaces stay downstream, read-only, and bounded to existing Runtime truth instead of becoming new write paths.",
                TruthRefs =
                [
                    "AGENTS.md",
                    ".codex/config.toml",
                    ".codex/rules/",
                    ".codex/skills/",
                ],
                NonClaims =
                [
                    "Projection-only surfaces do not become packaging, proof, or federation truth owners.",
                ],
            },
        ];
    }

    private string ToRepoRelative(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Path.GetRelativePath(repoRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
