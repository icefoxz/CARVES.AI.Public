using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonDelegatedRunRecoveryLedgerRepository : IDelegatedRunRecoveryLedgerRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonDelegatedRunRecoveryLedgerRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public DelegatedRunRecoveryLedgerSnapshot Load()
    {
        var path = File.Exists(paths.PlatformDelegatedRunRecoveryLedgerLiveStateFile)
            ? paths.PlatformDelegatedRunRecoveryLedgerLiveStateFile
            : paths.PlatformDelegatedRunRecoveryLedgerFile;

        if (!File.Exists(path))
        {
            return new DelegatedRunRecoveryLedgerSnapshot();
        }

        return JsonSerializer.Deserialize<DelegatedRunRecoveryLedgerSnapshot>(File.ReadAllText(path), JsonOptions)
            ?? new DelegatedRunRecoveryLedgerSnapshot();
    }

    public void Save(DelegatedRunRecoveryLedgerSnapshot snapshot)
    {
        using var _ = lockService.Acquire("platform-delegated-run-recovery-ledger");
        Directory.CreateDirectory(paths.PlatformDelegationLiveStateRoot);
        AtomicFileWriter.WriteAllText(paths.PlatformDelegatedRunRecoveryLedgerLiveStateFile, JsonSerializer.Serialize(snapshot, JsonOptions));
    }
}
