using Carves.Runtime.Application.AI;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectContextPack(string taskId)
    {
        var task = taskGraphService.GetTask(taskId);
        var pack = contextPackService.LoadForTask(taskId) ?? contextPackService.BuildForTask(task, aiProviderConfig.Model);
        if (!string.IsNullOrWhiteSpace(pack.ArtifactPath))
        {
            var routeGraph = new RuntimeSurfaceRouteGraphService(paths);
            routeGraph.RecordRouteEdge(new Carves.Runtime.Domain.AI.RuntimeConsumerRouteEdgeRecord
            {
                SurfaceId = pack.ArtifactPath,
                Consumer = "operator.inspect_context_pack",
                DeclaredRouteKind = "operator_only",
                ObservedRouteKind = "operator_only",
                ObservedCount = 1,
                SampleCount = 1,
                FrequencyWindow = "7d",
                EvidenceSource = pack.PackId,
                LastSeen = DateTimeOffset.UtcNow,
            });
        }

        var lines = new List<string>
        {
            $"Context pack for {taskId}:",
            $"Pack id: {pack.PackId}",
            $"Audience: {pack.Audience.ToString().ToLowerInvariant()}",
            $"Artifact path: {pack.ArtifactPath ?? "(none)"}",
            $"Budget profile: {pack.Budget.ProfileId}",
            $"Budget posture: {pack.Budget.BudgetPosture}",
            $"Budget: used={pack.Budget.UsedTokens}/{pack.Budget.MaxContextTokens} tokens (target={pack.Budget.TargetTokens}, advisory={pack.Budget.AdvisoryTokens}, hard_safety={pack.Budget.HardSafetyTokens}, model_limit={pack.Budget.ModelLimitTokens}, headroom={pack.Budget.ReservedHeadroomTokens})",
            $"Core budget: {pack.Budget.CoreBudgetTokens}",
            $"Relevant budget: {pack.Budget.RelevantBudgetTokens}",
            $"Estimator: {pack.Budget.EstimatorVersion}",
            $"Fixed/dynamic/total estimate: {pack.Budget.FixedTokensEstimate} / {pack.Budget.DynamicTokensEstimate} / {pack.Budget.TotalContextTokensEstimate}",
            $"L3 queries/evidence expansions: {pack.Budget.L3QueryCount} / {pack.Budget.EvidenceExpansionCount}",
            $"Truncated/dropped/full_doc_blocked: {pack.Budget.TruncatedItemsCount} / {pack.Budget.DroppedItemsCount} / {pack.Budget.FullDocBlockedCount}",
            string.Empty,
            "Budget reasons:",
        };

        if (pack.Budget.BudgetViolationReasonCodes.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.AddRange(pack.Budget.BudgetViolationReasonCodes.Select(item => $"- {item}"));
        }

        lines.Add(string.Empty);
        lines.Add("Largest contributors:");
        if (pack.Budget.LargestContributors.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.AddRange(pack.Budget.LargestContributors.Select(item => $"- {item.Kind}: {item.EstimatedTokens} tokens ({item.Summary})"));
        }

        lines.Add(string.Empty);
        lines.Add("Top sources:");
        if (pack.Budget.TopSources.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.AddRange(pack.Budget.TopSources.Select(item => $"- {item}"));
        }

        lines.AddRange(
        new[]
        {
            string.Empty,
            "Goal:",
            pack.Goal,
            string.Empty,
            "Task:",
            pack.Task,
            string.Empty,
            "Constraints:",
        });

        if (pack.Constraints.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.AddRange(pack.Constraints.Select(item => $"- {item}"));
        }

        lines.Add(string.Empty);
        lines.Add("Local task graph:");
        lines.Add($"- current: {pack.LocalTaskGraph.CurrentTaskId} {pack.LocalTaskGraph.CurrentTaskTitle}");
        lines.Add(pack.LocalTaskGraph.Dependencies.Count == 0
            ? "- depends_on: (none)"
            : $"- depends_on: {string.Join(", ", pack.LocalTaskGraph.Dependencies.Select(item => $"{item.TaskId}[{item.Status}]"))}");
        lines.Add(pack.LocalTaskGraph.Blockers.Count == 0
            ? "- blockers: (none)"
            : $"- blockers: {string.Join(", ", pack.LocalTaskGraph.Blockers.Select(item => $"{item.TaskId}[{item.Status}]"))}");

        lines.Add(string.Empty);
        lines.Add("Relevant modules:");
        if (pack.RelevantModules.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var module in pack.RelevantModules)
            {
                lines.Add($"- {module.Module}: {module.Summary}");
                if (module.Files.Count > 0)
                {
                    lines.Add($"  files: {string.Join(", ", module.Files)}");
                }
            }
        }

        lines.Add(string.Empty);
        lines.Add("Facet narrowing:");
        lines.Add($"- repo: {pack.FacetNarrowing.Repo}");
        lines.Add($"- phase: {pack.FacetNarrowing.Phase}");
        lines.Add($"- task/card: {pack.FacetNarrowing.TaskId ?? "(none)"} / {pack.FacetNarrowing.CardId ?? "(none)"}");
        lines.Add(pack.FacetNarrowing.Modules.Count == 0
            ? "- modules: (none)"
            : $"- modules: {string.Join(", ", pack.FacetNarrowing.Modules)}");
        lines.Add(pack.FacetNarrowing.ScopeFiles.Count == 0
            ? "- scope_files: (none)"
            : $"- scope_files: {string.Join(", ", pack.FacetNarrowing.ScopeFiles)}");
        lines.Add(pack.FacetNarrowing.ArtifactTypes.Count == 0
            ? "- artifact_types: (none)"
            : $"- artifact_types: {string.Join(", ", pack.FacetNarrowing.ArtifactTypes)}");

        lines.Add(string.Empty);
        lines.Add("Bounded recall:");
        if (pack.Recall.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var item in pack.Recall)
            {
                lines.Add($"- {item.Kind}: {item.Source} [{item.Scope}] score={item.Score:F1} tokens={item.TokenEstimate}");
                lines.Add($"  {item.Text}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Code hints:");
        if (pack.CodeHints.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.AddRange(pack.CodeHints.Select(item => $"- {item}"));
        }

        lines.Add(string.Empty);
        lines.Add("Windowed file reads:");
        if (pack.WindowedReads.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var window in pack.WindowedReads)
            {
                lines.Add($"- {window.Path}: lines {window.StartLine}-{window.EndLine}/{window.TotalLines} [{window.Reason}]");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Context compaction:");
        lines.Add($"- strategy: {pack.Compaction.Strategy}");
        lines.Add($"- candidate/relevant/windowed/full/omitted: {pack.Compaction.CandidateFileCount} / {pack.Compaction.RelevantFileCount} / {pack.Compaction.WindowedReadCount} / {pack.Compaction.FullReadCount} / {pack.Compaction.OmittedFileCount}");
        lines.Add($"- trimmed_items: {pack.Compaction.TrimmedItemCount}");

        lines.Add(string.Empty);
        lines.Add("Failure summary:");
        if (pack.LastFailureSummary is null)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.Add($"- type: {pack.LastFailureSummary.FailureType}");
            lines.Add($"- lane: {pack.LastFailureSummary.FailureLane}");
            lines.Add($"- file: {pack.LastFailureSummary.AffectedFile ?? "(none)"}");
            lines.Add($"- module: {pack.LastFailureSummary.AffectedModule ?? "(none)"}");
            lines.Add($"- reason: {pack.LastFailureSummary.Reason}");
            lines.Add($"- build/tests/runtime: {pack.LastFailureSummary.BuildStatus} / {pack.LastFailureSummary.TestStatus} / {pack.LastFailureSummary.RuntimeStatus}");
        }

        lines.Add(string.Empty);
        lines.Add("Last run summary:");
        if (pack.LastRunSummary is null)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.Add($"- run_id: {pack.LastRunSummary.RunId}");
            lines.Add($"- status: {pack.LastRunSummary.Status}");
            lines.Add($"- summary: {pack.LastRunSummary.Summary}");
            lines.Add($"- boundary: {pack.LastRunSummary.BoundaryReason ?? "(none)"}");
            lines.Add($"- replan: {pack.LastRunSummary.ReplanStrategy ?? "(none)"}");
        }

        lines.Add(string.Empty);
        lines.Add("Trimmed:");
        if (pack.Trimmed.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.AddRange(pack.Trimmed.Select(item =>
                $"- {item.Key} [{item.Layer.ToString().ToLowerInvariant()}/{item.Priority.ToString().ToLowerInvariant()}] {item.Reason} ({item.EstimatedTokens} tokens)"));
        }

        lines.Add(string.Empty);
        lines.Add("Expandable refs:");
        if (pack.ExpandableReferences.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.AddRange(pack.ExpandableReferences.Select(item => $"- {item.Kind}: {item.Path} ({item.Summary})"));
        }

        return new OperatorCommandResult(0, lines);
    }
}
