using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackDefaultContextGateTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    [Fact]
    public void Gate_DeclaresCandidateOnlyPostureAndClosedLines()
    {
        var gate = LoadGate();

        Assert.Equal(1, gate.SchemaVersion);
        Assert.Equal("CARD-925", gate.CardId);
        Assert.Equal("T-CARD-925-001", gate.TaskId);
        Assert.Equal("pack_default_context_gate", gate.ReadModelKind);
        Assert.Equal("repo_local_candidate_gate", gate.AuthorityPosture);
        Assert.Equal("docs/runtime/runtime-pack-identity-review-posture.json", gate.IdentityModelRef);
        Assert.Equal("docs/runtime/runtime-context-pack-budget-readback.json", gate.BudgetReadbackRef);
        Assert.Equal("candidate_only_no_activation", gate.GatePosture);
        Assert.Contains("eligible_candidate", gate.DecisionValues);
        Assert.Contains("pointer_only", gate.DecisionValues);
        Assert.Contains("blocked", gate.DecisionValues);
        Assert.Contains("manual_review_required", gate.DecisionValues);
        Assert.Contains("context_pack", gate.AllowedDefaultCandidateFamilies);
        Assert.Contains("not_automatic_activation", gate.ClosedLines);
        Assert.Contains("not_default_startup_context", gate.ClosedLines);
        Assert.Contains("not_a_runtime_inspect_or_api_surface", gate.ClosedLines);
        Assert.Contains("not_a_pack_registry", gate.ClosedLines);
        Assert.Contains("not_rollout_assignment", gate.ClosedLines);
        Assert.Contains("not_multi_pack_orchestration", gate.ClosedLines);
        Assert.Contains("does_not_load_any_pack_by_default", gate.NonClaims);
    }

    [Fact]
    public void Gate_ProjectsEveryIdentityEntryWithoutPromotingBlockedPacks()
    {
        var gate = LoadGate();
        var identity = LoadIdentity();
        var budget = LoadBudget();
        var decisions = gate.CandidateDecisions.ToDictionary(decision => decision.PackId, StringComparer.Ordinal);
        var readbacks = budget.Readbacks.ToDictionary(readback => readback.PackId, StringComparer.Ordinal);

        Assert.Equal(identity.Entries.Length, gate.CandidateDecisions.Length);

        foreach (var entry in identity.Entries)
        {
            Assert.True(decisions.TryGetValue(entry.PackId, out var decision), $"Missing gate decision for {entry.PackId}.");
            Assert.Equal(entry.PackFamily, decision.PackFamily);
            Assert.Equal(entry.DefaultContextPosture, decision.SourceDefaultContextPosture);
            Assert.Contains(decision.Decision, gate.DecisionValues);
            Assert.NotEmpty(decision.GateReasons);
            Assert.NotEmpty(decision.ExpansionRefs);
            Assert.NotEmpty(decision.NonClaims);

            if (entry.DefaultContextPosture == "blocked")
            {
                Assert.Equal("blocked", decision.Decision);
            }

            if (entry.DefaultContextPosture == "pointer_only")
            {
                Assert.Equal("pointer_only", decision.Decision);
            }

            if (entry.DefaultContextPosture == "manual_review_required")
            {
                Assert.Equal("manual_review_required", decision.Decision);
            }
        }

        Assert.DoesNotContain(gate.CandidateDecisions, decision => decision.Decision == "eligible_candidate");

        var contextPackDecision = decisions["runtime.context_pack"];
        Assert.Equal("pointer_only", contextPackDecision.Decision);
        Assert.True(readbacks.ContainsKey("runtime.context_pack"));
        Assert.Equal("readback_available", contextPackDecision.BudgetPosture);
    }

    [Fact]
    public void Gate_RequiresIdentityReviewBudgetPointersAndNonClaimsForEligibleCandidates()
    {
        var gate = LoadGate();
        var identity = EligibleIdentity();
        var budget = AcceptableBudget();

        Assert.Empty(ValidateEligibleCandidate(gate, identity, budget));

        Assert.Contains("missing_pack_id", ValidateEligibleCandidate(gate, identity with { PackId = "" }, budget));
        Assert.Contains("review_evidence_required", ValidateEligibleCandidate(gate, identity with { ReviewEvidenceRefs = [] }, budget));
        Assert.Contains("budget_readback_required", ValidateEligibleCandidate(gate, identity, null));
        Assert.Contains("expansion_refs_required", ValidateEligibleCandidate(gate, identity with { ExpansionRefs = [] }, budget));
        Assert.Contains("non_claims_required", ValidateEligibleCandidate(gate, identity with { NonClaims = [] }, budget));
    }

    [Fact]
    public void Gate_BlocksDraftDeprecatedStaleUnreviewedOverBudgetAndUnknownFamilyClaims()
    {
        var gate = LoadGate();
        var identity = EligibleIdentity();
        var budget = AcceptableBudget();

        Assert.Contains("default_context_ineligible_lifecycle", ValidateEligibleCandidate(gate, identity with { LifecycleState = "draft" }, budget));
        Assert.Contains("default_context_ineligible_lifecycle", ValidateEligibleCandidate(gate, identity with { LifecycleState = "deprecated" }, budget));
        Assert.Contains("default_context_ineligible_lifecycle", ValidateEligibleCandidate(gate, identity with { LifecycleState = "archived" }, budget));
        Assert.Contains("default_context_ineligible_review", ValidateEligibleCandidate(gate, identity with { ReviewPosture = "stale" }, budget));
        Assert.Contains("default_context_ineligible_review", ValidateEligibleCandidate(gate, identity with { ReviewPosture = "needs_review" }, budget));
        Assert.Contains("default_context_ineligible_review", ValidateEligibleCandidate(gate, identity with { ReviewPosture = "unreviewed" }, budget));
        Assert.Contains("default_context_ineligible_family", ValidateEligibleCandidate(gate, identity with { PackFamily = "runtime_pack" }, budget));
        Assert.Contains("default_context_ineligible_family", ValidateEligibleCandidate(gate, identity with { PackFamily = "unknown_pack_family" }, budget));
        Assert.Contains("explicit_candidate_posture_required", ValidateEligibleCandidate(gate, identity with { DefaultContextPosture = "pointer_only" }, budget));
        Assert.Contains("budget_over_limit", ValidateEligibleCandidate(gate, identity, budget with { EstimatedIncludedTokens = 9001 }));
        Assert.Contains("budget_posture_not_accepted", ValidateEligibleCandidate(gate, identity, budget with { BudgetPosture = "over_budget" }));
    }

    [Fact]
    public void Gate_DoesNotAddStartupOrAgentsReads()
    {
        var gate = LoadGate();

        Assert.False(gate.StartupReadPath.MandatoryStartupReadsChanged);
        Assert.False(gate.StartupReadPath.AgentsInitializationReadsChanged);
        Assert.False(gate.StartupReadPath.DefaultPackReadsAdded);
        Assert.False(gate.StartupReadPath.RuntimeInspectApiSurfaceAdded);
        Assert.False(gate.StartupReadPath.AutomaticActivationAdded);

        var forbiddenDefaultRead = "docs/runtime/runtime-pack-default-context-gate.json";
        var startupSources = new[]
        {
            "README.md",
            "AGENTS.md",
            ".ai/memory/architecture/00_AI_ENTRY_PROTOCOL.md",
            ".ai/memory/architecture/04_EXECUTION_RUNBOOK_CONTRACT.md",
            ".ai/memory/architecture/05_EXECUTION_OS_METHODOLOGY.md",
            ".ai/PROJECT_BOUNDARY.md",
            ".ai/STATE.md",
            ".ai/DEV_LOOP.md",
        };

        foreach (var source in startupSources)
        {
            var text = File.ReadAllText(Path.Combine(RepoRoot(), source));
            Assert.DoesNotContain(forbiddenDefaultRead, text, StringComparison.Ordinal);
        }
    }

    private static IReadOnlyList<string> ValidateEligibleCandidate(
        PackDefaultContextGateModel gate,
        PackIdentityEntry identity,
        ContextPackBudgetReadback? budget)
    {
        var errors = new List<string>();
        Require(identity.PackId, "missing_pack_id", errors);
        Require(identity.PackFamily, "missing_pack_family", errors);
        Require(identity.LifecycleState, "missing_lifecycle_state", errors);
        Require(identity.ReviewPosture, "missing_review_posture", errors);
        Require(identity.DefaultContextPosture, "missing_default_context_posture", errors);

        if (identity.SchemaVersion != 1)
        {
            errors.Add("unsupported_schema_version");
        }

        if (identity.LifecycleState != "active")
        {
            errors.Add("default_context_ineligible_lifecycle");
        }

        if (identity.ReviewPosture != "reviewed")
        {
            errors.Add("default_context_ineligible_review");
        }

        if (identity.ReviewEvidenceRefs.Length == 0)
        {
            errors.Add("review_evidence_required");
        }

        if (!gate.AllowedDefaultCandidateFamilies.Contains(identity.PackFamily, StringComparer.Ordinal))
        {
            errors.Add("default_context_ineligible_family");
        }

        if (identity.DefaultContextPosture != "eligible_candidate")
        {
            errors.Add("explicit_candidate_posture_required");
        }

        if (identity.ExpansionRefs.Length == 0)
        {
            errors.Add("expansion_refs_required");
        }

        if (identity.NonClaims.Length == 0)
        {
            errors.Add("non_claims_required");
        }

        if (budget is null)
        {
            errors.Add("budget_readback_required");
            return errors;
        }

        if (!gate.BudgetRules.AcceptedBudgetPostures.Contains(budget.BudgetPosture, StringComparer.Ordinal))
        {
            errors.Add("budget_posture_not_accepted");
        }

        if (budget.EstimatedIncludedTokens <= 0 || budget.BudgetLimitEstimatedTokens <= 0)
        {
            errors.Add("budget_readback_required");
        }

        if (budget.EstimatedIncludedTokens > budget.BudgetLimitEstimatedTokens)
        {
            errors.Add("budget_over_limit");
        }

        if (budget.ExpansionRefs.Length == 0)
        {
            errors.Add("budget_expansion_refs_required");
        }

        if (budget.NonClaims.Length == 0)
        {
            errors.Add("budget_non_claims_required");
        }

        return errors;
    }

    private static PackIdentityEntry EligibleIdentity()
    {
        return new PackIdentityEntry
        {
            PackId = "sample.context_pack",
            PackFamily = "context_pack",
            SchemaVersion = 1,
            LifecycleState = "active",
            ReviewPosture = "reviewed",
            ReviewEvidenceRefs =
            [
                new PackIdentityReviewEvidence
                {
                    Ref = "CARD-925",
                    Scope = "default_context_gate",
                    Reason = "Evidence-backed candidate used only for validation.",
                },
            ],
            DefaultContextPosture = "eligible_candidate",
            ExpansionRefs =
            [
                "docs/runtime/context-pack-builder.md",
            ],
            NonClaims =
            [
                "does_not_activate_pack",
            ],
        };
    }

    private static ContextPackBudgetReadback AcceptableBudget()
    {
        return new ContextPackBudgetReadback
        {
            PackId = "sample.context_pack",
            PackFamily = "context_pack",
            BudgetPosture = "readback_available",
            BudgetLimitEstimatedTokens = 8000,
            EstimatedIncludedTokens = 1200,
            ExpansionRefs =
            [
                "docs/runtime/context-pack-builder.md",
            ],
            NonClaims =
            [
                "does_not_grant_default_context_eligibility",
            ],
        };
    }

    private static void Require(string value, string errorCode, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(errorCode);
        }
    }

    private static PackDefaultContextGateModel LoadGate()
    {
        using var stream = File.OpenRead(Path.Combine(RepoRoot(), "docs", "runtime", "runtime-pack-default-context-gate.json"));
        return JsonSerializer.Deserialize<PackDefaultContextGateModel>(stream, JsonOptions)
               ?? throw new InvalidOperationException("Unable to read Runtime pack default context gate model.");
    }

    private static PackIdentityReadModel LoadIdentity()
    {
        using var stream = File.OpenRead(Path.Combine(RepoRoot(), "docs", "runtime", "runtime-pack-identity-review-posture.json"));
        return JsonSerializer.Deserialize<PackIdentityReadModel>(stream, JsonOptions)
               ?? throw new InvalidOperationException("Unable to read Runtime pack identity review posture model.");
    }

    private static ContextPackBudgetReadbackModel LoadBudget()
    {
        using var stream = File.OpenRead(Path.Combine(RepoRoot(), "docs", "runtime", "runtime-context-pack-budget-readback.json"));
        return JsonSerializer.Deserialize<ContextPackBudgetReadbackModel>(stream, JsonOptions)
               ?? throw new InvalidOperationException("Unable to read Runtime Context Pack budget readback model.");
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

    private sealed record PackDefaultContextGateModel
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("card_id")]
        public string CardId { get; init; } = "";

        [JsonPropertyName("task_id")]
        public string TaskId { get; init; } = "";

        [JsonPropertyName("read_model_kind")]
        public string ReadModelKind { get; init; } = "";

        [JsonPropertyName("authority_posture")]
        public string AuthorityPosture { get; init; } = "";

        [JsonPropertyName("identity_model_ref")]
        public string IdentityModelRef { get; init; } = "";

        [JsonPropertyName("budget_readback_ref")]
        public string BudgetReadbackRef { get; init; } = "";

        [JsonPropertyName("gate_posture")]
        public string GatePosture { get; init; } = "";

        [JsonPropertyName("decision_values")]
        public string[] DecisionValues { get; init; } = [];

        [JsonPropertyName("allowed_default_candidate_families")]
        public string[] AllowedDefaultCandidateFamilies { get; init; } = [];

        [JsonPropertyName("budget_rules")]
        public PackDefaultContextBudgetRules BudgetRules { get; init; } = new();

        [JsonPropertyName("startup_read_path")]
        public PackDefaultContextStartupReadPath StartupReadPath { get; init; } = new();

        [JsonPropertyName("closed_lines")]
        public string[] ClosedLines { get; init; } = [];

        [JsonPropertyName("candidate_decisions")]
        public PackDefaultContextDecision[] CandidateDecisions { get; init; } = [];

        [JsonPropertyName("non_claims")]
        public string[] NonClaims { get; init; } = [];
    }

    private sealed record PackDefaultContextBudgetRules
    {
        [JsonPropertyName("accepted_budget_postures")]
        public string[] AcceptedBudgetPostures { get; init; } = [];
    }

    private sealed record PackDefaultContextStartupReadPath
    {
        [JsonPropertyName("mandatory_startup_reads_changed")]
        public bool MandatoryStartupReadsChanged { get; init; }

        [JsonPropertyName("agents_initialization_reads_changed")]
        public bool AgentsInitializationReadsChanged { get; init; }

        [JsonPropertyName("default_pack_reads_added")]
        public bool DefaultPackReadsAdded { get; init; }

        [JsonPropertyName("runtime_inspect_api_surface_added")]
        public bool RuntimeInspectApiSurfaceAdded { get; init; }

        [JsonPropertyName("automatic_activation_added")]
        public bool AutomaticActivationAdded { get; init; }
    }

    private sealed record PackDefaultContextDecision
    {
        [JsonPropertyName("pack_id")]
        public string PackId { get; init; } = "";

        [JsonPropertyName("pack_family")]
        public string PackFamily { get; init; } = "";

        [JsonPropertyName("budget_posture")]
        public string BudgetPosture { get; init; } = "";

        [JsonPropertyName("source_default_context_posture")]
        public string SourceDefaultContextPosture { get; init; } = "";

        [JsonPropertyName("decision")]
        public string Decision { get; init; } = "";

        [JsonPropertyName("gate_reasons")]
        public string[] GateReasons { get; init; } = [];

        [JsonPropertyName("expansion_refs")]
        public string[] ExpansionRefs { get; init; } = [];

        [JsonPropertyName("non_claims")]
        public string[] NonClaims { get; init; } = [];
    }

    private sealed record PackIdentityReadModel
    {
        [JsonPropertyName("entries")]
        public PackIdentityEntry[] Entries { get; init; } = [];
    }

    private sealed record PackIdentityEntry
    {
        [JsonPropertyName("pack_id")]
        public string PackId { get; init; } = "";

        [JsonPropertyName("pack_family")]
        public string PackFamily { get; init; } = "";

        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("lifecycle_state")]
        public string LifecycleState { get; init; } = "";

        [JsonPropertyName("review_posture")]
        public string ReviewPosture { get; init; } = "";

        [JsonPropertyName("review_evidence_refs")]
        public PackIdentityReviewEvidence[] ReviewEvidenceRefs { get; init; } = [];

        [JsonPropertyName("default_context_posture")]
        public string DefaultContextPosture { get; init; } = "";

        [JsonPropertyName("expansion_refs")]
        public string[] ExpansionRefs { get; init; } = [];

        [JsonPropertyName("non_claims")]
        public string[] NonClaims { get; init; } = [];
    }

    private sealed record PackIdentityReviewEvidence
    {
        [JsonPropertyName("ref")]
        public string Ref { get; init; } = "";

        [JsonPropertyName("scope")]
        public string Scope { get; init; } = "";

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = "";
    }

    private sealed record ContextPackBudgetReadbackModel
    {
        [JsonPropertyName("readbacks")]
        public ContextPackBudgetReadback[] Readbacks { get; init; } = [];
    }

    private sealed record ContextPackBudgetReadback
    {
        [JsonPropertyName("pack_id")]
        public string PackId { get; init; } = "";

        [JsonPropertyName("pack_family")]
        public string PackFamily { get; init; } = "";

        [JsonPropertyName("budget_posture")]
        public string BudgetPosture { get; init; } = "";

        [JsonPropertyName("budget_limit_estimated_tokens")]
        public int BudgetLimitEstimatedTokens { get; init; }

        [JsonPropertyName("estimated_included_tokens")]
        public int EstimatedIncludedTokens { get; init; }

        [JsonPropertyName("expansion_refs")]
        public string[] ExpansionRefs { get; init; } = [];

        [JsonPropertyName("non_claims")]
        public string[] NonClaims { get; init; } = [];
    }
}
