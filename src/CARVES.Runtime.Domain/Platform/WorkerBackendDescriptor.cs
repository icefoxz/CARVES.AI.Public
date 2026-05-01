namespace Carves.Runtime.Domain.Platform;

public sealed class WorkerBackendDescriptor
{
    public string BackendId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string AdapterId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string RoutingIdentity { get; init; } = string.Empty;

    public string ProtocolFamily { get; init; } = string.Empty;

    public string RequestFamily { get; init; } = string.Empty;

    public IReadOnlyList<string> RoutingProfiles { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CompatibleTrustProfiles { get; init; } = Array.Empty<string>();

    public WorkerProviderCapabilities Capabilities { get; init; } = new();

    public WorkerBackendHealthSummary Health { get; init; } = new();
}
