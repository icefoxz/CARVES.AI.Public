using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class ExecutionPacketSurfaceSnapshot
{
    public string SchemaVersion { get; init; } = "execution-packet-surface.v1";

    public string SurfaceId { get; init; } = "execution-packet";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string TaskId { get; init; } = string.Empty;

    public string CardId { get; init; } = string.Empty;

    public PlannerIntent PlannerIntent { get; init; } = PlannerIntent.Execution;

    public string PacketPath { get; init; } = string.Empty;

    public bool Persisted { get; init; }

    public string RecoveryAuthority { get; init; } = "planner_only";

    public string WritebackAuthority { get; init; } = "planner_only";

    public string Summary { get; init; } = string.Empty;

    public ExecutionPacket Packet { get; init; } = new();
}
