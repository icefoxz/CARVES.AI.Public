using System.Collections.Concurrent;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Host;

internal sealed class HostAcceptedOperationStore
{
    private static readonly TimeSpan CompletedOperationRetention = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, HostAcceptedOperationEntry> operations = new(StringComparer.Ordinal);

    public HostAcceptedOperationStatusResponse Accept(string command, string? operationId = null)
    {
        PruneCompletedOperations();
        if (!string.IsNullOrWhiteSpace(operationId)
            && operations.TryGetValue(operationId, out var existing))
        {
            return ToStatus(existing);
        }

        var now = DateTimeOffset.UtcNow;
        operationId ??= $"hostop-{Guid.NewGuid():N}";
        var entry = new HostAcceptedOperationEntry(
            OperationId: operationId,
            Command: command,
            OperationState: HostAcceptedOperationProgressMarkers.Accepted,
            AcceptedAt: now,
            UpdatedAt: now,
            CompletedAt: null,
            ExitCode: null,
            Lines: Array.Empty<string>(),
            ProgressMarker: HostAcceptedOperationProgressMarkers.Accepted,
            ProgressOrdinal: HostAcceptedOperationProgressMarkers.ResolveOrdinal(HostAcceptedOperationProgressMarkers.Accepted),
            ProgressAt: now);
        operations[operationId] = entry;
        return ToStatus(entry);
    }

    public void MarkDispatching(string operationId)
    {
        AdvanceProgress(operationId, "accepted", HostAcceptedOperationProgressMarkers.Dispatching);
    }

    public void MarkRunning(string operationId)
    {
        AdvanceProgress(operationId, "running", HostAcceptedOperationProgressMarkers.Running);
    }

    public void MarkWriteback(string operationId)
    {
        AdvanceProgress(operationId, "running", HostAcceptedOperationProgressMarkers.Writeback);
    }

    public void RecordHeartbeat(string operationId)
    {
        Update(operationId, entry => entry.Completed
            ? entry
            : entry with
            {
                UpdatedAt = DateTimeOffset.UtcNow,
            });
    }

    public void Complete(string operationId, OperatorCommandResult result)
    {
        Update(operationId, entry =>
        {
            var now = DateTimeOffset.UtcNow;
            var progressMarker = result.ExitCode == 0
                ? HostAcceptedOperationProgressMarkers.Completed
                : HostAcceptedOperationProgressMarkers.Failed;
            return entry with
            {
                OperationState = result.ExitCode == 0 ? "completed" : "failed",
                UpdatedAt = now,
                CompletedAt = now,
                ExitCode = result.ExitCode,
                Lines = result.Lines,
                ProgressMarker = progressMarker,
                ProgressOrdinal = HostAcceptedOperationProgressMarkers.ResolveOrdinal(progressMarker),
                ProgressAt = now,
            };
        });
    }

    public void Fail(string operationId, string message)
    {
        Update(operationId, entry =>
        {
            var now = DateTimeOffset.UtcNow;
            return entry with
            {
                OperationState = "failed",
                UpdatedAt = now,
                CompletedAt = now,
                ExitCode = 1,
                Lines = [message],
                ProgressMarker = HostAcceptedOperationProgressMarkers.Failed,
                ProgressOrdinal = HostAcceptedOperationProgressMarkers.ResolveOrdinal(HostAcceptedOperationProgressMarkers.Failed),
                ProgressAt = now,
            };
        });
    }

    public HostAcceptedOperationStatusResponse? TryGet(string operationId)
    {
        return operations.TryGetValue(operationId, out var entry)
            ? ToStatus(entry)
            : null;
    }

    public IReadOnlyList<HostAcceptedOperationStatusResponse> ListRecent(int maxCount)
    {
        var count = Math.Max(0, maxCount);
        if (count == 0)
        {
            return Array.Empty<HostAcceptedOperationStatusResponse>();
        }

        return operations.Values
            .OrderByDescending(entry => entry.UpdatedAt)
            .Take(count)
            .Select(ToStatus)
            .ToArray();
    }

    private void Update(string operationId, Func<HostAcceptedOperationEntry, HostAcceptedOperationEntry> update)
    {
        operations.AddOrUpdate(
            operationId,
            _ => throw new InvalidOperationException($"Accepted host operation '{operationId}' was not found."),
            (_, existing) => update(existing));
    }

    private void AdvanceProgress(string operationId, string operationState, string progressMarker)
    {
        Update(operationId, entry =>
        {
            if (entry.Completed)
            {
                return entry;
            }

            var now = DateTimeOffset.UtcNow;
            var progressOrdinal = HostAcceptedOperationProgressMarkers.ResolveOrdinal(progressMarker);
            var markerChanged = !string.Equals(entry.ProgressMarker, progressMarker, StringComparison.OrdinalIgnoreCase);

            return entry with
            {
                OperationState = operationState,
                UpdatedAt = now,
                ProgressMarker = markerChanged ? progressMarker : entry.ProgressMarker,
                ProgressOrdinal = markerChanged ? progressOrdinal : entry.ProgressOrdinal,
                ProgressAt = markerChanged ? now : entry.ProgressAt,
            };
        });
    }

    private void PruneCompletedOperations()
    {
        var cutoff = DateTimeOffset.UtcNow - CompletedOperationRetention;
        foreach (var pair in operations)
        {
            if (pair.Value.CompletedAt is { } completedAt && completedAt < cutoff)
            {
                operations.TryRemove(pair.Key, out _);
            }
        }
    }

    private static HostAcceptedOperationStatusResponse ToStatus(HostAcceptedOperationEntry entry)
    {
        return new HostAcceptedOperationStatusResponse(
            entry.OperationId,
            entry.Command,
            entry.OperationState,
            entry.Completed,
            entry.ExitCode,
            entry.Lines,
            entry.AcceptedAt,
            entry.UpdatedAt,
            entry.CompletedAt,
            entry.ProgressMarker,
            entry.ProgressOrdinal,
            entry.ProgressAt);
    }

    private sealed record HostAcceptedOperationEntry(
        string OperationId,
        string Command,
        string OperationState,
        DateTimeOffset AcceptedAt,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? CompletedAt,
        int? ExitCode,
        IReadOnlyList<string> Lines,
        string ProgressMarker,
        int ProgressOrdinal,
        DateTimeOffset ProgressAt)
    {
        public bool Completed => CompletedAt is not null;
    }
}
