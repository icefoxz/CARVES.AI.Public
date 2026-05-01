using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentLoopStallGuardService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;

    public RuntimeAgentLoopStallGuardService(
        string repoRoot,
        ControlPlanePaths paths,
        TaskGraphService taskGraphService)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.taskGraphService = taskGraphService;
    }

    public RuntimeAgentLoopStallGuardSurface Build(string taskId)
    {
        taskGraphService.GetTask(taskId);
        var policy = RuntimeAgentGovernanceSupport.LoadPolicy(repoRoot, paths);
        var reportService = new ExecutionRunReportService(paths);
        var reports = reportService.ListReports(taskId);
        var guardPolicy = policy.LoopStallGuardPolicy;
        var pattern = new ExecutionPatternService().Analyze(taskId, reports, guardPolicy.DetectorWindow);
        var rule = guardPolicy.Rules.FirstOrDefault(item => string.Equals(item.PatternType, pattern.Type.ToString(), StringComparison.Ordinal))
                   ?? new AgentGovernanceLoopStallRule
                   {
                       PatternType = pattern.Type.ToString(),
                       StandardOutcome = pattern.Suggestion.ToString(),
                       WeakOutcome = pattern.Suggestion.ToString(),
                       Summary = "No explicit loop/stall rule existed; fell back to current execution-pattern suggestion.",
                   };

        var profileOutcomes = policy.ModelProfiles
            .OrderBy(item => item.ProfileId, StringComparer.Ordinal)
            .Select(profile =>
            {
                var strict = guardPolicy.StrictProfileIds.Contains(profile.ProfileId, StringComparer.OrdinalIgnoreCase);
                var forcedAction = strict ? rule.WeakOutcome : rule.StandardOutcome;
                return new AgentLoopStallProfileOutcome
                {
                    ProfileId = profile.ProfileId,
                    ForcedAction = forcedAction,
                    Reason = strict
                        ? $"{profile.ProfileId} is a strict profile for {pattern.Type}; weak outcome {forcedAction} is enforced."
                        : $"{profile.ProfileId} uses standard loop/stall outcome {forcedAction} for {pattern.Type}.",
                };
            })
            .ToArray();

        return new RuntimeAgentLoopStallGuardSurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformAgentGovernanceKernelFile),
            Guard = new AgentLoopStallGuardEvaluation
            {
                TaskId = taskId,
                DetectorWindow = guardPolicy.DetectorWindow,
                DetectionLineage = guardPolicy.DetectionLineage,
                Pattern = pattern,
                ProfileOutcomes = profileOutcomes,
            },
        };
    }
}
