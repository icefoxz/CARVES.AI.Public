using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Workers;

public sealed class WorkerRequestFactory
{
    private readonly string repoRoot;
    private readonly SystemConfig systemConfig;
    private readonly IGitClient gitClient;
    private readonly WorkerAdapterRegistry workerAdapterRegistry;
    private readonly WorkerAiRequestFactory workerAiRequestFactory;
    private readonly IWorktreeManager worktreeManager;
    private readonly WorktreeRuntimeService worktreeRuntimeService;
    private readonly RuntimeIncidentTimelineService incidentTimelineService;
    private readonly MemoryService memoryService;
    private readonly ContextPackService contextPackService;
    private readonly WorkerExecutionBoundaryService boundaryService;
    private readonly WorkerSelectionPolicyService selectionPolicyService;
    private readonly Planning.ExecutionPacketCompilerService executionPacketCompilerService;
    private readonly Planning.FormalPlanningExecutionGateService formalPlanningExecutionGateService;
    private readonly Planning.ModeExecutionEntryGateService modeExecutionEntryGateService;
    private readonly RuntimePackVerificationRecipeAdmissionService? runtimePackVerificationRecipeAdmissionService;

    public WorkerRequestFactory(
        string repoRoot,
        SystemConfig systemConfig,
        IGitClient gitClient,
        WorkerAdapterRegistry workerAdapterRegistry,
        WorkerAiRequestFactory workerAiRequestFactory,
        IWorktreeManager worktreeManager,
        WorktreeRuntimeService worktreeRuntimeService,
        RuntimeIncidentTimelineService incidentTimelineService,
        MemoryService memoryService,
        ContextPackService contextPackService,
        WorkerExecutionBoundaryService boundaryService,
        WorkerSelectionPolicyService selectionPolicyService,
        Planning.ExecutionPacketCompilerService executionPacketCompilerService,
        Planning.FormalPlanningExecutionGateService? formalPlanningExecutionGateService = null,
        RuntimePackVerificationRecipeAdmissionService? runtimePackVerificationRecipeAdmissionService = null)
    {
        this.repoRoot = repoRoot;
        this.systemConfig = systemConfig;
        this.gitClient = gitClient;
        this.workerAdapterRegistry = workerAdapterRegistry;
        this.workerAiRequestFactory = workerAiRequestFactory;
        this.worktreeManager = worktreeManager;
        this.worktreeRuntimeService = worktreeRuntimeService;
        this.incidentTimelineService = incidentTimelineService;
        this.memoryService = memoryService;
        this.contextPackService = contextPackService;
        this.boundaryService = boundaryService;
        this.selectionPolicyService = selectionPolicyService;
        this.executionPacketCompilerService = executionPacketCompilerService;
        this.formalPlanningExecutionGateService = formalPlanningExecutionGateService ?? new Planning.FormalPlanningExecutionGateService();
        this.modeExecutionEntryGateService = new Planning.ModeExecutionEntryGateService(this.formalPlanningExecutionGateService);
        this.runtimePackVerificationRecipeAdmissionService = runtimePackVerificationRecipeAdmissionService;
    }

    public WorkerRequest Create(TaskNode task, bool dryRun, WorkerSelectionOptions? selectionOptions = null)
    {
        modeExecutionEntryGateService.EnsureReadyForExecution(task);
        var currentCommit = gitClient.TryGetCurrentCommit(repoRoot);
        var worktreeRoot = worktreeManager.PrepareWorktree(systemConfig, repoRoot, task.TaskId, currentCommit);
        var selection = selectionPolicyService.Evaluate(task, options: selectionOptions);
        var backendId = selection.SelectedBackendId ?? workerAdapterRegistry.ActiveAdapter.BackendId;
        var adapter = workerAdapterRegistry.Resolve(backendId);
        var profile = selection.Profile ?? boundaryService.ResolveProfile(backendId, task, selection.RepoId);
        var worktreeRecord = worktreeRuntimeService.RecordPrepared(task.TaskId, worktreeRoot, currentCommit);
        if (worktreeRecord.State == WorktreeRuntimeState.Rebuilt)
        {
            incidentTimelineService.Append(new RuntimeIncidentRecord
            {
                IncidentType = RuntimeIncidentType.WorktreeRebuilt,
                RepoId = selection.RepoId ?? "local-repo",
                TaskId = task.TaskId,
                BackendId = selection.SelectedBackendId ?? backendId,
                ProviderId = selection.SelectedProviderId ?? adapter.ProviderId,
                RecoveryAction = WorkerRecoveryAction.RebuildWorktree,
                ActorKind = RuntimeIncidentActorKind.System,
                ActorIdentity = nameof(WorktreeRuntimeService),
                ReasonCode = "worktree_rebuilt",
                Summary = $"Prepared rebuilt worktree '{worktreeRoot}' for task '{task.TaskId}'.",
                ConsequenceSummary = $"Rebuilt from '{worktreeRecord.RebuiltFromWorktreePath}'.",
                ReferenceId = worktreeRecord.RecordId,
            });
        }
        var packVerification = runtimePackVerificationRecipeAdmissionService?.Resolve(task, task.Validation.Commands)
            ?? RuntimePackVerificationRecipeAdmissionResult.None(task.Validation.Commands, "Runtime Pack verification recipe admission service is unavailable.");
        var executionTask = CreateValidationOverlayTask(task, packVerification.EffectiveValidationCommands);
        var memory = memoryService.BundleForTask(executionTask);
        var contextPack = contextPackService.BuildForTask(executionTask, selection.SelectedProviderId ?? backendId);
        var executionPacket = executionPacketCompilerService.CompileAndPersist(executionTask, memory);
        var executionRequest = workerAiRequestFactory.Create(
            executionTask,
            contextPack,
            executionPacket,
            executionPacketCompilerService.GetPacketRepoRelativePath(executionTask.TaskId),
            profile,
            repoRoot,
            worktreeRoot,
            currentCommit,
            dryRun,
            backendId,
            packVerification.EffectiveValidationCommands,
            selection,
            BuildRuntimePackVerificationMetadata(packVerification));
        var session = new ExecutionSession(
            task.TaskId,
            task.Title,
            repoRoot,
            dryRun,
            currentCommit,
            adapter.AdapterId,
            worktreeRoot,
            DateTimeOffset.UtcNow)
        {
            RepoId = selection.RepoId ?? "local-repo",
            WorkerProfileId = profile.ProfileId,
            WorkerProfileTrusted = profile.Trusted,
            WorkerBackend = backendId,
            WorkerProviderId = selection.SelectedProviderId ?? adapter.ProviderId,
            WorkerRoutingProfileId = selection.SelectedRoutingProfileId,
            ActiveRoutingProfileId = selection.ActiveRoutingProfileId,
            WorkerRoutingRuleId = selection.AppliedRoutingRuleId,
            WorkerRoutingIntent = selection.RoutingIntent,
            WorkerRoutingModuleId = selection.RoutingModuleId,
            WorkerModelId = selection.SelectedModelId,
            WorkerRouteSource = selection.RouteSource,
            WorkerSelectionSummary = selection.Summary,
            WorkerRequestBudget = executionRequest.RequestBudget,
            RequestedWorkerThreadId = executionRequest.PriorThreadId,
        };

        return new WorkerRequest
        {
            Task = executionTask,
            Session = session,
            Memory = memory,
            Packet = executionPacket,
            ValidationCommands = packVerification.EffectiveValidationCommands,
            Selection = selection,
            ExecutionRequest = executionRequest,
        };
    }

    private static TaskNode CreateValidationOverlayTask(
        TaskNode task,
        IReadOnlyList<IReadOnlyList<string>> validationCommands)
    {
        if (task.Validation.Commands.Count == validationCommands.Count
            && task.Validation.Commands.Zip(validationCommands, static (left, right) => left.SequenceEqual(right, StringComparer.Ordinal)).All(static match => match))
        {
            return task;
        }

        return new TaskNode
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            TaskType = task.TaskType,
            Priority = task.Priority,
            Source = task.Source,
            CardId = task.CardId,
            ProposalSource = task.ProposalSource,
            ProposalReason = task.ProposalReason,
            ProposalConfidence = task.ProposalConfidence,
            ProposalPriorityHint = task.ProposalPriorityHint,
            BaseCommit = task.BaseCommit,
            ResultCommit = task.ResultCommit,
            Dependencies = task.Dependencies,
            Scope = task.Scope,
            Acceptance = task.Acceptance,
            Constraints = task.Constraints,
            AcceptanceContract = task.AcceptanceContract,
            Validation = new ValidationPlan
            {
                Commands = validationCommands,
                Checks = task.Validation.Checks,
                ExpectedEvidence = task.Validation.ExpectedEvidence,
            },
            RetryCount = task.RetryCount,
            Capabilities = task.Capabilities,
            Metadata = task.Metadata,
            LastWorkerRunId = task.LastWorkerRunId,
            LastWorkerBackend = task.LastWorkerBackend,
            LastWorkerFailureKind = task.LastWorkerFailureKind,
            LastWorkerRetryable = task.LastWorkerRetryable,
            LastWorkerSummary = task.LastWorkerSummary,
            LastWorkerDetailRef = task.LastWorkerDetailRef,
            LastProviderDetailRef = task.LastProviderDetailRef,
            LastRecoveryAction = task.LastRecoveryAction,
            LastRecoveryReason = task.LastRecoveryReason,
            RetryNotBefore = task.RetryNotBefore,
            PlannerReview = task.PlannerReview,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
        };
    }

    private static IReadOnlyDictionary<string, string>? BuildRuntimePackVerificationMetadata(
        RuntimePackVerificationRecipeAdmissionResult result)
    {
        if (!result.HasRuntimePackContribution)
        {
            return null;
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["runtime_pack_verification_recipe_ids"] = string.Join("|", result.RecipeIds),
            ["runtime_pack_command_admission_decision_ids"] = string.Join("|", result.DecisionIds),
            ["runtime_pack_command_admission_paths"] = string.Join("|", result.DecisionPaths),
            ["runtime_pack_verification_admitted_count"] = result.AdmittedCommandCount.ToString(),
            ["runtime_pack_verification_elevated_count"] = result.ElevatedRiskCommandCount.ToString(),
            ["runtime_pack_verification_blocked_count"] = result.BlockedCommandCount.ToString(),
            ["runtime_pack_verification_rejected_count"] = result.RejectedCommandCount.ToString(),
            ["runtime_pack_verification_summary"] = result.Summary,
        };
    }
}
