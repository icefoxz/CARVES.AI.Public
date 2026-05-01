using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Workers;

public sealed class WorkerRequestBudgetPolicyService
{
    private const string TimeoutCapEnvironmentVariable = "CARVES_WORKER_REQUEST_TIMEOUT_CAP_SECONDS";
    private readonly int fallbackTimeoutSeconds;
    private readonly ExecutionBudgetFactory executionBudgetFactory;

    public WorkerRequestBudgetPolicyService(int fallbackTimeoutSeconds, ExecutionBudgetFactory? executionBudgetFactory = null)
    {
        this.fallbackTimeoutSeconds = fallbackTimeoutSeconds > 0 ? fallbackTimeoutSeconds : 30;
        this.executionBudgetFactory = executionBudgetFactory ?? new ExecutionBudgetFactory(new ExecutionPathClassifier());
    }

    public WorkerRequestBudget Resolve(TaskNode task, WorkerSelectionDecision selection, string repoRoot, string worktreeRoot)
    {
        var executionBudget = executionBudgetFactory.Create(task);
        var providerBaselineSeconds = Math.Max(5, selection.SelectedProviderTimeoutSeconds ?? fallbackTimeoutSeconds);
        var validationCommandCount = task.Validation.Commands.Count;
        var repoTruthGuidanceRequired = RequiresRepoRootControlPlaneGuidance(repoRoot, worktreeRoot);
        var dynamicFloorSeconds = ResolveDynamicFloorSeconds(executionBudget);
        var dynamicHeadroomSeconds = ResolveDynamicHeadroomSeconds(
            executionBudget,
            validationCommandCount,
            selection.SelectedBackendSupportsLongRunningTasks,
            repoTruthGuidanceRequired);
        var selectedTimeoutSeconds = Math.Max(providerBaselineSeconds, dynamicFloorSeconds + dynamicHeadroomSeconds);
        var timeoutCeilingSeconds = selection.SelectedBackendSupportsLongRunningTasks ? 180 : 120;
        if (ShouldUseLongRunningRepoTruthBudget(selection, executionBudget, repoTruthGuidanceRequired))
        {
            selectedTimeoutSeconds = Math.Max(selectedTimeoutSeconds, timeoutCeilingSeconds);
        }

        selectedTimeoutSeconds = Math.Clamp(selectedTimeoutSeconds, 15, timeoutCeilingSeconds);

        var reasons = new List<string>
        {
            $"provider_baseline={providerBaselineSeconds}s",
            $"execution_budget={executionBudget.Size.ToString().ToLowerInvariant()}",
            $"confidence={executionBudget.ConfidenceLevel.ToString().ToLowerInvariant()}",
            $"max_duration_minutes={executionBudget.MaxDurationMinutes}",
        };
        if (validationCommandCount > 0)
        {
            reasons.Add($"validation_commands={validationCommandCount}");
        }

        if (selection.SelectedBackendSupportsLongRunningTasks)
        {
            reasons.Add("long_running_lane");
        }

        if (repoTruthGuidanceRequired)
        {
            reasons.Add("repo_root_truth_guidance");
        }

        if (ShouldUseLongRunningRepoTruthBudget(selection, executionBudget, repoTruthGuidanceRequired))
        {
            reasons.Add("long_running_repo_truth_budget");
        }

        if (TryResolveTimeoutCapSeconds(timeoutCeilingSeconds, out var timeoutCapSeconds)
            && timeoutCapSeconds < selectedTimeoutSeconds)
        {
            selectedTimeoutSeconds = timeoutCapSeconds;
            reasons.Add($"timeout_cap={timeoutCapSeconds}s");
        }

        var summary = $"Selected {selectedTimeoutSeconds}s delegated request budget (baseline {providerBaselineSeconds}s, execution budget {executionBudget.Size.ToString().ToLowerInvariant()}, confidence {executionBudget.ConfidenceLevel.ToString().ToLowerInvariant()}).";
        var rationale = $"Runtime policy derives the delegated request timeout from the selected provider baseline, execution budget duration, validation load, lane capability, and authoritative repo-truth access shape. Current max duration: {executionBudget.MaxDurationMinutes} minute(s).";

        return new WorkerRequestBudget
        {
            TimeoutSeconds = selectedTimeoutSeconds,
            ProviderBaselineSeconds = providerBaselineSeconds,
            ExecutionBudgetSize = executionBudget.Size,
            ConfidenceLevel = executionBudget.ConfidenceLevel,
            MaxDurationMinutes = executionBudget.MaxDurationMinutes,
            ValidationCommandCount = validationCommandCount,
            LongRunningLane = selection.SelectedBackendSupportsLongRunningTasks,
            RepoTruthGuidanceRequired = repoTruthGuidanceRequired,
            Summary = summary,
            Rationale = rationale,
            Reasons = reasons,
        };
    }

    private static int ResolveDynamicFloorSeconds(ExecutionBudget executionBudget)
    {
        return executionBudget.MaxDurationMinutes switch
        {
            >= 60 => 90,
            >= 30 => 60,
            >= 15 => 45,
            _ => 30,
        };
    }

    private static int ResolveDynamicHeadroomSeconds(
        ExecutionBudget executionBudget,
        int validationCommandCount,
        bool longRunningLane,
        bool repoTruthGuidanceRequired)
    {
        var headroom = 0;
        if (validationCommandCount > 0)
        {
            headroom += Math.Min(15, validationCommandCount * 5);
        }

        if (longRunningLane)
        {
            headroom += 15;
        }

        if (repoTruthGuidanceRequired)
        {
            headroom += 15;
        }

        headroom += executionBudget.ConfidenceLevel switch
        {
            ExecutionConfidenceLevel.High => 10,
            ExecutionConfidenceLevel.Low => -10,
            _ => 0,
        };

        return headroom;
    }

    private static bool RequiresRepoRootControlPlaneGuidance(string repoRoot, string worktreeRoot)
    {
        if (string.Equals(
                Path.GetFullPath(repoRoot),
                Path.GetFullPath(worktreeRoot),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var repoAiRoot = Path.Combine(repoRoot, ".ai");
        var worktreeAiRoot = Path.Combine(worktreeRoot, ".ai");
        return Directory.Exists(repoAiRoot) && !Directory.Exists(worktreeAiRoot);
    }

    private static bool ShouldUseLongRunningRepoTruthBudget(
        WorkerSelectionDecision selection,
        ExecutionBudget executionBudget,
        bool repoTruthGuidanceRequired)
    {
        return selection.SelectedBackendSupportsLongRunningTasks
            && repoTruthGuidanceRequired
            && executionBudget.Size is ExecutionBudgetSize.Medium or ExecutionBudgetSize.Large;
    }

    private static bool TryResolveTimeoutCapSeconds(int timeoutCeilingSeconds, out int timeoutCapSeconds)
    {
        timeoutCapSeconds = 0;
        var configuredValue = Environment.GetEnvironmentVariable(TimeoutCapEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredValue)
            || !int.TryParse(configuredValue, out var parsedValue)
            || parsedValue <= 0)
        {
            return false;
        }

        timeoutCapSeconds = Math.Clamp(parsedValue, 15, timeoutCeilingSeconds);
        return true;
    }
}
