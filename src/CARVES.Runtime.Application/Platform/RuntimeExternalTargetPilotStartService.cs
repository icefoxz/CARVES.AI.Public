using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeExternalTargetPilotStartService
{
    public const string QuickstartGuideDocumentPath = "docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<RuntimeAlphaExternalUseReadinessSurface> alphaReadinessFactory;
    private readonly Func<RuntimeCliInvocationContractSurface> cliInvocationFactory;
    private readonly Func<RuntimeExternalConsumerResourcePackSurface> resourcePackFactory;
    private readonly Func<RuntimeGovernedAgentHandoffProofSurface> handoffProofFactory;
    private readonly Func<RuntimeProductClosurePilotStatusSurface> pilotStatusFactory;

    public RuntimeExternalTargetPilotStartService(
        string repoRoot,
        Func<RuntimeAlphaExternalUseReadinessSurface> alphaReadinessFactory,
        Func<RuntimeCliInvocationContractSurface> cliInvocationFactory,
        Func<RuntimeExternalConsumerResourcePackSurface> resourcePackFactory,
        Func<RuntimeGovernedAgentHandoffProofSurface> handoffProofFactory,
        Func<RuntimeProductClosurePilotStatusSurface> pilotStatusFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.alphaReadinessFactory = alphaReadinessFactory;
        this.cliInvocationFactory = cliInvocationFactory;
        this.resourcePackFactory = resourcePackFactory;
        this.handoffProofFactory = handoffProofFactory;
        this.pilotStatusFactory = pilotStatusFactory;
    }

    public RuntimeExternalTargetPilotStartSurface BuildStart()
    {
        var errors = ValidateBaseDocuments();
        var alphaReadiness = alphaReadinessFactory();
        var cliInvocation = cliInvocationFactory();
        var resourcePack = resourcePackFactory();
        var handoffProof = handoffProofFactory();
        var pilotStatus = pilotStatusFactory();
        var dependencyErrors = BuildDependencyErrors(alphaReadiness, cliInvocation, resourcePack, handoffProof, pilotStatus).ToArray();
        var targetReadyForFormalPlanning = pilotStatus.RuntimeInitialized
                                           && pilotStatus.TargetAgentBootstrapMissingFiles.Count == 0
                                           && pilotStatus.CurrentStageOrder >= 8;
        var ready = errors.Count == 0
                    && alphaReadiness.IsValid
                    && alphaReadiness.AlphaExternalUseReady
                    && cliInvocation.IsValid
                    && cliInvocation.InvocationContractComplete
                    && resourcePack.IsValid
                    && resourcePack.ResourcePackComplete
                    && handoffProof.IsValid
                    && string.Equals(handoffProof.OverallPosture, "bounded_governed_agent_handoff_proof_ready", StringComparison.Ordinal)
                    && pilotStatus.IsValid;
        var discussionFirstSurface = IsDiscussionFirst(pilotStatus);
        var gaps = BuildStartGaps(
                errors,
                dependencyErrors,
                alphaReadiness,
                cliInvocation,
                resourcePack,
                handoffProof,
                pilotStatus)
            .ToArray();

        return new RuntimeExternalTargetPilotStartSurface
        {
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ready
                ? "external_target_pilot_start_bundle_ready"
                : "external_target_pilot_start_bundle_blocked",
            PilotStartBundleReady = ready,
            AlphaExternalUseReady = alphaReadiness.AlphaExternalUseReady,
            InvocationContractComplete = cliInvocation.InvocationContractComplete,
            ExternalConsumerResourcePackComplete = resourcePack.ResourcePackComplete,
            GovernedAgentHandoffReady = string.Equals(handoffProof.OverallPosture, "bounded_governed_agent_handoff_proof_ready", StringComparison.Ordinal),
            ProductPilotStatusValid = pilotStatus.IsValid,
            RuntimeInitialized = pilotStatus.RuntimeInitialized,
            TargetAgentBootstrapReady = pilotStatus.RuntimeInitialized && pilotStatus.TargetAgentBootstrapMissingFiles.Count == 0,
            TargetReadyForFormalPlanning = targetReadyForFormalPlanning,
            RecommendedInvocationMode = cliInvocation.RecommendedInvocationMode,
            RuntimeRootKind = cliInvocation.RuntimeRootKind,
            CandidateDistRoot = alphaReadiness.CandidateDistRoot,
            CurrentStageId = pilotStatus.CurrentStageId,
            CurrentStageOrder = pilotStatus.CurrentStageOrder,
            CurrentStageStatus = pilotStatus.CurrentStageStatus,
            NextGovernedCommand = pilotStatus.NextCommand,
            DiscussionFirstSurface = discussionFirstSurface,
            AutoRunAllowed = false,
            RecommendedActionId = null,
            AvailableActions = discussionFirstSurface
                ? RuntimeDiscussionFirstSurfacePolicy.BuildSafeMenu()
                : [],
            ForbiddenAutoActions = discussionFirstSurface
                ? RuntimeDiscussionFirstSurfacePolicy.BuildForbiddenAutoActions()
                : [],
            PilotStatusPosture = pilotStatus.OverallPosture,
            StartReadbackCommands = BuildStartReadbacks(alphaReadiness.CandidateDistRoot),
            AgentOperatingRules = BuildAgentOperatingRules(),
            StopAndReportTriggers = BuildStopAndReportTriggers(),
            Gaps = gaps,
            Summary = BuildPilotSummary(ready, pilotStatus),
            RecommendedNextAction = BuildPilotRecommendedNextAction(ready, pilotStatus),
            IsValid = errors.Count == 0 && dependencyErrors.Length == 0,
            Errors = [.. errors, .. dependencyErrors],
            NonClaims = BuildNonClaims(),
        };
    }

    public RuntimeExternalTargetPilotNextSurface BuildNext()
    {
        var errors = ValidateBaseDocuments();
        var alphaReadiness = alphaReadinessFactory();
        var pilotStatus = pilotStatusFactory();
        var dependencyErrors = alphaReadiness.Errors
            .Select(static error => $"runtime-alpha-external-use-readiness:{error}")
            .Concat(pilotStatus.Errors.Select(static error => $"runtime-product-closure-pilot-status:{error}"))
            .ToArray();
        var ready = errors.Count == 0
                    && dependencyErrors.Length == 0
                    && alphaReadiness.IsValid
                    && alphaReadiness.AlphaExternalUseReady
                    && pilotStatus.IsValid
                    && !string.IsNullOrWhiteSpace(pilotStatus.NextCommand);
        var discussionFirstSurface = IsDiscussionFirst(pilotStatus);
        var gaps = BuildNextGaps(errors, dependencyErrors, alphaReadiness, pilotStatus).ToArray();

        return new RuntimeExternalTargetPilotNextSurface
        {
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ready
                ? "external_target_pilot_next_ready"
                : "external_target_pilot_next_blocked",
            ReadyToRunNextCommand = ready,
            AlphaExternalUseReady = alphaReadiness.AlphaExternalUseReady,
            RuntimeInitialized = pilotStatus.RuntimeInitialized,
            CurrentStageId = pilotStatus.CurrentStageId,
            CurrentStageOrder = pilotStatus.CurrentStageOrder,
            CurrentStageStatus = pilotStatus.CurrentStageStatus,
            NextGovernedCommand = pilotStatus.NextCommand,
            DiscussionFirstSurface = discussionFirstSurface,
            AutoRunAllowed = false,
            RecommendedActionId = null,
            AvailableActions = discussionFirstSurface
                ? RuntimeDiscussionFirstSurfacePolicy.BuildSafeMenu()
                : [],
            ForbiddenAutoActions = discussionFirstSurface
                ? RuntimeDiscussionFirstSurfacePolicy.BuildForbiddenAutoActions()
                : [],
            PilotStatusPosture = pilotStatus.OverallPosture,
            StopAndReportTriggers = BuildStopAndReportTriggers(),
            Gaps = gaps,
            Summary = ready
                ? BuildPilotNextSummary(pilotStatus)
                : "The next governed command cannot be trusted until start-bundle dependencies are valid.",
            RecommendedNextAction = ready
                ? BuildPilotRecommendedNextAction(true, pilotStatus)
                : "Run carves pilot start --json and resolve the listed gaps before planning or editing.",
            IsValid = errors.Count == 0 && dependencyErrors.Length == 0,
            Errors = [.. errors, .. dependencyErrors],
            NonClaims = BuildNonClaims(),
        };
    }

    private List<string> ValidateBaseDocuments()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.CurrentDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.PreviousDocumentPath, "Product closure previous phase document", errors);
        ValidateRuntimeDocument(QuickstartGuideDocumentPath, "External agent quickstart guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotGuideService.GuideDocumentPath, "Productized pilot guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotStatusService.GuideDocumentPath, "Productized pilot status document", errors);
        return errors;
    }

    private static IEnumerable<string> BuildDependencyErrors(
        RuntimeAlphaExternalUseReadinessSurface alphaReadiness,
        RuntimeCliInvocationContractSurface cliInvocation,
        RuntimeExternalConsumerResourcePackSurface resourcePack,
        RuntimeGovernedAgentHandoffProofSurface handoffProof,
        RuntimeProductClosurePilotStatusSurface pilotStatus)
    {
        foreach (var error in alphaReadiness.Errors)
        {
            yield return $"runtime-alpha-external-use-readiness:{error}";
        }

        foreach (var error in cliInvocation.Errors)
        {
            yield return $"runtime-cli-invocation-contract:{error}";
        }

        foreach (var error in resourcePack.Errors)
        {
            yield return $"runtime-external-consumer-resource-pack:{error}";
        }

        foreach (var error in handoffProof.Errors)
        {
            yield return $"runtime-governed-agent-handoff-proof:{error}";
        }

        foreach (var error in pilotStatus.Errors)
        {
            yield return $"runtime-product-closure-pilot-status:{error}";
        }
    }

    private static IEnumerable<string> BuildStartGaps(
        IReadOnlyList<string> errors,
        IReadOnlyList<string> dependencyErrors,
        RuntimeAlphaExternalUseReadinessSurface alphaReadiness,
        RuntimeCliInvocationContractSurface cliInvocation,
        RuntimeExternalConsumerResourcePackSurface resourcePack,
        RuntimeGovernedAgentHandoffProofSurface handoffProof,
        RuntimeProductClosurePilotStatusSurface pilotStatus)
    {
        foreach (var error in errors)
        {
            yield return $"external_target_pilot_start_resource:{error}";
        }

        foreach (var error in dependencyErrors)
        {
            yield return $"external_target_pilot_start_dependency:{error}";
        }

        if (!alphaReadiness.AlphaExternalUseReady)
        {
            yield return "alpha_external_use_not_ready";
        }

        if (!cliInvocation.InvocationContractComplete)
        {
            yield return "cli_invocation_contract_not_complete";
        }

        if (!resourcePack.ResourcePackComplete)
        {
            yield return "external_consumer_resource_pack_not_complete";
        }

        if (!string.Equals(handoffProof.OverallPosture, "bounded_governed_agent_handoff_proof_ready", StringComparison.Ordinal))
        {
            yield return "governed_agent_handoff_not_ready";
        }

        if (!alphaReadiness.AlphaExternalUseReady)
        {
            foreach (var gap in alphaReadiness.Gaps)
            {
                yield return $"runtime-alpha-external-use-readiness:{gap}";
            }
        }

        foreach (var gap in cliInvocation.Gaps)
        {
            yield return $"runtime-cli-invocation-contract:{gap}";
        }

        foreach (var gap in resourcePack.Gaps)
        {
            yield return $"runtime-external-consumer-resource-pack:{gap}";
        }

        foreach (var gap in pilotStatus.Gaps)
        {
            yield return $"runtime-product-closure-pilot-status:{gap}";
        }
    }

    private static IEnumerable<string> BuildNextGaps(
        IReadOnlyList<string> errors,
        IReadOnlyList<string> dependencyErrors,
        RuntimeAlphaExternalUseReadinessSurface alphaReadiness,
        RuntimeProductClosurePilotStatusSurface pilotStatus)
    {
        foreach (var error in errors)
        {
            yield return $"external_target_pilot_next_resource:{error}";
        }

        foreach (var error in dependencyErrors)
        {
            yield return $"external_target_pilot_next_dependency:{error}";
        }

        if (!alphaReadiness.AlphaExternalUseReady)
        {
            yield return "alpha_external_use_not_ready";
        }

        if (string.IsNullOrWhiteSpace(pilotStatus.NextCommand))
        {
            yield return "pilot_status_next_command_missing";
        }

        if (!alphaReadiness.AlphaExternalUseReady)
        {
            foreach (var gap in alphaReadiness.Gaps)
            {
                yield return $"runtime-alpha-external-use-readiness:{gap}";
            }
        }

        foreach (var gap in pilotStatus.Gaps)
        {
            yield return $"runtime-product-closure-pilot-status:{gap}";
        }
    }

    private static string[] BuildStartReadbacks(string candidateDistRoot)
    {
        return
        [
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "start", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "problem-intake", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "triage", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "follow-up", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "follow-up-plan", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "follow-up-record", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "follow-up-intake", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "follow-up-gate", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "readiness", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "invocation", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "resources", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "agent", "handoff", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "next", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "status", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "guide", "--json"),
        ];
    }

    private static string FormatCandidateDistCommand(string candidateDistRoot, params string[] arguments)
    {
        if (string.IsNullOrWhiteSpace(candidateDistRoot))
        {
            return $"carves {string.Join(' ', arguments)}";
        }

        return RuntimeCliWrapperPaths.FormatShellCommand(RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot), arguments);
    }

    private static string[] BuildAgentOperatingRules()
    {
        return
        [
            "Run pilot start before planning or editing in an external target repo.",
            "Run pilot next before each governed step, but treat `next_governed_command` as a legacy projection hint; prefer `available_actions` when present and do not auto-run stateful work from that field alone.",
            "When a stop-and-report trigger fires, use pilot problem-intake to read the schema and pilot report-problem to submit bounded evidence instead of mutating protected truth; use pilot triage to show the operator the recorded friction queue, pilot follow-up to show operator-review candidates, pilot follow-up-plan to show decision choices, pilot follow-up-record to prove durable operator choices, pilot follow-up-intake to project accepted planning inputs, and pilot follow-up-gate before converting accepted decisions into formal planning.",
            "Runtime-owned docs stay in the Runtime document root; do not copy them into target product truth.",
            "Do not edit protected roots such as .ai/tasks/, .ai/memory/, .ai/artifacts/reviews/, or .carves-platform/ directly.",
            "Do not stage, commit, retarget Runtime manifests, or edit .gitignore unless the corresponding pilot surface says the operator-reviewed step is ready.",
            "When the next command is ambiguous or blocked, stop and preserve command output instead of rationalizing a bypass.",
        ];
    }

    private static string[] BuildStopAndReportTriggers()
    {
        return
        [
            "next_governed_command is empty or conflicts with the user's requested scope",
            "a command fails or returns blocked posture",
            "the agent wants to modify a protected truth root directly",
            "the agent cannot find an acceptance contract for executable work",
            "the agent is about to edit files outside a managed workspace lease or declared writable path",
            "the agent is tempted to explain away a CARVES warning instead of following the surfaced next action",
        ];
    }

    private static string[] BuildNonClaims()
    {
        return
        [
            "This surface is read-only and does not initialize, plan, issue workspaces, approve review, write back files, stage, commit, or retarget a repo.",
            "This surface does not replace per-target pilot status, target proof, product proof, or operator review.",
            "This surface does not replace pilot problem-intake or report-problem; problem reports remain separate target runtime evidence.",
            "This surface does not claim OS sandboxing, full ACP, full MCP, remote worker orchestration, package distribution, or automatic updates.",
        ];
    }

    private static string BuildPilotSummary(bool ready, RuntimeProductClosurePilotStatusSurface pilotStatus)
    {
        if (!ready)
        {
            return "External target pilot start bundle is blocked until the listed Runtime-owned entry surfaces and quickstart resources are restored.";
        }

        if (IsDiscussionFirst(pilotStatus))
        {
            return $"External target pilot start bundle is ready. Current stage is {pilotStatus.CurrentStageId}; next command is `{pilotStatus.NextCommand}` so the agent stays in normal discussion until the operator/user gives a bounded scope.";
        }

        return $"External target pilot start bundle is ready. Current stage is {pilotStatus.CurrentStageId}; `{pilotStatus.NextCommand}` remains a legacy projection hint, while `available_actions` is the preferred surface when present.";
    }

    private static string BuildPilotNextSummary(RuntimeProductClosurePilotStatusSurface pilotStatus)
    {
        return IsDiscussionFirst(pilotStatus)
            ? $"Next command is `{pilotStatus.NextCommand}` for stage `{pilotStatus.CurrentStageId}`. Stay in discussion and clarify scope before intent capture."
            : $"Next governed command is `{pilotStatus.NextCommand}` for stage `{pilotStatus.CurrentStageId}`, but it remains a legacy projection hint; prefer `available_actions` when present.";
    }

    private static string BuildPilotRecommendedNextAction(bool ready, RuntimeProductClosurePilotStatusSurface pilotStatus)
    {
        if (!ready)
        {
            return "Resolve the listed start-bundle gaps, then rerun carves pilot start --json.";
        }

        if (IsDiscussionFirst(pilotStatus))
        {
            return $"Run `{pilotStatus.NextCommand}` first. Use ordinary discussion to ask what the project is for, what outcome is wanted, and whether this should become real engineering work; only after a bounded scope is explicit should you run `carves intent draft --persist`.";
        }

        return $"Use `{pilotStatus.NextCommand}` only as the current projection hint. Prefer `available_actions` when present; if the projected step fails, asks for protected-root edits, or leaves the stage ambiguous, stop and report instead of improvising.";
    }

    private static bool IsDiscussionFirst(RuntimeProductClosurePilotStatusSurface pilotStatus)
    {
        return RuntimeDiscussionFirstSurfacePolicy.IsDiscussionFirstStage(pilotStatus.CurrentStageId)
               || RuntimeDiscussionFirstSurfacePolicy.IsDiscussionFirstCommand(pilotStatus.NextCommand);
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
