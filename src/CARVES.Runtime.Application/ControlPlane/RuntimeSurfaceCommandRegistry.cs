namespace Carves.Runtime.Application.ControlPlane;

public static class RuntimeSurfaceCommandRegistry
{
    public const int MaxDefaultVisibleSurfaceCount = 16;

    public const string RuntimeGovernanceArchiveStatusSurfaceId = "runtime-governance-archive-status";

    private static readonly RuntimeSurfaceCommandDescriptor[] Commands =
    [
        NoArg("codex-tool-surface", static service => service.InspectCodexToolSurface(), static service => service.ApiCodexToolSurface()),
        NoArg("execution-contract-surface", static service => service.InspectExecutionContractSurface(), static service => service.ApiExecutionContractSurface()),
        NoArg("runtime-core-adapter-boundary", static service => service.InspectRuntimeCoreAdapterBoundary(), static service => service.ApiRuntimeCoreAdapterBoundary()),
        NoArg("runtime-agent-governance-kernel", static service => service.InspectRuntimeAgentGovernanceKernel(), static service => service.ApiRuntimeAgentGovernanceKernel()),
        NoArg("runtime-agent-thread-start", static service => service.InspectRuntimeAgentThreadStart(), static service => service.ApiRuntimeAgentThreadStart(), defaultVisibility: RuntimeSurfaceDefaultVisibility.DefaultVisible),
        OptionalArg("runtime-agent-short-context", "task-id", static (service, argument) => service.InspectRuntimeAgentShortContext(argument), static (service, argument) => service.ApiRuntimeAgentShortContext(argument), RuntimeSurfaceContextTier.TaskScoped, RuntimeSurfaceDefaultVisibility.DefaultVisible),
        OptionalArg("runtime-markdown-read-path-budget", "task-id", static (service, argument) => service.InspectRuntimeMarkdownReadPathBudget(argument), static (service, argument) => service.ApiRuntimeMarkdownReadPathBudget(argument), defaultVisibility: RuntimeSurfaceDefaultVisibility.DefaultVisible),
        NoArg("runtime-agent-bootstrap-packet", static service => service.InspectRuntimeAgentBootstrapPacket(), static service => service.ApiRuntimeAgentBootstrapPacket(), RuntimeSurfaceContextTier.StartupSafe),
        OptionalArg("runtime-agent-bootstrap-receipt", "receipt-json-path", static (service, argument) => service.InspectRuntimeAgentBootstrapReceipt(argument), static (service, argument) => service.ApiRuntimeAgentBootstrapReceipt(argument), RuntimeSurfaceContextTier.StartupSafe),
        NoArg("runtime-agent-queue-projection", static service => service.InspectRuntimeAgentQueueProjection(), static service => service.ApiRuntimeAgentQueueProjection(), RuntimeSurfaceContextTier.StartupSafe),
        RequiredArg("runtime-agent-task-overlay", "task-id", static (service, argument) => service.InspectRuntimeAgentTaskOverlay(argument!), static (service, argument) => service.ApiRuntimeAgentTaskOverlay(argument!), RuntimeSurfaceContextTier.TaskScoped, RuntimeSurfaceDefaultVisibility.DefaultVisible),
        NoArg("runtime-agent-model-profile-routing", static service => service.InspectRuntimeAgentModelProfileRouting(), static service => service.ApiRuntimeAgentModelProfileRouting()),
        RequiredArg("runtime-agent-loop-stall-guard", "task-id", static (service, argument) => service.InspectRuntimeAgentLoopStallGuard(argument!), static (service, argument) => service.ApiRuntimeAgentLoopStallGuard(argument!)),
        NoArg("runtime-agent-weak-model-lane", static service => service.InspectRuntimeAgentWeakModelLane(), static service => service.ApiRuntimeAgentWeakModelLane()),
        NoArg("runtime-minimal-worker-baseline", static service => service.InspectRuntimeMinimalWorkerBaseline(), static service => service.ApiRuntimeMinimalWorkerBaseline()),
        NoArg("runtime-code-understanding-engine", static service => service.InspectRuntimeCodeUnderstandingEngine(), static service => service.ApiRuntimeCodeUnderstandingEngine()),
        NoArg("runtime-durable-execution-semantics", static service => service.InspectRuntimeDurableExecutionSemantics(), static service => service.ApiRuntimeDurableExecutionSemantics()),
        NoArg("runtime-repo-authored-gate-loop", static service => service.InspectRuntimeRepoAuthoredGateLoop(), static service => service.ApiRuntimeRepoAuthoredGateLoop()),
        NoArg("runtime-git-native-coding-loop", static service => service.InspectRuntimeGitNativeCodingLoop(), static service => service.ApiRuntimeGitNativeCodingLoop()),
        NoArg("runtime-context-kernel", static service => service.InspectRuntimeContextKernel(), static service => service.ApiRuntimeContextKernel()),
        NoArg("runtime-knowledge-kernel", static service => service.InspectRuntimeKnowledgeKernel(), static service => service.ApiRuntimeKnowledgeKernel()),
        NoArg("runtime-domain-graph-kernel", static service => service.InspectRuntimeDomainGraphKernel(), static service => service.ApiRuntimeDomainGraphKernel()),
        NoArg("runtime-execution-kernel", static service => service.InspectRuntimeExecutionKernel(), static service => service.ApiRuntimeExecutionKernel()),
        NoArg("runtime-artifact-policy-kernel", static service => service.InspectRuntimeArtifactPolicyKernel(), static service => service.ApiRuntimeArtifactPolicyKernel()),
        NoArg(RuntimeGovernanceArchiveStatusSurfaceId, static service => service.InspectRuntimeGovernanceArchiveStatus(), static service => service.ApiRuntimeGovernanceArchiveStatus(), RuntimeSurfaceContextTier.AuditOnly),
        OptionalArg("runtime-validationlab-proof-handoff", "lane-id", static (service, argument) => service.InspectRuntimeGovernanceArchiveStatusAlias("runtime-validationlab-proof-handoff", argument), static (service, argument) => service.ApiRuntimeGovernanceArchiveStatusAlias("runtime-validationlab-proof-handoff", argument), surfaceRole: RuntimeSurfaceRole.CompatibilityAlias, successorSurfaceId: RuntimeGovernanceArchiveStatusSurfaceId, retirementPosture: RuntimeSurfaceRetirementPosture.AliasRetained),
        OptionalArg("runtime-controlled-governance-proof", "lane-id", static (service, argument) => service.InspectRuntimeGovernanceArchiveStatusAlias("runtime-controlled-governance-proof", argument), static (service, argument) => service.ApiRuntimeGovernanceArchiveStatusAlias("runtime-controlled-governance-proof", argument), surfaceRole: RuntimeSurfaceRole.CompatibilityAlias, successorSurfaceId: RuntimeGovernanceArchiveStatusSurfaceId, retirementPosture: RuntimeSurfaceRetirementPosture.AliasRetained),
        NoArg("runtime-packaging-proof-federation-maturity", static service => service.InspectRuntimeGovernanceArchiveStatusAlias("runtime-packaging-proof-federation-maturity"), static service => service.ApiRuntimeGovernanceArchiveStatusAlias("runtime-packaging-proof-federation-maturity"), surfaceRole: RuntimeSurfaceRole.CompatibilityAlias, successorSurfaceId: RuntimeGovernanceArchiveStatusSurfaceId, retirementPosture: RuntimeSurfaceRetirementPosture.AliasRetained),
        NoArg("runtime-hotspot-backlog-drain", static service => service.InspectRuntimeGovernanceArchiveStatusAlias("runtime-hotspot-backlog-drain"), static service => service.ApiRuntimeGovernanceArchiveStatusAlias("runtime-hotspot-backlog-drain"), surfaceRole: RuntimeSurfaceRole.CompatibilityAlias, successorSurfaceId: RuntimeGovernanceArchiveStatusSurfaceId, retirementPosture: RuntimeSurfaceRetirementPosture.AliasRetained),
        NoArg("runtime-hotspot-cross-family-patterns", static service => service.InspectRuntimeGovernanceArchiveStatusAlias("runtime-hotspot-cross-family-patterns"), static service => service.ApiRuntimeGovernanceArchiveStatusAlias("runtime-hotspot-cross-family-patterns"), surfaceRole: RuntimeSurfaceRole.CompatibilityAlias, successorSurfaceId: RuntimeGovernanceArchiveStatusSurfaceId, retirementPosture: RuntimeSurfaceRetirementPosture.AliasRetained),
        NoArg("runtime-governance-program-reaudit", static service => service.InspectRuntimeGovernanceArchiveStatusAlias("runtime-governance-program-reaudit"), static service => service.ApiRuntimeGovernanceArchiveStatusAlias("runtime-governance-program-reaudit"), surfaceRole: RuntimeSurfaceRole.CompatibilityAlias, successorSurfaceId: RuntimeGovernanceArchiveStatusSurfaceId, retirementPosture: RuntimeSurfaceRetirementPosture.AliasRetained),
        NoArg("runtime-semantic-correctness-death-test-evidence", static service => service.InspectRuntimeSemanticCorrectnessDeathTestEvidence(), static service => service.ApiRuntimeSemanticCorrectnessDeathTestEvidence()),
        NoArg("runtime-central-interaction-multi-device-projection", static service => service.InspectRuntimeCentralInteractionMultiDeviceProjection(), static service => service.ApiRuntimeCentralInteractionMultiDeviceProjection()),
        NoArg("runtime-session-gateway-dogfood-validation", static service => service.InspectRuntimeSessionGatewayDogfoodValidation(), static service => service.ApiRuntimeSessionGatewayDogfoodValidation()),
        NoArg("runtime-session-gateway-private-alpha-handoff", static service => service.InspectRuntimeSessionGatewayPrivateAlphaHandoff(), static service => service.ApiRuntimeSessionGatewayPrivateAlphaHandoff()),
        NoArg("runtime-session-gateway-repeatability", static service => service.InspectRuntimeSessionGatewayRepeatability(), static service => service.ApiRuntimeSessionGatewayRepeatability()),
        NoArg("runtime-session-gateway-internal-beta-gate", static service => service.InspectRuntimeSessionGatewayInternalBetaGate(), static service => service.ApiRuntimeSessionGatewayInternalBetaGate()),
        NoArg("runtime-first-run-operator-packet", static service => service.InspectRuntimeFirstRunOperatorPacket(), static service => service.ApiRuntimeFirstRunOperatorPacket()),
        NoArg("runtime-guided-planning-boundary", static service => service.InspectRuntimeGuidedPlanningBoundary(), static service => service.ApiRuntimeGuidedPlanningBoundary()),
        NoArg("runtime-agent-working-modes", static service => service.InspectRuntimeAgentWorkingModes(), static service => service.ApiRuntimeAgentWorkingModes()),
        NoArg("runtime-formal-planning-posture", static service => service.InspectRuntimeFormalPlanningPosture(), static service => service.ApiRuntimeFormalPlanningPosture()),
        NoArg("runtime-vendor-native-acceleration", static service => service.InspectRuntimeVendorNativeAcceleration(), static service => service.ApiRuntimeVendorNativeAcceleration()),
        NoArg("runtime-managed-workspace", static service => service.InspectRuntimeManagedWorkspace(), static service => service.ApiRuntimeManagedWorkspace()),
        NoArg("runtime-acceptance-contract-ingress-policy", static service => service.InspectRuntimeAcceptanceContractIngressPolicy(), static service => service.ApiRuntimeAcceptanceContractIngressPolicy()),
        NoArg("runtime-adapter-handoff-contract", static service => service.InspectRuntimeAdapterHandoffContract(), static service => service.ApiRuntimeAdapterHandoffContract()),
        NoArg("runtime-protected-truth-root-policy", static service => service.InspectRuntimeProtectedTruthRootPolicy(), static service => service.ApiRuntimeProtectedTruthRootPolicy()),
        NoArg("runtime-governed-agent-handoff-proof", static service => service.InspectRuntimeGovernedAgentHandoffProof(), static service => service.ApiRuntimeGovernedAgentHandoffProof(), defaultVisibility: RuntimeSurfaceDefaultVisibility.DefaultVisible),
        NoArg("runtime-product-closure-pilot-guide", static service => service.InspectRuntimeProductClosurePilotGuide(), static service => service.ApiRuntimeProductClosurePilotGuide()),
        NoArg("runtime-product-closure-pilot-status", static service => service.InspectRuntimeProductClosurePilotStatus(), static service => service.ApiRuntimeProductClosurePilotStatus()),
        NoArg("runtime-target-agent-bootstrap-pack", static service => service.InspectRuntimeTargetAgentBootstrapPack(), static service => service.ApiRuntimeTargetAgentBootstrapPack()),
        NoArg("runtime-target-commit-hygiene", static service => service.InspectRuntimeTargetCommitHygiene(), static service => service.ApiRuntimeTargetCommitHygiene()),
        NoArg("runtime-target-commit-plan", static service => service.InspectRuntimeTargetCommitPlan(), static service => service.ApiRuntimeTargetCommitPlan()),
        NoArg("runtime-target-commit-closure", static service => service.InspectRuntimeTargetCommitClosure(), static service => service.ApiRuntimeTargetCommitClosure()),
        NoArg("runtime-target-residue-policy", static service => service.InspectRuntimeTargetResiduePolicy(), static service => service.ApiRuntimeTargetResiduePolicy()),
        NoArg("runtime-target-ignore-decision-plan", static service => service.InspectRuntimeTargetIgnoreDecisionPlan(), static service => service.ApiRuntimeTargetIgnoreDecisionPlan()),
        NoArg("runtime-target-ignore-decision-record", static service => service.InspectRuntimeTargetIgnoreDecisionRecord(), static service => service.ApiRuntimeTargetIgnoreDecisionRecord()),
        NoArg("runtime-local-dist-handoff", static service => service.InspectRuntimeLocalDistHandoff(), static service => service.ApiRuntimeLocalDistHandoff()),
        NoArg("runtime-product-pilot-proof", static service => service.InspectRuntimeProductPilotProof(), static service => service.ApiRuntimeProductPilotProof()),
        NoArg("runtime-external-consumer-resource-pack", static service => service.InspectRuntimeExternalConsumerResourcePack(), static service => service.ApiRuntimeExternalConsumerResourcePack()),
        NoArg("runtime-cli-invocation-contract", static service => service.InspectRuntimeCliInvocationContract(), static service => service.ApiRuntimeCliInvocationContract()),
        NoArg("runtime-cli-activation-plan", static service => service.InspectRuntimeCliActivationPlan(), static service => service.ApiRuntimeCliActivationPlan()),
        NoArg("runtime-target-dist-binding-plan", static service => service.InspectRuntimeTargetDistBindingPlan(), static service => service.ApiRuntimeTargetDistBindingPlan()),
        NoArg("runtime-local-dist-freshness-smoke", static service => service.InspectRuntimeLocalDistFreshnessSmoke(), static service => service.ApiRuntimeLocalDistFreshnessSmoke()),
        NoArg("runtime-frozen-dist-target-readback-proof", static service => service.InspectRuntimeFrozenDistTargetReadbackProof(), static service => service.ApiRuntimeFrozenDistTargetReadbackProof()),
        NoArg("runtime-alpha-external-use-readiness", static service => service.InspectRuntimeAlphaExternalUseReadiness(), static service => service.ApiRuntimeAlphaExternalUseReadiness()),
        NoArg("runtime-external-target-pilot-start", static service => service.InspectRuntimeExternalTargetPilotStart(), static service => service.ApiRuntimeExternalTargetPilotStart()),
        NoArg("runtime-external-target-pilot-next", static service => service.InspectRuntimeExternalTargetPilotNext(), static service => service.ApiRuntimeExternalTargetPilotNext()),
        NoArg("runtime-agent-problem-intake", static service => service.InspectRuntimeAgentProblemIntake(), static service => service.ApiRuntimeAgentProblemIntake()),
        NoArg("runtime-agent-problem-triage-ledger", static service => service.InspectRuntimeAgentProblemTriageLedger(), static service => service.ApiRuntimeAgentProblemTriageLedger()),
        NoArg("runtime-agent-problem-follow-up-candidates", static service => service.InspectRuntimeAgentProblemFollowUpCandidates(), static service => service.ApiRuntimeAgentProblemFollowUpCandidates()),
        NoArg("runtime-agent-problem-follow-up-decision-plan", static service => service.InspectRuntimeAgentProblemFollowUpDecisionPlan(), static service => service.ApiRuntimeAgentProblemFollowUpDecisionPlan()),
        NoArg("runtime-agent-problem-follow-up-decision-record", static service => service.InspectRuntimeAgentProblemFollowUpDecisionRecord(), static service => service.ApiRuntimeAgentProblemFollowUpDecisionRecord()),
        NoArg("runtime-agent-problem-follow-up-planning-intake", static service => service.InspectRuntimeAgentProblemFollowUpPlanningIntake(), static service => service.ApiRuntimeAgentProblemFollowUpPlanningIntake()),
        NoArg("runtime-agent-problem-follow-up-planning-gate", static service => service.InspectRuntimeAgentProblemFollowUpPlanningGate(), static service => service.ApiRuntimeAgentProblemFollowUpPlanningGate()),
        NoArg("runtime-agent-failure-recovery-closure", static service => service.InspectRuntimeAgentFailureRecoveryClosure(), static service => service.ApiRuntimeAgentFailureRecoveryClosure()),
        NoArg("runtime-agent-validation-bundle", static service => service.InspectRuntimeAgentValidationBundle(), static service => service.ApiRuntimeAgentValidationBundle()),
        NoArg("runtime-agent-delivery-readiness", static service => service.InspectRuntimeAgentDeliveryReadiness(), static service => service.ApiRuntimeAgentDeliveryReadiness()),
        NoArg("runtime-agent-operator-feedback-closure", static service => service.InspectRuntimeAgentOperatorFeedbackClosure(), static service => service.ApiRuntimeAgentOperatorFeedbackClosure()),
        NoArg("runtime-session-gateway-internal-beta-exit-contract", static service => service.InspectRuntimeSessionGatewayInternalBetaExitContract(), static service => service.ApiRuntimeSessionGatewayInternalBetaExitContract()),
        NoArg("runtime-beta-program-status", static service => service.InspectRuntimeBetaProgramStatus(), static service => service.ApiRuntimeBetaProgramStatus()),
        NoArg("runtime-session-gateway-governance-assist", static service => service.InspectRuntimeSessionGatewayGovernanceAssist(), static service => service.ApiRuntimeSessionGatewayGovernanceAssist()),
        NoArg("runtime-kernel-upgrade-qualification", static service => service.InspectRuntimeKernelUpgradeQualification(), static service => service.ApiRuntimeKernelUpgradeQualification()),
        NoArg("runtime-remote-worker-qualification", static service => service.InspectRuntimeRemoteWorkerQualification(), static service => service.ApiRuntimeRemoteWorkerQualification()),
        NoArg("runtime-remote-worker-onboarding-checklist", static service => service.InspectRuntimeRemoteWorkerOnboardingChecklist(), static service => service.ApiRuntimeRemoteWorkerOnboardingChecklist()),
        NoArg("runtime-claude-worker-qualification", static service => service.InspectRuntimeClaudeWorkerQualification(), static service => service.ApiRuntimeClaudeWorkerQualification()),
        NoArg("runtime-pack-admission", static service => service.InspectRuntimePackAdmission(), static service => service.ApiRuntimePackAdmission()),
        NoArg("runtime-pack-admission-policy", static service => service.InspectRuntimePackAdmissionPolicy(), static service => service.ApiRuntimePackAdmissionPolicy()),
        NoArg("runtime-pack-policy-transfer", static service => service.InspectRuntimePackPolicyTransfer(), static service => service.ApiRuntimePackPolicyTransfer()),
        NoArg("runtime-pack-policy-preview", static service => service.InspectRuntimePackPolicyPreview(), static service => service.ApiRuntimePackPolicyPreview()),
        NoArg("runtime-pack-selection", static service => service.InspectRuntimePackSelection(), static service => service.ApiRuntimePackSelection()),
        RequiredArg("runtime-pack-task-explainability", "task-id", static (service, argument) => service.InspectRuntimePackTaskExplainability(argument!), static (service, argument) => service.ApiRuntimePackTaskExplainability(argument!)),
        NoArg("runtime-pack-switch-policy", static service => service.InspectRuntimePackSwitchPolicy(), static service => service.ApiRuntimePackSwitchPolicy()),
        NoArg("runtime-pack-policy-audit", static service => service.InspectRuntimePackPolicyAudit(), static service => service.ApiRuntimePackPolicyAudit()),
        NoArg("runtime-pack-execution-audit", static service => service.InspectRuntimePackExecutionAudit(), static service => service.ApiRuntimePackExecutionAudit()),
        OptionalArg("runtime-worker-execution-audit", "query", static (service, argument) => service.InspectRuntimeWorkerExecutionAudit(argument), static (service, argument) => service.ApiRuntimeWorkerExecutionAudit(argument), defaultVisibility: RuntimeSurfaceDefaultVisibility.DefaultVisible),
        NoArg("runtime-governance-surface-coverage-audit", static service => service.InspectRuntimeGovernanceSurfaceCoverageAudit(), static service => service.ApiRuntimeGovernanceSurfaceCoverageAudit()),
        NoArg("runtime-file-granularity-audit", static service => service.InspectRuntimeFileGranularityAudit(), static service => service.ApiRuntimeFileGranularityAudit()),
        NoArg("runtime-default-workflow-proof", static service => service.InspectRuntimeDefaultWorkflowProof(), static service => service.ApiRuntimeDefaultWorkflowProof(), defaultVisibility: RuntimeSurfaceDefaultVisibility.DefaultVisible),
        NoArg("runtime-pack-mismatch-diagnostics", static service => service.InspectRuntimePackMismatchDiagnostics(), static service => service.ApiRuntimePackMismatchDiagnostics()),
        NoArg("runtime-pack-distribution-boundary", static service => service.InspectRuntimePackDistributionBoundary(), static service => service.ApiRuntimePackDistributionBoundary()),
        OptionalArg("runtime-export-profiles", "profile-id", static (service, argument) => service.InspectRuntimeExportProfiles(argument), static (service, argument) => service.ApiRuntimeExportProfiles(argument)),
        RequiredArg("execution-packet", "task-id", static (service, argument) => service.InspectExecutionPacket(argument!), static (service, argument) => service.ApiExecutionPacket(argument!), defaultVisibility: RuntimeSurfaceDefaultVisibility.DefaultVisible),
        RequiredArg("packet-enforcement", "task-id", static (service, argument) => service.InspectPacketEnforcement(argument!), static (service, argument) => service.ApiPacketEnforcement(argument!), defaultVisibility: RuntimeSurfaceDefaultVisibility.DefaultVisible),
        RequiredArg("runtime-brokered-execution", "task-id", static (service, argument) => service.InspectRuntimeBrokeredExecution(argument!), static (service, argument) => service.ApiRuntimeBrokeredExecution(argument!), defaultVisibility: RuntimeSurfaceDefaultVisibility.DefaultVisible),
        RequiredArg("runtime-workspace-mutation-audit", "task-id", static (service, argument) => service.InspectRuntimeWorkspaceMutationAudit(argument!), static (service, argument) => service.ApiRuntimeWorkspaceMutationAudit(argument!), defaultVisibility: RuntimeSurfaceDefaultVisibility.DefaultVisible),
        NoArg("authoritative-truth-store", static service => service.InspectAuthoritativeTruthStore(), static service => service.ApiAuthoritativeTruthStore()),
        RequiredArg("execution-hardening", "task-id", static (service, argument) => service.InspectExecutionHardening(argument!), static (service, argument) => service.ApiExecutionHardening(argument!), defaultVisibility: RuntimeSurfaceDefaultVisibility.DefaultVisible)
    ];

    private static readonly IReadOnlyDictionary<string, RuntimeSurfaceCommandDescriptor> Index =
        Commands.ToDictionary(command => command.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> CommandNames { get; } = Commands.Select(command => command.Name).ToArray();

    public static IReadOnlyList<RuntimeSurfaceCommandMetadata> CommandMetadata { get; } =
        Commands
            .Select(command => new RuntimeSurfaceCommandMetadata(
                command.Name,
                command.ContextTier,
                command.DefaultVisibility,
                $"inspect {command.BuildUsageTail()}",
                $"api {command.BuildUsageTail()}",
                command.SurfaceRole,
                command.SuccessorSurfaceId,
                command.RetirementPosture))
            .ToArray();

    public static IReadOnlyList<RuntimeSurfaceCommandMetadata> DefaultVisibleCommandMetadata { get; } =
        CommandMetadata
            .Where(command => command.DefaultVisibility == RuntimeSurfaceDefaultVisibility.DefaultVisible)
            .ToArray();

    public static IReadOnlyList<string> DefaultVisibleCommandNames { get; } =
        DefaultVisibleCommandMetadata
            .Select(command => command.Name)
            .ToArray();

    public static IReadOnlyList<string> ExplicitOnlyCommandNames { get; } =
        CommandMetadata
            .Where(command => command.DefaultVisibility == RuntimeSurfaceDefaultVisibility.ExplicitOnly)
            .Select(command => command.Name)
            .ToArray();

    public static IReadOnlyList<string> CompatibilityOnlyCommandNames { get; } =
        CommandMetadata
            .Where(command => command.DefaultVisibility == RuntimeSurfaceDefaultVisibility.CompatibilityOnly)
            .Select(command => command.Name)
            .ToArray();

    public static IReadOnlyList<RuntimeSurfaceCommandMetadata> PrimaryCommandMetadata { get; } =
        CommandMetadata
            .Where(command => command.SurfaceRole == RuntimeSurfaceRole.Primary)
            .ToArray();

    public static IReadOnlyList<string> PrimaryCommandNames { get; } =
        PrimaryCommandMetadata
            .Select(command => command.Name)
            .ToArray();

    public static IReadOnlyList<RuntimeSurfaceCommandMetadata> CompatibilityAliasCommandMetadata { get; } =
        CommandMetadata
            .Where(command => command.SurfaceRole == RuntimeSurfaceRole.CompatibilityAlias)
            .ToArray();

    public static IReadOnlyList<string> CompatibilityAliasCommandNames { get; } =
        CompatibilityAliasCommandMetadata
            .Select(command => command.Name)
            .ToArray();

    public static IReadOnlyList<string> BuildInspectCommands(RuntimeSurfaceContextTier contextTier)
    {
        return CommandMetadata
            .Where(command => command.ContextTier == contextTier)
            .Select(command => command.InspectUsage)
            .ToArray();
    }

    public static IReadOnlyList<string> BuildApiCommands(RuntimeSurfaceContextTier contextTier)
    {
        return CommandMetadata
            .Where(command => command.ContextTier == contextTier)
            .Select(command => command.ApiUsage)
            .ToArray();
    }

    public static bool TryGetContextTier(string name, out RuntimeSurfaceContextTier contextTier)
    {
        if (Index.TryGetValue(name, out var descriptor))
        {
            contextTier = descriptor.ContextTier;
            return true;
        }

        contextTier = default;
        return false;
    }

    public static IReadOnlyList<string> BuildHelpLines(string verb)
    {
        if (DefaultVisibleCommandMetadata.Count > MaxDefaultVisibleSurfaceCount)
        {
            throw new InvalidOperationException(
                $"Default-visible Runtime surface count {DefaultVisibleCommandMetadata.Count} exceeds budget {MaxDefaultVisibleSurfaceCount}.");
        }

        var lines = new List<string>();
        var defaultVisibleNames = DefaultVisibleCommandNames.ToHashSet(StringComparer.Ordinal);
        var noArgumentCommands = Commands
            .Where(command => defaultVisibleNames.Contains(command.Name)
                              && command.ParameterKind == RuntimeSurfaceCommandParameterKind.None)
            .Select(command => command.Name)
            .ToArray();

        foreach (var chunk in noArgumentCommands.Chunk(12))
        {
            lines.Add($"{verb} <{string.Join('|', chunk)}>");
        }

        foreach (var command in Commands.Where(command => defaultVisibleNames.Contains(command.Name)
                                                          && command.ParameterKind != RuntimeSurfaceCommandParameterKind.None))
        {
            lines.Add($"{verb} {command.BuildUsageTail()}");
        }

        lines.Add($"{verb} <explicit-runtime-surface> # explicit-only and audit-only surfaces stay callable but are omitted from default help; run `{verb} all-surfaces` for full compatibility usage.");
        return lines;
    }

    public static bool TryDispatchInspect(OperatorSurfaceService service, IReadOnlyList<string> arguments, out OperatorCommandResult result)
    {
        return TryDispatch(service, arguments, "inspect", static descriptor => descriptor.InspectHandler, out result);
    }

    public static bool TryDispatchApi(OperatorSurfaceService service, IReadOnlyList<string> arguments, out OperatorCommandResult result)
    {
        return TryDispatch(service, arguments, "api", static descriptor => descriptor.ApiHandler, out result);
    }

    private static bool TryDispatch(
        OperatorSurfaceService service,
        IReadOnlyList<string> arguments,
        string verb,
        Func<RuntimeSurfaceCommandDescriptor, Func<OperatorSurfaceService, string?, OperatorCommandResult>> selector,
        out OperatorCommandResult result)
    {
        result = default!;
        if (arguments.Count == 0 || !Index.TryGetValue(arguments[0], out var descriptor))
        {
            return false;
        }

        var argument = arguments.Count >= 2 ? arguments[1] : null;
        if (descriptor.ParameterKind == RuntimeSurfaceCommandParameterKind.Required
            && string.IsNullOrWhiteSpace(argument))
        {
            result = OperatorCommandResult.Failure($"Usage: {verb} {descriptor.BuildUsageTail()}");
            return true;
        }

        result = selector(descriptor)(service, argument);
        return true;
    }

    private static RuntimeSurfaceCommandDescriptor NoArg(
        string name,
        Func<OperatorSurfaceService, OperatorCommandResult> inspectHandler,
        Func<OperatorSurfaceService, OperatorCommandResult> apiHandler,
        RuntimeSurfaceContextTier? contextTier = null,
        RuntimeSurfaceDefaultVisibility? defaultVisibility = null,
        RuntimeSurfaceRole surfaceRole = RuntimeSurfaceRole.Primary,
        string? successorSurfaceId = null,
        RuntimeSurfaceRetirementPosture? retirementPosture = null)
    {
        var resolvedContextTier = contextTier ?? InferContextTier(name, RuntimeSurfaceCommandParameterKind.None);
        return new RuntimeSurfaceCommandDescriptor(
            name,
            RuntimeSurfaceCommandParameterKind.None,
            null,
            resolvedContextTier,
            defaultVisibility ?? InferDefaultVisibility(resolvedContextTier),
            surfaceRole,
            successorSurfaceId,
            retirementPosture ?? InferRetirementPosture(surfaceRole),
            (service, _) => inspectHandler(service),
            (service, _) => apiHandler(service));
    }

    private static RuntimeSurfaceCommandDescriptor OptionalArg(
        string name,
        string placeholder,
        Func<OperatorSurfaceService, string?, OperatorCommandResult> inspectHandler,
        Func<OperatorSurfaceService, string?, OperatorCommandResult> apiHandler,
        RuntimeSurfaceContextTier? contextTier = null,
        RuntimeSurfaceDefaultVisibility? defaultVisibility = null,
        RuntimeSurfaceRole surfaceRole = RuntimeSurfaceRole.Primary,
        string? successorSurfaceId = null,
        RuntimeSurfaceRetirementPosture? retirementPosture = null)
    {
        var resolvedContextTier = contextTier ?? InferContextTier(name, RuntimeSurfaceCommandParameterKind.Optional);
        return new RuntimeSurfaceCommandDescriptor(
            name,
            RuntimeSurfaceCommandParameterKind.Optional,
            placeholder,
            resolvedContextTier,
            defaultVisibility ?? InferDefaultVisibility(resolvedContextTier),
            surfaceRole,
            successorSurfaceId,
            retirementPosture ?? InferRetirementPosture(surfaceRole),
            inspectHandler,
            apiHandler);
    }

    private static RuntimeSurfaceCommandDescriptor RequiredArg(
        string name,
        string placeholder,
        Func<OperatorSurfaceService, string?, OperatorCommandResult> inspectHandler,
        Func<OperatorSurfaceService, string?, OperatorCommandResult> apiHandler,
        RuntimeSurfaceContextTier? contextTier = null,
        RuntimeSurfaceDefaultVisibility? defaultVisibility = null,
        RuntimeSurfaceRole surfaceRole = RuntimeSurfaceRole.Primary,
        string? successorSurfaceId = null,
        RuntimeSurfaceRetirementPosture? retirementPosture = null)
    {
        var resolvedContextTier = contextTier ?? InferContextTier(name, RuntimeSurfaceCommandParameterKind.Required);
        return new RuntimeSurfaceCommandDescriptor(
            name,
            RuntimeSurfaceCommandParameterKind.Required,
            placeholder,
            resolvedContextTier,
            defaultVisibility ?? InferDefaultVisibility(resolvedContextTier),
            surfaceRole,
            successorSurfaceId,
            retirementPosture ?? InferRetirementPosture(surfaceRole),
            inspectHandler,
            apiHandler);
    }

    private static RuntimeSurfaceRetirementPosture InferRetirementPosture(RuntimeSurfaceRole surfaceRole)
    {
        return surfaceRole == RuntimeSurfaceRole.CompatibilityAlias
            ? RuntimeSurfaceRetirementPosture.AliasRetained
            : RuntimeSurfaceRetirementPosture.ActivePrimary;
    }

    private static RuntimeSurfaceContextTier InferContextTier(string name, RuntimeSurfaceCommandParameterKind parameterKind)
    {
        if (IsAuditOnlySurface(name))
        {
            return RuntimeSurfaceContextTier.AuditOnly;
        }

        if (parameterKind != RuntimeSurfaceCommandParameterKind.None)
        {
            return RuntimeSurfaceContextTier.TaskScoped;
        }

        return RuntimeSurfaceContextTier.OperatorExpansion;
    }

    private static RuntimeSurfaceDefaultVisibility InferDefaultVisibility(RuntimeSurfaceContextTier contextTier)
    {
        return contextTier == RuntimeSurfaceContextTier.StartupSafe
            ? RuntimeSurfaceDefaultVisibility.DefaultVisible
            : RuntimeSurfaceDefaultVisibility.ExplicitOnly;
    }

    private static bool IsAuditOnlySurface(string name)
    {
        return name.Contains("audit", StringComparison.OrdinalIgnoreCase)
            || name.Contains("hotspot", StringComparison.OrdinalIgnoreCase)
            || name.Contains("backlog", StringComparison.OrdinalIgnoreCase)
            || name.Contains("history", StringComparison.OrdinalIgnoreCase)
            || name.Contains("reaudit", StringComparison.OrdinalIgnoreCase);
    }
}

public enum RuntimeSurfaceContextTier
{
    StartupSafe,
    TaskScoped,
    OperatorExpansion,
    AuditOnly,
}

public enum RuntimeSurfaceDefaultVisibility
{
    DefaultVisible,
    ExplicitOnly,
    CompatibilityOnly,
}

public enum RuntimeSurfaceRole
{
    Primary,
    CompatibilityAlias,
}

public enum RuntimeSurfaceRetirementPosture
{
    ActivePrimary,
    AliasRetained,
}

public sealed record RuntimeSurfaceCommandMetadata(
    string Name,
    RuntimeSurfaceContextTier ContextTier,
    RuntimeSurfaceDefaultVisibility DefaultVisibility,
    string InspectUsage,
    string ApiUsage,
    RuntimeSurfaceRole SurfaceRole,
    string? SuccessorSurfaceId,
    RuntimeSurfaceRetirementPosture RetirementPosture);

internal enum RuntimeSurfaceCommandParameterKind
{
    None,
    Optional,
    Required,
}

internal sealed record RuntimeSurfaceCommandDescriptor(
    string Name,
    RuntimeSurfaceCommandParameterKind ParameterKind,
    string? ParameterPlaceholder,
    RuntimeSurfaceContextTier ContextTier,
    RuntimeSurfaceDefaultVisibility DefaultVisibility,
    RuntimeSurfaceRole SurfaceRole,
    string? SuccessorSurfaceId,
    RuntimeSurfaceRetirementPosture RetirementPosture,
    Func<OperatorSurfaceService, string?, OperatorCommandResult> InspectHandler,
    Func<OperatorSurfaceService, string?, OperatorCommandResult> ApiHandler)
{
    public string BuildUsageTail()
    {
        return ParameterKind switch
        {
            RuntimeSurfaceCommandParameterKind.None => Name,
            RuntimeSurfaceCommandParameterKind.Optional => $"{Name} [<{ParameterPlaceholder}>]",
            RuntimeSurfaceCommandParameterKind.Required => $"{Name} <{ParameterPlaceholder}>",
            _ => Name,
        };
    }
}
