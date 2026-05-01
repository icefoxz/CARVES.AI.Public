using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Planning;

public sealed class PlanningDraftService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly ICardDraftRepository cardDraftRepository;
    private readonly ITaskGraphDraftRepository taskGraphDraftRepository;
    private readonly RuntimeMethodologyComplianceService methodologyComplianceService;
    private readonly FormalPlanningExecutionGateService formalPlanningExecutionGateService;
    private readonly RuntimePolicyBundleService? runtimePolicyBundleService;

    public PlanningDraftService(
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        ICardDraftRepository cardDraftRepository,
        ITaskGraphDraftRepository taskGraphDraftRepository,
        RuntimePolicyBundleService? runtimePolicyBundleService = null,
        FormalPlanningExecutionGateService? formalPlanningExecutionGateService = null)
    {
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.cardDraftRepository = cardDraftRepository;
        this.taskGraphDraftRepository = taskGraphDraftRepository;
        methodologyComplianceService = new RuntimeMethodologyComplianceService(paths);
        this.formalPlanningExecutionGateService = formalPlanningExecutionGateService ?? new FormalPlanningExecutionGateService();
        this.runtimePolicyBundleService = runtimePolicyBundleService;
    }

    public IReadOnlyList<CardDraftRecord> ListCardDrafts()
    {
        return cardDraftRepository.List();
    }

    public IReadOnlyList<TaskGraphDraftRecord> ListTaskGraphDrafts()
    {
        return taskGraphDraftRepository.List();
    }

    public CardDraftRecord? TryGetCardDraft(string draftIdOrCardId)
    {
        return cardDraftRepository.TryGet(draftIdOrCardId);
    }

    public TaskGraphDraftRecord? TryGetTaskGraphDraft(string draftId)
    {
        return taskGraphDraftRepository.TryGet(draftId);
    }

    public IReadOnlyList<CardDraftRecord> ListCards()
    {
        return cardDraftRepository.List();
    }

    public CardDraftRecord CreateCardDraft(string jsonPath)
    {
        var payload = Deserialize<CardDraftSubmission>(jsonPath);
        var title = Require(payload.Title, "title");
        var goal = Require(payload.Goal, "goal");
        var acceptance = NormalizeRequiredList(payload.Acceptance, "acceptance");
        var cardId = string.IsNullOrWhiteSpace(payload.CardId) ? BuildNextCardId() : payload.CardId.Trim();
        var draft = new CardDraftRecord
        {
            DraftId = $"draft-{cardId.ToLowerInvariant()}",
            CardId = cardId,
            Title = title,
            Goal = goal,
            Acceptance = acceptance,
            Scope = NormalizeOptionalList(payload.Scope),
            Constraints = NormalizeOptionalList(payload.Constraints),
            Notes = NormalizeOptionalList(payload.Notes),
            AcceptanceContract = AcceptanceContractFactory.NormalizeCardContract(
                cardId,
                title,
                goal,
                acceptance,
                NormalizeOptionalList(payload.Constraints),
                payload.RealityModel,
                payload.AcceptanceContract),
            RealityModel = NormalizeRealityModel(payload.RealityModel),
            PlanningLineage = NormalizePlanningLineage(payload.PlanningLineage),
            Status = CardLifecycleState.Draft,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            MarkdownPath = Path.Combine(paths.CardsRoot, $"{cardId}.md"),
        };
        draft = methodologyComplianceService.NormalizeDraft(draft);

        Directory.CreateDirectory(paths.CardsRoot);
        File.WriteAllText(draft.MarkdownPath, RenderCardMarkdown(draft));
        cardDraftRepository.Save(draft);
        return draft;
    }

    public CardDraftRecord UpdateCardDraft(string cardId, string jsonPath)
    {
        var existing = cardDraftRepository.TryGet(cardId)
            ?? throw new InvalidOperationException($"Card draft '{cardId}' was not found.");
        var payload = Deserialize<CardDraftSubmission>(jsonPath);
        var updated = new CardDraftRecord
        {
            DraftId = existing.DraftId,
            CardId = existing.CardId,
            Title = string.IsNullOrWhiteSpace(payload.Title) ? existing.Title : payload.Title.Trim(),
            Goal = string.IsNullOrWhiteSpace(payload.Goal) ? existing.Goal : payload.Goal.Trim(),
            Acceptance = payload.Acceptance is null ? existing.Acceptance : NormalizeRequiredList(payload.Acceptance, "acceptance"),
            Scope = payload.Scope is null ? existing.Scope : NormalizeOptionalList(payload.Scope),
            Constraints = payload.Constraints is null ? existing.Constraints : NormalizeOptionalList(payload.Constraints),
            Notes = payload.Notes is null ? existing.Notes : NormalizeOptionalList(payload.Notes),
            AcceptanceContract = AcceptanceContractFactory.NormalizeCardContract(
                existing.CardId,
                string.IsNullOrWhiteSpace(payload.Title) ? existing.Title : payload.Title.Trim(),
                string.IsNullOrWhiteSpace(payload.Goal) ? existing.Goal : payload.Goal.Trim(),
                payload.Acceptance is null ? existing.Acceptance : NormalizeRequiredList(payload.Acceptance, "acceptance"),
                payload.Constraints is null ? existing.Constraints : NormalizeOptionalList(payload.Constraints),
                payload.RealityModel is null ? existing.RealityModel : payload.RealityModel,
                payload.AcceptanceContract ?? existing.AcceptanceContract),
            RealityModel = payload.RealityModel is null ? existing.RealityModel : NormalizeRealityModel(payload.RealityModel),
            PlanningLineage = existing.PlanningLineage,
            Status = existing.Status,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            ReviewedAtUtc = existing.ReviewedAtUtc,
            ApprovedAtUtc = existing.ApprovedAtUtc,
            RejectedAtUtc = existing.RejectedAtUtc,
            ArchivedAtUtc = existing.ArchivedAtUtc,
            LifecycleReason = existing.LifecycleReason,
            MethodologyRequired = existing.MethodologyRequired,
            MethodologyAcknowledged = existing.MethodologyAcknowledged,
            MethodologyReferencePath = existing.MethodologyReferencePath,
            MethodologyCoverageStatus = existing.MethodologyCoverageStatus,
            MethodologyRelatedCards = existing.MethodologyRelatedCards,
            MethodologySummary = existing.MethodologySummary,
            MethodologyRecommendedAction = existing.MethodologyRecommendedAction,
            MarkdownPath = existing.MarkdownPath,
        };
        updated = methodologyComplianceService.NormalizeDraft(updated);

        File.WriteAllText(updated.MarkdownPath, RenderCardMarkdown(updated));
        cardDraftRepository.Save(updated);
        return updated;
    }

    public CardDraftRecord SetCardStatus(string cardId, CardLifecycleState state, string? reason)
    {
        var existing = cardDraftRepository.TryGet(cardId)
            ?? throw new InvalidOperationException($"Card draft '{cardId}' was not found.");
        var now = DateTimeOffset.UtcNow;
        var updated = new CardDraftRecord
        {
            DraftId = existing.DraftId,
            CardId = existing.CardId,
            Title = existing.Title,
            Goal = existing.Goal,
            Acceptance = existing.Acceptance,
            Scope = existing.Scope,
            Constraints = existing.Constraints,
            Notes = existing.Notes,
            AcceptanceContract = existing.AcceptanceContract,
            RealityModel = existing.RealityModel,
            PlanningLineage = existing.PlanningLineage,
            Status = state,
            CreatedAtUtc = existing.CreatedAtUtc,
            UpdatedAtUtc = now,
            ReviewedAtUtc = state == CardLifecycleState.Reviewed ? now : existing.ReviewedAtUtc,
            ApprovedAtUtc = state == CardLifecycleState.Approved ? now : existing.ApprovedAtUtc,
            RejectedAtUtc = state == CardLifecycleState.Rejected ? now : existing.RejectedAtUtc,
            ArchivedAtUtc = state == CardLifecycleState.Archived ? now : existing.ArchivedAtUtc,
            LifecycleReason = string.IsNullOrWhiteSpace(reason) ? existing.LifecycleReason : reason.Trim(),
            MethodologyRequired = existing.MethodologyRequired,
            MethodologyAcknowledged = existing.MethodologyAcknowledged,
            MethodologyReferencePath = existing.MethodologyReferencePath,
            MethodologyCoverageStatus = existing.MethodologyCoverageStatus,
            MethodologyRelatedCards = existing.MethodologyRelatedCards,
            MethodologySummary = existing.MethodologySummary,
            MethodologyRecommendedAction = existing.MethodologyRecommendedAction,
            MarkdownPath = existing.MarkdownPath,
        };

        File.WriteAllText(updated.MarkdownPath, RenderCardMarkdown(updated));
        cardDraftRepository.Save(updated);
        return updated;
    }

    public CardLifecycleState ResolveCardLifecycleState(string cardId)
    {
        return cardDraftRepository.TryGet(cardId)?.Status ?? CardLifecycleState.Approved;
    }

    public void EnsureCardApprovedForPlanning(string cardId)
    {
        var state = ResolveCardLifecycleState(cardId);
        if (state != CardLifecycleState.Approved)
        {
            throw new InvalidOperationException($"Card '{cardId}' is in state '{state.ToString().ToLowerInvariant()}' and cannot enter planning.");
        }
    }

    public TaskGraphDraftRecord CreateTaskGraphDraft(string jsonPath)
    {
        var payload = Deserialize<TaskGraphDraftSubmission>(jsonPath);
        var cardId = Require(payload.CardId, "card_id");
        EnsureCardApprovedForPlanning(cardId);
        var cardDraft = cardDraftRepository.TryGet(cardId);
        var tasks = NormalizeTasks(cardId, payload.Tasks);
        var draft = new TaskGraphDraftRecord
        {
            DraftId = string.IsNullOrWhiteSpace(payload.DraftId)
                ? BuildTaskGraphDraftId(cardId)
                : payload.DraftId.Trim(),
            CardId = cardId,
            Status = PlanningDraftStatus.Draft,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            PlanningLineage = cardDraft?.PlanningLineage ?? NormalizePlanningLineage(payload.PlanningLineage),
            Tasks = tasks,
        };
        draft = methodologyComplianceService.NormalizeTaskGraphDraft(draft, cardDraft);
        formalPlanningExecutionGateService.EnsureReadyForTaskGraphPersistence(draft);

        taskGraphDraftRepository.Save(draft);
        return draft;
    }

    public TaskGraphDraftRecord ApproveTaskGraphDraft(string draftId, string reason)
    {
        var draft = taskGraphDraftRepository.TryGet(draftId)
            ?? throw new InvalidOperationException($"TaskGraph draft '{draftId}' was not found.");
        if (draft.Status != PlanningDraftStatus.Draft)
        {
            throw new InvalidOperationException($"TaskGraph draft '{draftId}' is not in draft state.");
        }

        EnsureCardApprovedForPlanning(draft.CardId);
        formalPlanningExecutionGateService.EnsureReadyForTaskGraphPersistence(draft);
        TaskGraphAcceptanceContractMaterializationGuard.EnsureCanMaterialize(draft);
        ValidateDependencies(draft);
        ValidateNoCycles(draft);

        var graph = taskGraphService.Load();
        var existingTaskIds = graph.Tasks.Keys.ToHashSet(StringComparer.Ordinal);
        var roleGovernancePolicy = runtimePolicyBundleService?.LoadRoleGovernancePolicy() ?? RoleGovernanceRuntimePolicy.CreateDefault();
        var cardDraft = cardDraftRepository.TryGet(draft.CardId);
        var accepted = new List<TaskNode>();
        foreach (var item in draft.Tasks)
        {
            if (existingTaskIds.Contains(item.TaskId))
            {
                throw new InvalidOperationException($"Task '{item.TaskId}' already exists in governed truth.");
            }

            var taskGraphDraftMetadata = BuildTaskGraphDraftApprovalMetadata(draft, item);
            accepted.Add(new TaskNode
            {
                TaskId = item.TaskId,
                Title = item.Title,
                Description = item.Description,
                Status = DomainTaskStatus.Pending,
                TaskType = item.TaskType,
                Priority = item.Priority,
                Source = "PLANNER_DRAFT",
                CardId = draft.CardId,
                ProposalSource = TaskProposalSource.PlannerGapDetection,
                ProposalReason = $"Accepted from taskgraph draft {draft.DraftId}.",
                Dependencies = item.Dependencies,
                Scope = item.Scope,
                Acceptance = item.Acceptance,
                Constraints = item.Constraints,
                AcceptanceContract = AcceptanceContractFactory.NormalizeTaskContract(
                    item.TaskId,
                    item.Title,
                    item.Description,
                    draft.CardId,
                    item.Acceptance,
                    item.Constraints,
                    validation: null,
                    item.AcceptanceContract,
                    cardDraft?.AcceptanceContract),
                Metadata = TaskRoleBindingMetadata.Merge(
                    PlanningLineageMetadata.Merge(
                        PlanningProofTargetMetadata.Merge(taskGraphDraftMetadata, item.ProofTarget),
                        draft.PlanningLineage),
                    MergeRoleBinding(TaskRoleBindingMetadata.Resolve(item.TaskType, null, roleGovernancePolicy), item.RoleBinding)),
            });
        }

        taskGraphService.AddTasks(accepted);
        draft = methodologyComplianceService.NormalizeTaskGraphDraft(new TaskGraphDraftRecord
        {
            DraftId = draft.DraftId,
            CardId = draft.CardId,
            Status = PlanningDraftStatus.Approved,
            CreatedAtUtc = draft.CreatedAtUtc,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            ApprovedAtUtc = DateTimeOffset.UtcNow,
            ApprovalReason = string.IsNullOrWhiteSpace(reason)
                ? "Approved through host-routed taskgraph draft workflow."
                : reason.Trim(),
            MethodologyRequired = draft.MethodologyRequired,
            MethodologyAcknowledged = draft.MethodologyAcknowledged,
            MethodologyReferencePath = draft.MethodologyReferencePath,
            MethodologyCoverageStatus = draft.MethodologyCoverageStatus,
            MethodologyRelatedCards = draft.MethodologyRelatedCards,
            MethodologySummary = draft.MethodologySummary,
            MethodologyRecommendedAction = draft.MethodologyRecommendedAction,
            PlanningLineage = draft.PlanningLineage,
            Tasks = draft.Tasks,
        }, cardDraft);
        taskGraphDraftRepository.Save(draft);
        return draft;
    }

    private void ValidateDependencies(TaskGraphDraftRecord draft)
    {
        var draftTaskIds = draft.Tasks.Select(task => task.TaskId).ToHashSet(StringComparer.Ordinal);
        var governedTaskIds = taskGraphService.Load().Tasks.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var task in draft.Tasks)
        {
            foreach (var dependency in task.Dependencies)
            {
                if (!draftTaskIds.Contains(dependency) && !governedTaskIds.Contains(dependency))
                {
                    throw new InvalidOperationException($"Draft task '{task.TaskId}' references unknown dependency '{dependency}'.");
                }
            }
        }
    }

    private static void ValidateNoCycles(TaskGraphDraftRecord draft)
    {
        var draftTaskIds = draft.Tasks.Select(task => task.TaskId).ToHashSet(StringComparer.Ordinal);
        var adjacency = draft.Tasks.ToDictionary(
            task => task.TaskId,
            task => task.Dependencies.Where(draftTaskIds.Contains).ToArray(),
            StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var taskId in adjacency.Keys)
        {
            Visit(taskId);
        }

        void Visit(string taskId)
        {
            if (visiting.Contains(taskId))
            {
                throw new InvalidOperationException($"TaskGraph draft '{draft.DraftId}' contains a dependency cycle at '{taskId}'.");
            }

            if (visited.Contains(taskId))
            {
                return;
            }

            visiting.Add(taskId);

            foreach (var dependency in adjacency[taskId])
            {
                Visit(dependency);
            }

            visiting.Remove(taskId);
            visited.Add(taskId);
        }
    }

    private static IReadOnlyList<TaskGraphDraftTask> NormalizeTasks(string cardId, IReadOnlyList<TaskGraphDraftTaskSubmission> tasks)
    {
        if (tasks.Count == 0)
        {
            throw new InvalidOperationException("TaskGraph draft requires at least one task.");
        }

        var normalized = new List<TaskGraphDraftTask>(tasks.Count);
        for (var index = 0; index < tasks.Count; index++)
        {
            var item = tasks[index];
            var taskId = string.IsNullOrWhiteSpace(item.TaskId)
                ? $"T-{cardId}-{index + 1:000}"
                : item.TaskId.Trim();
            var title = string.IsNullOrWhiteSpace(item.Title)
                ? Require(item.Description, $"tasks[{index}].description")
                : item.Title.Trim();
            var description = string.IsNullOrWhiteSpace(item.Description) ? title : item.Description.Trim();
            var acceptance = NormalizeOptionalList(item.Acceptance);
            var constraints = NormalizeOptionalList(item.Constraints);
            var explicitContractProvided = item.AcceptanceContract is not null;
            normalized.Add(new TaskGraphDraftTask
            {
                TaskId = taskId,
                Title = title,
                Description = description,
                TaskType = item.TaskType,
                Priority = string.IsNullOrWhiteSpace(item.Priority) ? "P1" : item.Priority.Trim(),
                Dependencies = NormalizeOptionalList(item.Dependencies),
                Scope = NormalizeOptionalList(item.Scope),
                Acceptance = acceptance,
                Constraints = constraints,
                AcceptanceContract = AcceptanceContractFactory.NormalizeTaskContract(
                    taskId,
                    title,
                    description,
                    cardId,
                    acceptance,
                    constraints,
                    validation: null,
                    item.AcceptanceContract),
                AcceptanceContractProjectionSource = TaskGraphAcceptanceContractMaterializationGuard.ResolveProjectionSource(explicitContractProvided),
                AcceptanceContractProjectionPolicy = TaskGraphAcceptanceContractMaterializationGuard.AutoMinimumContractPolicy,
                AcceptanceContractProjectionReason = TaskGraphAcceptanceContractMaterializationGuard.BuildProjectionReason(explicitContractProvided),
                ProofTarget = NormalizeTaskProofTarget(item.ProofTarget, $"tasks[{index}].proof_target"),
                RoleBinding = NormalizeTaskRoleBinding(item.RoleBinding),
            });
        }

        foreach (var task in normalized)
        {
            if (PlanningProofTargetMetadata.RequiresProofTarget(task.TaskType, task.Scope) && task.ProofTarget is null)
            {
                throw new InvalidOperationException($"TaskGraph draft task '{task.TaskId}' is a scoped execution task and requires proof_target.");
            }
        }

        if (normalized.Select(task => task.TaskId).Distinct(StringComparer.Ordinal).Count() != normalized.Count)
        {
            throw new InvalidOperationException("TaskGraph draft contains duplicate task ids.");
        }

        return normalized;
    }

    private static IReadOnlyDictionary<string, string> BuildTaskGraphDraftApprovalMetadata(
        TaskGraphDraftRecord draft,
        TaskGraphDraftTask task)
    {
        var metadata = new Dictionary<string, string>(TaskGraphAcceptanceContractMaterializationGuard.BuildMaterializationMetadata(task), StringComparer.Ordinal)
        {
            ["taskgraph_draft_id"] = draft.DraftId,
            ["taskgraph_draft_status"] = PlanningDraftStatus.Approved.ToString().ToLowerInvariant(),
        };
        return metadata;
    }

    private static T Deserialize<T>(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            throw new InvalidOperationException($"Draft payload '{jsonPath}' was not found.");
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(jsonPath), JsonOptions)
            ?? throw new InvalidOperationException($"Draft payload '{jsonPath}' could not be parsed.");
    }

    private static string RenderCardMarkdown(CardDraftRecord draft)
    {
        var lines = new List<string>
        {
            $"# {draft.CardId}",
            $"Title: {draft.Title}",
            "Type: FEATURE",
            "Priority: P1",
            $"LifecycleState: {draft.Status.ToString().ToLowerInvariant()}",
            $"CreatedAt: {draft.CreatedAtUtc:O}",
            $"MethodologyRequired: {draft.MethodologyRequired.ToString().ToLowerInvariant()}",
            $"MethodologyAcknowledged: {draft.MethodologyAcknowledged.ToString().ToLowerInvariant()}",
            $"MethodologyCoverage: {draft.MethodologyCoverageStatus ?? "not_applicable"}",
            $"MethodologyReference: {draft.MethodologyReferencePath ?? "(none)"}",
            string.Empty,
            "## Goal",
            draft.Goal,
            string.Empty,
            "## Reality Model",
        };

        if (draft.RealityModel is null)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.Add($"- outer_vision: {draft.RealityModel.OuterVision}");
            lines.Add($"- current_solid_scope: {draft.RealityModel.CurrentSolidScope}");
            lines.Add($"- next_real_slice: {draft.RealityModel.NextRealSlice}");
            lines.Add($"- reality_state: {ToSnakeCase(draft.RealityModel.RealityState)}");
            lines.Add($"- solidity_class: {ToSnakeCase(draft.RealityModel.SolidityClass)}");
            lines.Add($"- proof_target.kind: {ToSnakeCase(draft.RealityModel.ProofTarget.Kind)}");
            lines.Add($"- proof_target.description: {draft.RealityModel.ProofTarget.Description}");
            lines.AddRange(draft.RealityModel.NonGoals.Count == 0
                ? ["- non_goals: (none)"]
                : draft.RealityModel.NonGoals.Select(item => $"- non_goal: {item}"));
            lines.Add($"- illusion_risk.level: {ToSnakeCase(draft.RealityModel.IllusionRisk.Level)}");
            lines.AddRange(draft.RealityModel.IllusionRisk.Reasons.Count == 0
                ? ["- illusion_risk.reasons: (none)"]
                : draft.RealityModel.IllusionRisk.Reasons.Select(item => $"- illusion_risk.reason: {item}"));
            lines.Add($"- promotion_gate: {draft.RealityModel.PromotionGate}");
        }

        lines.Add(string.Empty);
        lines.Add("## Scope");

        lines.AddRange(draft.Scope.Count == 0 ? ["- (none)"] : draft.Scope.Select(item => $"- {item}"));
        lines.Add(string.Empty);
        lines.Add("## Acceptance");
        lines.AddRange(draft.Acceptance.Select(item => $"- {item}"));
        lines.Add(string.Empty);
        lines.Add("## Acceptance Contract");
        if (draft.AcceptanceContract is null)
        {
            lines.Add("- (none)");
        }
        else
        {
            lines.Add("```json");
            lines.Add(JsonSerializer.Serialize(draft.AcceptanceContract, JsonOptions));
            lines.Add("```");
        }
        lines.Add(string.Empty);
        lines.Add("## Constraints");
        lines.AddRange(draft.Constraints.Count == 0 ? ["- (none)"] : draft.Constraints.Select(item => $"- {item}"));
        lines.Add(string.Empty);
        lines.Add("## Notes");
        lines.AddRange(draft.Notes.Count == 0 ? ["- (none)"] : draft.Notes.Select(item => $"- {item}"));
        lines.Add(string.Empty);
        lines.Add("## Methodology");
        lines.Add($"- summary: {draft.MethodologySummary ?? "(none)"}");
        lines.Add($"- recommended_action: {draft.MethodologyRecommendedAction ?? "(none)"}");
        lines.AddRange(draft.MethodologyRelatedCards.Count == 0
            ? ["- related_cards: (none)"]
            : draft.MethodologyRelatedCards.Select(item => $"- related_card: {item}"));
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Draft field '{field}' is required.");
        }

        return value.Trim();
    }

    private static IReadOnlyList<string> NormalizeRequiredList(IReadOnlyList<string>? values, string field)
    {
        var normalized = NormalizeOptionalList(values);
        if (normalized.Count == 0)
        {
            throw new InvalidOperationException($"Draft field '{field}' must contain at least one item.");
        }

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeOptionalList(IReadOnlyList<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string BuildNextCardId()
    {
        var markdownIds = Directory.Exists(paths.CardsRoot)
            ? Directory.GetFiles(paths.CardsRoot, "CARD-*.md", SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetFileNameWithoutExtension(path))
            : Enumerable.Empty<string>();
        var managedIds = cardDraftRepository.List().Select(item => item.CardId);
        var max = markdownIds
            .Concat(managedIds)
            .Select(ParseCardSequence)
            .DefaultIfEmpty(0)
            .Max();
        return $"CARD-{max + 1:000}";
    }

    private static string BuildTaskGraphDraftId(string cardId)
    {
        return $"TG-{cardId}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
    }

    private sealed class CardDraftSubmission
    {
        public string? CardId { get; init; }

        public string? Title { get; init; }

        public string? Goal { get; init; }

        public IReadOnlyList<string>? Acceptance { get; init; }

        public IReadOnlyList<string>? Scope { get; init; }

        public IReadOnlyList<string>? Constraints { get; init; }

        public IReadOnlyList<string>? Notes { get; init; }

        public AcceptanceContract? AcceptanceContract { get; init; }

        public CardRealityModel? RealityModel { get; init; }

        [JsonPropertyName("planning_lineage")]
        public PlanningLineage? PlanningLineage { get; init; }
    }

    private sealed class TaskGraphDraftSubmission
    {
        public string? DraftId { get; init; }

        public string? CardId { get; init; }

        [JsonPropertyName("planning_lineage")]
        public PlanningLineage? PlanningLineage { get; init; }

        public IReadOnlyList<TaskGraphDraftTaskSubmission> Tasks { get; init; } = Array.Empty<TaskGraphDraftTaskSubmission>();
    }

    private sealed class TaskGraphDraftTaskSubmission
    {
        public string? TaskId { get; init; }

        public string? Title { get; init; }

        public string? Description { get; init; }

        public TaskType TaskType { get; init; } = TaskType.Execution;

        public string? Priority { get; init; }

        public IReadOnlyList<string>? Dependencies { get; init; }

        public IReadOnlyList<string>? Scope { get; init; }

        public IReadOnlyList<string>? Acceptance { get; init; }

        public IReadOnlyList<string>? Constraints { get; init; }

        public AcceptanceContract? AcceptanceContract { get; init; }

        public RealityProofTarget? ProofTarget { get; init; }

        public TaskRoleBinding? RoleBinding { get; init; }
    }

    private static int ParseCardSequence(string? cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId) || !cardId.StartsWith("CARD-", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var suffix = cardId["CARD-".Length..];
        return int.TryParse(suffix, out var parsed) ? parsed : 0;
    }

    private static CardRealityModel? NormalizeRealityModel(CardRealityModel? model)
    {
        if (model is null)
        {
            return null;
        }

        return new CardRealityModel
        {
            OuterVision = Require(model.OuterVision, "reality_model.outer_vision"),
            CurrentSolidScope = Require(model.CurrentSolidScope, "reality_model.current_solid_scope"),
            NextRealSlice = Require(model.NextRealSlice, "reality_model.next_real_slice"),
            RealityState = model.RealityState,
            SolidityClass = model.SolidityClass,
            ProofTarget = new RealityProofTarget
            {
                Kind = model.ProofTarget.Kind,
                Description = Require(model.ProofTarget.Description, "reality_model.proof_target.description"),
            },
            NonGoals = NormalizeOptionalList(model.NonGoals),
            IllusionRisk = new IllusionRiskModel
            {
                Level = model.IllusionRisk.Level,
                Reasons = NormalizeOptionalList(model.IllusionRisk.Reasons),
            },
            PromotionGate = Require(model.PromotionGate, "reality_model.promotion_gate"),
        };
    }

    private static RealityProofTarget? NormalizeTaskProofTarget(RealityProofTarget? model, string fieldPrefix)
    {
        if (model is null)
        {
            return null;
        }

        return new RealityProofTarget
        {
            Kind = model.Kind,
            Description = Require(model.Description, $"{fieldPrefix}.description"),
        };
    }

    private static TaskRoleBinding? NormalizeTaskRoleBinding(TaskRoleBinding? binding)
    {
        if (binding is null)
        {
            return null;
        }

        var normalized = new TaskRoleBinding
        {
            Producer = binding.Producer?.Trim() ?? string.Empty,
            Executor = binding.Executor?.Trim() ?? string.Empty,
            Reviewer = binding.Reviewer?.Trim() ?? string.Empty,
            Approver = binding.Approver?.Trim() ?? string.Empty,
            ScopeSteward = binding.ScopeSteward?.Trim() ?? string.Empty,
            PolicyOwner = binding.PolicyOwner?.Trim() ?? string.Empty,
        };

        return string.IsNullOrWhiteSpace(normalized.Producer)
               && string.IsNullOrWhiteSpace(normalized.Executor)
               && string.IsNullOrWhiteSpace(normalized.Reviewer)
               && string.IsNullOrWhiteSpace(normalized.Approver)
               && string.IsNullOrWhiteSpace(normalized.ScopeSteward)
               && string.IsNullOrWhiteSpace(normalized.PolicyOwner)
            ? null
            : normalized;
    }

    private static TaskRoleBinding MergeRoleBinding(TaskRoleBinding defaults, TaskRoleBinding? binding)
    {
        if (binding is null)
        {
            return defaults;
        }

        return new TaskRoleBinding
        {
            Producer = string.IsNullOrWhiteSpace(binding.Producer) ? defaults.Producer : binding.Producer,
            Executor = string.IsNullOrWhiteSpace(binding.Executor) ? defaults.Executor : binding.Executor,
            Reviewer = string.IsNullOrWhiteSpace(binding.Reviewer) ? defaults.Reviewer : binding.Reviewer,
            Approver = string.IsNullOrWhiteSpace(binding.Approver) ? defaults.Approver : binding.Approver,
            ScopeSteward = string.IsNullOrWhiteSpace(binding.ScopeSteward) ? defaults.ScopeSteward : binding.ScopeSteward,
            PolicyOwner = string.IsNullOrWhiteSpace(binding.PolicyOwner) ? defaults.PolicyOwner : binding.PolicyOwner,
        };
    }

    private static PlanningLineage? NormalizePlanningLineage(PlanningLineage? lineage)
    {
        if (lineage is null)
        {
            return null;
        }

        var planningSlotId = Require(lineage.PlanningSlotId, "planning_lineage.planning_slot_id");
        var activePlanningCardId = Require(lineage.ActivePlanningCardId, "planning_lineage.active_planning_card_id");
        var sourceIntentDraftId = Require(lineage.SourceIntentDraftId, "planning_lineage.source_intent_draft_id");
        return new PlanningLineage
        {
            PlanningSlotId = planningSlotId,
            ActivePlanningCardId = activePlanningCardId,
            SourceIntentDraftId = sourceIntentDraftId,
            SourceCandidateCardId = NormalizeOptionalValue(lineage.SourceCandidateCardId),
            FormalPlanningState = lineage.FormalPlanningState,
        };
    }

    private static string ToSnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }
}
