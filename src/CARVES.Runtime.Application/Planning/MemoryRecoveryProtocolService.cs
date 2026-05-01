namespace Carves.Runtime.Application.Planning;

public sealed record MemoryRecoveryProtocolEntry(
    string RecoveryCase,
    string Decision,
    bool RequiresRebuild,
    bool RebuildFromAuthoritativeInputs,
    bool ExcludeInvalidatedAndSuperseded,
    bool RequiresAuditTrail,
    bool AllowsSilentDelete,
    bool RequiresConflictHold);

public sealed record MemoryRecoveryDrillFixture(
    string FixtureId,
    string RecoveryCase,
    string ExpectedDecision,
    bool ExpectedRequiresRebuild,
    bool ExpectedExcludeInvalidatedAndSuperseded,
    bool ExpectedRequiresAuditTrail);

public sealed class MemoryRecoveryProtocolService
{
    private static readonly IReadOnlyDictionary<string, MemoryRecoveryProtocolEntry> RecoveryCases =
        new Dictionary<string, MemoryRecoveryProtocolEntry>(StringComparer.Ordinal)
        {
            ["projection_mismatch"] = new(
                "projection_mismatch",
                "strict_block_then_rebuild_projection",
                RequiresRebuild: true,
                RebuildFromAuthoritativeInputs: true,
                ExcludeInvalidatedAndSuperseded: true,
                RequiresAuditTrail: true,
                AllowsSilentDelete: false,
                RequiresConflictHold: false),
            ["bad_canonical_fact"] = new(
                "bad_canonical_fact",
                "invalidate_or_supersede_with_audit",
                RequiresRebuild: false,
                RebuildFromAuthoritativeInputs: false,
                ExcludeInvalidatedAndSuperseded: true,
                RequiresAuditTrail: true,
                AllowsSilentDelete: false,
                RequiresConflictHold: false),
            ["retrieval_index_stale"] = new(
                "retrieval_index_stale",
                "rebuild_index_from_authoritative_read_contract",
                RequiresRebuild: true,
                RebuildFromAuthoritativeInputs: true,
                ExcludeInvalidatedAndSuperseded: true,
                RequiresAuditTrail: true,
                AllowsSilentDelete: false,
                RequiresConflictHold: false),
            ["active_view_cache_corrupt"] = new(
                "active_view_cache_corrupt",
                "discard_cache_and_rebuild_from_authoritative_inputs",
                RequiresRebuild: true,
                RebuildFromAuthoritativeInputs: true,
                ExcludeInvalidatedAndSuperseded: true,
                RequiresAuditTrail: true,
                AllowsSilentDelete: false,
                RequiresConflictHold: false),
            ["legacy_markdown_conflict"] = new(
                "legacy_markdown_conflict",
                "conflict_hold_no_auto_overwrite",
                RequiresRebuild: false,
                RebuildFromAuthoritativeInputs: false,
                ExcludeInvalidatedAndSuperseded: false,
                RequiresAuditTrail: true,
                AllowsSilentDelete: false,
                RequiresConflictHold: true),
        };

    private static readonly IReadOnlyDictionary<string, MemoryRecoveryDrillFixture> DrillFixtures =
        new Dictionary<string, MemoryRecoveryDrillFixture>(StringComparer.Ordinal)
        {
            ["projection_rebuild_deterministic"] = new(
                "projection_rebuild_deterministic",
                "projection_mismatch",
                "strict_block_then_rebuild_projection",
                ExpectedRequiresRebuild: true,
                ExpectedExcludeInvalidatedAndSuperseded: true,
                ExpectedRequiresAuditTrail: true),
            ["bad_canonical_fact_invalidated_with_audit"] = new(
                "bad_canonical_fact_invalidated_with_audit",
                "bad_canonical_fact",
                "invalidate_or_supersede_with_audit",
                ExpectedRequiresRebuild: false,
                ExpectedExcludeInvalidatedAndSuperseded: true,
                ExpectedRequiresAuditTrail: true),
            ["stale_index_rebuild_excludes_invalidated"] = new(
                "stale_index_rebuild_excludes_invalidated",
                "retrieval_index_stale",
                "rebuild_index_from_authoritative_read_contract",
                ExpectedRequiresRebuild: true,
                ExpectedExcludeInvalidatedAndSuperseded: true,
                ExpectedRequiresAuditTrail: true),
            ["active_view_cache_discard_rebuild"] = new(
                "active_view_cache_discard_rebuild",
                "active_view_cache_corrupt",
                "discard_cache_and_rebuild_from_authoritative_inputs",
                ExpectedRequiresRebuild: true,
                ExpectedExcludeInvalidatedAndSuperseded: true,
                ExpectedRequiresAuditTrail: true),
            ["legacy_markdown_conflict_not_auto_resolved"] = new(
                "legacy_markdown_conflict_not_auto_resolved",
                "legacy_markdown_conflict",
                "conflict_hold_no_auto_overwrite",
                ExpectedRequiresRebuild: false,
                ExpectedExcludeInvalidatedAndSuperseded: false,
                ExpectedRequiresAuditTrail: true),
        };

    public IReadOnlyList<MemoryRecoveryProtocolEntry> GetRecoveryCases()
    {
        return RecoveryCases.Values
            .OrderBy(entry => entry.RecoveryCase, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<MemoryRecoveryDrillFixture> GetDrillFixtures()
    {
        return DrillFixtures.Values
            .OrderBy(entry => entry.FixtureId, StringComparer.Ordinal)
            .ToArray();
    }

    public MemoryRecoveryProtocolEntry GetRecoveryCase(string recoveryCase)
    {
        if (!RecoveryCases.TryGetValue(recoveryCase, out var entry))
        {
            throw new ArgumentOutOfRangeException(nameof(recoveryCase), recoveryCase, "Unknown memory recovery case.");
        }

        return entry;
    }

    public MemoryRecoveryDrillFixture GetDrillFixture(string fixtureId)
    {
        if (!DrillFixtures.TryGetValue(fixtureId, out var entry))
        {
            throw new ArgumentOutOfRangeException(nameof(fixtureId), fixtureId, "Unknown memory recovery drill fixture.");
        }

        return entry;
    }

    public string ResolveDecision(string recoveryCase)
    {
        return GetRecoveryCase(recoveryCase).Decision;
    }
}
