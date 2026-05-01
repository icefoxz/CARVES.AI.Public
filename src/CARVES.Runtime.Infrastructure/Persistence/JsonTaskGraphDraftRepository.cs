using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonTaskGraphDraftRepository : ITaskGraphDraftRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string root;
    private readonly IControlPlaneLockService lockService;

    public JsonTaskGraphDraftRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        root = paths.PlanningTaskGraphDraftsRoot;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public IReadOnlyList<TaskGraphDraftRecord> List()
    {
        if (!Directory.Exists(root))
        {
            return Array.Empty<TaskGraphDraftRecord>();
        }

        return Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<TaskGraphDraftRecord>(File.ReadAllText(path), JsonOptions))
            .OfType<TaskGraphDraftRecord>()
            .OrderBy(item => item.DraftId, StringComparer.Ordinal)
            .ToArray();
    }

    public TaskGraphDraftRecord? TryGet(string draftId)
    {
        if (string.IsNullOrWhiteSpace(draftId))
        {
            return null;
        }

        var path = Path.Combine(root, $"{draftId}.json");
        return File.Exists(path)
            ? JsonSerializer.Deserialize<TaskGraphDraftRecord>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    public void Save(TaskGraphDraftRecord record)
    {
        using var _ = lockService.Acquire("taskgraph-drafts");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{record.DraftId}.json");
        AtomicFileWriter.WriteAllText(path, JsonSerializer.Serialize(record, JsonOptions));
    }
}
