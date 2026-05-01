using Carves.Runtime.Domain.Cards;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;
using Carves.Runtime.Application.CodeGraph;

namespace Carves.Runtime.Application.Planning;

public sealed class TaskDecomposer
{
    private static readonly TaskTypePolicy TaskTypePolicy = TaskTypePolicy.Default;

    public IReadOnlyList<TaskNode> Decompose(
        CardDefinition card,
        string baseCommit,
        IReadOnlyList<IReadOnlyList<string>> validationCommands,
        CodeGraphScopeAnalysis? scopeAnalysis = null,
        CodeGraphImpactAnalysis? impactAnalysis = null)
    {
        var metadata = BuildMetadata(card, scopeAnalysis, impactAnalysis);
        var capabilities = BuildCapabilities(scopeAnalysis, impactAnalysis);
        const TaskType taskType = TaskType.Execution;

        if (!TaskTypePolicy.AllowPlannerGeneration(taskType))
        {
            throw new InvalidOperationException("Card decomposition must produce planner-generatable task types.");
        }

        return
        [
            new TaskNode
            {
                TaskId = BuildTaskId(card.CardId, 1),
                Title = $"Shape interfaces for {card.Title}",
                Description = "Map affected files and define the smallest implementation boundary.",
                Status = DomainTaskStatus.Pending,
                TaskType = taskType,
                Priority = card.Priority,
                Source = "PLANNER",
                CardId = card.CardId,
                ProposalSource = TaskProposalSource.CardDecomposition,
                ProposalReason = $"card {card.CardId} shape step",
                ProposalConfidence = 1.0,
                ProposalPriorityHint = card.Priority,
                BaseCommit = baseCommit,
                Scope = card.Scope,
                Acceptance = ["affected files and interfaces are identified", "task boundary is explicit"],
                Constraints = card.Constraints,
                AcceptanceContract = AcceptanceContractFactory.NormalizeTaskContract(
                    BuildTaskId(card.CardId, 1),
                    $"Shape interfaces for {card.Title}",
                    "Map affected files and define the smallest implementation boundary.",
                    card.CardId,
                    ["affected files and interfaces are identified", "task boundary is explicit"],
                    card.Constraints,
                    new ValidationPlan
                    {
                        Checks = ["TaskGraph persists the scoped task correctly"],
                        ExpectedEvidence = ["task node exists in .ai/tasks/nodes/"],
                    },
                    sourceContract: card.AcceptanceContract),
                Capabilities = capabilities,
                Metadata = metadata,
                Validation = new ValidationPlan
                {
                    Checks = ["TaskGraph persists the scoped task correctly"],
                    ExpectedEvidence = ["task node exists in .ai/tasks/nodes/"],
                },
            },
            new TaskNode
            {
                TaskId = BuildTaskId(card.CardId, 2),
                Title = $"Implement {card.Title}",
                Description = string.IsNullOrWhiteSpace(card.Goal) ? $"Implement the core behavior for {card.Title}." : card.Goal,
                Status = DomainTaskStatus.Pending,
                TaskType = taskType,
                Priority = card.Priority,
                Source = "PLANNER",
                CardId = card.CardId,
                ProposalSource = TaskProposalSource.CardDecomposition,
                ProposalReason = $"card {card.CardId} implementation step",
                ProposalConfidence = 1.0,
                ProposalPriorityHint = card.Priority,
                BaseCommit = baseCommit,
                Dependencies = [BuildTaskId(card.CardId, 1)],
                Scope = card.Scope,
                Acceptance = card.Acceptance,
                Constraints = card.Constraints,
                AcceptanceContract = AcceptanceContractFactory.NormalizeTaskContract(
                    BuildTaskId(card.CardId, 2),
                    $"Implement {card.Title}",
                    string.IsNullOrWhiteSpace(card.Goal) ? $"Implement the core behavior for {card.Title}." : card.Goal,
                    card.CardId,
                    card.Acceptance,
                    card.Constraints,
                    new ValidationPlan
                    {
                        Checks = ["Implementation changes stay within scope", "Patch remains reviewable"],
                        ExpectedEvidence = ["patch file saved under .ai/patches/"],
                    },
                    sourceContract: card.AcceptanceContract),
                Capabilities = capabilities,
                Metadata = metadata,
                Validation = new ValidationPlan
                {
                    Checks = ["Implementation changes stay within scope", "Patch remains reviewable"],
                    ExpectedEvidence = ["patch file saved under .ai/patches/"],
                },
            },
            new TaskNode
            {
                TaskId = BuildTaskId(card.CardId, 3),
                Title = $"Validate {card.Title}",
                Description = "Add or update tests and runtime validation for the implemented behavior.",
                Status = DomainTaskStatus.Pending,
                TaskType = taskType,
                Priority = card.Priority,
                Source = "PLANNER",
                CardId = card.CardId,
                ProposalSource = TaskProposalSource.CardDecomposition,
                ProposalReason = $"card {card.CardId} validation step",
                ProposalConfidence = 1.0,
                ProposalPriorityHint = card.Priority,
                BaseCommit = baseCommit,
                Dependencies = [BuildTaskId(card.CardId, 2)],
                Scope = card.Scope.Concat(["tests/"]).Distinct(StringComparer.Ordinal).ToArray(),
                Acceptance = ["validation evidence recorded", "tests relevant to the change pass"],
                Constraints = card.Constraints,
                AcceptanceContract = AcceptanceContractFactory.NormalizeTaskContract(
                    BuildTaskId(card.CardId, 3),
                    $"Validate {card.Title}",
                    "Add or update tests and runtime validation for the implemented behavior.",
                    card.CardId,
                    ["validation evidence recorded", "tests relevant to the change pass"],
                    card.Constraints,
                    new ValidationPlan
                    {
                        Commands = validationCommands,
                        Checks = ["tests pass"],
                        ExpectedEvidence = ["test runner output"],
                    },
                    sourceContract: card.AcceptanceContract),
                Capabilities = capabilities,
                Metadata = metadata,
                Validation = new ValidationPlan
                {
                    Commands = validationCommands,
                    Checks = ["tests pass"],
                    ExpectedEvidence = ["test runner output"],
                },
            },
        ];
    }

    public IReadOnlyList<TaskNode> DecomposePlanningTask(
        TaskNode planningTask,
        string baseCommit,
        IReadOnlyList<IReadOnlyList<string>> validationCommands,
        CodeGraphScopeAnalysis? scopeAnalysis = null,
        CodeGraphImpactAnalysis? impactAnalysis = null)
    {
        var metadata = MergeMetadata(
            planningTask.Metadata,
            BuildCodeGraphMetadata(scopeAnalysis, impactAnalysis),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["derived_from_planning_task_id"] = planningTask.TaskId,
            });
        var capabilities = BuildCapabilities(scopeAnalysis, impactAnalysis);
        var titleSeed = planningTask.Title.StartsWith("Plan ", StringComparison.OrdinalIgnoreCase)
            ? planningTask.Title["Plan ".Length..]
            : planningTask.Title;
        var scope = planningTask.Scope.Count == 0 ? [".ai/opportunities/index.json"] : planningTask.Scope;
        const DomainTaskStatus initialStatus = DomainTaskStatus.Suggested;

        return
        [
            new TaskNode
            {
                TaskId = $"{planningTask.TaskId}-001",
                Title = $"Shape implementation for {titleSeed}",
                Description = "Define the smallest execution boundary for this planner-generated opportunity.",
                Status = initialStatus,
                TaskType = TaskType.Execution,
                Priority = planningTask.Priority,
                Source = "PLANNER_OPPORTUNITY",
                ProposalSource = planningTask.ProposalSource,
                ProposalReason = planningTask.ProposalReason,
                ProposalConfidence = planningTask.ProposalConfidence,
                ProposalPriorityHint = planningTask.ProposalPriorityHint,
                BaseCommit = baseCommit,
                Dependencies = [planningTask.TaskId],
                Scope = scope,
                Acceptance = ["affected implementation files are explicit", "execution stays scoped to the originating opportunity"],
                Constraints = planningTask.Constraints,
                AcceptanceContract = AcceptanceContractFactory.NormalizeTaskContract(
                    $"{planningTask.TaskId}-001",
                    $"Shape implementation for {titleSeed}",
                    "Define the smallest execution boundary for this planner-generated opportunity.",
                    planningTask.CardId,
                    ["affected implementation files are explicit", "execution stays scoped to the originating opportunity"],
                    planningTask.Constraints,
                    new ValidationPlan
                    {
                        Checks = ["planner-generated execution scope is explicit"],
                        ExpectedEvidence = ["task node persists opportunity lineage"],
                    },
                    sourceContract: planningTask.AcceptanceContract),
                Capabilities = capabilities,
                Metadata = metadata,
                Validation = new ValidationPlan
                {
                    Checks = ["planner-generated execution scope is explicit"],
                    ExpectedEvidence = ["task node persists opportunity lineage"],
                },
            },
            new TaskNode
            {
                TaskId = $"{planningTask.TaskId}-002",
                Title = $"Implement {titleSeed}",
                Description = planningTask.Description,
                Status = initialStatus,
                TaskType = TaskType.Execution,
                Priority = planningTask.Priority,
                Source = "PLANNER_OPPORTUNITY",
                ProposalSource = planningTask.ProposalSource,
                ProposalReason = planningTask.ProposalReason,
                ProposalConfidence = planningTask.ProposalConfidence,
                ProposalPriorityHint = planningTask.ProposalPriorityHint,
                BaseCommit = baseCommit,
                Dependencies = [$"{planningTask.TaskId}-001"],
                Scope = scope,
                Acceptance = ["implementation closes the originating opportunity"],
                Constraints = planningTask.Constraints,
                AcceptanceContract = AcceptanceContractFactory.NormalizeTaskContract(
                    $"{planningTask.TaskId}-002",
                    $"Implement {titleSeed}",
                    planningTask.Description,
                    planningTask.CardId,
                    ["implementation closes the originating opportunity"],
                    planningTask.Constraints,
                    new ValidationPlan
                    {
                        Checks = ["implementation remains governed and reviewable"],
                        ExpectedEvidence = ["task node remains linked to planning truth"],
                    },
                    sourceContract: planningTask.AcceptanceContract),
                Capabilities = capabilities,
                Metadata = metadata,
                Validation = new ValidationPlan
                {
                    Checks = ["implementation remains governed and reviewable"],
                    ExpectedEvidence = ["task node remains linked to planning truth"],
                },
            },
            new TaskNode
            {
                TaskId = $"{planningTask.TaskId}-003",
                Title = $"Validate {titleSeed}",
                Description = "Add or update validation for the planner-generated execution work.",
                Status = initialStatus,
                TaskType = TaskType.Execution,
                Priority = planningTask.Priority,
                Source = "PLANNER_OPPORTUNITY",
                ProposalSource = planningTask.ProposalSource,
                ProposalReason = planningTask.ProposalReason,
                ProposalConfidence = planningTask.ProposalConfidence,
                ProposalPriorityHint = planningTask.ProposalPriorityHint,
                BaseCommit = baseCommit,
                Dependencies = [$"{planningTask.TaskId}-002"],
                Scope = scope.Concat(["tests/"]).Distinct(StringComparer.Ordinal).ToArray(),
                Acceptance = ["validation evidence exists for the originating opportunity"],
                Constraints = planningTask.Constraints,
                AcceptanceContract = AcceptanceContractFactory.NormalizeTaskContract(
                    $"{planningTask.TaskId}-003",
                    $"Validate {titleSeed}",
                    "Add or update validation for the planner-generated execution work.",
                    planningTask.CardId,
                    ["validation evidence exists for the originating opportunity"],
                    planningTask.Constraints,
                    new ValidationPlan
                    {
                        Commands = validationCommands,
                        Checks = ["tests pass"],
                        ExpectedEvidence = ["test runner output"],
                    },
                    sourceContract: planningTask.AcceptanceContract),
                Capabilities = capabilities,
                Metadata = metadata,
                Validation = new ValidationPlan
                {
                    Commands = validationCommands,
                    Checks = ["tests pass"],
                    ExpectedEvidence = ["test runner output"],
                },
            },
        ];
    }

    private static string BuildTaskId(string cardId, int index)
    {
        return $"T-{cardId}-{index:000}";
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(
        CardDefinition card,
        CodeGraphScopeAnalysis? scopeAnalysis,
        CodeGraphImpactAnalysis? impactAnalysis)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["card_type"] = card.CardType,
            ["planner_step_model"] = "shape -> implement -> validate",
        };

        return MergeMetadata(metadata, BuildCodeGraphMetadata(scopeAnalysis, impactAnalysis));
    }

    private static IReadOnlyDictionary<string, string> BuildCodeGraphMetadata(
        CodeGraphScopeAnalysis? scopeAnalysis,
        CodeGraphImpactAnalysis? impactAnalysis)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        if (scopeAnalysis?.HasMatches != true)
        {
            return metadata;
        }

        metadata["codegraph_modules"] = Join(scopeAnalysis.Modules);
        metadata["codegraph_files"] = Join(scopeAnalysis.Files);

        if (scopeAnalysis.Callables.Count > 0)
        {
            metadata["codegraph_callables"] = Join(scopeAnalysis.Callables);
        }

        if (scopeAnalysis.DependencyModules.Count > 0)
        {
            metadata["codegraph_dependency_modules"] = Join(scopeAnalysis.DependencyModules);
        }

        if (scopeAnalysis.SummaryLines.Count > 0)
        {
            metadata["codegraph_summary"] = Join(scopeAnalysis.SummaryLines, " | ");
        }

        if (impactAnalysis?.HasMatches == true)
        {
            if (impactAnalysis.ImpactedModules.Count > 0)
            {
                metadata["codegraph_impacted_modules"] = Join(impactAnalysis.ImpactedModules);
            }

            if (impactAnalysis.ImpactedFiles.Count > 0)
            {
                metadata["codegraph_impacted_files"] = Join(impactAnalysis.ImpactedFiles);
            }

            if (impactAnalysis.SummaryLines.Count > 0)
            {
                metadata["codegraph_impact_summary"] = Join(impactAnalysis.SummaryLines, " | ");
            }
        }

        return metadata;
    }

    private static IReadOnlyDictionary<string, string> MergeMetadata(params IReadOnlyDictionary<string, string>[] dictionaries)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var dictionary in dictionaries)
        {
            foreach (var pair in dictionary)
            {
                metadata[pair.Key] = pair.Value;
            }
        }

        return metadata;
    }

    private static IReadOnlyList<string> BuildCapabilities(CodeGraphScopeAnalysis? scopeAnalysis, CodeGraphImpactAnalysis? impactAnalysis)
    {
        var capabilities = new List<string>();

        if (scopeAnalysis?.HasMatches == true)
        {
            capabilities.Add("codegraph_scope_analysis");
        }

        if (impactAnalysis?.HasMatches == true)
        {
            capabilities.Add("codegraph_impact_analysis");
        }

        return capabilities;
    }

    private static string Join(IReadOnlyList<string> values, string separator = ", ")
    {
        return string.Join(separator, values.Take(6));
    }
}
