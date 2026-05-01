using System.Text.Json.Serialization;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Domain.Planning;

public sealed class TaskGraphDraftRecord
{
    public string DraftId { get; init; } = string.Empty;

    public string CardId { get; init; } = string.Empty;

    public PlanningDraftStatus Status { get; set; } = PlanningDraftStatus.Draft;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public string? ApprovalReason { get; set; }

    public bool MethodologyRequired { get; init; }

    public bool MethodologyAcknowledged { get; init; }

    public string? MethodologyReferencePath { get; init; }

    public string? MethodologyCoverageStatus { get; init; }

    public IReadOnlyList<string> MethodologyRelatedCards { get; init; } = Array.Empty<string>();

    public string? MethodologySummary { get; init; }

    public string? MethodologyRecommendedAction { get; init; }

    [JsonPropertyName("planning_lineage")]
    public PlanningLineage? PlanningLineage { get; init; }

    public IReadOnlyList<TaskGraphDraftTask> Tasks { get; init; } = Array.Empty<TaskGraphDraftTask>();
}
