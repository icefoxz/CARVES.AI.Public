using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeCliInvocationContractService
{
    public const string PhaseDocumentPath = "docs/runtime/carves-product-closure-phase-19-cli-invocation-contract.md";
    public const string PreviousPhaseDocumentPath = RuntimeExternalConsumerResourcePackService.PhaseDocumentPath;
    public const string InvocationGuideDocumentPath = "docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md";
    public const string CliDistributionGuideDocumentPath = "docs/guides/CARVES_CLI_DISTRIBUTION.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeCliInvocationContractService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeCliInvocationContractSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 19 CLI invocation contract document", errors);
        ValidateRuntimeDocument(PreviousPhaseDocumentPath, "Product closure Phase 18 external consumer resource pack document", errors);
        ValidateRuntimeDocument(InvocationGuideDocumentPath, "CLI invocation contract guide document", errors);
        ValidateRuntimeDocument(CliDistributionGuideDocumentPath, "CLI distribution guide document", errors);
        ValidateRuntimeDocument(RuntimeExternalConsumerResourcePackService.ResourcePackGuideDocumentPath, "External consumer resource pack guide document", errors);
        ValidateRuntimeDocument("docs/runtime/runtime-governed-agent-handoff-proof.md", "Runtime governed agent handoff proof document", errors);

        var runtimeRoot = documentRoot.DocumentRoot;
        var hasPowerShellWrapper = File.Exists(Path.Combine(runtimeRoot, "carves.ps1"));
        var hasCmdWrapper = File.Exists(Path.Combine(runtimeRoot, "carves.cmd"));
        var hasPreferredWrapper = RuntimeCliWrapperPaths.HasPreferredWrapper(runtimeRoot);
        var hasDistManifest = File.Exists(Path.Combine(runtimeRoot, "MANIFEST.json"))
                              && File.Exists(Path.Combine(runtimeRoot, "VERSION"));
        var hasSolution = File.Exists(Path.Combine(runtimeRoot, "CARVES.Runtime.sln"));

        if (!hasPreferredWrapper)
        {
            errors.Add($"Runtime CLI wrapper '{RuntimeCliWrapperPaths.PreferredWrapperFileName}' is missing.");
        }

        var lanes = BuildInvocationLanes(runtimeRoot);
        var invocationComplete = errors.Count == 0 && hasPreferredWrapper;
        var rootKind = ResolveRuntimeRootKind(runtimeRoot, hasDistManifest, hasSolution);
        var recommendedMode = ResolveRecommendedInvocationMode(hasDistManifest, hasSolution);

        return new RuntimeCliInvocationContractSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            PreviousPhaseDocumentPath = PreviousPhaseDocumentPath,
            InvocationGuideDocumentPath = InvocationGuideDocumentPath,
            CliDistributionGuideDocumentPath = CliDistributionGuideDocumentPath,
            RuntimeDocumentRoot = runtimeRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = invocationComplete
                ? "cli_invocation_contract_ready"
                : "cli_invocation_contract_blocked_by_missing_runtime_entry",
            InvocationContractComplete = invocationComplete,
            RecommendedInvocationMode = recommendedMode,
            RuntimeRootKind = rootKind,
            RuntimeRootHasPowerShellWrapper = hasPowerShellWrapper,
            RuntimeRootHasCmdWrapper = hasCmdWrapper,
            RuntimeRootHasDistManifest = hasDistManifest,
            RuntimeRootHasSolution = hasSolution,
            InvocationLaneCount = lanes.Length,
            InvocationLanes = lanes,
            RequiredReadbackCommands =
            [
                "carves pilot invocation --json",
                "carves pilot resources --json",
                "carves agent handoff --json",
                "carves pilot status --json",
                "carves pilot guide --json",
            ],
            BoundaryRules =
            [
                "External agents should invoke the wrapper recorded by Runtime or target bootstrap before assuming a global alias exists.",
                "A frozen local dist wrapper is the stable alpha external-project baseline; source-tree wrappers are development dogfood.",
                "A future global `carves` alias is allowed only when it resolves to the same Runtime document root and does not change authority.",
                "Invocation mode does not grant mutation authority; planning, workspace, review, writeback, and commit closure remain Runtime-governed.",
                "Do not teach agents `dotnet run --project ...` as the external baseline unless the wrapper itself falls back internally.",
            ],
            Gaps = BuildGaps(errors, hasPreferredWrapper).ToArray(),
            Summary = invocationComplete
                ? "CLI invocation contract is ready: agents can distinguish source-tree, local-dist, and future global alias invocation without changing Runtime authority."
                : "CLI invocation contract is blocked until the Runtime wrapper and invocation guide are restored.",
            RecommendedNextAction = invocationComplete
                ? "From an external target repo, prefer the runtime-root wrapper recorded by bootstrap or a frozen local dist; run carves pilot invocation --json before relying on a global alias."
                : "Restore the missing Runtime invocation resources, then rerun carves pilot invocation --json.",
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not install a global alias, publish a package, or rewrite PATH.",
                "This surface does not initialize, plan, review, write back, stage, commit, push, tag, release, pack, repair, or retarget anything.",
                "This surface does not make `dotnet run --project` the external agent baseline.",
                "This surface does not claim signed packages, automatic updates, OS sandboxing, full ACP, full MCP, or remote worker orchestration.",
            ],
        };
    }

    private static RuntimeCliInvocationLaneSurface[] BuildInvocationLanes(string runtimeRoot)
    {
        var wrapper = RuntimeCliWrapperPaths.PreferredWrapperPath(runtimeRoot);
        var cmdWrapper = RuntimeCliWrapperPaths.CmdWrapperPath(runtimeRoot);
        return
        [
            BuildLane(
                "source_tree_wrapper",
                "source_tree",
                RuntimeCliWrapperPaths.FormatCommandPattern(wrapper),
                "development_dogfood",
                "Runtime document root is the live source checkout.",
                "Allowed for Runtime development and controlled dogfood; not the stable external-project baseline.",
                "Use when editing or validating CARVES.Runtime itself."),
            BuildLane(
                "local_dist_wrapper",
                "local_dist",
                RuntimeCliWrapperPaths.FormatCommandPattern(wrapper),
                "stable_alpha_external_baseline",
                "Runtime document root is a frozen local dist with MANIFEST.json, VERSION, and wrapper files.",
                "Preferred external-project baseline; target repos should read Runtime docs from this root.",
                "Use for attached target repos that need stable alpha consumption."),
            BuildLane(
                "cmd_wrapper",
                "windows_cmd",
                $"\"{cmdWrapper}\" <command> [args]",
                "shell_compatibility",
                "Windows shells or tooling prefer a .cmd shim.",
                "Compatibility lane only; it must route to the same Runtime authority as the preferred wrapper.",
                "Use only when PowerShell wrapper invocation is inconvenient."),
            BuildLane(
                "global_alias",
                "future_global_alias",
                "carves <command> [args]",
                "optional_operator_configured_alias",
                "The operator has installed or aliased a CARVES command that resolves to the intended Runtime root.",
                "Convenience only; it cannot replace runtime-root verification or change authority.",
                "Use after `pilot invocation --json` confirms the intended invocation contract."),
        ];
    }

    private static IEnumerable<string> BuildGaps(IReadOnlyList<string> errors, bool hasPreferredWrapper)
    {
        foreach (var error in errors)
        {
            yield return $"missing_or_invalid_invocation_resource:{error}";
        }

        if (!hasPreferredWrapper)
        {
            yield return $"runtime_{RuntimeCliWrapperPaths.PreferredWrapperFileName}_wrapper_missing";
        }
    }

    private static string ResolveRuntimeRootKind(string runtimeRoot, bool hasDistManifest, bool hasSolution)
    {
        if (hasDistManifest)
        {
            return "local_dist";
        }

        if (hasSolution || Directory.Exists(Path.Combine(runtimeRoot, ".git")))
        {
            return "source_tree";
        }

        return "runtime_document_root";
    }

    private static string ResolveRecommendedInvocationMode(bool hasDistManifest, bool hasSolution)
    {
        if (hasDistManifest)
        {
            return "local_dist_wrapper";
        }

        return hasSolution
            ? "source_tree_wrapper"
            : "recorded_runtime_wrapper";
    }

    private static RuntimeCliInvocationLaneSurface BuildLane(
        string laneId,
        string invocationMode,
        string commandPattern,
        string stabilityPosture,
        string appliesWhen,
        string boundary,
        string recommendedUse)
    {
        return new RuntimeCliInvocationLaneSurface
        {
            LaneId = laneId,
            InvocationMode = invocationMode,
            CommandPattern = commandPattern,
            StabilityPosture = stabilityPosture,
            AppliesWhen = appliesWhen,
            Boundary = boundary,
            RecommendedUse = recommendedUse,
        };
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
