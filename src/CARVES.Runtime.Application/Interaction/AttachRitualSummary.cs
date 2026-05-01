namespace Carves.Runtime.Application.Interaction;

public sealed record AttachRitualSummary(
    string RepoId,
    string RepoPath,
    string Stage,
    string AttachMode,
    string ReadyState,
    string ReadinessSummary,
    string ProtocolMode,
    ConversationProtocolStatus Protocol,
    IntentDiscoveryStatus Intent,
    PromptKernelDefinition PromptKernel,
    PromptTemplateDefinition ActiveTemplate,
    ProjectUnderstandingProjection ProjectUnderstanding,
    string RecommendedNextAction);
