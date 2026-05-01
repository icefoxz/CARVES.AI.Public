namespace Carves.Runtime.Application.Interaction;

public sealed record InteractionSnapshot(
    string ProtocolMode,
    ConversationProtocolStatus Protocol,
    IntentDiscoveryStatus Intent,
    PromptKernelDefinition PromptKernel,
    PromptTemplateDefinition ActiveTemplate,
    ProjectUnderstandingProjection ProjectUnderstanding,
    string RecommendedNextAction);
