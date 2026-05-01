using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonOpportunityRepository : IOpportunityRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonOpportunityRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public OpportunitySnapshot Load()
    {
        if (!File.Exists(paths.OpportunitiesFile))
        {
            return new OpportunitySnapshot();
        }

        var snapshot = JsonSerializer.Deserialize<OpportunitySnapshot>(File.ReadAllText(paths.OpportunitiesFile), JsonOptions);
        return snapshot ?? new OpportunitySnapshot();
    }

    public void Save(OpportunitySnapshot snapshot)
    {
        using var _ = lockService.Acquire("opportunities");
        Directory.CreateDirectory(paths.OpportunitiesRoot);
        AtomicFileWriter.WriteAllText(paths.OpportunitiesFile, JsonSerializer.Serialize(snapshot, JsonOptions));
    }
}
