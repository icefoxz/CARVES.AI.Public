using Carves.Runtime.Domain.Memory;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Domain.Execution;

public sealed class WorkerRequest
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public TaskNode Task { get; init; } = new();

    public ExecutionSession Session { get; init; } = new(string.Empty, string.Empty, string.Empty, false, string.Empty, string.Empty, string.Empty, DateTimeOffset.UtcNow);

    public MemoryBundle Memory { get; init; } = new();

    public AiExecutionRequest? AiRequest { get; init; }

    public WorkerExecutionRequest? ExecutionRequest { get; init; }

    public ExecutionPacket? Packet { get; init; }

    public IReadOnlyList<IReadOnlyList<string>> ValidationCommands { get; init; } = Array.Empty<IReadOnlyList<string>>();

    public WorkerSelectionDecision? Selection { get; init; }
}
