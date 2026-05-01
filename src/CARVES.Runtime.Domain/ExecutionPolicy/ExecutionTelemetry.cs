namespace Carves.Runtime.Domain.ExecutionPolicy;

public sealed record ExecutionTelemetry(
    int FilesChanged,
    int LinesAdded,
    int LinesDeleted,
    int NewFiles,
    int ChangeKindsCount,
    int TouchedModules,
    int TouchedLayers,
    int NonWriteSteps,
    int ToolCalls,
    int ElapsedMinutes,
    bool RequiredOutputsPresent,
    double AcceptanceCoveredRatio,
    double ValidationPassRatio,
    int OpenQuestionsCount);
