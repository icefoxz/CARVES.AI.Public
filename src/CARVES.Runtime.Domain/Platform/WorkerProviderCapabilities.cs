namespace Carves.Runtime.Domain.Platform;

public sealed class WorkerProviderCapabilities
{
    public bool SupportsExecution { get; init; }

    public bool SupportsEventStream { get; init; }

    public bool SupportsHealthProbe { get; init; }

    public bool SupportsCancellation { get; init; }

    public bool SupportsTrustedProfiles { get; init; }

    public bool SupportsNetworkAccess { get; init; }

    public bool SupportsDotNetBuild { get; init; }

    public bool SupportsLongRunningTasks { get; init; }

    public bool SupportsStreaming { get; init; }

    public bool SupportsToolCalls { get; init; }

    public bool SupportsJsonMode { get; init; }

    public bool SupportsSystemPrompt { get; init; }

    public bool SupportsFileUpload { get; init; }
}
