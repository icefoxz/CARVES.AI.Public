using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonRoutingValidationRepository : IRoutingValidationRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly string validationRoot;
    private readonly string tasksRoot;
    private readonly string tracesRoot;
    private readonly string summariesRoot;
    private readonly string catalogFile;
    private readonly string latestSummaryFile;
    private readonly IControlPlaneLockService lockService;

    public JsonRoutingValidationRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        validationRoot = Path.Combine(paths.AiRoot, "validation");
        tasksRoot = Path.Combine(validationRoot, "tasks");
        tracesRoot = Path.Combine(validationRoot, "traces");
        summariesRoot = Path.Combine(validationRoot, "summaries");
        catalogFile = Path.Combine(tasksRoot, "catalog.json");
        latestSummaryFile = Path.Combine(summariesRoot, "latest.json");
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public RoutingValidationCatalog? LoadCatalog() => Load<RoutingValidationCatalog>(catalogFile);

    public void SaveCatalog(RoutingValidationCatalog catalog) => Save(catalogFile, "routing-validation-catalog", catalog);

    public RoutingValidationTrace? LoadTrace(string traceId)
    {
        return Load<RoutingValidationTrace>(Path.Combine(tracesRoot, $"{traceId}.json"));
    }

    public IReadOnlyList<RoutingValidationTrace> LoadTraces(string? runId = null)
    {
        if (!Directory.Exists(tracesRoot))
        {
            return [];
        }

        return Directory.EnumerateFiles(tracesRoot, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => Load<RoutingValidationTrace>(path))
            .Where(trace => trace is not null)
            .Cast<RoutingValidationTrace>()
            .Where(trace => string.IsNullOrWhiteSpace(runId) || string.Equals(trace.RunId, runId, StringComparison.Ordinal))
            .OrderBy(trace => trace.EndedAt)
            .ToArray();
    }

    public void SaveTrace(RoutingValidationTrace trace)
    {
        Save(Path.Combine(tracesRoot, $"{trace.TraceId}.json"), $"routing-validation-trace-{trace.TraceId}", trace);
    }

    public RoutingValidationSummary? LoadSummary(string runId)
    {
        return Load<RoutingValidationSummary>(Path.Combine(summariesRoot, $"{runId}.json"));
    }

    public IReadOnlyList<RoutingValidationSummary> LoadSummaries(int? limit = null)
    {
        if (!Directory.Exists(summariesRoot))
        {
            return [];
        }

        var summaries = Directory.EnumerateFiles(summariesRoot, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), "latest.json", StringComparison.OrdinalIgnoreCase))
            .Select(path => Load<RoutingValidationSummary>(path))
            .Where(summary => summary is not null)
            .Cast<RoutingValidationSummary>()
            .OrderByDescending(summary => summary.GeneratedAt)
            .ToArray();

        if (limit is null || limit <= 0 || summaries.Length <= limit.Value)
        {
            return summaries;
        }

        return summaries.Take(limit.Value).ToArray();
    }

    public void SaveSummary(RoutingValidationSummary summary)
    {
        Save(Path.Combine(summariesRoot, $"{summary.RunId}.json"), $"routing-validation-summary-{summary.RunId}", summary);
    }

    public RoutingValidationSummary? LoadLatestSummary() => Load<RoutingValidationSummary>(latestSummaryFile);

    public void SaveLatestSummary(RoutingValidationSummary summary) => Save(latestSummaryFile, "routing-validation-summary", summary);

    private static T? Load<T>(string path)
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
