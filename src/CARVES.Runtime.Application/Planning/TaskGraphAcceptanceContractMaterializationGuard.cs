using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public sealed record TaskGraphAcceptanceContractMaterializationTaskProjection(
    string TaskId,
    TaskType TaskType,
    bool RequiresContract,
    bool HasContract,
    string? ContractId,
    string ProjectionSource,
    string ProjectionPolicy,
    string State,
    bool BlocksMaterialization,
    string ReasonCode,
    string Summary,
    string RecommendedAction);

public sealed record TaskGraphAcceptanceContractMaterializationReport(
    string State,
    bool BlocksMaterialization,
    int TaskCount,
    int ExecutableTaskCount,
    int ExplicitContractCount,
    int SynthesizedMinimumContractCount,
    int FailureCount,
    string Summary,
    string RecommendedAction,
    IReadOnlyList<TaskGraphAcceptanceContractMaterializationTaskProjection> Tasks,
    IReadOnlyList<TaskGraphAcceptanceContractMaterializationTaskProjection> Failures);

public static class TaskGraphAcceptanceContractMaterializationGuard
{
    public const string ReadyState = "ready";
    public const string BlockedByAcceptanceContractGapState = "blocked_by_acceptance_contract_gap";
    public const string AlreadyMaterializedReadbackState = "already_materialized_readback";
    public const string ProjectedState = "projected";
    public const string NotRequiredState = "not_required";
    public const string MissingContractState = "missing_acceptance_contract";
    public const string MissingProjectionSourceState = "missing_projection_source";
    public const string LegacyUnverifiedProjectionState = "legacy_unverified_projection";
    public const string LegacyUnclassifiedProjectionState = "legacy_unclassified_projection";

    public const string ExplicitProjectionSource = "explicit";
    public const string SynthesizedMinimumProjectionSource = "synthesized_minimum";
    public const string NotRequiredProjectionSource = "not_required";
    public const string MissingProjectionSource = "missing";
    public const string AutoMinimumContractPolicy = "auto_minimum_contract";
    public const string NotRequiredPolicy = "not_required";

    public const string MetadataIngressKey = "acceptance_contract_materialization_ingress";
    public const string MetadataStateKey = "acceptance_contract_materialization_state";
    public const string MetadataRequiredKey = "acceptance_contract_materialization_required";
    public const string MetadataReasonCodeKey = "acceptance_contract_materialization_reason_code";
    public const string MetadataProjectionSourceKey = "acceptance_contract_projection_source";
    public const string MetadataProjectionPolicyKey = "acceptance_contract_projection_policy";
    public const string MetadataContractIdKey = "acceptance_contract_projection_contract_id";
    public const string MetadataSummaryKey = "acceptance_contract_materialization_summary";

    private const string IngressId = "taskgraph_draft_approval_ingress";

    public static string ResolveProjectionSource(bool explicitContractProvided)
    {
        return explicitContractProvided ? ExplicitProjectionSource : SynthesizedMinimumProjectionSource;
    }

    public static string BuildProjectionReason(bool explicitContractProvided)
    {
        return explicitContractProvided
            ? "Taskgraph draft payload supplied an explicit acceptance_contract object."
            : "Taskgraph draft payload omitted acceptance_contract; Runtime synthesized a bounded minimum contract during planning ingress.";
    }

    public static TaskGraphAcceptanceContractMaterializationReport Evaluate(TaskGraphDraftRecord draft)
    {
        var enforceMaterialization = draft.Status == PlanningDraftStatus.Draft;
        var tasks = draft.Tasks.Select(task => EvaluateTask(task, enforceMaterialization)).ToArray();
        var failures = enforceMaterialization
            ? tasks.Where(task => task.BlocksMaterialization).ToArray()
            : [];
        var executableCount = tasks.Count(task => task.RequiresContract);
        var explicitCount = tasks.Count(task =>
            task.RequiresContract
            && string.Equals(task.ProjectionSource, ExplicitProjectionSource, StringComparison.Ordinal));
        var synthesizedCount = tasks.Count(task =>
            task.RequiresContract
            && string.Equals(task.ProjectionSource, SynthesizedMinimumProjectionSource, StringComparison.Ordinal));

        return new TaskGraphAcceptanceContractMaterializationReport(
            ResolveReportState(draft, failures),
            failures.Length > 0,
            tasks.Length,
            executableCount,
            explicitCount,
            synthesizedCount,
            failures.Length,
            !enforceMaterialization
                ? $"Taskgraph draft {draft.DraftId} is already {draft.Status.ToString().ToLowerInvariant()}; materialization guard is readback-only for this draft."
                : failures.Length == 0
                    ? $"Taskgraph draft {draft.DraftId} has acceptance contract projection for all {executableCount} executable task(s)."
                : $"Taskgraph draft {draft.DraftId} has {failures.Length} executable task(s) blocked by acceptance contract materialization gaps.",
            !enforceMaterialization
                ? "Inspect the materialized task truth for current execution contract state; do not re-approve an already materialized draft."
                : failures.Length == 0
                    ? $"Approve taskgraph draft {draft.DraftId} when dependency and cycle checks also pass."
                : $"Recreate or update taskgraph draft {draft.DraftId} so every executable task records an explicit or policy-synthesized acceptance contract projection before approval.",
            tasks,
            failures);
    }

    public static void EnsureCanMaterialize(TaskGraphDraftRecord draft)
    {
        var report = Evaluate(draft);
        if (!report.BlocksMaterialization)
        {
            return;
        }

        var firstFailure = report.Failures[0];
        throw new InvalidOperationException(
            $"TaskGraph draft '{draft.DraftId}' cannot materialize because task '{firstFailure.TaskId}' has acceptance contract materialization gap '{firstFailure.ReasonCode}'. {firstFailure.RecommendedAction}");
    }

    public static IReadOnlyDictionary<string, string> BuildMaterializationMetadata(TaskGraphDraftTask task)
    {
        var projection = EvaluateTask(task, enforceMaterialization: true);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MetadataIngressKey] = IngressId,
            [MetadataStateKey] = projection.State,
            [MetadataRequiredKey] = projection.RequiresContract.ToString().ToLowerInvariant(),
            [MetadataReasonCodeKey] = projection.ReasonCode,
            [MetadataProjectionSourceKey] = projection.ProjectionSource,
            [MetadataProjectionPolicyKey] = projection.ProjectionPolicy,
            [MetadataContractIdKey] = projection.ContractId ?? string.Empty,
            [MetadataSummaryKey] = projection.Summary,
        };
    }

    private static string ResolveReportState(TaskGraphDraftRecord draft, IReadOnlyList<TaskGraphAcceptanceContractMaterializationTaskProjection> failures)
    {
        if (draft.Status != PlanningDraftStatus.Draft)
        {
            return AlreadyMaterializedReadbackState;
        }

        return failures.Count == 0 ? ReadyState : BlockedByAcceptanceContractGapState;
    }

    private static TaskGraphAcceptanceContractMaterializationTaskProjection EvaluateTask(
        TaskGraphDraftTask task,
        bool enforceMaterialization)
    {
        var requiresContract = task.TaskType.CanExecuteInWorker();
        if (!requiresContract)
        {
            return new TaskGraphAcceptanceContractMaterializationTaskProjection(
                task.TaskId,
                task.TaskType,
                false,
                task.AcceptanceContract is not null,
                task.AcceptanceContract?.ContractId,
                NormalizeValue(task.AcceptanceContractProjectionSource, NotRequiredProjectionSource),
                NormalizeValue(task.AcceptanceContractProjectionPolicy, NotRequiredPolicy),
                NotRequiredState,
                false,
                "not_required",
                $"Task {task.TaskId} is {task.TaskType} and is not a worker-executable materialization target.",
                "Follow the task-type specific governed lane.");
        }

        if (task.AcceptanceContract is null)
        {
            if (!enforceMaterialization)
            {
                return new TaskGraphAcceptanceContractMaterializationTaskProjection(
                    task.TaskId,
                    task.TaskType,
                    true,
                    false,
                    null,
                    MissingProjectionSource,
                    NormalizeValue(task.AcceptanceContractProjectionPolicy, AutoMinimumContractPolicy),
                    LegacyUnverifiedProjectionState,
                    false,
                    "legacy_approved_without_acceptance_contract_projection",
                    $"Executable draft task {task.TaskId} has no draft-level acceptance contract projection in this already materialized legacy readback.",
                    "Inspect the materialized task truth for current acceptance contract state.");
            }

            return new TaskGraphAcceptanceContractMaterializationTaskProjection(
                task.TaskId,
                task.TaskType,
                true,
                false,
                null,
                MissingProjectionSource,
                NormalizeValue(task.AcceptanceContractProjectionPolicy, AutoMinimumContractPolicy),
                MissingContractState,
                true,
                "acceptance_contract_missing",
                $"Executable task {task.TaskId} has no acceptance contract projection.",
                $"Recreate or update taskgraph draft task {task.TaskId} through planning ingress so Runtime can record an explicit or synthesized minimum acceptance contract.");
        }

        var source = NormalizeValue(task.AcceptanceContractProjectionSource, MissingProjectionSource);
        if (!IsAllowedProjectionSource(source))
        {
            if (!enforceMaterialization)
            {
                return new TaskGraphAcceptanceContractMaterializationTaskProjection(
                    task.TaskId,
                    task.TaskType,
                    true,
                    true,
                    task.AcceptanceContract.ContractId,
                    source,
                    NormalizeValue(task.AcceptanceContractProjectionPolicy, AutoMinimumContractPolicy),
                    LegacyUnclassifiedProjectionState,
                    false,
                    "legacy_acceptance_contract_projection_source_unclassified",
                    $"Executable draft task {task.TaskId} has acceptance contract {task.AcceptanceContract.ContractId}, but the draft-level projection source is unclassified in this already materialized legacy readback.",
                    "Inspect the materialized task truth for current acceptance contract source metadata.");
            }

            return new TaskGraphAcceptanceContractMaterializationTaskProjection(
                task.TaskId,
                task.TaskType,
                true,
                true,
                task.AcceptanceContract.ContractId,
                source,
                NormalizeValue(task.AcceptanceContractProjectionPolicy, AutoMinimumContractPolicy),
                MissingProjectionSourceState,
                true,
                "acceptance_contract_projection_source_missing",
                $"Executable task {task.TaskId} has acceptance contract {task.AcceptanceContract.ContractId}, but the materialization source is not recorded as explicit or synthesized_minimum.",
                $"Recreate or update taskgraph draft task {task.TaskId} through planning ingress so the contract projection source is recorded before approval.");
        }

        return new TaskGraphAcceptanceContractMaterializationTaskProjection(
            task.TaskId,
            task.TaskType,
            true,
            true,
            task.AcceptanceContract.ContractId,
            source,
            NormalizeValue(task.AcceptanceContractProjectionPolicy, AutoMinimumContractPolicy),
            ProjectedState,
            false,
            "none",
            $"Executable task {task.TaskId} has acceptance contract {task.AcceptanceContract.ContractId} projected from {source}.",
            $"Task {task.TaskId} can materialize when dependency and cycle checks also pass.");
    }

    private static bool IsAllowedProjectionSource(string source)
    {
        return string.Equals(source, ExplicitProjectionSource, StringComparison.Ordinal)
            || string.Equals(source, SynthesizedMinimumProjectionSource, StringComparison.Ordinal);
    }

    private static string NormalizeValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
