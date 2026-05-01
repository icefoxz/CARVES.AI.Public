using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackColdAuditCompletionGateTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    [Fact]
    public void Audit_DeclaresColdCompletionGateAndClosedPlatformLines()
    {
        var audit = LoadAudit();

        Assert.Equal(1, audit.SchemaVersion);
        Assert.Equal("CARD-927", audit.CardId);
        Assert.Equal("T-CARD-927-001", audit.TaskId);
        Assert.Equal("pack_cold_audit_completion_gate", audit.ReadModelKind);
        Assert.Equal("repo_local_cold_audit_readback", audit.AuthorityPosture);
        Assert.Equal("completion_gate_no_platform_escalation", audit.AuditPosture);
        Assert.Equal("docs/runtime/runtime-pack-identity-review-posture.json", audit.SourceArtifactRefs.IdentityReviewPosture);
        Assert.Equal("docs/runtime/runtime-context-pack-budget-readback.json", audit.SourceArtifactRefs.BudgetReadback);
        Assert.Equal("docs/runtime/runtime-pack-default-context-gate.json", audit.SourceArtifactRefs.DefaultContextGate);
        Assert.Equal("docs/runtime/runtime-pack-agent-phase-usage-contract.json", audit.SourceArtifactRefs.PhaseUsageContract);
        Assert.Contains("not_registry", audit.ClosedLines);
        Assert.Contains("not_rollout_assignment", audit.ClosedLines);
        Assert.Contains("not_automatic_activation", audit.ClosedLines);
        Assert.Contains("not_multi_pack_orchestration", audit.ClosedLines);
        Assert.Contains("audit_success_does_not_open_pack_platform", audit.NonClaims);
    }

    [Fact]
    public void Audit_CountsMatchCurrentIdentityBudgetAndGateArtifacts()
    {
        var audit = LoadAudit();
        var identity = LoadIdentity();
        var budget = LoadBudget();
        var gate = LoadGate();

        var reviewedCount = identity.Entries.Count(entry => entry.ReviewPosture == "reviewed");
        var staleCount = identity.Entries.Count(entry => entry.ReviewPosture == "stale");
        var unreviewedCount = identity.Entries.Count(entry => entry.ReviewPosture == "unreviewed");
        var needsReviewCount = identity.Entries.Count(entry => entry.ReviewPosture == "needs_review");
        var decisionCounts = gate.CandidateDecisions
            .GroupBy(decision => decision.Decision, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(identity.Entries.Length, audit.ColdAuditSummary.PackCandidateCount);
        Assert.Equal(identity.Entries.Length, audit.ColdAuditSummary.IdentityCoveredCount);
        Assert.Equal(reviewedCount, audit.ColdAuditSummary.ReviewedCount);
        Assert.Equal(staleCount, audit.ColdAuditSummary.StaleCount);
        Assert.Equal(unreviewedCount, audit.ColdAuditSummary.UnreviewedCount);
        Assert.Equal(needsReviewCount, audit.ColdAuditSummary.NeedsReviewCount);
        Assert.Equal(budget.Readbacks.Length, audit.ColdAuditSummary.BudgetReadbackCount);
        Assert.Equal(decisionCounts.GetValueOrDefault("eligible_candidate"), audit.ColdAuditSummary.DefaultContextEligibleCandidateCount);
        Assert.Equal(decisionCounts.GetValueOrDefault("pointer_only"), audit.ColdAuditSummary.PointerOnlyCount);
        Assert.Equal(decisionCounts.GetValueOrDefault("blocked"), audit.ColdAuditSummary.BlockedCount);
        Assert.Equal(decisionCounts.GetValueOrDefault("manual_review_required"), audit.ColdAuditSummary.ManualReviewRequiredCount);
        Assert.Equal(budget.Readbacks.Sum(readback => readback.EstimatedIncludedTokens), audit.ColdAuditSummary.EstimatedIncludedTokens);
        Assert.Equal(budget.Readbacks.Sum(readback => readback.EstimatedOmittedTokens), audit.ColdAuditSummary.EstimatedOmittedTokens);
    }

    [Fact]
    public void Audit_CoversEveryPackAndPreservesBudgetDebtReadback()
    {
        var audit = LoadAudit();
        var identity = LoadIdentity();
        var budget = LoadBudget();
        var gate = LoadGate();
        var coverageByPack = audit.CoverageByPack.ToDictionary(pack => pack.PackId, StringComparer.Ordinal);
        var gateByPack = gate.CandidateDecisions.ToDictionary(decision => decision.PackId, StringComparer.Ordinal);
        var budgetByPack = budget.Readbacks.ToDictionary(readback => readback.PackId, StringComparer.Ordinal);

        Assert.Equal(identity.Entries.Length, audit.CoverageByPack.Length);

        foreach (var identityEntry in identity.Entries)
        {
            Assert.True(coverageByPack.TryGetValue(identityEntry.PackId, out var coverage), $"Missing audit coverage for {identityEntry.PackId}.");
            Assert.True(gateByPack.TryGetValue(identityEntry.PackId, out var gateDecision), $"Missing gate decision for {identityEntry.PackId}.");
            Assert.True(coverage.IdentityCovered);
            Assert.Equal(identityEntry.PackFamily, coverage.PackFamily);
            Assert.Equal(identityEntry.ReviewPosture, coverage.ReviewPosture);
            Assert.Equal(gateDecision.Decision, coverage.DefaultContextDecision);
            Assert.NotEmpty(coverage.RemainingDebt);

            if (budgetByPack.TryGetValue(identityEntry.PackId, out var budgetReadback))
            {
                Assert.Equal(budgetReadback.BudgetPosture, coverage.BudgetReadback);
                Assert.Equal(budgetReadback.EstimatedIncludedTokens, coverage.EstimatedIncludedTokens);
                Assert.Equal(budgetReadback.EstimatedOmittedTokens, coverage.EstimatedOmittedTokens);
            }
        }

        var contextPack = coverageByPack["runtime.context_pack"];
        Assert.Equal("readback_available", contextPack.BudgetReadback);
        Assert.Contains("conservative_estimator_not_tokenizer_exact", contextPack.RemainingDebt);
        Assert.Contains("omitted_material_requires_explicit_expansion", contextPack.RemainingDebt);
    }

    [Fact]
    public void CompletionGate_StopsCurrentLineWithoutHidingOptionalDebt()
    {
        var audit = LoadAudit();

        Assert.Equal("stop_current_pack_governance_line", audit.CompletionGate.LineVerdict);
        Assert.False(audit.CompletionGate.ContinueWithTargetedDebt);
        Assert.False(audit.CompletionGate.EscalateToPlatformTrack);
        Assert.True(audit.CompletionGate.RequiresNewOperatorApprovedCardForAnyPlatformWork);
        Assert.NotEmpty(audit.RemainingPackDebt);
        Assert.Contains(audit.RemainingPackDebt, debt => debt.DebtId == "registry_rollout_activation" && debt.CurrentPosture == "explicitly_closed");
        Assert.Contains(audit.RemainingPackDebt, debt => debt.DebtId == "multi_pack_orchestration" && debt.CurrentPosture == "explicitly_closed");
        Assert.Contains(audit.RemainingPackDebt, debt => debt.DebtId == "tokenizer_exact_budgeting" && debt.CurrentPosture == "conservative_estimate_only");
    }

    [Fact]
    public void Validation_RejectsMissingRequiredAuditSourcesAndMismatchedCounts()
    {
        var audit = LoadAudit();
        var identity = LoadIdentity();
        var budget = LoadBudget();
        var gate = LoadGate();

        Assert.Empty(ValidateAudit(audit, identity, budget, gate));

        var missingIdentityRef = audit with
        {
            SourceArtifactRefs = audit.SourceArtifactRefs with { IdentityReviewPosture = "" },
        };
        Assert.Contains("identity_source_ref_required", ValidateAudit(missingIdentityRef, identity, budget, gate));

        var identityMismatch = audit with
        {
            ColdAuditSummary = audit.ColdAuditSummary with { PackCandidateCount = audit.ColdAuditSummary.PackCandidateCount + 1 },
        };
        Assert.Contains("pack_candidate_count_mismatch", ValidateAudit(identityMismatch, identity, budget, gate));

        var budgetMismatch = audit with
        {
            ColdAuditSummary = audit.ColdAuditSummary with { BudgetReadbackCount = 0 },
        };
        Assert.Contains("budget_readback_count_mismatch", ValidateAudit(budgetMismatch, identity, budget, gate));

        var missingContextCoverageBudget = audit with
        {
            CoverageByPack =
            [
                audit.CoverageByPack[0] with { BudgetReadback = "missing_readback" },
                .. audit.CoverageByPack.Skip(1),
            ],
        };
        Assert.Contains("required_context_budget_readback_missing:runtime.context_pack", ValidateAudit(missingContextCoverageBudget, identity, budget, gate));
    }

    [Fact]
    public void Audit_DoesNotAddStartupReadsOrPlatformCapabilities()
    {
        var audit = LoadAudit();

        Assert.False(audit.StartupReadPath.MandatoryStartupReadsChanged);
        Assert.False(audit.StartupReadPath.AgentsInitializationReadsChanged);
        Assert.False(audit.StartupReadPath.DefaultPackReadsAdded);
        Assert.False(audit.StartupReadPath.RuntimeInspectApiSurfaceAdded);
        Assert.False(audit.StartupReadPath.RegistryAdded);
        Assert.False(audit.StartupReadPath.RolloutAdded);
        Assert.False(audit.StartupReadPath.AutomaticActivationAdded);
        Assert.False(audit.StartupReadPath.MultiPackOrchestrationAdded);

        var forbiddenDefaultRead = "docs/runtime/runtime-pack-cold-audit-completion-gate.json";
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

    private static IReadOnlyList<string> ValidateAudit(
        PackColdAuditCompletionGate audit,
        PackIdentityReadModel identity,
        ContextPackBudgetReadbackModel budget,
        PackDefaultContextGate gate)
    {
        var errors = new List<string>();
        Require(audit.SourceArtifactRefs.IdentityReviewPosture, "identity_source_ref_required", errors);
        Require(audit.SourceArtifactRefs.BudgetReadback, "budget_source_ref_required", errors);
        Require(audit.SourceArtifactRefs.DefaultContextGate, "default_context_gate_source_ref_required", errors);
        Require(audit.SourceArtifactRefs.PhaseUsageContract, "phase_usage_contract_source_ref_required", errors);

        if (audit.ColdAuditSummary.PackCandidateCount != identity.Entries.Length)
        {
            errors.Add("pack_candidate_count_mismatch");
        }

        if (audit.ColdAuditSummary.IdentityCoveredCount != identity.Entries.Length)
        {
            errors.Add("identity_covered_count_mismatch");
        }

        if (audit.ColdAuditSummary.BudgetReadbackCount != budget.Readbacks.Length)
        {
            errors.Add("budget_readback_count_mismatch");
        }

        var coverageByPack = audit.CoverageByPack.ToDictionary(pack => pack.PackId, StringComparer.Ordinal);
        var gateByPack = gate.CandidateDecisions.ToDictionary(decision => decision.PackId, StringComparer.Ordinal);
        var budgetByPack = budget.Readbacks.ToDictionary(readback => readback.PackId, StringComparer.Ordinal);

        foreach (var identityEntry in identity.Entries)
        {
            if (!coverageByPack.TryGetValue(identityEntry.PackId, out var coverage))
            {
                errors.Add($"missing_pack_coverage:{identityEntry.PackId}");
                continue;
            }

            if (!coverage.IdentityCovered)
            {
                errors.Add($"identity_not_covered:{identityEntry.PackId}");
            }

            if (coverage.ReviewPosture != identityEntry.ReviewPosture)
            {
                errors.Add($"review_posture_mismatch:{identityEntry.PackId}");
            }

            if (gateByPack.TryGetValue(identityEntry.PackId, out var gateDecision)
                && coverage.DefaultContextDecision != gateDecision.Decision)
            {
                errors.Add($"gate_decision_mismatch:{identityEntry.PackId}");
            }

            if (budgetByPack.TryGetValue(identityEntry.PackId, out var budgetReadback)
                && coverage.BudgetReadback != budgetReadback.BudgetPosture)
            {
                errors.Add($"required_context_budget_readback_missing:{identityEntry.PackId}");
            }
        }

        if (audit.CompletionGate.LineVerdict != "stop_current_pack_governance_line")
        {
            errors.Add("line_verdict_must_stop_current_line");
        }

        if (audit.CompletionGate.EscalateToPlatformTrack)
        {
            errors.Add("platform_escalation_forbidden");
        }

        return errors;
    }

    private static void Require(string value, string errorCode, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(errorCode);
        }
    }

    private static PackColdAuditCompletionGate LoadAudit()
    {
        using var stream = File.OpenRead(Path.Combine(RepoRoot(), "docs", "runtime", "runtime-pack-cold-audit-completion-gate.json"));
        return JsonSerializer.Deserialize<PackColdAuditCompletionGate>(stream, JsonOptions)
               ?? throw new InvalidOperationException("Unable to read Runtime pack cold audit completion gate.");
    }

    private static PackIdentityReadModel LoadIdentity()
    {
        using var stream = File.OpenRead(Path.Combine(RepoRoot(), "docs", "runtime", "runtime-pack-identity-review-posture.json"));
        return JsonSerializer.Deserialize<PackIdentityReadModel>(stream, JsonOptions)
               ?? throw new InvalidOperationException("Unable to read Runtime pack identity review posture.");
    }

    private static ContextPackBudgetReadbackModel LoadBudget()
    {
        using var stream = File.OpenRead(Path.Combine(RepoRoot(), "docs", "runtime", "runtime-context-pack-budget-readback.json"));
        return JsonSerializer.Deserialize<ContextPackBudgetReadbackModel>(stream, JsonOptions)
               ?? throw new InvalidOperationException("Unable to read Runtime Context Pack budget readback.");
    }

    private static PackDefaultContextGate LoadGate()
    {
        using var stream = File.OpenRead(Path.Combine(RepoRoot(), "docs", "runtime", "runtime-pack-default-context-gate.json"));
        return JsonSerializer.Deserialize<PackDefaultContextGate>(stream, JsonOptions)
               ?? throw new InvalidOperationException("Unable to read Runtime pack default context gate.");
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

    private sealed record PackColdAuditCompletionGate
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

        [JsonPropertyName("audit_posture")]
        public string AuditPosture { get; init; } = "";

        [JsonPropertyName("source_artifact_refs")]
        public SourceArtifactRefs SourceArtifactRefs { get; init; } = new();

        [JsonPropertyName("cold_audit_summary")]
        public ColdAuditSummary ColdAuditSummary { get; init; } = new();

        [JsonPropertyName("coverage_by_pack")]
        public PackCoverage[] CoverageByPack { get; init; } = [];

        [JsonPropertyName("remaining_pack_debt")]
        public RemainingPackDebt[] RemainingPackDebt { get; init; } = [];

        [JsonPropertyName("completion_gate")]
        public CompletionGate CompletionGate { get; init; } = new();

        [JsonPropertyName("startup_read_path")]
        public StartupReadPath StartupReadPath { get; init; } = new();

        [JsonPropertyName("closed_lines")]
        public string[] ClosedLines { get; init; } = [];

        [JsonPropertyName("non_claims")]
        public string[] NonClaims { get; init; } = [];
    }

    private sealed record SourceArtifactRefs
    {
        [JsonPropertyName("identity_review_posture")]
        public string IdentityReviewPosture { get; init; } = "";

        [JsonPropertyName("budget_readback")]
        public string BudgetReadback { get; init; } = "";

        [JsonPropertyName("default_context_gate")]
        public string DefaultContextGate { get; init; } = "";

        [JsonPropertyName("phase_usage_contract")]
        public string PhaseUsageContract { get; init; } = "";
    }

    private sealed record ColdAuditSummary
    {
        [JsonPropertyName("pack_candidate_count")]
        public int PackCandidateCount { get; init; }

        [JsonPropertyName("identity_covered_count")]
        public int IdentityCoveredCount { get; init; }

        [JsonPropertyName("reviewed_count")]
        public int ReviewedCount { get; init; }

        [JsonPropertyName("stale_count")]
        public int StaleCount { get; init; }

        [JsonPropertyName("unreviewed_count")]
        public int UnreviewedCount { get; init; }

        [JsonPropertyName("needs_review_count")]
        public int NeedsReviewCount { get; init; }

        [JsonPropertyName("budget_readback_count")]
        public int BudgetReadbackCount { get; init; }

        [JsonPropertyName("default_context_eligible_candidate_count")]
        public int DefaultContextEligibleCandidateCount { get; init; }

        [JsonPropertyName("pointer_only_count")]
        public int PointerOnlyCount { get; init; }

        [JsonPropertyName("blocked_count")]
        public int BlockedCount { get; init; }

        [JsonPropertyName("manual_review_required_count")]
        public int ManualReviewRequiredCount { get; init; }

        [JsonPropertyName("estimated_included_tokens")]
        public int EstimatedIncludedTokens { get; init; }

        [JsonPropertyName("estimated_omitted_tokens")]
        public int EstimatedOmittedTokens { get; init; }
    }

    private sealed record PackCoverage
    {
        [JsonPropertyName("pack_id")]
        public string PackId { get; init; } = "";

        [JsonPropertyName("pack_family")]
        public string PackFamily { get; init; } = "";

        [JsonPropertyName("identity_covered")]
        public bool IdentityCovered { get; init; }

        [JsonPropertyName("review_posture")]
        public string ReviewPosture { get; init; } = "";

        [JsonPropertyName("budget_readback")]
        public string BudgetReadback { get; init; } = "";

        [JsonPropertyName("default_context_decision")]
        public string DefaultContextDecision { get; init; } = "";

        [JsonPropertyName("estimated_included_tokens")]
        public int EstimatedIncludedTokens { get; init; }

        [JsonPropertyName("estimated_omitted_tokens")]
        public int EstimatedOmittedTokens { get; init; }

        [JsonPropertyName("remaining_debt")]
        public string[] RemainingDebt { get; init; } = [];
    }

    private sealed record RemainingPackDebt
    {
        [JsonPropertyName("debt_id")]
        public string DebtId { get; init; } = "";

        [JsonPropertyName("current_posture")]
        public string CurrentPosture { get; init; } = "";
    }

    private sealed record CompletionGate
    {
        [JsonPropertyName("line_verdict")]
        public string LineVerdict { get; init; } = "";

        [JsonPropertyName("continue_with_targeted_debt")]
        public bool ContinueWithTargetedDebt { get; init; }

        [JsonPropertyName("escalate_to_platform_track")]
        public bool EscalateToPlatformTrack { get; init; }

        [JsonPropertyName("requires_new_operator_approved_card_for_any_platform_work")]
        public bool RequiresNewOperatorApprovedCardForAnyPlatformWork { get; init; }
    }

    private sealed record StartupReadPath
    {
        [JsonPropertyName("mandatory_startup_reads_changed")]
        public bool MandatoryStartupReadsChanged { get; init; }

        [JsonPropertyName("agents_initialization_reads_changed")]
        public bool AgentsInitializationReadsChanged { get; init; }

        [JsonPropertyName("default_pack_reads_added")]
        public bool DefaultPackReadsAdded { get; init; }

        [JsonPropertyName("runtime_inspect_api_surface_added")]
        public bool RuntimeInspectApiSurfaceAdded { get; init; }

        [JsonPropertyName("registry_added")]
        public bool RegistryAdded { get; init; }

        [JsonPropertyName("rollout_added")]
        public bool RolloutAdded { get; init; }

        [JsonPropertyName("automatic_activation_added")]
        public bool AutomaticActivationAdded { get; init; }

        [JsonPropertyName("multi_pack_orchestration_added")]
        public bool MultiPackOrchestrationAdded { get; init; }
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

        [JsonPropertyName("review_posture")]
        public string ReviewPosture { get; init; } = "";
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

        [JsonPropertyName("budget_posture")]
        public string BudgetPosture { get; init; } = "";

        [JsonPropertyName("estimated_included_tokens")]
        public int EstimatedIncludedTokens { get; init; }

        [JsonPropertyName("estimated_omitted_tokens")]
        public int EstimatedOmittedTokens { get; init; }
    }

    private sealed record PackDefaultContextGate
    {
        [JsonPropertyName("candidate_decisions")]
        public PackDefaultContextDecision[] CandidateDecisions { get; init; } = [];
    }

    private sealed record PackDefaultContextDecision
    {
        [JsonPropertyName("pack_id")]
        public string PackId { get; init; } = "";

        [JsonPropertyName("decision")]
        public string Decision { get; init; } = "";
    }
}
