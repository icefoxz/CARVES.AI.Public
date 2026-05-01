using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Execution;

public sealed class ValidationResult
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public bool Passed { get; init; }

    public IReadOnlyList<IReadOnlyList<string>> Commands { get; init; } = Array.Empty<IReadOnlyList<string>>();

    public IReadOnlyList<CommandExecutionRecord> CommandResults { get; init; } = Array.Empty<CommandExecutionRecord>();

    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();
}
