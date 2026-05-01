using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeValidationLabProofHandoffService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;
    private readonly RoleGovernanceRuntimePolicy roleGovernancePolicy;

    public RuntimeValidationLabProofHandoffService(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        RoleGovernanceRuntimePolicy roleGovernancePolicy)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.paths = paths;
        this.systemConfig = systemConfig;
        this.roleGovernancePolicy = roleGovernancePolicy;
    }

    public RuntimeValidationLabProofHandoffSurface Build(string? laneId = null)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var boundaryDocumentPath = ToRepoRelative(Path.Combine(repoRoot, "docs", "runtime", "runtime-validationlab-proof-handoff-boundary.md"));
        var boundaryDocumentFullPath = Path.Combine(repoRoot, boundaryDocumentPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(boundaryDocumentFullPath))
        {
            errors.Add($"Boundary document '{boundaryDocumentPath}' is missing.");
        }

        var artifactCatalog = new RuntimeArtifactCatalogService(repoRoot, paths, systemConfig).LoadOrBuild(persist: false);
        var familyIndex = artifactCatalog.Families.ToDictionary(family => family.FamilyId, StringComparer.Ordinal);
        var lanes = BuildLanes(familyIndex, errors, warnings);
        if (!string.IsNullOrWhiteSpace(laneId))
        {
            lanes = lanes
                .Where(lane => string.Equals(lane.LaneId, laneId, StringComparison.Ordinal))
                .ToArray();
            if (lanes.Length == 0)
            {
                throw new InvalidOperationException($"Runtime ValidationLab proof handoff lane '{laneId}' was not found.");
            }
        }

        if (!roleGovernancePolicy.ValidationLabFollowOnLanes.Contains("approval_recovery", StringComparer.Ordinal))
        {
            warnings.Add("Role governance policy does not declare the 'approval_recovery' ValidationLab follow-on lane.");
        }

        if (!roleGovernancePolicy.ValidationLabFollowOnLanes.Contains("controlled_mode_governance", StringComparer.Ordinal))
        {
            warnings.Add("Role governance policy does not declare the 'controlled_mode_governance' ValidationLab follow-on lane.");
        }

        return new RuntimeValidationLabProofHandoffSurface
        {
            BoundaryDocumentPath = boundaryDocumentPath,
            ArtifactCatalogSchemaVersion = artifactCatalog.SchemaVersion,
            Summary = "ValidationLab handoff remains a bounded Runtime read path over existing task, review, approval, failure, and recovery evidence rather than a second proof control plane.",
            ControlledModeDefault = roleGovernancePolicy.ControlledModeDefault,
            ValidationLabFollowOnLanes = roleGovernancePolicy.ValidationLabFollowOnLanes,
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Lanes = lanes,
        };
    }

    private RuntimeValidationLabProofLaneSurface[] BuildLanes(
        IReadOnlyDictionary<string, RuntimeArtifactFamilyPolicy> familyIndex,
        List<string> errors,
        List<string> warnings)
    {
        return
        [
            BuildLane(
                "approval_proof",
                "Approval proof",
                "Approval proof packages bounded task, review, merge-candidate, and permission-decision evidence so ValidationLab can consume governed approval flow proof without rewriting Runtime truth.",
                "Runtime stays authoritative for task/review/approval evidence and role-governed approval separation.",
                "ValidationLab owns lab drills, scenario execution, and external proof choreography built from the bounded Runtime handoff.",
                [
                    "task inspect <task-id>",
                    "inspect execution-memory <task-id>",
                    "worker audit [--task-id <task-id>]",
                    "approve-review <task-id> <reason...>",
                    "report approvals",
                ],
                [
                    ResolveFamily("task_truth", RuntimeExportPackagingMode.Full, "Canonical task/card lineage is part of proof-ready approval context.", familyIndex, errors),
                    ResolveFamily("execution_memory_truth", RuntimeExportPackagingMode.Full, "Execution outcome memory stays governed truth for approval proof bundles.", familyIndex, errors),
                    ResolveFamily("governed_markdown_mirror", RuntimeExportPackagingMode.ManifestOnly, "Governed mirrors remain summary context for bounded human review.", familyIndex, errors),
                    ResolveFamily("worker_execution_artifact_history", RuntimeExportPackagingMode.PointerOnly, "Worker, permission, review, and merge evidence remain pointer-first to avoid bloating every proof handoff.", familyIndex, errors),
                ],
                [
                    ToRepoRelative(paths.ReviewArtifactsRoot),
                    ToRepoRelative(paths.MergeArtifactsRoot),
                    ToRepoRelative(paths.WorkerPermissionArtifactsRoot),
                    ToRepoRelative(paths.PlatformPermissionAuditRuntimeFile),
                    "docs/runtime/approval-flow-runtime-proof.md",
                    "docs/runtime/runtime-validationlab-proof-handoff-boundary.md",
                ],
                [
                    "approval-recovery drills",
                    "resettable approval scenarios",
                    "lab-only evaluation summaries",
                ],
                [
                    "ValidationLab does not approve reviews or change task truth.",
                    "Runtime does not persist lab scenario outputs as Runtime truth.",
                ],
                warnings),
            BuildLane(
                "recovery_proof",
                "Refusal and recovery proof",
                "Recovery proof packages refusal, interruption, failure, and delegated-recovery evidence so ValidationLab can consume bounded recovery proof without introducing a second recovery truth store.",
                "Runtime stays authoritative for refusal decisions, recovery classification, failure detail, and delegated-recovery ledgers.",
                "ValidationLab owns resettable refusal/interruption drills and scenario assets derived from Runtime evidence pointers.",
                [
                    "task inspect <task-id>",
                    "failures [--task <task-id>]",
                    "worker audit [--task-id <task-id>]",
                    "inspect archive-readiness",
                    "inspect execution-run-exceptions",
                ],
                [
                    ResolveFamily("task_truth", RuntimeExportPackagingMode.Full, "Task truth preserves the blocked/recovery lineage for refusal and interruption proof.", familyIndex, errors),
                    ResolveFamily("execution_memory_truth", RuntimeExportPackagingMode.Full, "Execution memory remains the governed memory spine for recovery proof.", familyIndex, errors),
                    ResolveFamily("governed_markdown_mirror", RuntimeExportPackagingMode.ManifestOnly, "Mirrors remain summary context for human-readable recovery posture.", familyIndex, errors),
                    ResolveFamily("runtime_live_state", RuntimeExportPackagingMode.ManifestOnly, "Live session/worktree posture remains manifest-first for bounded recovery context.", familyIndex, errors),
                    ResolveFamily("runtime_failure_detail_history", RuntimeExportPackagingMode.PointerOnly, "Failure detail stays pointer-first and compactable instead of becoming default package payload.", familyIndex, errors),
                    ResolveFamily("platform_runtime_ledger_history", RuntimeExportPackagingMode.PointerOnly, "Delegated lifecycle and recovery ledgers remain bounded coordination evidence, not a new proof truth root.", familyIndex, errors),
                    ResolveFamily("worker_execution_artifact_history", RuntimeExportPackagingMode.PointerOnly, "Worker and review artifacts remain pointer-first supporting evidence for recovery proof.", familyIndex, errors),
                ],
                [
                    ToRepoRelative(paths.FailuresRoot),
                    ToRepoRelative(paths.RuntimeFailureArtifactsRoot),
                    ToRepoRelative(paths.PlatformDelegatedRunLifecycleLiveStateFile),
                    ToRepoRelative(paths.PlatformDelegatedRunRecoveryLedgerLiveStateFile),
                    "docs/runtime/expired-delegated-run-recovery-classification.md",
                    "docs/runtime/runtime-validationlab-proof-handoff-boundary.md",
                ],
                [
                    "interruption and refusal drills",
                    "recovery scenario traces",
                    "lab-only comparative recovery evaluations",
                ],
                [
                    "ValidationLab does not become a recovery authority or truth owner.",
                    "Runtime does not duplicate delegated recovery truth into a second ledger for convenience.",
                ],
                warnings),
        ];
    }

    private RuntimeValidationLabProofLaneSurface BuildLane(
        string laneId,
        string displayName,
        string summary,
        string runtimeAuthoritySummary,
        string validationLabAuthoritySummary,
        IReadOnlyList<string> runtimeCommands,
        IReadOnlyList<RuntimeValidationLabProofFamilySurface> truthFamilies,
        IReadOnlyList<string> runtimeEvidencePaths,
        IReadOnlyList<string> validationLabOwnedAssets,
        IReadOnlyList<string> nonClaims,
        List<string> warnings)
    {
        var discipline = BuildDiscipline(truthFamilies);
        if (runtimeEvidencePaths.Count == 0)
        {
            warnings.Add($"ValidationLab proof handoff lane '{laneId}' does not expose any Runtime evidence paths.");
        }

        return new RuntimeValidationLabProofLaneSurface
        {
            LaneId = laneId,
            DisplayName = displayName,
            Summary = summary,
            RuntimeAuthoritySummary = runtimeAuthoritySummary,
            ValidationLabAuthoritySummary = validationLabAuthoritySummary,
            RuntimeCommands = runtimeCommands,
            RuntimeTruthFamilies = truthFamilies,
            RuntimeEvidencePaths = runtimeEvidencePaths,
            ValidationLabOwnedAssets = validationLabOwnedAssets,
            NonClaims = nonClaims,
            Discipline = discipline,
        };
    }

    private static RuntimeValidationLabProofDisciplineSurface BuildDiscipline(IReadOnlyList<RuntimeValidationLabProofFamilySurface> truthFamilies)
    {
        var fullFamilyIds = truthFamilies
            .Where(family => family.PackagingMode == RuntimeExportPackagingMode.Full)
            .Select(family => family.FamilyId)
            .ToArray();
        var manifestOnlyFamilyIds = truthFamilies
            .Where(family => family.PackagingMode == RuntimeExportPackagingMode.ManifestOnly)
            .Select(family => family.FamilyId)
            .ToArray();
        var pointerOnlyFamilyIds = truthFamilies
            .Where(family => family.PackagingMode == RuntimeExportPackagingMode.PointerOnly)
            .Select(family => family.FamilyId)
            .ToArray();

        return new RuntimeValidationLabProofDisciplineSurface
        {
            IsValid = true,
            FullFamilyCount = fullFamilyIds.Length,
            ManifestOnlyFamilyCount = manifestOnlyFamilyIds.Length,
            PointerOnlyFamilyCount = pointerOnlyFamilyIds.Length,
            FullFamilyIds = fullFamilyIds,
            ManifestOnlyFamilyIds = manifestOnlyFamilyIds,
            PointerOnlyFamilyIds = pointerOnlyFamilyIds,
        };
    }

    private RuntimeValidationLabProofFamilySurface ResolveFamily(
        string familyId,
        RuntimeExportPackagingMode packagingMode,
        string reason,
        IReadOnlyDictionary<string, RuntimeArtifactFamilyPolicy> familyIndex,
        List<string> errors)
    {
        if (!familyIndex.TryGetValue(familyId, out var family))
        {
            errors.Add($"ValidationLab proof handoff references unknown artifact family '{familyId}'.");
            return new RuntimeValidationLabProofFamilySurface
            {
                FamilyId = familyId,
                PackagingMode = packagingMode,
                Reason = reason,
            };
        }

        return new RuntimeValidationLabProofFamilySurface
        {
            FamilyId = family.FamilyId,
            DisplayName = family.DisplayName,
            ArtifactClass = family.ArtifactClass,
            PackagingMode = packagingMode,
            Roots = family.Roots,
            Summary = family.Summary,
            Reason = reason,
        };
    }

    private string ToRepoRelative(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return Path.GetRelativePath(repoRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }
}
