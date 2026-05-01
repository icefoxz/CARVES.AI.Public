using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class SessionOwnershipService
{
    private readonly IOwnershipRepository repository;
    private readonly ActorSessionService actorSessionService;
    private readonly OperatorOsEventStreamService eventStreamService;

    public SessionOwnershipService(
        IOwnershipRepository repository,
        ActorSessionService actorSessionService,
        OperatorOsEventStreamService eventStreamService)
    {
        this.repository = repository;
        this.actorSessionService = actorSessionService;
        this.eventStreamService = eventStreamService;
    }

    public OwnershipDecision Claim(ActorSessionRecord actorSession, OwnershipScope scope, string targetId, string reason)
    {
        if (!CanOwn(actorSession.Kind, scope))
        {
            return new OwnershipDecision
            {
                Allowed = false,
                Outcome = OwnershipDecisionOutcome.Denied,
                Summary = $"{actorSession.Kind} session '{actorSession.ActorIdentity}' may not own {scope}.",
                ReasonCode = "ownership_scope_not_permitted",
            };
        }

        var snapshot = repository.Load();
        var bindings = snapshot.Bindings.ToList();
        var binding = bindings.FirstOrDefault(item => item.Scope == scope && string.Equals(item.TargetId, targetId, StringComparison.Ordinal));
        if (binding is null)
        {
            binding = new OwnershipBinding
            {
                Scope = scope,
                TargetId = targetId,
                OwnerActorSessionId = actorSession.ActorSessionId,
                OwnerKind = actorSession.Kind,
                OwnerIdentity = actorSession.ActorIdentity,
                Reason = reason,
            };
            bindings.Add(binding);
            repository.Save(new OwnershipSnapshot
            {
                Bindings = bindings.OrderBy(item => item.Scope).ThenBy(item => item.TargetId, StringComparer.Ordinal).ToArray(),
            });
            actorSessionService.MarkState(actorSession.ActorSessionId, actorSession.State, reason, ownershipScope: scope, ownershipTargetId: targetId);
            eventStreamService.Append(new OperatorOsEventRecord
            {
                EventKind = OperatorOsEventKind.OwnershipClaimed,
                RepoId = actorSession.RepoId,
                ActorSessionId = actorSession.ActorSessionId,
                ActorKind = actorSession.Kind,
                ActorIdentity = actorSession.ActorIdentity,
                OwnershipScope = scope,
                OwnershipTargetId = targetId,
                ReferenceId = binding.BindingId,
                ReasonCode = "ownership_claimed",
                Summary = reason,
            });
            return new OwnershipDecision
            {
                Allowed = true,
                Outcome = OwnershipDecisionOutcome.Granted,
                Summary = reason,
                ReasonCode = "ownership_claimed",
                Binding = binding,
            };
        }

        if (string.Equals(binding.OwnerActorSessionId, actorSession.ActorSessionId, StringComparison.Ordinal))
        {
            binding.Touch(reason);
            repository.Save(new OwnershipSnapshot
            {
                Bindings = bindings.OrderBy(item => item.Scope).ThenBy(item => item.TargetId, StringComparer.Ordinal).ToArray(),
            });
            actorSessionService.MarkState(actorSession.ActorSessionId, actorSession.State, reason, ownershipScope: scope, ownershipTargetId: targetId);
            return new OwnershipDecision
            {
                Allowed = true,
                Outcome = OwnershipDecisionOutcome.Granted,
                Summary = reason,
                ReasonCode = "ownership_retained",
                Binding = binding,
            };
        }

        var outcome = scope switch
        {
            OwnershipScope.ApprovalDecision or OwnershipScope.WorkerInterruption or OwnershipScope.PlannerControl => OwnershipDecisionOutcome.Escalated,
            OwnershipScope.TaskMutation => OwnershipDecisionOutcome.Deferred,
            _ => OwnershipDecisionOutcome.Denied,
        };
        return new OwnershipDecision
        {
            Allowed = false,
            Outcome = outcome,
            Summary = $"Ownership for {scope}/{targetId} is currently occupied by {binding.OwnerKind}:{binding.OwnerIdentity}.",
            ReasonCode = outcome switch
            {
                OwnershipDecisionOutcome.Escalated => "ownership_conflict_escalated",
                OwnershipDecisionOutcome.Deferred => "ownership_conflict_deferred",
                _ => "ownership_conflict_denied",
            },
            Binding = binding,
            ExistingOwnerActorSessionId = binding.OwnerActorSessionId,
            ExistingOwnerKind = binding.OwnerKind,
            ExistingOwnerIdentity = binding.OwnerIdentity,
        };
    }

    public void Release(OwnershipScope scope, string targetId, string reason)
    {
        var snapshot = repository.Load();
        var bindings = snapshot.Bindings.ToList();
        var binding = bindings.FirstOrDefault(item => item.Scope == scope && string.Equals(item.TargetId, targetId, StringComparison.Ordinal));
        if (binding is null)
        {
            return;
        }

        bindings.Remove(binding);
        repository.Save(new OwnershipSnapshot
        {
            Bindings = bindings.OrderBy(item => item.Scope).ThenBy(item => item.TargetId, StringComparer.Ordinal).ToArray(),
        });
        actorSessionService.ClearOwnership(binding.OwnerActorSessionId, reason);
        eventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.OwnershipReleased,
            RepoId = actorSessionService.TryGet(binding.OwnerActorSessionId)?.RepoId ?? string.Empty,
            ActorSessionId = binding.OwnerActorSessionId,
            ActorKind = binding.OwnerKind,
            ActorIdentity = binding.OwnerIdentity,
            OwnershipScope = scope,
            OwnershipTargetId = targetId,
            ReferenceId = binding.BindingId,
            ReasonCode = "ownership_released",
            Summary = reason,
        });
    }

    public IReadOnlyList<OwnershipBinding> List(OwnershipScope? scope = null, string? targetId = null)
    {
        return repository.Load().Bindings
            .Where(item => scope is null || item.Scope == scope.Value)
            .Where(item => string.IsNullOrWhiteSpace(targetId) || string.Equals(item.TargetId, targetId, StringComparison.Ordinal))
            .OrderBy(item => item.Scope)
            .ThenBy(item => item.TargetId, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool CanOwn(ActorSessionKind kind, OwnershipScope scope)
    {
        return scope switch
        {
            OwnershipScope.TaskMutation => kind is ActorSessionKind.Worker or ActorSessionKind.Operator or ActorSessionKind.Agent,
            OwnershipScope.WorkerInterruption => kind == ActorSessionKind.Operator,
            OwnershipScope.ApprovalDecision => kind is ActorSessionKind.Operator or ActorSessionKind.Agent,
            OwnershipScope.PlannerControl => kind is ActorSessionKind.Planner or ActorSessionKind.Operator or ActorSessionKind.Agent,
            OwnershipScope.RuntimeControl => kind == ActorSessionKind.Operator,
            _ => false,
        };
    }
}
