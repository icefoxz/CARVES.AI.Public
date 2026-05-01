using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonCardDraftRepository : ICardDraftRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string root;
    private readonly IControlPlaneLockService lockService;

    public JsonCardDraftRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        root = paths.PlanningCardDraftsRoot;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public IReadOnlyList<CardDraftRecord> List()
    {
        if (!Directory.Exists(root))
        {
            return Array.Empty<CardDraftRecord>();
        }

        return Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<CardDraftRecord>(File.ReadAllText(path), JsonOptions))
            .OfType<CardDraftRecord>()
            .OrderBy(item => item.CardId, StringComparer.Ordinal)
            .ToArray();
    }

    public CardDraftRecord? TryGet(string draftIdOrCardId)
    {
        return List().FirstOrDefault(item =>
            string.Equals(item.DraftId, draftIdOrCardId, StringComparison.Ordinal)
            || string.Equals(item.CardId, draftIdOrCardId, StringComparison.Ordinal));
    }

    public void Save(CardDraftRecord record)
    {
        using var _ = lockService.Acquire("card-drafts");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{record.CardId}.json");
        AtomicFileWriter.WriteAllText(path, JsonSerializer.Serialize(record, JsonOptions));
    }
}
