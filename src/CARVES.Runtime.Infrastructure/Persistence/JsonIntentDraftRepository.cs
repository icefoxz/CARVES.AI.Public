using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Interaction;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonIntentDraftRepository : IIntentDraftRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string draftPath;
    private readonly IControlPlaneLockService lockService;

    public JsonIntentDraftRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        draftPath = Path.Combine(paths.RuntimeRoot, "intent_draft.json");
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public IntentDiscoveryDraft? Load()
    {
        return LoadFromDisk();
    }

    public void Save(IntentDiscoveryDraft draft)
    {
        using var _ = lockService.Acquire("intent-draft");
        Directory.CreateDirectory(Path.GetDirectoryName(draftPath)!);
        AtomicFileWriter.WriteAllText(draftPath, JsonSerializer.Serialize(draft, JsonOptions));
    }

    public IntentDiscoveryDraft Update(Func<IntentDiscoveryDraft, IntentDiscoveryDraft> update)
    {
        using var _ = lockService.Acquire("intent-draft");
        var current = LoadFromDisk() ?? throw new InvalidOperationException("No intent draft exists. Run `intent draft --persist` first.");
        var updated = update(current);
        Directory.CreateDirectory(Path.GetDirectoryName(draftPath)!);
        AtomicFileWriter.WriteAllText(draftPath, JsonSerializer.Serialize(updated, JsonOptions));
        return updated;
    }

    public void Delete()
    {
        using var _ = lockService.Acquire("intent-draft");
        if (File.Exists(draftPath))
        {
            File.Delete(draftPath);
        }
    }

    private IntentDiscoveryDraft? LoadFromDisk()
    {
        if (!File.Exists(draftPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<IntentDiscoveryDraft>(File.ReadAllText(draftPath), JsonOptions)
               ?? throw new InvalidOperationException($"Intent draft at '{draftPath}' could not be deserialized.");
    }
}
