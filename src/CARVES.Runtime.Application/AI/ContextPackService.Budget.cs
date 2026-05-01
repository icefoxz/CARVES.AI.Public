using System.Text;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.AI;

public sealed partial class ContextPackService
{
    private ContextPack ApplyBudget(ContextPackDraft draft, string? model, int? overrideMaxContextTokens)
    {
        var policy = ContextBudgetPolicyResolver.Resolve(draft.Audience, model, overrideMaxContextTokens);
        var goal = draft.Goal;
        var task = draft.Task;
        var constraints = draft.Constraints.ToList();
        var localTaskGraph = new TaskGraphLocalProjection
        {
            CurrentTaskId = draft.LocalTaskGraph.CurrentTaskId,
            CurrentTaskTitle = draft.LocalTaskGraph.CurrentTaskTitle,
            Dependencies = draft.LocalTaskGraph.Dependencies.ToArray(),
            Blockers = draft.LocalTaskGraph.Blockers.ToArray(),
        };
        var relevantModules = draft.RelevantModules.ToList();
        var recall = draft.Recall.ToList();
        var codeHints = draft.CodeHints.ToList();
        var windowedReads = draft.WindowedReads.ToList();
        var lastFailure = draft.LastFailureSummary;
        var lastRun = draft.LastRunSummary;
        var trimmed = new List<ContextPackTrimmedItem>();
        var initialRender = RenderPrompt(
            draft.Goal,
            draft.Task,
            draft.Constraints,
            draft.AcceptanceContract,
            draft.LocalTaskGraph,
            draft.RelevantModules,
            draft.FacetNarrowing,
            draft.Recall,
            draft.CodeHints,
            draft.WindowedReads,
            BuildCompaction(draft.Compaction, draft.WindowedReads.Count, 0),
            draft.LastFailureSummary,
            draft.LastRunSummary);
        var initialPromptTokens = ContextBudgetPolicyResolver.EstimateTokens(initialRender.Text);

        TrimCoreToBudget(policy, ref goal, ref task, constraints, trimmed);

        var renderedPrompt = RenderPrompt(
            goal,
            task,
            constraints,
            draft.AcceptanceContract,
            localTaskGraph,
            relevantModules,
            draft.FacetNarrowing,
            recall,
            codeHints,
            windowedReads,
            BuildCompaction(draft.Compaction, windowedReads.Count, trimmed.Count),
            lastFailure,
            lastRun);
        var prompt = renderedPrompt.Text;
        while (ContextBudgetPolicyResolver.EstimateTokens(prompt) > policy.MaxContextTokens)
        {
            if (lastRun is not null)
            {
                trimmed.Add(NewTrimmed("last_run_summary", ContextPackLayer.Relevant, ContextPackPriority.History, lastRun.Summary, "budget_trim"));
                lastRun = null;
            }
            else if (recall.Count > 0)
            {
                var removed = recall[^1];
                trimmed.Add(NewTrimmed($"recall:{removed.Source}", ContextPackLayer.Relevant, ContextPackPriority.Modules, removed.Text, "budget_trim"));
                recall.RemoveAt(recall.Count - 1);
            }
            else if (relevantModules.Count > 3)
            {
                var removed = relevantModules[^1];
                trimmed.Add(NewTrimmed($"module:{removed.Module}", ContextPackLayer.Relevant, ContextPackPriority.Modules, removed.Summary, "budget_trim"));
                relevantModules.RemoveAt(relevantModules.Count - 1);
            }
            else if (codeHints.Count > 3)
            {
                var removed = codeHints[^1];
                trimmed.Add(NewTrimmed("code_hint", ContextPackLayer.Relevant, ContextPackPriority.Modules, removed, "budget_trim"));
                codeHints.RemoveAt(codeHints.Count - 1);
            }
            else if (windowedReads.Count > 2)
            {
                var removed = windowedReads[^1];
                trimmed.Add(NewTrimmed($"windowed_read:{removed.Path}", ContextPackLayer.Relevant, ContextPackPriority.Modules, removed.Snippet, "budget_trim"));
                windowedReads.RemoveAt(windowedReads.Count - 1);
            }
            else if (relevantModules.Count > 1)
            {
                var removed = relevantModules[^1];
                trimmed.Add(NewTrimmed($"module:{removed.Module}", ContextPackLayer.Relevant, ContextPackPriority.Modules, removed.Summary, "budget_trim"));
                relevantModules.RemoveAt(relevantModules.Count - 1);
            }
            else if (relevantModules.Count > 0 && relevantModules[^1].Files.Count > 0)
            {
                var current = relevantModules[^1];
                trimmed.Add(NewTrimmed($"module_files:{current.Module}", ContextPackLayer.Relevant, ContextPackPriority.Modules, string.Join(", ", current.Files), "budget_trim"));
                relevantModules[^1] = current with { Files = Array.Empty<string>() };
            }
            else if (lastFailure is not null)
            {
                trimmed.Add(NewTrimmed("last_failure_summary", ContextPackLayer.Relevant, ContextPackPriority.Failure, lastFailure.Reason, "budget_trim"));
                lastFailure = null;
            }
            else if (constraints.Count > 2)
            {
                var removed = constraints[^1];
                trimmed.Add(NewTrimmed("constraint", ContextPackLayer.Core, ContextPackPriority.Task, removed, "budget_trim"));
                constraints.RemoveAt(constraints.Count - 1);
            }
            else if (localTaskGraph.Blockers.Count > 0)
            {
                var blockers = localTaskGraph.Blockers.ToList();
                var removed = blockers[^1];
                trimmed.Add(NewTrimmed($"blocker:{removed.TaskId}", ContextPackLayer.Relevant, ContextPackPriority.Task, removed.Title, "budget_trim"));
                blockers.RemoveAt(blockers.Count - 1);
                localTaskGraph = localTaskGraph with { Blockers = blockers.ToArray() };
            }
            else if (localTaskGraph.Dependencies.Count > 0)
            {
                var dependencies = localTaskGraph.Dependencies.ToList();
                var removed = dependencies[^1];
                trimmed.Add(NewTrimmed($"dependency:{removed.TaskId}", ContextPackLayer.Relevant, ContextPackPriority.Task, removed.Title, "budget_trim"));
                dependencies.RemoveAt(dependencies.Count - 1);
                localTaskGraph = localTaskGraph with { Dependencies = dependencies.ToArray() };
            }
            else if (goal.Length > 180)
            {
                trimmed.Add(NewTrimmed("goal", ContextPackLayer.Core, ContextPackPriority.Goal, goal, "budget_trim"));
                goal = Truncate(goal, 180);
            }
            else if (task.Length > 360)
            {
                trimmed.Add(NewTrimmed("task", ContextPackLayer.Core, ContextPackPriority.Task, task, "budget_trim"));
                task = Truncate(task, 360);
            }
            else if (constraints.Count > 0)
            {
                var removed = constraints[^1];
                trimmed.Add(NewTrimmed("constraint", ContextPackLayer.Core, ContextPackPriority.Task, removed, "budget_trim"));
                constraints.RemoveAt(constraints.Count - 1);
            }
            else if (relevantModules.Count > 0)
            {
                var removed = relevantModules[^1];
                trimmed.Add(NewTrimmed($"module:{removed.Module}", ContextPackLayer.Relevant, ContextPackPriority.Modules, removed.Summary, "budget_trim"));
                relevantModules.RemoveAt(relevantModules.Count - 1);
            }
            else if (goal.Length > 32)
            {
                trimmed.Add(NewTrimmed("goal", ContextPackLayer.Core, ContextPackPriority.Goal, goal, "budget_trim"));
                goal = Truncate(goal, Math.Max(32, goal.Length - 24));
            }
            else if (task.Length > 64)
            {
                trimmed.Add(NewTrimmed("task", ContextPackLayer.Core, ContextPackPriority.Task, task, "budget_trim"));
                task = Truncate(task, Math.Max(64, task.Length - 48));
            }
            else
            {
                break;
            }

            renderedPrompt = RenderPrompt(
                goal,
                task,
                constraints,
                draft.AcceptanceContract,
                localTaskGraph,
                relevantModules,
                draft.FacetNarrowing,
                recall,
                codeHints,
                windowedReads,
                BuildCompaction(draft.Compaction, windowedReads.Count, trimmed.Count),
                lastFailure,
                lastRun);
            prompt = renderedPrompt.Text;
        }

        var usedTokens = ContextBudgetPolicyResolver.EstimateTokens(prompt);
        var largestContributors = BuildBudgetContributors(
            goal,
            task,
            constraints,
            localTaskGraph,
            relevantModules,
            recall,
            codeHints,
            windowedReads,
            lastFailure,
            lastRun);
        var posture = DetermineBudgetPosture(policy, initialPromptTokens, usedTokens);
        var reasonCodes = BuildReasonCodes(
            draft,
            initialPromptTokens,
            usedTokens,
            trimmed,
            recall,
            windowedReads,
            lastFailure,
            lastRun);
        var topSources = BuildTopSources(recall, relevantModules, draft.ExpandableReferences);
        var dynamicTokensEstimate = recall.Sum(item => item.TokenEstimate);
        var fixedTokensEstimate = Math.Max(0, usedTokens - dynamicTokensEstimate);
        return new ContextPack
        {
            PackId = draft.PackId,
            Audience = draft.Audience,
            TaskId = draft.TaskId,
            ArtifactPath = draft.ArtifactPath,
            Goal = goal,
            Task = task,
            Constraints = constraints,
            AcceptanceContract = draft.AcceptanceContract,
            LocalTaskGraph = localTaskGraph,
            RelevantModules = relevantModules,
            FacetNarrowing = draft.FacetNarrowing,
            Recall = recall,
            CodeHints = codeHints,
            WindowedReads = windowedReads,
            LastFailureSummary = lastFailure,
            LastRunSummary = lastRun,
            ExpandableReferences = draft.ExpandableReferences,
            Budget = new ContextPackBudget
            {
                PolicyVersion = policy.PolicyId,
                ProfileId = policy.ProfileId,
                Model = policy.Model,
                EstimatorVersion = policy.EstimatorVersion,
                ModelLimitTokens = policy.ModelLimitTokens,
                TargetTokens = policy.TargetTokens,
                AdvisoryTokens = policy.AdvisoryTokens,
                HardSafetyTokens = policy.HardSafetyTokens,
                MaxContextTokens = policy.MaxContextTokens,
                ReservedHeadroomTokens = policy.ReservedHeadroomTokens,
                CoreBudgetTokens = policy.CoreBudgetTokens,
                RelevantBudgetTokens = policy.RelevantBudgetTokens,
                UsedTokens = usedTokens,
                TrimmedTokens = trimmed.Sum(item => item.EstimatedTokens),
                FixedTokensEstimate = fixedTokensEstimate,
                DynamicTokensEstimate = dynamicTokensEstimate,
                TotalContextTokensEstimate = fixedTokensEstimate + dynamicTokensEstimate,
                BudgetPosture = posture,
                BudgetViolationReasonCodes = reasonCodes,
                LargestContributors = largestContributors,
                L3QueryCount = recall.Count == 0 ? 0 : 1,
                TruncatedItemsCount = trimmed.Count,
                DroppedItemsCount = 0,
                FullDocBlockedCount = draft.ExpandableReferences.Count,
                TopSources = topSources,
            },
            Trimmed = trimmed,
            Compaction = BuildCompaction(draft.Compaction, windowedReads.Count, trimmed.Count),
            PromptInput = prompt,
            PromptSections = renderedPrompt.Sections,
        };
    }

    private static void TrimCoreToBudget(
        ContextBudgetPolicy policy,
        ref string goal,
        ref string task,
        IReadOnlyList<string> constraints,
        ICollection<ContextPackTrimmedItem> trimmed)
    {
        var coreTokens = EstimateCoreTokens(goal, task, constraints);
        while (coreTokens > policy.CoreBudgetTokens)
        {
            if (task.Length > 420)
            {
                trimmed.Add(NewTrimmed("task", ContextPackLayer.Core, ContextPackPriority.Task, task, "core_budget_trim"));
                task = Truncate(task, task.Length - 120);
            }
            else if (goal.Length > 180)
            {
                trimmed.Add(NewTrimmed("goal", ContextPackLayer.Core, ContextPackPriority.Goal, goal, "core_budget_trim"));
                goal = Truncate(goal, goal.Length - 60);
            }
            else
            {
                break;
            }

            coreTokens = EstimateCoreTokens(goal, task, constraints);
        }
    }

    private static int EstimateCoreTokens(string goal, string task, IReadOnlyList<string> constraints)
    {
        return ContextBudgetPolicyResolver.EstimateTokens(goal)
               + ContextBudgetPolicyResolver.EstimateTokens(task)
               + ContextBudgetPolicyResolver.EstimateTokens(constraints);
    }

    private static ContextPackTrimmedItem NewTrimmed(
        string key,
        ContextPackLayer layer,
        ContextPackPriority priority,
        string content,
        string reason)
    {
        return new ContextPackTrimmedItem
        {
            Key = key,
            Layer = layer,
            Priority = priority,
            EstimatedTokens = ContextBudgetPolicyResolver.EstimateTokens(content),
            Reason = reason,
        };
    }

    private static string DetermineBudgetPosture(ContextBudgetPolicy policy, int initialPromptTokens, int usedTokens)
    {
        if (usedTokens > policy.MaxContextTokens || usedTokens > policy.HardSafetyTokens)
        {
            return ContextBudgetPostures.DegradedContext;
        }

        if (initialPromptTokens > policy.MaxContextTokens)
        {
            return ContextBudgetPostures.HardCapEnforced;
        }

        if (usedTokens > policy.AdvisoryTokens)
        {
            return ContextBudgetPostures.OverAdvisory;
        }

        if (usedTokens > policy.TargetTokens)
        {
            return ContextBudgetPostures.OverTarget;
        }

        return ContextBudgetPostures.WithinTarget;
    }

    private static IReadOnlyList<string> BuildReasonCodes(
        ContextPackDraft draft,
        int initialPromptTokens,
        int usedTokens,
        IReadOnlyList<ContextPackTrimmedItem> trimmed,
        IReadOnlyList<ContextPackRecallItem> recall,
        IReadOnlyList<ContextPackWindowedRead> windowedReads,
        CompactFailureSummary? lastFailure,
        ExecutionHistorySummary? lastRun)
    {
        var reasons = new List<string>();
        if (draft.ExpandableReferences.Count > 0)
        {
            reasons.Add(ContextBudgetReasonCodes.FullDocBlocked);
        }

        if (draft.Compaction.CandidateFileCount > 3
            || draft.Compaction.OmittedFileCount > 0
            || windowedReads.Any(item => item.Truncated)
            || draft.RelevantModules.Count > 3)
        {
            reasons.Add(ContextBudgetReasonCodes.RepoScale);
        }

        if (initialPromptTokens > 700
            || trimmed.Count > 0
            || recall.Count > 0
            || draft.LocalTaskGraph.Dependencies.Count > 0
            || draft.LocalTaskGraph.Blockers.Count > 0
            || draft.Constraints.Count > 4)
        {
            reasons.Add(ContextBudgetReasonCodes.TaskComplexity);
        }

        if (lastFailure is not null || lastRun is not null)
        {
            reasons.Add(ContextBudgetReasonCodes.EvidenceRequired);
        }

        if (windowedReads.Any(item => item.Truncated) || Math.Abs(initialPromptTokens - usedTokens) > 200)
        {
            reasons.Add(ContextBudgetReasonCodes.EstimatorUncertain);
        }

        return reasons
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ContextPackBudgetContributor> BuildBudgetContributors(
        string goal,
        string task,
        IReadOnlyList<string> constraints,
        TaskGraphLocalProjection localTaskGraph,
        IReadOnlyList<ContextPackModuleProjection> relevantModules,
        IReadOnlyList<ContextPackRecallItem> recall,
        IReadOnlyList<string> codeHints,
        IReadOnlyList<ContextPackWindowedRead> windowedReads,
        CompactFailureSummary? lastFailure,
        ExecutionHistorySummary? lastRun)
    {
        var contributors = new List<ContextPackBudgetContributor>();
        AddContributor(contributors, "goal", goal, goal);
        AddContributor(contributors, "task", task, task);
        AddContributor(contributors, "constraints", string.Join(Environment.NewLine, constraints), string.Join("; ", constraints));
        AddContributor(
            contributors,
            "local_task_graph",
            string.Join(
                Environment.NewLine,
                new[] { localTaskGraph.CurrentTaskId, localTaskGraph.CurrentTaskTitle }
                    .Concat(localTaskGraph.Dependencies.Select(item => $"{item.TaskId}:{item.Title}:{item.Summary}"))
                    .Concat(localTaskGraph.Blockers.Select(item => $"{item.TaskId}:{item.Title}:{item.Summary}"))),
            $"dependencies={localTaskGraph.Dependencies.Count}; blockers={localTaskGraph.Blockers.Count}");
        AddContributor(
            contributors,
            "relevant_modules",
            string.Join(Environment.NewLine, relevantModules.Select(item => $"{item.Module}:{item.Summary}:{string.Join(", ", item.Files)}")),
            string.Join(", ", relevantModules.Select(item => item.Module)));
        AddContributor(
            contributors,
            "bounded_recall",
            string.Join(Environment.NewLine, recall.Select(item => $"{item.Kind}:{item.Source}:{item.Text}")),
            string.Join(", ", recall.Select(item => item.Source)));
        AddContributor(contributors, "code_hints", string.Join(Environment.NewLine, codeHints), string.Join("; ", codeHints.Take(2)));
        AddContributor(
            contributors,
            "windowed_reads",
            string.Join(Environment.NewLine, windowedReads.Select(item => $"{item.Path}:{item.StartLine}-{item.EndLine}:{item.Snippet}")),
            string.Join(", ", windowedReads.Select(item => item.Path)));
        if (lastFailure is not null)
        {
            AddContributor(
                contributors,
                "last_failure_summary",
                $"{lastFailure.FailureType}:{lastFailure.FailureLane}:{lastFailure.Reason}:{lastFailure.BuildStatus}:{lastFailure.TestStatus}:{lastFailure.RuntimeStatus}",
                lastFailure.Reason);
        }

        if (lastRun is not null)
        {
            AddContributor(
                contributors,
                "last_run_summary",
                $"{lastRun.RunId}:{lastRun.Status}:{lastRun.Summary}:{lastRun.BoundaryReason}:{lastRun.ReplanStrategy}",
                lastRun.Summary);
        }

        return contributors
            .Where(item => item.EstimatedTokens > 0)
            .OrderByDescending(item => item.EstimatedTokens)
            .ThenBy(item => item.Kind, StringComparer.Ordinal)
            .Take(5)
            .ToArray();
    }

    private static void AddContributor(ICollection<ContextPackBudgetContributor> contributors, string kind, string content, string summary)
    {
        var estimatedTokens = ContextBudgetPolicyResolver.EstimateTokens(content);
        if (estimatedTokens == 0)
        {
            return;
        }

        contributors.Add(new ContextPackBudgetContributor
        {
            Kind = kind,
            EstimatedTokens = estimatedTokens,
            Summary = Truncate(summary, 120),
        });
    }

    private static IReadOnlyList<string> BuildTopSources(
        IReadOnlyList<ContextPackRecallItem> recall,
        IReadOnlyList<ContextPackModuleProjection> relevantModules,
        IReadOnlyList<ContextPackArtifactReference> expandableReferences)
    {
        return recall
            .Select(item => $"{item.Kind}:{item.Source}")
            .Concat(relevantModules
            .Select(item => $"module:{item.Module}")
            .Concat(expandableReferences.Select(item => $"{item.Kind}:{item.Path}")))
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToArray();
    }

}
