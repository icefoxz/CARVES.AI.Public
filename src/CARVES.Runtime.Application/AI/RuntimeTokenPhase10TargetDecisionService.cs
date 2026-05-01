using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase10TargetDecisionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;

    public RuntimeTokenPhase10TargetDecisionService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenPhase10TargetDecisionResult Persist(
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineTrustLineResult trustLineResult)
    {
        return Persist(paths, evidenceResult, trustLineResult, evidenceResult.ResultDate);
    }

    internal static RuntimeTokenPhase10TargetDecisionResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineTrustLineResult trustLineResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(evidenceResult, trustLineResult, resultDate);

        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var mayReferenceTrustLine = trustLineResult.Phase10TargetDecisionMayReferenceThisLine
                                    && string.Equals(
                                        trustLineResult.TrustLineClassification,
                                        "recomputed_trusted_for_phase_1_target_decision",
                                        StringComparison.Ordinal);
        var decision = mayReferenceTrustLine
            ? evidenceResult.Recommendation.Decision
            : "insufficient_data";
        var nextTrack = mayReferenceTrustLine
            ? evidenceResult.Recommendation.NextTrack
            : "insufficient_data";
        var blockedCriteria = mayReferenceTrustLine
            ? evidenceResult.Recommendation.BlockedCriteria
            : trustLineResult.BlockingReasons;
        var result = new RuntimeTokenPhase10TargetDecisionResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = evidenceResult.Aggregation.Cohort.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            EvidenceMarkdownArtifactPath = evidenceResult.MarkdownArtifactPath,
            EvidenceJsonArtifactPath = evidenceResult.JsonArtifactPath,
            TrustMarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, RuntimeTokenBaselineTrustLineService.GetMarkdownArtifactPathFor(paths, resultDate)),
            TrustJsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, RuntimeTokenBaselineTrustLineService.GetJsonArtifactPathFor(paths, resultDate)),
            TrustLineClassification = trustLineResult.TrustLineClassification,
            Phase10TargetDecisionMayReferenceThisLine = mayReferenceTrustLine,
            Decision = decision,
            NextTrack = nextTrack,
            TargetSegment = mayReferenceTrustLine ? evidenceResult.Recommendation.TargetSegment : null,
            TargetSegmentClass = mayReferenceTrustLine ? evidenceResult.Recommendation.TargetSegmentClass : null,
            TargetShareP95 = mayReferenceTrustLine ? evidenceResult.Recommendation.TargetShareP95 : null,
            TrimmedShareProxyP95 = mayReferenceTrustLine ? evidenceResult.Recommendation.TrimmedShareProxyP95 : null,
            HardCapTriggerSegment = mayReferenceTrustLine ? evidenceResult.Recommendation.HardCapTriggerSegment : null,
            DominanceBasis = mayReferenceTrustLine ? evidenceResult.Recommendation.DominanceBasis : Array.Empty<string>(),
            Confidence = mayReferenceTrustLine ? evidenceResult.Recommendation.Confidence : "low",
            TopP95Contributors = evidenceResult.DecisionInputs.TopP95Contributors,
            TopTrimmedContributors = evidenceResult.DecisionInputs.TopTrimmedContributors,
            DecisionInputs = evidenceResult.DecisionInputs,
            BlockingReasons = blockedCriteria.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            WhatMustNotHappenNext = BuildWhatMustNotHappenNext(decision),
            Notes = BuildNotes(mayReferenceTrustLine, trustLineResult, decision),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetMarkdownArtifactPath(paths, resultDate);

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetJsonArtifactPath(paths, resultDate);

    internal static string FormatMarkdown(RuntimeTokenPhase10TargetDecisionResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 1.0 Target Decision Result");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Trust line classification: `{result.TrustLineClassification}`");
        builder.AppendLine($"- Phase 1.0 target decision may reference this line: `{(result.Phase10TargetDecisionMayReferenceThisLine ? "yes" : "no")}`");
        builder.AppendLine($"- Decision: `{result.Decision}`");
        builder.AppendLine($"- Next track: `{result.NextTrack}`");
        builder.AppendLine($"- Evidence markdown artifact: `{result.EvidenceMarkdownArtifactPath}`");
        builder.AppendLine($"- Evidence json artifact: `{result.EvidenceJsonArtifactPath}`");
        builder.AppendLine($"- Trust markdown artifact: `{result.TrustMarkdownArtifactPath}`");
        builder.AppendLine($"- Trust json artifact: `{result.TrustJsonArtifactPath}`");
        builder.AppendLine();

        builder.AppendLine("## Attribution Summary");
        builder.AppendLine();
        builder.AppendLine($"- ContextPack explicit share p95: `{FormatRatio(result.DecisionInputs.ContextPackExplicitShareP95)}`");
        builder.AppendLine($"- Non-ContextPack explicit share p95: `{FormatRatio(result.DecisionInputs.NonContextPackExplicitShareP95)}`");
        builder.AppendLine($"- Stable explicit share p95: `{FormatRatio(result.DecisionInputs.StableExplicitShareP95)}`");
        builder.AppendLine($"- Dynamic explicit share p95: `{FormatRatio(result.DecisionInputs.DynamicExplicitShareP95)}`");
        builder.AppendLine($"- Renderer share p95 proxy: `{FormatRatio(result.DecisionInputs.RendererShareP95Proxy)}`");
        builder.AppendLine($"- Tool schema share p95 proxy: `{FormatRatio(result.DecisionInputs.ToolSchemaShareP95Proxy)}`");
        builder.AppendLine($"- Wrapper policy share p95 proxy: `{FormatRatio(result.DecisionInputs.WrapperPolicyShareP95Proxy)}`");
        builder.AppendLine($"- Other segment share p95 proxy: `{FormatRatio(result.DecisionInputs.OtherSegmentShareP95Proxy)}`");
        builder.AppendLine($"- Parent residual share p95: `{FormatRatio(result.DecisionInputs.ParentResidualShareP95)}`");
        builder.AppendLine($"- Known provider overhead share p95: `{FormatRatio(result.DecisionInputs.KnownProviderOverheadShareP95)}`");
        builder.AppendLine($"- Unknown unattributed share p95: `{FormatRatio(result.DecisionInputs.UnknownUnattributedShareP95)}`");
        builder.AppendLine();

        builder.AppendLine("## Top P95 Contributors");
        builder.AppendLine();
        builder.AppendLine("| Segment | Class | Share P95 | Context Tokens P95 | Billable Tokens P95 |");
        builder.AppendLine("| --- | --- | ---: | ---: | ---: |");
        foreach (var contributor in result.TopP95Contributors)
        {
            builder.AppendLine($"| `{contributor.SegmentKind}` | `{contributor.TargetSegmentClass}` | {FormatRatio(contributor.ShareP95)} | {FormatNumber(contributor.ContextTokensP95)} | {FormatNumber(contributor.BillableTokensP95)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Top P95 Trimmed Contributors");
        builder.AppendLine();
        builder.AppendLine("| Segment | Class | Trimmed Tokens P95 | Trimmed Share Proxy P95 |");
        builder.AppendLine("| --- | --- | ---: | ---: |");
        foreach (var contributor in result.TopTrimmedContributors)
        {
            builder.AppendLine($"| `{contributor.SegmentKind}` | `{contributor.TargetSegmentClass}` | {FormatNumber(contributor.TrimmedTokensP95)} | {FormatRatio(contributor.TrimmedShareProxyP95)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Hard Cap Trigger Segments");
        builder.AppendLine();
        if (result.DecisionInputs.HardCapTriggerSegments.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var segment in result.DecisionInputs.HardCapTriggerSegments)
            {
                builder.AppendLine($"- `{segment}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## ContextPack Versus Non-ContextPack Share");
        builder.AppendLine();
        builder.AppendLine($"- ContextPack explicit share p95: `{FormatRatio(result.DecisionInputs.ContextPackExplicitShareP95)}`");
        builder.AppendLine($"- Non-ContextPack explicit share p95: `{FormatRatio(result.DecisionInputs.NonContextPackExplicitShareP95)}`");

        builder.AppendLine();
        builder.AppendLine("## Stable Versus Dynamic Section Share");
        builder.AppendLine();
        builder.AppendLine($"- Stable explicit share p95: `{FormatRatio(result.DecisionInputs.StableExplicitShareP95)}`");
        builder.AppendLine($"- Dynamic explicit share p95: `{FormatRatio(result.DecisionInputs.DynamicExplicitShareP95)}`");

        builder.AppendLine();
        builder.AppendLine("## Decision");
        builder.AppendLine();
        builder.AppendLine($"- Decision: `{result.Decision}`");
        builder.AppendLine($"- Target segment: `{result.TargetSegment ?? "none"}`");
        builder.AppendLine($"- Target segment class: `{result.TargetSegmentClass ?? "none"}`");
        builder.AppendLine($"- Target share p95: `{FormatNullableNumber(result.TargetShareP95)}`");
        builder.AppendLine($"- Trimmed share p95: `{FormatNullableNumber(result.TrimmedShareProxyP95)}`");
        builder.AppendLine($"- Hard cap trigger: `{result.HardCapTriggerSegment ?? "none"}`");
        builder.AppendLine($"- Dominance basis: {(result.DominanceBasis.Count == 0 ? "none" : string.Join(", ", result.DominanceBasis.Select(item => $"`{item}`")))}");
        builder.AppendLine($"- Confidence: `{result.Confidence}`");

        builder.AppendLine();
        builder.AppendLine("## Why This Decision Follows From The Data");
        builder.AppendLine();
        if (result.BlockingReasons.Count == 0)
        {
            builder.AppendLine("- the trust line is classified as trusted for Phase 1.0 target decision");
            builder.AppendLine("- the recommendation is taken from the recomputed baseline evidence result");
            builder.AppendLine("- the dominance basis is frozen in the baseline evidence result");
        }
        else
        {
            foreach (var reason in result.BlockingReasons)
            {
                builder.AppendLine($"- blocked by `{reason}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## What Must Not Happen Next");
        builder.AppendLine();
        foreach (var item in result.WhatMustNotHappenNext)
        {
            builder.AppendLine($"- {item}");
        }

        if (result.Notes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Notes");
            builder.AppendLine();
            foreach (var note in result.Notes)
            {
                builder.AppendLine($"- {note}");
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> BuildWhatMustNotHappenNext(string decision)
    {
        var items = new List<string>
        {
            "do not enable runtime shadow execution",
            "do not start active canary",
            "do not replace the main renderer",
        };

        if (string.Equals(decision, "insufficient_data", StringComparison.Ordinal))
        {
            items.Add("do not start Phase 1.2 targeted compact candidate work");
        }

        return items;
    }

    private static IReadOnlyList<string> BuildNotes(
        bool mayReferenceTrustLine,
        RuntimeTokenBaselineTrustLineResult trustLineResult,
        string decision)
    {
        var notes = new List<string>();
        if (!mayReferenceTrustLine)
        {
            notes.Add("This result is forced to insufficient_data because the trust line is not eligible for Phase 1.0 reference.");
        }

        if (string.Equals(decision, "insufficient_data", StringComparison.Ordinal))
        {
            notes.Add("Phase 1.2 targeted compact candidate work remains blocked.");
        }

        if (!trustLineResult.TotalCostClaimAllowed)
        {
            notes.Add("Authoritative total-cost claims are still blocked on the referenced trust line.");
        }

        return notes;
    }

    private static void ValidateInputs(
        RuntimeTokenBaselineEvidenceResult evidenceResult,
        RuntimeTokenBaselineTrustLineResult trustLineResult,
        DateOnly resultDate)
    {
        if (evidenceResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Phase 1.0 target decision requires evidence result date to match the requested result date.");
        }

        if (string.IsNullOrWhiteSpace(evidenceResult.MarkdownArtifactPath)
            || string.IsNullOrWhiteSpace(evidenceResult.JsonArtifactPath))
        {
            throw new InvalidOperationException("Phase 1.0 target decision requires evidence artifact paths.");
        }

        if (!string.Equals(evidenceResult.Aggregation.Cohort.CohortId, trustLineResult.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Phase 1.0 target decision requires trust line and evidence to reference the same cohort.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-1-target-decision-result-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-1",
            $"target-decision-result-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string absolutePath)
    {
        return Path.GetRelativePath(repoRoot, absolutePath).Replace('\\', '/');
    }

    private static string FormatRatio(double value) => value.ToString("0.000");

    private static string FormatNumber(double value) => value.ToString("0.###");

    private static string FormatNullableNumber(double? value) => value.HasValue ? value.Value.ToString("0.###") : "n/a";
}
