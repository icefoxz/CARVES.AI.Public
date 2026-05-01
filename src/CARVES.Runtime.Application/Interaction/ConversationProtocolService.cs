using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Runtime;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Interaction;

public sealed class ConversationProtocolService
{
    private readonly TaskGraphService taskGraphService;
    private readonly IntentDiscoveryService intentDiscoveryService;

    public ConversationProtocolService(TaskGraphService taskGraphService, IntentDiscoveryService intentDiscoveryService)
    {
        this.taskGraphService = taskGraphService;
        this.intentDiscoveryService = intentDiscoveryService;
    }

    public ConversationProtocolStatus GetStatus(RuntimeSessionState? session)
    {
        var graph = taskGraphService.Load();
        var intentStatus = intentDiscoveryService.GetStatus();
        var completedTaskIds = graph.CompletedTaskIds();
        var phase = ResolvePhase(graph, session, completedTaskIds);
        return new ConversationProtocolStatus(
            phase,
            ResolveAllowedNext(phase, intentStatus, graph, completedTaskIds),
            BuildRecommendedNextAction(phase, intentStatus),
            "CARVES interaction flow stays ordered: intent -> cards -> tasks -> execution -> review.");
    }

    public ConversationProtocolValidation ValidateRequestedPhase(ConversationPhase requestedPhase, RuntimeSessionState? session)
    {
        var status = GetStatus(session);
        var allowed = (int)requestedPhase <= (int)status.CurrentPhase
            || status.AllowedNextPhases.Contains(requestedPhase);
        return new ConversationProtocolValidation(
            allowed,
            status.CurrentPhase,
            requestedPhase,
            allowed
                ? $"Phase '{requestedPhase}' is allowed from current phase '{status.CurrentPhase}'."
                : $"Phase '{requestedPhase}' is out of order from current phase '{status.CurrentPhase}'.",
            status.RecommendedNextAction);
    }

    private static ConversationPhase ResolvePhase(
        DomainTaskGraph graph,
        RuntimeSessionState? session,
        IReadOnlySet<string> completedTaskIds)
    {
        if ((session?.ReviewPendingTaskIds.Count ?? 0) > 0 || graph.Tasks.Values.Any(task => task.Status == DomainTaskStatus.Review))
        {
            return ConversationPhase.Review;
        }

        if ((session?.ActiveTaskIds.Count ?? 0) > 0
            || graph.Tasks.Values.Any(task => task.Status == DomainTaskStatus.Running)
            || graph.Tasks.Values.Any(task => AcceptanceContractExecutionGate.IsReadyForDispatch(task, completedTaskIds)))
        {
            return ConversationPhase.Execution;
        }

        if (graph.Tasks.Count > 0)
        {
            return ConversationPhase.Tasks;
        }

        if (graph.Cards.Count > 0)
        {
            return ConversationPhase.Cards;
        }

        return ConversationPhase.Intent;
    }

    private static IReadOnlyList<ConversationPhase> ResolveAllowedNext(
        ConversationPhase phase,
        IntentDiscoveryStatus intentStatus,
        DomainTaskGraph graph,
        IReadOnlySet<string> completedTaskIds)
    {
        return phase switch
        {
            ConversationPhase.Intent => intentStatus.State is IntentDiscoveryState.Accepted or IntentDiscoveryState.Stale
                ? [ConversationPhase.Intent, ConversationPhase.Cards]
                : [ConversationPhase.Intent],
            ConversationPhase.Cards => [ConversationPhase.Cards, ConversationPhase.Tasks],
            ConversationPhase.Tasks => graph.Tasks.Values.Any(task => AcceptanceContractExecutionGate.IsReadyForDispatch(task, completedTaskIds))
                ? [ConversationPhase.Tasks, ConversationPhase.Execution]
                : [ConversationPhase.Tasks],
            ConversationPhase.Execution => graph.Tasks.Values.Any(task => task.Status == DomainTaskStatus.Review)
                ? [ConversationPhase.Execution, ConversationPhase.Review]
                : [ConversationPhase.Execution],
            ConversationPhase.Review => [ConversationPhase.Review, ConversationPhase.Tasks],
            _ => [ConversationPhase.Intent],
        };
    }

    private static string BuildRecommendedNextAction(ConversationPhase phase, IntentDiscoveryStatus intentStatus)
    {
        return phase switch
        {
            ConversationPhase.Intent => intentStatus.RecommendedNextAction,
            ConversationPhase.Cards => "Review cards and make sure the next planning boundary is explicit.",
            ConversationPhase.Tasks => "Inspect task truth and confirm which task should be delegated through CARVES Host next.",
            ConversationPhase.Execution => "Delegate ready execution work through CARVES Host or inspect the active worker and review boundary.",
            ConversationPhase.Review => "Resolve review before attempting more execution work.",
            _ => "Inspect the current interaction phase.",
        };
    }
}
