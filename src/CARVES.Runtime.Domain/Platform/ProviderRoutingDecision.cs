namespace Carves.Runtime.Domain.Platform;

public sealed record ProviderRoutingDecision(
    string RepoId,
    string Role,
    bool Allowed,
    bool UsedFallback,
    string? ProviderId,
    string? ProfileId,
    ProviderRoutingDenialReason DenialReason,
    string Reason,
    ProviderQuotaEntry? QuotaEntry);
