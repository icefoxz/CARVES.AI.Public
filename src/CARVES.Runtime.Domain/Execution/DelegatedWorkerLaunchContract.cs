namespace Carves.Runtime.Domain.Execution;

public sealed class DelegatedWorkerLaunchContract
{
    public int SchemaVersion { get; init; } = 1;

    public string TaskId { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string WorktreeRoot { get; init; } = string.Empty;

    public string RuntimeHomeRoot { get; init; } = string.Empty;

    public string DotNetCliHome { get; init; } = string.Empty;

    public string TempRoot { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
