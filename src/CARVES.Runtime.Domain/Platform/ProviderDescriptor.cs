namespace Carves.Runtime.Domain.Platform;

public sealed class ProviderDescriptor
{
    public int SchemaVersion { get; init; } = 1;

    public string ProviderId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string SecretEnvironmentVariable { get; init; } = string.Empty;

    public AIProviderCapabilities Capabilities { get; init; } = new(false, false, false, false, false);

    public IReadOnlyList<ProviderProfileBinding> Profiles { get; init; } = Array.Empty<ProviderProfileBinding>();

    public IReadOnlyList<WorkerBackendDescriptor> WorkerBackends { get; init; } = Array.Empty<WorkerBackendDescriptor>();

    public int TimeoutSeconds { get; init; } = 30;

    public int RetryLimit { get; init; } = 1;

    public IReadOnlyList<string> PermittedRepoScopes { get; init; } = Array.Empty<string>();
}
