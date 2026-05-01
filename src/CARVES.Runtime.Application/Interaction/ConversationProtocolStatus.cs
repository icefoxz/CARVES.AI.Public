namespace Carves.Runtime.Application.Interaction;

public sealed record ConversationProtocolStatus(
    ConversationPhase CurrentPhase,
    IReadOnlyList<ConversationPhase> AllowedNextPhases,
    string RecommendedNextAction,
    string Rationale);
