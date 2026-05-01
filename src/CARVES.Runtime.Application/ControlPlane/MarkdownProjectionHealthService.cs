using System.Text.Json;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class MarkdownProjectionHealthService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private readonly ControlPlanePaths paths;

    public MarkdownProjectionHealthService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public string HealthFilePath => Path.Combine(paths.PlatformRuntimeStateRoot, "markdown_projection_health.json");

    public MarkdownProjectionHealthSnapshot Load()
    {
        if (!File.Exists(HealthFilePath))
        {
            return MarkdownProjectionHealthSnapshot.Healthy();
        }

        try
        {
            return JsonSerializer.Deserialize<MarkdownProjectionHealthSnapshot>(File.ReadAllText(HealthFilePath), JsonOptions)
                   ?? MarkdownProjectionHealthSnapshot.Degraded(
                       "Markdown projection health record is unreadable.",
                       failureCount: 1,
                       affectedTargets: [HealthFilePath],
                       updatedAt: DateTimeOffset.UtcNow,
                       lastFailureAt: DateTimeOffset.UtcNow,
                       lastFailure: "Health record deserialized to null.");
        }
        catch (Exception exception)
        {
            return MarkdownProjectionHealthSnapshot.Degraded(
                "Markdown projection health record is unreadable.",
                failureCount: 1,
                affectedTargets: [HealthFilePath],
                updatedAt: DateTimeOffset.UtcNow,
                lastFailureAt: DateTimeOffset.UtcNow,
                lastFailure: exception.Message);
        }
    }

    public void RecordSuccess()
    {
        var current = Load();
        if (string.Equals(current.State, "healthy", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        WriteSnapshot(new MarkdownProjectionHealthSnapshot
        {
            State = "healthy",
            Summary = "Markdown projection writeback is healthy.",
            ConsecutiveFailureCount = 0,
            AffectedTargets = Array.Empty<string>(),
            UpdatedAt = now,
            LastSuccessAt = now,
            LastFailureAt = current.LastFailureAt,
            LastFailure = current.LastFailure,
        });
    }

    public void RecordFailure(Exception exception, IReadOnlyList<string> affectedTargets)
    {
        var current = Load();
        var now = DateTimeOffset.UtcNow;
        var targets = affectedTargets
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var targetSummary = targets.Length == 0 ? "markdown projection targets" : string.Join(", ", targets);
        var failureCount = string.Equals(current.State, "degraded", StringComparison.OrdinalIgnoreCase)
            ? current.ConsecutiveFailureCount + 1
            : 1;
        WriteSnapshot(new MarkdownProjectionHealthSnapshot
        {
            State = "degraded",
            Summary = $"Markdown projection writeback degraded while updating {targetSummary}: {exception.Message}",
            ConsecutiveFailureCount = failureCount,
            AffectedTargets = targets,
            UpdatedAt = now,
            LastSuccessAt = current.LastSuccessAt,
            LastFailureAt = now,
            LastFailure = exception.Message,
        });
    }

    private void WriteSnapshot(MarkdownProjectionHealthSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HealthFilePath)!);
            File.WriteAllText(HealthFilePath, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch
        {
        }
    }
}

public sealed class MarkdownProjectionHealthSnapshot
{
    public string State { get; init; } = "healthy";

    public string Summary { get; init; } = "Markdown projection writeback is healthy.";

    public int ConsecutiveFailureCount { get; init; }

    public IReadOnlyList<string> AffectedTargets { get; init; } = Array.Empty<string>();

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastSuccessAt { get; init; }

    public DateTimeOffset? LastFailureAt { get; init; }

    public string? LastFailure { get; init; }

    public static MarkdownProjectionHealthSnapshot Healthy()
    {
        return new MarkdownProjectionHealthSnapshot
        {
            State = "healthy",
            Summary = "Markdown projection writeback is healthy.",
            ConsecutiveFailureCount = 0,
        };
    }

    public static MarkdownProjectionHealthSnapshot Degraded(
        string summary,
        int failureCount,
        IReadOnlyList<string> affectedTargets,
        DateTimeOffset updatedAt,
        DateTimeOffset? lastFailureAt,
        string? lastFailure)
    {
        return new MarkdownProjectionHealthSnapshot
        {
            State = "degraded",
            Summary = summary,
            ConsecutiveFailureCount = failureCount,
            AffectedTargets = affectedTargets,
            UpdatedAt = updatedAt,
            LastFailureAt = lastFailureAt,
            LastFailure = lastFailure,
        };
    }
}
