namespace Carves.Runtime.Domain.Platform;

public sealed class ActorSessionSnapshot
{
    public IReadOnlyList<ActorSessionRecord> Entries { get; init; } = Array.Empty<ActorSessionRecord>();
}
