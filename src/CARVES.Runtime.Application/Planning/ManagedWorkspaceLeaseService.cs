using System.Text.Json;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Platform.SurfaceModels;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Planning;

public sealed class ManagedWorkspaceLeaseService
{
    private static readonly IReadOnlyList<string> DefaultAllowedOperationClasses =
    [
        "inspect",
        "edit",
        "create",
        "validate",
        "test",
        "return_patch",
    ];

    private static readonly IReadOnlyList<string> DefaultAllowedToolsOrAdapters =
    [
        "cli",
        "local_host",
        "agent_gateway",
        "ide_agent",
    ];

    private readonly string repoRoot;
    private readonly FormalPlanningPacketService formalPlanningPacketService;
    private readonly TaskGraphService taskGraphService;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly IGitClient gitClient;
    private readonly IWorktreeManager worktreeManager;
    private readonly WorktreeRuntimeService worktreeRuntimeService;
    private readonly IManagedWorkspaceLeaseRepository repository;
    private readonly SystemConfig systemConfig;

    public ManagedWorkspaceLeaseService(
        string repoRoot,
        SystemConfig systemConfig,
        FormalPlanningPacketService formalPlanningPacketService,
        TaskGraphService taskGraphService,
        IGitClient gitClient,
        IWorktreeManager worktreeManager,
        WorktreeRuntimeService worktreeRuntimeService,
        IManagedWorkspaceLeaseRepository repository)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.systemConfig = systemConfig;
        this.formalPlanningPacketService = formalPlanningPacketService;
        this.taskGraphService = taskGraphService;
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.gitClient = gitClient;
        this.worktreeManager = worktreeManager;
        this.worktreeRuntimeService = worktreeRuntimeService;
        this.repository = repository;
    }

    public ManagedWorkspaceLease IssueForTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new InvalidOperationException("Usage: plan issue-workspace <task-id>");
        }

        var packet = formalPlanningPacketService.BuildCurrentPacket();
        var graph = taskGraphService.Load();
        if (!graph.Tasks.TryGetValue(taskId.Trim(), out var task))
        {
            throw new InvalidOperationException($"Task '{taskId}' was not found.");
        }

        if (IsTerminal(task.Status))
        {
            throw new InvalidOperationException($"Task '{task.TaskId}' is already terminal and cannot receive a managed workspace lease.");
        }

        var lineage = PlanningLineageMetadata.TryRead(task.Metadata)
            ?? throw new InvalidOperationException($"Task '{task.TaskId}' is not bound to planning lineage truth and cannot receive a managed workspace lease.");
        var expectedPlanHandle = FormalPlanningPacketService.BuildPlanHandle(lineage);
        if (!string.Equals(expectedPlanHandle, packet.PlanHandle, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Task '{task.TaskId}' is bound to plan handle '{expectedPlanHandle}', not the current active plan handle '{packet.PlanHandle}'.");
        }

        var baseCommit = string.IsNullOrWhiteSpace(task.BaseCommit)
            ? gitClient.TryGetCurrentCommit(repoRoot) ?? string.Empty
            : task.BaseCommit!;
        var worktreePath = worktreeManager.PrepareWorktree(systemConfig, repoRoot, task.TaskId, baseCommit);
        var worktreeRecord = worktreeRuntimeService.RecordPrepared(task.TaskId, worktreePath, baseCommit);
        var now = DateTimeOffset.UtcNow;
        var snapshot = repository.Load();
        var leases = snapshot.Leases
            .Select(existing => SupersedeIfConflicting(existing, task.TaskId, now))
            .ToList();
        var lease = new ManagedWorkspaceLease
        {
            WorkspaceId = $"workspace-{task.TaskId.ToLowerInvariant()}-{now:yyyyMMddHHmmss}",
            PlanHandle = packet.PlanHandle,
            PlanningSlotId = packet.PlanningSlotId,
            PlanningCardId = packet.PlanningCardId,
            SourceIntentDraftId = packet.SourceIntentDraftId,
            SourceCandidateCardId = packet.SourceCandidateCardId,
            TaskId = task.TaskId,
            CardId = task.CardId,
            WorkspacePath = worktreePath,
            RepoRoot = repoRoot,
            BaseCommit = baseCommit,
            WorktreeRuntimeRecordId = worktreeRecord.RecordId,
            AllowedWritablePaths = BuildAllowedWritablePaths(task, packet),
            AllowedOperationClasses = DefaultAllowedOperationClasses,
            AllowedToolsOrAdapters = DefaultAllowedToolsOrAdapters,
            PathPolicies = BuildDefaultPathPolicies(),
            ApprovalPosture = "host_routed_review_and_writeback_required",
            CleanupPosture = systemConfig.RemoveWorktreeOnSuccess
                ? "cleanup_on_acceptance_or_expiry"
                : "manual_cleanup_after_expiry",
            ExpiresAt = now.AddHours(24),
        };

        leases.Add(lease);
        repository.Save(new ManagedWorkspaceLeaseSnapshot
        {
            Leases = leases.OrderByDescending(item => item.CreatedAt).ToArray(),
        });

        return lease;
    }

    public IReadOnlyList<ManagedWorkspaceLease> LoadActive(string? taskId = null)
    {
        var graph = taskGraphService.Load();
        return repository.Load().Leases
            .Where(lease => lease.Status == ManagedWorkspaceLeaseStatus.Active)
            .Where(lease => !IsLeaseBoundToTerminalTask(lease, graph))
            .Where(lease => string.IsNullOrWhiteSpace(taskId) || string.Equals(lease.TaskId, taskId, StringComparison.Ordinal))
            .OrderByDescending(lease => lease.CreatedAt)
            .ToArray();
    }

    public int ReleaseForTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        var releasedCount = 0;
        var snapshot = repository.Load();
        foreach (var lease in snapshot.Leases)
        {
            if (lease.Status != ManagedWorkspaceLeaseStatus.Active
                || !string.Equals(lease.TaskId, taskId.Trim(), StringComparison.Ordinal))
            {
                continue;
            }

            lease.Status = ManagedWorkspaceLeaseStatus.Released;
            lease.UpdatedAt = now;
            releasedCount++;
        }

        if (releasedCount > 0)
        {
            repository.Save(snapshot);
        }

        return releasedCount;
    }

    public ManagedWorkspaceSubmissionCandidate BuildSubmissionCandidate(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new InvalidOperationException("Usage: plan submit-workspace <task-id>");
        }

        var task = taskGraphService.GetTask(taskId.Trim());
        var lease = LoadActive(task.TaskId).FirstOrDefault()
            ?? throw new InvalidOperationException($"Task '{task.TaskId}' has no active managed workspace lease to submit.");
        if (string.IsNullOrWhiteSpace(lease.WorkspacePath) || !Directory.Exists(lease.WorkspacePath))
        {
            throw new InvalidOperationException($"Task '{task.TaskId}' active managed workspace is unavailable: {lease.WorkspacePath}");
        }

        var changedPaths = CollectSubmissionChangedPaths(lease);
        if (changedPaths.Count == 0)
        {
            throw new InvalidOperationException($"Task '{task.TaskId}' managed workspace has no changed files to submit.");
        }

        var policy = EvaluatePathPolicy(task.TaskId, changedPaths);
        if (policy.ScopeEscapeCount > 0 || policy.HostOnlyCount > 0 || policy.DenyCount > 0)
        {
            throw new InvalidOperationException(
                $"Task '{task.TaskId}' managed workspace submission is blocked: {policy.Summary} {policy.RecommendedNextAction}.");
        }

        foreach (var changedPath in changedPaths)
        {
            var sourcePath = Path.Combine(lease.WorkspacePath, changedPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourcePath))
            {
                throw new InvalidOperationException($"Task '{task.TaskId}' managed workspace submission path '{changedPath}' is missing from the workspace.");
            }
        }

        return new ManagedWorkspaceSubmissionCandidate
        {
            TaskId = task.TaskId,
            Lease = lease,
            ChangedPaths = changedPaths,
            PathPolicy = policy,
        };
    }

    public ManagedWorkspacePathPolicyAssessment EvaluatePathPolicy(string taskId, IEnumerable<string> changedPaths)
    {
        return new ManagedWorkspacePathPolicyService(repoRoot, repository).Evaluate(taskId, changedPaths);
    }

    public RuntimeManagedWorkspaceSurface BuildSurface()
    {
        var errors = new List<string>();
        const string policyDocumentPath = "docs/runtime/runtime-managed-workspace-file-operation-model.md";
        const string workingModesDocumentPath = "docs/runtime/runtime-agent-working-modes-and-constraint-ladder.md";
        const string implementationPlanPath = "docs/runtime/runtime-agent-working-modes-implementation-plan.md";
        const string modeDHardeningDocumentPath = "docs/runtime/runtime-mode-d-scoped-task-workspace-hardening.md";

        ValidatePath(policyDocumentPath, "Managed workspace policy document", errors);
        ValidatePath(workingModesDocumentPath, "Working modes doctrine document", errors);
        ValidatePath(implementationPlanPath, "Working modes implementation plan", errors);
        ValidatePath(modeDHardeningDocumentPath, "Mode D scoped task workspace hardening document", errors);

        var packet = formalPlanningPacketService.TryBuildCurrentPacket();
        var graph = taskGraphService.Load();
        var snapshot = repository.Load();
        var now = DateTimeOffset.UtcNow;
        var activeLeases = snapshot.Leases
            .Where(lease => lease.Status == ManagedWorkspaceLeaseStatus.Active)
            .Where(lease => !IsLeaseBoundToTerminalTask(lease, graph))
            .Where(lease => packet is null || string.Equals(lease.PlanHandle, packet.PlanHandle, StringComparison.Ordinal))
            .OrderByDescending(lease => lease.CreatedAt)
            .Select(ToLeaseSurface)
            .ToArray();
        var recoverableResidues = snapshot.Leases
            .Select(lease => BuildRecoverableResidueSurface(lease, graph, packet, now))
            .Where(static residue => residue is not null)
            .Cast<RuntimeManagedWorkspaceResidueSurface>()
            .OrderBy(residue => residue.ResidueClass, StringComparer.Ordinal)
            .ThenBy(residue => residue.LeaseId, StringComparer.Ordinal)
            .ToArray();
        var highestRecoverableResidueSeverity = ResolveHighestRecoverableResidueSeverity(recoverableResidues);
        var recoverableResidueBlocksAutoRun = recoverableResidues.Any(static residue => residue.BlocksAutoRun);
        var pathPolicies = BuildDefaultPathPolicies();
        var modeDHardeningState = ResolveModeDHardeningState(packet, activeLeases, pathPolicies, errors);
        var overallPosture = ResolveOverallPosture(packet, activeLeases, errors);

        return new RuntimeManagedWorkspaceSurface
        {
            PolicyDocumentPath = policyDocumentPath,
            WorkingModesDocumentPath = workingModesDocumentPath,
            ImplementationPlanPath = implementationPlanPath,
            ModeDHardeningDocumentPath = modeDHardeningDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            OverallPosture = overallPosture,
            OperationalState = ResolveOperationalState(overallPosture, recoverableResidues),
            SafeToStartNewExecution = !recoverableResidueBlocksAutoRun,
            SafeToDiscuss = true,
            SafeToCleanup = recoverableResidues.Length > 0,
            ModeDHardeningState = modeDHardeningState,
            PlanHandle = packet?.PlanHandle,
            PlanningCardId = packet?.PlanningCardId,
            FormalPlanningState = packet is null
                ? null
                : JsonNamingPolicy.SnakeCaseLower.ConvertName(packet.FormalPlanningState.ToString()),
            PathPolicyEnforcementState = "active",
            PathPolicyEnforcementSummary = "scoped path policy enforcement now distinguishes workspace_open, review_required, host_only, deny, and lease scope escape before official ingress.",
            BoundTaskIds = packet?.LinkedTruth.TaskIds ?? Array.Empty<string>(),
            ActiveLeases = activeLeases,
            RecoverableResiduePosture = recoverableResidues.Length == 0
                ? ControlPlaneResidueContract.NoRecoverableResiduePosture
                : ControlPlaneResidueContract.RecoverableResiduePresentPosture,
            RecoverableResidueCount = recoverableResidues.Length,
            HighestRecoverableResidueSeverity = highestRecoverableResidueSeverity,
            RecoverableResidueBlocksAutoRun = recoverableResidueBlocksAutoRun,
            RecoverableResidueSummary = ResolveRecoverableResidueSummary(recoverableResidues),
            RecoverableResidueRecommendedNextAction = ResolveRecoverableResidueRecommendedNextAction(recoverableResidues),
            RecoverableCleanupActionId = recoverableResidues.Length == 0 ? string.Empty : ControlPlaneResidueContract.CleanupActionId,
            RecoverableCleanupActionMode = recoverableResidues.Length == 0
                ? ControlPlaneResidueContract.NoCleanupActionMode
                : ControlPlaneResidueContract.CleanupActionMode,
            AvailableActions = BuildRecoverableCleanupActions(recoverableResidues.Length > 0),
            RecoverableResidues = recoverableResidues,
            PathPolicies = pathPolicies.Select(rule => new RuntimeManagedWorkspacePathPolicySurface
            {
                PolicyClass = rule.PolicyClass,
                Summary = rule.Summary,
                EnforcementEffect = rule.EnforcementEffect,
                Examples = rule.Examples,
            }).ToArray(),
            ModeDHardeningChecks = BuildModeDHardeningChecks(packet, activeLeases, pathPolicies, errors),
            RecommendedNextAction = ResolveRecommendedNextAction(packet, activeLeases, modeDHardeningState),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not make the leased workspace official truth; review and writeback still decide ingress.",
                "This surface does not replace worker execution worktrees or introduce a second execution store.",
                "This surface does not claim OS or process isolation; Mode D remains a scoped workspace and host-ingress enforcement profile.",
                "This surface does not broker patch-only execution or Mode E result-bundle execution.",
            ],
        };
    }

    private static bool IsTerminal(DomainTaskStatus status)
    {
        return status is DomainTaskStatus.Completed or DomainTaskStatus.Merged or DomainTaskStatus.Discarded or DomainTaskStatus.Superseded;
    }

    private static ManagedWorkspaceLease SupersedeIfConflicting(ManagedWorkspaceLease existing, string taskId, DateTimeOffset updatedAt)
    {
        if (existing.Status != ManagedWorkspaceLeaseStatus.Active
            || !string.Equals(existing.TaskId, taskId, StringComparison.Ordinal))
        {
            return existing;
        }

        return new ManagedWorkspaceLease
        {
            SchemaVersion = existing.SchemaVersion,
            LeaseId = existing.LeaseId,
            WorkspaceId = existing.WorkspaceId,
            PlanHandle = existing.PlanHandle,
            PlanningSlotId = existing.PlanningSlotId,
            PlanningCardId = existing.PlanningCardId,
            SourceIntentDraftId = existing.SourceIntentDraftId,
            SourceCandidateCardId = existing.SourceCandidateCardId,
            TaskId = existing.TaskId,
            CardId = existing.CardId,
            WorkspacePath = existing.WorkspacePath,
            RepoRoot = existing.RepoRoot,
            BaseCommit = existing.BaseCommit,
            WorktreeRuntimeRecordId = existing.WorktreeRuntimeRecordId,
            Status = ManagedWorkspaceLeaseStatus.Superseded,
            AllowedWritablePaths = existing.AllowedWritablePaths,
            AllowedOperationClasses = existing.AllowedOperationClasses,
            AllowedToolsOrAdapters = existing.AllowedToolsOrAdapters,
            PathPolicies = existing.PathPolicies,
            ApprovalPosture = existing.ApprovalPosture,
            CleanupPosture = existing.CleanupPosture,
            ExpiresAt = existing.ExpiresAt,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = updatedAt,
        };
    }

    private static IReadOnlyList<string> BuildAllowedWritablePaths(TaskNode task, FormalPlanningPacket packet)
    {
        var taskScope = task.Scope
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return taskScope.Length > 0
            ? taskScope
            : packet.AllowedScopeSummary
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
    }

    private static IReadOnlyList<ManagedWorkspacePathPolicyRule> BuildDefaultPathPolicies()
    {
        return ManagedWorkspacePathPolicyService.BuildDefaultRules();
    }

    private IReadOnlyList<string> CollectSubmissionChangedPaths(ManagedWorkspaceLease lease)
    {
        var changedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (gitClient.IsRepository(lease.WorkspacePath))
        {
            foreach (var path in gitClient.GetChangedPathsSince(lease.WorkspacePath, lease.BaseCommit))
            {
                AddIfSubmissionPath(changedPaths, path);
            }

            foreach (var path in gitClient.GetUncommittedPaths(lease.WorkspacePath))
            {
                AddIfSubmissionPath(changedPaths, path);
            }
        }

        foreach (var path in lease.AllowedWritablePaths)
        {
            var normalized = NormalizeSubmissionPath(path);
            if (normalized is null)
            {
                continue;
            }

            var workspacePath = Path.Combine(lease.WorkspacePath, normalized.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(workspacePath))
            {
                continue;
            }

            var targetPath = Path.Combine(repoRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(targetPath) || !FilesAreEqual(workspacePath, targetPath))
            {
                changedPaths.Add(normalized);
            }
        }

        return changedPaths
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddIfSubmissionPath(HashSet<string> paths, string path)
    {
        var normalized = NormalizeSubmissionPath(path);
        if (normalized is not null)
        {
            paths.Add(normalized);
        }
    }

    private static string? NormalizeSubmissionPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalized = path.Replace('\\', '/').Trim();
        if (normalized.EndsWith("/", StringComparison.Ordinal)
            || normalized.EndsWith("/.", StringComparison.Ordinal)
            || string.Equals(normalized, ".carves-worktree.json", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".git/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized;
    }

    private static bool FilesAreEqual(string leftPath, string rightPath)
    {
        var leftInfo = new FileInfo(leftPath);
        var rightInfo = new FileInfo(rightPath);
        if (leftInfo.Length != rightInfo.Length)
        {
            return false;
        }

        return File.ReadAllBytes(leftPath).SequenceEqual(File.ReadAllBytes(rightPath));
    }

    private static string ResolveModeDHardeningState(
        FormalPlanningPacket? packet,
        IReadOnlyList<RuntimeManagedWorkspaceLeaseSurface> activeLeases,
        IReadOnlyList<ManagedWorkspacePathPolicyRule> pathPolicies,
        IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            return "blocked_by_mode_d_doctrine_gaps";
        }

        if (packet is null)
        {
            return "plan_init_required";
        }

        if (packet.FormalPlanningState == FormalPlanningState.Closed && activeLeases.Count == 0)
        {
            return "closed_no_active_workspace";
        }

        if (packet.LinkedTruth.TaskIds.Count == 0)
        {
            return "waiting_for_bound_task_truth";
        }

        if (activeLeases.Count == 0)
        {
            return "workspace_issue_required";
        }

        if (activeLeases.Any(lease => lease.AllowedWritablePaths.Count == 0))
        {
            return "incomplete_lease_scope";
        }

        return HasRequiredModeDPolicyClasses(pathPolicies)
            ? "active"
            : "blocked_by_path_policy_gaps";
    }

    private static IReadOnlyList<RuntimeManagedWorkspaceHardeningCheckSurface> BuildModeDHardeningChecks(
        FormalPlanningPacket? packet,
        IReadOnlyList<RuntimeManagedWorkspaceLeaseSurface> activeLeases,
        IReadOnlyList<ManagedWorkspacePathPolicyRule> pathPolicies,
        IReadOnlyList<string> errors)
    {
        var hasPacket = packet is not null;
        var hasBoundTaskTruth = packet is not null && packet.LinkedTruth.TaskIds.Count > 0;
        var hasActiveLease = activeLeases.Count > 0;
        var hasDeclaredWritableScope = hasActiveLease && activeLeases.All(lease => lease.AllowedWritablePaths.Count > 0);
        var hasRequiredPolicyClasses = HasRequiredModeDPolicyClasses(pathPolicies);

        return
        [
            BuildHardeningCheck(
                "mode_d_doctrine_anchor",
                errors.Count == 0,
                "Mode D hardening doctrine is present and versioned with the managed-workspace surface.",
                "restore missing Mode D doctrine anchors before claiming scoped workspace hardening"),
            BuildHardeningCheck(
                "formal_plan_bound",
                hasPacket,
                "Mode D issuance is bound to a current formal planning packet rather than a prompt-only task.",
                "run `plan init [candidate-card-id]` before issuing a scoped task workspace"),
            BuildHardeningCheck(
                "task_truth_bound",
                hasBoundTaskTruth,
                "Mode D workspace authority is tied to persisted task truth on the current plan handle.",
                "approve task truth for the active planning card before workspace issuance"),
            BuildHardeningCheck(
                "active_scoped_lease",
                hasActiveLease,
                "At least one active managed workspace lease exists for the current plan handle.",
                hasPacket && hasBoundTaskTruth
                    ? $"run `plan issue-workspace {packet!.LinkedTruth.TaskIds[0]}`"
                    : "complete formal planning and task truth before issuing a workspace"),
            BuildHardeningCheck(
                "declared_writable_scope",
                hasDeclaredWritableScope,
                "Every active Mode D lease declares concrete writable paths instead of opening the whole workspace as scope.",
                "reissue or repair the lease with task-bound writable paths"),
            BuildHardeningCheck(
                "path_policy_fail_closed",
                hasRequiredPolicyClasses,
                "Path policy classes include workspace_open, review_required, scope_escape, host_only, and deny.",
                "restore the complete Mode D path policy class set"),
            new RuntimeManagedWorkspaceHardeningCheckSurface
            {
                CheckId = "official_truth_host_ingress",
                State = "satisfied",
                Summary = "Official task, memory, review, and platform truth remains host-routed even when an IDE agent owns the leased workspace.",
                RequiredAction = "return changes through review/writeback; do not treat the leased workspace as official truth",
            },
        ];
    }

    private static RuntimeManagedWorkspaceHardeningCheckSurface BuildHardeningCheck(
        string checkId,
        bool isSatisfied,
        string satisfiedSummary,
        string requiredAction)
    {
        return new RuntimeManagedWorkspaceHardeningCheckSurface
        {
            CheckId = checkId,
            State = isSatisfied ? "satisfied" : "blocked",
            Summary = isSatisfied ? satisfiedSummary : requiredAction,
            RequiredAction = isSatisfied ? "none" : requiredAction,
        };
    }

    private static bool HasRequiredModeDPolicyClasses(IReadOnlyList<ManagedWorkspacePathPolicyRule> pathPolicies)
    {
        string[] required = ["workspace_open", "review_required", "scope_escape", "host_only", "deny"];
        return required.All(policyClass => pathPolicies.Any(rule => string.Equals(rule.PolicyClass, policyClass, StringComparison.Ordinal)));
    }

    private static RuntimeManagedWorkspaceLeaseSurface ToLeaseSurface(ManagedWorkspaceLease lease)
    {
        return new RuntimeManagedWorkspaceLeaseSurface
        {
            LeaseId = lease.LeaseId,
            WorkspaceId = lease.WorkspaceId,
            TaskId = lease.TaskId,
            CardId = lease.CardId,
            WorkspacePath = lease.WorkspacePath,
            BaseCommit = lease.BaseCommit,
            Status = JsonNamingPolicy.SnakeCaseLower.ConvertName(lease.Status.ToString()),
            ApprovalPosture = lease.ApprovalPosture,
            CleanupPosture = lease.CleanupPosture,
            ExpiresAt = lease.ExpiresAt,
            AllowedWritablePaths = lease.AllowedWritablePaths,
            AllowedOperationClasses = lease.AllowedOperationClasses,
            AllowedToolsOrAdapters = lease.AllowedToolsOrAdapters,
        };
    }

    private static RuntimeManagedWorkspaceResidueSurface? BuildRecoverableResidueSurface(
        ManagedWorkspaceLease lease,
        Carves.Runtime.Domain.Tasks.TaskGraph graph,
        FormalPlanningPacket? packet,
        DateTimeOffset now)
    {
        if (lease.Status != ManagedWorkspaceLeaseStatus.Active)
        {
            return null;
        }

        if (IsLeaseBoundToTerminalTask(lease, graph))
        {
            return BuildResidueSurface(
                lease,
                "terminal_task_active_lease",
                $"Active managed workspace lease '{lease.LeaseId}' is still recorded even though task '{lease.TaskId}' is already terminal.",
                "Run `carves cleanup` before treating the repo as residue-free.");
        }

        if (lease.ExpiresAt <= now)
        {
            return BuildResidueSurface(
                lease,
                "expired_active_lease",
                $"Active managed workspace lease '{lease.LeaseId}' expired at {lease.ExpiresAt:O} and should no longer be treated as a healthy workspace binding.",
                "Run `carves cleanup` and re-read `inspect runtime-managed-workspace`.");
        }

        if (!string.IsNullOrWhiteSpace(lease.WorkspacePath) && !Directory.Exists(lease.WorkspacePath))
        {
            return BuildResidueSurface(
                lease,
                "missing_workspace_path",
                $"Active managed workspace lease '{lease.LeaseId}' points to missing workspace path '{lease.WorkspacePath}'.",
                "Run `carves cleanup` before issuing or trusting another managed workspace lease.");
        }

        if (packet is not null && !string.Equals(lease.PlanHandle, packet.PlanHandle, StringComparison.Ordinal))
        {
            return BuildResidueSurface(
                lease,
                "foreign_plan_active_lease",
                $"Active managed workspace lease '{lease.LeaseId}' is still bound to plan handle '{lease.PlanHandle}', not the current active plan handle '{packet.PlanHandle}'.",
                "Run `carves cleanup` before treating the current planning lineage as residue-free.");
        }

        return null;
    }

    private static RuntimeManagedWorkspaceResidueSurface BuildResidueSurface(
        ManagedWorkspaceLease lease,
        string residueClass,
        string summary,
        string recommendedNextAction)
    {
        return new RuntimeManagedWorkspaceResidueSurface
        {
            ResidueId = $"{residueClass}:{lease.LeaseId}",
            ResidueClass = residueClass,
            Kind = residueClass,
            Severity = ControlPlaneResidueContract.WarningResidueSeverity,
            LeaseId = lease.LeaseId,
            TaskId = lease.TaskId,
            WorkspacePath = string.IsNullOrWhiteSpace(lease.WorkspacePath) ? null : lease.WorkspacePath,
            Summary = summary,
            RecommendedNextAction = recommendedNextAction,
            Recoverable = true,
            BlocksAutoRun = true,
            BlocksHealthyIdle = true,
        };
    }

    private static string ResolveHighestRecoverableResidueSeverity(IReadOnlyList<RuntimeManagedWorkspaceResidueSurface> residues)
    {
        if (residues.Count == 0)
        {
            return ControlPlaneResidueContract.NoResidueSeverity;
        }

        return residues.Any(static residue => string.Equals(residue.Severity, "error", StringComparison.OrdinalIgnoreCase))
            ? ControlPlaneResidueContract.ErrorResidueSeverity
            : ControlPlaneResidueContract.WarningResidueSeverity;
    }

    private static string ResolveRecoverableResidueSummary(IReadOnlyList<RuntimeManagedWorkspaceResidueSurface> residues)
    {
        if (residues.Count == 0)
        {
            return "No recoverable managed workspace residue is currently projected.";
        }

        return residues.Count == 1
            ? residues[0].Summary
            : $"Recoverable managed workspace residue remains in {residues.Count} places; clear it before treating the repo as residue-free.";
    }

    private static string ResolveRecoverableResidueRecommendedNextAction(IReadOnlyList<RuntimeManagedWorkspaceResidueSurface> residues)
    {
        return residues.Count == 0
            ? "No managed workspace cleanup action is currently required."
            : "Run `carves cleanup` and re-read `inspect runtime-managed-workspace` before treating the repo as residue-free.";
    }

    private static IReadOnlyList<RuntimeInteractionActionSurface> BuildRecoverableCleanupActions(bool residuePresent)
    {
        if (!residuePresent)
        {
            return Array.Empty<RuntimeInteractionActionSurface>();
        }

        return
        [
            new RuntimeInteractionActionSurface
            {
                ActionId = ControlPlaneResidueContract.CleanupActionId,
                Label = "Clean recoverable runtime residue",
                Kind = "cleanup",
                Command = "carves cleanup",
                ActionMode = ControlPlaneResidueContract.CleanupActionMode,
                Summary = "Use the existing cleanup route to clear recoverable runtime residue, but keep the cleanup posture dry-run-first and bounded instead of auto-applying broader repair or writeback work.",
            },
        ];
    }

    private static string ResolveOperationalState(
        string overallPosture,
        IReadOnlyList<RuntimeManagedWorkspaceResidueSurface> residues)
    {
        if (residues.Count > 0)
        {
            return ControlPlaneResidueContract.RecoverableResidueHealthState;
        }

        return overallPosture switch
        {
            "blocked_by_managed_workspace_doctrine_gaps" => "blocked",
            "plan_init_required_before_managed_workspace_issuance" => "planning_required",
            "waiting_for_bound_task_truth_before_managed_workspace_issuance" => "planning_required",
            _ => ControlPlaneResidueContract.CleanHealthState,
        };
    }

    private static string ResolveOverallPosture(
        FormalPlanningPacket? packet,
        IReadOnlyList<RuntimeManagedWorkspaceLeaseSurface> activeLeases,
        IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            return "blocked_by_managed_workspace_doctrine_gaps";
        }

        if (packet is null)
        {
            return "plan_init_required_before_managed_workspace_issuance";
        }

        if (packet.FormalPlanningState == FormalPlanningState.Closed && activeLeases.Count == 0)
        {
            return "planning_lineage_closed_no_active_workspace";
        }

        if (packet.LinkedTruth.TaskIds.Count == 0)
        {
            return "waiting_for_bound_task_truth_before_managed_workspace_issuance";
        }

        return activeLeases.Count == 0
            ? "task_bound_workspace_ready_to_issue"
            : "task_bound_workspace_active";
    }

    private static string ResolveRecommendedNextAction(
        FormalPlanningPacket? packet,
        IReadOnlyList<RuntimeManagedWorkspaceLeaseSurface> activeLeases,
        string modeDHardeningState)
    {
        if (packet is null)
        {
            return "Run `plan init [candidate-card-id]` before requesting a managed workspace lease.";
        }

        if (packet.FormalPlanningState == FormalPlanningState.Closed && activeLeases.Count == 0)
        {
            return "The current formal planning lineage is closed; no active managed workspace is required. Run `plan init [candidate-card-id]` only for a new bounded slice.";
        }

        if (packet.LinkedTruth.TaskIds.Count == 0)
        {
            return "Approve task truth on the current plan handle before requesting a managed workspace lease.";
        }

        if (activeLeases.Count == 0)
        {
            return $"Run `plan issue-workspace {packet.LinkedTruth.TaskIds[0]}` to issue the first task-bound managed workspace lease on the current plan handle.";
        }

        return string.Equals(modeDHardeningState, "active", StringComparison.Ordinal)
            ? $"Continue Mode D work inside '{activeLeases[0].WorkspacePath}', stay within declared writable paths, and return changes through review/writeback."
            : $"Repair Mode D hardening state '{modeDHardeningState}' before treating '{activeLeases[0].WorkspacePath}' as a scoped task workspace.";
    }

    private void ValidatePath(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private static bool IsLeaseBoundToTerminalTask(ManagedWorkspaceLease lease, Carves.Runtime.Domain.Tasks.TaskGraph graph)
    {
        return graph.Tasks.TryGetValue(lease.TaskId, out var task) && IsTerminal(task.Status);
    }
}

public sealed class ManagedWorkspaceSubmissionCandidate
{
    public string TaskId { get; init; } = string.Empty;

    public ManagedWorkspaceLease Lease { get; init; } = new();

    public IReadOnlyList<string> ChangedPaths { get; init; } = Array.Empty<string>();

    public ManagedWorkspacePathPolicyAssessment PathPolicy { get; init; } = new();
}
