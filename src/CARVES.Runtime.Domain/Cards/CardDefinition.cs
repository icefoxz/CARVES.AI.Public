namespace Carves.Runtime.Domain.Cards;

public sealed record CardDefinition(
    string CardId,
    string Title,
    string Goal,
    string CardType,
    string Priority,
    IReadOnlyList<string> Scope,
    IReadOnlyList<string> Acceptance,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> Dependencies,
    Carves.Runtime.Domain.Planning.AcceptanceContract? AcceptanceContract = null);
