using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public sealed record OpportunityTaskPreviewResult(
    IReadOnlyDictionary<string, IReadOnlyList<string>> ProposedTaskIdsByOpportunity,
    IReadOnlyList<TaskNode> ProposedTasks);
