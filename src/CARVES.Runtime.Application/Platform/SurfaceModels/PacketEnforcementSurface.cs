using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class PacketEnforcementSurfaceSnapshot
{
    public string SchemaVersion { get; init; } = "packet-enforcement-surface.v1";

    public string SurfaceId { get; init; } = "packet-enforcement";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string TaskId { get; init; } = string.Empty;

    public string CardId { get; init; } = string.Empty;

    public string PacketPath { get; init; } = string.Empty;

    public string EnforcementPath { get; init; } = string.Empty;

    public bool Persisted { get; init; }

    public string Summary { get; init; } = string.Empty;

    public PacketEnforcementRecord Record { get; init; } = new();
}
