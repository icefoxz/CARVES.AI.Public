using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeRepoAuthoredGateLoopService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;

    public RuntimeRepoAuthoredGateLoopService(string repoRoot, ControlPlanePaths paths)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
    }

    public RuntimeRepoAuthoredGateLoopSurface Build()
    {
        var policy = LoadOrCreatePolicy();
        return new RuntimeRepoAuthoredGateLoopSurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformRepoAuthoredGateLoopFile),
            Policy = policy,
        };
    }

    public RepoAuthoredGateLoopPolicy LoadPolicy()
    {
        return LoadOrCreatePolicy();
    }

    private RepoAuthoredGateLoopPolicy LoadOrCreatePolicy()
    {
        if (File.Exists(paths.PlatformRepoAuthoredGateLoopFile))
        {
            var persisted = JsonSerializer.Deserialize<RepoAuthoredGateLoopPolicy>(
                                File.ReadAllText(paths.PlatformRepoAuthoredGateLoopFile),
                                JsonOptions)
                            ?? BuildDefaultPolicy();
            if (NeedsRefresh(persisted))
            {
                var refreshed = BuildDefaultPolicy();
                File.WriteAllText(paths.PlatformRepoAuthoredGateLoopFile, JsonSerializer.Serialize(refreshed, JsonOptions));
                return refreshed;
            }

            return persisted;
        }

        var policy = BuildDefaultPolicy();
        Directory.CreateDirectory(paths.PlatformPoliciesRoot);
        File.WriteAllText(paths.PlatformRepoAuthoredGateLoopFile, JsonSerializer.Serialize(policy, JsonOptions));
        return policy;
    }

    private RepoAuthoredGateLoopPolicy BuildDefaultPolicy()
    {
        return new RepoAuthoredGateLoopPolicy
        {
            Summary = "Machine-readable repo-authored review and gate loop boundary derived from Continue checks-as-code, independent gate execution, remediation suggestions, and PR/branch/local workflow projections without replacing CARVES policy truth or Host governance.",
            ExtractionBoundary = new RepoAuthoredGateLoopExtractionBoundary
            {
                DirectAbsorptions =
                [
                    "repo-authored review units that can be versioned with repository truth",
                    "independent gate execution units instead of one monolithic review pass",
                    "suggested-diff remediation with explicit accept or reject semantics",
                    "workflow projection across PR, branch, and local operator contexts",
                ],
                TranslatedIntoCarves =
                [
                    "policy truth under .carves-platform/policies/repo-authored-gate-loop.json",
                    "runtime-repo-authored-gate-loop inspect/api surfaces for operators and agents",
                    "review and gate result surfaces attached to existing task, review, and artifact truth",
                    "projection guidance that keeps CI and PR as one workflow projection rather than the sole worldview",
                ],
                RejectedAnchors =
                [
                    "markdown prompt files as the final truth owner",
                    "GitHub-only or PR-only workflow ownership",
                    "auto-apply remediation as an implicit side effect of failing review output",
                ],
            },
            DefinitionKernel = new RepoAuthoredReviewDefinitionKernel
            {
                Summary = "Repo-authored review definitions are machine-readable first and may project into markdown prompt files as one downstream packaging path.",
                MachineTruthPath = RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformRepoAuthoredGateLoopFile),
                ProjectionPaths =
                [
                    ".continue/checks/",
                    ".agents/checks/",
                ],
                DiscoveryModes =
                [
                    "policy truth discovery through inspect runtime-repo-authored-gate-loop",
                    "repo-local projection discovery through check directories when present",
                ],
                UnitFamilies =
                [
                    "review_definition_units",
                    "gate_execution_units",
                    "remediation_suggestion_units",
                ],
                NonGoals =
                [
                    "markdown prompt body as final truth owner",
                    "single central prompt stack as the only definition source",
                ],
            },
            GateExecution = new RepoAuthoredGateExecutionSurface
            {
                Summary = "Review and gate runs remain independent execution units with explicit result states and governed remediation artifacts.",
                ExecutionUnitKinds =
                [
                    "review_unit",
                    "gate_unit",
                    "remediation_suggestion",
                ],
                ResultStates =
                [
                    "pass",
                    "fail",
                    "pending",
                    "accepted",
                    "rejected",
                ],
                ArtifactFamilies =
                [
                    ".ai/artifacts/reviews/",
                    ".ai/artifacts/safety/",
                    ".ai/artifacts/merge-candidates/",
                    ".ai/artifacts/provider/",
                ],
                ReviewBoundaries =
                [
                    "gate results stay attached to existing review and approval surfaces",
                    "remediation suggestions remain governed artifacts until operator accept or reject",
                ],
                NonGoals =
                [
                    "second review control plane",
                    "provider-specific output as sole review truth",
                ],
            },
            WorkflowProjections =
            [
                new RepoAuthoredWorkflowProjection
                {
                    ProjectionId = "pull_request_ci_projection",
                    Context = "pull_request",
                    Summary = "PR and CI workflow is one projection path where repo-authored review units run against diff-shaped input and surface status results.",
                    TriggerSurfaces =
                    [
                        "review-task",
                        "approve-review",
                        "runtime-repo-authored-gate-loop",
                    ],
                    ResultSurfaces =
                    [
                        ".ai/artifacts/reviews/",
                        ".ai/artifacts/merge-candidates/",
                    ],
                    Notes =
                    [
                        "GitHub PR and CI are projections, not sole ownership.",
                    ],
                },
                new RepoAuthoredWorkflowProjection
                {
                    ProjectionId = "branch_review_projection",
                    Context = "branch",
                    Summary = "Branch-scoped review runs remain valid even when no PR exists, so gate units are not bound to one platform workflow.",
                    TriggerSurfaces =
                    [
                        "task inspect <task-id>",
                        "review-task",
                        "runtime inspect <repo-id>",
                    ],
                    ResultSurfaces =
                    [
                        ".ai/artifacts/reviews/",
                        ".ai/artifacts/safety/",
                    ],
                },
                new RepoAuthoredWorkflowProjection
                {
                    ProjectionId = "local_operator_projection",
                    Context = "local_operator",
                    Summary = "Local operator workflows may inspect gate definitions, run bounded review units, and accept or reject remediation without a PR host.",
                    TriggerSurfaces =
                    [
                        "inspect runtime-repo-authored-gate-loop",
                        "review-task",
                        "approve-review",
                    ],
                    ResultSurfaces =
                    [
                        ".ai/artifacts/reviews/",
                        ".ai/artifacts/merge-candidates/",
                        ".ai/tasks/nodes/",
                    ],
                },
            ],
            ConcernFamilies =
            [
                new RepoAuthoredGateConcernFamily
                {
                    FamilyId = "review_definition_kernel",
                    Layer = "definition_kernel",
                    Summary = "Repo-authored review definitions are discovered from machine-readable policy truth and may project into markdown prompt files when needed.",
                    CurrentStatus = "ready_for_bounded_followup",
                    SourceProjects = ["Continue"],
                    DirectAbsorptions =
                    [
                        "repo-authored checks-as-code concept",
                        "multiple independent review definition units",
                        "projection-friendly check packaging",
                    ],
                    TranslationTargets =
                    [
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformRepoAuthoredGateLoopFile),
                        ".continue/checks/",
                        ".agents/checks/",
                    ],
                    GovernedEntryPoints =
                    [
                        "inspect runtime-repo-authored-gate-loop",
                        "api runtime-repo-authored-gate-loop",
                    ],
                    ReviewBoundaries =
                    [
                        "machine-readable policy truth remains upstream over markdown prompt projections",
                    ],
                    OutOfScope =
                    [
                        "markdown prompts as final truth owner",
                        "single central prompt stack",
                    ],
                },
                new RepoAuthoredGateConcernFamily
                {
                    FamilyId = "independent_gate_execution",
                    Layer = "gate_execution",
                    Summary = "Review and gate runs remain independent units with pass, fail, and pending result truth instead of one monolithic review pass.",
                    CurrentStatus = "ready_for_bounded_followup",
                    SourceProjects = ["Continue"],
                    DirectAbsorptions =
                    [
                        "independent check execution",
                        "per-unit result visibility",
                        "status-style review surface",
                    ],
                    TranslationTargets =
                    [
                        ".ai/artifacts/reviews/",
                        ".ai/artifacts/safety/",
                        "review-task",
                    ],
                    GovernedEntryPoints =
                    [
                        "task inspect <task-id>",
                        "review-task <task-id> <verdict> <reason...>",
                    ],
                    ReviewBoundaries =
                    [
                        "gate units remain attached to existing task and review truth",
                        "result states do not create a second control plane",
                    ],
                    OutOfScope =
                    [
                        "GitHub-specific status pipeline ownership",
                    ],
                },
                new RepoAuthoredGateConcernFamily
                {
                    FamilyId = "remediation_accept_reject_boundary",
                    Layer = "remediation",
                    Summary = "Suggested diffs and remediation decisions remain governed artifacts with explicit accept and reject semantics.",
                    CurrentStatus = "ready_for_bounded_followup",
                    SourceProjects = ["Continue"],
                    DirectAbsorptions =
                    [
                        "suggested diff as review output",
                        "explicit accept action",
                        "explicit reject action",
                    ],
                    TranslationTargets =
                    [
                        ".ai/artifacts/merge-candidates/",
                        ".ai/artifacts/reviews/",
                        "approve-review",
                    ],
                    GovernedEntryPoints =
                    [
                        "review-task <task-id> <verdict> <reason...>",
                        "approve-review <task-id> <reason...>",
                    ],
                    ReviewBoundaries =
                    [
                        "operator accept or reject remains explicit",
                        "provider output does not become sole truth ownership",
                    ],
                    OutOfScope =
                    [
                        "implicit auto-apply on review failure",
                    ],
                },
                new RepoAuthoredGateConcernFamily
                {
                    FamilyId = "workflow_projection_qualification",
                    Layer = "workflow_projection",
                    Summary = "Repo-authored gate loops remain valid across PR, branch, and local operator contexts, with GitHub/CI as one projection path.",
                    CurrentStatus = "ready_for_bounded_followup",
                    SourceProjects = ["Continue"],
                    DirectAbsorptions =
                    [
                        "PR-native projection",
                        "branch-scoped gate loop",
                        "result detail link or follow-up surface",
                    ],
                    TranslationTargets =
                    [
                        "pull_request_ci_projection",
                        "branch_review_projection",
                        "local_operator_projection",
                    ],
                    GovernedEntryPoints =
                    [
                        "inspect runtime-repo-authored-gate-loop",
                        "runtime inspect <repo-id>",
                    ],
                    ReviewBoundaries =
                    [
                        "CI and PR remain one projection path",
                        "local and branch workflows remain first-class",
                    ],
                    OutOfScope =
                    [
                        "GitHub-only worldview",
                    ],
                },
            ],
            TruthRoots =
            [
                Root(
                    "repo_authored_gate_policy_truth",
                    "canonical_truth",
                    "Machine-readable repo-authored review and gate loop policy remains the upstream truth owner for definition, execution, remediation, and workflow projection boundaries.",
                    [
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformRepoAuthoredGateLoopFile),
                    ],
                    [
                        "inspect runtime-repo-authored-gate-loop",
                        "api runtime-repo-authored-gate-loop",
                    ]),
                Root(
                    "review_and_gate_result_artifacts",
                    "operational_history",
                    "Review, safety, and merge-candidate artifacts remain the visible result layer for independent gate units and remediation suggestions.",
                    [
                        ".ai/artifacts/reviews/",
                        ".ai/artifacts/safety/",
                        ".ai/artifacts/merge-candidates/",
                    ],
                    [
                        "review-task",
                        "approve-review",
                        "InspectReviewArtifact",
                    ]),
                Root(
                    "task_and_review_control_truth",
                    "canonical_truth",
                    "Task graph, task nodes, and review writeback remain the stronger control truth for gate execution and operator decisions.",
                    [
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.TaskGraphFile),
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.TaskNodesRoot),
                        ".ai/memory/execution/",
                    ],
                    [
                        "TaskGraphService",
                        "ReviewWritebackService",
                        "PlannerEmergenceService",
                    ]),
                Root(
                    "provider_review_output_dependency",
                    "stronger_truth_dependency",
                    "Provider and worker execution output may inform review results and remediation suggestions but never becomes the sole truth owner.",
                    [
                        ".ai/artifacts/provider/",
                        ".ai/artifacts/worker-executions/",
                    ],
                    [
                        "AiExecutionArtifact",
                        "WorkerExecutionArtifact",
                    ]),
            ],
            BoundaryRules =
            [
                Rule(
                    "markdown_prompt_files_remain_projection_only",
                    "Markdown check files are one downstream projection path and may not replace machine-readable policy truth as the upstream owner.",
                    [
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformRepoAuthoredGateLoopFile),
                        ".continue/checks/",
                        ".agents/checks/",
                    ],
                    [
                        "markdown_prompt_truth_owner",
                    ]),
                Rule(
                    "gate_units_attach_to_existing_review_and_task_truth",
                    "Independent gate execution units attach to existing task and review truth instead of creating a second review control plane.",
                    [
                        "review-task",
                        "approve-review",
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.TaskGraphFile),
                    ],
                    [
                        "second_review_control_plane",
                        "parallel_gate_runtime_truth",
                    ]),
                Rule(
                    "remediation_accept_reject_requires_operator_decision",
                    "Suggested diffs and remediation remain governed artifacts until an explicit operator accept or reject decision is recorded.",
                    [
                        ".ai/artifacts/merge-candidates/",
                        "approve-review",
                    ],
                    [
                        "implicit_auto_apply",
                        "provider_output_as_final_truth",
                    ]),
                Rule(
                    "workflow_projections_are_not_pr_only",
                    "GitHub PR and CI remain one projection path; branch and local operator workflows stay valid first-class contexts.",
                    [
                        "pull_request_ci_projection",
                        "branch_review_projection",
                        "local_operator_projection",
                    ],
                    [
                        "github_only_worldview",
                        "pr_only_workflow_lock_in",
                    ]),
            ],
            GovernedReadPaths =
            [
                ReadPath(
                    "repo_authored_gate_loop_surface",
                    "inspect runtime-repo-authored-gate-loop",
                    "Read the machine-readable repo-authored gate loop boundary before proposing review definition, gate execution, remediation, or workflow projection changes.",
                    [
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformRepoAuthoredGateLoopFile),
                    ]),
                ReadPath(
                    "task_and_review_read_path",
                    "task inspect <task-id> | review-task <task-id> <verdict> <reason...>",
                    "Independent gate units and result transitions stay grounded in current task inspection and review writeback surfaces.",
                    [
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.TaskGraphFile),
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.TaskNodesRoot),
                        ".ai/artifacts/reviews/",
                    ]),
                ReadPath(
                    "remediation_artifact_read_path",
                    "approve-review <task-id> <reason...>",
                    "Remediation suggestions and accept/reject decisions remain visible through existing review approval surfaces and governed artifact families.",
                    [
                        ".ai/artifacts/merge-candidates/",
                        ".ai/artifacts/reviews/",
                    ]),
                ReadPath(
                    "workflow_projection_read_path",
                    "runtime inspect <repo-id> | inspect runtime-repo-authored-gate-loop",
                    "Workflow projection judgments should start from runtime and gate-loop surfaces rather than a PR-only assumption.",
                    [
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformRuntimeStateRoot),
                        RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformRepoAuthoredGateLoopFile),
                    ]),
            ],
            Qualification = new RepoAuthoredGateQualificationLine
            {
                Summary = "CARVES can absorb Continue-style repo-authored gate loops when machine-readable policy truth stays upstream, gate execution remains attached to existing review/task surfaces, remediation stays operator-controlled, and PR/CI remain one projection path among several.",
                SuccessCriteria =
                [
                    "Repo-authored review definitions are machine-readable first and projection-friendly second.",
                    "Independent gate execution and remediation accept/reject semantics are explicit in runtime truth.",
                    "Workflow qualification explicitly preserves PR, branch, and local operator projections without GitHub-only lock-in.",
                ],
                RejectedDirections =
                [
                    "markdown prompt files as final truth owner",
                    "GitHub-only or PR-only workflow ownership",
                    "implicit auto-apply remediation",
                ],
                DeferredDirections =
                [
                    "provider-specific PR platform integration stays projection-specific",
                    "automatic branch or PR commit materialization remains out of scope in this slice",
                ],
                StopConditions =
                [
                    "Do not let markdown prompt files replace policy truth.",
                    "Do not make GitHub or PR the only supported workflow worldview.",
                    "Do not auto-apply remediation suggestions without explicit operator acceptance.",
                ],
            },
            ReadinessMap =
            [
                Ready(
                    "repo_authored_review_definitions",
                    "ready_for_bounded_followup",
                    "Repo-authored review definitions are ready for bounded implementation as machine-readable-first policy truth with projection paths.",
                    [
                        "runtime-repo-authored-gate-loop",
                        "inspect runtime-repo-authored-gate-loop",
                    ]),
                Ready(
                    "independent_gate_result_units",
                    "ready_for_bounded_followup",
                    "Independent gate execution units and pass/fail/pending result truth are ready to extend existing review and task surfaces.",
                    [
                        "review-task",
                        "task inspect <task-id>",
                    ]),
                Ready(
                    "remediation_accept_reject_boundary",
                    "ready_for_bounded_followup",
                    "Suggested diffs and remediation decisions are ready to extend current review and merge-candidate artifact families.",
                    [
                        "approve-review",
                        ".ai/artifacts/merge-candidates/",
                    ]),
                Ready(
                    "pr_branch_local_workflow_projection",
                    "ready_for_bounded_followup",
                    "Workflow projection across PR, branch, and local operator contexts is ready for bounded follow-up without GitHub-only ownership.",
                    [
                        "pull_request_ci_projection",
                        "branch_review_projection",
                        "local_operator_projection",
                    ]),
                Ready(
                    "github_only_worldview",
                    "rejected",
                    "GitHub-only or PR-only workflow ownership is explicitly rejected by this qualification line.",
                    [
                        "runtime-repo-authored-gate-loop",
                    ]),
                Ready(
                    "markdown_prompt_truth_owner",
                    "rejected",
                    "Markdown check prompt files remain downstream projections instead of upstream truth owners.",
                    [
                        "runtime-repo-authored-gate-loop",
                    ]),
            ],
            Notes =
            [
                "Continue is treated here as a repo-authored review/gate loop reference, not as a replacement for CARVES policy truth or Host governance.",
                "The gate-loop slice reuses current review, approval, artifact, and task truth surfaces instead of creating a second control plane.",
            ],
        };
    }

    private static bool NeedsRefresh(RepoAuthoredGateLoopPolicy policy)
    {
        return policy.PolicyVersion < 1
               || policy.ConcernFamilies.Length < 4
               || policy.WorkflowProjections.Length < 3
               || policy.TruthRoots.Length < 4
               || policy.BoundaryRules.Length < 4
               || policy.GovernedReadPaths.Length < 4
               || policy.ReadinessMap.Length < 6
               || !policy.ExtractionBoundary.RejectedAnchors.Contains("markdown prompt files as the final truth owner", StringComparer.Ordinal)
               || !policy.ReadinessMap.Any(item => string.Equals(item.SemanticId, "github_only_worldview", StringComparison.Ordinal) && string.Equals(item.Readiness, "rejected", StringComparison.Ordinal))
               || !policy.ReadinessMap.Any(item => string.Equals(item.SemanticId, "markdown_prompt_truth_owner", StringComparison.Ordinal) && string.Equals(item.Readiness, "rejected", StringComparison.Ordinal));
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

    private static RepoAuthoredGateReadinessEntry Ready(
        string semanticId,
        string readiness,
        string summary,
        string[] governedFollowUp,
        string[]? notes = null)
    {
        return new RepoAuthoredGateReadinessEntry
        {
            SemanticId = semanticId,
            Readiness = readiness,
            Summary = summary,
            GovernedFollowUp = governedFollowUp,
            Notes = notes ?? [],
        };
    }
}
