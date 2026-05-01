using System.Text.Json;
using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.Tests;

public sealed class MemoryRecoveryProtocolServiceTests
{
    [Fact]
    public void RecoveryCases_FixDeterministicDecisionsForProjectionIndexCacheAndConflictPaths()
    {
        var service = new MemoryRecoveryProtocolService();

        Assert.Equal("strict_block_then_rebuild_projection", service.ResolveDecision("projection_mismatch"));
        Assert.Equal("invalidate_or_supersede_with_audit", service.ResolveDecision("bad_canonical_fact"));
        Assert.Equal("rebuild_index_from_authoritative_read_contract", service.ResolveDecision("retrieval_index_stale"));
        Assert.Equal("discard_cache_and_rebuild_from_authoritative_inputs", service.ResolveDecision("active_view_cache_corrupt"));
        Assert.Equal("conflict_hold_no_auto_overwrite", service.ResolveDecision("legacy_markdown_conflict"));
    }

    [Fact]
    public void RecoveryCases_RebuildPathsExcludeInvalidatedAndSupersededAndBadFactsRequireAudit()
    {
        var service = new MemoryRecoveryProtocolService();

        var projection = service.GetRecoveryCase("projection_mismatch");
        var staleIndex = service.GetRecoveryCase("retrieval_index_stale");
        var corruptCache = service.GetRecoveryCase("active_view_cache_corrupt");
        var badCanonical = service.GetRecoveryCase("bad_canonical_fact");
        var legacyConflict = service.GetRecoveryCase("legacy_markdown_conflict");

        Assert.True(projection.RequiresRebuild);
        Assert.True(staleIndex.RequiresRebuild);
        Assert.True(corruptCache.RequiresRebuild);
        Assert.All(new[] { projection, staleIndex, corruptCache }, entry =>
        {
            Assert.True(entry.RebuildFromAuthoritativeInputs);
            Assert.True(entry.ExcludeInvalidatedAndSuperseded);
            Assert.True(entry.RequiresAuditTrail);
        });

        Assert.True(badCanonical.ExcludeInvalidatedAndSuperseded);
        Assert.True(badCanonical.RequiresAuditTrail);
        Assert.False(badCanonical.AllowsSilentDelete);
        Assert.True(legacyConflict.RequiresConflictHold);
        Assert.False(legacyConflict.RequiresRebuild);
    }

    [Fact]
    public void RecoveryProtocolDocument_MatchesRuntimePolicyTable()
    {
        var service = new MemoryRecoveryProtocolService();
        var json = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "docs", "runtime", "memory-gate", "MEMORY_RECOVERY_PROTOCOL_V1.json")));
        using var document = JsonDocument.Parse(json);

        var cases = document.RootElement.GetProperty("cases")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("recovery_case").GetString()!,
                item => item,
                StringComparer.Ordinal);

        Assert.Equal("memory-recovery-protocol.v1", document.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("memory-gate.v1", document.RootElement.GetProperty("policy_version").GetString());
        Assert.Equal(service.GetRecoveryCases().Count, cases.Count);

        foreach (var entry in service.GetRecoveryCases())
        {
            var item = cases[entry.RecoveryCase];
            Assert.Equal(entry.Decision, item.GetProperty("decision").GetString());
            Assert.Equal(entry.RequiresRebuild, item.GetProperty("requires_rebuild").GetBoolean());
            Assert.Equal(entry.RebuildFromAuthoritativeInputs, item.GetProperty("rebuild_from_authoritative_inputs").GetBoolean());
            Assert.Equal(entry.ExcludeInvalidatedAndSuperseded, item.GetProperty("exclude_invalidated_and_superseded").GetBoolean());
            Assert.Equal(entry.RequiresAuditTrail, item.GetProperty("requires_audit_trail").GetBoolean());
            Assert.Equal(entry.AllowsSilentDelete, item.GetProperty("allows_silent_delete").GetBoolean());
            Assert.Equal(entry.RequiresConflictHold, item.GetProperty("requires_conflict_hold").GetBoolean());
        }
    }

    [Fact]
    public void RecoveryDrillsDocument_MatchesRuntimeFixtures()
    {
        var service = new MemoryRecoveryProtocolService();
        var json = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "docs", "runtime", "memory-gate", "MEMORY_RECOVERY_DRILLS_V1.json")));
        using var document = JsonDocument.Parse(json);

        var fixtures = document.RootElement.GetProperty("fixtures")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("fixture_id").GetString()!,
                item => item,
                StringComparer.Ordinal);

        Assert.Equal("memory-recovery-drills.v1", document.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("memory-gate.v1", document.RootElement.GetProperty("policy_version").GetString());
        Assert.Equal(service.GetDrillFixtures().Count, fixtures.Count);

        foreach (var entry in service.GetDrillFixtures())
        {
            var item = fixtures[entry.FixtureId];
            Assert.Equal(entry.RecoveryCase, item.GetProperty("recovery_case").GetString());
            Assert.Equal(entry.ExpectedDecision, item.GetProperty("expected_decision").GetString());
            Assert.Equal(entry.ExpectedRequiresRebuild, item.GetProperty("expected_requires_rebuild").GetBoolean());
            Assert.Equal(entry.ExpectedExcludeInvalidatedAndSuperseded, item.GetProperty("expected_exclude_invalidated_and_superseded").GetBoolean());
            Assert.Equal(entry.ExpectedRequiresAuditTrail, item.GetProperty("expected_requires_audit_trail").GetBoolean());
        }
    }
}
