using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonCurrentModelQualificationRepository : ICurrentModelQualificationRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonCurrentModelQualificationRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public ModelQualificationMatrix? LoadMatrix()
    {
        return Load<ModelQualificationMatrix>(paths.PlatformQualificationMatrixFile);
    }

    public void SaveMatrix(ModelQualificationMatrix matrix)
    {
        Save(paths.PlatformQualificationMatrixFile, "platform-qualification-matrix", matrix);
    }

    public ModelQualificationRunLedger? LoadLatestRun()
    {
        return Load<ModelQualificationRunLedger>(paths.PlatformQualificationRunLedgerFile);
    }

    public void SaveLatestRun(ModelQualificationRunLedger run)
    {
        Save(paths.PlatformQualificationRunLedgerFile, "platform-qualification-run", run);
    }

    public ModelQualificationCandidateProfile? LoadCandidate()
    {
        return Load<ModelQualificationCandidateProfile>(paths.PlatformCandidateRoutingProfileFile);
    }

    public void SaveCandidate(ModelQualificationCandidateProfile candidate)
    {
        Save(paths.PlatformCandidateRoutingProfileFile, "platform-candidate-routing-profile", candidate);
    }

    private T? Load<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
    }

    private void Save<T>(string path, string lockName, T value)
    {
        using var _ = lockService.Acquire(lockName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFileWriter.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }
}
