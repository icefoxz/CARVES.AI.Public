namespace Carves.Runtime.Domain.ExecutionPolicy;

public sealed record ExecutionBoundaryVerdict(
    ExecutionRiskLevel RiskLevel,
    int RiskScore,
    bool ShouldStop,
    bool ShouldSplit,
    bool ShouldReturnToPlanner,
    bool ShouldReducePatchScope,
    string Reason);
