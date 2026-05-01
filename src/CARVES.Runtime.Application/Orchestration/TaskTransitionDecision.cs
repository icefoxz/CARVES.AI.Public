using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Orchestration;

public sealed record TaskTransitionDecision(
    DomainTaskStatus NextStatus,
    bool IncrementRetry,
    string Reason);
