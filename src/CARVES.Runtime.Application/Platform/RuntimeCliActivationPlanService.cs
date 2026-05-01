using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeCliActivationPlanService
{
    public const string PhaseDocumentPath = "docs/runtime/carves-product-closure-phase-20-cli-activation-plan.md";
    public const string PreviousPhaseDocumentPath = RuntimeCliInvocationContractService.PhaseDocumentPath;
    public const string ActivationGuideDocumentPath = "docs/guides/CARVES_CLI_ACTIVATION_PLAN.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeCliActivationPlanService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeCliActivationPlanSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 20 CLI activation plan document", errors);
        ValidateRuntimeDocument(PreviousPhaseDocumentPath, "Product closure Phase 19 CLI invocation contract document", errors);
        ValidateRuntimeDocument(ActivationGuideDocumentPath, "CLI activation plan guide document", errors);
        ValidateRuntimeDocument(RuntimeCliInvocationContractService.InvocationGuideDocumentPath, "CLI invocation contract guide document", errors);
        ValidateRuntimeDocument(RuntimeCliInvocationContractService.CliDistributionGuideDocumentPath, "CLI distribution guide document", errors);

        var runtimeRoot = documentRoot.DocumentRoot;
        var hasPowerShellWrapper = File.Exists(Path.Combine(runtimeRoot, "carves.ps1"));
        var hasCmdWrapper = File.Exists(Path.Combine(runtimeRoot, "carves.cmd"));
        var hasPreferredWrapper = RuntimeCliWrapperPaths.HasPreferredWrapper(runtimeRoot);
        var hasDistManifest = File.Exists(Path.Combine(runtimeRoot, "MANIFEST.json"))
                              && File.Exists(Path.Combine(runtimeRoot, "VERSION"));
        var pathContainsRuntimeRoot = ProcessPathContains(runtimeRoot);
        var runtimeRootEnvMatches = RuntimeRootEnvironmentMatches(runtimeRoot);

        if (!hasPreferredWrapper)
        {
            errors.Add($"Runtime CLI wrapper '{RuntimeCliWrapperPaths.PreferredWrapperFileName}' is missing.");
        }

        var rootKind = ResolveRuntimeRootKind(runtimeRoot, hasDistManifest);
        var lanes = BuildActivationLanes(runtimeRoot);
        var activationPlanComplete = errors.Count == 0 && hasPreferredWrapper;

        return new RuntimeCliActivationPlanSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            PreviousPhaseDocumentPath = PreviousPhaseDocumentPath,
            ActivationGuideDocumentPath = ActivationGuideDocumentPath,
            InvocationGuideDocumentPath = RuntimeCliInvocationContractService.InvocationGuideDocumentPath,
            RuntimeDocumentRoot = runtimeRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolvePosture(errors.Count, pathContainsRuntimeRoot, runtimeRootEnvMatches),
            ActivationPlanComplete = activationPlanComplete,
            RecommendedActivationLane = ResolveRecommendedActivationLane(rootKind, pathContainsRuntimeRoot, runtimeRootEnvMatches),
            RuntimeRootKind = rootKind,
            RuntimeRootHasPowerShellWrapper = hasPowerShellWrapper,
            RuntimeRootHasCmdWrapper = hasCmdWrapper,
            RuntimeRootHasDistManifest = hasDistManifest,
            RuntimeRootOnProcessPath = pathContainsRuntimeRoot,
            CarvesRuntimeRootEnvironmentMatches = runtimeRootEnvMatches,
            ActivationLaneCount = lanes.Length,
            ActivationLanes = lanes,
            RequiredSmokeCommands =
            [
                "carves pilot activation --json",
                "carves pilot invocation --json",
                "carves pilot resources --json",
                "carves pilot status --json",
            ],
            BoundaryRules =
            [
                "Activation is operator-owned convenience; it cannot change Runtime authority or target write permissions.",
                "A target bootstrap absolute wrapper path remains valid even when no global alias is active.",
                "PATH or profile updates must be performed explicitly by the operator, outside this read-only surface.",
                "External agents should not edit shell profile files or machine PATH as part of governed project work.",
                "If a global `carves` alias is used, rerun pilot invocation and pilot activation to prove the Runtime root.",
            ],
            Gaps = BuildGaps(errors, hasPreferredWrapper).ToArray(),
            Summary = activationPlanComplete
                ? "CLI activation plan is ready: operators can choose absolute wrapper, session alias, PATH entry, cmd shim, or optional tool activation without changing Runtime authority."
                : "CLI activation plan is blocked until the Runtime wrapper and activation guide are restored.",
            RecommendedNextAction = activationPlanComplete
                ? "Use the recorded absolute wrapper or the recommended activation lane, then run carves pilot activation --json and carves pilot invocation --json before planning or editing."
                : "Restore the missing Runtime activation resources, then rerun carves pilot activation --json.",
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not install, modify PATH, edit shell profiles, write environment variables, or create a global alias.",
                "This surface does not initialize, plan, review, write back, stage, commit, push, tag, release, pack, repair, or retarget anything.",
                "This surface does not make vendor-specific IDE settings the portable baseline.",
                "This surface does not claim public package distribution, signed installers, automatic updates, OS sandboxing, full ACP, full MCP, or remote worker orchestration.",
            ],
        };
    }

    private static RuntimeCliActivationLaneSurface[] BuildActivationLanes(string runtimeRoot)
    {
        var wrapper = RuntimeCliWrapperPaths.PreferredWrapperPath(runtimeRoot);
        var cmdWrapper = RuntimeCliWrapperPaths.CmdWrapperPath(runtimeRoot);
        var sessionAliasCommand = OperatingSystem.IsWindows()
            ? $"Set-Alias carves \"{wrapper}\""
            : $"alias carves='{wrapper}'";
        var pathEntryCommand = OperatingSystem.IsWindows()
            ? $"$env:Path = \"{runtimeRoot};$env:Path\""
            : $"export PATH=\"{runtimeRoot}:$PATH\"";
        return
        [
            BuildLane(
                "absolute_wrapper",
                "direct_runtime_root_wrapper",
                RuntimeCliWrapperPaths.FormatShellCommand(wrapper, "pilot", "status", "--json"),
                "none",
                "The target bootstrap or operator has the Runtime root path.",
                "Safest baseline; no shell profile or PATH mutation is required.",
                "Use this when agent reliability matters more than command brevity."),
            BuildLane(
                "session_alias",
                "session_alias",
                sessionAliasCommand,
                "current_shell_only",
                "The operator wants a short command in the current shell session.",
                "Operator convenience only; agents should not edit profiles to persist it.",
                "Use for temporary local work after verifying the Runtime root."),
            BuildLane(
                "path_entry",
                "operator_path_entry",
                pathEntryCommand,
                "current_shell_or_operator_persisted",
                "The operator wants `carves` to resolve through PATH.",
                "PATH mutation is outside CARVES Runtime authority and must be explicit operator action.",
                "Use mainly with a frozen local dist root."),
            BuildLane(
                "cmd_shim",
                "windows_cmd_shim",
                $"\"{cmdWrapper}\" pilot status --json",
                "none",
                "Windows shell tooling prefers a .cmd entry.",
                "Compatibility lane only; it must route to the same Runtime root.",
                "Use for IDE terminals or tools that cannot call PowerShell scripts directly."),
            BuildLane(
                "dotnet_tool",
                "optional_dotnet_tool",
                $"dotnet tool install --global CARVES.Runtime.Cli --add-source <package-root> --version {RuntimeAlphaVersion.Current}",
                "operator_installed_tool",
                "The operator has built a local tool package or uses a future published package.",
                "Optional distribution lane; it cannot replace Runtime-root proof surfaces.",
                "Use only after package source and version are operator-reviewed."),
        ];
    }

    private static IEnumerable<string> BuildGaps(IReadOnlyList<string> errors, bool hasPreferredWrapper)
    {
        foreach (var error in errors)
        {
            yield return $"missing_or_invalid_activation_resource:{error}";
        }

        if (!hasPreferredWrapper)
        {
            yield return $"runtime_{RuntimeCliWrapperPaths.PreferredWrapperFileName}_wrapper_missing";
        }
    }

    private static string ResolvePosture(int errorCount, bool pathContainsRuntimeRoot, bool runtimeRootEnvMatches)
    {
        if (errorCount > 0)
        {
            return "cli_activation_plan_blocked_by_missing_activation_resources";
        }

        if (pathContainsRuntimeRoot || runtimeRootEnvMatches)
        {
            return "cli_activation_plan_ready_with_detected_operator_activation";
        }

        return "cli_activation_plan_ready";
    }

    private static string ResolveRecommendedActivationLane(string rootKind, bool pathContainsRuntimeRoot, bool runtimeRootEnvMatches)
    {
        if (pathContainsRuntimeRoot)
        {
            return "path_entry";
        }

        if (runtimeRootEnvMatches)
        {
            return "session_alias";
        }

        return string.Equals(rootKind, "local_dist", StringComparison.Ordinal)
            ? "path_entry"
            : "absolute_wrapper";
    }

    private static string ResolveRuntimeRootKind(string runtimeRoot, bool hasDistManifest)
    {
        if (hasDistManifest)
        {
            return "local_dist";
        }

        return File.Exists(Path.Combine(runtimeRoot, "CARVES.Runtime.sln"))
               || Directory.Exists(Path.Combine(runtimeRoot, ".git"))
            ? "source_tree"
            : "runtime_document_root";
    }

    private static bool ProcessPathContains(string runtimeRoot)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(entry => PathEquals(entry, runtimeRoot));
    }

    private static bool RuntimeRootEnvironmentMatches(string runtimeRoot)
    {
        var value = Environment.GetEnvironmentVariable("CARVES_RUNTIME_ROOT");
        return !string.IsNullOrWhiteSpace(value) && PathEquals(value, runtimeRoot);
    }

    private static bool PathEquals(string first, string second)
    {
        return string.Equals(
            Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static RuntimeCliActivationLaneSurface BuildLane(
        string laneId,
        string activationMode,
        string commandPreview,
        string persistence,
        string appliesWhen,
        string boundary,
        string recommendedUse)
    {
        return new RuntimeCliActivationLaneSurface
        {
            LaneId = laneId,
            ActivationMode = activationMode,
            CommandPreview = commandPreview,
            Persistence = persistence,
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
