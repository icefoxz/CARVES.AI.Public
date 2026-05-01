using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class ProviderRoutingService
{
    private readonly ProviderRegistryService providerRegistryService;
    private readonly RepoRegistryService repoRegistryService;
    private readonly PlatformGovernanceService governanceService;
    private readonly IProviderQuotaRepository quotaRepository;

    public ProviderRoutingService(
        ProviderRegistryService providerRegistryService,
        RepoRegistryService repoRegistryService,
        PlatformGovernanceService governanceService,
        IProviderQuotaRepository quotaRepository)
    {
        this.providerRegistryService = providerRegistryService;
        this.repoRegistryService = repoRegistryService;
        this.governanceService = governanceService;
        this.quotaRepository = quotaRepository;
    }

    public ProviderQuotaSnapshot GetQuotaSnapshot()
    {
        var snapshot = quotaRepository.Load();
        var platformPolicy = governanceService.GetSnapshot().PlatformPolicy;
        var reconciled = ReconcileQuotaSnapshot(snapshot, platformPolicy.ProviderQuotaPerHour);
        if (reconciled.Modified)
        {
            quotaRepository.Save(reconciled.Snapshot);
        }

        return reconciled.Snapshot;
    }

    public ProviderRoutingDecision Route(string repoId, string role, bool allowFallback, bool reserve = false)
    {
        var descriptor = repoRegistryService.Inspect(repoId);
        var repoPolicy = governanceService.ResolveRepoPolicy(descriptor.PolicyProfile);
        var providerPolicy = governanceService.ResolveProviderPolicy(repoPolicy.ProviderPolicyProfile);
        var providers = providerRegistryService.List();
        var quotaSnapshot = GetQuotaSnapshot();
        var candidates = BuildCandidates(descriptor, role, allowFallback, providerPolicy, providers);

        ProviderRoutingDenialReason lastDenial = ProviderRoutingDenialReason.NoMatchingRoleBinding;
        foreach (var candidate in candidates)
        {
            if (!providerPolicy.AllowedRepoScopes.Any(scope => scope == "*" || string.Equals(scope, repoId, StringComparison.Ordinal)))
            {
                lastDenial = ProviderRoutingDenialReason.RepoScopeForbidden;
                continue;
            }

            var quotaEntry = quotaSnapshot.Entries.First(entry => string.Equals(entry.ProfileId, candidate.ProfileId, StringComparison.Ordinal));
            if (quotaEntry.Exhausted)
            {
                lastDenial = ProviderRoutingDenialReason.QuotaExhausted;
                continue;
            }

            if (reserve)
            {
                quotaEntry.UsedThisHour += 1;
                quotaRepository.Save(quotaSnapshot);
            }

            return new ProviderRoutingDecision(
                repoId,
                role,
                true,
                !string.Equals(candidate.ProfileId, descriptor.ProviderProfile, StringComparison.Ordinal),
                candidate.ProviderId,
                candidate.ProfileId,
                ProviderRoutingDenialReason.None,
                string.Equals(candidate.ProfileId, descriptor.ProviderProfile, StringComparison.Ordinal)
                    ? $"Selected bound provider profile '{candidate.ProfileId}'."
                    : $"Selected fallback provider profile '{candidate.ProfileId}' for role '{role}'.",
                quotaEntry);
        }

        return new ProviderRoutingDecision(
            repoId,
            role,
            false,
            false,
            null,
            null,
            lastDenial,
            lastDenial switch
            {
                ProviderRoutingDenialReason.ProviderProfileNotAllowed => $"Provider policy '{providerPolicy.PolicyId}' forbids the requested binding.",
                ProviderRoutingDenialReason.RepoScopeForbidden => $"Provider policy '{providerPolicy.PolicyId}' forbids repo '{repoId}'.",
                ProviderRoutingDenialReason.QuotaExhausted => $"Provider quota is exhausted for role '{role}'.",
                _ => $"No provider profile is available for role '{role}'.",
            },
            null);
    }

    private static IReadOnlyList<(string ProviderId, string ProfileId)> BuildCandidates(
        RepoDescriptor descriptor,
        string role,
        bool allowFallback,
        ProviderPolicy providerPolicy,
        IReadOnlyList<ProviderDescriptor> providers)
    {
        var orderedProfiles = new List<string>();
        if (!string.IsNullOrWhiteSpace(descriptor.ProviderProfile))
        {
            orderedProfiles.Add(descriptor.ProviderProfile);
        }

        if (allowFallback && providerPolicy.AllowFallbackProfiles)
        {
            orderedProfiles.AddRange(providerPolicy.FallbackProviderProfiles);
        }

        orderedProfiles.AddRange(providerPolicy.AllowedProviderProfiles);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var matches = new List<(string ProviderId, string ProfileId)>();
        foreach (var profileId in orderedProfiles)
        {
            if (!seen.Add(profileId))
            {
                continue;
            }

            var match = providers
                .Select(provider => (provider.ProviderId, Profile: provider.Profiles.FirstOrDefault(binding =>
                    string.Equals(binding.ProfileId, profileId, StringComparison.Ordinal)
                    && string.Equals(binding.Role, role, StringComparison.OrdinalIgnoreCase))))
                .FirstOrDefault(item => item.Profile is not null);

            if (match.Profile is null)
            {
                continue;
            }

            if (!providerPolicy.AllowedProviderProfiles.Any(allowed => string.Equals(allowed, profileId, StringComparison.Ordinal)))
            {
                continue;
            }

            matches.Add((match.ProviderId, profileId));
        }

        return matches;
    }

    private ProviderQuotaReconciliationResult ReconcileQuotaSnapshot(ProviderQuotaSnapshot snapshot, int defaultLimitPerHour)
    {
        var currentProfiles = providerRegistryService.List()
            .SelectMany(provider => provider.Profiles)
            .Select(profile => profile.ProfileId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(profileId => profileId, StringComparer.Ordinal)
            .ToArray();

        var existingEntries = snapshot.Entries
            .GroupBy(entry => entry.ProfileId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        var reconciledEntries = new List<ProviderQuotaEntry>(existingEntries.Count + currentProfiles.Length);
        var modified = snapshot.Entries.Count == 0;

        foreach (var profileId in currentProfiles)
        {
            if (existingEntries.TryGetValue(profileId, out var existing))
            {
                if (existing.LimitPerHour != defaultLimitPerHour)
                {
                    modified = true;
                    reconciledEntries.Add(new ProviderQuotaEntry
                    {
                        ProfileId = existing.ProfileId,
                        UsedThisHour = existing.UsedThisHour,
                        LimitPerHour = defaultLimitPerHour,
                        WindowStartedAt = existing.WindowStartedAt,
                    });
                }
                else
                {
                    reconciledEntries.Add(existing);
                }

                continue;
            }

            modified = true;
            reconciledEntries.Add(new ProviderQuotaEntry
            {
                ProfileId = profileId,
                LimitPerHour = defaultLimitPerHour,
                WindowStartedAt = DateTimeOffset.UtcNow,
            });
        }

        foreach (var existing in existingEntries.Values.OrderBy(entry => entry.ProfileId, StringComparer.Ordinal))
        {
            if (reconciledEntries.Any(entry => string.Equals(entry.ProfileId, existing.ProfileId, StringComparison.Ordinal)))
            {
                continue;
            }

            reconciledEntries.Add(existing);
        }

        return new ProviderQuotaReconciliationResult(
            modified,
            new ProviderQuotaSnapshot
            {
                SchemaVersion = snapshot.SchemaVersion,
                Entries = reconciledEntries.ToArray(),
            });
    }

    private sealed record ProviderQuotaReconciliationResult(bool Modified, ProviderQuotaSnapshot Snapshot);
}
