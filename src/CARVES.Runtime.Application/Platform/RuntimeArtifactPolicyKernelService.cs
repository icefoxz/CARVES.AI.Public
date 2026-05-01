using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeArtifactPolicyKernelService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;

    public RuntimeArtifactPolicyKernelService(string repoRoot, ControlPlanePaths paths, SystemConfig systemConfig)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.systemConfig = systemConfig;
    }

    public RuntimeKernelBoundarySurface Build()
    {
        var artifactCatalog = new RuntimeArtifactCatalogService(repoRoot, paths, systemConfig).LoadOrBuild(persist: false);
        var exportProfilesPath = ToRepoRelative(RuntimeExportProfileService.GetPolicyPath(paths));
        return new RuntimeKernelBoundarySurface
        {
            SurfaceId = "runtime-artifact-policy-kernel",
            KernelId = "artifact_policy",
            Summary = "Artifact and Policy kernels converge worker/provider/review/safety evidence and runtime policy bundles into one governed gate line rather than parallel review or safety pipelines.",
            TruthRoots =
            [
                Root(
                    "artifact_bundle_truth",
                    "operational_history",
                    "Worker, provider, review, safety, and runtime-failure artifacts remain the bounded evidence bundle for execution audit and review.",
                    [
                        ToRepoRelative(paths.WorkerArtifactsRoot),
                        ToRepoRelative(paths.WorkerExecutionArtifactsRoot),
                        ToRepoRelative(paths.ProviderArtifactsRoot),
                        ToRepoRelative(paths.ReviewArtifactsRoot),
                        ToRepoRelative(paths.SafetyArtifactsRoot),
                        ToRepoRelative(paths.RuntimeFailureArtifactsRoot),
                    ],
                    [
                        "IRuntimeArtifactRepository",
                        "PlannerReviewArtifactFactory",
                        "FailureReportService",
                        "failures --summary",
                    ]),
                Root(
                    "policy_bundle_truth",
                    "canonical_truth",
                    "Policy bundle truth remains explicit and queryable through repo-local config and platform policy roots rather than adapter-local gate rules.",
                    [
                        ToRepoRelative(paths.ConfigRoot),
                        ToRepoRelative(paths.PlatformPoliciesRoot),
                    ],
                    [
                        "RuntimePolicyBundleService",
                        "PlatformGovernanceService",
                        "policy inspect",
                    ]),
                Root(
                    "gate_decision_truth",
                    "governed_mirror",
                    "Gate decisions are expressed through review artifacts, safety outcomes, permission audit, and task writeback instead of a separate gate ledger.",
                    [
                        ToRepoRelative(paths.ReviewArtifactsRoot),
                        ToRepoRelative(paths.SafetyArtifactsRoot),
                        ToRepoRelative(paths.PlatformPermissionAuditRuntimeFile),
                    ],
                    [
                        "ReviewWritebackService",
                        "SafetyService",
                        "ApprovalPolicyEngine",
                        "api worker-permission-audit",
                    ]),
                Root(
                    "artifact_catalog_projection",
                    "projection",
                    "Artifact catalog remains the summary-first projection explaining which evidence roots are canonical, governed, operational, live, or residue.",
                    [
                        ToRepoRelative(RuntimeArtifactCatalogService.GetCatalogPath(paths)),
                    ],
                    [
                        "RuntimeArtifactCatalogService",
                        "audit sustainability",
                    ]),
            ],
            BoundaryRules =
            [
                Rule(
                    "one_evidence_path_for_execution_and_review",
                    "Execution results, review outcomes, and runtime failures must remain explainable through one artifact bundle instead of separate review or safety subsystems.",
                    [
                        "IRuntimeArtifactRepository",
                        "ReviewWritebackService",
                        "FailureReportService",
                        "ExecutionContractSurfaceService",
                    ],
                    [
                        "second_review_pipeline",
                        "shadow_safety_cache",
                    ]),
                Rule(
                    "policy_bundle_governs_gate_decisions",
                    "Approval, worker selection, and runtime gate decisions must resolve from the existing policy bundle and governance services rather than provider-local overrides.",
                    [
                        "RuntimePolicyBundleService",
                        "ApprovalPolicyEngine",
                        "WorkerSelectionPolicyService",
                        "PlatformGovernanceService",
                    ],
                    [
                        "adapter_local_gate_rules",
                        "provider_specific_policy_truth",
                    ]),
                Rule(
                    "gate_writeback_returns_to_task_truth",
                    "Artifacts and gate verdicts stay evidence unless and until review writeback updates canonical task truth.",
                    [
                        "ReviewWritebackService",
                        "OperatorSurfaceService.ReviewTask",
                        "TaskGraphService",
                    ],
                    [
                        "artifact_only_completion",
                        "gate_decision_without_task_transition",
                    ]),
            ],
            GovernedReadPaths =
            [
                ReadPath(
                    "execution_contract_surface",
                    "inspect execution-contract-surface",
                    "Execution-contract inspection remains the summary read path over packet, result, and pack contracts that feed the evidence line.",
                    [
                        "ExecutionContractSurfaceService",
                        "docs/contracts/",
                    ]),
                ReadPath(
                    "policy_bundle_surface",
                    "policy inspect",
                    "Policy inspection remains the governed read path for delegation, approval, worker selection, and trust-profile rules.",
                    [
                        "RuntimePolicyBundleService",
                        ToRepoRelative(paths.PlatformPoliciesRoot),
                    ]),
                ReadPath(
                    "runtime_export_profiles_surface",
                    "inspect runtime-export-profiles",
                    "Export profile inspection is the governed read path for source-review, proof-bundle, and runtime-state packaging rules over the existing artifact catalog.",
                    [
                        "RuntimeExportProfileService",
                        exportProfilesPath,
                        ToRepoRelative(RuntimeArtifactCatalogService.GetCatalogPath(paths)),
                    ]),
                ReadPath(
                    "artifact_failure_surface",
                    "failures [--task <task-id>] [--summary]",
                    "Failure and runtime-failure reads stay attached to the same artifact/evidence path used by review and audit.",
                    [
                        "FailureReportService",
                        ToRepoRelative(paths.RuntimeFailureArtifactsRoot),
                    ]),
            ],
            SuccessCriteria =
            [
                "Artifact and policy boundaries are runtime-queryable through one surface.",
                "Review, safety, approval, and failure evidence all point back to one governed artifact line.",
                "The boundary makes policy bundle ownership explicit without opening another review or safety pipeline.",
                "Export profile rules keep bulky runtime-local families on explicit full, manifest_only, or pointer_only packaging modes instead of implicit live-clutter exports.",
            ],
            StopConditions =
            [
                "Do not introduce a second review queue or a provider-specific safety truth line.",
                "Do not move gate policy into worker adapter configuration or prompt text.",
                "Do not bypass review writeback by treating artifacts as self-sufficient completion truth.",
            ],
            Notes =
            [
                $"Artifact catalog schema {artifactCatalog.SchemaVersion} remains the summary projection over these roots; this card does not replace it.",
            ],
        };
    }

    private static RuntimeKernelTruthRootDescriptor Root(
        string rootId,
        string classification,
        string summary,
        string[] pathRefs,
        string[] truthRefs,
        string[]? notes = null)
    {
        return new RuntimeKernelTruthRootDescriptor
        {
            RootId = rootId,
            Classification = classification,
            Summary = summary,
            PathRefs = pathRefs,
            TruthRefs = truthRefs,
            Notes = notes ?? [],
        };
    }

    private static RuntimeKernelBoundaryRule Rule(
        string ruleId,
        string summary,
        string[] allowedRefs,
        string[] forbiddenRefs,
        string[]? notes = null)
    {
        return new RuntimeKernelBoundaryRule
        {
            RuleId = ruleId,
            Summary = summary,
            AllowedRefs = allowedRefs,
            ForbiddenRefs = forbiddenRefs,
            Notes = notes ?? [],
        };
    }

    private static RuntimeKernelGovernedReadPath ReadPath(
        string pathId,
        string entryPoint,
        string summary,
        string[] truthRefs,
        string[]? notes = null)
    {
        return new RuntimeKernelGovernedReadPath
        {
            PathId = pathId,
            EntryPoint = entryPoint,
            Summary = summary,
            TruthRefs = truthRefs,
            Notes = notes ?? [],
        };
    }

    private string ToRepoRelative(string path)
    {
        return Path.GetRelativePath(repoRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    }
}
