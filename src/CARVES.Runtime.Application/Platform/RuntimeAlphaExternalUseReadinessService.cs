using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAlphaExternalUseReadinessService
{
    public const string PhaseDocumentPath = RuntimeProductClosureMetadata.CurrentDocumentPath;
    public const string PreviousPhaseDocumentPath = RuntimeProductClosureMetadata.PreviousDocumentPath;

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<RuntimeLocalDistFreshnessSmokeSurface> localDistFreshnessSmokeFactory;
    private readonly Func<RuntimeExternalConsumerResourcePackSurface> externalConsumerResourcePackFactory;
    private readonly Func<RuntimeGovernedAgentHandoffProofSurface> governedAgentHandoffProofFactory;
    private readonly Func<RuntimeProductClosurePilotGuideSurface> productizedPilotGuideFactory;
    private readonly Func<RuntimeSessionGatewayPrivateAlphaHandoffSurface> sessionGatewayPrivateAlphaFactory;
    private readonly Func<RuntimeSessionGatewayRepeatabilitySurface> sessionGatewayRepeatabilityFactory;

    public RuntimeAlphaExternalUseReadinessService(
        string repoRoot,
        Func<RuntimeLocalDistFreshnessSmokeSurface> localDistFreshnessSmokeFactory,
        Func<RuntimeExternalConsumerResourcePackSurface> externalConsumerResourcePackFactory,
        Func<RuntimeGovernedAgentHandoffProofSurface> governedAgentHandoffProofFactory,
        Func<RuntimeProductClosurePilotGuideSurface> productizedPilotGuideFactory,
        Func<RuntimeSessionGatewayPrivateAlphaHandoffSurface> sessionGatewayPrivateAlphaFactory,
        Func<RuntimeSessionGatewayRepeatabilitySurface> sessionGatewayRepeatabilityFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.localDistFreshnessSmokeFactory = localDistFreshnessSmokeFactory;
        this.externalConsumerResourcePackFactory = externalConsumerResourcePackFactory;
        this.governedAgentHandoffProofFactory = governedAgentHandoffProofFactory;
        this.productizedPilotGuideFactory = productizedPilotGuideFactory;
        this.sessionGatewayPrivateAlphaFactory = sessionGatewayPrivateAlphaFactory;
        this.sessionGatewayRepeatabilityFactory = sessionGatewayRepeatabilityFactory;
    }

    public RuntimeAlphaExternalUseReadinessSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidateRuntimeDocument(PreviousPhaseDocumentPath, "Product closure previous phase document", errors);
        ValidateRuntimeDocument(RuntimeTargetIgnoreDecisionRecordService.AuditPhaseDocumentPath, "Product closure Phase 30 target ignore decision record audit document", errors);
        ValidateRuntimeDocument(RuntimeExternalConsumerResourcePackService.ResourcePackGuideDocumentPath, "External consumer resource pack guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotGuideService.GuideDocumentPath, "Productized pilot guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotStatusService.GuideDocumentPath, "Productized pilot status document", errors);
        ValidateRuntimeDocument("docs/runtime/runtime-governed-agent-handoff-proof.md", "Runtime governed agent handoff proof document", errors);
        ValidateRuntimeDocument("docs/session-gateway/repeatability-readiness.md", "Session Gateway repeatability readiness document", errors);

        var localDistFreshnessSmoke = localDistFreshnessSmokeFactory();
        var externalConsumerResourcePack = externalConsumerResourcePackFactory();
        var governedAgentHandoffProof = governedAgentHandoffProofFactory();
        var productizedPilotGuide = productizedPilotGuideFactory();
        var privateAlpha = sessionGatewayPrivateAlphaFactory();
        var repeatability = sessionGatewayRepeatabilityFactory();

        var dependencyErrors = localDistFreshnessSmoke.Errors.Select(static error => $"runtime-local-dist-freshness-smoke: {error}")
            .Concat(externalConsumerResourcePack.Errors.Select(static error => $"runtime-external-consumer-resource-pack: {error}"))
            .Concat(governedAgentHandoffProof.Errors.Select(static error => $"runtime-governed-agent-handoff-proof: {error}"))
            .Concat(productizedPilotGuide.Errors.Select(static error => $"runtime-product-closure-pilot-guide: {error}"))
            .Concat(privateAlpha.Errors.Select(static error => $"runtime-session-gateway-private-alpha-handoff: {error}"))
            .Concat(repeatability.Errors.Select(static error => $"runtime-session-gateway-repeatability: {error}"))
            .ToArray();

        var warnings = privateAlpha.Warnings
            .Select(static warning => $"runtime-session-gateway-private-alpha-handoff: {warning}")
            .Concat(repeatability.Warnings.Select(static warning => $"runtime-session-gateway-repeatability: {warning}"))
            .ToArray();

        var frozenLocalDistReady = localDistFreshnessSmoke.IsValid
                                   && localDistFreshnessSmoke.LocalDistFreshnessSmokeReady
                                   && localDistFreshnessSmoke.ManifestSourceCommitMatchesSourceHead
                                   && localDistFreshnessSmoke.SourceGitWorktreeClean;
        var externalConsumerResourcePackReady = externalConsumerResourcePack.IsValid
                                                && externalConsumerResourcePack.ResourcePackComplete;
        var governedAgentHandoffReady = governedAgentHandoffProof.IsValid
                                        && string.Equals(governedAgentHandoffProof.OverallPosture, "bounded_governed_agent_handoff_proof_ready", StringComparison.Ordinal);
        var productizedPilotGuideReady = productizedPilotGuide.IsValid
                                         && string.Equals(productizedPilotGuide.OverallPosture, "productized_pilot_guide_ready", StringComparison.Ordinal);
        var sessionGatewayPrivateAlphaReady = privateAlpha.IsValid
                                              && string.Equals(privateAlpha.OverallPosture, "private_alpha_deliverable_ready", StringComparison.Ordinal);
        var sessionGatewayRepeatabilityReady = repeatability.IsValid
                                               && string.Equals(repeatability.OverallPosture, "repeatable_private_alpha_ready", StringComparison.Ordinal);
        var checks = new[]
        {
            BuildCheck(
                "frozen_local_dist",
                localDistFreshnessSmoke.SurfaceId,
                localDistFreshnessSmoke.OverallPosture,
                frozenLocalDistReady,
                "Frozen local dist exists, includes current Runtime resources, and matches the clean source HEAD."),
            BuildCheck(
                "external_consumer_resource_pack",
                externalConsumerResourcePack.SurfaceId,
                externalConsumerResourcePack.OverallPosture,
                externalConsumerResourcePackReady,
                "External projects can read Runtime-owned docs and command entries without copying Runtime truth."),
            BuildCheck(
                "governed_agent_handoff",
                governedAgentHandoffProof.SurfaceId,
                governedAgentHandoffProof.OverallPosture,
                governedAgentHandoffReady,
                "Agents can read the constraint ladder, collaboration plane, and governed handoff proof."),
            BuildCheck(
                "productized_pilot_guide",
                productizedPilotGuide.SurfaceId,
                productizedPilotGuide.OverallPosture,
                productizedPilotGuideReady,
                "Operators and agents can follow one staged external-project route."),
            BuildCheck(
                "session_gateway_private_alpha",
                "runtime-session-gateway-private-alpha-handoff",
                privateAlpha.OverallPosture,
                sessionGatewayPrivateAlphaReady,
                "Session Gateway v1 private-alpha handoff stays Runtime-owned and bounded.",
                blocksAlphaUse: false),
            BuildCheck(
                "session_gateway_repeatability",
                "runtime-session-gateway-repeatability",
                repeatability.OverallPosture,
                sessionGatewayRepeatabilityReady,
                "Private-alpha gateway use can be repeated through the same Runtime-owned lane.",
                blocksAlphaUse: false),
            BuildCheck(
                "target_product_pilot_proof",
                "runtime-product-pilot-proof",
                "required_per_target",
                ready: true,
                "Each external target still must run its own product pilot proof after attach, writeback, commit closure, and dist binding.",
                blocksAlphaUse: false),
        };

        var alphaReady = errors.Count == 0 && checks.Where(static check => check.BlocksAlphaUse).All(static check => check.Ready);
        var alphaVersion = ResolveAlphaVersion(localDistFreshnessSmoke);
        var gaps = BuildGaps(
                localDistFreshnessSmoke,
                externalConsumerResourcePack,
                governedAgentHandoffProof,
                productizedPilotGuide,
                privateAlpha,
                repeatability,
                checks,
                errors,
                dependencyErrors)
            .ToArray();

        return new RuntimeAlphaExternalUseReadinessSurface
        {
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = alphaReady
                ? "alpha_external_use_readiness_ready"
                : "alpha_external_use_readiness_blocked",
            AlphaVersion = alphaVersion,
            AlphaExternalUseReady = alphaReady,
            FrozenLocalDistReady = frozenLocalDistReady,
            ExternalConsumerResourcePackReady = externalConsumerResourcePackReady,
            GovernedAgentHandoffReady = governedAgentHandoffReady,
            ProductizedPilotGuideReady = productizedPilotGuideReady,
            SessionGatewayPrivateAlphaReady = sessionGatewayPrivateAlphaReady,
            SessionGatewayRepeatabilityReady = sessionGatewayRepeatabilityReady,
            CandidateDistRoot = localDistFreshnessSmoke.CandidateDistRoot,
            DistManifestSourceCommit = localDistFreshnessSmoke.ManifestSourceCommit,
            SourceGitHead = localDistFreshnessSmoke.SourceGitHead,
            SourceGitWorktreeClean = localDistFreshnessSmoke.SourceGitWorktreeClean,
            DistManifestMatchesSourceHead = localDistFreshnessSmoke.ManifestSourceCommitMatchesSourceHead,
            ReadinessChecks = checks,
            MinimumOperatorReadbacks = BuildMinimumOperatorReadbacks(),
            ExternalTargetStartCommands = BuildExternalTargetStartCommands(localDistFreshnessSmoke.CandidateDistRoot),
            BoundaryRules =
            [
                "Alpha external-use readiness is a Runtime-owned rollup; it is not target product completion.",
                "A target repo still needs its own init, bootstrap, status, managed work, review, commit closure, residue, ignore decision, dist binding, target proof, and product proof readbacks.",
                "External projects should consume a frozen local Runtime dist for stable alpha work, not the active Runtime source checkout.",
                "Runtime-owned docs stay in the Runtime document root; target repos should reference them through attach/bootstrap/resource surfaces.",
                "Session Gateway private-alpha readiness remains Strict Broker-only and does not create a client-owned control plane.",
            ],
            Gaps = gaps,
            Summary = alphaReady
                ? $"CARVES Runtime {alphaVersion} is ready for bounded external-project alpha use from the frozen local dist; each target still must prove its own governed pilot closure."
                : $"CARVES Runtime {alphaVersion} is not ready for bounded external-project alpha use until blocking readiness checks are restored.",
            RecommendedNextAction = alphaReady
                ? "Start a target repo from the frozen dist wrapper with carves agent start --json, then follow next_governed_command exactly; use readiness, invocation, activation, dist-smoke, resources, status, guide, and detailed pilot readbacks only when the start payload asks for them."
                : "Resolve the listed readiness gaps, refresh the local dist from a clean Runtime source tree, then rerun carves pilot readiness --json.",
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "This rollup does not claim public package distribution, signed installers, automatic updates, OS sandboxing, full ACP, full MCP, or remote worker orchestration.",
                "This rollup does not make AgentCoach or any specific target repo a Runtime closure prerequisite.",
                "This rollup does not initialize, repair, retarget, stage, commit, push, tag, release, pack, or copy files.",
                "This rollup does not replace per-target product pilot proof or operator review of target git commits.",
            ],
        };
    }

    private static string ResolveAlphaVersion(RuntimeLocalDistFreshnessSmokeSurface localDistFreshnessSmoke)
    {
        if (!string.IsNullOrWhiteSpace(localDistFreshnessSmoke.ManifestVersion))
        {
            return localDistFreshnessSmoke.ManifestVersion;
        }

        if (!string.IsNullOrWhiteSpace(localDistFreshnessSmoke.VersionFileValue))
        {
            return localDistFreshnessSmoke.VersionFileValue;
        }

        if (!string.IsNullOrWhiteSpace(localDistFreshnessSmoke.DistVersion))
        {
            return localDistFreshnessSmoke.DistVersion;
        }

        return RuntimeAlphaVersion.Current;
    }

    private static RuntimeAlphaExternalUseReadinessCheckSurface BuildCheck(
        string checkId,
        string surfaceId,
        string posture,
        bool ready,
        string summary,
        bool blocksAlphaUse = true)
    {
        return new RuntimeAlphaExternalUseReadinessCheckSurface
        {
            CheckId = checkId,
            SurfaceId = surfaceId,
            Posture = posture,
            Ready = ready,
            BlocksAlphaUse = blocksAlphaUse,
            Summary = summary,
        };
    }

    private static string[] BuildMinimumOperatorReadbacks()
    {
        return
        [
            "carves agent start --json",
            "carves pilot start --json",
            "carves pilot problem-intake --json",
            "carves pilot triage --json",
            "carves pilot follow-up --json",
            "carves pilot follow-up-plan --json",
            "carves pilot follow-up-record --json",
            "carves pilot follow-up-intake --json",
            "carves pilot follow-up-gate --json",
            "carves pilot readiness --json",
            "carves pilot invocation --json",
            "carves pilot activation --json",
            "carves pilot dist-smoke --json",
            "carves pilot resources --json",
            "carves agent handoff --json",
            "carves pilot next --json",
            "carves pilot status --json",
            "carves pilot guide",
        ];
    }

    private static string[] BuildExternalTargetStartCommands(string candidateDistRoot)
    {
        return
        [
            FormatCandidateDistCommand(candidateDistRoot, "agent", "start", "--json"),
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
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "activation", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "dist-smoke", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "resources", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "agent", "handoff", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "next", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "status", "--json"),
            FormatCandidateDistCommand(candidateDistRoot, "pilot", "guide"),
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

    private static IEnumerable<string> BuildGaps(
        RuntimeLocalDistFreshnessSmokeSurface localDistFreshnessSmoke,
        RuntimeExternalConsumerResourcePackSurface externalConsumerResourcePack,
        RuntimeGovernedAgentHandoffProofSurface governedAgentHandoffProof,
        RuntimeProductClosurePilotGuideSurface productizedPilotGuide,
        RuntimeSessionGatewayPrivateAlphaHandoffSurface privateAlpha,
        RuntimeSessionGatewayRepeatabilitySurface repeatability,
        IReadOnlyList<RuntimeAlphaExternalUseReadinessCheckSurface> checks,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> dependencyErrors)
    {
        foreach (var error in errors)
        {
            yield return $"alpha_readiness_surface:{error}";
        }

        foreach (var error in dependencyErrors)
        {
            yield return $"alpha_readiness_dependency:{error}";
        }

        foreach (var check in checks.Where(static check => check.BlocksAlphaUse && !check.Ready))
        {
            yield return $"alpha_readiness_check_not_ready:{check.CheckId}";
        }

        foreach (var check in checks.Where(static check => !check.BlocksAlphaUse && !check.Ready))
        {
            yield return $"alpha_readiness_advisory_not_ready:{check.CheckId}";
        }

        foreach (var gap in localDistFreshnessSmoke.Gaps)
        {
            yield return $"runtime-local-dist-freshness-smoke:{gap}";
        }

        foreach (var gap in externalConsumerResourcePack.Gaps)
        {
            yield return $"runtime-external-consumer-resource-pack:{gap}";
        }

        foreach (var error in governedAgentHandoffProof.Errors)
        {
            yield return $"runtime-governed-agent-handoff-proof:{error}";
        }

        foreach (var error in productizedPilotGuide.Errors)
        {
            yield return $"runtime-product-closure-pilot-guide:{error}";
        }

        foreach (var error in privateAlpha.Errors)
        {
            yield return $"runtime-session-gateway-private-alpha-handoff:{error}";
        }

        foreach (var error in repeatability.Errors)
        {
            yield return $"runtime-session-gateway-repeatability:{error}";
        }
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
