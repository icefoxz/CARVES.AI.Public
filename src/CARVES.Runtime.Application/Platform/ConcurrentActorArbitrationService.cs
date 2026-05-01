using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class ConcurrentActorArbitrationService
{
    private readonly SessionOwnershipService ownershipService;
    private readonly RuntimeIncidentTimelineService incidentTimelineService;
    private readonly OperatorOsEventStreamService eventStreamService;

    public ConcurrentActorArbitrationService(
        SessionOwnershipService ownershipService,
        RuntimeIncidentTimelineService incidentTimelineService,
        OperatorOsEventStreamService eventStreamService)
    {
        this.ownershipService = ownershipService;
        this.incidentTimelineService = incidentTimelineService;
        this.eventStreamService = eventStreamService;
    }

    public ActorArbitrationDecision Resolve(ActorSessionRecord challenger, OwnershipScope scope, string targetId, string reason)
    {
        var ownership = ownershipService.Claim(challenger, scope, targetId, reason);
        var decision = new ActorArbitrationDecision
        {
            Scope = scope,
            TargetId = targetId,
            ChallengerActorSessionId = challenger.ActorSessionId,
            ChallengerKind = challenger.Kind,
            ChallengerIdentity = challenger.ActorIdentity,
            Outcome = ownership.Allowed
                ? ActorArbitrationOutcome.Granted
                : ownership.Outcome switch
                {
                    OwnershipDecisionOutcome.Deferred => ActorArbitrationOutcome.Deferred,
                    OwnershipDecisionOutcome.Escalated => ActorArbitrationOutcome.Escalated,
                    _ => ActorArbitrationOutcome.DeniedChallenger,
                },
            Summary = ownership.Allowed
                ? ownership.Summary
                : $"Concurrent actor conflict on {scope}/{targetId}: {ownership.Summary} Poll `actor ownership --scope {scope} --target-id {targetId}` or `api actor-ownership --scope {scope} --target-id {targetId}` to watch for release, then retry.",
            ReasonCode = ownership.ReasonCode,
            CurrentOwnerActorSessionId = ownership.ExistingOwnerActorSessionId,
            CurrentOwnerKind = ownership.ExistingOwnerKind,
            CurrentOwnerIdentity = ownership.ExistingOwnerIdentity,
        };

        eventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.ArbitrationResolved,
            RepoId = challenger.RepoId,
            ActorSessionId = challenger.ActorSessionId,
            ActorKind = challenger.Kind,
            ActorIdentity = challenger.ActorIdentity,
            OwnershipScope = scope,
            OwnershipTargetId = targetId,
            ReferenceId = decision.ArbitrationId,
            ReasonCode = decision.ReasonCode,
            Summary = decision.Summary,
        });

        if (decision.Outcome != ActorArbitrationOutcome.Granted)
        {
            incidentTimelineService.Append(new RuntimeIncidentRecord
            {
                IncidentType = RuntimeIncidentType.OperatorIntervention,
                RepoId = challenger.RepoId,
                ActorKind = RuntimeIncidentActorKind.Human,
                ActorIdentity = challenger.ActorIdentity,
                Summary = decision.Summary,
                ConsequenceSummary = decision.Outcome.ToString(),
                ReasonCode = decision.ReasonCode,
                ReferenceId = decision.ArbitrationId,
            });
        }

        return decision;
    }
}
