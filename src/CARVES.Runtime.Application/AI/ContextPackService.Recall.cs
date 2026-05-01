using System.Text;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Memory;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.AI;

public sealed partial class ContextPackService
{
    private static readonly HashSet<string> RecallStopWords =
    [
        "about",
        "after",
        "before",
        "build",
        "card",
        "change",
        "changes",
        "code",
        "context",
        "current",
        "file",
        "files",
        "from",
        "into",
        "module",
        "project",
        "review",
        "scope",
        "task",
        "tests",
        "that",
        "this",
        "with",
        "worker",
    ];

    private ContextPackFacetNarrowing BuildTaskFacetNarrowing(
        TaskNode task,
        CodeGraphScopeAnalysis scopeAnalysis,
        IReadOnlyList<string> scopeFiles,
        bool hasRuntimePackProjectUnderstanding)
    {
        return new ContextPackFacetNarrowing
        {
            Repo = GetRepoLabel(),
            TaskId = task.TaskId,
            CardId = task.CardId,
            Phase = "task_context_build",
            Modules = scopeAnalysis.Modules.Take(5).ToArray(),
            ScopeFiles = scopeFiles.Take(5).ToArray(),
            ArtifactTypes = hasRuntimePackProjectUnderstanding
                ? ["module_memory", "project_memory", "codegraph", "runtime_pack_project_understanding"]
                : ["module_memory", "project_memory", "codegraph"],
        };
    }

    private ContextPackFacetNarrowing BuildPlannerFacetNarrowing(
        RuntimeSessionState session,
        IReadOnlyList<TaskNode> previewTasks,
        CodeGraphScopeAnalysis scopeAnalysis,
        IReadOnlyList<string> scopeFiles,
        bool hasRuntimePackProjectUnderstanding)
    {
        return new ContextPackFacetNarrowing
        {
            Repo = GetRepoLabel(),
            TaskId = previewTasks.FirstOrDefault()?.TaskId,
            CardId = previewTasks.Select(task => task.CardId).FirstOrDefault(cardId => !string.IsNullOrWhiteSpace(cardId)),
            Phase = "planner_context_build",
            Modules = scopeAnalysis.Modules.Take(5).ToArray(),
            ScopeFiles = scopeFiles.Take(5).ToArray(),
            ArtifactTypes = hasRuntimePackProjectUnderstanding
                ? ["project_memory", "codegraph", "runtime_pack_project_understanding"]
                : ["project_memory", "codegraph"],
        };
    }

    private IReadOnlyList<ContextPackRecallItem> BuildTaskRecall(
        TaskNode task,
        CodeGraphScopeAnalysis scopeAnalysis,
        IReadOnlyList<MemoryDocument> moduleMemory,
        IReadOnlyList<MemoryDocument> projectMemory)
    {
        var terms = BuildRecallTermsFromSets(
            [task.Title, task.Description],
            task.Acceptance,
            task.Scope,
            scopeAnalysis.Modules,
            scopeAnalysis.Files);
        var candidates = new List<RecallCandidate>();
        candidates.AddRange(BuildMemoryCandidates("module_memory", $"task:{task.TaskId}", moduleMemory, terms, baseScore: 320));
        candidates.AddRange(BuildMemoryCandidates("project_memory", $"task:{task.TaskId}", projectMemory, terms, baseScore: 220));
        candidates.AddRange(BuildCodeGraphCandidates($"task:{task.TaskId}", scopeAnalysis, terms));
        return BuildRecallItems(candidates, top: 4);
    }

    private IReadOnlyList<ContextPackRecallItem> BuildPlannerRecall(
        RuntimeSessionState session,
        string wakeDetail,
        IReadOnlyList<Opportunity> selectedOpportunities,
        IReadOnlyList<TaskNode> previewTasks,
        CodeGraphScopeAnalysis scopeAnalysis,
        IReadOnlyList<MemoryDocument> projectMemory)
    {
        var terms = BuildRecallTermsFromSets(
            [wakeDetail, string.Join(Environment.NewLine, selectedOpportunities.Select(item => $"{item.Title} {item.Description} {item.Reason}"))],
            previewTasks.Select(task => task.Title),
            previewTasks.SelectMany(task => task.Scope),
            scopeAnalysis.Modules,
            scopeAnalysis.Files);
        var candidates = new List<RecallCandidate>();
        candidates.AddRange(BuildMemoryCandidates("project_memory", $"planner:{session.SessionId}", projectMemory, terms, baseScore: 240));
        candidates.AddRange(BuildCodeGraphCandidates($"planner:{session.SessionId}", scopeAnalysis, terms));
        return BuildRecallItems(candidates, top: 3);
    }

    private static IReadOnlyList<RecallCandidate> BuildMemoryCandidates(
        string kind,
        string scope,
        IReadOnlyList<MemoryDocument> documents,
        IReadOnlySet<string> terms,
        int baseScore)
    {
        return documents
            .Select(document =>
            {
                var score = ScoreRecall(document.Title, document.Content, terms, titleWeight: 12, contentWeight: 3);
                return score == 0
                    ? null
                    : new RecallCandidate(kind, document.Path, scope, SelectBestExcerpt(document.Content, terms), baseScore + score);
            })
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToArray();
    }

    private static IReadOnlyList<RecallCandidate> BuildCodeGraphCandidates(
        string scope,
        CodeGraphScopeAnalysis scopeAnalysis,
        IReadOnlySet<string> terms)
    {
        return scopeAnalysis.SummaryLines
            .Select((line, index) =>
            {
                var score = ScoreRecall(line, line, terms, titleWeight: 0, contentWeight: 4);
                return score == 0
                    ? null
                    : new RecallCandidate(
                        "codegraph",
                        BuildCodeGraphSource(scopeAnalysis, line, index),
                        scope,
                        line,
                        260 + score);
            })
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToArray();
    }

    private static IReadOnlyList<ContextPackRecallItem> BuildRecallItems(
        IReadOnlyList<RecallCandidate> candidates,
        int top)
    {
        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Source, StringComparer.OrdinalIgnoreCase)
            .Take(top)
            .Select(candidate =>
            {
                var excerpt = BuildExcerpt(candidate.Content);
                var projection = PromptSafeArtifactProjectionFactory.Create(excerpt, excerpt, candidate.Source);
                return new ContextPackRecallItem
                {
                    Kind = candidate.Kind,
                    Source = candidate.Source,
                    Scope = candidate.Scope,
                    Score = candidate.Score,
                    Chars = projection.Summary.Length,
                    TokenEstimate = ContextBudgetPolicyResolver.EstimateTokens(projection.Summary),
                    Text = projection.Summary,
                };
            })
            .ToArray();
    }

    private static string BuildExcerpt(string content)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var segments = normalized
            .Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => string.Join(" ", segment.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();
        var best = segments.FirstOrDefault() ?? normalized;
        return best.Length <= 420 ? best : $"{best[..Math.Min(320, best.Length)].TrimEnd()} ... {best[^Math.Min(80, best.Length)..].TrimStart()}";
    }

    private static string SelectBestExcerpt(string content, IReadOnlySet<string> terms)
    {
        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var segments = normalized
            .Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => string.Join(" ", segment.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();
        if (segments.Length == 0)
        {
            return normalized;
        }

        return segments
            .OrderByDescending(segment => ScoreRecall(segment, segment, terms, titleWeight: 0, contentWeight: 4))
            .ThenBy(segment => segment.Length)
            .First();
    }

    private static IReadOnlySet<string> BuildRecallTermsFromSets(params IEnumerable<string>[] termSets)
    {
        return termSets
            .SelectMany(set => set)
            .SelectMany(Tokenize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> Tokenize(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (builder.Length > 0)
            {
                var token = builder.ToString();
                if (IsRecallTerm(token))
                {
                    yield return token;
                }

                builder.Clear();
            }
        }

        if (builder.Length > 0)
        {
            var token = builder.ToString();
            if (IsRecallTerm(token))
            {
                yield return token;
            }
        }
    }

    private static bool IsRecallTerm(string token)
    {
        return token.Length is >= 4 and <= 32
               && !RecallStopWords.Contains(token);
    }

    private static int ScoreRecall(string title, string content, IReadOnlySet<string> terms, int titleWeight, int contentWeight)
    {
        if (terms.Count == 0)
        {
            return 0;
        }

        var titleHits = terms.Count(term => title.Contains(term, StringComparison.OrdinalIgnoreCase));
        var contentHits = terms.Count(term => content.Contains(term, StringComparison.OrdinalIgnoreCase));
        return (titleHits * titleWeight) + (contentHits * contentWeight);
    }

    private static string BuildCodeGraphSource(CodeGraphScopeAnalysis scopeAnalysis, string line, int index)
    {
        var module = scopeAnalysis.Modules.FirstOrDefault(item => line.Contains(item, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(module)
            ? $"codegraph:summary:{index + 1}"
            : $"codegraph:module:{module}";
    }

    private static IReadOnlyList<MemoryDocument> NarrowModuleMemory(
        IReadOnlyList<MemoryDocument> documents,
        IReadOnlyList<string> modules,
        IReadOnlyList<string> scope)
    {
        var normalizedModules = modules
            .Select(NormalizeFacetToken)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        var normalizedScope = scope
            .Select(item => NormalizeFacetToken(Path.GetFileNameWithoutExtension(item)))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);

        return documents
            .Where(document =>
            {
                var title = NormalizeFacetToken(document.Title);
                return normalizedModules.Contains(title) || normalizedScope.Contains(title);
            })
            .ToArray();
    }

    private static string NormalizeFacetToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private string GetRepoLabel()
    {
        return Path.GetFileName(paths.RepoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private sealed record RecallCandidate(
        string Kind,
        string Source,
        string Scope,
        string Content,
        double Score);
}
