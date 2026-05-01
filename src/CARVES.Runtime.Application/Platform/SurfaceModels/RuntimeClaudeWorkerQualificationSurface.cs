using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeClaudeWorkerQualificationSurface
{
    public string SchemaVersion { get; init; } = "runtime-claude-worker-qualification.v1";

    public string SurfaceId { get; init; } = "runtime-claude-worker-qualification";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;

    public RuntimeClaudeWorkerQualificationPolicy CurrentPolicy { get; init; } = RuntimeClaudeWorkerQualificationPolicy.CreateDefault();

    public string[] Notes { get; init; } = [];
}

public sealed record RuntimeClaudeWorkerQualificationPolicy
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string PolicyId { get; init; } = "claude-worker-bounded-v1";

    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProviderId { get; init; } = "claude";

    public string BackendId { get; init; } = "claude_api";

    public RuntimeClaudeWorkerLaneQualification[] Lanes { get; init; } = [];

    public string[] ChecksPassed { get; init; } = [];

    public static RuntimeClaudeWorkerQualificationPolicy CreateDefault()
    {
        return new RuntimeClaudeWorkerQualificationPolicy
        {
            PolicyId = "claude-worker-bounded-v1",
            Lanes =
            [
                new RuntimeClaudeWorkerLaneQualification
                {
                    LaneId = "claude-review-summary",
                    RoutingIntent = "review_summary",
                    Allowed = true,
                    Summary = "Claude is qualified for bounded review-summary tasks that return assessment-style output rather than materialized patches.",
                    Constraints =
                    [
                        "bounded review output only",
                        "no materialized patch/result submission"
                    ],
                },
                new RuntimeClaudeWorkerLaneQualification
                {
                    LaneId = "claude-failure-summary",
                    RoutingIntent = "failure_summary",
                    Allowed = true,
                    Summary = "Claude is qualified for failure-summary tasks that explain errors, retries, and recovery guidance.",
                    Constraints =
                    [
                        "failure explanation only",
                        "no taskgraph mutation"
                    ],
                },
                new RuntimeClaudeWorkerLaneQualification
                {
                    LaneId = "claude-reasoning-summary",
                    RoutingIntent = "reasoning_summary",
                    Allowed = true,
                    Summary = "Claude is qualified for compact reasoning-summary lanes that stay in assessment territory.",
                    Constraints =
                    [
                        "assessment-only output",
                        "no materialized patch/result submission"
                    ],
                },
                new RuntimeClaudeWorkerLaneQualification
                {
                    LaneId = "claude-structured-output",
                    RoutingIntent = "structured_output",
                    Allowed = true,
                    Summary = "Claude is qualified for structured-output lanes where bounded JSON/text output is the goal.",
                    Constraints =
                    [
                        "bounded structured output",
                        "no repository write claim"
                    ],
                },
                new RuntimeClaudeWorkerLaneQualification
                {
                    LaneId = "claude-patch-draft",
                    RoutingIntent = "patch_draft",
                    Allowed = false,
                    Summary = "Patch-draft remains out of scope because the current Claude remote API lane does not support materialized patch/result submission.",
                    Constraints =
                    [
                        "materialized patch/result submission remains out of scope",
                        "use codex/local lanes for materialized patch work"
                    ],
                },
            ],
            ChecksPassed =
            [
                "qualification remains runtime-local and explicit",
                "no rollout or registry truth introduced",
                "materialized patch lanes remain closed for Claude"
            ],
        };
    }
}

public sealed record RuntimeClaudeWorkerLaneQualification
{
    public string LaneId { get; init; } = string.Empty;

    public string RoutingIntent { get; init; } = string.Empty;

    public bool Allowed { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string[] Constraints { get; init; } = [];
}

public sealed record RuntimeClaudeWorkerLaneDecision(
    bool Allowed,
    string ReasonCode,
    string Summary,
    string? RoutingIntent,
    string? LaneId,
    IReadOnlyList<string> Constraints)
{
    public static RuntimeClaudeWorkerLaneDecision NotApplicable()
    {
        return new RuntimeClaudeWorkerLaneDecision(
            Allowed: true,
            ReasonCode: "not_applicable",
            Summary: "Claude worker qualification does not apply to this backend.",
            RoutingIntent: null,
            LaneId: null,
            Constraints: Array.Empty<string>());
    }
}
