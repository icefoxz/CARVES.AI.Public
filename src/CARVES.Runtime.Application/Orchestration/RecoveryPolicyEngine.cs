using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Orchestration;

public sealed class RecoveryPolicyEngine
{
    private readonly SafetyRules safetyRules;
    private readonly WorkerSelectionPolicyService selectionPolicyService;
    private readonly ProviderHealthMonitorService providerHealthMonitorService;
    private readonly WorkerOperationalPolicyService operationalPolicyService;

    public RecoveryPolicyEngine(
        SafetyRules safetyRules,
        WorkerSelectionPolicyService selectionPolicyService,
        ProviderHealthMonitorService providerHealthMonitorService,
        WorkerOperationalPolicyService operationalPolicyService)
    {
        this.safetyRules = safetyRules;
        this.selectionPolicyService = selectionPolicyService;
        this.providerHealthMonitorService = providerHealthMonitorService;
        this.operationalPolicyService = operationalPolicyService;
    }

    public RecoveryPolicyEngine(
        SafetyRules safetyRules,
        WorkerSelectionPolicyService selectionPolicyService,
        ProviderHealthMonitorService providerHealthMonitorService)
        : this(safetyRules, selectionPolicyService, providerHealthMonitorService, new WorkerOperationalPolicyService(WorkerOperationalPolicy.CreateDefault()))
    {
    }

    public WorkerRecoveryDecision Evaluate(TaskNode task, WorkerExecutionResult result, string? repoId = null)
    {
        var policy = operationalPolicyService.GetPolicy().Recovery;
        var health = providerHealthMonitorService.GetHealth(result.BackendId);
        var alternative = selectionPolicyService.FindAlternative(task, result.BackendId, repoId);
        var retryCount = task.RetryCount;
        var maxRetryCount = policy.MaxRetryCount > 0 ? policy.MaxRetryCount : safetyRules.MaxRetryCount;
        var classification = WorkerFailureSemantics.Classify(result);
        var substrateCategory = classification.SubstrateCategory;

        if (classification.ReasonCode == "policy_denied" || classification.ReasonCode == "approval_required")
        {
            return new WorkerRecoveryDecision
            {
                Action = WorkerRecoveryAction.EscalateToOperator,
                ReasonCode = "permission_or_policy_boundary",
                Reason = "Worker execution crossed a permission or policy boundary and requires operator attention.",
                Actionability = RuntimeActionability.HumanActionable,
            };
        }

        if (classification.Lane == WorkerFailureLane.Substrate)
        {
            if (ContainsAny(result.FailureReason, "OPENAI_API_KEY", "CODEX_API_KEY", "api key", "authentication", "unauthorized", "secret"))
            {
                return new WorkerRecoveryDecision
                {
                    Action = WorkerRecoveryAction.BlockTask,
                    ReasonCode = "environment_configuration_required",
                    Reason = "Worker execution is blocked by missing or invalid environment credentials and requires operator correction.",
                    Actionability = RuntimeActionability.HumanActionable,
                };
            }

            if (substrateCategory is "environment_setup_failed" or "delegated_worker_launch_failed" or "attach_failure" or "wrapper_failure" or "artifact_path_failure")
            {
                return new WorkerRecoveryDecision
                {
                    Action = retryCount < maxRetryCount ? WorkerRecoveryAction.RebuildWorktree : WorkerRecoveryAction.EscalateToOperator,
                    ReasonCode = substrateCategory ?? classification.ReasonCode,
                    Reason = $"Delegated worker substrate failure '{substrateCategory ?? classification.ReasonCode}' requires environment correction before semantic task retry.",
                    Actionability = retryCount < maxRetryCount ? RuntimeActionability.WorkerActionable : RuntimeActionability.HumanActionable,
                    RetryNotBefore = retryCount < maxRetryCount
                        ? DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, policy.EnvironmentRebuildBackoffSeconds))
                        : null,
                    AutoApplied = retryCount < maxRetryCount,
                };
            }

            if (policy.SwitchProviderOnEnvironmentBlocked && alternative is not null && result.FailureKind == WorkerFailureKind.EnvironmentBlocked)
            {
                return new WorkerRecoveryDecision
                {
                    Action = WorkerRecoveryAction.SwitchProvider,
                    ReasonCode = "environment_blocked_switch_provider",
                    Reason = $"Current backend '{result.BackendId}' is environment-blocked; switch to '{alternative.BackendId}'.",
                    Actionability = RuntimeActionability.WorkerActionable,
                    AlternateBackendId = alternative.BackendId,
                    AlternateProviderId = alternative.ProviderId,
                    AutoApplied = true,
                };
            }

            if (substrateCategory == "delegated_worker_hung")
            {
                return new WorkerRecoveryDecision
                {
                    Action = retryCount < maxRetryCount ? WorkerRecoveryAction.Retry : WorkerRecoveryAction.EscalateToOperator,
                    ReasonCode = substrateCategory,
                    Reason = "Delegated worker stopped converging and requires bounded recovery before semantic replan.",
                    Actionability = retryCount < maxRetryCount ? RuntimeActionability.WorkerActionable : RuntimeActionability.HumanActionable,
                    RetryNotBefore = retryCount < maxRetryCount
                        ? DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, policy.TimeoutBackoffSeconds) * (retryCount + 1))
                        : null,
                    AutoApplied = retryCount < maxRetryCount,
                };
            }

            if (substrateCategory == "delegated_worker_invalid_bootstrap_output")
            {
                return new WorkerRecoveryDecision
                {
                    Action = WorkerRecoveryAction.EscalateToOperator,
                    ReasonCode = substrateCategory,
                    Reason = "Delegated worker bootstrap output did not converge to governed runtime truth and should not enter semantic retry.",
                    Actionability = RuntimeActionability.HumanActionable,
                };
            }

            if (policy.SwitchProviderOnUnavailableBackend
                && health?.State == WorkerBackendHealthState.Unavailable
                && alternative is not null
                && result.FailureKind is WorkerFailureKind.EnvironmentBlocked or WorkerFailureKind.TransientInfra or WorkerFailureKind.Timeout)
            {
                return new WorkerRecoveryDecision
                {
                    Action = WorkerRecoveryAction.SwitchProvider,
                    ReasonCode = "provider_unavailable_switch_provider",
                    Reason = $"Backend '{result.BackendId}' is unavailable; switch to '{alternative.BackendId}'.",
                    Actionability = RuntimeActionability.WorkerActionable,
                    AlternateBackendId = alternative.BackendId,
                    AlternateProviderId = alternative.ProviderId,
                    AutoApplied = true,
                };
            }

            if (retryCount < maxRetryCount)
            {
                return new WorkerRecoveryDecision
                {
                    Action = WorkerRecoveryAction.RebuildWorktree,
                    ReasonCode = classification.ReasonCode,
                    Reason = "Execution substrate remained blocked and should be retried only after rebuilding the worktree and runtime inputs.",
                    Actionability = RuntimeActionability.WorkerActionable,
                    RetryNotBefore = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, policy.EnvironmentRebuildBackoffSeconds)),
                    AutoApplied = true,
                };
            }

            return new WorkerRecoveryDecision
            {
                Action = WorkerRecoveryAction.EscalateToOperator,
                ReasonCode = classification.ReasonCode,
                Reason = "Execution substrate remained blocked after bounded recovery attempts.",
                Actionability = RuntimeActionability.HumanActionable,
            };
        }

        if (result.FailureKind is WorkerFailureKind.TransientInfra or WorkerFailureKind.Timeout or WorkerFailureKind.InvalidOutput)
        {
            if (policy.SwitchProviderOnUnavailableBackend && health?.State == WorkerBackendHealthState.Unavailable && alternative is not null)
            {
                return new WorkerRecoveryDecision
                {
                    Action = WorkerRecoveryAction.SwitchProvider,
                    ReasonCode = "provider_unavailable_switch_provider",
                    Reason = $"Backend '{result.BackendId}' is unavailable; switch to '{alternative.BackendId}'.",
                    Actionability = RuntimeActionability.WorkerActionable,
                    AlternateBackendId = alternative.BackendId,
                    AlternateProviderId = alternative.ProviderId,
                    AutoApplied = true,
                };
            }

            if (retryCount < maxRetryCount)
            {
                var backoff = result.FailureKind switch
                {
                    WorkerFailureKind.Timeout => TimeSpan.FromSeconds(Math.Max(0, policy.TimeoutBackoffSeconds) * (retryCount + 1)),
                    WorkerFailureKind.InvalidOutput => TimeSpan.FromSeconds(Math.Max(0, policy.InvalidOutputBackoffSeconds) * (retryCount + 1)),
                    _ => TimeSpan.FromSeconds(Math.Max(0, policy.TransientInfraBackoffSeconds) * (retryCount + 1)),
                };
                return new WorkerRecoveryDecision
                {
                    Action = WorkerRecoveryAction.Retry,
                    ReasonCode = "retryable_failure",
                    Reason = $"Failure '{result.FailureKind}' is retryable within the configured retry budget.",
                    Actionability = RuntimeActionability.WorkerActionable,
                    RetryNotBefore = DateTimeOffset.UtcNow.Add(backoff),
                    AutoApplied = true,
                };
            }

            return new WorkerRecoveryDecision
            {
                Action = WorkerRecoveryAction.BlockTask,
                ReasonCode = "retry_budget_exhausted",
                Reason = $"Failure '{result.FailureKind}' exhausted the bounded retry budget.",
                Actionability = RuntimeActionability.HumanActionable,
            };
        }

        if (classification.Lane == WorkerFailureLane.Semantic)
        {
            return new WorkerRecoveryDecision
            {
                Action = WorkerRecoveryAction.BlockTask,
                ReasonCode = classification.ReasonCode,
                Reason = "Worker execution failed at the semantic layer and should route through planner review instead of substrate recovery.",
                Actionability = RuntimeActionability.HumanActionable,
            };
        }

        return new WorkerRecoveryDecision
        {
            Action = WorkerRecoveryAction.EscalateToOperator,
            ReasonCode = "unknown_failure",
            Reason = "Failure could not be safely classified into an automatic recovery path.",
            Actionability = RuntimeActionability.HumanActionable,
        };
    }

    private static bool ContainsAny(string? value, params string[] patterns)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return patterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}
