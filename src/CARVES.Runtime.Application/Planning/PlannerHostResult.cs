using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerHostResult
{
    public RuntimeSessionState Session { get; init; } = new();

    public PlannerReentryResult Reentry { get; init; } = new(PlannerReentryOutcome.NoJustifiedGap, "planner host did not run", Array.Empty<string>(), false);

    public PlannerProposalEnvelope? Proposal { get; init; }

    public PlannerProposalValidationResult? Validation { get; init; }
}
