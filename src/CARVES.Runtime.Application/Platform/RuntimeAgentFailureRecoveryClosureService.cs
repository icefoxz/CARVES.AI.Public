namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentFailureRecoveryClosureService
{
    private readonly string repoRoot;

    public RuntimeAgentFailureRecoveryClosureService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeAgentFailureRecoveryClosureSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        const string boundaryDocumentPath = "docs/runtime/runtime-agent-governed-failure-classification-recovery-closure-contract.md";
        const string runtimeFailureModelPath = "docs/runtime/runtime-failures.md";
        const string retryPolicyPath = "docs/runtime/worker-failure-classification-retry-policy.md";
        const string recoveryPolicyPath = "docs/runtime/recovery-policy-engine.md";
        const string runtimeConsistencyPath = "docs/runtime/runtime-consistency-check.md";
        const string lifecycleReconciliationPath = "docs/runtime/delegated-worker-lifecycle-reconciliation.md";
        const string expiredRunClassificationPath = "docs/runtime/expired-delegated-run-recovery-classification.md";

        ValidateDocument(boundaryDocumentPath, "Stage 6 failure recovery contract", errors);
        ValidateDocument(runtimeFailureModelPath, "Runtime failure model", errors);
        ValidateDocument(retryPolicyPath, "Worker failure classification and retry policy", errors);
        ValidateDocument(recoveryPolicyPath, "Recovery policy engine", errors);
        ValidateDocument(runtimeConsistencyPath, "Runtime consistency check", errors);
        ValidateDocument(lifecycleReconciliationPath, "Delegated worker lifecycle reconciliation", errors);
        ValidateDocument(expiredRunClassificationPath, "Expired delegated run recovery classification", errors);

        var failureClasses = BuildFailureClasses(
            runtimeFailureModelPath,
            retryPolicyPath,
            recoveryPolicyPath,
            runtimeConsistencyPath,
            lifecycleReconciliationPath,
            expiredRunClassificationPath);

        if (failureClasses.Count == 0)
        {
            errors.Add("No bounded Stage 6 failure classes were projected.");
        }

        if (failureClasses.All(item => !string.Equals(item.RetryPosture, "bounded_auto_retry_only_for_retryable_failure_truth", StringComparison.Ordinal)))
        {
            warnings.Add("No failure class currently projects bounded auto-retry posture.");
        }

        return new RuntimeAgentFailureRecoveryClosureSurface
        {
            BoundaryDocumentPath = boundaryDocumentPath,
            RuntimeFailureModelPath = runtimeFailureModelPath,
            RetryPolicyPath = retryPolicyPath,
            RecoveryPolicyPath = recoveryPolicyPath,
            RuntimeConsistencyPath = runtimeConsistencyPath,
            LifecycleReconciliationPath = lifecycleReconciliationPath,
            ExpiredRunClassificationPath = expiredRunClassificationPath,
            OverallPosture = errors.Count == 0
                ? "bounded_failure_recovery_closure_ready"
                : "blocked_by_failure_recovery_gaps",
            RetryableFailureKinds =
            [
                "transient_infra",
                "timeout",
                "invalid_output",
            ],
            RecoveryOutcomes =
            [
                "retry",
                "rebuild_worktree",
                "switch_provider",
                "block_task",
                "escalate_to_operator",
            ],
            ReadOnlyVisibilityCommands =
            [
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-agent-failure-recovery-closure"),
                RuntimeHostCommandLauncher.Cold("api", "runtime-agent-failure-recovery-closure"),
                RuntimeHostCommandLauncher.Cold("verify", "runtime"),
                RuntimeHostCommandLauncher.Cold("task", "inspect", "<task-id>", "--runs"),
            ],
            OperatorHandoffCommands =
            [
                RuntimeHostCommandLauncher.Cold("reconcile", "runtime"),
                RuntimeHostCommandLauncher.Cold("task", "inspect", "<task-id>", "--runs"),
                RuntimeHostCommandLauncher.Cold("review-task", "<task-id>", "<verdict>", "<reason...>"),
            ],
            FailureClasses = failureClasses,
            BlockedBehaviors =
            [
                "silent retry loops",
                "synthetic success after repeated failure",
                "gateway_or_frontend_owned_recovery_truth",
                "provider_owned_recovery_planner",
                "second_recovery_control_plane",
            ],
            RecommendedNextAction = errors.Count == 0
                ? "Inspect runtime-agent-failure-recovery-closure, run verify runtime before trusting drifted state, and use reconcile runtime only to convert known drift into explicit Runtime recovery posture."
                : "Restore the missing Stage 6 failure and recovery anchors before treating bounded recovery closure as delivery-ready.",
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "This surface does not introduce a new recovery planner, new lifecycle truth owner, or hidden auto-healing loop.",
                "This surface explains bounded retry, repair, and operator handoff posture; it does not bypass review-task, approve-review, or sync-state.",
                "This surface does not turn gateway, frontend, or packaging lanes into recovery truth owners.",
            ],
        };
    }

    private static List<RuntimeAgentFailureRecoveryClassSurface> BuildFailureClasses(
        string runtimeFailureModelPath,
        string retryPolicyPath,
        string recoveryPolicyPath,
        string runtimeConsistencyPath,
        string lifecycleReconciliationPath,
        string expiredRunClassificationPath)
    {
        return
        [
            new RuntimeAgentFailureRecoveryClassSurface
            {
                FailureClassId = "transient_execution_instability",
                Summary = "Transport, timeout, or protocol instability may use bounded retry policy, but repeated failure must remain explicit and inspectable.",
                FailureKinds = ["transient_infra", "timeout", "invalid_output"],
                Signals = ["transport_or_provider_timeout", "protocol_invalid_output", "retryable_failure_truth"],
                RetryPosture = "bounded_auto_retry_only_for_retryable_failure_truth",
                RuntimeOutcome = "retry_then_visible_failure_state",
                OperatorHandoff = "inspect execution evidence and decide repair or replan if retry budget is exhausted",
                GoverningRefs = [retryPolicyPath, recoveryPolicyPath],
                NonClaims =
                [
                    "No silent infinite retry.",
                    "No synthetic success after retry exhaustion.",
                ],
            },
            new RuntimeAgentFailureRecoveryClassSurface
            {
                FailureClassId = "policy_or_approval_boundary",
                Summary = "Policy denial and approval wait stay explicit Runtime boundary states and require existing approval or review paths.",
                FailureKinds = ["policy_denied", "approval_required"],
                Signals = ["permission_boundary", "approval_wait"],
                RetryPosture = "no_automatic_retry",
                RuntimeOutcome = "approval_wait_or_blocked",
                OperatorHandoff = "resolve approval or policy boundary on the existing Host-governed lane",
                GoverningRefs = [retryPolicyPath, recoveryPolicyPath],
                NonClaims =
                [
                    "No background permission escalation.",
                    "No approval bypass from provider or gateway lanes.",
                ],
            },
            new RuntimeAgentFailureRecoveryClassSurface
            {
                FailureClassId = "delegated_launch_or_environment_break",
                Summary = "Launch, attach, wrapper, artifact-path, or environment breakage must stay explicit so substrate repair happens before retry.",
                FailureKinds = ["environment_blocked", "launch_failure", "attach_failure", "wrapper_failure", "artifact_failure"],
                Signals =
                [
                    "environment_setup_failed",
                    "attach_failure",
                    "wrapper_failure",
                    "delegated_worker_launch_failed",
                    "artifact_path_failure",
                    "execution_substrate_failure",
                ],
                RetryPosture = "repair_substrate_before_retry",
                RuntimeOutcome = "blocked_or_retryable_after_explicit_repair",
                OperatorHandoff = "repair attach, launcher, worktree, or artifact boundary on the same Runtime lane before retry",
                GoverningRefs = [retryPolicyPath, recoveryPolicyPath, lifecycleReconciliationPath],
                NonClaims =
                [
                    "No hidden fallback to a second launch path.",
                    "No environment reset outside Runtime-owned boundaries.",
                ],
            },
            new RuntimeAgentFailureRecoveryClassSurface
            {
                FailureClassId = "delegated_lifecycle_drift",
                Summary = "Expired, orphaned, quarantined, or blocked delegated runs stay explicit until verify/reconcile projects a stronger recovery state.",
                FailureKinds = [],
                Signals = ["expired", "orphaned", "quarantined", "retryable", "manual_review_required", "blocked"],
                RetryPosture = "reconcile_before_retry",
                RuntimeOutcome = "retryable_or_quarantined_or_blocked_after_reconciliation",
                OperatorHandoff = "run verify runtime before trusting pending state, then use reconcile runtime to materialize explicit recovery posture",
                GoverningRefs = [runtimeConsistencyPath, lifecycleReconciliationPath, expiredRunClassificationPath],
                NonClaims =
                [
                    "No weak expired_to_pending collapse.",
                    "No stale worktree disappearance without explicit recovery state.",
                ],
            },
            new RuntimeAgentFailureRecoveryClassSurface
            {
                FailureClassId = "semantic_task_failure",
                Summary = "Build, test, contract, patch, or task-logic failures remain reviewable task outcomes rather than hidden transport recovery.",
                FailureKinds = ["task_logic_failed", "build_failure", "test_failure", "contract_failure", "patch_failure"],
                Signals = ["build_failure", "test_failure", "contract_failure", "patch_failure", "task_logic_failed"],
                RetryPosture = "review_or_replan_before_retry",
                RuntimeOutcome = "review_or_pending_only_when_retry_is_explicit",
                OperatorHandoff = "inspect run evidence, then reject, repair, or replan on the existing review lane",
                GoverningRefs =
                [
                    retryPolicyPath,
                    recoveryPolicyPath,
                    "docs/runtime/runtime-agent-governed-run-diff-review-surfaces-contract.md",
                ],
                NonClaims =
                [
                    "No semantic failure collapse into transport retry.",
                    "No writeback bypass because partial output exists.",
                ],
            },
            new RuntimeAgentFailureRecoveryClassSurface
            {
                FailureClassId = "operator_stop_or_unknown_failure",
                Summary = "Cancelled, aborted, or still-unknown failures stay explicitly stopped until an operator chooses rerun, repair, or replan.",
                FailureKinds = ["cancelled", "aborted", "unknown"],
                Signals = ["explicit_stop", "unknown_failure"],
                RetryPosture = "operator_resubmit_only",
                RuntimeOutcome = "blocked_until_explicit_operator_decision",
                OperatorHandoff = "inspect evidence and choose rerun or replan explicitly on the existing Host lane",
                GoverningRefs = [runtimeFailureModelPath, recoveryPolicyPath],
                NonClaims =
                [
                    "No hidden resume after an explicit stop.",
                    "No synthetic classification beyond available evidence.",
                ],
            },
        ];
    }

    private void ValidateDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
