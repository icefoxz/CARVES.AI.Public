namespace Carves.Runtime.Application.Interaction;

public sealed record IntentDiscoveryStatus(
    IntentDiscoveryState State,
    string AcceptedIntentPath,
    bool AcceptedIntentExists,
    string AcceptedIntentPreview,
    IntentDiscoveryDraft? Draft,
    bool AcceptanceRequired,
    string RecommendedNextAction,
    string Rationale);
