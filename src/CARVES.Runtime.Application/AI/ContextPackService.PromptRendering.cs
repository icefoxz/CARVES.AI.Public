using System.Text;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.AI;

public sealed partial class ContextPackService
{
    private sealed record RenderedPrompt(string Text, IReadOnlyList<RenderedPromptSection> Sections);

    private static RenderedPrompt RenderPrompt(
        string goal,
        string task,
        IReadOnlyList<string> constraints,
        AcceptanceContract? acceptanceContract,
        TaskGraphLocalProjection localTaskGraph,
        IReadOnlyList<ContextPackModuleProjection> relevantModules,
        ContextPackFacetNarrowing facetNarrowing,
        IReadOnlyList<ContextPackRecallItem> recall,
        IReadOnlyList<string> codeHints,
        IReadOnlyList<ContextPackWindowedRead> windowedReads,
        ContextPackCompaction compaction,
        CompactFailureSummary? lastFailureSummary,
        ExecutionHistorySummary? lastRunSummary)
    {
        var builder = new StringBuilder()
            .AppendLine("Context Pack")
            .AppendLine();
        var sections = new List<RenderedPromptSection>();

        AppendSection(builder, sections, "goal", "goal", "goal", section =>
        {
            section.AppendLine("Goal:");
            section.AppendLine(goal);
            section.AppendLine();
        });

        AppendSection(builder, sections, "task", "task", "task", section =>
        {
            section.AppendLine("Task:");
            section.AppendLine(task);
            section.AppendLine();
        });

        AppendSection(builder, sections, "constraints", "constraints", "constraints", section =>
        {
            section.AppendLine("Constraints:");
            if (constraints.Count == 0)
            {
                section.AppendLine("- (none)");
            }
            else
            {
                foreach (var constraint in constraints)
                {
                    section.AppendLine($"- {constraint}");
                }
            }
        });

        var acceptanceContractBlock = AcceptanceContractSummaryFormatter.BuildPlainTextBlock("Acceptance contract:", acceptanceContract);
        if (!string.IsNullOrWhiteSpace(acceptanceContractBlock))
        {
            builder.AppendLine();
            AppendSection(builder, sections, "acceptance_contract", "acceptance_contract", acceptanceContract?.ContractId, section =>
            {
                section.AppendLine(acceptanceContractBlock);
            });
        }

        builder.AppendLine();
        AppendSection(builder, sections, "local_task_graph", "local_task_graph", localTaskGraph.CurrentTaskId, section =>
        {
            section.AppendLine("Local task graph:");
            section.AppendLine($"- current: {localTaskGraph.CurrentTaskId} {localTaskGraph.CurrentTaskTitle}");
            if (localTaskGraph.Dependencies.Count == 0)
            {
                section.AppendLine("- depends_on: (none)");
            }
            else
            {
                section.AppendLine("- depends_on:");
                foreach (var dependency in localTaskGraph.Dependencies)
                {
                    section.AppendLine($"  - {dependency.TaskId}[{dependency.Status}] {dependency.Title}".TrimEnd());
                    if (!string.IsNullOrWhiteSpace(dependency.Summary))
                    {
                        section.AppendLine($"    summary: {dependency.Summary}");
                    }
                }
            }

            if (localTaskGraph.Blockers.Count == 0)
            {
                section.AppendLine("- blockers: (none)");
            }
            else
            {
                section.AppendLine("- blockers:");
                foreach (var blocker in localTaskGraph.Blockers)
                {
                    section.AppendLine($"  - {blocker.TaskId}[{blocker.Status}] {blocker.Title}".TrimEnd());
                }
            }
        });

        builder.AppendLine();
        AppendSection(builder, sections, "relevant_modules", "relevant_modules", null, section =>
        {
            section.AppendLine("Relevant modules:");
            if (relevantModules.Count == 0)
            {
                section.AppendLine("- (none)");
            }
            else
            {
                foreach (var module in relevantModules)
                {
                    section.AppendLine($"- {module.Module}: {module.Summary}");
                    if (module.Files.Count > 0)
                    {
                        section.AppendLine($"  files: {string.Join(", ", module.Files)}");
                    }
                }
            }
        });

        builder.AppendLine();
        AppendSection(builder, sections, "facet_narrowing", "facet_narrowing", facetNarrowing.TaskId ?? facetNarrowing.CardId, section =>
        {
            section.AppendLine("Facet narrowing:");
            section.AppendLine($"- repo: {facetNarrowing.Repo}");
            section.AppendLine($"- phase: {facetNarrowing.Phase}");
            section.AppendLine($"- task: {facetNarrowing.TaskId ?? "(none)"}");
            section.AppendLine($"- card: {facetNarrowing.CardId ?? "(none)"}");
            section.AppendLine($"- modules: {(facetNarrowing.Modules.Count == 0 ? "(none)" : string.Join(", ", facetNarrowing.Modules))}");
            section.AppendLine($"- scope_files: {(facetNarrowing.ScopeFiles.Count == 0 ? "(none)" : string.Join(", ", facetNarrowing.ScopeFiles))}");
            section.AppendLine($"- artifact_types: {(facetNarrowing.ArtifactTypes.Count == 0 ? "(none)" : string.Join(", ", facetNarrowing.ArtifactTypes))}");
        });

        builder.AppendLine();
        AppendSection(builder, sections, "recall", "recall", null, section =>
        {
            section.AppendLine("Bounded recall:");
            if (recall.Count == 0)
            {
                section.AppendLine("- (none)");
            }
            else
            {
                foreach (var item in recall)
                {
                    section.AppendLine($"- {item.Kind}: {item.Source} [{item.Scope}] score={item.Score:F1} tokens={item.TokenEstimate}");
                    section.AppendLine($"  {item.Text}");
                }
            }
        });

        builder.AppendLine();
        AppendSection(builder, sections, "code_hints", "code_hints", null, section =>
        {
            section.AppendLine("Code hints:");
            if (codeHints.Count == 0)
            {
                section.AppendLine("- (none)");
            }
            else
            {
                foreach (var hint in codeHints)
                {
                    section.AppendLine($"- {hint}");
                }
            }
        });

        builder.AppendLine();
        AppendSection(builder, sections, "windowed_reads", "windowed_reads", null, section =>
        {
            section.AppendLine("Windowed file reads:");
            if (windowedReads.Count == 0)
            {
                section.AppendLine("- (none)");
            }
            else
            {
                foreach (var window in windowedReads)
                {
                    section.AppendLine($"- {window.Path}: lines {window.StartLine}-{window.EndLine} of {window.TotalLines} [{window.Reason}]");
                    if (!string.IsNullOrWhiteSpace(window.Snippet))
                    {
                        section.AppendLine(window.Snippet);
                    }
                }
            }
        });

        builder.AppendLine();
        AppendSection(builder, sections, "context_compaction", "context_compaction", null, section =>
        {
            section.AppendLine("Context compaction:");
            section.AppendLine($"- strategy: {compaction.Strategy}");
            section.AppendLine($"- candidate_files: {compaction.CandidateFileCount}");
            section.AppendLine($"- relevant_files: {compaction.RelevantFileCount}");
            section.AppendLine($"- windowed_reads: {compaction.WindowedReadCount}");
            section.AppendLine($"- full_reads: {compaction.FullReadCount}");
            section.AppendLine($"- omitted_files: {compaction.OmittedFileCount}");
            section.AppendLine($"- trimmed_items: {compaction.TrimmedItemCount}");
        });

        builder.AppendLine();
        AppendSection(builder, sections, "last_failure", "last_failure", null, section =>
        {
            section.AppendLine("Last failure summary:");
            if (lastFailureSummary is null)
            {
                section.AppendLine("- (none)");
            }
            else
            {
                section.AppendLine($"- type: {lastFailureSummary.FailureType}");
                section.AppendLine($"- lane: {lastFailureSummary.FailureLane}");
                section.AppendLine($"- reason: {lastFailureSummary.Reason}");
                section.AppendLine($"- build: {lastFailureSummary.BuildStatus}");
                section.AppendLine($"- tests: {lastFailureSummary.TestStatus}");
                section.AppendLine($"- runtime: {lastFailureSummary.RuntimeStatus}");
            }
        });

        builder.AppendLine();
        AppendSection(builder, sections, "last_run", "last_run", lastRunSummary?.RunId, section =>
        {
            section.AppendLine("Last run summary:");
            if (lastRunSummary is null)
            {
                section.AppendLine("- (none)");
            }
            else
            {
                section.AppendLine($"- run_id: {lastRunSummary.RunId}");
                section.AppendLine($"- status: {lastRunSummary.Status}");
                section.AppendLine($"- summary: {lastRunSummary.Summary}");
                if (!string.IsNullOrWhiteSpace(lastRunSummary.BoundaryReason))
                {
                    section.AppendLine($"- boundary_reason: {lastRunSummary.BoundaryReason}");
                }

                if (!string.IsNullOrWhiteSpace(lastRunSummary.ReplanStrategy))
                {
                    section.AppendLine($"- replan_strategy: {lastRunSummary.ReplanStrategy}");
                }
            }
        });

        var text = builder.ToString().TrimEnd();
        var maxLength = text.Length;
        return new RenderedPrompt(
            text,
            sections
                .Where(section => section.StartChar < maxLength)
                .Select(section => section with { EndChar = Math.Min(section.EndChar, maxLength) })
                .ToArray());
    }

    private static void AppendSection(
        StringBuilder builder,
        ICollection<RenderedPromptSection> sections,
        string sectionId,
        string sectionKind,
        string? sourceItemId,
        Action<StringBuilder> write)
    {
        var start = builder.Length;
        var sectionBuilder = new StringBuilder();
        write(sectionBuilder);
        builder.Append(sectionBuilder);
        sections.Add(new RenderedPromptSection
        {
            SectionId = sectionId,
            SectionKind = sectionKind,
            SourceItemId = sourceItemId,
            RendererVersion = "prose_v1",
            StartChar = start,
            EndChar = builder.Length,
        });
    }
}
