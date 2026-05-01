using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    public JsonObject BuildCardInspect(string cardId)
    {
        var graph = services.TaskGraphService.Load();
        var tasks = graph.ListTasks().Where(task => string.Equals(task.CardId, cardId, StringComparison.Ordinal)).ToArray();
        var draft = services.PlanningDraftService.TryGetCardDraft(cardId);
        var cardPath = Path.Combine(services.Paths.CardsRoot, $"{cardId}.md");
        var card = File.Exists(cardPath) ? services.PlannerService.ParseCard(cardPath) : null;
        var methodology = draft is not null
            ? new RuntimeMethodologyComplianceService(services.Paths).AssessDraft(draft)
            : card is not null
                ? new RuntimeMethodologyComplianceService(services.Paths).AssessCard(card, cardPath)
                : new RuntimeMethodologyAssessment
                {
                    Applies = false,
                    Acknowledged = true,
                    ReferencePath = ".ai/memory/architecture/05_EXECUTION_OS_METHODOLOGY.md",
                    CoverageStatus = RuntimeMethodologyCoverageStatus.NotApplicable,
                    Summary = "No managed card or markdown card was found.",
                    RecommendedAction = "inspect the card path or create a host-routed draft",
                };
        var completed = graph.CompletedTaskIds();
        var status = tasks.Length == 0 && draft is not null
            ? draft.Status.ToString().ToLowerInvariant()
            : ResolveCardStatus(tasks, completed);
        var nextAction = ResolveCardNextAction(tasks, completed);
        var blockedReason = ResolveCardBlockedReason(tasks, completed);
        var latestSummary = tasks
            .Select(task => task.LastWorkerSummary)
            .FirstOrDefault(summary => !string.IsNullOrWhiteSpace(summary))
            ?? tasks.Select(task => task.PlannerReview.Reason).FirstOrDefault(reason => !string.IsNullOrWhiteSpace(reason))
            ?? draft?.Goal
            ?? "(none)";

        return new JsonObject
        {
            ["kind"] = "card",
            ["card_id"] = cardId,
            ["title"] = card?.Title ?? draft?.Title ?? tasks.FirstOrDefault()?.Title ?? "(unknown)",
            ["goal"] = card?.Goal ?? draft?.Goal ?? "(none)",
            ["status"] = status,
            ["lifecycle_state"] = (draft?.Status ?? CardLifecycleState.Approved).ToString().ToLowerInvariant(),
            ["managed"] = draft is not null,
            ["draft_status"] = draft?.Status.ToString().ToLowerInvariant(),
            ["created_at"] = draft?.CreatedAtUtc,
            ["updated_at"] = draft?.UpdatedAtUtc,
            ["dependencies"] = ToJsonArray(tasks.SelectMany(task => task.Dependencies).Distinct(StringComparer.Ordinal)),
            ["blocked_reason"] = blockedReason,
            ["latest_summary"] = latestSummary,
            ["next_action"] = tasks.Length == 0
                && draft is not null
                && draft.Status is CardLifecycleState.Draft or CardLifecycleState.Reviewed or CardLifecycleState.Approved
                ? $"create taskgraph draft for {cardId} and approve it through host"
                : nextAction,
            ["delegation"] = new JsonObject
            {
                ["inspect_command"] = tasks.Length == 0 ? null : $"inspect card {cardId}",
                ["delegate_command"] = tasks.FirstOrDefault(task => AcceptanceContractExecutionGate.IsReadyForDispatch(task, completed)) is { } readyTask
                    ? $"task run {readyTask.TaskId}"
                    : null,
            },
            ["related_tasks"] = ToJsonArray(tasks.Select(task => task.TaskId)),
            ["task_statuses"] = new JsonObject(
                tasks.GroupBy(task => task.Status.ToString(), StringComparer.OrdinalIgnoreCase)
                    .Select(group => new KeyValuePair<string, JsonNode?>(group.Key, group.Count()))),
            ["methodology_gate"] = new JsonObject
            {
                ["applies"] = methodology.Applies,
                ["acknowledged"] = methodology.Acknowledged,
                ["reference"] = methodology.ReferencePath,
                ["coverage"] = RuntimeMethodologyComplianceService.DescribeCoverage(methodology.CoverageStatus),
                ["summary"] = methodology.Summary,
                ["recommended_action"] = methodology.RecommendedAction,
                ["related_cards"] = ToJsonArray(methodology.RelatedCardIds),
            },
            ["planning_context"] = BuildCardPlanningContext(draft, tasks),
            ["scope"] = ToJsonArray(card?.Scope ?? draft?.Scope ?? Array.Empty<string>()),
            ["acceptance"] = ToJsonArray(card?.Acceptance ?? draft?.Acceptance ?? Array.Empty<string>()),
        };
    }

    public JsonObject BuildCardList(string? lifecycleState = null)
    {
        var graph = services.TaskGraphService.Load();
        var drafts = services.PlanningDraftService.ListCardDrafts();
        var cardIds = graph.Cards
            .Concat(drafts.Select(item => item.CardId))
            .Concat(Directory.Exists(services.Paths.CardsRoot)
                ? Directory.GetFiles(services.Paths.CardsRoot, "CARD-*.md", SearchOption.TopDirectoryOnly)
                    .Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty)
                : Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        var items = new JsonArray();
        foreach (var cardId in cardIds)
        {
            var inspect = BuildCardInspect(cardId);
            if (lifecycleState is not null
                && !string.Equals(inspect["lifecycle_state"]?.GetValue<string>(), lifecycleState, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(inspect);
        }

        return new JsonObject
        {
            ["kind"] = "card_list",
            ["count"] = items.Count,
            ["items"] = items,
        };
    }

    public JsonObject BuildTaskInspect(string taskId, bool includeRuns = false)
    {
        var graph = services.TaskGraphService.Load();
        var task = services.TaskGraphService.GetTask(taskId);
        var runs = services.ExecutionRunService.ListRuns(taskId);
        var latestRun = runs.LastOrDefault();
        var completed = graph.CompletedTaskIds();
        var unresolvedDependencies = task.Dependencies.Where(dependency => !completed.Contains(dependency)).ToArray();
        var session = services.DevLoopService.GetSession();
        var lifecycleSnapshot = services.DelegatedWorkerLifecycleReconciliationService.Capture(persist: false);
        var delegatedLifecycle = lifecycleSnapshot.Records.FirstOrDefault(item => string.Equals(item.TaskId, taskId, StringComparison.Ordinal));
        var blocker = ResolveTaskBlockerContext(task, session, delegatedLifecycle, lifecycleSnapshot.Records, unresolvedDependencies);
        var blockedReason = ResolveTaskBlockedReason(task, unresolvedDependencies);
        var acceptanceContractGate = BuildAcceptanceContractGateNode(task);
        if (task.Status == DomainTaskStatus.Pending && blocker.BlocksDispatch && !string.IsNullOrWhiteSpace(blocker.Summary))
        {
            blockedReason = blocker.Summary;
        }

        var relatedTasks = graph.ListTasks()
            .Where(item =>
                !string.Equals(item.TaskId, task.TaskId, StringComparison.Ordinal)
                && (string.Equals(item.CardId, task.CardId, StringComparison.Ordinal)
                    || item.Dependencies.Contains(task.TaskId, StringComparer.Ordinal)
                    || task.Dependencies.Contains(item.TaskId, StringComparer.Ordinal)))
            .Select(item => item.TaskId)
            .ToArray();
        var nextAction = task.Status == DomainTaskStatus.Pending && blocker.BlocksDispatch
            ? ResolveDispatchBlockedNextAction(blocker, task.TaskId)
            : ResolveTaskNextAction(task, unresolvedDependencies);
        var latestSummary = task.LastWorkerSummary
            ?? task.PlannerReview.Reason
            ?? "(none)";
        var dispatch = task.Status == DomainTaskStatus.Pending
            && unresolvedDependencies.Length == 0
            && task.CanDispatchToWorkerPool
            && !AcceptanceContractExecutionGate.Evaluate(task).BlocksExecution
            && !blocker.BlocksDispatch
            ? "dispatchable"
            : "dispatch_blocked";
        var dispatchReason = dispatch == "dispatchable"
            ? "ready for host dispatch"
            : ResolveDispatchStateReason(task, blockedReason);
        var permissionArtifact = services.ArtifactRepository.TryLoadWorkerPermissionArtifact(taskId);
        var runArtifact = services.ArtifactRepository.TryLoadWorkerArtifact(taskId);
        var workerArtifact = services.ArtifactRepository.TryLoadWorkerExecutionArtifact(taskId);
        var providerArtifact = services.ArtifactRepository.TryLoadProviderArtifact(taskId);
        var reviewArtifact = services.ArtifactRepository.TryLoadPlannerReviewArtifact(taskId);
        var recentFailures = services.FailureContextService.GetTaskFailures(taskId, 5);
        var plannerEmergence = new PlannerEmergenceService(services.Paths, services.TaskGraphService, services.ExecutionRunService);
        var replanEntry = plannerEmergence.TryGetLatestReplan(taskId);
        var suggestedTasks = plannerEmergence.ListSuggestedTasks(taskId);
        var executionMemory = plannerEmergence.ListExecutionMemory(taskId, 5);
        var executionRunException = CreateExecutionRunHistoricalExceptionService().TryGet(taskId);
        var completion = BuildCompletionNode(task);
        var workerRoute = BuildWorkerRouteNode(task, runArtifact, workerArtifact, providerArtifact);
        var reviewEvidenceGate = BuildReviewEvidenceGateNode(task, reviewArtifact, workerArtifact);
        var reviewClosureBundle = BuildReviewClosureBundleNode(reviewArtifact);
        var runtimePackReviewRubric = BuildRuntimePackReviewRubricNode();
        if (completion is not null
            && workerRoute is not null
            && string.Equals(completion["mode"]?.GetValue<string>(), "manual_fallback", StringComparison.Ordinal))
        {
            workerRoute["historical"] = true;
        }

        return new JsonObject
        {
            ["kind"] = "task",
            ["task_id"] = task.TaskId,
            ["title"] = task.Title,
            ["status"] = task.Status.ToString(),
            ["task_type"] = task.TaskType.ToString(),
            ["priority"] = task.Priority,
            ["card_id"] = task.CardId ?? "(none)",
            ["proposal_source"] = task.ProposalSource.ToString(),
            ["proposal_reason"] = task.ProposalReason ?? "(none)",
            ["blocker_task_id"] = blocker.BlockerTaskId,
            ["blocker_lifecycle_id"] = blocker.BlockerLifecycleId,
            ["blocker_scope"] = blocker.BlockerScope,
            ["blocked_by_external_residue"] = blocker.BlockedByExternalResidue,
            ["dependencies"] = ToJsonArray(task.Dependencies),
            ["unresolved_dependencies"] = ToJsonArray(unresolvedDependencies),
            ["blocked_reason"] = blockedReason,
            ["related_tasks"] = ToJsonArray(relatedTasks),
            ["latest_summary"] = completion?["summary"]?.GetValue<string>() ?? latestSummary,
            ["next_action"] = nextAction,
            ["completion"] = completion,
            ["dispatch"] = new JsonObject
            {
                ["state"] = dispatch,
                ["reason"] = dispatchReason,
            },
            ["execution_run"] = BuildExecutionRunNode(task, latestRun, runs.Count, completion),
            ["execution_run_exception"] = executionRunException is null
                ? null
                : new JsonObject
                {
                    ["categories"] = ToJsonArray(executionRunException.Categories.Select(static category => category.ToString())),
                    ["auto_reconcile_eligible"] = executionRunException.AutoReconcileEligible,
                    ["summary"] = executionRunException.Summary,
                    ["recommended_action"] = executionRunException.RecommendedAction,
                    ["review_decision_status"] = executionRunException.ReviewDecisionStatus.ToString(),
                    ["review_resulting_status"] = executionRunException.ReviewResultingStatus.ToString(),
                    ["validation_passed"] = executionRunException.ValidationPassed,
                    ["safety_outcome"] = executionRunException.SafetyOutcome.ToString(),
                    ["review_artifact_ref"] = executionRunException.ReviewArtifactRef,
                    ["run_artifact_ref"] = executionRunException.RunArtifactRef,
                },
            ["latest_worker_route"] = workerRoute,
            ["runtime_pack_review_rubric"] = runtimePackReviewRubric,
            ["failure_context"] = new JsonObject
            {
                ["failure_count"] = recentFailures.Count,
                ["recent_failure_ids"] = ToJsonArray(recentFailures.Select(item => item.Id)),
                ["recent_failure_types"] = ToJsonArray(recentFailures.Select(item => item.Failure.Type.ToString())),
            },
            ["planner_emergence"] = new JsonObject
            {
                ["replan_required"] = task.Metadata.GetValueOrDefault("planner_replan_required") ?? "false",
                ["replan_entry_id"] = task.Metadata.GetValueOrDefault("planner_replan_entry_id"),
                ["entry_reason"] = task.Metadata.GetValueOrDefault("planner_entry_reason"),
                ["latest_replan"] = replanEntry is null
                    ? null
                    : new JsonObject
                    {
                        ["entry_id"] = replanEntry.EntryId,
                        ["trigger"] = replanEntry.Trigger.ToString(),
                        ["reason"] = replanEntry.Reason,
                        ["run_id"] = replanEntry.RunId,
                    },
                ["suggested_task_count"] = suggestedTasks.Count,
                ["suggested_task_ids"] = ToJsonArray(suggestedTasks.Select(item => item.SuggestionId)),
                ["last_memory_event"] = executionMemory.FirstOrDefault()?.EventKind,
            },
            ["recent_failures"] = new JsonArray(recentFailures.Select(item => new JsonObject
            {
                ["failure_id"] = item.Id,
                ["type"] = item.Failure.Type.ToString(),
                ["message"] = item.Failure.Message,
                ["timestamp"] = item.Timestamp,
            }).ToArray()),
            ["delegated_lifecycle"] = delegatedLifecycle is null
                ? null
                : new JsonObject
                {
                    ["state"] = delegatedLifecycle.State.ToString(),
                    ["lease_id"] = delegatedLifecycle.LeaseId,
                    ["lease_status"] = delegatedLifecycle.LeaseStatus?.ToString(),
                    ["run_id"] = delegatedLifecycle.RunId,
                    ["backend_id"] = delegatedLifecycle.BackendId,
                    ["worktree_path"] = delegatedLifecycle.WorktreePath,
                    ["worktree_state"] = delegatedLifecycle.WorktreeState?.ToString(),
                    ["recovery_action"] = delegatedLifecycle.RecoveryAction.ToString(),
                    ["retryable"] = delegatedLifecycle.Retryable,
                    ["requires_operator_action"] = delegatedLifecycle.RequiresOperatorAction,
                    ["summary"] = delegatedLifecycle.Summary,
                    ["recommended_next_action"] = delegatedLifecycle.RecommendedNextAction,
                    ["latest_recovery_entry_id"] = delegatedLifecycle.LatestRecoveryEntryId,
                    ["latest_recovery_actor_identity"] = delegatedLifecycle.LatestRecoveryActorIdentity,
                    ["latest_recovery_recorded_at"] = delegatedLifecycle.LatestRecoveryRecordedAt,
                },
            ["delegation"] = new JsonObject
            {
                ["inspect_command"] = $"inspect task {task.TaskId}",
                ["delegate_command"] = task.Status == DomainTaskStatus.Pending
                    && unresolvedDependencies.Length == 0
                    && !AcceptanceContractExecutionGate.Evaluate(task).BlocksExecution
                    && !blocker.BlocksDispatch
                        ? $"task run {task.TaskId}"
                        : null,
                ["fallback"] = "--cold task run <task-id> only with explicit operator approval",
            },
            ["last_recovery_action"] = task.LastRecoveryAction.ToString(),
            ["last_recovery_reason"] = task.LastRecoveryReason ?? "(none)",
            ["execution_substrate"] = new JsonObject
            {
                ["detected"] = task.Metadata.GetValueOrDefault("execution_substrate_failure") ?? "false",
                ["lane"] = task.Metadata.GetValueOrDefault("execution_failure_lane"),
                ["reason_code"] = task.Metadata.GetValueOrDefault("execution_failure_reason_code"),
                ["replan_allowed"] = task.Metadata.GetValueOrDefault("execution_replan_allowed"),
                ["category"] = task.Metadata.GetValueOrDefault("execution_substrate_category"),
                ["next_action"] = task.Metadata.GetValueOrDefault("execution_substrate_next_action"),
                ["recommended_next_action"] = task.Metadata.GetValueOrDefault("execution_failure_next_action"),
            },
            ["scope"] = ToJsonArray(task.Scope),
            ["acceptance"] = ToJsonArray(task.Acceptance),
            ["planning_context"] = BuildTaskPlanningContext(task),
            ["acceptance_contract_gate"] = acceptanceContractGate,
            ["acceptance_contract_materialization"] = BuildTaskAcceptanceContractMaterializationNode(task),
            ["review_evidence_gate"] = reviewEvidenceGate,
            ["review_closure_bundle"] = reviewClosureBundle,
            ["permission_requests"] = permissionArtifact is null
                ? new JsonArray()
                : new JsonArray(permissionArtifact.Requests.Select(request => new JsonObject
                {
                    ["permission_request_id"] = request.PermissionRequestId,
                    ["state"] = request.State.ToString(),
                    ["kind"] = request.Kind.ToString(),
                    ["risk_level"] = request.RiskLevel.ToString(),
                    ["scope_summary"] = request.ScopeSummary,
                    ["resource_path"] = request.ResourcePath,
                    ["summary"] = request.Summary,
                    ["recommended_decision"] = request.RecommendedDecision.ToString(),
                    ["recommended_reason"] = request.RecommendedReason,
                    ["decision"] = request.FinalDecision?.ToString(),
                    ["decision_reason"] = request.DecisionReason,
                }).ToArray()),
            ["runs"] = includeRuns
                ? new JsonArray(runs.Select(BuildRunSummaryNode).ToArray())
                : null,
        };
    }

    private static JsonObject BuildTaskAcceptanceContractMaterializationNode(TaskNode task)
    {
        var metadata = task.Metadata;
        var state = metadata.GetValueOrDefault(TaskGraphAcceptanceContractMaterializationGuard.MetadataStateKey) ?? "not_recorded";
        var source = metadata.GetValueOrDefault(TaskGraphAcceptanceContractMaterializationGuard.MetadataProjectionSourceKey) ?? "not_recorded";
        var policy = metadata.GetValueOrDefault(TaskGraphAcceptanceContractMaterializationGuard.MetadataProjectionPolicyKey) ?? "not_recorded";
        var reasonCode = metadata.GetValueOrDefault(TaskGraphAcceptanceContractMaterializationGuard.MetadataReasonCodeKey) ?? "not_recorded";
        return new JsonObject
        {
            ["state"] = state,
            ["ingress"] = metadata.GetValueOrDefault(TaskGraphAcceptanceContractMaterializationGuard.MetadataIngressKey),
            ["required"] = bool.TryParse(metadata.GetValueOrDefault(TaskGraphAcceptanceContractMaterializationGuard.MetadataRequiredKey), out var required) ? required : task.CanExecuteInWorker,
            ["projection_source"] = source,
            ["projection_policy"] = policy,
            ["contract_id"] = metadata.GetValueOrDefault(TaskGraphAcceptanceContractMaterializationGuard.MetadataContractIdKey) ?? task.AcceptanceContract?.ContractId,
            ["reason_code"] = reasonCode,
            ["summary"] = metadata.GetValueOrDefault(TaskGraphAcceptanceContractMaterializationGuard.MetadataSummaryKey)
                ?? (task.AcceptanceContract is null
                    ? "Task was not materialized through a recorded taskgraph acceptance-contract guard."
                    : $"Task has acceptance contract {task.AcceptanceContract.ContractId}, but materialization source metadata was not recorded."),
        };
    }

    private static JsonObject? BuildCardPlanningContext(CardDraftRecord? draft, IReadOnlyList<TaskNode> tasks)
    {
        var lineage = draft?.PlanningLineage ?? tasks.Select(task => PlanningLineageMetadata.TryRead(task.Metadata)).FirstOrDefault(item => item is not null);
        if (lineage is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["planning_slot_id"] = lineage.PlanningSlotId,
            ["active_planning_card_id"] = lineage.ActivePlanningCardId,
            ["plan_handle"] = FormalPlanningPacketService.BuildPlanHandle(lineage),
            ["source_intent_draft_id"] = lineage.SourceIntentDraftId,
            ["source_candidate_card_id"] = lineage.SourceCandidateCardId,
            ["formal_planning_state"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(lineage.FormalPlanningState.ToString()),
            ["task_ids"] = ToJsonArray(tasks
                .Where(task => MatchesPlanningLineage(PlanningLineageMetadata.TryRead(task.Metadata), lineage))
                .Select(task => task.TaskId)),
        };
    }

    private static JsonObject? BuildTaskPlanningContext(TaskNode task)
    {
        var lineage = PlanningLineageMetadata.TryRead(task.Metadata);
        if (lineage is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["planning_slot_id"] = lineage.PlanningSlotId,
            ["active_planning_card_id"] = lineage.ActivePlanningCardId,
            ["plan_handle"] = FormalPlanningPacketService.BuildPlanHandle(lineage),
            ["source_intent_draft_id"] = lineage.SourceIntentDraftId,
            ["source_candidate_card_id"] = lineage.SourceCandidateCardId,
            ["formal_planning_state"] = JsonNamingPolicy.SnakeCaseLower.ConvertName(lineage.FormalPlanningState.ToString()),
            ["taskgraph_draft_id"] = task.Metadata.GetValueOrDefault("taskgraph_draft_id"),
            ["taskgraph_draft_status"] = task.Metadata.GetValueOrDefault("taskgraph_draft_status"),
        };
    }

    private static bool MatchesPlanningLineage(PlanningLineage? left, PlanningLineage right)
    {
        return left is not null
            && string.Equals(left.PlanningSlotId, right.PlanningSlotId, StringComparison.Ordinal)
            && string.Equals(left.ActivePlanningCardId, right.ActivePlanningCardId, StringComparison.Ordinal);
    }
}
