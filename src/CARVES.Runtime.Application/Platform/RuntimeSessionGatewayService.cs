using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeSessionGatewayService
{
    private const string StrictBrokerMode = "strict_broker";
    private const string RuntimeEmbeddedAuthority = "runtime_embedded";
    private const string RuntimeControlKernelAuthority = "runtime_control_kernel";
    private const string SessionGatewayOperationClass = "session_gateway";
    private const string SessionGatewayActorIdentity = "session-gateway";
    private const string TurnPostureIntentLaneGuardNotRequired = "not_required";
    private const string TurnPostureIntentLaneGuardSuppressed = "suppressed";
    private const string TurnPostureGuidanceStateNone = "none";
    private const string TurnPostureGuidanceStateActiveSession = "active_session";
    private const string TurnPostureGuidanceStateLiveStateRestored = "live_state_restored";
    private const string TurnPostureGuidanceStateStale = "stale";
    private const string TurnPostureGuidanceStateCleared = "cleared";
    private const string TurnPostureGuidanceStateForgotten = "forgotten";
    private const int TurnPostureObservationMaxIssues = 8;
    private const int TurnPostureObservationMaxEvidence = 24;
    private const int TurnPosturePersistedSuppressionKeyLimit = 32;
    // Runtime guidance live-state is a short continuity hint; older state is ignored and cleaned up.
    private static readonly TimeSpan TurnPostureLiveStateTtl = TimeSpan.FromDays(7);
    private static readonly Regex ConversationControlSanitizer = new(@"[^\p{L}\p{N}\s]+", RegexOptions.Compiled);
    private static readonly Regex ConversationWhitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions TurnPostureStateJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly RuntimeTurnPostureGuidance TurnPostureGuidance = new();
    private static readonly RuntimeTurnStyleProfileLoader TurnStyleProfileLoader = new();
    private static readonly RuntimeTurnAwarenessProfileLoader TurnAwarenessProfileLoader = new();
    private static readonly RuntimeTurnAwarenessPolicyCompiler TurnAwarenessPolicyCompiler = new();
    private readonly ActorSessionService actorSessionService;
    private readonly OperatorOsEventStreamService operatorOsEventStreamService;
    private readonly Func<string?> runtimeSessionIdProvider;
    private readonly EffectLedgerService effectLedgerService;
    private readonly ResourceLeaseService resourceLeaseService;
    private readonly ExternalModuleReceiptService externalModuleReceiptService;
    private readonly PrivilegedWorkOrderService privilegedWorkOrderService;
    private readonly IPrivilegedActorRoleResolver privilegedActorRoleResolver;
    private readonly Func<RoleGovernanceRuntimePolicy> roleGovernancePolicyProvider;
    private readonly TypedExecutionCoreService typedExecutionCoreService;
    private readonly Dictionary<string, HashSet<string>> turnPostureSuppressionKeysBySessionId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, RuntimeGuidanceCandidate>> guidanceCandidatesBySessionId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> activeGuidanceCandidateIdsBySessionId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> turnPostureSessionLanguagesBySessionId = new(StringComparer.Ordinal);
    private readonly HashSet<string> loadedTurnPostureStateSessionIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> restoredGuidanceCandidateIdsBySessionId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> staleGuidanceCandidateIdsBySessionId = new(StringComparer.Ordinal);
    private readonly ControlPlanePaths paths;
    private readonly string repoId;

    public RuntimeSessionGatewayService(
        string repoRoot,
        ActorSessionService actorSessionService,
        OperatorOsEventStreamService operatorOsEventStreamService,
        Func<string?> runtimeSessionIdProvider,
        IPrivilegedActorRoleResolver? privilegedActorRoleResolver = null,
        Func<RoleGovernanceRuntimePolicy>? roleGovernancePolicyProvider = null)
    {
        this.actorSessionService = actorSessionService;
        this.operatorOsEventStreamService = operatorOsEventStreamService;
        this.runtimeSessionIdProvider = runtimeSessionIdProvider;
        this.roleGovernancePolicyProvider = roleGovernancePolicyProvider ?? RoleGovernanceRuntimePolicy.CreateDefault;
        paths = ControlPlanePaths.FromRepoRoot(repoRoot);
        effectLedgerService = new EffectLedgerService(paths);
        resourceLeaseService = new ResourceLeaseService(paths);
        externalModuleReceiptService = new ExternalModuleReceiptService(paths);
        privilegedWorkOrderService = new PrivilegedWorkOrderService(paths);
        this.privilegedActorRoleResolver = privilegedActorRoleResolver ?? new PrivilegedActorRolePolicyService(paths);
        typedExecutionCoreService = new TypedExecutionCoreService();
        repoId = ResolveRepoId(repoRoot);
    }

    public SessionGatewaySessionSurface CreateOrResumeSession(SessionGatewaySessionCreateRequest request)
    {
        var requestedSessionId = NormalizeOptional(request.SessionId);
        var existing = string.IsNullOrWhiteSpace(requestedSessionId)
            ? null
            : actorSessionService.TryGet(requestedSessionId);
        var actorIdentity = NormalizeOptional(request.ActorIdentity)
            ?? existing?.ActorIdentity
            ?? SessionGatewayActorIdentity;
        var reason = existing is null
            ? "Session Gateway session created."
            : "Session Gateway session resumed.";
        var session = actorSessionService.Ensure(
            ActorSessionKind.Agent,
            actorIdentity,
            repoId,
            reason,
            actorSessionId: requestedSessionId,
            runtimeSessionId: runtimeSessionIdProvider(),
            operationClass: SessionGatewayOperationClass,
            operation: existing is null ? "create_session" : "resume_session");

        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewaySessionCreated,
            RepoId = session.RepoId,
            ActorSessionId = session.ActorSessionId,
            ActorKind = session.Kind,
            ActorIdentity = session.ActorIdentity,
            ReferenceId = session.ActorSessionId,
            ReasonCode = "session_gateway_session_created",
            Summary = reason,
        });

        return ProjectSession(session);
    }

    public SessionGatewaySessionSurface? TryGetSession(string sessionId)
    {
        var session = actorSessionService.TryGet(sessionId);
        return session is null ? null : ProjectSession(session);
    }

    public SessionGatewayTurnSurface SubmitMessage(string sessionId, SessionGatewayMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserText))
        {
            throw new InvalidOperationException("Session Gateway messages require user_text.");
        }

        var session = actorSessionService.MarkState(
            sessionId,
            ActorSessionState.Active,
            "Session Gateway message accepted.",
            runtimeSessionId: runtimeSessionIdProvider(),
            operationClass: SessionGatewayOperationClass,
            operation: "submit_message");
        var messageId = NormalizeOptional(request.MessageId) ?? $"message-{Guid.NewGuid():N}";
        var turnId = $"turn-{Guid.NewGuid():N}";
        var roleGovernancePolicy = LoadRoleGovernancePolicyForTurn();
        var classifiedIntent = ClassifyIntent(request, roleGovernancePolicy);
        var intentEnvelope = BuildIntentEnvelope(request, classifiedIntent, roleGovernancePolicy);
        var workOrderDryRun = BuildWorkOrderDryRun(session, request, intentEnvelope);
        var turnPosture = BuildTurnPosture(session.ActorSessionId, request, classifiedIntent, messageId);
        RecordTurnPostureSuppression(session.ActorSessionId, turnPosture);
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayTurnAccepted,
            RepoId = session.RepoId,
            ActorSessionId = session.ActorSessionId,
            ActorKind = session.Kind,
            ActorIdentity = session.ActorIdentity,
            RunId = messageId,
            ReferenceId = turnId,
            ReasonCode = "session_gateway_turn_accepted",
            Summary = $"Accepted Session Gateway turn {turnId}.",
        });
        operatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.SessionGatewayTurnClassified,
            RepoId = session.RepoId,
            ActorSessionId = session.ActorSessionId,
            ActorKind = session.Kind,
            ActorIdentity = session.ActorIdentity,
            RunId = messageId,
            ReferenceId = turnId,
            ReasonCode = $"session_gateway_turn_classified:{classifiedIntent}",
            Summary = $"Classified Session Gateway turn {turnId} as {classifiedIntent}.",
        });

        var operationId = AllowsGatewayOperationBinding(workOrderDryRun)
            ? BindGovernedOperation(session.ActorSessionId, turnId, messageId, request, classifiedIntent)
            : null;

        return new SessionGatewayTurnSurface
        {
            SessionId = session.ActorSessionId,
            TurnId = turnId,
            MessageId = messageId,
            ClassifiedIntent = classifiedIntent,
            OperationId = operationId,
            NextProjectionHint = ResolveProjectionHint(classifiedIntent),
            BrokerMode = StrictBrokerMode,
            RouteAuthority = RuntimeControlKernelAuthority,
            IntentEnvelope = intentEnvelope,
            TurnPosture = turnPosture,
            WorkOrderDryRun = workOrderDryRun,
        };
    }

    public SessionGatewayEventsSurface GetEvents(string sessionId)
    {
        _ = actorSessionService.TryGet(sessionId)
            ?? throw new InvalidOperationException($"Session Gateway session '{sessionId}' was not found.");
        var events = LoadGatewayEvents(sessionId)
            .OrderBy(record => record.OccurredAt)
            .Select(ProjectEvent)
            .ToArray();
        return new SessionGatewayEventsSurface
        {
            SessionId = sessionId,
            Events = events,
        };
    }

    public EffectLedgerReplayResult ReplayWorkOrderEffectLedger(string workOrderId)
    {
        return effectLedgerService.ReplayWorkOrder(workOrderId);
    }

    private SessionGatewaySessionSurface ProjectSession(ActorSessionRecord session)
    {
        var gatewayEvents = LoadGatewayEvents(session.ActorSessionId).ToArray();
        var latestClassification = gatewayEvents
            .Select(record => TryReadClassifiedIntent(record.ReasonCode))
            .FirstOrDefault(intent => !string.IsNullOrWhiteSpace(intent));

        return new SessionGatewaySessionSurface
        {
            SessionId = session.ActorSessionId,
            ActorSessionId = session.ActorSessionId,
            ActorIdentity = session.ActorIdentity,
            RepoId = session.RepoId,
            BrokerMode = StrictBrokerMode,
            SessionAuthority = RuntimeEmbeddedAuthority,
            State = session.State.ToString().ToLowerInvariant(),
            RuntimeSessionId = session.RuntimeSessionId,
            LastOperationClass = session.LastOperationClass,
            LastOperation = session.LastOperation,
            LastReason = session.LastReason,
            LatestClassifiedIntent = latestClassification,
            EventCount = gatewayEvents.Length,
        };
    }

    private IEnumerable<OperatorOsEventRecord> LoadGatewayEvents(string sessionId)
    {
        return operatorOsEventStreamService.Load(actorSessionId: sessionId)
            .Where(record => IsSessionGatewayEvent(record.EventKind));
    }

    private RoleGovernanceRuntimePolicy LoadRoleGovernancePolicyForTurn()
    {
        try
        {
            return roleGovernancePolicyProvider();
        }
        catch
        {
            return RoleGovernanceRuntimePolicy.CreateDefault();
        }
    }

    private SessionGatewayEventSurface ProjectEvent(OperatorOsEventRecord record)
    {
        var operationId = ResolveOperationId(record);
        var binding = !string.IsNullOrWhiteSpace(operationId)
            ? TryGetOperationBinding(operationId)
            : null;
        var turnId = binding?.TurnId;
        var messageId = binding?.MessageId ?? record.RunId;
        var requestedAction = binding?.RequestedAction ?? TryReadRequestedAction(record.ReasonCode);
        var (eventType, stage) = record.EventKind switch
        {
            OperatorOsEventKind.SessionGatewaySessionCreated => ("session.created", "accepted"),
            OperatorOsEventKind.SessionGatewayTurnAccepted => ("turn.accepted", "accepted"),
            OperatorOsEventKind.SessionGatewayTurnClassified => ("turn.classified", "classified"),
            OperatorOsEventKind.SessionGatewayOperationAccepted => ("operation.accepted", AcceptedStage),
            OperatorOsEventKind.SessionGatewayOperationProgressed => ("operation.progressed", TryReadProgressMarker(record.ReasonCode) ?? "progressed"),
            OperatorOsEventKind.SessionGatewayReviewResolved => ("review.resolved", TryReadReviewResolutionStage(record.ReasonCode)),
            OperatorOsEventKind.SessionGatewayReplanRequested => ("replan.requested", "requested"),
            OperatorOsEventKind.SessionGatewayReplanProjected => ("replan.projected", "projected"),
            OperatorOsEventKind.SessionGatewayOperationCompleted => ("operation.completed", CompletedStage),
            OperatorOsEventKind.SessionGatewayOperationFailed => ("operation.failed", FailedStage),
            OperatorOsEventKind.SessionGatewayOperatorActionRequired => ("operator.action_required", TryReadOperatorRequiredState(record.ReasonCode) ?? SessionGatewayOperatorWaitStates.WaitingOperatorSetup),
            OperatorOsEventKind.SessionGatewayOperatorProjectRequired => ("operator.project_required", TryReadOperatorRequiredState(record.ReasonCode) ?? SessionGatewayOperatorWaitStates.WaitingOperatorSetup),
            OperatorOsEventKind.SessionGatewayOperatorEvidenceRequired => ("operator.evidence_required", TryReadOperatorRequiredState(record.ReasonCode) ?? SessionGatewayOperatorWaitStates.WaitingOperatorEvidence),
            OperatorOsEventKind.SessionGatewayRealWorldProofMissing => ("proof.real_world_missing", "blocked"),
            _ => ("projection.updated", "projected"),
        };

        var classifiedIntent = TryReadClassifiedIntent(record.ReasonCode);
        var projection = new JsonObject
        {
            ["broker_mode"] = StrictBrokerMode,
            ["route_authority"] = RuntimeControlKernelAuthority,
        };
        if (string.IsNullOrWhiteSpace(classifiedIntent))
        {
            classifiedIntent = binding?.ClassifiedIntent;
        }

        if (!string.IsNullOrWhiteSpace(classifiedIntent))
        {
            projection["classified_intent"] = classifiedIntent;
            projection["next_projection_hint"] = ResolveProjectionHint(classifiedIntent);
        }

        if (!string.IsNullOrWhiteSpace(record.TaskId))
        {
            projection["task_id"] = record.TaskId;
        }

        if (!string.IsNullOrWhiteSpace(requestedAction))
        {
            projection["requested_action"] = requestedAction;
        }

        if (!string.IsNullOrWhiteSpace(operationId))
        {
            projection["operation_id"] = operationId;
        }

        var operatorRequiredState = TryReadOperatorRequiredState(record.ReasonCode);
        if (!string.IsNullOrWhiteSpace(operatorRequiredState))
        {
            projection["operator_required_state"] = operatorRequiredState;
        }

        var proofSource = TryReadProofSource(record.ReasonCode);
        if (!string.IsNullOrWhiteSpace(proofSource))
        {
            projection["proof_source"] = proofSource;
        }

        return new SessionGatewayEventSurface
        {
            EventId = record.EventId,
            EventType = eventType,
            RecordedAt = record.OccurredAt,
            SessionId = record.ActorSessionId ?? string.Empty,
            TurnId = turnId ?? (record.EventKind is OperatorOsEventKind.SessionGatewayTurnAccepted or OperatorOsEventKind.SessionGatewayTurnClassified ? record.ReferenceId : null),
            MessageId = messageId,
            OperationId = operationId,
            Stage = stage,
            Summary = record.Summary,
            Projection = projection,
        };
    }

    private SessionGatewayTurnPostureSurface BuildTurnPosture(
        string sessionId,
        SessionGatewayMessageRequest request,
        string classifiedIntent,
        string messageId)
    {
        EnsureTurnPostureStateLoaded(sessionId);
        var styleProfileLoad = TurnStyleProfileLoader.Load(request.ClientCapabilities);
        var awarenessProfileLoad = LoadTurnAwarenessProfile(request.ClientCapabilities, styleProfileLoad);
        var requestedCandidateId = ResolveTurnPostureCandidateId(request);
        var sessionLanguage = ResolveTurnPostureSessionLanguage(sessionId);
        RecordTurnPostureSessionLanguage(sessionId, request.UserText, sessionLanguage);
        if (TryResolveTurnPostureLifecycleControl(request.UserText, out var lifecycleState))
        {
            ClearGuidanceLiveState(sessionId, requestedCandidateId);
            return BuildGuidanceLifecycleControlTurnPosture(
                request.UserText,
                styleProfileLoad,
                awarenessProfileLoad,
                lifecycleState,
                sessionLanguage,
                requestedCandidateId);
        }

        var existingGuidanceCandidate = TryGetGuidanceCandidate(sessionId, requestedCandidateId);
        var existingGuidanceStateStatus = ResolveTurnPostureGuidanceStateStatus(sessionId, existingGuidanceCandidate, requestedCandidateId);
        var candidateId = requestedCandidateId ?? existingGuidanceCandidate?.CandidateId;
        var candidateRevisionHash = ResolveTurnPostureCandidateRevisionHash(existingGuidanceCandidate, candidateId);
        if (IsTurnPostureReopen(request.UserText))
        {
            ClearTurnPostureSuppression(sessionId, candidateId);
        }

        var candidateAlreadyParked = !IsTurnPostureMaterialCandidateChange(request.UserText)
            && IsTurnPostureCandidateParked(sessionId, candidateId, candidateRevisionHash);
        var guidance = TurnPostureGuidance.Build(new RuntimeTurnPostureInput(
            request.UserText,
            StyleProfileId: styleProfileLoad.EffectiveProfile.StyleProfileId,
            CandidateAlreadyParked: candidateAlreadyParked,
            CandidateId: candidateId,
            StyleProfile: styleProfileLoad.ProfileProvided ? styleProfileLoad.EffectiveProfile : null,
            SourceTurnRef: messageId,
            ExistingGuidanceCandidate: existingGuidanceCandidate,
            CandidateRevisionHash: candidateRevisionHash,
            AwarenessProfile: awarenessProfileLoad.ProfileProvided ? awarenessProfileLoad.EffectiveProfile : null,
            SessionLanguage: sessionLanguage));
        if (ShouldSuppressTurnPostureGuidance(classifiedIntent, guidance))
        {
            return BuildIntentLaneGuardedTurnPosture(
                request.UserText,
                classifiedIntent,
                styleProfileLoad,
                awarenessProfileLoad,
                existingGuidanceStateStatus,
                sessionLanguage,
                guidance.AdmissionSignals);
        }

        var projectionFields = new Dictionary<string, string>(
            guidance.TurnProjectionFields,
            StringComparer.Ordinal);
        var guidanceStateStatus = ResolveTurnPostureGuidanceStateStatus(sessionId, existingGuidanceCandidate, guidance, requestedCandidateId);
        var fieldDiagnostics = guidance.FieldDiagnostics?.ToSurfaceSafe();

        var result = new SessionGatewayTurnPostureSurface
        {
            ProjectionFields = projectionFields,
            Observation = BuildTurnPostureObservation(
                projectionFields,
                ResolveTurnPostureActiveProfileStatus(styleProfileLoad, awarenessProfileLoad),
                TurnPostureIntentLaneGuardNotRequired,
                guardReason: null,
                ResolveTurnPostureActiveProfileIssues(styleProfileLoad, awarenessProfileLoad),
                guidance.ShouldGuide,
                guidance.PromptSuppressed,
                guidanceStateStatus,
                guidance.IntentDraftCandidate is not null,
                fieldDiagnostics,
                guidance.AdmissionSignals),
            GuidanceStateStatus = guidanceStateStatus,
            ShouldGuide = guidance.ShouldGuide,
            DirectionSummary = guidance.DirectionSummary,
            Question = guidance.Question,
            RecommendedNextAction = guidance.RecommendedNextAction,
            SynthesizedResponse = guidance.SynthesizedResponse,
            GuidanceCandidate = guidance.GuidanceCandidate,
            IntentDraftCandidate = guidance.IntentDraftCandidate,
            AwarenessPolicy = guidance.AwarenessPolicy,
            LanguageResolution = guidance.LanguageResolution,
            AwarenessPolicyStatus = ResolveTurnPostureAwarenessPolicyStatus(awarenessProfileLoad, guidance.AwarenessPolicyStatus),
            AwarenessPolicyIssues = ResolveTurnPostureAwarenessPolicyIssues(awarenessProfileLoad, guidance.AwarenessPolicyIssues),
            FieldDiagnostics = fieldDiagnostics,
            AdmissionSignals = guidance.AdmissionSignals,
            PromptSuppressed = guidance.PromptSuppressed,
            SuppressionKey = guidance.SuppressionKey,
            IntentLaneGuard = TurnPostureIntentLaneGuardNotRequired,
            StyleProfileStatus = styleProfileLoad.Status,
            StyleProfileSource = styleProfileLoad.Source,
            StyleProfileRevisionHash = styleProfileLoad.EffectiveProfile.RevisionHash,
            StyleProfileIssues = styleProfileLoad.Issues,
            AwarenessProfileStatus = awarenessProfileLoad.Status,
            AwarenessProfileSource = awarenessProfileLoad.Source,
            AwarenessProfileRevisionHash = awarenessProfileLoad.EffectiveProfile.RevisionHash,
            AwarenessProfileIssues = awarenessProfileLoad.Issues,
            ShouldLoadPostureRegistryProse = guidance.ShouldLoadPostureRegistryProse,
            ShouldLoadCustomPersonalityNotes = guidance.ShouldLoadCustomPersonalityNotes,
        };
        RecordGuidanceCandidate(sessionId, result);
        return result;
    }

    private static RuntimeTurnAwarenessProfileLoadResult LoadTurnAwarenessProfile(
        JsonObject? capabilities,
        RuntimeTurnStyleProfileLoadResult styleProfileLoad)
    {
        var fallback = styleProfileLoad.ProfileProvided
            ? RuntimeTurnAwarenessProfile.FromStyleProfile(styleProfileLoad.EffectiveProfile)
            : null;
        return TurnAwarenessProfileLoader.Load(capabilities, fallback);
    }

    private static RuntimeTurnStyleProfile ResolveTurnPostureProjectionStyleProfile(
        RuntimeTurnStyleProfileLoadResult styleProfileLoad,
        RuntimeTurnAwarenessProfileLoadResult awarenessProfileLoad)
    {
        return awarenessProfileLoad.ProfileProvided
            ? awarenessProfileLoad.EffectiveProfile.ToStyleProfile()
            : styleProfileLoad.EffectiveProfile;
    }

    private static string ResolveTurnPostureActiveProfileStatus(
        RuntimeTurnStyleProfileLoadResult styleProfileLoad,
        RuntimeTurnAwarenessProfileLoadResult awarenessProfileLoad)
    {
        return awarenessProfileLoad.ProfileProvided ? awarenessProfileLoad.Status : styleProfileLoad.Status;
    }

    private static IReadOnlyList<string> ResolveTurnPostureActiveProfileIssues(
        RuntimeTurnStyleProfileLoadResult styleProfileLoad,
        RuntimeTurnAwarenessProfileLoadResult awarenessProfileLoad)
    {
        return awarenessProfileLoad.ProfileProvided ? awarenessProfileLoad.Issues : styleProfileLoad.Issues;
    }

    private static SessionGatewayTurnPostureObservationSurface BuildTurnPostureObservation(
        IReadOnlyDictionary<string, string> projectionFields,
        string profileStatus,
        string guardStatus,
        string? guardReason,
        IReadOnlyList<string> profileIssues,
        bool shouldGuide,
        bool promptSuppressed,
        string guidanceStateStatus,
        bool hasIntentDraftCandidate,
        RuntimeGuidanceFieldCollectionDiagnostics? fieldDiagnostics,
        RuntimeGuidanceAdmissionClassifierResult? admissionSignals)
    {
        var turnClass = projectionFields.GetValueOrDefault(RuntimeTurnPostureFields.TurnClass)
            ?? RuntimeTurnPostureCodes.TurnClass.OrdinaryNoOp;
        var issueCodes = BuildTurnPostureObservationIssueCodes(profileIssues, guardReason);

        return new SessionGatewayTurnPostureObservationSurface
        {
            DecisionPath = ResolveTurnPostureDecisionPath(turnClass, guardStatus, promptSuppressed, shouldGuide),
            ProfileStatus = profileStatus,
            GuardStatus = guardStatus,
            GuidanceStateStatus = guidanceStateStatus,
            PromptSuppressed = promptSuppressed,
            ShouldGuide = shouldGuide,
            ProjectionFieldCount = projectionFields.Count,
            IssueCodes = issueCodes,
            EvidenceCodes = BuildTurnPostureObservationEvidenceCodes(
                projectionFields,
                profileStatus,
                guardStatus,
                promptSuppressed,
                guidanceStateStatus,
                hasIntentDraftCandidate,
                fieldDiagnostics,
                admissionSignals),
        };
    }

    private static IReadOnlyList<string> BuildTurnPostureObservationIssueCodes(
        IReadOnlyList<string> profileIssues,
        string? guardReason)
    {
        var issues = new List<string>();
        foreach (var issue in profileIssues)
        {
            AddTurnPostureObservationCode(issues, issue, TurnPostureObservationMaxIssues);
        }

        AddTurnPostureObservationCode(issues, guardReason, TurnPostureObservationMaxIssues);
        return issues;
    }

    private static IReadOnlyList<string> BuildTurnPostureObservationEvidenceCodes(
        IReadOnlyDictionary<string, string> projectionFields,
        string profileStatus,
        string guardStatus,
        bool promptSuppressed,
        string guidanceStateStatus,
        bool hasIntentDraftCandidate,
        RuntimeGuidanceFieldCollectionDiagnostics? fieldDiagnostics,
        RuntimeGuidanceAdmissionClassifierResult? admissionSignals)
    {
        var evidence = new List<string>();
        AddTurnPostureProjectionEvidence(evidence, projectionFields, RuntimeTurnPostureFields.TurnClass);
        AddTurnPostureProjectionEvidence(evidence, projectionFields, RuntimeTurnPostureFields.IntentLane);
        AddTurnPostureProjectionEvidence(evidence, projectionFields, RuntimeTurnPostureFields.Readiness);
        AddTurnPostureProjectionEvidence(evidence, projectionFields, RuntimeTurnPostureFields.NextAct);
        AddTurnPostureProjectionEvidence(evidence, projectionFields, RuntimeTurnPostureFields.HostRouteHint);
        AddTurnPostureProjectionEvidence(evidence, projectionFields, RuntimeTurnPostureFields.ResponseLanguage);
        AddTurnPostureProjectionEvidence(evidence, projectionFields, RuntimeTurnPostureFields.LanguageResolutionSource);
        AddTurnPostureProjectionEvidence(evidence, projectionFields, RuntimeTurnPostureFields.SessionLanguage);
        AddTurnPostureProjectionEvidence(evidence, projectionFields, RuntimeTurnPostureFields.AwarenessResponseOrder);
        AddTurnPostureProjectionEvidence(evidence, projectionFields, RuntimeTurnPostureFields.AwarenessCorrectionMode);
        AddTurnPostureProjectionEvidence(evidence, projectionFields, RuntimeTurnPostureFields.AwarenessEvidenceMode);
        if (projectionFields.ContainsKey(RuntimeTurnPostureFields.Blockers))
        {
            AddTurnPostureObservationCode(evidence, "blockers:present", TurnPostureObservationMaxEvidence);
        }
        AddTurnPostureObservationCode(evidence, $"profile_status:{profileStatus}", TurnPostureObservationMaxEvidence);
        AddTurnPostureObservationCode(evidence, $"guard_status:{guardStatus}", TurnPostureObservationMaxEvidence);
        AddTurnPostureObservationCode(evidence, $"guidance_state:{guidanceStateStatus}", TurnPostureObservationMaxEvidence);
        AddTurnPostureAdmissionEvidence(evidence, admissionSignals);
        if (hasIntentDraftCandidate)
        {
            AddTurnPostureObservationCode(evidence, "intent_draft_candidate:present", TurnPostureObservationMaxEvidence);
        }
        if (fieldDiagnostics is not null)
        {
            AddTurnPostureObservationCode(evidence, "field_diagnostics:present", TurnPostureObservationMaxEvidence);
            if (fieldDiagnostics.ResetsCandidate)
            {
                AddTurnPostureObservationCode(evidence, "field_reset:true", TurnPostureObservationMaxEvidence);
            }

            if (fieldDiagnostics.Ambiguities.Count > 0)
            {
                AddTurnPostureObservationCode(evidence, "field_ambiguity:present", TurnPostureObservationMaxEvidence);
            }
        }
        if (promptSuppressed)
        {
            AddTurnPostureObservationCode(evidence, "prompt_suppressed:true", TurnPostureObservationMaxEvidence);
        }
        if (projectionFields.ContainsKey(RuntimeTurnPostureFields.SuppressionKey))
        {
            AddTurnPostureObservationCode(evidence, "suppression_key:present", TurnPostureObservationMaxEvidence);
        }
        if (projectionFields.ContainsKey(RuntimeTurnPostureFields.FocusCodes))
        {
            AddTurnPostureObservationCode(evidence, "focus_codes:present", TurnPostureObservationMaxEvidence);
        }
        AddTurnPostureProjectionTrueEvidence(evidence, projectionFields, RuntimeTurnPostureFields.LanguageExplicitOverride);
        AddTurnPostureProjectionTrueEvidence(evidence, projectionFields, RuntimeTurnPostureFields.LanguageDetectedChinese);

        return evidence;
    }

    private static void AddTurnPostureAdmissionEvidence(
        List<string> evidence,
        RuntimeGuidanceAdmissionClassifierResult? admissionSignals)
    {
        if (admissionSignals is null)
        {
            return;
        }

        AddTurnPostureObservationCode(evidence, $"admission_kind:{admissionSignals.RecommendedKind}", TurnPostureObservationMaxEvidence);
        AddTurnPostureObservationCode(evidence, $"admission_score:{admissionSignals.CandidateSignalScore}", TurnPostureObservationMaxEvidence);
        if (admissionSignals.HasAwarenessGuidanceSignal)
        {
            AddTurnPostureObservationCode(evidence, "admission_awareness:true", TurnPostureObservationMaxEvidence);
        }

        foreach (var evidenceCode in admissionSignals.EvidenceCodes.Take(3))
        {
            AddTurnPostureObservationCode(evidence, $"admission_evidence:{evidenceCode}", TurnPostureObservationMaxEvidence);
        }

        foreach (var reasonCode in admissionSignals.DecisionReasonCodes.Take(3))
        {
            AddTurnPostureObservationCode(evidence, $"admission_reason:{reasonCode}", TurnPostureObservationMaxEvidence);
        }
    }

    private static void AddTurnPostureProjectionEvidence(
        List<string> evidence,
        IReadOnlyDictionary<string, string> projectionFields,
        string fieldName)
    {
        if (!projectionFields.TryGetValue(fieldName, out var value))
        {
            return;
        }

        AddTurnPostureObservationCode(evidence, $"{fieldName}:{value}", TurnPostureObservationMaxEvidence);
    }

    private static void AddTurnPostureProjectionTrueEvidence(
        List<string> evidence,
        IReadOnlyDictionary<string, string> projectionFields,
        string fieldName)
    {
        if (!projectionFields.TryGetValue(fieldName, out var value)
            || !string.Equals(value, "true", StringComparison.Ordinal))
        {
            return;
        }

        AddTurnPostureObservationCode(evidence, $"{fieldName}:true", TurnPostureObservationMaxEvidence);
    }

    private static void AddTurnPostureObservationCode(
        List<string> codes,
        string? code,
        int maxCount)
    {
        if (codes.Count >= maxCount)
        {
            return;
        }

        var normalized = NormalizeTurnPostureObservationCode(code);
        if (normalized is null || codes.Contains(normalized, StringComparer.Ordinal))
        {
            return;
        }

        codes.Add(normalized);
    }

    private static string? NormalizeTurnPostureObservationCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var trimmed = code.Trim();
        if (trimmed.Length > 96)
        {
            return null;
        }

        foreach (var character in trimmed)
        {
            if (!IsTurnPostureObservationCodeCharacter(character))
            {
                return null;
            }
        }

        return trimmed;
    }

    private static bool IsTurnPostureObservationCodeCharacter(char character)
    {
        return character is (>= 'a' and <= 'z')
            or (>= 'A' and <= 'Z')
            or (>= '0' and <= '9')
            or '_'
            or '-'
            or '.'
            or ':';
    }

    private static string ResolveTurnPostureDecisionPath(
        string turnClass,
        string guardStatus,
        bool promptSuppressed,
        bool shouldGuide)
    {
        if (string.Equals(guardStatus, TurnPostureIntentLaneGuardSuppressed, StringComparison.Ordinal))
        {
            return "intent_lane_guarded";
        }

        if (promptSuppressed)
        {
            return "prompt_suppressed";
        }

        if (string.Equals(turnClass, RuntimeTurnPostureCodes.TurnClass.Parked, StringComparison.Ordinal))
        {
            return "parked";
        }

        return shouldGuide ? "guided_collection" : "ordinary_no_op";
    }

    private static SessionGatewayTurnPostureSurface BuildGuidanceLifecycleControlTurnPosture(
        string userText,
        RuntimeTurnStyleProfileLoadResult styleProfileLoad,
        RuntimeTurnAwarenessProfileLoadResult awarenessProfileLoad,
        string guidanceStateStatus,
        string? sessionLanguage,
        string? candidateId = null)
    {
        var projectionStyleProfile = ResolveTurnPostureProjectionStyleProfile(styleProfileLoad, awarenessProfileLoad);
        var baseProjectionFields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTurnPostureFields.TurnClass] = RuntimeTurnPostureCodes.TurnClass.OrdinaryNoOp,
            [RuntimeTurnPostureFields.IntentLane] = RuntimeTurnPostureCodes.IntentLane.None,
            [RuntimeTurnPostureFields.PostureId] = RuntimeTurnPostureCodes.Posture.None,
            [RuntimeTurnPostureFields.StyleProfileId] = projectionStyleProfile.StyleProfileId,
            [RuntimeTurnPostureFields.Readiness] = RuntimeTurnPostureCodes.Readiness.NoOp,
            [RuntimeTurnPostureFields.NextAct] = RuntimeTurnPostureCodes.NextAct.Answer,
            [RuntimeTurnPostureFields.HostRouteHint] = RuntimeTurnPostureCodes.HostRouteHint.None,
            [RuntimeTurnPostureFields.MaxQuestions] = "0",
        };
        if (styleProfileLoad.ProfileProvided || awarenessProfileLoad.ProfileProvided)
        {
            baseProjectionFields[RuntimeTurnPostureFields.RevisionHash] = projectionStyleProfile.RevisionHash;
        }
        if (NormalizeOptional(candidateId) is { } normalizedCandidateId)
        {
            baseProjectionFields[RuntimeTurnPostureFields.CandidateId] = normalizedCandidateId;
        }

        var awarenessPolicyCompile = BuildTurnPostureAwarenessPolicy(styleProfileLoad, awarenessProfileLoad);
        var awarenessPolicy = awarenessPolicyCompile.Policy;
        var languageResolution = ResolveTurnPostureLanguage(userText, awarenessPolicy, sessionLanguage);
        var projectionFields = BuildTurnPostureLanguageProjectionFields(
            BuildTurnPostureAwarenessPolicyProjectionFields(baseProjectionFields, awarenessPolicy),
            languageResolution);
        var admissionSignals = RuntimeGuidanceAdmission.ClassifySignals(
            userText,
            admissionHints: awarenessPolicy.AdmissionHints);
        return new SessionGatewayTurnPostureSurface
        {
            ProjectionFields = projectionFields,
            Observation = BuildTurnPostureObservation(
                projectionFields,
                ResolveTurnPostureActiveProfileStatus(styleProfileLoad, awarenessProfileLoad),
                TurnPostureIntentLaneGuardNotRequired,
                guardReason: null,
                ResolveTurnPostureActiveProfileIssues(styleProfileLoad, awarenessProfileLoad),
                shouldGuide: false,
                promptSuppressed: false,
                guidanceStateStatus,
                hasIntentDraftCandidate: false,
                fieldDiagnostics: null,
                admissionSignals),
            GuidanceStateStatus = guidanceStateStatus,
            ShouldGuide = false,
            AwarenessPolicy = awarenessPolicy,
            LanguageResolution = languageResolution,
            AwarenessPolicyStatus = ResolveTurnPostureAwarenessPolicyStatus(awarenessProfileLoad, awarenessPolicyCompile.Status),
            AwarenessPolicyIssues = ResolveTurnPostureAwarenessPolicyIssues(awarenessProfileLoad, awarenessPolicyCompile.Issues),
            PromptSuppressed = false,
            IntentLaneGuard = TurnPostureIntentLaneGuardNotRequired,
            StyleProfileStatus = styleProfileLoad.Status,
            StyleProfileSource = styleProfileLoad.Source,
            StyleProfileRevisionHash = styleProfileLoad.EffectiveProfile.RevisionHash,
            StyleProfileIssues = styleProfileLoad.Issues,
            AwarenessProfileStatus = awarenessProfileLoad.Status,
            AwarenessProfileSource = awarenessProfileLoad.Source,
            AwarenessProfileRevisionHash = awarenessProfileLoad.EffectiveProfile.RevisionHash,
            AwarenessProfileIssues = awarenessProfileLoad.Issues,
            AdmissionSignals = admissionSignals,
            ShouldLoadPostureRegistryProse = false,
            ShouldLoadCustomPersonalityNotes = false,
        };
    }

    private static bool ShouldSuppressTurnPostureGuidance(
        string classifiedIntent,
        RuntimeTurnPostureGuidanceResult guidance)
    {
        return string.Equals(
                guidance.TurnProjectionFields.GetValueOrDefault(RuntimeTurnPostureFields.TurnClass),
                RuntimeTurnPostureCodes.TurnClass.GuidedCollection,
                StringComparison.Ordinal)
            && !IsTurnPostureCollectableIntentLane(classifiedIntent);
    }

    private static bool IsTurnPostureCollectableIntentLane(string classifiedIntent)
    {
        return string.Equals(classifiedIntent, "discuss", StringComparison.Ordinal)
            || string.Equals(classifiedIntent, "plan", StringComparison.Ordinal);
    }

    private static SessionGatewayTurnPostureSurface BuildIntentLaneGuardedTurnPosture(
        string userText,
        string classifiedIntent,
        RuntimeTurnStyleProfileLoadResult styleProfileLoad,
        RuntimeTurnAwarenessProfileLoadResult awarenessProfileLoad,
        string guidanceStateStatus,
        string? sessionLanguage,
        RuntimeGuidanceAdmissionClassifierResult? admissionSignals)
    {
        var projectionStyleProfile = ResolveTurnPostureProjectionStyleProfile(styleProfileLoad, awarenessProfileLoad);
        var baseProjectionFields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTurnPostureFields.TurnClass] = RuntimeTurnPostureCodes.TurnClass.OrdinaryNoOp,
            [RuntimeTurnPostureFields.IntentLane] = RuntimeTurnPostureCodes.IntentLane.None,
            [RuntimeTurnPostureFields.PostureId] = RuntimeTurnPostureCodes.Posture.None,
            [RuntimeTurnPostureFields.StyleProfileId] = projectionStyleProfile.StyleProfileId,
            [RuntimeTurnPostureFields.Readiness] = RuntimeTurnPostureCodes.Readiness.NoOp,
            [RuntimeTurnPostureFields.NextAct] = RuntimeTurnPostureCodes.NextAct.Answer,
            [RuntimeTurnPostureFields.HostRouteHint] = RuntimeTurnPostureCodes.HostRouteHint.None,
            [RuntimeTurnPostureFields.MaxQuestions] = "0",
        };
        if (styleProfileLoad.ProfileProvided || awarenessProfileLoad.ProfileProvided)
        {
            baseProjectionFields[RuntimeTurnPostureFields.RevisionHash] = projectionStyleProfile.RevisionHash;
        }

        var awarenessPolicyCompile = BuildTurnPostureAwarenessPolicy(styleProfileLoad, awarenessProfileLoad);
        var awarenessPolicy = awarenessPolicyCompile.Policy;
        var languageResolution = ResolveTurnPostureLanguage(userText, awarenessPolicy, sessionLanguage);
        var projectionFields = BuildTurnPostureLanguageProjectionFields(
            BuildTurnPostureAwarenessPolicyProjectionFields(baseProjectionFields, awarenessPolicy),
            languageResolution);
        return new SessionGatewayTurnPostureSurface
        {
            ProjectionFields = projectionFields,
            Observation = BuildTurnPostureObservation(
                projectionFields,
                ResolveTurnPostureActiveProfileStatus(styleProfileLoad, awarenessProfileLoad),
                TurnPostureIntentLaneGuardSuppressed,
                $"non_collectable_intent_lane:{classifiedIntent}",
                ResolveTurnPostureActiveProfileIssues(styleProfileLoad, awarenessProfileLoad),
                shouldGuide: false,
                promptSuppressed: false,
                guidanceStateStatus,
                hasIntentDraftCandidate: false,
                fieldDiagnostics: null,
                admissionSignals),
            GuidanceStateStatus = guidanceStateStatus,
            ShouldGuide = false,
            AwarenessPolicy = awarenessPolicy,
            LanguageResolution = languageResolution,
            AwarenessPolicyStatus = ResolveTurnPostureAwarenessPolicyStatus(awarenessProfileLoad, awarenessPolicyCompile.Status),
            AwarenessPolicyIssues = ResolveTurnPostureAwarenessPolicyIssues(awarenessProfileLoad, awarenessPolicyCompile.Issues),
            PromptSuppressed = false,
            IntentLaneGuard = TurnPostureIntentLaneGuardSuppressed,
            IntentLaneGuardReason = $"non_collectable_intent_lane:{classifiedIntent}",
            StyleProfileStatus = styleProfileLoad.Status,
            StyleProfileSource = styleProfileLoad.Source,
            StyleProfileRevisionHash = styleProfileLoad.EffectiveProfile.RevisionHash,
            StyleProfileIssues = styleProfileLoad.Issues,
            AwarenessProfileStatus = awarenessProfileLoad.Status,
            AwarenessProfileSource = awarenessProfileLoad.Source,
            AwarenessProfileRevisionHash = awarenessProfileLoad.EffectiveProfile.RevisionHash,
            AwarenessProfileIssues = awarenessProfileLoad.Issues,
            AdmissionSignals = admissionSignals,
            ShouldLoadPostureRegistryProse = false,
            ShouldLoadCustomPersonalityNotes = false,
        };
    }

    private static RuntimeTurnAwarenessPolicyCompileResult BuildTurnPostureAwarenessPolicy(
        RuntimeTurnStyleProfileLoadResult styleProfileLoad,
        RuntimeTurnAwarenessProfileLoadResult awarenessProfileLoad)
    {
        return TurnAwarenessPolicyCompiler.Compile(
            ResolveTurnPostureAwarenessPolicyProfile(styleProfileLoad, awarenessProfileLoad));
    }

    private static RuntimeTurnAwarenessProfile ResolveTurnPostureAwarenessPolicyProfile(
        RuntimeTurnStyleProfileLoadResult styleProfileLoad,
        RuntimeTurnAwarenessProfileLoadResult awarenessProfileLoad)
    {
        if (awarenessProfileLoad.ProfileProvided)
        {
            return awarenessProfileLoad.EffectiveProfile;
        }

        var styleProfile = styleProfileLoad.ProfileProvided ? styleProfileLoad.EffectiveProfile : null;
        return RuntimeTurnAwarenessProfile.FromStyleProfile(styleProfile) with
        {
            LanguageMode = RuntimeTurnAwarenessProfileLanguageModes.Auto,
            ExpressionPolicy = new RuntimeTurnAwarenessExpressionPolicy(),
        };
    }

    private static string ResolveTurnPostureAwarenessPolicyStatus(
        RuntimeTurnAwarenessProfileLoadResult awarenessProfileLoad,
        string policyStatus)
    {
        return awarenessProfileLoad.ProfileProvided
            && string.Equals(awarenessProfileLoad.Status, "fallback", StringComparison.Ordinal)
                ? RuntimeTurnAwarenessPolicyStatuses.Fallback
                : policyStatus;
    }

    private static IReadOnlyList<string> ResolveTurnPostureAwarenessPolicyIssues(
        RuntimeTurnAwarenessProfileLoadResult awarenessProfileLoad,
        IReadOnlyList<string> policyIssues)
    {
        if (!awarenessProfileLoad.ProfileProvided
            || !string.Equals(awarenessProfileLoad.Status, "fallback", StringComparison.Ordinal)
            || awarenessProfileLoad.Issues.Count == 0)
        {
            return policyIssues;
        }

        return policyIssues
            .Concat(awarenessProfileLoad.Issues)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> BuildTurnPostureAwarenessPolicyProjectionFields(
        IReadOnlyDictionary<string, string> projectionFields,
        RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        return RuntimeTurnPostureGuidance.AddAwarenessPolicyProjectionFields(projectionFields, awarenessPolicy);
    }

    private static IReadOnlyDictionary<string, string> BuildTurnPostureLanguageProjectionFields(
        IReadOnlyDictionary<string, string> projectionFields,
        RuntimeTurnLanguageResolution languageResolution)
    {
        return RuntimeTurnPostureGuidance.AddLanguageProjectionFields(projectionFields, languageResolution);
    }

    private static RuntimeTurnLanguageResolution ResolveTurnPostureLanguage(
        string? userText,
        RuntimeTurnAwarenessPolicy awarenessPolicy,
        string? sessionLanguage)
    {
        return new RuntimeTurnLanguageResolver().Resolve(new RuntimeTurnLanguageResolutionInput(
            userText,
            awarenessPolicy.LanguageMode,
            sessionLanguage));
    }

    private static string? ResolveTurnPostureCandidateId(SessionGatewayMessageRequest request)
    {
        return NormalizeOptional(request.TargetCardId)
            ?? NormalizeOptional(request.TargetTaskGraphId)
            ?? NormalizeOptional(request.TargetTaskId);
    }

    private bool IsTurnPostureCandidateParked(string sessionId, string? candidateId, string? revisionHash)
    {
        var suppressionKey = BuildTurnPostureParkedSuppressionKey(candidateId, revisionHash);
        return turnPostureSuppressionKeysBySessionId.TryGetValue(sessionId, out var suppressionKeys)
            && suppressionKey is not null
            && suppressionKeys.Contains(suppressionKey);
    }

    private RuntimeGuidanceCandidate? TryGetGuidanceCandidate(string sessionId, string? candidateId)
    {
        if (!guidanceCandidatesBySessionId.TryGetValue(sessionId, out var candidates) || candidates.Count == 0)
        {
            return null;
        }

        var normalizedCandidateId = NormalizeOptional(candidateId)
            ?? activeGuidanceCandidateIdsBySessionId.GetValueOrDefault(sessionId);
        if (!string.IsNullOrWhiteSpace(normalizedCandidateId)
            && candidates.TryGetValue(normalizedCandidateId, out var targetCandidate))
        {
            return targetCandidate;
        }

        return candidates.Count == 1 ? candidates.Values.First() : null;
    }

    private string ResolveTurnPostureGuidanceStateStatus(
        string sessionId,
        RuntimeGuidanceCandidate? candidate,
        string? requestedCandidateId)
    {
        var candidateId = NormalizeOptional(candidate?.CandidateId)
            ?? NormalizeOptional(requestedCandidateId);
        if (HasGuidanceCandidateState(staleGuidanceCandidateIdsBySessionId, sessionId, candidateId))
        {
            return TurnPostureGuidanceStateStale;
        }

        if (candidate is null)
        {
            return TurnPostureGuidanceStateNone;
        }

        return HasGuidanceCandidateState(restoredGuidanceCandidateIdsBySessionId, sessionId, candidate.CandidateId)
            ? TurnPostureGuidanceStateLiveStateRestored
            : TurnPostureGuidanceStateActiveSession;
    }

    private string ResolveTurnPostureGuidanceStateStatus(
        string sessionId,
        RuntimeGuidanceCandidate? existingCandidate,
        RuntimeTurnPostureGuidanceResult guidance,
        string? requestedCandidateId)
    {
        return guidance.GuidanceCandidate is null
            ? ResolveTurnPostureGuidanceStateStatus(sessionId, existingCandidate, requestedCandidateId)
            : TurnPostureGuidanceStateActiveSession;
    }

    private static bool HasGuidanceCandidateState(
        IReadOnlyDictionary<string, HashSet<string>> stateBySessionId,
        string sessionId,
        string? candidateId)
    {
        if (!stateBySessionId.TryGetValue(sessionId, out var candidateIds) || candidateIds.Count == 0)
        {
            return false;
        }

        var normalizedCandidateId = NormalizeOptional(candidateId);
        return normalizedCandidateId is null
            ? candidateIds.Count > 0
            : candidateIds.Contains(normalizedCandidateId);
    }

    private static void RemoveGuidanceCandidateState(
        Dictionary<string, HashSet<string>> stateBySessionId,
        string sessionId,
        string? candidateId)
    {
        var normalizedCandidateId = NormalizeOptional(candidateId);
        if (normalizedCandidateId is null
            || !stateBySessionId.TryGetValue(sessionId, out var candidateIds))
        {
            return;
        }

        candidateIds.Remove(normalizedCandidateId);
        if (candidateIds.Count == 0)
        {
            stateBySessionId.Remove(sessionId);
        }
    }

    private void RecordGuidanceCandidate(string sessionId, SessionGatewayTurnPostureSurface posture)
    {
        if (posture.GuidanceCandidate is null || !posture.ShouldGuide || posture.PromptSuppressed)
        {
            return;
        }

        ClearStaleTurnPostureSuppression(
            sessionId,
            posture.GuidanceCandidate.CandidateId,
            posture.GuidanceCandidate.RevisionHash);
        RemoveGuidanceCandidateState(restoredGuidanceCandidateIdsBySessionId, sessionId, posture.GuidanceCandidate.CandidateId);
        RemoveGuidanceCandidateState(staleGuidanceCandidateIdsBySessionId, sessionId, posture.GuidanceCandidate.CandidateId);
        var candidates = GetGuidanceCandidateStore(sessionId);
        candidates[posture.GuidanceCandidate.CandidateId] = posture.GuidanceCandidate;
        activeGuidanceCandidateIdsBySessionId[sessionId] = posture.GuidanceCandidate.CandidateId;
        PersistTurnPostureState(sessionId);
    }

    private Dictionary<string, RuntimeGuidanceCandidate> GetGuidanceCandidateStore(string sessionId)
    {
        if (!guidanceCandidatesBySessionId.TryGetValue(sessionId, out var candidates))
        {
            candidates = new Dictionary<string, RuntimeGuidanceCandidate>(StringComparer.Ordinal);
            guidanceCandidatesBySessionId[sessionId] = candidates;
        }

        return candidates;
    }

    private void RecordTurnPostureSuppression(string sessionId, SessionGatewayTurnPostureSurface posture)
    {
        if (string.IsNullOrWhiteSpace(posture.SuppressionKey))
        {
            return;
        }

        if (!turnPostureSuppressionKeysBySessionId.TryGetValue(sessionId, out var suppressionKeys))
        {
            suppressionKeys = new HashSet<string>(StringComparer.Ordinal);
            turnPostureSuppressionKeysBySessionId[sessionId] = suppressionKeys;
        }

        suppressionKeys.Add(posture.SuppressionKey);
        MarkGuidanceCandidateParked(sessionId, posture);
        PersistTurnPostureState(sessionId);
    }

    private void ClearGuidanceLiveState(string sessionId, string? candidateId = null)
    {
        var normalizedCandidateId = NormalizeOptional(candidateId);
        if (normalizedCandidateId is null)
        {
            guidanceCandidatesBySessionId.Remove(sessionId);
            activeGuidanceCandidateIdsBySessionId.Remove(sessionId);
            turnPostureSuppressionKeysBySessionId.Remove(sessionId);
            restoredGuidanceCandidateIdsBySessionId.Remove(sessionId);
            staleGuidanceCandidateIdsBySessionId.Remove(sessionId);
            PersistTurnPostureState(sessionId);
            return;
        }

        if (guidanceCandidatesBySessionId.TryGetValue(sessionId, out var candidates))
        {
            candidates.Remove(normalizedCandidateId);
            if (candidates.Count == 0)
            {
                guidanceCandidatesBySessionId.Remove(sessionId);
            }
        }

        if (activeGuidanceCandidateIdsBySessionId.TryGetValue(sessionId, out var activeCandidateId)
            && string.Equals(activeCandidateId, normalizedCandidateId, StringComparison.Ordinal))
        {
            if (guidanceCandidatesBySessionId.TryGetValue(sessionId, out var remainingCandidates)
                && remainingCandidates.Count > 0)
            {
                activeGuidanceCandidateIdsBySessionId[sessionId] = remainingCandidates.Keys
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .First();
            }
            else
            {
                activeGuidanceCandidateIdsBySessionId.Remove(sessionId);
            }
        }

        if (turnPostureSuppressionKeysBySessionId.TryGetValue(sessionId, out var suppressionKeys)
            && BuildTurnPostureParkedSuppressionKeyPrefix(normalizedCandidateId) is { } prefix)
        {
            suppressionKeys.RemoveWhere(key => key.StartsWith(prefix, StringComparison.Ordinal));
            if (suppressionKeys.Count == 0)
            {
                turnPostureSuppressionKeysBySessionId.Remove(sessionId);
            }
        }

        RemoveGuidanceCandidateState(restoredGuidanceCandidateIdsBySessionId, sessionId, normalizedCandidateId);
        RemoveGuidanceCandidateState(staleGuidanceCandidateIdsBySessionId, sessionId, normalizedCandidateId);
        PersistTurnPostureState(sessionId);
    }

    private void ClearTurnPostureSuppression(string sessionId, string? candidateId)
    {
        var prefix = BuildTurnPostureParkedSuppressionKeyPrefix(candidateId);
        if (prefix is not null && turnPostureSuppressionKeysBySessionId.TryGetValue(sessionId, out var suppressionKeys))
        {
            suppressionKeys.RemoveWhere(key => key.StartsWith(prefix, StringComparison.Ordinal));
            PersistTurnPostureState(sessionId);
        }
    }

    private void ClearStaleTurnPostureSuppression(string sessionId, string? candidateId, string? currentRevisionHash)
    {
        var prefix = BuildTurnPostureParkedSuppressionKeyPrefix(candidateId);
        if (prefix is null || !turnPostureSuppressionKeysBySessionId.TryGetValue(sessionId, out var suppressionKeys))
        {
            return;
        }

        var currentKey = BuildTurnPostureParkedSuppressionKey(candidateId, currentRevisionHash);
        var removedCount = suppressionKeys.RemoveWhere(key =>
            key.StartsWith(prefix, StringComparison.Ordinal)
            && !string.Equals(key, currentKey, StringComparison.Ordinal));
        if (removedCount > 0)
        {
            PersistTurnPostureState(sessionId);
        }
    }

    private void MarkGuidanceCandidateParked(string sessionId, SessionGatewayTurnPostureSurface posture)
    {
        var candidateId = posture.ProjectionFields.GetValueOrDefault(RuntimeTurnPostureFields.CandidateId);
        if (string.IsNullOrWhiteSpace(candidateId)
            || !guidanceCandidatesBySessionId.TryGetValue(sessionId, out var candidates)
            || !candidates.TryGetValue(candidateId, out var candidate)
            || !string.Equals(candidate.CandidateId, candidateId, StringComparison.Ordinal))
        {
            return;
        }

        candidates[candidateId] = candidate with
        {
            State = RuntimeGuidanceCandidateStates.Parked,
            ParkedUntilChangeHash = candidate.RevisionHash,
        };
        activeGuidanceCandidateIdsBySessionId[sessionId] = candidateId;
    }

    private string? ResolveTurnPostureSessionLanguage(string sessionId)
    {
        return turnPostureSessionLanguagesBySessionId.TryGetValue(sessionId, out var language)
            ? NormalizeTurnPostureSessionLanguage(language)
            : null;
    }

    private void RecordTurnPostureSessionLanguage(
        string sessionId,
        string? userText,
        string? currentSessionLanguage)
    {
        var resolution = new RuntimeTurnLanguageResolver().Resolve(new RuntimeTurnLanguageResolutionInput(
            userText,
            SessionLanguage: currentSessionLanguage));
        if (!string.Equals(resolution.Source, RuntimeTurnLanguageResolutionSources.ExplicitUserLanguage, StringComparison.Ordinal)
            && !string.Equals(resolution.Source, RuntimeTurnLanguageResolutionSources.TextContainsChinese, StringComparison.Ordinal))
        {
            return;
        }

        if (NormalizeTurnPostureSessionLanguage(resolution.SessionLanguage) is not { } sessionLanguage)
        {
            return;
        }

        if (string.Equals(currentSessionLanguage, sessionLanguage, StringComparison.Ordinal))
        {
            return;
        }

        turnPostureSessionLanguagesBySessionId[sessionId] = sessionLanguage;
        PersistTurnPostureState(sessionId);
    }

    private static string? NormalizeTurnPostureSessionLanguage(string? language)
    {
        return language switch
        {
            RuntimeTurnAwarenessProfileLanguageModes.Chinese => RuntimeTurnAwarenessProfileLanguageModes.Chinese,
            RuntimeTurnAwarenessProfileLanguageModes.English => RuntimeTurnAwarenessProfileLanguageModes.English,
            _ => null,
        };
    }

    private void EnsureTurnPostureStateLoaded(string sessionId)
    {
        if (!loadedTurnPostureStateSessionIds.Add(sessionId))
        {
            return;
        }

        var statePath = GetTurnPostureSessionStatePath(sessionId);
        if (!File.Exists(statePath))
        {
            return;
        }

        RuntimeTurnPostureSessionState? state;
        try
        {
            state = JsonSerializer.Deserialize<RuntimeTurnPostureSessionState>(
                File.ReadAllText(statePath),
                TurnPostureStateJsonOptions);
        }
        catch (JsonException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        if (state is null || !string.Equals(state.SessionId, sessionId, StringComparison.Ordinal))
        {
            return;
        }

        if (IsTurnPostureStateStale(state.UpdatedAtUtc))
        {
            var staleCandidateIds = ResolvePersistedGuidanceCandidateIds(state);
            if (staleCandidateIds.Count > 0)
            {
                staleGuidanceCandidateIdsBySessionId[sessionId] = staleCandidateIds.ToHashSet(StringComparer.Ordinal);
            }

            TryDeleteTurnPostureStateFile(statePath);
            return;
        }

        var restoredCandidates = RestorePersistedGuidanceCandidates(state);
        if (restoredCandidates.Count > 0)
        {
            guidanceCandidatesBySessionId[sessionId] = restoredCandidates.ToDictionary(
                candidate => candidate.CandidateId,
                candidate => candidate,
                StringComparer.Ordinal);
            activeGuidanceCandidateIdsBySessionId[sessionId] = ResolvePersistedActiveCandidateId(state, restoredCandidates);
            restoredGuidanceCandidateIdsBySessionId[sessionId] = restoredCandidates
                .Select(candidate => candidate.CandidateId)
                .ToHashSet(StringComparer.Ordinal);
        }

        var suppressionKeys = state.SuppressionKeys
            .Select(NormalizeOptional)
            .OfType<string>()
            .Where(IsTurnPostureParkedSuppressionKey)
            .Take(TurnPosturePersistedSuppressionKeyLimit)
            .ToHashSet(StringComparer.Ordinal);
        if (suppressionKeys.Count > 0)
        {
            turnPostureSuppressionKeysBySessionId[sessionId] = suppressionKeys;
        }

        if (NormalizeTurnPostureSessionLanguage(state.SessionLanguage) is { } sessionLanguage)
        {
            turnPostureSessionLanguagesBySessionId[sessionId] = sessionLanguage;
        }
    }

    private void PersistTurnPostureState(string sessionId)
    {
        var sessionLanguage = ResolveTurnPostureSessionLanguage(sessionId);
        var candidates = guidanceCandidatesBySessionId.TryGetValue(sessionId, out var storedCandidates)
            ? storedCandidates.Values
                .Where(IsPersistableGuidanceCandidate)
                .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<RuntimeGuidanceCandidate>();
        var activeCandidateId = ResolvePersistableActiveCandidateId(sessionId, candidates);
        var legacyCandidate = activeCandidateId is not null
            ? candidates.FirstOrDefault(candidate => string.Equals(candidate.CandidateId, activeCandidateId, StringComparison.Ordinal))
            : candidates.Length == 1 ? candidates[0] : null;
        var suppressionKeys = turnPostureSuppressionKeysBySessionId.TryGetValue(sessionId, out var keys)
            ? keys
                .Where(IsTurnPostureParkedSuppressionKey)
                .OrderBy(key => key, StringComparer.Ordinal)
                .Take(TurnPosturePersistedSuppressionKeyLimit)
                .ToArray()
            : Array.Empty<string>();

        var statePath = GetTurnPostureSessionStatePath(sessionId);
        if (candidates.Length == 0 && suppressionKeys.Length == 0 && sessionLanguage is null)
        {
            TryDeleteTurnPostureStateFile(statePath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        File.WriteAllText(
            statePath,
            JsonSerializer.Serialize(
                new RuntimeTurnPostureSessionState
                {
                    SessionId = sessionId,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    GuidanceCandidate = legacyCandidate,
                    GuidanceCandidates = candidates,
                    ActiveCandidateId = activeCandidateId,
                    SuppressionKeys = suppressionKeys,
                    SessionLanguage = sessionLanguage,
                },
                TurnPostureStateJsonOptions));
    }

    private static IReadOnlyList<RuntimeGuidanceCandidate> RestorePersistedGuidanceCandidates(RuntimeTurnPostureSessionState state)
    {
        return state.GuidanceCandidates
            .Append(state.GuidanceCandidate)
            .Where(IsPersistableGuidanceCandidate)
            .Select(candidate => candidate!)
            .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolvePersistedGuidanceCandidateIds(RuntimeTurnPostureSessionState state)
    {
        return RestorePersistedGuidanceCandidates(state)
            .Select(candidate => NormalizeOptional(candidate.CandidateId))
            .Append(NormalizeOptional(state.ActiveCandidateId))
            .Concat(state.SuppressionKeys.Select(TryReadTurnPostureSuppressionCandidateId))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(candidateId => candidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolvePersistedActiveCandidateId(
        RuntimeTurnPostureSessionState state,
        IReadOnlyList<RuntimeGuidanceCandidate> candidates)
    {
        var activeCandidateId = NormalizeOptional(state.ActiveCandidateId);
        if (activeCandidateId is not null
            && candidates.Any(candidate => string.Equals(candidate.CandidateId, activeCandidateId, StringComparison.Ordinal)))
        {
            return activeCandidateId;
        }

        var legacyCandidateId = NormalizeOptional(state.GuidanceCandidate?.CandidateId);
        if (legacyCandidateId is not null
            && candidates.Any(candidate => string.Equals(candidate.CandidateId, legacyCandidateId, StringComparison.Ordinal)))
        {
            return legacyCandidateId;
        }

        return candidates[0].CandidateId;
    }

    private string? ResolvePersistableActiveCandidateId(string sessionId, IReadOnlyList<RuntimeGuidanceCandidate> candidates)
    {
        var activeCandidateId = activeGuidanceCandidateIdsBySessionId.GetValueOrDefault(sessionId);
        if (!string.IsNullOrWhiteSpace(activeCandidateId)
            && candidates.Any(candidate => string.Equals(candidate.CandidateId, activeCandidateId, StringComparison.Ordinal)))
        {
            return activeCandidateId;
        }

        if (candidates.Count == 1)
        {
            return candidates[0].CandidateId;
        }

        return null;
    }

    private static bool IsTurnPostureStateStale(DateTimeOffset updatedAtUtc)
    {
        return updatedAtUtc == default || DateTimeOffset.UtcNow - updatedAtUtc > TurnPostureLiveStateTtl;
    }

    private static void TryDeleteTurnPostureStateFile(string statePath)
    {
        try
        {
            if (File.Exists(statePath))
            {
                File.Delete(statePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private string GetTurnPostureSessionStatePath(string sessionId)
    {
        return Path.Combine(
            paths.RuntimeLiveStateRoot,
            "session-gateway-turn-posture",
            $"{ToSafeTurnPostureStateFileName(sessionId)}.json");
    }

    private static bool IsPersistableGuidanceCandidate(RuntimeGuidanceCandidate? candidate)
    {
        return candidate is not null
            && string.Equals(candidate.SchemaVersion, RuntimeGuidanceCandidate.CurrentVersion, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(candidate.CandidateId)
            && !string.IsNullOrWhiteSpace(candidate.RevisionHash);
    }

    private static bool IsTurnPostureParkedSuppressionKey(string key)
    {
        return key.StartsWith("runtime_turn_posture:parked:", StringComparison.Ordinal)
            && key.Count(character => character == ':') >= 3;
    }

    private static string ToSafeTurnPostureStateFileName(string sessionId)
    {
        var safe = new string(sessionId
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.'
                ? character
                : '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "unknown-session" : safe;
    }

    private static bool TryResolveTurnPostureLifecycleControl(string text, out string guidanceStateStatus)
    {
        var lifecycleControl = RuntimeGuidanceLifecycleControl.Decide(text);
        if (lifecycleControl.RequestsForget)
        {
            guidanceStateStatus = TurnPostureGuidanceStateForgotten;
            return true;
        }

        if (lifecycleControl.RequestsClear)
        {
            guidanceStateStatus = TurnPostureGuidanceStateCleared;
            return true;
        }

        guidanceStateStatus = TurnPostureGuidanceStateNone;
        return false;
    }

    private static bool IsTurnPostureReopen(string text)
    {
        var normalized = NormalizeConversationControlText(text);
        return ContainsAny(
            normalized,
            "reopen",
            "re open",
            "resume",
            "unpark",
            "重新打开",
            "重新开启",
            "恢复",
            "继续落案");
    }

    private static bool IsTurnPostureMaterialCandidateChange(string text)
    {
        var normalized = NormalizeConversationControlText(text);
        return ContainsAny(
            normalized,
            "修正",
            "改成",
            "改为",
            "更新为",
            "去掉",
            "移除",
            "删除",
            "不再包含",
            "换一个方向",
            "换个方向",
            "另一个方向",
            "新方向",
            "目标是",
            "范围",
            "成功标准",
            "验收",
            "约束",
            "风险",
            "correction",
            "correct",
            "replace",
            "change to",
            "remove",
            "drop",
            "delete",
            "new topic",
            "new direction",
            "different direction",
            "goal is",
            "scope",
            "success",
            "acceptance",
            "constraint",
            "risk");
    }

    private static string? ResolveTurnPostureCandidateRevisionHash(
        RuntimeGuidanceCandidate? existingGuidanceCandidate,
        string? candidateId)
    {
        return string.Equals(existingGuidanceCandidate?.CandidateId, NormalizeOptional(candidateId), StringComparison.Ordinal)
            ? NormalizeOptional(existingGuidanceCandidate?.RevisionHash)
            : null;
    }

    private static string? BuildTurnPostureParkedSuppressionKey(string? candidateId, string? revisionHash)
    {
        var normalizedCandidateId = NormalizeOptional(candidateId);
        var normalizedRevisionHash = NormalizeOptional(revisionHash);
        return normalizedCandidateId is null || normalizedRevisionHash is null
            ? null
            : $"{BuildTurnPostureParkedSuppressionKeyPrefix(normalizedCandidateId)}{normalizedRevisionHash}";
    }

    private static string? BuildTurnPostureParkedSuppressionKeyPrefix(string? candidateId)
    {
        var normalizedCandidateId = NormalizeOptional(candidateId);
        return normalizedCandidateId is null
            ? null
            : $"runtime_turn_posture:parked:{normalizedCandidateId}:";
    }

    private static string? TryReadTurnPostureSuppressionCandidateId(string? suppressionKey)
    {
        var normalizedSuppressionKey = NormalizeOptional(suppressionKey);
        const string Prefix = "runtime_turn_posture:parked:";
        if (normalizedSuppressionKey is null || !normalizedSuppressionKey.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var remainder = normalizedSuppressionKey[Prefix.Length..];
        var revisionSeparatorIndex = remainder.LastIndexOf(':');
        return revisionSeparatorIndex <= 0
            ? NormalizeOptional(remainder)
            : NormalizeOptional(remainder[..revisionSeparatorIndex]);
    }

    private sealed record RuntimeTurnPostureSessionState
    {
        public string SessionId { get; init; } = string.Empty;

        public DateTimeOffset UpdatedAtUtc { get; init; }

        public RuntimeGuidanceCandidate? GuidanceCandidate { get; init; }

        public IReadOnlyList<RuntimeGuidanceCandidate> GuidanceCandidates { get; init; } = Array.Empty<RuntimeGuidanceCandidate>();

        public string? ActiveCandidateId { get; init; }

        public IReadOnlyList<string> SuppressionKeys { get; init; } = Array.Empty<string>();

        public string? SessionLanguage { get; init; }
    }

    private static string ClassifyIntent(
        SessionGatewayMessageRequest request,
        RoleGovernanceRuntimePolicy roleGovernancePolicy)
    {
        var normalizedMode = NormalizeRequestedMode(request.RequestedMode);
        var text = NormalizeConversationControlText(request.UserText);
        var hasStructuredBinding = HasStructuredActionBinding(request, normalizedMode);
        if (ShouldTreatRoleAutomationAsDiscussion(request, roleGovernancePolicy, text, normalizedMode))
        {
            return "discuss";
        }

        if ((IsAcknowledgementOnly(text) || IsBlanketConsentOnly(text)) && !hasStructuredBinding)
        {
            return "discuss";
        }

        if (string.Equals(normalizedMode, "governed_run", StringComparison.Ordinal)
            && ContainsPrivilegedOperationText(request.UserText))
        {
            return "privileged_work_order";
        }

        if (!string.IsNullOrWhiteSpace(normalizedMode))
        {
            return normalizedMode;
        }

        var actionIntentText = RemoveNegatedActionText(text);
        if (ContainsPrivilegedOperationText(actionIntentText))
        {
            return "privileged_work_order";
        }

        if (ContainsAny(
                actionIntentText,
                "execute",
                "implement",
                "run ",
                "run-task",
                "task run",
                "review-task",
                "approve-review",
                "sync-state",
                "patch",
                "fix ",
                "apply ",
                "执行",
                "实现",
                "推进",
                "提交",
                "完成",
                "批准"))
        {
            return "governed_run";
        }

        if (ContainsAny(
                text,
                "plan",
                "roadmap",
                "next step",
                "next steps",
                "stage",
                "slice",
                "sequenc",
                "卡",
                "计划",
                "路线"))
        {
            return "plan";
        }

        return "discuss";
    }

    private static bool ShouldTreatRoleAutomationAsDiscussion(
        SessionGatewayMessageRequest request,
        RoleGovernanceRuntimePolicy roleGovernancePolicy,
        string normalizedText,
        string? normalizedMode)
    {
        return !IsRoleAutomationEnabled(roleGovernancePolicy)
               && !HasStructuredActionBinding(request, normalizedMode)
               && ContainsRoleAutomationReference(normalizedText);
    }

    private static bool IsRoleAutomationEnabled(RoleGovernanceRuntimePolicy roleGovernancePolicy)
    {
        return string.Equals(roleGovernancePolicy.RoleMode, RoleGovernanceRuntimePolicy.EnabledMode, StringComparison.Ordinal)
               && (roleGovernancePolicy.PlannerWorkerSplitEnabled
                   || roleGovernancePolicy.WorkerDelegationEnabled
                   || roleGovernancePolicy.SchedulerAutoDispatchEnabled);
    }

    private static bool ContainsRoleAutomationReference(string text)
    {
        return ContainsAny(
            text,
            "worker",
            "planner",
            "scheduler",
            "schedule",
            "taskgraph",
            "task graph",
            "task run",
            "run task",
            "delegated task",
            "delegation",
            "auto dispatch",
            "autodispatch",
            "worker dispatch",
            "worker lease",
            "worker thread",
            "planner worker",
            "role mode",
            "role assignment",
            "角色模式",
            "角色分配",
            "工作者",
            "计划者",
            "调度",
            "自动分发",
            "自动调度",
            "分发给",
            "分配给",
            "任务图",
            "工作线程",
            "注册 worker",
            "注册为 worker");
    }

    private static string RemoveNegatedActionText(string text)
    {
        var result = text;
        foreach (var phrase in new[]
        {
            "不能自动执行",
            "不能执行",
            "不要执行",
            "不执行",
            "不得执行",
            "不可以执行",
            "不能自动实现",
            "不能实现",
            "不要实现",
            "不实现",
            "不得实现",
            "不能自动提交",
            "不能提交",
            "不要提交",
            "不提交",
            "不得提交",
            "do not execute",
            "must not execute",
            "cannot execute",
            "without executing",
            "do not implement",
            "must not implement",
            "cannot implement",
            "without implementing",
            "do not commit",
            "must not commit",
            "cannot commit",
            "without committing",
        })
        {
            result = result.Replace(phrase, " ", StringComparison.Ordinal);
        }

        return ConversationWhitespace.Replace(result, " ").Trim();
    }

    private static string? NormalizeRequestedMode(string? requestedMode)
    {
        var normalized = NormalizeOptional(requestedMode)?
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal)
            .ToLowerInvariant();
        return normalized switch
        {
            "discuss" => "discuss",
            "plan" => "plan",
            "governedrun" => "governed_run",
            "governed_run" => "governed_run",
            "privileged" => "privileged_work_order",
            "privileged_work_order" => "privileged_work_order",
            "l5" => "privileged_work_order",
            _ => null,
        };
    }

    private static string ResolveProjectionHint(string classifiedIntent)
    {
        return classifiedIntent switch
        {
            "plan" => "runtime_planning_surface",
            "governed_run" => "runtime_governed_run_surface",
            "privileged_work_order" => "runtime_privileged_work_order_surface",
            _ => "runtime_discussion_surface",
        };
    }

    private static string? TryReadClassifiedIntent(string reasonCode)
    {
        const string prefix = "session_gateway_turn_classified:";
        return reasonCode.StartsWith(prefix, StringComparison.Ordinal)
            ? reasonCode[prefix.Length..]
            : null;
    }

    private static string ResolveRepoId(string repoRoot)
    {
        var normalizedRoot = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(normalizedRoot);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool ContainsAny(string text, params string[] fragments)
    {
        return fragments.Any(fragment => text.Contains(fragment, StringComparison.Ordinal));
    }

    private static string NormalizeConversationControlText(string text)
    {
        var normalized = (text ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("’", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);
        normalized = ConversationControlSanitizer.Replace(normalized, " ");
        normalized = ConversationWhitespace.Replace(normalized, " ").Trim();
        return normalized;
    }

    private static bool IsAcknowledgementOnly(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text is "ok"
            or "okay"
            or "好的"
            or "好"
            or "继续"
            or "可以"
            or "收到"
            or "明白了"
            or "continue"
            or "continue please"
            or "ok continue"
            or "okay continue"
            or "好的 继续"
            or "好的 继续吧"
            or "可以 继续")
        {
            return true;
        }

        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var acknowledgementTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "ok", "okay", "好的", "好", "继续", "可以", "收到", "明白了", "continue",
        };
        var fillerTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "please", "吧", "呀", "呢", "先", "就", "then",
        };
        return tokens.Any(token => acknowledgementTokens.Contains(token))
               && tokens.All(token => acknowledgementTokens.Contains(token) || fillerTokens.Contains(token));
    }

    private static bool IsBlanketConsentOnly(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.Contains("go ahead with everything", StringComparison.Ordinal)
            || text.Contains("continue with everything", StringComparison.Ordinal)
            || text.Contains("approve everything", StringComparison.Ordinal)
            || text.Contains("confirm everything", StringComparison.Ordinal)
            || text.Contains("all needed confirmations", StringComparison.Ordinal)
            || text.Contains("后面都自动执行", StringComparison.Ordinal)
            || text.Contains("都确认", StringComparison.Ordinal)
            || text.Contains("全部确认", StringComparison.Ordinal)
            || text.Contains("不要问我", StringComparison.Ordinal)
            || text.Contains("go ahead and do everything", StringComparison.Ordinal)
            || text.Contains("continue and approve everything", StringComparison.Ordinal)
            || text.Contains("不要问我 直接跑完", StringComparison.Ordinal)
            || text.Contains("按你认为安全的方式继续", StringComparison.Ordinal)
            || text.Contains("按你认为安全的方式", StringComparison.Ordinal)
            || text.Contains("as you think is safe", StringComparison.Ordinal))
        {
            return true;
        }

        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var consentTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "go", "ahead", "continue", "approve", "confirm", "run", "execute", "do",
            "继续", "批准", "确认", "执行", "跑", "跑完", "自动",
        };
        var scopeTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "everything", "all", "后面", "都", "全部", "所有", "剩下的",
        };
        var fillerTokens = new HashSet<string>(StringComparer.Ordinal)
        {
            "please", "just", "and", "with", "the", "needed", "confirmations",
            "不要", "问我", "直接", "吧", "呀", "呢",
        };
        return tokens.Any(token => consentTokens.Contains(token))
               && tokens.Any(token => scopeTokens.Contains(token))
               && tokens.All(token => consentTokens.Contains(token) || scopeTokens.Contains(token) || fillerTokens.Contains(token));
    }

    private static bool HasStructuredActionBinding(SessionGatewayMessageRequest request, string? normalizedMode)
    {
        if (string.IsNullOrWhiteSpace(normalizedMode))
        {
            return false;
        }

        if (string.Equals(normalizedMode, "privileged_work_order", StringComparison.Ordinal))
        {
            return ReadPrivilegedConfirmation(request.ClientCapabilities) is not null;
        }

        if (string.Equals(normalizedMode, "governed_run", StringComparison.Ordinal))
        {
            return !string.IsNullOrWhiteSpace(request.TargetTaskId)
                   || !string.IsNullOrWhiteSpace(request.TargetTaskGraphId)
                   || !string.IsNullOrWhiteSpace(request.TargetCardId)
                   || !string.IsNullOrWhiteSpace(request.AcceptanceContractHash)
                   || !string.IsNullOrWhiteSpace(request.PlanHash)
                   || !string.IsNullOrWhiteSpace(request.TaskGraphHash);
        }

        if (string.Equals(normalizedMode, "plan", StringComparison.Ordinal))
        {
            return !string.IsNullOrWhiteSpace(request.TargetCardId)
                   || !string.IsNullOrWhiteSpace(request.PlanHash)
                   || !string.IsNullOrWhiteSpace(request.TaskGraphHash);
        }

        return false;
    }
}
