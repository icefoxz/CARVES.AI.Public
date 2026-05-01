using Carves.Runtime.Domain.ExecutionPolicy;

namespace Carves.Runtime.Application.ExecutionPolicy;

public sealed class ExecutionRiskEvaluator
{
    private readonly ExecutionRiskPolicy policy;

    public ExecutionRiskEvaluator(ExecutionRiskPolicy? policy = null)
    {
        this.policy = policy ?? ExecutionRiskPolicy.CreateDefault();
    }

    public ExecutionBoundaryVerdict Evaluate(ExecutionBudget budget, ExecutionTelemetry telemetry)
    {
        var size = ClassifySize(telemetry);
        var breadth = ClassifyBreadth(telemetry);
        var variety = ClassifyVariety(telemetry);
        var duration = ClassifyDuration(telemetry);
        var certainty = ClassifyCertainty(telemetry);

        var riskScore = ScoreSize(size)
            + ScoreBreadth(breadth)
            + ScoreVariety(variety)
            + ScoreDuration(duration)
            + ScoreCertainty(certainty);
        var riskLevel = ClassifyRiskLevel(riskScore);
        var overBudget = telemetry.FilesChanged > budget.MaxFilesChanged
            || telemetry.ChangeKindsCount > budget.MaxChangeKinds
            || telemetry.NonWriteSteps > budget.MaxNonWriteSteps
            || telemetry.ElapsedMinutes > budget.MaxElapsedMinutes
            || telemetry.TouchedModules > budget.MaxTouchedModules
            || telemetry.TouchedLayers > budget.MaxTouchedLayers;
        var stopBecauseOutputsPresent = budget.RequireStopWhenOutputsPresent
            && telemetry.RequiredOutputsPresent
            && telemetry.AcceptanceCoveredRatio >= policy.MediumCertaintyAcceptanceRatio
            && telemetry.ValidationPassRatio >= policy.MediumCertaintyAcceptanceRatio;

        var reasons = new List<string>();
        if (size == TaskChunkSize.Large)
        {
            reasons.Add("size exceeds large threshold");
        }
        if (breadth == TaskChunkSize.Large)
        {
            reasons.Add("breadth exceeds medium threshold");
        }
        if (variety == TaskChunkSize.Large)
        {
            reasons.Add("change kinds too high");
        }
        if (duration == TaskChunkSize.Large)
        {
            reasons.Add("execution duration/non-write steps too high");
        }
        if (certainty == "low")
        {
            reasons.Add("certainty too low");
        }
        if (overBudget)
        {
            reasons.Add("execution exceeded its declared budget");
        }
        if (stopBecauseOutputsPresent)
        {
            reasons.Add("required outputs are present with minimum validation coverage");
        }
        if (reasons.Count == 0)
        {
            reasons.Add("execution remained within governed budget");
        }

        var shouldStop = riskLevel == ExecutionRiskLevel.Critical || stopBecauseOutputsPresent;
        var shouldSplit = riskLevel >= ExecutionRiskLevel.High || size > budget.ExpectedSize || breadth > budget.ExpectedBreadth || variety > budget.ExpectedVariety;
        var shouldReturnToPlanner = riskLevel >= ExecutionRiskLevel.High || overBudget;
        var shouldReducePatchScope = riskLevel >= ExecutionRiskLevel.High
            || telemetry.ChangeKindsCount > budget.MaxChangeKinds
            || telemetry.FilesChanged > budget.MaxFilesChanged;

        return new ExecutionBoundaryVerdict(
            riskLevel,
            riskScore,
            shouldStop,
            shouldSplit,
            shouldReturnToPlanner,
            shouldReducePatchScope,
            string.Join("; ", reasons));
    }

    private TaskChunkSize ClassifySize(ExecutionTelemetry telemetry)
    {
        var linesChanged = telemetry.LinesAdded + telemetry.LinesDeleted;
        if (telemetry.FilesChanged <= policy.SmallMaxFilesChanged
            && linesChanged <= policy.SmallMaxLinesChanged
            && telemetry.NewFiles <= policy.SmallMaxNewFiles)
        {
            return TaskChunkSize.Small;
        }

        if (telemetry.FilesChanged <= policy.MediumMaxFilesChanged
            && linesChanged <= policy.MediumMaxLinesChanged
            && telemetry.NewFiles <= policy.MediumMaxNewFiles)
        {
            return TaskChunkSize.Medium;
        }

        return TaskChunkSize.Large;
    }

    private TaskChunkSize ClassifyBreadth(ExecutionTelemetry telemetry)
    {
        if (telemetry.TouchedModules <= policy.SmallMaxTouchedModules
            && telemetry.TouchedLayers <= policy.SmallMaxTouchedLayers)
        {
            return TaskChunkSize.Small;
        }

        if (telemetry.TouchedModules <= policy.MediumMaxTouchedModules
            && telemetry.TouchedLayers <= policy.MediumMaxTouchedLayers)
        {
            return TaskChunkSize.Medium;
        }

        return TaskChunkSize.Large;
    }

    private TaskChunkSize ClassifyVariety(ExecutionTelemetry telemetry)
    {
        if (telemetry.ChangeKindsCount <= policy.SmallMaxChangeKinds)
        {
            return TaskChunkSize.Small;
        }

        if (telemetry.ChangeKindsCount <= policy.MediumMaxChangeKinds)
        {
            return TaskChunkSize.Medium;
        }

        return TaskChunkSize.Large;
    }

    private TaskChunkSize ClassifyDuration(ExecutionTelemetry telemetry)
    {
        if (telemetry.ElapsedMinutes <= policy.SmallMaxElapsedMinutes
            && telemetry.NonWriteSteps <= policy.SmallMaxNonWriteSteps)
        {
            return TaskChunkSize.Small;
        }

        if (telemetry.ElapsedMinutes <= policy.MediumMaxElapsedMinutes
            && telemetry.NonWriteSteps <= policy.MediumMaxNonWriteSteps)
        {
            return TaskChunkSize.Medium;
        }

        return TaskChunkSize.Large;
    }

    private string ClassifyCertainty(ExecutionTelemetry telemetry)
    {
        if (telemetry.RequiredOutputsPresent
            && telemetry.AcceptanceCoveredRatio >= policy.HighCertaintyAcceptanceRatio
            && telemetry.ValidationPassRatio >= policy.HighCertaintyValidationRatio
            && telemetry.OpenQuestionsCount == 0)
        {
            return "high";
        }

        if (telemetry.AcceptanceCoveredRatio >= policy.MediumCertaintyAcceptanceRatio)
        {
            return "medium";
        }

        return "low";
    }

    private static int ScoreSize(TaskChunkSize size) => size switch
    {
        TaskChunkSize.Small => 0,
        TaskChunkSize.Medium => 1,
        _ => 3,
    };

    private static int ScoreBreadth(TaskChunkSize breadth) => breadth switch
    {
        TaskChunkSize.Small => 0,
        TaskChunkSize.Medium => 1,
        _ => 3,
    };

    private static int ScoreVariety(TaskChunkSize variety) => variety switch
    {
        TaskChunkSize.Small => 0,
        TaskChunkSize.Medium => 2,
        _ => 4,
    };

    private static int ScoreDuration(TaskChunkSize duration) => duration switch
    {
        TaskChunkSize.Small => 0,
        TaskChunkSize.Medium => 1,
        _ => 3,
    };

    private static int ScoreCertainty(string certainty) => certainty switch
    {
        "high" => 0,
        "medium" => 2,
        _ => 4,
    };

    private static ExecutionRiskLevel ClassifyRiskLevel(int riskScore) => riskScore switch
    {
        <= 3 => ExecutionRiskLevel.Low,
        <= 7 => ExecutionRiskLevel.Medium,
        <= 11 => ExecutionRiskLevel.High,
        _ => ExecutionRiskLevel.Critical,
    };
}
