using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform;

public sealed class OperatorOsEventStreamService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly IOperatorOsEventRepository repository;
    private readonly ControlPlanePaths? paths;

    public OperatorOsEventStreamService(IOperatorOsEventRepository repository, ControlPlanePaths? paths = null)
    {
        this.repository = repository;
        this.paths = paths;
    }

    public IReadOnlyList<OperatorOsEventRecord> Load(
        string? taskId = null,
        string? actorSessionId = null,
        OperatorOsEventKind? eventKind = null)
    {
        return repository.Load().Entries
            .Where(record => string.IsNullOrWhiteSpace(taskId) || string.Equals(record.TaskId, taskId, StringComparison.Ordinal))
            .Where(record => string.IsNullOrWhiteSpace(actorSessionId) || string.Equals(record.ActorSessionId, actorSessionId, StringComparison.Ordinal))
            .Where(record => eventKind is null || record.EventKind == eventKind.Value)
            .OrderByDescending(record => record.OccurredAt)
            .ToArray();
    }

    public void Append(OperatorOsEventRecord record)
    {
        var normalized = Normalize(record);
        var snapshot = repository.Load();
        var records = snapshot.Entries.Select(Normalize).ToList();
        records.Add(normalized);
        repository.Save(new OperatorOsEventSnapshot
        {
            Entries = records,
        });
    }

    private OperatorOsEventRecord Normalize(OperatorOsEventRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Summary))
        {
            return record;
        }

        if (!string.IsNullOrWhiteSpace(record.DetailRef) && record.OriginalSummaryLength > 0)
        {
            return record;
        }

        var detailRef = record.DetailRef;
        if (paths is not null && record.Summary.Length > 320)
        {
            detailRef ??= PersistDetailArtifact(record.Summary);
        }

        var projection = PromptSafeArtifactProjectionFactory.ForOperatorEvent(record, detailRef);
        return new OperatorOsEventRecord
        {
            SchemaVersion = record.SchemaVersion,
            EventId = record.EventId,
            EventKind = record.EventKind,
            RepoId = record.RepoId,
            ActorSessionId = record.ActorSessionId,
            ActorKind = record.ActorKind,
            ActorIdentity = record.ActorIdentity,
            TaskId = record.TaskId,
            RunId = record.RunId,
            BackendId = record.BackendId,
            ProviderId = record.ProviderId,
            PermissionRequestId = record.PermissionRequestId,
            OwnershipScope = record.OwnershipScope,
            OwnershipTargetId = record.OwnershipTargetId,
            IncidentId = record.IncidentId,
            ReferenceId = record.ReferenceId,
            ReasonCode = record.ReasonCode,
            Summary = projection.Summary,
            DetailRef = projection.DetailRef,
            DetailHash = projection.DetailHash,
            ExcerptTail = projection.ExcerptTail,
            OriginalSummaryLength = projection.OriginalLength,
            SummaryTruncated = projection.Truncated,
            OccurredAt = record.OccurredAt,
        };
    }

    private string PersistDetailArtifact(string summary)
    {
        if (paths is null)
        {
            throw new InvalidOperationException("Operator OS event detail persistence requires control-plane paths.");
        }

        var detailHash = PromptSafeArtifactProjectionFactory.Hash(summary);
        var detailRoot = Path.Combine(paths.PlatformEventsRoot, "details");
        var detailPath = Path.Combine(detailRoot, $"{detailHash}.json");
        if (!File.Exists(detailPath))
        {
            Directory.CreateDirectory(detailRoot);
            var payload = new OperatorOsEventDetailArtifact
            {
                DetailHash = detailHash,
                DetailText = summary,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            File.WriteAllText(detailPath, JsonSerializer.Serialize(payload, JsonOptions));
        }

        return $".carves-platform/events/details/{detailHash}.json";
    }

    private sealed class OperatorOsEventDetailArtifact
    {
        public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

        public string DetailHash { get; init; } = string.Empty;

        public string DetailText { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    }
}
