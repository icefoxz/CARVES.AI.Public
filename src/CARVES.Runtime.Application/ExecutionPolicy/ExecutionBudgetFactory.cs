using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ExecutionPolicy;

public sealed class ExecutionBudgetFactory
{
    private readonly ExecutionPathClassifier pathClassifier;
    private readonly ExecutionConfidenceService confidenceService;

    public ExecutionBudgetFactory(ExecutionPathClassifier pathClassifier, ExecutionConfidenceService? confidenceService = null)
    {
        this.pathClassifier = pathClassifier;
        this.confidenceService = confidenceService ?? new ExecutionConfidenceService();
    }

    public ExecutionBudget Create(TaskNode task)
    {
        return Create(task, AssessConfidence(task));
    }

    public ExecutionConfidence AssessConfidence(TaskNode task)
    {
        return confidenceService.Assess(task);
    }

    private ExecutionBudget Create(TaskNode task, ExecutionConfidence confidence)
    {
        var scopeCount = task.Scope.Count;
        var validationCount = task.Validation.Commands.Count + task.Validation.Checks.Count + task.Validation.ExpectedEvidence.Count;
        var size = scopeCount switch
        {
            <= 2 => ExecutionBudgetSize.Small,
            <= 5 => ExecutionBudgetSize.Medium,
            _ => ExecutionBudgetSize.Large,
        };

        var baseline = size switch
        {
            ExecutionBudgetSize.Small => Build(task, size, 3, 180, 20, validationCount),
            ExecutionBudgetSize.Medium => Build(task, size, 6, 420, 40, validationCount),
            _ => Build(task, size, 10, 900, 75, validationCount),
        };

        return Scale(baseline, confidence);
    }

    private ExecutionBudget Build(TaskNode task, ExecutionBudgetSize size, int maxFiles, int maxLinesChanged, int maxDurationMinutes, int validationCount)
    {
        var changeKinds = pathClassifier.ClassifyMany(task.Scope);
        var reviewBoundary = task.RequiresReviewBoundary
            || changeKinds.Contains(ExecutionChangeKind.ControlPlaneState)
            || changeKinds.Contains(ExecutionChangeKind.Contracts)
            || validationCount >= 3;
        return new ExecutionBudget
        {
            Size = size,
            ConfidenceLevel = ExecutionConfidenceLevel.Medium,
            MaxFiles = maxFiles,
            MaxLinesChanged = maxLinesChanged,
            MaxRetries = size switch
            {
                ExecutionBudgetSize.Small => 2,
                ExecutionBudgetSize.Medium => 3,
                _ => 5,
            },
            MaxFailureDensity = size switch
            {
                ExecutionBudgetSize.Small => 0.35,
                ExecutionBudgetSize.Medium => 0.5,
                _ => 0.7,
            },
            MaxDurationMinutes = maxDurationMinutes,
            RequiresReviewBoundary = reviewBoundary,
            ChangeKinds = changeKinds,
            Summary = $"{size} execution budget: <= {maxFiles} files, <= {maxLinesChanged} lines, <= {maxDurationMinutes} minutes.",
            Rationale = "Budget is derived from task scope width, validation cost, and protected path classes.",
        };
    }

    private static ExecutionBudget Scale(ExecutionBudget baseline, ExecutionConfidence confidence)
    {
        var fileFactor = confidence.Level switch
        {
            ExecutionConfidenceLevel.High => 1.5,
            ExecutionConfidenceLevel.Low => 0.6,
            _ => 1.0,
        };
        var lineFactor = confidence.Level switch
        {
            ExecutionConfidenceLevel.High => 1.4,
            ExecutionConfidenceLevel.Low => 0.65,
            _ => 1.0,
        };
        var durationFactor = confidence.Level switch
        {
            ExecutionConfidenceLevel.High => 1.3,
            ExecutionConfidenceLevel.Low => 0.75,
            _ => 1.0,
        };

        var maxFiles = Math.Max(1, (int)Math.Round(baseline.MaxFiles * fileFactor, MidpointRounding.AwayFromZero));
        var maxLinesChanged = Math.Max(20, (int)Math.Round(baseline.MaxLinesChanged * lineFactor, MidpointRounding.AwayFromZero));
        var maxDurationMinutes = Math.Max(5, (int)Math.Round(baseline.MaxDurationMinutes * durationFactor, MidpointRounding.AwayFromZero));
        var maxRetries = confidence.Level switch
        {
            ExecutionConfidenceLevel.High => baseline.MaxRetries + 1,
            ExecutionConfidenceLevel.Low => Math.Max(1, baseline.MaxRetries - 1),
            _ => baseline.MaxRetries,
        };
        var maxFailureDensity = confidence.Level switch
        {
            ExecutionConfidenceLevel.High => Math.Min(0.95, baseline.MaxFailureDensity + 0.15),
            ExecutionConfidenceLevel.Low => Math.Max(0.2, baseline.MaxFailureDensity - 0.15),
            _ => baseline.MaxFailureDensity,
        };

        return baseline with
        {
            ConfidenceLevel = confidence.Level,
            MaxFiles = maxFiles,
            MaxLinesChanged = maxLinesChanged,
            MaxRetries = maxRetries,
            MaxFailureDensity = maxFailureDensity,
            MaxDurationMinutes = maxDurationMinutes,
            Summary = $"{baseline.Size} execution budget ({confidence.Level.ToString().ToLowerInvariant()} confidence): <= {maxFiles} files, <= {maxLinesChanged} lines, <= {maxDurationMinutes} minutes, <= {maxRetries} retries, failure density <= {maxFailureDensity:0.00}.",
            Rationale = $"{baseline.Rationale} Confidence scaling is derived from historical execution success rate and failure streak.",
        };
    }
}
