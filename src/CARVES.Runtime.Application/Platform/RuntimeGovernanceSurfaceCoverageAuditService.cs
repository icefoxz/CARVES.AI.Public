using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeGovernanceSurfaceCoverageAuditService
{
    private const int MaxGovernanceCriticalSurfaceCount = 12;
    private const int MaxDefaultPathSurfaceCount = 2;
    private const int MaxAuditHandoffSurfaceCount = 4;
    private const string QuickstartPath = "docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md";
    private const string ConsumerResourcePackPath = "docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md";
    private const string IntegrationTestRoot = "tests/Carves.Runtime.IntegrationTests";

    private static readonly string[] CoverageDimensions =
    [
        "runtime_surface_registry",
        "inspect_usage",
        "api_usage",
        "external_consumer_resource_pack_command",
        "external_agent_quickstart_documentation",
        "external_consumer_resource_pack_documentation",
        "host_contract_test_evidence",
        "surface_lifecycle_class",
        "read_path_budget_class",
    ];

    private static readonly GovernanceSurfaceCoverageRequirement[] Requirements =
    [
        new(
            SurfaceId: "runtime-agent-thread-start",
            Role: "first_thread_entry_surface",
            ExternalAgentVisible: true,
            ResourcePackRequired: true,
            ResourcePackNeedles: ["runtime-agent-thread-start", "carves agent start --json"],
            QuickstartRequired: true,
            QuickstartNeedles: ["carves agent start --json"],
            ConsumerPackRequired: true,
            ConsumerPackNeedles: ["runtime-agent-thread-start", "carves agent start --json"],
            LifecycleClass: "active_default_entry",
            ReadPathClass: "default_path",
            DefaultPathParticipation: "first_thread_required",
            HostContractEvidencePaths: ["tests/Carves.Runtime.IntegrationTests/RuntimeGovernedAgentHandoffHostContractTests.cs"]),
        new(
            SurfaceId: "runtime-agent-short-context",
            Role: "single_short_context_aggregate",
            ExternalAgentVisible: true,
            ResourcePackRequired: true,
            ResourcePackNeedles: ["runtime-agent-short-context", "carves agent context --json"],
            QuickstartRequired: true,
            QuickstartNeedles: ["carves agent context --json", "runtime-agent-short-context"],
            ConsumerPackRequired: true,
            ConsumerPackNeedles: ["runtime-agent-short-context", "carves agent context --json"],
            LifecycleClass: "active_default_reorientation",
            ReadPathClass: "default_path",
            DefaultPathParticipation: "warm_reorientation_required",
            HostContractEvidencePaths: ["tests/Carves.Runtime.IntegrationTests/RuntimeKernelHostContractTests.cs"]),
        new(
            SurfaceId: "runtime-markdown-read-path-budget",
            Role: "markdown_read_path_budget",
            ExternalAgentVisible: true,
            ResourcePackRequired: true,
            ResourcePackNeedles: ["runtime-markdown-read-path-budget", "carves api runtime-markdown-read-path-budget"],
            QuickstartRequired: true,
            QuickstartNeedles: ["runtime-markdown-read-path-budget", "carves api runtime-markdown-read-path-budget"],
            ConsumerPackRequired: true,
            ConsumerPackNeedles: ["runtime-markdown-read-path-budget", "carves api runtime-markdown-read-path-budget"],
            LifecycleClass: "active_decision_support",
            ReadPathClass: "conditional_decision_support",
            DefaultPathParticipation: "optional_budget_check",
            HostContractEvidencePaths: ["tests/Carves.Runtime.IntegrationTests/RuntimeKernelHostContractTests.cs"]),
        new(
            SurfaceId: "runtime-worker-execution-audit",
            Role: "worker_execution_audit_query",
            ExternalAgentVisible: true,
            ResourcePackRequired: true,
            ResourcePackNeedles: ["runtime-worker-execution-audit", "carves api runtime-worker-execution-audit"],
            QuickstartRequired: true,
            QuickstartNeedles: ["runtime-worker-execution-audit", "carves api runtime-worker-execution-audit"],
            ConsumerPackRequired: true,
            ConsumerPackNeedles: ["runtime-worker-execution-audit", "carves api runtime-worker-execution-audit"],
            LifecycleClass: "active_audit_query",
            ReadPathClass: "audit_handoff",
            DefaultPathParticipation: "troubleshooting_only",
            HostContractEvidencePaths: ["tests/Carves.Runtime.IntegrationTests/RuntimeWorkerExecutionAuditHostContractTests.cs"]),
        new(
            SurfaceId: "runtime-governed-agent-handoff-proof",
            Role: "governed_agent_handoff_contract",
            ExternalAgentVisible: true,
            ResourcePackRequired: true,
            ResourcePackNeedles: ["runtime-governed-agent-handoff-proof", "carves agent handoff --json"],
            QuickstartRequired: false,
            QuickstartNeedles: [],
            ConsumerPackRequired: true,
            ConsumerPackNeedles: ["runtime-governed-agent-handoff-proof", "carves agent handoff --json"],
            LifecycleClass: "active_handoff_proof",
            ReadPathClass: "audit_handoff",
            DefaultPathParticipation: "handoff_only",
            HostContractEvidencePaths: ["tests/Carves.Runtime.IntegrationTests/RuntimeGovernedAgentHandoffHostContractTests.cs"]),
        new(
            SurfaceId: "runtime-agent-bootstrap-packet",
            Role: "low_context_bootstrap_packet",
            ExternalAgentVisible: true,
            ResourcePackRequired: false,
            ResourcePackNeedles: [],
            QuickstartRequired: true,
            QuickstartNeedles: ["bootstrap packet"],
            ConsumerPackRequired: true,
            ConsumerPackNeedles: ["bootstrap packet"],
            LifecycleClass: "active_bootstrap_detail",
            ReadPathClass: "task_scoped_detail",
            DefaultPathParticipation: "detail_only",
            HostContractEvidencePaths: ["tests/Carves.Runtime.IntegrationTests/RuntimeKernelHostContractTests.cs"]),
        new(
            SurfaceId: "runtime-agent-task-overlay",
            Role: "task_scoped_governance_overlay",
            ExternalAgentVisible: true,
            ResourcePackRequired: false,
            ResourcePackNeedles: [],
            QuickstartRequired: true,
            QuickstartNeedles: ["task overlay"],
            ConsumerPackRequired: true,
            ConsumerPackNeedles: ["task overlay"],
            LifecycleClass: "active_task_detail",
            ReadPathClass: "task_scoped_detail",
            DefaultPathParticipation: "task_detail_only",
            HostContractEvidencePaths: ["tests/Carves.Runtime.IntegrationTests/RuntimeKernelHostContractTests.cs"]),
        new(
            SurfaceId: "execution-packet",
            Role: "task_execution_packet",
            ExternalAgentVisible: false,
            ResourcePackRequired: false,
            ResourcePackNeedles: [],
            QuickstartRequired: false,
            QuickstartNeedles: [],
            ConsumerPackRequired: false,
            ConsumerPackNeedles: [],
            LifecycleClass: "active_execution_contract_detail",
            ReadPathClass: "task_scoped_detail",
            DefaultPathParticipation: "task_detail_only",
            HostContractEvidencePaths: ["tests/Carves.Runtime.IntegrationTests/HostContractTests.cs"]),
        new(
            SurfaceId: "packet-enforcement",
            Role: "pre_execution_packet_enforcement",
            ExternalAgentVisible: false,
            ResourcePackRequired: false,
            ResourcePackNeedles: [],
            QuickstartRequired: false,
            QuickstartNeedles: [],
            ConsumerPackRequired: false,
            ConsumerPackNeedles: [],
            LifecycleClass: "active_internal_gate",
            ReadPathClass: "internal_gate",
            DefaultPathParticipation: "not_default",
            HostContractEvidencePaths: ["tests/Carves.Runtime.IntegrationTests/HostContractTests.cs"]),
        new(
            SurfaceId: "runtime-brokered-execution",
            Role: "brokered_execution_review_boundary",
            ExternalAgentVisible: false,
            ResourcePackRequired: false,
            ResourcePackNeedles: [],
            QuickstartRequired: false,
            QuickstartNeedles: [],
            ConsumerPackRequired: false,
            ConsumerPackNeedles: [],
            LifecycleClass: "active_review_boundary_detail",
            ReadPathClass: "internal_gate",
            DefaultPathParticipation: "not_default",
            HostContractEvidencePaths: ["tests/Carves.Runtime.IntegrationTests/RuntimeBrokeredExecutionHostContractTests.cs"]),
        new(
            SurfaceId: "runtime-workspace-mutation-audit",
            Role: "workspace_mutation_writeback_guard",
            ExternalAgentVisible: false,
            ResourcePackRequired: false,
            ResourcePackNeedles: [],
            QuickstartRequired: false,
            QuickstartNeedles: [],
            ConsumerPackRequired: false,
            ConsumerPackNeedles: [],
            LifecycleClass: "active_writeback_guard",
            ReadPathClass: "internal_gate",
            DefaultPathParticipation: "not_default",
            HostContractEvidencePaths: ["tests/Carves.Runtime.IntegrationTests/RuntimeGovernedAgentHandoffHostContractTests.cs"]),
        new(
            SurfaceId: "runtime-default-workflow-proof",
            Role: "default_workflow_proof",
            ExternalAgentVisible: true,
            ResourcePackRequired: true,
            ResourcePackNeedles: ["runtime-default-workflow-proof", "carves api runtime-default-workflow-proof"],
            QuickstartRequired: true,
            QuickstartNeedles: ["runtime-default-workflow-proof", "carves api runtime-default-workflow-proof"],
            ConsumerPackRequired: true,
            ConsumerPackNeedles: ["runtime-default-workflow-proof", "carves api runtime-default-workflow-proof"],
            LifecycleClass: "active_workflow_proof",
            ReadPathClass: "audit_handoff",
            DefaultPathParticipation: "handoff_only",
            HostContractEvidencePaths: ["tests/Carves.Runtime.IntegrationTests/RuntimeDefaultWorkflowProofHostContractTests.cs"]),
    ];

    private readonly string repoRoot;

    public RuntimeGovernanceSurfaceCoverageAuditService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeGovernanceSurfaceCoverageAuditSurface Build(
        IReadOnlyCollection<string> registeredSurfaceNames,
        RuntimeExternalConsumerResourcePackSurface externalConsumerResourcePack)
    {
        var registered = registeredSurfaceNames.ToHashSet(StringComparer.Ordinal);
        var inspectUsage = string.Join('\n', RuntimeSurfaceCommandRegistry.BuildHelpLines("inspect"));
        var apiUsage = string.Join('\n', RuntimeSurfaceCommandRegistry.BuildHelpLines("api"));
        var quickstart = ReadRepoFile(QuickstartPath);
        var consumerPack = ReadRepoFile(ConsumerResourcePackPath);
        var commandEntries = externalConsumerResourcePack.CommandEntries;

        var entries = Requirements
            .Select(requirement => BuildEntry(requirement, registered, inspectUsage, apiUsage, quickstart, consumerPack, commandEntries))
            .ToArray();
        var lifecycleBudgetGaps = BuildLifecycleBudgetGaps(entries).ToArray();
        var blockingGaps = entries.SelectMany(static entry => entry.Gaps).Concat(lifecycleBudgetGaps).ToArray();
        var advisoryGaps = entries.SelectMany(static entry => entry.AdvisoryGaps).ToArray();
        var coverageComplete = blockingGaps.Length == 0;
        var defaultPathSurfaceCount = entries.Count(static entry => entry.CountsTowardDefaultPathBudget);
        var auditHandoffSurfaceCount = entries.Count(static entry => string.Equals(entry.ReadPathClass, "audit_handoff", StringComparison.Ordinal));

        return new RuntimeGovernanceSurfaceCoverageAuditSurface
        {
            RepoRoot = repoRoot,
            OverallPosture = coverageComplete
                ? "governance_surface_coverage_ready"
                : "governance_surface_coverage_blocked_by_gaps",
            CoverageComplete = coverageComplete,
            LifecycleBudgetComplete = lifecycleBudgetGaps.Length == 0,
            RegisteredSurfaceCount = registered.Count,
            RequiredSurfaceCount = entries.Length,
            MaxGovernanceCriticalSurfaceCount = MaxGovernanceCriticalSurfaceCount,
            CoveredSurfaceCount = entries.Count(static entry => string.Equals(entry.CoverageStatus, "covered", StringComparison.Ordinal)),
            DefaultPathSurfaceCount = defaultPathSurfaceCount,
            MaxDefaultPathSurfaceCount = MaxDefaultPathSurfaceCount,
            AuditHandoffSurfaceCount = auditHandoffSurfaceCount,
            MaxAuditHandoffSurfaceCount = MaxAuditHandoffSurfaceCount,
            BlockingGapCount = blockingGaps.Length,
            AdvisoryGapCount = advisoryGaps.Length,
            CoverageDimensions = CoverageDimensions,
            RequiredSurfaces = Requirements.Select(static requirement => requirement.SurfaceId).ToArray(),
            Entries = entries,
            Gaps = blockingGaps,
            AdvisoryGaps = advisoryGaps,
            LifecycleBudgetGaps = lifecycleBudgetGaps,
            EvidenceSourcePaths = BuildEvidenceSourcePaths().ToArray(),
            Summary = coverageComplete
                ? $"Governance surface coverage audit found {entries.Length}/{entries.Length} bounded governance surfaces wired through required registry, usage, docs/resource-pack, host-contract evidence, and lifecycle/read-path budgets."
                : $"Governance surface coverage audit found {blockingGaps.Length} blocking gap(s) across {entries.Length} bounded governance surfaces.",
            RecommendedNextAction = coverageComplete
                ? "Use this read-only audit before alpha handoff or governance-surface changes; keep behavior validation in the owning host-contract tests and keep new surfaces out of the default path unless the lifecycle budget is explicitly revised."
                : "Restore the listed registry, documentation, resource-pack, host-contract, lifecycle, or read-path budget evidence before claiming governance surface coverage.",
            NonClaims =
            [
                "This surface is read-only and does not initialize, plan, approve, execute, write back, stage, commit, push, tag, release, or retarget anything.",
                "This surface does not replace canonical task truth, execution truth, worker audit truth, or governed card/task ownership.",
                "This surface does not prove behavior correctness; it only audits coverage wiring for a bounded set of governance-critical surfaces.",
                "This surface lifecycle budget governs the bounded governance-critical set and default read path, not every historical inspect/api command in the registry.",
                "This surface does not grant planning authority, continuation authority, review approval, or external target product proof.",
            ],
        };
    }

    private RuntimeGovernanceSurfaceCoverageEntry BuildEntry(
        GovernanceSurfaceCoverageRequirement requirement,
        IReadOnlySet<string> registered,
        string inspectUsage,
        string apiUsage,
        string quickstart,
        string consumerPack,
        IReadOnlyList<RuntimeExternalConsumerCommandEntrySurface> commandEntries)
    {
        var gaps = new List<string>();
        var advisoryGaps = new List<string>();
        var evidence = new List<string>();

        var registryRegistered = registered.Contains(requirement.SurfaceId);
        AddRequiredGap(gaps, evidence, registryRegistered, requirement.SurfaceId, "registry_registered", $"registry_missing:{requirement.SurfaceId}");

        var inspectUsageExposed = ContainsAny(inspectUsage, [requirement.SurfaceId]);
        AddRequiredGap(gaps, evidence, inspectUsageExposed, requirement.SurfaceId, "inspect_usage_exposed", $"inspect_usage_missing:{requirement.SurfaceId}");

        var apiUsageExposed = ContainsAny(apiUsage, [requirement.SurfaceId]);
        AddRequiredGap(gaps, evidence, apiUsageExposed, requirement.SurfaceId, "api_usage_exposed", $"api_usage_missing:{requirement.SurfaceId}");

        var resourcePackCovered = commandEntries.Any(entry =>
            string.Equals(entry.SurfaceId, requirement.SurfaceId, StringComparison.Ordinal)
            || ContainsAny(entry.Command, requirement.ResourcePackNeedles)
            || ContainsAny(entry.ConsumerUse, requirement.ResourcePackNeedles));
        AddConditionalGap(
            requirement.ResourcePackRequired,
            gaps,
            evidence,
            resourcePackCovered,
            requirement.SurfaceId,
            "external_consumer_resource_pack_command",
            $"resource_pack_command_missing:{requirement.SurfaceId}");

        var quickstartDocumented = ContainsAny(quickstart, requirement.QuickstartNeedles);
        AddConditionalGap(
            requirement.QuickstartRequired,
            gaps,
            evidence,
            quickstartDocumented,
            requirement.SurfaceId,
            "external_agent_quickstart_documented",
            $"quickstart_documentation_missing:{requirement.SurfaceId}");

        var consumerPackDocumented = ContainsAny(consumerPack, requirement.ConsumerPackNeedles);
        AddConditionalGap(
            requirement.ConsumerPackRequired,
            gaps,
            evidence,
            consumerPackDocumented,
            requirement.SurfaceId,
            "external_consumer_resource_pack_documented",
            $"consumer_pack_documentation_missing:{requirement.SurfaceId}");

        var hostContract = FindHostContractEvidence(requirement);
        AddRequiredGap(gaps, evidence, hostContract.Covered, requirement.SurfaceId, "host_contract_covered", $"host_contract_missing:{requirement.SurfaceId}");

        if (hostContract.Covered)
        {
            evidence.Add($"host_contract_path:{hostContract.Path}");
        }

        return new RuntimeGovernanceSurfaceCoverageEntry
        {
            SurfaceId = requirement.SurfaceId,
            Role = requirement.Role,
            LifecycleClass = requirement.LifecycleClass,
            ReadPathClass = requirement.ReadPathClass,
            DefaultPathParticipation = requirement.DefaultPathParticipation,
            CountsTowardDefaultPathBudget = string.Equals(requirement.ReadPathClass, "default_path", StringComparison.Ordinal),
            LifecycleBudgetStatus = ResolveLifecycleBudgetStatus(requirement),
            ExternalAgentVisible = requirement.ExternalAgentVisible,
            RegistryRequired = true,
            RegistryRegistered = registryRegistered,
            InspectUsageExposed = inspectUsageExposed,
            ApiUsageExposed = apiUsageExposed,
            ResourcePackRequired = requirement.ResourcePackRequired,
            ResourcePackCovered = resourcePackCovered,
            ResourcePackNeedle = requirement.ResourcePackNeedles.FirstOrDefault() ?? "N/A",
            QuickstartRequired = requirement.QuickstartRequired,
            QuickstartDocumented = quickstartDocumented,
            QuickstartNeedle = requirement.QuickstartNeedles.FirstOrDefault() ?? "N/A",
            ConsumerPackRequired = requirement.ConsumerPackRequired,
            ConsumerPackDocumented = consumerPackDocumented,
            ConsumerPackNeedle = requirement.ConsumerPackNeedles.FirstOrDefault() ?? "N/A",
            HostContractRequired = true,
            HostContractCovered = hostContract.Covered,
            HostContractEvidencePath = hostContract.Path,
            CoverageStatus = gaps.Count == 0 ? "covered" : "blocking_gap",
            Gaps = gaps,
            AdvisoryGaps = advisoryGaps,
            Evidence = evidence,
        };
    }

    private static IEnumerable<string> BuildLifecycleBudgetGaps(IReadOnlyList<RuntimeGovernanceSurfaceCoverageEntry> entries)
    {
        if (entries.Count > MaxGovernanceCriticalSurfaceCount)
        {
            yield return $"surface_budget_exceeded:governance_critical:{entries.Count}>{MaxGovernanceCriticalSurfaceCount}";
        }

        var defaultPathCount = entries.Count(static entry => entry.CountsTowardDefaultPathBudget);
        if (defaultPathCount > MaxDefaultPathSurfaceCount)
        {
            yield return $"surface_budget_exceeded:default_path:{defaultPathCount}>{MaxDefaultPathSurfaceCount}";
        }

        var auditHandoffCount = entries.Count(static entry => string.Equals(entry.ReadPathClass, "audit_handoff", StringComparison.Ordinal));
        if (auditHandoffCount > MaxAuditHandoffSurfaceCount)
        {
            yield return $"surface_budget_exceeded:audit_handoff:{auditHandoffCount}>{MaxAuditHandoffSurfaceCount}";
        }

        foreach (var entry in entries)
        {
            if (string.Equals(entry.LifecycleBudgetStatus, "missing_lifecycle_or_read_path_class", StringComparison.Ordinal))
            {
                yield return $"surface_lifecycle_missing:{entry.SurfaceId}";
            }

            if (entry.CountsTowardDefaultPathBudget && !entry.ExternalAgentVisible)
            {
                yield return $"default_path_surface_not_external_visible:{entry.SurfaceId}";
            }
        }
    }

    private static string ResolveLifecycleBudgetStatus(GovernanceSurfaceCoverageRequirement requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement.LifecycleClass)
            || string.IsNullOrWhiteSpace(requirement.ReadPathClass)
            || string.IsNullOrWhiteSpace(requirement.DefaultPathParticipation))
        {
            return "missing_lifecycle_or_read_path_class";
        }

        return "within_budget";
    }

    private static void AddRequiredGap(
        List<string> gaps,
        List<string> evidence,
        bool passed,
        string surfaceId,
        string evidenceId,
        string gap)
    {
        if (passed)
        {
            evidence.Add($"{evidenceId}:{surfaceId}");
            return;
        }

        gaps.Add(gap);
    }

    private static void AddConditionalGap(
        bool required,
        List<string> gaps,
        List<string> evidence,
        bool passed,
        string surfaceId,
        string evidenceId,
        string gap)
    {
        if (!required)
        {
            evidence.Add($"{evidenceId}:not_required:{surfaceId}");
            return;
        }

        AddRequiredGap(gaps, evidence, passed, surfaceId, evidenceId, gap);
    }

    private HostContractEvidence FindHostContractEvidence(GovernanceSurfaceCoverageRequirement requirement)
    {
        foreach (var path in requirement.HostContractEvidencePaths)
        {
            var text = ReadRepoFile(path);
            if (ContainsAny(text, [requirement.SurfaceId]))
            {
                return new HostContractEvidence(true, path);
            }
        }

        var root = ResolveRepoPath(IntegrationTestRoot);
        if (!Directory.Exists(root))
        {
            return new HostContractEvidence(false, "N/A");
        }

        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.TopDirectoryOnly).OrderBy(static path => path, StringComparer.Ordinal))
        {
            var text = File.ReadAllText(path);
            if (ContainsAny(text, [requirement.SurfaceId]))
            {
                return new HostContractEvidence(true, NormalizePath(Path.GetRelativePath(repoRoot, path)));
            }
        }

        return new HostContractEvidence(false, "N/A");
    }

    private IEnumerable<string> BuildEvidenceSourcePaths()
    {
        yield return QuickstartPath;
        yield return ConsumerResourcePackPath;
        foreach (var path in Requirements.SelectMany(static requirement => requirement.HostContractEvidencePaths).Distinct(StringComparer.Ordinal))
        {
            yield return path;
        }
    }

    private string ReadRepoFile(string repoRelativePath)
    {
        var path = ResolveRepoPath(repoRelativePath);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        return File.ReadAllText(path);
    }

    private string ResolveRepoPath(string repoRelativePath)
    {
        return Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool ContainsAny(string text, IEnumerable<string> needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private sealed record GovernanceSurfaceCoverageRequirement(
        string SurfaceId,
        string Role,
        bool ExternalAgentVisible,
        bool ResourcePackRequired,
        IReadOnlyList<string> ResourcePackNeedles,
        bool QuickstartRequired,
        IReadOnlyList<string> QuickstartNeedles,
        bool ConsumerPackRequired,
        IReadOnlyList<string> ConsumerPackNeedles,
        string LifecycleClass,
        string ReadPathClass,
        string DefaultPathParticipation,
        IReadOnlyList<string> HostContractEvidencePaths);

    private sealed record HostContractEvidence(bool Covered, string Path);
}
