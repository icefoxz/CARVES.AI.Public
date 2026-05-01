namespace Carves.Runtime.Domain.Platform;

public enum ProviderRoutingDenialReason
{
    None,
    ProviderProfileNotAllowed,
    RepoScopeForbidden,
    QuotaExhausted,
    NoMatchingRoleBinding,
}
