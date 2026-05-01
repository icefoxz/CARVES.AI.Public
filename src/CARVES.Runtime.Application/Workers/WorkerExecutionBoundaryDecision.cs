using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Workers;

public sealed record WorkerExecutionBoundaryDecision(
    bool Allowed,
    WorkerExecutionProfile Profile,
    string Reason);
