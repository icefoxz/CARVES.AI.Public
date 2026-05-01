namespace Carves.Runtime.Application.AI;

internal static class RuntimeTokenBaselineReadinessPolicy
{
    public const double MaxAllowedUnattributedShareRatio = 0.05d;

    public const double MinClassifiedSegmentCoverageRatio = 0.80d;
}
