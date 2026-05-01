using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Interaction;

public sealed class InteractionLayerService
{
    private readonly IntentDiscoveryService intentDiscoveryService;
    private readonly ConversationProtocolService conversationProtocolService;
    private readonly PromptProtocolService promptProtocolService;
    private readonly PromptKernelService promptKernelService;
    private readonly ProjectUnderstandingProjectionService projectUnderstandingProjectionService;

    public InteractionLayerService(
        IntentDiscoveryService intentDiscoveryService,
        ConversationProtocolService conversationProtocolService,
        PromptProtocolService promptProtocolService,
        PromptKernelService promptKernelService,
        ProjectUnderstandingProjectionService projectUnderstandingProjectionService)
    {
        this.intentDiscoveryService = intentDiscoveryService;
        this.conversationProtocolService = conversationProtocolService;
        this.promptProtocolService = promptProtocolService;
        this.promptKernelService = promptKernelService;
        this.projectUnderstandingProjectionService = projectUnderstandingProjectionService;
    }

    public InteractionSnapshot GetSnapshot(RuntimeSessionState? session, bool hydrateProjectUnderstanding = false)
    {
        var intent = intentDiscoveryService.GetStatus();
        var protocol = conversationProtocolService.GetStatus(session);
        var kernel = promptKernelService.GetKernel();
        var template = promptProtocolService.ResolveForPhase(protocol.CurrentPhase);
        var projectUnderstanding = projectUnderstandingProjectionService.Evaluate(hydrateProjectUnderstanding);
        var recommendedNextAction = intent.State is IntentDiscoveryState.Missing or IntentDiscoveryState.Drafted
            ? intent.RecommendedNextAction
            : protocol.RecommendedNextAction;

        return new InteractionSnapshot(
            "carves_development",
            protocol,
            intent,
            kernel,
            template,
            projectUnderstanding,
            recommendedNextAction);
    }

    public AttachRitualSummary BuildAttachSummary(
        string repoId,
        string repoPath,
        string attachMode,
        string readyState,
        string readinessSummary)
    {
        var snapshot = GetSnapshot(session: null, hydrateProjectUnderstanding: true);
        return new AttachRitualSummary(
            repoId,
            repoPath,
            RuntimeStageInfo.CurrentStage,
            attachMode,
            readyState,
            readinessSummary,
            snapshot.ProtocolMode,
            snapshot.Protocol,
            snapshot.Intent,
            snapshot.PromptKernel,
            snapshot.ActiveTemplate,
            snapshot.ProjectUnderstanding,
            snapshot.RecommendedNextAction);
    }
}
