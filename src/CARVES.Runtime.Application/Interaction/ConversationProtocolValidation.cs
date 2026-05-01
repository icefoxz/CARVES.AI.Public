namespace Carves.Runtime.Application.Interaction;

public sealed record ConversationProtocolValidation(
    bool Allowed,
    ConversationPhase CurrentPhase,
    ConversationPhase RequestedPhase,
    string Message,
    string RecommendedNextAction);
