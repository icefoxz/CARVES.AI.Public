using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

internal sealed record RuntimeTokenCapTruthSnapshot
{
    public bool? ProviderContextCapHit { get; init; }

    public bool? InternalPromptBudgetCapHit { get; init; }

    public bool? SectionBudgetCapHit { get; init; }

    public bool? TrimLoopCapHit { get; init; }

    public string? CapTriggerSegmentKind { get; init; }

    public string? CapTriggerSource { get; init; }
}

internal static class RuntimeTokenCapTruthResolver
{
    public static RuntimeTokenCapTruthSnapshot? FromContextPackBudget(ContextPackBudget? budget)
    {
        if (budget is null)
        {
            return null;
        }

        var sectionBudgetCapHit = budget.TruncatedItemsCount > 0
                                  || budget.DroppedItemsCount > 0
                                  || budget.FullDocBlockedCount > 0;
        var trimLoopCapHit = budget.TruncatedItemsCount > 0;
        var internalPromptBudgetCapHit = string.Equals(budget.BudgetPosture, ContextBudgetPostures.HardCapEnforced, StringComparison.Ordinal)
                                         || string.Equals(budget.BudgetPosture, ContextBudgetPostures.DegradedContext, StringComparison.Ordinal);
        var capTriggerSegmentKind = ResolveCapTriggerSegmentKind(budget);

        return new RuntimeTokenCapTruthSnapshot
        {
            ProviderContextCapHit = null,
            InternalPromptBudgetCapHit = internalPromptBudgetCapHit,
            SectionBudgetCapHit = sectionBudgetCapHit,
            TrimLoopCapHit = trimLoopCapHit,
            CapTriggerSegmentKind = capTriggerSegmentKind,
            CapTriggerSource = ResolveCapTriggerSource(budget, capTriggerSegmentKind),
        };
    }

    public static RuntimeTokenCapTruthSnapshot? FromMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        var providerContextCapHit = TryParseBoolean(metadata, "context_pack_provider_context_cap_hit");
        var internalPromptBudgetCapHit = TryParseBoolean(metadata, "context_pack_internal_prompt_budget_cap_hit");
        var sectionBudgetCapHit = TryParseBoolean(metadata, "context_pack_section_budget_cap_hit");
        var trimLoopCapHit = TryParseBoolean(metadata, "context_pack_trim_loop_cap_hit");
        var capTriggerSegmentKind = GetNonEmpty(metadata, "context_pack_cap_trigger_segment_kind");
        var capTriggerSource = GetNonEmpty(metadata, "context_pack_cap_trigger_source");

        if (!providerContextCapHit.HasValue
            && !internalPromptBudgetCapHit.HasValue
            && !sectionBudgetCapHit.HasValue
            && !trimLoopCapHit.HasValue
            && string.IsNullOrWhiteSpace(capTriggerSegmentKind)
            && string.IsNullOrWhiteSpace(capTriggerSource))
        {
            return null;
        }

        return new RuntimeTokenCapTruthSnapshot
        {
            ProviderContextCapHit = providerContextCapHit,
            InternalPromptBudgetCapHit = internalPromptBudgetCapHit,
            SectionBudgetCapHit = sectionBudgetCapHit,
            TrimLoopCapHit = trimLoopCapHit,
            CapTriggerSegmentKind = capTriggerSegmentKind,
            CapTriggerSource = capTriggerSource,
        };
    }

    public static LlmRequestEnvelopeUsage Apply(LlmRequestEnvelopeUsage usage, RuntimeTokenCapTruthSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return usage;
        }

        return usage with
        {
            ProviderContextCapHit = snapshot.ProviderContextCapHit,
            InternalPromptBudgetCapHit = snapshot.InternalPromptBudgetCapHit,
            SectionBudgetCapHit = snapshot.SectionBudgetCapHit,
            TrimLoopCapHit = snapshot.TrimLoopCapHit,
            CapTriggerSegmentKind = snapshot.CapTriggerSegmentKind,
            CapTriggerSource = snapshot.CapTriggerSource,
        };
    }

    private static bool? TryParseBoolean(IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string? GetNonEmpty(IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? ResolveCapTriggerSource(ContextPackBudget budget, string? capTriggerSegmentKind)
    {
        if (string.IsNullOrWhiteSpace(capTriggerSegmentKind))
        {
            return null;
        }

        if (budget.TrimmedTokens > 0 || budget.TruncatedItemsCount > 0)
        {
            return "context_pack_trimmed_items";
        }

        if (budget.LargestContributors.Count > 0)
        {
            return "context_pack_budget_contributors";
        }

        return "context_pack_budget_posture";
    }

    private static string? ResolveCapTriggerSegmentKind(ContextPackBudget budget)
    {
        var fromTrimmed = budget.TrimmedTokens > 0
            ? budget.LargestContributors
                .Select(item => NormalizeSegmentKind(item.Kind))
                .FirstOrDefault(kind => !string.IsNullOrWhiteSpace(kind))
            : null;
        if (!string.IsNullOrWhiteSpace(fromTrimmed))
        {
            return fromTrimmed;
        }

        return budget.LargestContributors
            .Select(item => NormalizeSegmentKind(item.Kind))
            .FirstOrDefault(kind => !string.IsNullOrWhiteSpace(kind));
    }

    private static string? NormalizeSegmentKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("recall:", StringComparison.Ordinal))
        {
            return "recall";
        }

        if (normalized.StartsWith("windowed_read:", StringComparison.Ordinal))
        {
            return "windowed_reads";
        }

        if (normalized.StartsWith("module:", StringComparison.Ordinal)
            || normalized.StartsWith("module_files:", StringComparison.Ordinal))
        {
            return "relevant_modules";
        }

        if (normalized.StartsWith("dependency:", StringComparison.Ordinal)
            || normalized.StartsWith("blocker:", StringComparison.Ordinal))
        {
            return "local_task_graph";
        }

        return normalized switch
        {
            "bounded_recall" => "recall",
            "last_failure_summary" => "last_failure",
            "last_run_summary" => "last_run",
            "code_hint" => "code_hints",
            "constraint" => "constraints",
            _ => normalized,
        };
    }
}
