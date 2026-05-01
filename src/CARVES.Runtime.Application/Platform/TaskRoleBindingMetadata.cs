using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform;

public static class TaskRoleBindingMetadata
{
    public const string ProducerKey = "role_binding_producer";
    public const string ExecutorKey = "role_binding_executor";
    public const string ReviewerKey = "role_binding_reviewer";
    public const string ApproverKey = "role_binding_approver";
    public const string ScopeStewardKey = "role_binding_scope_steward";
    public const string PolicyOwnerKey = "role_binding_policy_owner";

    public static IReadOnlyDictionary<string, string> Merge(IReadOnlyDictionary<string, string> metadata, TaskRoleBinding? binding)
    {
        var merged = new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        Apply(merged, ProducerKey, binding?.Producer);
        Apply(merged, ExecutorKey, binding?.Executor);
        Apply(merged, ReviewerKey, binding?.Reviewer);
        Apply(merged, ApproverKey, binding?.Approver);
        Apply(merged, ScopeStewardKey, binding?.ScopeSteward);
        Apply(merged, PolicyOwnerKey, binding?.PolicyOwner);
        return merged;
    }

    public static TaskRoleBinding? TryRead(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        var binding = new TaskRoleBinding
        {
            Producer = Read(metadata, ProducerKey),
            Executor = Read(metadata, ExecutorKey),
            Reviewer = Read(metadata, ReviewerKey),
            Approver = Read(metadata, ApproverKey),
            ScopeSteward = Read(metadata, ScopeStewardKey),
            PolicyOwner = Read(metadata, PolicyOwnerKey),
        };

        return HasAny(binding) ? binding : null;
    }

    public static TaskRoleBinding Resolve(TaskNode task, RoleGovernanceRuntimePolicy policy)
    {
        return Resolve(task.TaskType, task.Metadata, policy);
    }

    public static TaskRoleBinding Resolve(TaskType taskType, IReadOnlyDictionary<string, string>? metadata, RoleGovernanceRuntimePolicy policy)
    {
        var defaults = ResolveDefaults(taskType, policy);
        var binding = TryRead(metadata);
        return binding is null ? defaults : Overlay(defaults, binding);
    }

    public static string? EvaluateApprovalSeparation(TaskNode task, RoleGovernanceRuntimePolicy policy)
    {
        var binding = Resolve(task, policy);
        if (policy.ProducerCannotSelfApprove
            && EqualsIgnoreCase(binding.Producer, binding.Approver))
        {
            return $"Task {task.TaskId} violates producer_cannot_self_approve: producer and approver are both '{binding.Producer}'.";
        }

        if (policy.ReviewerCannotApproveSameTask
            && EqualsIgnoreCase(binding.Reviewer, binding.Approver))
        {
            return $"Task {task.TaskId} violates reviewer_cannot_approve_same_task: reviewer and approver are both '{binding.Reviewer}'.";
        }

        return null;
    }

    public static bool HasExplicitBinding(IReadOnlyDictionary<string, string>? metadata)
    {
        return TryRead(metadata) is not null;
    }

    private static TaskRoleBinding ResolveDefaults(TaskType taskType, RoleGovernanceRuntimePolicy policy)
    {
        var defaults = policy.DefaultRoleBinding;
        var executor = Normalize(defaults.Executor);
        if (string.IsNullOrWhiteSpace(executor))
        {
            executor = taskType.CanExecuteInWorker() ? "worker" : "planner";
        }
        else if (!taskType.CanExecuteInWorker() && string.Equals(executor, "worker", StringComparison.OrdinalIgnoreCase))
        {
            executor = "planner";
        }

        return new TaskRoleBinding
        {
            Producer = FirstNonBlank(defaults.Producer, "planner"),
            Executor = executor,
            Reviewer = FirstNonBlank(defaults.Reviewer, "planner"),
            Approver = FirstNonBlank(defaults.Approver, "operator"),
            ScopeSteward = FirstNonBlank(defaults.ScopeSteward, "operator"),
            PolicyOwner = FirstNonBlank(defaults.PolicyOwner, "operator"),
        };
    }

    private static TaskRoleBinding Overlay(TaskRoleBinding defaults, TaskRoleBinding binding)
    {
        return new TaskRoleBinding
        {
            Producer = FirstNonBlank(binding.Producer, defaults.Producer),
            Executor = FirstNonBlank(binding.Executor, defaults.Executor),
            Reviewer = FirstNonBlank(binding.Reviewer, defaults.Reviewer),
            Approver = FirstNonBlank(binding.Approver, defaults.Approver),
            ScopeSteward = FirstNonBlank(binding.ScopeSteward, defaults.ScopeSteward),
            PolicyOwner = FirstNonBlank(binding.PolicyOwner, defaults.PolicyOwner),
        };
    }

    private static bool HasAny(TaskRoleBinding binding)
    {
        return !string.IsNullOrWhiteSpace(binding.Producer)
            || !string.IsNullOrWhiteSpace(binding.Executor)
            || !string.IsNullOrWhiteSpace(binding.Reviewer)
            || !string.IsNullOrWhiteSpace(binding.Approver)
            || !string.IsNullOrWhiteSpace(binding.ScopeSteward)
            || !string.IsNullOrWhiteSpace(binding.PolicyOwner);
    }

    private static void Apply(IDictionary<string, string> metadata, string key, string? value)
    {
        var normalized = Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            metadata.Remove(key);
            return;
        }

        metadata[key] = normalized;
    }

    private static string Read(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) ? Normalize(value) : string.Empty;
    }

    private static string FirstNonBlank(string? value, string fallback)
    {
        var normalized = Normalize(value);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool EqualsIgnoreCase(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left)
            && !string.IsNullOrWhiteSpace(right)
            && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
