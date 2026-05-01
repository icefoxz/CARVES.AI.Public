namespace Carves.Runtime.Application.AI;

public static class RuntimeTokenPhase10DecisionPolicy
{
    public const double TopTwoTargetShareThreshold = 0.20d;
    public const double TrimmedShareProxyThreshold = 0.40d;
    public const double AmbiguousClassShareGapThreshold = 0.02d;

    private static readonly HashSet<string> StableRendererSegmentKinds = new(StringComparer.Ordinal)
    {
        "goal",
        "task",
        "constraints",
        "acceptance_contract",
        "local_task_graph",
        "relevant_modules",
        "facet_narrowing",
        "code_hints",
        "context_compaction",
    };

    private static readonly HashSet<string> DynamicRendererSegmentKinds = new(StringComparer.Ordinal)
    {
        "recall",
        "windowed_reads",
        "last_failure",
        "last_run",
    };

    private static readonly HashSet<string> ToolSchemaSegmentKinds = new(StringComparer.Ordinal)
    {
        "tool_schema",
    };

    private static readonly HashSet<string> WrapperPolicySegmentKinds = new(StringComparer.Ordinal)
    {
        "system",
        "system_instructions",
        "output_contract",
        "planner_output_contract",
    };

    public static bool IsStableRendererSegmentKind(string segmentKind)
    {
        return StableRendererSegmentKinds.Contains(segmentKind);
    }

    public static bool IsDynamicRendererSegmentKind(string segmentKind)
    {
        return DynamicRendererSegmentKinds.Contains(segmentKind);
    }

    public static bool IsRendererSegmentKind(string segmentKind)
    {
        return IsStableRendererSegmentKind(segmentKind) || IsDynamicRendererSegmentKind(segmentKind);
    }

    public static string ClassifyOptimizationSurface(string segmentKind)
    {
        if (IsRendererSegmentKind(segmentKind))
        {
            return "renderer";
        }

        if (ToolSchemaSegmentKinds.Contains(segmentKind))
        {
            return "tool_schema";
        }

        if (WrapperPolicySegmentKinds.Contains(segmentKind))
        {
            return "wrapper";
        }

        return "other";
    }
}
