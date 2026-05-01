using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackAgentPhaseUsageContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    private static readonly string[] RequiredPhaseIds =
    [
        "planning",
        "implementation",
        "review",
        "cleanup",
        "audit",
    ];

    [Fact]
    public void Contract_DeclaresPointerFirstUsageContractAndClosedLines()
    {
        var contract = LoadContract();

        Assert.Equal(1, contract.SchemaVersion);
        Assert.Equal("CARD-926", contract.CardId);
        Assert.Equal("T-CARD-926-001", contract.TaskId);
        Assert.Equal("pack_agent_phase_usage_contract", contract.ReadModelKind);
        Assert.Equal("repo_local_usage_contract", contract.AuthorityPosture);
        Assert.Equal("pointer_first_phase_specific_no_orchestration", contract.ContractPosture);
        Assert.Equal("docs/runtime/runtime-pack-identity-review-posture.json", contract.IdentityModelRef);
        Assert.Equal("docs/runtime/runtime-context-pack-budget-readback.json", contract.BudgetReadbackRef);
        Assert.Equal("docs/runtime/runtime-pack-default-context-gate.json", contract.DefaultContextGateRef);
        Assert.Contains("identity_header", contract.PackLayers);
        Assert.Contains("review_posture", contract.PackLayers);
        Assert.Contains("budget_readback", contract.PackLayers);
        Assert.Contains("summary", contract.PackLayers);
        Assert.Contains("source_excerpt", contract.PackLayers);
        Assert.Contains("full_expansion", contract.PackLayers);
        Assert.Contains("full_expansion_never_default", contract.UniversalRules);
        Assert.Contains("expansion_requires_task_relevant_reason", contract.UniversalRules);
        Assert.Contains("not_startup_read_path", contract.ClosedLines);
        Assert.Contains("not_agents_initialization_change", contract.ClosedLines);
        Assert.Contains("not_runtime_inspect_or_api_surface", contract.ClosedLines);
        Assert.Contains("not_agent_orchestration", contract.ClosedLines);
        Assert.Contains("not_multi_pack_runtime_scheduling", contract.ClosedLines);
        Assert.Contains("not_task_acceptance_override", contract.ClosedLines);
    }

    [Fact]
    public void Contract_DefinesAllRequiredPhasesWithPointerFirstExpansionRules()
    {
        var contract = LoadContract();
        var errors = ValidateContract(contract);

        Assert.Empty(errors);
        Assert.Equal(RequiredPhaseIds, contract.RequiredPhaseIds);

        foreach (var phaseId in RequiredPhaseIds)
        {
            var phase = Assert.Single(contract.PhaseRules, rule => rule.PhaseId == phaseId);
            Assert.NotEmpty(phase.FirstReadLayers);
            Assert.DoesNotContain("full_expansion", phase.FirstReadLayers);
            Assert.False(phase.FullExpansionDefault);
            Assert.True(phase.ExpansionRequiresTaskRelevantReason);
            Assert.True(phase.PreserveExpansionRefs);
            Assert.NotEmpty(phase.RequiredExpansionReasonKinds);
            Assert.NotEmpty(phase.MustNot);
        }
    }

    [Fact]
    public void Contract_CoversDefaultContextGateDecisionValues()
    {
        var contract = LoadContract();
        var gate = LoadGate();
        var usageByDecision = contract.PackDecisionUsage.ToDictionary(usage => usage.Decision, StringComparer.Ordinal);

        foreach (var decision in gate.DecisionValues)
        {
            Assert.True(usageByDecision.ContainsKey(decision), $"Missing usage contract for gate decision {decision}.");
        }

        Assert.True(usageByDecision["eligible_candidate"].PhaseContractRequired);
        Assert.True(usageByDecision["pointer_only"].PhaseContractRequired);
        Assert.True(usageByDecision["manual_review_required"].PhaseContractRequired);
        Assert.False(usageByDecision["blocked"].PhaseContractRequired);
        Assert.All(contract.PackDecisionUsage, usage => Assert.False(usage.FullExpansionDefault));
    }

    [Fact]
    public void Validation_RejectsMissingPhaseRulesAndDefaultFullExpansion()
    {
        var contract = LoadContract();

        var missingPlanning = contract with
        {
            PhaseRules = contract.PhaseRules.Where(rule => rule.PhaseId != "planning").ToArray(),
        };
        Assert.Contains("missing_phase_rule:planning", ValidateContract(missingPlanning));

        var fullExpansionFirst = contract with
        {
            PhaseRules =
            [
                contract.PhaseRules[0] with
                {
                    FirstReadLayers = ["identity_header", "full_expansion"],
                },
                .. contract.PhaseRules.Skip(1),
            ],
        };
        Assert.Contains("full_expansion_first_read_forbidden:planning", ValidateContract(fullExpansionFirst));

        var defaultFullExpansion = contract with
        {
            PhaseRules =
            [
                contract.PhaseRules[0] with
                {
                    FullExpansionDefault = true,
                },
                .. contract.PhaseRules.Skip(1),
            ],
        };
        Assert.Contains("full_expansion_default_forbidden:planning", ValidateContract(defaultFullExpansion));
    }

    [Fact]
    public void Validation_RejectsCandidateUsageWithoutPhaseContractOrExpansionPointers()
    {
        var contract = LoadContract();

        var candidateWithoutContract = contract with
        {
            PackDecisionUsage =
            [
                contract.PackDecisionUsage[0] with
                {
                    Decision = "eligible_candidate",
                    PhaseContractRequired = false,
                },
                .. contract.PackDecisionUsage.Skip(1),
            ],
        };
        Assert.Contains("candidate_decision_requires_phase_contract:eligible_candidate", ValidateContract(candidateWithoutContract));

        var missingExpansionReasons = contract with
        {
            PhaseRules =
            [
                contract.PhaseRules[0] with
                {
                    RequiredExpansionReasonKinds = [],
                },
                .. contract.PhaseRules.Skip(1),
            ],
        };
        Assert.Contains("expansion_reason_required:planning", ValidateContract(missingExpansionReasons));

        var missingExpansionRefs = contract with
        {
            PhaseRules =
            [
                contract.PhaseRules[0] with
                {
                    PreserveExpansionRefs = false,
                },
                .. contract.PhaseRules.Skip(1),
            ],
        };
        Assert.Contains("expansion_refs_preservation_required:planning", ValidateContract(missingExpansionRefs));
    }

    [Fact]
    public void Contract_DoesNotAddStartupReadsOrOrchestration()
    {
        var contract = LoadContract();

        Assert.False(contract.StartupReadPath.MandatoryStartupReadsChanged);
        Assert.False(contract.StartupReadPath.AgentsInitializationReadsChanged);
        Assert.False(contract.StartupReadPath.DefaultPackReadsAdded);
        Assert.False(contract.StartupReadPath.RuntimeInspectApiSurfaceAdded);
        Assert.False(contract.StartupReadPath.AutomaticOrchestrationAdded);
        Assert.False(contract.StartupReadPath.MultiPackSchedulingAdded);

        var forbiddenDefaultRead = "docs/runtime/runtime-pack-agent-phase-usage-contract.json";
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

    private static IReadOnlyList<string> ValidateContract(PackAgentPhaseUsageContract contract)
    {
        var errors = new List<string>();
        var phaseById = contract.PhaseRules.ToDictionary(rule => rule.PhaseId, StringComparer.Ordinal);

        foreach (var requiredPhase in RequiredPhaseIds)
        {
            if (!phaseById.ContainsKey(requiredPhase))
            {
                errors.Add($"missing_phase_rule:{requiredPhase}");
            }
        }

        foreach (var phase in contract.PhaseRules)
        {
            Require(phase.PhaseId, "missing_phase_id", errors);

            if (phase.FirstReadLayers.Length == 0)
            {
                errors.Add($"first_read_layers_required:{phase.PhaseId}");
            }

            if (phase.FirstReadLayers.Contains("full_expansion", StringComparer.Ordinal))
            {
                errors.Add($"full_expansion_first_read_forbidden:{phase.PhaseId}");
            }

            if (phase.FullExpansionDefault)
            {
                errors.Add($"full_expansion_default_forbidden:{phase.PhaseId}");
            }

            if (!phase.ExpansionRequiresTaskRelevantReason)
            {
                errors.Add($"task_relevant_reason_required:{phase.PhaseId}");
            }

            if (phase.RequiredExpansionReasonKinds.Length == 0)
            {
                errors.Add($"expansion_reason_required:{phase.PhaseId}");
            }

            if (!phase.PreserveExpansionRefs)
            {
                errors.Add($"expansion_refs_preservation_required:{phase.PhaseId}");
            }

            if (phase.MustNot.Length == 0)
            {
                errors.Add($"must_not_rules_required:{phase.PhaseId}");
            }
        }

        var decisionUsage = contract.PackDecisionUsage.ToDictionary(usage => usage.Decision, StringComparer.Ordinal);
        foreach (var candidateDecision in new[] { "eligible_candidate", "pointer_only", "manual_review_required" })
        {
            if (!decisionUsage.TryGetValue(candidateDecision, out var usage) || !usage.PhaseContractRequired)
            {
                errors.Add($"candidate_decision_requires_phase_contract:{candidateDecision}");
            }
        }

        foreach (var usage in contract.PackDecisionUsage)
        {
            if (usage.FullExpansionDefault)
            {
                errors.Add($"decision_full_expansion_default_forbidden:{usage.Decision}");
            }

            if (!usage.ExpansionRequiresTaskRelevantReason)
            {
                errors.Add($"decision_task_reason_required:{usage.Decision}");
            }
        }

        if (contract.StartupReadPath.MandatoryStartupReadsChanged)
        {
            errors.Add("startup_reads_changed");
        }

        if (contract.StartupReadPath.AgentsInitializationReadsChanged)
        {
            errors.Add("agents_initialization_changed");
        }

        if (contract.StartupReadPath.RuntimeInspectApiSurfaceAdded)
        {
            errors.Add("inspect_api_surface_added");
        }

        if (contract.StartupReadPath.AutomaticOrchestrationAdded)
        {
            errors.Add("automatic_orchestration_added");
        }

        if (contract.StartupReadPath.MultiPackSchedulingAdded)
        {
            errors.Add("multi_pack_scheduling_added");
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

    private static PackAgentPhaseUsageContract LoadContract()
    {
        using var stream = File.OpenRead(Path.Combine(RepoRoot(), "docs", "runtime", "runtime-pack-agent-phase-usage-contract.json"));
        return JsonSerializer.Deserialize<PackAgentPhaseUsageContract>(stream, JsonOptions)
               ?? throw new InvalidOperationException("Unable to read Runtime pack agent phase usage contract.");
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

    private sealed record PackAgentPhaseUsageContract
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

        [JsonPropertyName("contract_posture")]
        public string ContractPosture { get; init; } = "";

        [JsonPropertyName("identity_model_ref")]
        public string IdentityModelRef { get; init; } = "";

        [JsonPropertyName("budget_readback_ref")]
        public string BudgetReadbackRef { get; init; } = "";

        [JsonPropertyName("default_context_gate_ref")]
        public string DefaultContextGateRef { get; init; } = "";

        [JsonPropertyName("pack_layers")]
        public string[] PackLayers { get; init; } = [];

        [JsonPropertyName("required_phase_ids")]
        public string[] RequiredPhaseIds { get; init; } = [];

        [JsonPropertyName("universal_rules")]
        public string[] UniversalRules { get; init; } = [];

        [JsonPropertyName("pack_decision_usage")]
        public PackDecisionUsage[] PackDecisionUsage { get; init; } = [];

        [JsonPropertyName("phase_rules")]
        public PhaseUsageRule[] PhaseRules { get; init; } = [];

        [JsonPropertyName("startup_read_path")]
        public StartupReadPath StartupReadPath { get; init; } = new();

        [JsonPropertyName("closed_lines")]
        public string[] ClosedLines { get; init; } = [];
    }

    private sealed record PackDecisionUsage
    {
        [JsonPropertyName("decision")]
        public string Decision { get; init; } = "";

        [JsonPropertyName("phase_contract_required")]
        public bool PhaseContractRequired { get; init; }

        [JsonPropertyName("full_expansion_default")]
        public bool FullExpansionDefault { get; init; }

        [JsonPropertyName("expansion_requires_task_relevant_reason")]
        public bool ExpansionRequiresTaskRelevantReason { get; init; }
    }

    private sealed record PhaseUsageRule
    {
        [JsonPropertyName("phase_id")]
        public string PhaseId { get; init; } = "";

        [JsonPropertyName("first_read_layers")]
        public string[] FirstReadLayers { get; init; } = [];

        [JsonPropertyName("allowed_next_layers")]
        public string[] AllowedNextLayers { get; init; } = [];

        [JsonPropertyName("full_expansion_default")]
        public bool FullExpansionDefault { get; init; }

        [JsonPropertyName("expansion_requires_task_relevant_reason")]
        public bool ExpansionRequiresTaskRelevantReason { get; init; }

        [JsonPropertyName("required_expansion_reason_kinds")]
        public string[] RequiredExpansionReasonKinds { get; init; } = [];

        [JsonPropertyName("preserve_expansion_refs")]
        public bool PreserveExpansionRefs { get; init; }

        [JsonPropertyName("must_not")]
        public string[] MustNot { get; init; } = [];
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

        [JsonPropertyName("automatic_orchestration_added")]
        public bool AutomaticOrchestrationAdded { get; init; }

        [JsonPropertyName("multi_pack_scheduling_added")]
        public bool MultiPackSchedulingAdded { get; init; }
    }

    private sealed record PackDefaultContextGate
    {
        [JsonPropertyName("decision_values")]
        public string[] DecisionValues { get; init; } = [];
    }
}
