using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeSurfaceInventoryBaselineTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    private static readonly string[] ProtectedSurfaceNames =
    [
        "runtime-agent-thread-start",
        "runtime-agent-short-context",
        "runtime-markdown-read-path-budget",
        "runtime-agent-bootstrap-packet",
        "runtime-agent-bootstrap-receipt",
        "runtime-agent-queue-projection",
        "runtime-agent-task-overlay",
        "runtime-governed-agent-handoff-proof",
        "runtime-worker-execution-audit",
        "runtime-default-workflow-proof",
        "execution-packet",
        "packet-enforcement",
        "runtime-brokered-execution",
        "runtime-workspace-mutation-audit",
        "execution-hardening",
    ];

    private static readonly string[] AllowedReviewedScopes =
    [
        "protected_execution_startup",
        "historical_governance_hotspot",
        "agent_problem_follow_up",
        "product_pilot_target_dist",
        "session_gateway",
    ];

    private static readonly string[] HistoricalGovernanceAliasNames =
    [
        "runtime-validationlab-proof-handoff",
        "runtime-controlled-governance-proof",
        "runtime-packaging-proof-federation-maturity",
        "runtime-hotspot-backlog-drain",
        "runtime-hotspot-cross-family-patterns",
        "runtime-governance-program-reaudit",
    ];

    [Fact]
    public void Baseline_TracksEveryRegistrySurfaceAndHash()
    {
        var baseline = LoadBaseline();
        var registryMetadata = RuntimeSurfaceCommandRegistry.CommandMetadata;
        var registryNames = registryMetadata.Select(item => item.Name).ToArray();
        var baselineNames = baseline.SurfaceEntries.Select(item => item.SurfaceId).ToArray();

        Assert.Equal(1, baseline.SchemaVersion);
        Assert.Equal("CARD-923", baseline.CardId);
        Assert.Equal("T-CARD-923-001", baseline.TaskId);
        Assert.True(baseline.StaleIfRegistryChanged);
        Assert.Equal(
            "sha256(command_metadata_lines_v2:name|context_tier|default_visibility|inspect_usage|api_usage|surface_role|successor_surface_id|retirement_posture)",
            baseline.RegistryHashAlgorithm);
        Assert.Equal(registryMetadata.Count, baseline.RegistryCount);
        Assert.Equal(RuntimeSurfaceCommandRegistry.PrimaryCommandMetadata.Count, baseline.PrimarySurfaceCount);
        Assert.Equal(RuntimeSurfaceCommandRegistry.CompatibilityAliasCommandMetadata.Count, baseline.CompatibilityAliasSurfaceCount);
        Assert.Equal(registryMetadata.Count, baseline.SurfaceEntries.Length);
        Assert.Equal(registryMetadata.Count, baselineNames.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(baseline.MissingSurfaces);
        Assert.Empty(baseline.ExtraSurfaces);
        Assert.Empty(registryNames.Except(baselineNames, StringComparer.Ordinal));
        Assert.Empty(baselineNames.Except(registryNames, StringComparer.Ordinal));
        Assert.Equal(ComputeRegistryHash(registryMetadata), baseline.RegistryHash);
    }

    [Fact]
    public void Baseline_SeparatesObservedFactsFromReviewJudgments()
    {
        var baseline = LoadBaseline();
        var metadataByName = RuntimeSurfaceCommandRegistry.CommandMetadata.ToDictionary(
            item => item.Name,
            StringComparer.Ordinal);

        foreach (var entry in baseline.SurfaceEntries)
        {
            Assert.True(metadataByName.TryGetValue(entry.SurfaceId, out var metadata), entry.SurfaceId);
            Assert.Equal(metadata!.SurfaceRole == RuntimeSurfaceRole.Primary, entry.PrimaryEntry);
            Assert.NotNull(entry.ObservedFacts);
            Assert.NotNull(entry.ReviewJudgment);
            Assert.Equal(metadata.ContextTier.ToString(), entry.ObservedFacts.ContextTier);
            Assert.Equal(metadata.DefaultVisibility.ToString(), entry.ObservedFacts.DefaultVisibility);
            Assert.Equal(metadata.InspectUsage, entry.ObservedFacts.InspectUsage);
            Assert.Equal(metadata.ApiUsage, entry.ObservedFacts.ApiUsage);
            Assert.Equal(ToToken(metadata.SurfaceRole), entry.ObservedFacts.SurfaceRole);
            Assert.Equal(metadata.SuccessorSurfaceId, entry.ObservedFacts.SuccessorSurfaceId);
            Assert.Equal(ToToken(metadata.RetirementPosture), entry.ObservedFacts.RetirementPosture);
            Assert.Equal(
                metadata.DefaultVisibility == RuntimeSurfaceDefaultVisibility.DefaultVisible,
                entry.ObservedFacts.DefaultVisible);
            Assert.Equal(
                metadata.DefaultVisibility == RuntimeSurfaceDefaultVisibility.CompatibilityOnly,
                entry.ObservedFacts.CompatibilityOnly);
            Assert.StartsWith("src/CARVES.Runtime.Application/ControlPlane/RuntimeSurfaceCommandRegistry.cs:", entry.ObservedFacts.SourceRef, StringComparison.Ordinal);
            Assert.NotEmpty(entry.ReviewJudgment.ReviewStatus);
            Assert.NotEmpty(entry.ReviewJudgment.ReviewScope);
            Assert.NotEmpty(entry.ReviewJudgment.Necessity);
            Assert.NotEmpty(entry.ReviewJudgment.CleanupPriority);
            Assert.NotEmpty(entry.ReviewJudgment.RecommendedAction);
            Assert.NotEmpty(entry.ReviewJudgment.EvidenceRefs);
        }
    }

    [Fact]
    public void Baseline_KeepsFirstReviewScopeBounded()
    {
        var baseline = LoadBaseline();

        foreach (var entry in baseline.SurfaceEntries)
        {
            if (entry.ReviewJudgment.ReviewStatus == "reviewed")
            {
                Assert.Contains(entry.ReviewJudgment.ReviewScope, AllowedReviewedScopes);
                continue;
            }

            Assert.Equal("operator_review_first", entry.ReviewJudgment.ReviewStatus);
            Assert.Equal("unreviewed", entry.ReviewJudgment.ReviewScope);
            Assert.Equal("operator_review_first", entry.ReviewJudgment.Necessity);
            Assert.Equal("operator_review_first", entry.ReviewJudgment.CleanupPriority);
            Assert.Equal("operator_review_first", entry.ReviewJudgment.RecommendedAction);
            Assert.Equal("operator_review_first", entry.ReviewJudgment.VisibilityRecommendation);
        }
    }

    [Fact]
    public void Baseline_EnforcesDefaultVisibleBudgetAndProtectedSurfaceRestrictions()
    {
        var baseline = LoadBaseline();
        var defaultVisibleCount = RuntimeSurfaceCommandRegistry.DefaultVisibleCommandMetadata.Count;

        Assert.Equal(defaultVisibleCount, baseline.DefaultVisibleSurfaceCount);
        Assert.Equal(RuntimeSurfaceCommandRegistry.MaxDefaultVisibleSurfaceCount, baseline.DefaultVisibleBudget);
        Assert.True(defaultVisibleCount <= RuntimeSurfaceCommandRegistry.MaxDefaultVisibleSurfaceCount);

        var entriesByName = baseline.SurfaceEntries.ToDictionary(entry => entry.SurfaceId, StringComparer.Ordinal);
        foreach (var surfaceName in ProtectedSurfaceNames)
        {
            Assert.True(entriesByName.TryGetValue(surfaceName, out var entry), surfaceName);
            Assert.Equal("reviewed", entry!.ReviewJudgment.ReviewStatus);
            Assert.Equal("protected_execution_startup", entry.ReviewJudgment.ReviewScope);
            Assert.DoesNotContain("low", entry.ReviewJudgment.Necessity, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hidden", entry.ReviewJudgment.VisibilityRecommendation, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hide", entry.ReviewJudgment.VisibilityRecommendation, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("compatibility_only", entry.ReviewJudgment.VisibilityRecommendation, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("merge_ready", entry.ReviewJudgment.RecommendedAction, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("retire_ready", entry.ReviewJudgment.RecommendedAction, StringComparison.OrdinalIgnoreCase);
            Assert.False(entry.ObservedFacts.CompatibilityOnly, surfaceName);
            Assert.Equal("primary", entry.ObservedFacts.SurfaceRole);
        }
    }

    [Fact]
    public void Baseline_RecordsHistoricalGovernanceAliasesWithSuccessorMetadata()
    {
        var baseline = LoadBaseline();
        var entriesByName = baseline.SurfaceEntries.ToDictionary(entry => entry.SurfaceId, StringComparer.Ordinal);

        Assert.True(entriesByName.TryGetValue("runtime-governance-archive-status", out var primary));
        Assert.True(primary!.PrimaryEntry);
        Assert.Equal("primary", primary.ObservedFacts.SurfaceRole);
        Assert.Null(primary.ObservedFacts.SuccessorSurfaceId);
        Assert.Equal("active_primary", primary.ObservedFacts.RetirementPosture);
        Assert.Equal("historical_governance_hotspot", primary.ReviewJudgment.ReviewScope);

        foreach (var aliasName in HistoricalGovernanceAliasNames)
        {
            Assert.True(entriesByName.TryGetValue(aliasName, out var entry), aliasName);
            Assert.False(entry!.PrimaryEntry);
            Assert.Equal("compatibility_alias", entry.ObservedFacts.SurfaceRole);
            Assert.Equal("runtime-governance-archive-status", entry.ObservedFacts.SuccessorSurfaceId);
            Assert.Equal("alias_retained", entry.ObservedFacts.RetirementPosture);
            Assert.False(entry.ObservedFacts.DefaultVisible, aliasName);
            Assert.Equal("reviewed", entry.ReviewJudgment.ReviewStatus);
            Assert.Equal("historical_governance_hotspot", entry.ReviewJudgment.ReviewScope);
            Assert.Equal("compatibility_alias_retained", entry.ReviewJudgment.RecommendedAction);
            Assert.Equal("name_compat_only", entry.ReviewJudgment.CompatibilityClass);
            Assert.NotEmpty(entry.ReviewJudgment.CompatibilityEvidenceRefs);
            Assert.NotEmpty(entry.ReviewJudgment.LegacyApiFields);
            Assert.True(entry.ReviewJudgment.CommandReferenceCount > 0, aliasName);
            Assert.Equal(0, entry.ReviewJudgment.JsonFieldConsumerReferenceCount);
            Assert.Contains("runtime-governance-archive-status", entry.ReviewJudgment.ReplacementOrAliasPlan, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Baseline_BlocksUnreviewedCleanupAndRequiresRetirementEvidence()
    {
        var baseline = LoadBaseline();

        foreach (var entry in baseline.SurfaceEntries)
        {
            if (entry.ReviewJudgment.ReviewStatus == "operator_review_first")
            {
                Assert.Equal("operator_review_first", entry.ReviewJudgment.RecommendedAction);
                Assert.Null(entry.ReviewJudgment.ReplacementOrAliasPlan);
            }

            if (string.Equals(entry.ReviewJudgment.RecommendedAction, "retire_ready", StringComparison.Ordinal))
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.ReviewJudgment.ReplacementOrAliasPlan));
                Assert.NotEmpty(entry.ReviewJudgment.EvidenceRefs);
            }
        }
    }

    private static SurfaceInventoryBaseline LoadBaseline()
    {
        var path = Path.Combine(RepoRoot(), "docs", "runtime", "runtime-surface-inventory-baseline.json");
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<SurfaceInventoryBaseline>(stream, JsonOptions)
               ?? throw new InvalidOperationException($"Unable to read baseline: {path}");
    }

    private static string ComputeRegistryHash(IReadOnlyList<RuntimeSurfaceCommandMetadata> metadata)
    {
        var lines = metadata.Select(item =>
            string.Join(
                '|',
                item.Name,
                item.ContextTier.ToString(),
                item.DefaultVisibility.ToString(),
                item.InspectUsage,
                item.ApiUsage,
                ToToken(item.SurfaceRole),
                item.SuccessorSurfaceId ?? "",
                ToToken(item.RetirementPosture)));
        var payload = string.Join('\n', lines);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ToToken(RuntimeSurfaceRole role)
    {
        return role switch
        {
            RuntimeSurfaceRole.CompatibilityAlias => "compatibility_alias",
            _ => "primary",
        };
    }

    private static string ToToken(RuntimeSurfaceRetirementPosture posture)
    {
        return posture switch
        {
            RuntimeSurfaceRetirementPosture.AliasRetained => "alias_retained",
            _ => "active_primary",
        };
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                && Directory.Exists(Path.Combine(directory.FullName, ".ai")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to find repository root.");
    }

    private sealed record SurfaceInventoryBaseline
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("card_id")]
        public string CardId { get; init; } = "";

        [JsonPropertyName("task_id")]
        public string TaskId { get; init; } = "";

        [JsonPropertyName("registry_count")]
        public int RegistryCount { get; init; }

        [JsonPropertyName("default_visible_surface_count")]
        public int DefaultVisibleSurfaceCount { get; init; }

        [JsonPropertyName("primary_surface_count")]
        public int PrimarySurfaceCount { get; init; }

        [JsonPropertyName("compatibility_alias_surface_count")]
        public int CompatibilityAliasSurfaceCount { get; init; }

        [JsonPropertyName("default_visible_budget")]
        public int DefaultVisibleBudget { get; init; }

        [JsonPropertyName("registry_hash_algorithm")]
        public string RegistryHashAlgorithm { get; init; } = "";

        [JsonPropertyName("registry_hash")]
        public string RegistryHash { get; init; } = "";

        [JsonPropertyName("stale_if_registry_changed")]
        public bool StaleIfRegistryChanged { get; init; }

        [JsonPropertyName("missing_surfaces")]
        public string[] MissingSurfaces { get; init; } = [];

        [JsonPropertyName("extra_surfaces")]
        public string[] ExtraSurfaces { get; init; } = [];

        [JsonPropertyName("surface_entries")]
        public SurfaceInventoryEntry[] SurfaceEntries { get; init; } = [];
    }

    private sealed record SurfaceInventoryEntry
    {
        [JsonPropertyName("surface_id")]
        public string SurfaceId { get; init; } = "";

        [JsonPropertyName("primary_entry")]
        public bool PrimaryEntry { get; init; }

        [JsonPropertyName("observed_facts")]
        public SurfaceObservedFacts ObservedFacts { get; init; } = new();

        [JsonPropertyName("review_judgment")]
        public SurfaceReviewJudgment ReviewJudgment { get; init; } = new();
    }

    private sealed record SurfaceObservedFacts
    {
        [JsonPropertyName("source_ref")]
        public string SourceRef { get; init; } = "";

        [JsonPropertyName("context_tier")]
        public string ContextTier { get; init; } = "";

        [JsonPropertyName("default_visibility")]
        public string DefaultVisibility { get; init; } = "";

        [JsonPropertyName("inspect_usage")]
        public string InspectUsage { get; init; } = "";

        [JsonPropertyName("api_usage")]
        public string ApiUsage { get; init; } = "";

        [JsonPropertyName("surface_role")]
        public string SurfaceRole { get; init; } = "";

        [JsonPropertyName("successor_surface_id")]
        public string? SuccessorSurfaceId { get; init; }

        [JsonPropertyName("retirement_posture")]
        public string RetirementPosture { get; init; } = "";

        [JsonPropertyName("default_visible")]
        public bool DefaultVisible { get; init; }

        [JsonPropertyName("compatibility_only")]
        public bool CompatibilityOnly { get; init; }
    }

    private sealed record SurfaceReviewJudgment
    {
        [JsonPropertyName("review_status")]
        public string ReviewStatus { get; init; } = "";

        [JsonPropertyName("review_scope")]
        public string ReviewScope { get; init; } = "";

        [JsonPropertyName("necessity")]
        public string Necessity { get; init; } = "";

        [JsonPropertyName("cleanup_priority")]
        public string CleanupPriority { get; init; } = "";

        [JsonPropertyName("recommended_action")]
        public string RecommendedAction { get; init; } = "";

        [JsonPropertyName("visibility_recommendation")]
        public string VisibilityRecommendation { get; init; } = "";

        [JsonPropertyName("replacement_or_alias_plan")]
        public string? ReplacementOrAliasPlan { get; init; }

        [JsonPropertyName("evidence_refs")]
        public string[] EvidenceRefs { get; init; } = [];

        [JsonPropertyName("compatibility_class")]
        public string CompatibilityClass { get; init; } = "";

        [JsonPropertyName("compatibility_evidence_refs")]
        public string[] CompatibilityEvidenceRefs { get; init; } = [];

        [JsonPropertyName("legacy_api_fields")]
        public string[] LegacyApiFields { get; init; } = [];

        [JsonPropertyName("command_reference_count")]
        public int CommandReferenceCount { get; init; }

        [JsonPropertyName("json_field_consumer_reference_count")]
        public int JsonFieldConsumerReferenceCount { get; init; }
    }
}
