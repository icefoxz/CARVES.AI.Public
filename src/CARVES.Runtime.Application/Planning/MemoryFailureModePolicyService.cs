namespace Carves.Runtime.Application.Planning;

public enum MemoryFailureMode
{
    Strict,
    Normal,
    Search,
    Release,
}

public sealed record MemoryFailurePolicyEntry(
    string FailureCode,
    string Strict,
    string Normal,
    string Search,
    string Release);

public sealed record MemoryCommandModeEntry(
    string Command,
    MemoryFailureMode DefaultMode,
    IReadOnlyList<string> Flags,
    string CriticalFailureBehavior,
    int ExitCode,
    string JsonErrorShape,
    string TelemetryEvent);

public sealed class MemoryFailureModePolicyService
{
    private static readonly IReadOnlyDictionary<string, MemoryFailurePolicyEntry> FailurePolicies =
        new Dictionary<string, MemoryFailurePolicyEntry>(StringComparer.Ordinal)
        {
            ["canonical_fact_without_evidence"] = new("canonical_fact_without_evidence", "block", "block", "exclude", "blocker"),
            ["invalidated_fact_retrieved"] = new("invalidated_fact_retrieved", "block", "block", "exclude", "blocker"),
            ["ledger_projection_mismatch"] = new("ledger_projection_mismatch", "block", "degraded", "exclude_disputed", "blocker_if_unresolved"),
            ["active_view_build_failed"] = new("active_view_build_failed", "block", "degraded", "fallback_lexical_only", "blocker_for_writeback"),
            ["stale_codegraph_high_risk_task"] = new("stale_codegraph_high_risk_task", "block", "block", "warn", "blocker"),
            ["stale_codegraph_low_risk_task"] = new("stale_codegraph_low_risk_task", "degraded", "degraded", "warn", "non_blocking"),
            ["orphan_claim_used_as_canonical"] = new("orphan_claim_used_as_canonical", "block", "block", "exclude", "blocker"),
            ["handoff_claim_promoted_directly"] = new("handoff_claim_promoted_directly", "block", "block", "candidate_only", "blocker"),
            ["worker_self_claim_promoted_directly"] = new("worker_self_claim_promoted_directly", "block", "block", "candidate_only", "blocker"),
            ["conflicting_canonical_facts"] = new("conflicting_canonical_facts", "block", "block", "surface_conflict", "blocker"),
        };

    private static readonly IReadOnlyDictionary<string, MemoryCommandModeEntry> CommandModes =
        new Dictionary<string, MemoryCommandModeEntry>(StringComparer.Ordinal)
        {
            ["carves memory verify --strict"] = new(
                "carves memory verify --strict",
                MemoryFailureMode.Strict,
                ["--json"],
                "exit_nonzero",
                1,
                "memory_verify_result.v1",
                "memory.drift.blocked"),
            ["carves memory search"] = new(
                "carves memory search",
                MemoryFailureMode.Search,
                ["--json"],
                "exclude_unsafe_results_with_reason",
                0,
                "memory_search_result.v1",
                "memory.search.filtered"),
            ["carves context show"] = new(
                "carves context show",
                MemoryFailureMode.Normal,
                ["--json"],
                "degraded_or_block_if_task_writeback_sensitive",
                0,
                "context_show_result.v1",
                "context.source.excluded"),
            ["carves context estimate"] = new(
                "carves context estimate",
                MemoryFailureMode.Normal,
                ["--json"],
                "degraded_with_warning",
                0,
                "context_estimate_result.v1",
                "context.source.excluded"),
            ["writeback preflight"] = new(
                "writeback preflight",
                MemoryFailureMode.Strict,
                Array.Empty<string>(),
                "block",
                1,
                "writeback_preflight_result.v1",
                "memory.write.blocked"),
        };

    public IReadOnlyList<MemoryFailurePolicyEntry> GetFailurePolicies()
    {
        return FailurePolicies.Values
            .OrderBy(entry => entry.FailureCode, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<MemoryCommandModeEntry> GetCommandModeMatrix()
    {
        return CommandModes.Values
            .OrderBy(entry => entry.Command, StringComparer.Ordinal)
            .ToArray();
    }

    public MemoryFailurePolicyEntry GetFailurePolicy(string failureCode)
    {
        if (!FailurePolicies.TryGetValue(failureCode, out var entry))
        {
            throw new ArgumentOutOfRangeException(nameof(failureCode), failureCode, "Unknown memory failure code.");
        }

        return entry;
    }

    public string ResolveFailureBehavior(string failureCode, MemoryFailureMode mode)
    {
        var entry = GetFailurePolicy(failureCode);
        return mode switch
        {
            MemoryFailureMode.Strict => entry.Strict,
            MemoryFailureMode.Normal => entry.Normal,
            MemoryFailureMode.Search => entry.Search,
            MemoryFailureMode.Release => entry.Release,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown memory failure mode."),
        };
    }

    public MemoryCommandModeEntry GetCommandMode(string command)
    {
        if (!CommandModes.TryGetValue(command, out var entry))
        {
            throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown command failure mode surface.");
        }

        return entry;
    }
}
