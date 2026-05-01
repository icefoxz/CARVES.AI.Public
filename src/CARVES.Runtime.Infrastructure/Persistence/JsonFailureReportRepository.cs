using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Domain.Failures;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonFailureReportRepository : IFailureReportRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonFailureReportRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public void Append(FailureReport report)
    {
        using var _ = lockService.Acquire($"failure:{report.Id}");
        Directory.CreateDirectory(paths.FailuresRoot);
        var outputPath = Path.Combine(paths.FailuresRoot, $"{report.Id}.json");
        if (File.Exists(outputPath))
        {
            throw new InvalidOperationException($"Failure report '{report.Id}' already exists.");
        }

        AtomicFileWriter.WriteAllText(outputPath, JsonSerializer.Serialize(report, JsonOptions));
    }

    public IReadOnlyList<FailureReport> LoadAll()
    {
        if (!Directory.Exists(paths.FailuresRoot))
        {
            return Array.Empty<FailureReport>();
        }

        return Directory.GetFiles(paths.FailuresRoot, "FAIL-*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => JsonSerializer.Deserialize<FailureReport>(File.ReadAllText(path), JsonOptions))
            .Where(item => item is not null)
            .Cast<FailureReport>()
            .ToArray();
    }
}
