namespace Carves.Runtime.Application.Configuration;

public sealed record RuntimeRoleModeExecutionGateDecision(
    bool Allowed,
    string Outcome,
    string Summary,
    string NextAction,
    IReadOnlyList<string> Guidance);

public static class RuntimeRoleModeExecutionGate
{
    public static RuntimeRoleModeExecutionGateDecision EvaluateDelegatedExecution(RoleGovernanceRuntimePolicy policy)
    {
        if (!string.Equals(policy.RoleMode, RoleGovernanceRuntimePolicy.EnabledMode, StringComparison.Ordinal))
        {
            return Reject(
                "role_mode_disabled",
                "Role mode is disabled; delegated worker execution is closed.",
                "Continue in main-thread direct mode, or explicitly re-enable role mode through governed policy before using task run.");
        }

        if (!policy.PlannerWorkerSplitEnabled)
        {
            return Reject(
                "planner_worker_split_disabled",
                "Planner/Worker split is disabled; delegated worker execution is closed.",
                "Enable planner_worker_split_enabled through governed policy before dispatching worker tasks.");
        }

        if (!policy.WorkerDelegationEnabled)
        {
            return Reject(
                "worker_delegation_disabled",
                "Worker delegation is disabled; delegated worker execution is closed.",
                "Enable worker_delegation_enabled through governed policy before dispatching worker tasks.");
        }

        return new RuntimeRoleModeExecutionGateDecision(
            Allowed: true,
            Outcome: "delegated_execution_enabled",
            Summary: "Role mode allows delegated worker execution.",
            NextAction: "Continue through the governed task run path.",
            Guidance:
            [
                "Role mode is enabled.",
                "Planner/Worker split is enabled.",
                "Worker delegation is enabled."
            ]);
    }

    public static RuntimeRoleModeExecutionGateDecision EvaluateSchedulerAutoDispatch(RoleGovernanceRuntimePolicy policy)
    {
        if (!string.Equals(policy.RoleMode, RoleGovernanceRuntimePolicy.EnabledMode, StringComparison.Ordinal))
        {
            return Reject(
                "role_mode_disabled",
                "Role mode is disabled; scheduler auto-dispatch is closed.",
                "Continue in main-thread direct mode, or explicitly re-enable role mode through governed policy before using scheduler automation.");
        }

        if (!policy.SchedulerAutoDispatchEnabled)
        {
            return Reject(
                "scheduler_auto_dispatch_disabled",
                "Scheduler auto-dispatch is disabled by role governance policy.",
                "Enable scheduler_auto_dispatch_enabled through governed policy before host scheduling can advance repos automatically.");
        }

        if (!policy.PlannerWorkerSplitEnabled)
        {
            return Reject(
                "planner_worker_split_disabled",
                "Planner/Worker split is disabled; scheduler auto-dispatch is closed.",
                "Enable planner_worker_split_enabled through governed policy before host scheduling can advance worker automation.");
        }

        if (!policy.WorkerDelegationEnabled)
        {
            return Reject(
                "worker_delegation_disabled",
                "Worker delegation is disabled; scheduler auto-dispatch is closed.",
                "Enable worker_delegation_enabled through governed policy before host scheduling can advance worker automation.");
        }

        return new RuntimeRoleModeExecutionGateDecision(
            Allowed: true,
            Outcome: "scheduler_auto_dispatch_enabled",
            Summary: "Role mode allows scheduler auto-dispatch.",
            NextAction: "Continue through the governed host scheduler path.",
            Guidance:
            [
                "Role mode is enabled.",
                "Scheduler auto-dispatch is enabled.",
                "Planner/Worker split and worker delegation are enabled."
            ]);
    }

    public static RuntimeRoleModeExecutionGateDecision EvaluateReviewAutoContinue(RoleGovernanceRuntimePolicy policy)
    {
        if (!string.Equals(policy.RoleMode, RoleGovernanceRuntimePolicy.EnabledMode, StringComparison.Ordinal))
        {
            return Reject(
                "role_mode_disabled",
                "Role mode is disabled; review auto-continue is closed.",
                "Review approval may complete the current task, but it must not automatically dispatch the next task until role mode is explicitly re-enabled.");
        }

        if (!policy.SchedulerAutoDispatchEnabled)
        {
            return Reject(
                "scheduler_auto_dispatch_disabled",
                "Scheduler auto-dispatch is disabled; review auto-continue is closed.",
                "Enable scheduler_auto_dispatch_enabled through governed policy before review approval can automatically continue to the next task.");
        }

        if (!policy.PlannerWorkerSplitEnabled)
        {
            return Reject(
                "planner_worker_split_disabled",
                "Planner/Worker split is disabled; review auto-continue is closed.",
                "Enable planner_worker_split_enabled through governed policy before review approval can automatically dispatch worker tasks.");
        }

        if (!policy.WorkerDelegationEnabled)
        {
            return Reject(
                "worker_delegation_disabled",
                "Worker delegation is disabled; review auto-continue is closed.",
                "Enable worker_delegation_enabled through governed policy before review approval can automatically dispatch worker tasks.");
        }

        return new RuntimeRoleModeExecutionGateDecision(
            Allowed: true,
            Outcome: "review_auto_continue_enabled",
            Summary: "Role mode allows review auto-continue.",
            NextAction: "Review approval may auto-continue when the dispatch projection is dispatchable.",
            Guidance:
            [
                "Role mode is enabled.",
                "Scheduler auto-dispatch is enabled.",
                "Planner/Worker split and worker delegation are enabled."
            ]);
    }

    private static RuntimeRoleModeExecutionGateDecision Reject(
        string outcome,
        string summary,
        string nextAction)
    {
        return new RuntimeRoleModeExecutionGateDecision(
            Allowed: false,
            Outcome: outcome,
            Summary: summary,
            NextAction: nextAction,
            Guidance:
            [
                "This rejects task run before worker lease, execution run, result ingestion, or task truth mutation.",
                "Use policy inspect to confirm role_mode, planner_worker_split_enabled, and worker_delegation_enabled."
            ]);
    }
}
