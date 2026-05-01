namespace Carves.Runtime.Domain.Platform;

public sealed record WorkerNodeCapabilities(
    bool SupportsDotNetBuild,
    bool SupportsUnityBuild,
    bool SupportsLongRunningTests,
    bool SupportsContainerIsolation,
    int MaxConcurrentTasks,
    IReadOnlyList<string> AllowedRepoScopes,
    bool SupportsCodexSdk = false,
    bool SupportsTrustedAutomation = false,
    bool SupportsNetworkedAgents = false);
