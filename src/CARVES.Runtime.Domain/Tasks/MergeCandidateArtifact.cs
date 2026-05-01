using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Domain.Tasks;

public sealed class MergeCandidateArtifact
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string TaskId { get; init; } = string.Empty;

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ReviewReason { get; init; } = string.Empty;

    public string PlannerComment { get; init; } = string.Empty;

    public string ResultCommit { get; init; } = string.Empty;

    public string PatchSummary { get; init; } = string.Empty;

    public bool ValidationPassed { get; init; }

    public SafetyOutcome SafetyOutcome { get; init; } = SafetyOutcome.Allow;

    public ReviewWritebackRecord Writeback { get; init; } = new();
}
