namespace Carves.Runtime.Domain.Platform;

public sealed class OwnershipSnapshot
{
    public IReadOnlyList<OwnershipBinding> Bindings { get; init; } = Array.Empty<OwnershipBinding>();
}
