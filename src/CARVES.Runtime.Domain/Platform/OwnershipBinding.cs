using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Platform;

public sealed class OwnershipBinding
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string BindingId { get; init; } = $"ownership-{Guid.NewGuid():N}";

    public OwnershipScope Scope { get; init; } = OwnershipScope.TaskMutation;

    public string TargetId { get; init; } = string.Empty;

    public string OwnerActorSessionId { get; init; } = string.Empty;

    public ActorSessionKind OwnerKind { get; init; } = ActorSessionKind.Operator;

    public string OwnerIdentity { get; init; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset ClaimedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Touch(string reason)
    {
        Reason = reason;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
