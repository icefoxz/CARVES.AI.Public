using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.AI;

public sealed class QualificationLaneExecutor : IQualificationLaneExecutor
{
    private readonly string repoRoot;
    private readonly IHttpTransport transport;

    public QualificationLaneExecutor(string repoRoot, IHttpTransport transport)
    {
        this.repoRoot = repoRoot;
        this.transport = transport;
    }

    public WorkerExecutionResult Execute(ModelQualificationLane lane, ModelQualificationCase qualificationCase, int attempt)
    {
        PromoteEnvironmentVariableToProcess(lane.ApiKeyEnvironmentVariable);

        var config = new AiProviderConfig(
            lane.ProviderId,
            true,
            lane.Model,
            lane.BaseUrl,
            lane.ApiKeyEnvironmentVariable,
            false,
            45,
            500,
            "low",
            lane.RequestFamily,
            null,
            null);

        var aiClient = AiClientFactory.Create(config);
        var registry = WorkerAdapterFactory.Create(config, aiClient, transport);
        var adapter = registry.Resolve(lane.BackendId);
        var request = new WorkerExecutionRequest
        {
            TaskId = $"qualification-{qualificationCase.CaseId}",
            RepoId = "qualification",
            Title = qualificationCase.CaseId,
            Description = qualificationCase.Summary ?? qualificationCase.CaseId,
            Instructions = "You are running inside CARVES.Runtime qualification. Do not claim filesystem execution. Produce only the requested output for the routing intent being evaluated.",
            Input = qualificationCase.Prompt,
            MaxOutputTokens = 500,
            TimeoutSeconds = 45,
            RepoRoot = repoRoot,
            WorktreeRoot = repoRoot,
            BaseCommit = string.Empty,
            BackendHint = lane.BackendId,
            ModelOverride = lane.Model,
            RoutingIntent = qualificationCase.RoutingIntent,
            RoutingModuleId = qualificationCase.ModuleId,
            RoutingProfileId = lane.RoutingProfileId,
            DryRun = false,
            Profile = WorkerExecutionProfile.UntrustedDefault,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["qualification_lane_id"] = lane.LaneId,
                ["qualification_case_id"] = qualificationCase.CaseId,
                ["qualification_attempt"] = attempt.ToString(),
                ["route_group"] = lane.RouteGroup ?? string.Empty,
            },
        };

        return adapter.Execute(request);
    }

    private static void PromoteEnvironmentVariableToProcess(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName))
        {
            return;
        }

        var current = Environment.GetEnvironmentVariable(variableName);
        if (!string.IsNullOrWhiteSpace(current))
        {
            return;
        }

        var fallback = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Machine);
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            Environment.SetEnvironmentVariable(variableName, fallback, EnvironmentVariableTarget.Process);
        }
    }
}
