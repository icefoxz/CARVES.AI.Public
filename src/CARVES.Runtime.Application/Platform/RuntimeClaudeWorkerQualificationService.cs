using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeClaudeWorkerQualificationService
{
    private readonly RuntimeRemoteWorkerQualificationService remoteWorkerQualificationService;

    public RuntimeClaudeWorkerQualificationService(RuntimeRemoteWorkerQualificationService? remoteWorkerQualificationService = null)
    {
        this.remoteWorkerQualificationService = remoteWorkerQualificationService ?? new RuntimeRemoteWorkerQualificationService();
    }

    public RuntimeClaudeWorkerQualificationSurface BuildSurface()
    {
        var policy = remoteWorkerQualificationService.GetPolicy("claude_api");
        return new RuntimeClaudeWorkerQualificationSurface
        {
            CurrentPolicy = ProjectPolicy(policy),
            Summary = "Claude worker remains a bounded remote API lane: review, failure, reasoning, and structured output are qualified; materialized patch lanes stay closed.",
            Notes =
            [
                "This qualification line is runtime-local and explicit.",
                "This provider-specific surface is a projection over runtime-remote-worker-qualification.",
                "Selection stays governed through existing worker routing and audit surfaces.",
                "Registry rollout, automatic default promotion, and multi-runtime orchestration remain out of scope."
            ],
        };
    }

    public RuntimeClaudeWorkerLaneDecision Evaluate(TaskNode? task, WorkerBackendDescriptor backend)
    {
        if (!string.Equals(backend.BackendId, "claude_api", StringComparison.OrdinalIgnoreCase))
        {
            return RuntimeClaudeWorkerLaneDecision.NotApplicable();
        }

        var decision = remoteWorkerQualificationService.Evaluate(task, backend);
        return new RuntimeClaudeWorkerLaneDecision(
            Allowed: decision.Allowed,
            ReasonCode: TranslateReasonCode(decision.ReasonCode),
            Summary: decision.Summary,
            RoutingIntent: decision.RoutingIntent,
            LaneId: decision.LaneId,
            Constraints: decision.Constraints);
    }

    private static RuntimeClaudeWorkerQualificationPolicy ProjectPolicy(RuntimeRemoteWorkerQualificationPolicy policy)
    {
        return new RuntimeClaudeWorkerQualificationPolicy
        {
            PolicyId = policy.PolicyId,
            RecordedAt = policy.RecordedAt,
            ProviderId = policy.ProviderId,
            BackendId = policy.BackendId,
            Lanes = policy.Lanes.Select(item => new RuntimeClaudeWorkerLaneQualification
            {
                LaneId = item.LaneId,
                RoutingIntent = item.RoutingIntent,
                Allowed = item.Allowed,
                Summary = item.Summary,
                Constraints = item.Constraints,
            }).ToArray(),
            ChecksPassed = policy.ChecksPassed,
        };
    }

    private static string TranslateReasonCode(string value)
    {
        return value switch
        {
            "remote_worker_requires_explicit_routing_intent" => "claude_worker_requires_explicit_routing_intent",
            "remote_worker_lane_not_qualified" => "claude_worker_lane_not_qualified",
            "remote_worker_lane_out_of_scope" => "claude_worker_lane_out_of_scope",
            "remote_worker_lane_qualified" => "claude_worker_lane_qualified",
            _ => value,
        };
    }
}
