namespace Carves.Runtime.Domain.ExecutionPolicy;

public sealed record ExecutionBudget(
    TaskChunkSize ExpectedSize,
    TaskChunkSize ExpectedBreadth,
    TaskChunkSize ExpectedVariety,
    int MaxFilesChanged,
    int MaxChangeKinds,
    int MaxNonWriteSteps,
    int MaxElapsedMinutes,
    int MaxTouchedModules,
    int MaxTouchedLayers,
    bool RequireStopWhenOutputsPresent);
