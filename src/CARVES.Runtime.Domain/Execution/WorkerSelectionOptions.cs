namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerSelectionOptions
{
    public bool IgnoreActiveRoutingProfile { get; init; }

    public bool ForceFallbackOnly { get; init; }

    public string? RequestedBackendOverride { get; init; }

    public string? RoutingIntentOverride { get; init; }

    public string? RoutingModuleIdOverride { get; init; }
}
