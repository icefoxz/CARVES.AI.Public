using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Planning;

public sealed class PlannerHostService
{
    private readonly TaskGraph.TaskGraphService taskGraphService;
    private readonly OpportunityDetectorService opportunityDetectorService;
    private readonly PlannerOpportunityEvaluator opportunityEvaluator;
    private readonly PlannerAdapterRegistry plannerAdapterRegistry;
    private readonly PlannerContextAssembler plannerContextAssembler;
    private readonly PlannerProposalValidator plannerProposalValidator;
    private readonly PlannerProposalAcceptanceService plannerProposalAcceptanceService;
    private readonly IRuntimeArtifactRepository artifactRepository;

    public PlannerHostService(
        TaskGraph.TaskGraphService taskGraphService,
        OpportunityDetectorService opportunityDetectorService,
        PlannerOpportunityEvaluator opportunityEvaluator,
        PlannerAdapterRegistry plannerAdapterRegistry,
        PlannerContextAssembler plannerContextAssembler,
        PlannerProposalValidator plannerProposalValidator,
        PlannerProposalAcceptanceService plannerProposalAcceptanceService,
        IRuntimeArtifactRepository artifactRepository)
    {
        this.taskGraphService = taskGraphService;
        this.opportunityDetectorService = opportunityDetectorService;
        this.opportunityEvaluator = opportunityEvaluator;
        this.plannerAdapterRegistry = plannerAdapterRegistry;
        this.plannerContextAssembler = plannerContextAssembler;
        this.plannerProposalValidator = plannerProposalValidator;
        this.plannerProposalAcceptanceService = plannerProposalAcceptanceService;
        this.artifactRepository = artifactRepository;
    }

    public PlannerHostResult RunOnce(RuntimeSessionState session, PlannerWakeReason wakeReason, string wakeDetail)
    {
        var graph = taskGraphService.Load();
        session.WakePlanner(wakeReason, wakeDetail);

        if (session.ReviewPendingTaskIds.Count > 0 || graph.ByStatus(DomainTaskStatus.Review).Count > 0)
        {
            session.WaitPlanner(PlannerSleepReason.WaitingForReview, "planner is waiting for the review boundary to clear");
            return BuildTerminalResult(session, PlannerReentryOutcome.DeferredReviewBoundary, "review-pending work still governs this session", true);
        }

        var governedOpenTasks = graph.ListTasks()
            .Where(task => task.Status is DomainTaskStatus.Suggested or DomainTaskStatus.Pending && !task.CanDispatchToWorkerPool)
            .Select(task => task.TaskId)
            .ToArray();
        if (governedOpenTasks.Length > 0)
        {
            session.SleepPlanner(PlannerSleepReason.ExistingGovernedWork, "existing governed work already awaits planner or human follow-up");
            return BuildTerminalResult(
                session,
                PlannerReentryOutcome.ExistingGovernedWork,
                $"governed follow-up work already exists and awaits operator handling: {string.Join(", ", governedOpenTasks)}",
                true,
                governedOpenTasks);
        }

        var detection = opportunityDetectorService.DetectAndStore();
        var preview = opportunityEvaluator.Preview(session, detection.Snapshot);
        if (!preview.ProducedWork)
        {
            session.SleepPlanner(MapSleepReason(preview), preview.Reason);
            return new PlannerHostResult
            {
                Session = session,
                Reentry = new PlannerReentryResult(
                    PlannerReentryOutcome.NoJustifiedGap,
                    preview.Reason,
                    Array.Empty<string>(),
                    preview.RequiresOperatorPause,
                    preview.PlannerRound,
                    preview.DetectedOpportunityCount,
                    preview.EvaluatedOpportunityCount,
                    preview.OpportunitySourceSummary,
                    preview.AutonomyLimit),
            };
        }

        var adapter = plannerAdapterRegistry.ActiveAdapter;
        session.ActivatePlanner($"planner adapter '{adapter.AdapterId}' evaluating governed work", adapter.AdapterId);
        var request = plannerContextAssembler.Build(session, wakeReason, wakeDetail, detection.Snapshot, preview.SelectedOpportunities, preview.Preview);

        PlannerProposalEnvelope envelope;
        try
        {
            envelope = adapter.Run(request);
        }
        catch (Exception exception)
        {
            session.EscalatePlanner(PlannerEscalationReason.AdapterFailure, $"planner adapter '{adapter.AdapterId}' failed: {exception.Message}");
            return BuildTerminalResult(
                session,
                PlannerReentryOutcome.NoJustifiedGap,
                $"planner adapter '{adapter.AdapterId}' failed: {exception.Message}",
                true,
                plannerRound: preview.PlannerRound,
                detectedOpportunityCount: preview.DetectedOpportunityCount,
                evaluatedOpportunityCount: preview.EvaluatedOpportunityCount,
                opportunitySourceSummary: preview.OpportunitySourceSummary,
                autonomyLimit: preview.AutonomyLimit);
        }

        session.RecordPlannerProposal(envelope.ProposalId, envelope.AdapterId, $"planner adapter '{envelope.AdapterId}' produced proposal '{envelope.ProposalId}'");
        RecordPlannerRequestTelemetry(request, envelope);
        artifactRepository.SavePlannerProposalArtifact(envelope);

        var validation = plannerProposalValidator.Validate(envelope.Proposal);
        if (!validation.IsValid)
        {
            envelope = plannerProposalAcceptanceService.Reject(envelope, $"Planner proposal rejected: {string.Join("; ", validation.Errors)}");
            artifactRepository.SavePlannerProposalArtifact(envelope);
            session.EscalatePlanner(PlannerEscalationReason.InvalidProposal, envelope.AcceptanceReason);
            return new PlannerHostResult
            {
                Session = session,
                Proposal = envelope,
                Validation = validation,
                Reentry = new PlannerReentryResult(
                    PlannerReentryOutcome.NoJustifiedGap,
                    envelope.AcceptanceReason,
                    Array.Empty<string>(),
                    true,
                    preview.PlannerRound,
                    preview.DetectedOpportunityCount,
                    preview.EvaluatedOpportunityCount,
                    preview.OpportunitySourceSummary,
                    preview.AutonomyLimit),
            };
        }

        envelope = plannerProposalAcceptanceService.Accept(envelope);
        artifactRepository.SavePlannerProposalArtifact(envelope);
        session.RecordPlannerReentry(
            PlannerReentryOutcome.SuggestedPlanningWork.ToString(),
            envelope.AcceptedTaskIds,
            envelope.AcceptanceReason,
            RuntimeActionability.HumanActionable,
            preview.PlannerRound,
            preview.DetectedOpportunityCount,
            preview.EvaluatedOpportunityCount,
            preview.OpportunitySourceSummary,
            preview.Reason);
        session.SleepPlanner(
            envelope.AcceptedTaskIds.Count == 0 ? PlannerSleepReason.WaitingForHumanAction : PlannerSleepReason.ExistingGovernedWork,
            envelope.AcceptanceReason);

        return new PlannerHostResult
        {
            Session = session,
            Proposal = envelope,
            Validation = validation,
            Reentry = new PlannerReentryResult(
                envelope.AcceptedTaskIds.Count == 0 ? PlannerReentryOutcome.NoJustifiedGap : PlannerReentryOutcome.SuggestedPlanningWork,
                envelope.AcceptanceReason,
                envelope.AcceptedTaskIds,
                true,
                preview.PlannerRound,
                preview.DetectedOpportunityCount,
                preview.EvaluatedOpportunityCount,
                preview.OpportunitySourceSummary,
                preview.AutonomyLimit),
        };
    }

    private static PlannerSleepReason MapSleepReason(PlannerOpportunityPreviewResult preview)
    {
        return preview.AutonomyLimit != PlannerAutonomyLimit.None
            ? PlannerSleepReason.AutonomyLimitReached
            : preview.EvaluatedOpportunityCount == 0
                ? PlannerSleepReason.NoOpenOpportunities
                : PlannerSleepReason.WaitingForHumanAction;
    }

    private static PlannerHostResult BuildTerminalResult(
        RuntimeSessionState session,
        PlannerReentryOutcome outcome,
        string reason,
        bool requiresOperatorPause,
        IReadOnlyList<string>? proposedTaskIds = null,
        int plannerRound = 0,
        int detectedOpportunityCount = 0,
        int evaluatedOpportunityCount = 0,
        string opportunitySourceSummary = "(none)",
        PlannerAutonomyLimit autonomyLimit = PlannerAutonomyLimit.None)
    {
        return new PlannerHostResult
        {
            Session = session,
            Reentry = new PlannerReentryResult(
                outcome,
                reason,
                proposedTaskIds ?? Array.Empty<string>(),
                requiresOperatorPause,
                plannerRound,
                detectedOpportunityCount,
                evaluatedOpportunityCount,
                opportunitySourceSummary,
                autonomyLimit),
        };
    }

    private static void RecordPlannerRequestTelemetry(PlannerRunRequest request, PlannerProposalEnvelope envelope)
    {
        if (envelope.RequestEnvelopeDraft is null)
        {
            return;
        }

        var paths = ControlPlanePaths.FromRepoRoot(request.RepoRoot);
        var attributionService = new LlmRequestEnvelopeAttributionService(paths);
        var draft = envelope.RequestEnvelopeDraft with
        {
            Model = string.IsNullOrWhiteSpace(envelope.RequestEnvelopeDraft.Model) ? envelope.Proposal.PlannerBackend : envelope.RequestEnvelopeDraft.Model,
            RunId = request.Session.SessionId,
            TaskId = request.PreviewTasks.FirstOrDefault()?.TaskId,
        };
        var usage = RuntimeTokenCapTruthResolver.Apply(
            new Carves.Runtime.Domain.AI.LlmRequestEnvelopeUsage
            {
                TokenAccountingSource = envelope.TokenAccountingSource,
                ProviderReportedInputTokens = envelope.ProviderReportedInputTokens,
                ProviderReportedCachedInputTokens = envelope.ProviderReportedCachedInputTokens,
                ProviderReportedUncachedInputTokens = envelope.ProviderReportedUncachedInputTokens,
                ProviderReportedOutputTokens = envelope.ProviderReportedOutputTokens,
                ProviderReportedReasoningTokens = envelope.ProviderReportedReasoningTokens,
                ProviderReportedTotalTokens = envelope.ProviderReportedTotalTokens,
                ReasoningTokensReportedSeparately = envelope.ReasoningTokensReportedSeparately,
                ReasoningTokensIncludedInOutput = envelope.ReasoningTokensIncludedInOutput,
                ProviderTotalIncludesReasoning = envelope.ProviderTotalIncludesReasoning,
                KnownProviderOverheadClass = envelope.ProviderReportedInputTokens.HasValue ? "provider_serialization_delta" : null,
            },
            RuntimeTokenCapTruthResolver.FromContextPackBudget(request.ContextPack?.Budget));
        attributionService.Record(draft, usage);

        if (request.ContextPack?.ArtifactPath is null)
        {
            return;
        }

        var routeGraph = new RuntimeSurfaceRouteGraphService(paths);
        routeGraph.RecordRouteEdge(new Carves.Runtime.Domain.AI.RuntimeConsumerRouteEdgeRecord
        {
            SurfaceId = request.ContextPack.ArtifactPath,
            Consumer = $"planner:{envelope.ProviderId}:{envelope.AdapterId}",
            DeclaredRouteKind = "direct_to_llm",
            ObservedRouteKind = "direct_to_llm",
            ObservedCount = 1,
            SampleCount = 1,
            FrequencyWindow = "7d",
            LlmReinjectionCount = 1,
            AverageFanout = 1,
            EvidenceSource = envelope.ProposalId,
            LastSeen = DateTimeOffset.UtcNow,
        });
    }
}
