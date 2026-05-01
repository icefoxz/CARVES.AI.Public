using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonProviderRegistryRepository : IProviderRegistryRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonProviderRegistryRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public ProviderRegistry Load()
    {
        if (!File.Exists(paths.PlatformProviderRegistryFile))
        {
            return new ProviderRegistry();
        }

        var registry = DeserializeRegistry(File.ReadAllText(paths.PlatformProviderRegistryFile));
        var providerHealth = LoadHealthLookup();
        var items = registry.Items
            .Select(item => ApplyBackendHealth(ResolveProvider(item.ProviderId) ?? item, providerHealth))
            .OrderBy(item => item.ProviderId, StringComparer.Ordinal)
            .ToArray();
        return new ProviderRegistry
        {
            Items = items,
        };
    }

    public void Save(ProviderRegistry registry)
    {
        using var _ = lockService.Acquire("platform-provider-registry");
        Directory.CreateDirectory(paths.PlatformProvidersRoot);
        foreach (var item in registry.Items)
        {
            AtomicFileWriter.WriteAllTextIfChanged(Path.Combine(paths.PlatformProvidersRoot, $"{item.ProviderId}.json"), JsonSerializer.Serialize(ToDocument(item), JsonOptions));
        }

        AtomicFileWriter.WriteAllTextIfChanged(paths.PlatformProviderRegistryFile, JsonSerializer.Serialize(new ProviderRegistryDocument
        {
            Items = registry.Items
                .OrderBy(item => item.ProviderId, StringComparer.Ordinal)
                .Select(ToDocument)
                .ToArray(),
        }, JsonOptions));
    }

    private ProviderDescriptor? ResolveProvider(string providerId)
    {
        var path = Path.Combine(paths.PlatformProvidersRoot, $"{providerId}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return DeserializeProvider(File.ReadAllText(path));
    }

    private ProviderRegistry DeserializeRegistry(string payload)
    {
        if (payload.Contains("\"health\"", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Deserialize<ProviderRegistry>(payload, JsonOptions) ?? new ProviderRegistry();
        }

        var document = JsonSerializer.Deserialize<ProviderRegistryDocument>(payload, JsonOptions);
        if (document is not null && document.Items.Length > 0)
        {
            return new ProviderRegistry
            {
                Items = document.Items.Select(ToDomain).ToArray(),
            };
        }

        return JsonSerializer.Deserialize<ProviderRegistry>(payload, JsonOptions) ?? new ProviderRegistry();
    }

    private ProviderDescriptor? DeserializeProvider(string payload)
    {
        if (payload.Contains("\"health\"", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Deserialize<ProviderDescriptor>(payload, JsonOptions);
        }

        var document = JsonSerializer.Deserialize<ProviderDocument>(payload, JsonOptions);
        if (document is not null && !string.IsNullOrWhiteSpace(document.ProviderId))
        {
            return ToDomain(document);
        }

        return JsonSerializer.Deserialize<ProviderDescriptor>(payload, JsonOptions);
    }

    private IReadOnlyDictionary<string, WorkerBackendHealthSummary> LoadHealthLookup()
    {
        var healthPath = paths.PlatformProviderHealthFile;
        if (!File.Exists(healthPath))
        {
            var legacyHealthPath = Path.Combine(paths.PlatformProvidersRoot, "health.json");
            healthPath = File.Exists(legacyHealthPath)
                ? legacyHealthPath
                : string.Empty;
        }

        if (string.IsNullOrWhiteSpace(healthPath) || !File.Exists(healthPath))
        {
            return new Dictionary<string, WorkerBackendHealthSummary>(StringComparer.Ordinal);
        }

        var snapshot = JsonSerializer.Deserialize<ProviderHealthSnapshot>(File.ReadAllText(healthPath), JsonOptions) ?? new ProviderHealthSnapshot();
        return snapshot.Entries.ToDictionary(
            entry => entry.BackendId,
            entry => new WorkerBackendHealthSummary
            {
                State = entry.State,
                Summary = entry.Summary,
                LatencyMs = entry.LatencyMs,
                DegradationReason = entry.DegradationReason,
                ConsecutiveFailureCount = entry.ConsecutiveFailureCount,
                CheckedAt = entry.CheckedAt,
            },
            StringComparer.Ordinal);
    }

    private static ProviderDescriptor ApplyBackendHealth(
        ProviderDescriptor descriptor,
        IReadOnlyDictionary<string, WorkerBackendHealthSummary> providerHealth)
    {
        return new ProviderDescriptor
        {
            SchemaVersion = descriptor.SchemaVersion,
            ProviderId = descriptor.ProviderId,
            DisplayName = descriptor.DisplayName,
            SecretEnvironmentVariable = descriptor.SecretEnvironmentVariable,
            Capabilities = descriptor.Capabilities,
            Profiles = descriptor.Profiles,
            WorkerBackends = descriptor.WorkerBackends
                .Select(backend => new WorkerBackendDescriptor
                {
                    BackendId = backend.BackendId,
                    ProviderId = backend.ProviderId,
                    AdapterId = backend.AdapterId,
                    DisplayName = backend.DisplayName,
                    RoutingIdentity = backend.RoutingIdentity,
                    ProtocolFamily = backend.ProtocolFamily,
                    RequestFamily = backend.RequestFamily,
                    RoutingProfiles = backend.RoutingProfiles,
                    CompatibleTrustProfiles = backend.CompatibleTrustProfiles,
                    Capabilities = backend.Capabilities,
                    Health = providerHealth.TryGetValue(backend.BackendId, out var health)
                        ? health
                        : backend.Health,
                })
                .ToArray(),
            TimeoutSeconds = descriptor.TimeoutSeconds,
            RetryLimit = descriptor.RetryLimit,
            PermittedRepoScopes = descriptor.PermittedRepoScopes,
        };
    }

    private static ProviderDocument ToDocument(ProviderDescriptor descriptor)
    {
        return new ProviderDocument
        {
            SchemaVersion = descriptor.SchemaVersion,
            ProviderId = descriptor.ProviderId,
            DisplayName = descriptor.DisplayName,
            SecretEnvironmentVariable = descriptor.SecretEnvironmentVariable,
            Capabilities = descriptor.Capabilities,
            Profiles = descriptor.Profiles.ToArray(),
            WorkerBackends = descriptor.WorkerBackends
                .Select(backend => new WorkerBackendDocument
                {
                    BackendId = backend.BackendId,
                    ProviderId = backend.ProviderId,
                    AdapterId = backend.AdapterId,
                    DisplayName = backend.DisplayName,
                    RoutingIdentity = backend.RoutingIdentity,
                    ProtocolFamily = backend.ProtocolFamily,
                    RequestFamily = backend.RequestFamily,
                    RoutingProfiles = backend.RoutingProfiles.ToArray(),
                    CompatibleTrustProfiles = backend.CompatibleTrustProfiles.ToArray(),
                    Capabilities = backend.Capabilities,
                })
                .ToArray(),
            TimeoutSeconds = descriptor.TimeoutSeconds,
            RetryLimit = descriptor.RetryLimit,
            PermittedRepoScopes = descriptor.PermittedRepoScopes.ToArray(),
        };
    }

    private static ProviderDescriptor ToDomain(ProviderDocument document)
    {
        return new ProviderDescriptor
        {
            SchemaVersion = document.SchemaVersion,
            ProviderId = document.ProviderId,
            DisplayName = document.DisplayName,
            SecretEnvironmentVariable = document.SecretEnvironmentVariable,
            Capabilities = document.Capabilities,
            Profiles = document.Profiles,
            WorkerBackends = document.WorkerBackends
                .Select(backend => new WorkerBackendDescriptor
                {
                    BackendId = backend.BackendId,
                    ProviderId = backend.ProviderId,
                    AdapterId = backend.AdapterId,
                    DisplayName = backend.DisplayName,
                    RoutingIdentity = backend.RoutingIdentity,
                    ProtocolFamily = backend.ProtocolFamily,
                    RequestFamily = backend.RequestFamily,
                    RoutingProfiles = backend.RoutingProfiles,
                    CompatibleTrustProfiles = backend.CompatibleTrustProfiles,
                    Capabilities = backend.Capabilities,
                })
                .ToArray(),
            TimeoutSeconds = document.TimeoutSeconds,
            RetryLimit = document.RetryLimit,
            PermittedRepoScopes = document.PermittedRepoScopes,
        };
    }

    private sealed class ProviderRegistryDocument
    {
        public ProviderDocument[] Items { get; init; } = [];
    }

    private sealed class ProviderDocument
    {
        public int SchemaVersion { get; init; } = 1;

        public string ProviderId { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string SecretEnvironmentVariable { get; init; } = string.Empty;

        public AIProviderCapabilities Capabilities { get; init; } = new(false, false, false, false, false);

        public ProviderProfileBinding[] Profiles { get; init; } = [];

        public WorkerBackendDocument[] WorkerBackends { get; init; } = [];

        public int TimeoutSeconds { get; init; } = 30;

        public int RetryLimit { get; init; } = 1;

        public string[] PermittedRepoScopes { get; init; } = [];
    }

    private sealed class WorkerBackendDocument
    {
        public string BackendId { get; init; } = string.Empty;

        public string ProviderId { get; init; } = string.Empty;

        public string AdapterId { get; init; } = string.Empty;

        public string DisplayName { get; init; } = string.Empty;

        public string RoutingIdentity { get; init; } = string.Empty;

        public string ProtocolFamily { get; init; } = string.Empty;

        public string RequestFamily { get; init; } = string.Empty;

        public string[] RoutingProfiles { get; init; } = [];

        public string[] CompatibleTrustProfiles { get; init; } = [];

        public WorkerProviderCapabilities Capabilities { get; init; } = new();
    }
}
