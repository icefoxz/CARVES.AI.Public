using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeRemoteWorkerQualificationService
{
    private readonly IReadOnlyDictionary<string, RuntimeRemoteWorkerQualificationPolicy> policiesByBackendId;

    public RuntimeRemoteWorkerQualificationService()
    {
        policiesByBackendId = CreateDefaultPolicies()
            .ToDictionary(item => item.BackendId, item => item, StringComparer.OrdinalIgnoreCase);
    }

    public RuntimeRemoteWorkerQualificationSurface BuildSurface()
    {
        return new RuntimeRemoteWorkerQualificationSurface
        {
            Summary = "Bounded remote worker qualification is runtime-local truth: Claude and Gemini are qualified only for explicit non-materialized lanes, while broader execution remains on existing codex/local paths.",
            CurrentPolicies = policiesByBackendId.Values
                .OrderBy(item => item.ProviderId, StringComparer.Ordinal)
                .ThenBy(item => item.BackendId, StringComparer.Ordinal)
                .ToArray(),
            Notes =
            [
                "This surface classifies bounded remote workers without promoting provider-specific adapter artifacts into core truth.",
                "Patch-draft and other materialized result lanes stay closed for remote API workers until a separate bounded proof line opens them.",
                "Registry rollout, automatic default promotion, and multi-runtime orchestration remain out of scope."
            ],
        };
    }

    public RuntimeRemoteWorkerQualificationPolicy GetPolicy(string backendId)
    {
        if (!policiesByBackendId.TryGetValue(backendId, out var policy))
        {
            throw new InvalidOperationException($"No bounded remote worker qualification policy exists for backend '{backendId}'.");
        }

        return policy;
    }

    public bool TryGetPolicy(string backendId, out RuntimeRemoteWorkerQualificationPolicy? policy)
    {
        return policiesByBackendId.TryGetValue(backendId, out policy);
    }

    public RuntimeRemoteWorkerLaneDecision Evaluate(TaskNode? task, WorkerBackendDescriptor backend)
    {
        if (!TryGetPolicy(backend.BackendId, out var policy) || policy is null)
        {
            return RuntimeRemoteWorkerLaneDecision.NotApplicable(backend.ProviderId, backend.BackendId);
        }

        if (task is null)
        {
            return new RuntimeRemoteWorkerLaneDecision(
                Allowed: true,
                ReasonCode: "conditional_without_task_context",
                Summary: $"{policy.ProviderId}/{policy.BackendId} qualification is evaluated at task lane selection time; no task context was supplied.",
                ProviderId: policy.ProviderId,
                BackendId: policy.BackendId,
                RoutingIntent: null,
                LaneId: null,
                Constraints: Array.Empty<string>());
        }

        task.Metadata.TryGetValue("routing_intent", out var routingIntent);
        if (string.IsNullOrWhiteSpace(routingIntent))
        {
            return new RuntimeRemoteWorkerLaneDecision(
                Allowed: false,
                ReasonCode: "remote_worker_requires_explicit_routing_intent",
                Summary: $"{policy.ProviderId}/{policy.BackendId} is bounded to explicit qualified routing intents and cannot be selected for tasks without routing_intent.",
                ProviderId: policy.ProviderId,
                BackendId: policy.BackendId,
                RoutingIntent: null,
                LaneId: null,
                Constraints:
                [
                    "set routing_intent to an explicitly qualified non-materialized lane",
                    "leave patch_draft and other materialized lanes on existing codex/local paths"
                ]);
        }

        var lane = policy.Lanes.FirstOrDefault(item => string.Equals(item.RoutingIntent, routingIntent, StringComparison.OrdinalIgnoreCase));
        if (lane is null)
        {
            return new RuntimeRemoteWorkerLaneDecision(
                Allowed: false,
                ReasonCode: "remote_worker_lane_not_qualified",
                Summary: $"{policy.ProviderId}/{policy.BackendId} is not qualified for routing intent '{routingIntent}'.",
                ProviderId: policy.ProviderId,
                BackendId: policy.BackendId,
                RoutingIntent: routingIntent,
                LaneId: null,
                Constraints:
                [
                    "use an explicitly qualified non-materialized lane",
                    "keep broader execution lanes on existing codex/local paths"
                ]);
        }

        if (!lane.Allowed)
        {
            return new RuntimeRemoteWorkerLaneDecision(
                Allowed: false,
                ReasonCode: "remote_worker_lane_out_of_scope",
                Summary: lane.Summary,
                ProviderId: policy.ProviderId,
                BackendId: policy.BackendId,
                RoutingIntent: lane.RoutingIntent,
                LaneId: lane.LaneId,
                Constraints: lane.Constraints);
        }

        return new RuntimeRemoteWorkerLaneDecision(
            Allowed: true,
            ReasonCode: "remote_worker_lane_qualified",
            Summary: lane.Summary,
            ProviderId: policy.ProviderId,
            BackendId: policy.BackendId,
            RoutingIntent: lane.RoutingIntent,
            LaneId: lane.LaneId,
            Constraints: lane.Constraints);
    }

    private static RuntimeRemoteWorkerQualificationPolicy[] CreateDefaultPolicies()
    {
        return
        [
            new RuntimeRemoteWorkerQualificationPolicy
            {
                PolicyId = "claude-worker-bounded-v2",
                ProviderId = "claude",
                BackendId = "claude_api",
                RoutingProfileId = "claude-worker-bounded",
                ProtocolFamily = "anthropic_native",
                Lanes =
                [
                    Allow("claude-review-summary", "review_summary", "Claude is qualified for bounded review-summary tasks that return assessment-style output rather than materialized patches.", "bounded review output only", "no materialized patch/result submission"),
                    Allow("claude-failure-summary", "failure_summary", "Claude is qualified for failure-summary tasks that explain errors, retries, and recovery guidance.", "failure explanation only", "no taskgraph mutation"),
                    Allow("claude-reasoning-summary", "reasoning_summary", "Claude is qualified for compact reasoning-summary lanes that stay in assessment territory.", "assessment-only output", "no materialized patch/result submission"),
                    Allow("claude-structured-output", "structured_output", "Claude is qualified for structured-output lanes where bounded JSON/text output is the goal.", "bounded structured output", "no repository write claim"),
                    Deny("claude-patch-draft", "patch_draft", "Patch-draft remains out of scope because the current Claude remote API lane does not support materialized patch/result submission.", "materialized patch/result submission remains out of scope", "use codex/local lanes for materialized patch work")
                ],
                ChecksPassed =
                [
                    "qualification remains runtime-local and explicit",
                    "no rollout or registry truth introduced",
                    "materialized patch lanes remain closed for Claude"
                ],
            },
            new RuntimeRemoteWorkerQualificationPolicy
            {
                PolicyId = "gemini-worker-bounded-v1",
                ProviderId = "gemini",
                BackendId = "gemini_api",
                RoutingProfileId = "gemini-worker-balanced",
                ProtocolFamily = "gemini_native",
                Lanes =
                [
                    Allow("gemini-review-summary", "review_summary", "Gemini is qualified for bounded review-summary tasks that return assessment-style output rather than materialized patches.", "bounded review output only", "no materialized patch/result submission"),
                    Allow("gemini-failure-summary", "failure_summary", "Gemini is qualified for failure-summary tasks that explain errors, retries, and recovery guidance.", "failure explanation only", "no taskgraph mutation"),
                    Allow("gemini-reasoning-summary", "reasoning_summary", "Gemini is qualified for compact reasoning-summary lanes that stay in assessment territory.", "assessment-only output", "no materialized patch/result submission"),
                    Allow("gemini-structured-output", "structured_output", "Gemini is qualified for structured-output lanes where bounded JSON/text output is the goal.", "bounded structured output", "no repository write claim"),
                    Deny("gemini-patch-draft", "patch_draft", "Patch-draft remains out of scope because the current Gemini remote API lane does not support materialized patch/result submission.", "materialized patch/result submission remains out of scope", "use codex/local lanes for materialized patch work")
                ],
                ChecksPassed =
                [
                    "qualification remains runtime-local and explicit",
                    "no rollout or registry truth introduced",
                    "materialized patch lanes remain closed for Gemini"
                ],
            },
        ];
    }

    private static RuntimeRemoteWorkerLaneQualification Allow(string laneId, string routingIntent, string summary, params string[] constraints)
    {
        return new RuntimeRemoteWorkerLaneQualification
        {
            LaneId = laneId,
            RoutingIntent = routingIntent,
            Allowed = true,
            Summary = summary,
            Constraints = constraints,
        };
    }

    private static RuntimeRemoteWorkerLaneQualification Deny(string laneId, string routingIntent, string summary, params string[] constraints)
    {
        return new RuntimeRemoteWorkerLaneQualification
        {
            LaneId = laneId,
            RoutingIntent = routingIntent,
            Allowed = false,
            Summary = summary,
            Constraints = constraints,
        };
    }
}
