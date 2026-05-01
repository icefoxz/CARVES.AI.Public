namespace Carves.Runtime.Domain.Platform;

public sealed class OperatorOsEventSnapshot
{
    public IReadOnlyList<OperatorOsEventRecord> Entries { get; init; } = Array.Empty<OperatorOsEventRecord>();
}
