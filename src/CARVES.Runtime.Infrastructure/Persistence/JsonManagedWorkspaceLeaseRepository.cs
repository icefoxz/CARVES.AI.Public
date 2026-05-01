using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonManagedWorkspaceLeaseRepository : IManagedWorkspaceLeaseRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonManagedWorkspaceLeaseRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public ManagedWorkspaceLeaseSnapshot Load()
    {
        if (!File.Exists(paths.RuntimeManagedWorkspaceLeaseStateFile))
        {
            return new ManagedWorkspaceLeaseSnapshot();
        }

        return JsonSerializer.Deserialize<ManagedWorkspaceLeaseSnapshot>(File.ReadAllText(paths.RuntimeManagedWorkspaceLeaseStateFile), JsonOptions)
            ?? new ManagedWorkspaceLeaseSnapshot();
    }

    public void Save(ManagedWorkspaceLeaseSnapshot snapshot)
    {
        using var _ = lockService.Acquire("runtime-managed-workspace-leases");
        Directory.CreateDirectory(Path.GetDirectoryName(paths.RuntimeManagedWorkspaceLeaseStateFile)!);
        AtomicFileWriter.WriteAllText(paths.RuntimeManagedWorkspaceLeaseStateFile, JsonSerializer.Serialize(snapshot, JsonOptions));
    }
}
