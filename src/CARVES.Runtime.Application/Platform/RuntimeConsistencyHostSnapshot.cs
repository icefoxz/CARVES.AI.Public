namespace Carves.Runtime.Application.Platform;

public sealed record RuntimeConsistencyHostSnapshot(
    bool DescriptorExists,
    bool LiveHostRunning,
    string Message,
    string DescriptorPath,
    string? SnapshotPath,
    string? SnapshotState,
    string? SnapshotSummary);
