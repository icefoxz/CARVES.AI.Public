namespace Carves.Runtime.Domain.ExecutionPolicy;

public sealed record ExecutionRiskPolicy(
    int SmallMaxFilesChanged,
    int MediumMaxFilesChanged,
    int SmallMaxLinesChanged,
    int MediumMaxLinesChanged,
    int SmallMaxNewFiles,
    int MediumMaxNewFiles,
    int SmallMaxTouchedModules,
    int MediumMaxTouchedModules,
    int SmallMaxTouchedLayers,
    int MediumMaxTouchedLayers,
    int SmallMaxChangeKinds,
    int MediumMaxChangeKinds,
    int SmallMaxElapsedMinutes,
    int MediumMaxElapsedMinutes,
    int SmallMaxNonWriteSteps,
    int MediumMaxNonWriteSteps,
    double HighCertaintyAcceptanceRatio,
    double HighCertaintyValidationRatio,
    double MediumCertaintyAcceptanceRatio)
{
    public static ExecutionRiskPolicy CreateDefault()
    {
        return new ExecutionRiskPolicy(
            SmallMaxFilesChanged: 3,
            MediumMaxFilesChanged: 8,
            SmallMaxLinesChanged: 120,
            MediumMaxLinesChanged: 350,
            SmallMaxNewFiles: 1,
            MediumMaxNewFiles: 3,
            SmallMaxTouchedModules: 1,
            MediumMaxTouchedModules: 3,
            SmallMaxTouchedLayers: 1,
            MediumMaxTouchedLayers: 2,
            SmallMaxChangeKinds: 2,
            MediumMaxChangeKinds: 4,
            SmallMaxElapsedMinutes: 15,
            MediumMaxElapsedMinutes: 35,
            SmallMaxNonWriteSteps: 8,
            MediumMaxNonWriteSteps: 15,
            HighCertaintyAcceptanceRatio: 0.8,
            HighCertaintyValidationRatio: 0.8,
            MediumCertaintyAcceptanceRatio: 0.5);
    }
}
