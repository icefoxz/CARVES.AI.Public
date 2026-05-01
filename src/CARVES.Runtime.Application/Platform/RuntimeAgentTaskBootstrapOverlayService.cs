using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Safety;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentTaskBootstrapOverlayService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly ExecutionPacketCompilerService executionPacketCompilerService;
    private readonly IGitClient? gitClient;

    public RuntimeAgentTaskBootstrapOverlayService(
        string repoRoot,
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        ExecutionPacketCompilerService executionPacketCompilerService,
        IGitClient? gitClient = null)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.executionPacketCompilerService = executionPacketCompilerService;
        this.gitClient = gitClient;
    }

    public RuntimeAgentTaskBootstrapOverlaySurface Build(string taskId)
    {
        var policy = RuntimeAgentGovernanceSupport.LoadPolicy(repoRoot, paths);
        var task = taskGraphService.GetTask(taskId);
        var packet = executionPacketCompilerService.BuildSnapshot(taskId).Packet;
        var scopeFiles = packet.Context.RelevantFiles.Count == 0 ? packet.Scope.ToArray() : packet.Context.RelevantFiles.ToArray();
        var protectedRoots = packet.Permissions.ReadOnlyRoots
            .Concat(packet.Permissions.TruthRoots)
            .Concat(packet.Permissions.RepoMirrorRoots)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var validationCommands = task.Validation.Commands
            .Select(command => string.Join(' ', command))
            .Where(static command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var markdownReadGuidance = BuildMarkdownReadGuidance(task, packet);
        var acceptanceContract = BuildAcceptanceContractSummary(task.AcceptanceContract, packet.AcceptanceContract);
        var scopeFileContexts = BuildScopeFileContexts(scopeFiles, packet);
        var safetyContext = BuildSafetyContext(packet, protectedRoots);

        return new RuntimeAgentTaskBootstrapOverlaySurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformAgentGovernanceKernelFile),
            Overlay = new AgentTaskBootstrapOverlay
            {
                OverlayId = $"{policy.TaskOverlayContract.SurfaceId}:{taskId}",
                TaskId = task.TaskId,
                CardId = task.CardId ?? "CARD-UNKNOWN",
                Title = task.Title,
                TaskStatus = task.Status.ToString().ToLowerInvariant(),
                ScopeFiles = scopeFiles,
                ProtectedRoots = protectedRoots,
                AllowedActions = packet.WorkerAllowedActions.ToArray(),
                RequiredVerification = packet.RequiredValidation.ToArray(),
                StableEvidenceSurfaces = packet.StableEvidenceSurfaces.ToArray(),
                StopConditions = packet.StopConditions.ToArray(),
                MemoryBundleRefs = packet.Context.MemoryBundleRefs.ToArray(),
                Acceptance = task.Acceptance.ToArray(),
                Constraints = task.Constraints.ToArray(),
                AcceptanceContract = acceptanceContract,
                ScopeFileContexts = scopeFileContexts,
                SafetyContext = safetyContext,
                EditableRoots = packet.Permissions.EditableRoots.ToArray(),
                ReadOnlyRoots = packet.Permissions.ReadOnlyRoots.ToArray(),
                TruthRoots = packet.Permissions.TruthRoots.ToArray(),
                RepoMirrorRoots = packet.Permissions.RepoMirrorRoots.ToArray(),
                ValidationContext = new AgentTaskBootstrapValidationContext
                {
                    Commands = validationCommands,
                    Checks = task.Validation.Checks.ToArray(),
                    ExpectedEvidence = task.Validation.ExpectedEvidence.ToArray(),
                    RequiredVerification = packet.RequiredValidation.ToArray(),
                },
                LastWorker = new AgentTaskBootstrapWorkerSummary
                {
                    RunId = task.LastWorkerRunId ?? "N/A",
                    Backend = task.LastWorkerBackend ?? "N/A",
                    FailureKind = ToSnakeCase(task.LastWorkerFailureKind),
                    Retryable = task.LastWorkerRetryable,
                    Summary = task.LastWorkerSummary ?? "N/A",
                    DetailRef = task.LastWorkerDetailRef ?? "N/A",
                    ProviderDetailRef = task.LastProviderDetailRef ?? "N/A",
                    RecoveryAction = ToSnakeCase(task.LastRecoveryAction),
                    RecoveryReason = task.LastRecoveryReason ?? "N/A",
                },
                PlannerReview = new AgentTaskBootstrapPlannerReviewSummary
                {
                    Verdict = ToSnakeCase(task.PlannerReview.Verdict),
                    DecisionStatus = ToSnakeCase(task.PlannerReview.DecisionStatus),
                    Reason = task.PlannerReview.Reason,
                    AcceptanceMet = task.PlannerReview.AcceptanceMet,
                    BoundaryPreserved = task.PlannerReview.BoundaryPreserved,
                    ScopeDriftDetected = task.PlannerReview.ScopeDriftDetected,
                    FollowUpSuggestions = task.PlannerReview.FollowUpSuggestions.ToArray(),
                },
                MarkdownReadGuidance = markdownReadGuidance,
            },
        };
    }

    private AgentTaskBootstrapAcceptanceContractSummary BuildAcceptanceContractSummary(AcceptanceContract? taskContract, AcceptanceContract? packetContract)
    {
        var contract = taskContract ?? packetContract;
        if (contract is null)
        {
            return new AgentTaskBootstrapAcceptanceContractSummary
            {
                BindingState = "missing",
                Status = "missing",
            };
        }

        return new AgentTaskBootstrapAcceptanceContractSummary
        {
            BindingState = taskContract is not null ? "task_truth_bound" : "packet_bound",
            ContractId = contract.ContractId,
            Status = ToSnakeCase(contract.Status),
            Goal = string.IsNullOrWhiteSpace(contract.Intent.Goal) ? "N/A" : contract.Intent.Goal,
            BusinessValue = string.IsNullOrWhiteSpace(contract.Intent.BusinessValue) ? "N/A" : contract.Intent.BusinessValue,
            EvidenceRequired = contract.EvidenceRequired.Select(FormatEvidenceRequirement).ToArray(),
            MustNot = contract.Constraints.MustNot.ToArray(),
            ArchitectureConstraints = contract.Constraints.Architecture.ToArray(),
            HumanReviewRequired = contract.HumanReview.Required,
            ProvisionalAllowed = contract.HumanReview.ProvisionalAllowed,
        };
    }

    private AgentTaskScopeFileContext[] BuildScopeFileContexts(IReadOnlyList<string> scopeFiles, ExecutionPacket packet)
    {
        var uncommittedPaths = gitClient is null ? [] : GetGitPaths(gitClient.GetUncommittedPaths);
        var untrackedPaths = gitClient is null ? [] : GetGitPaths(gitClient.GetUntrackedPaths);
        var gitStatusAvailable = gitClient is not null && IsGitRepository();

        return scopeFiles
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new AgentTaskScopeFileContext
            {
                Path = NormalizePath(path),
                Exists = PathExists(path),
                GitStatus = ResolveGitStatus(path, gitStatusAvailable, uncommittedPaths, untrackedPaths),
                BoundaryClass = ResolveBoundaryClass(path, packet.Permissions),
                Source = packet.Context.RelevantFiles.Contains(path, StringComparer.OrdinalIgnoreCase)
                    ? "execution_packet_relevant_file"
                    : "execution_packet_scope",
            })
            .ToArray();
    }

    private static AgentTaskSafetyBoundaryContext BuildSafetyContext(ExecutionPacket packet, IReadOnlyList<string> protectedRoots)
    {
        var layers = SafetyLayerSemantics.WorkerExecutionLayers
            .Select(static layer => new AgentTaskSafetyLayerContext
            {
                LayerId = layer.LayerId,
                Phase = layer.Phase,
                Authority = layer.Authority,
                EnforcementTiming = layer.EnforcementTiming,
                Enforces = layer.Enforces.ToArray(),
                NonClaims = layer.NonClaims.ToArray(),
            })
            .ToArray();

        return new AgentTaskSafetyBoundaryContext
        {
            Summary = $"editable_roots={packet.Permissions.EditableRoots.Count}; read_only_roots={packet.Permissions.ReadOnlyRoots.Count}; truth_roots={packet.Permissions.TruthRoots.Count}; max_files_changed={packet.Budgets.MaxFilesChanged}; max_lines_changed={packet.Budgets.MaxLinesChanged}",
            LayerSummary = SafetyLayerSemantics.Summary,
            Layers = layers,
            NonClaims = layers
                .SelectMany(static layer => layer.NonClaims)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            EditableRoots = packet.Permissions.EditableRoots.ToArray(),
            ReadOnlyRoots = packet.Permissions.ReadOnlyRoots.ToArray(),
            TruthRoots = packet.Permissions.TruthRoots.ToArray(),
            RepoMirrorRoots = packet.Permissions.RepoMirrorRoots.ToArray(),
            ProtectedRoots = protectedRoots.ToArray(),
            MaxFilesChanged = packet.Budgets.MaxFilesChanged,
            MaxLinesChanged = packet.Budgets.MaxLinesChanged,
            MaxShellCommands = packet.Budgets.MaxShellCommands,
            RequiredValidation = packet.RequiredValidation.ToArray(),
            WorkerAllowedActions = packet.WorkerAllowedActions.ToArray(),
            PlannerOnlyActions = packet.PlannerOnlyActions.ToArray(),
            StopConditions = packet.StopConditions.ToArray(),
        };
    }

    private static AgentTaskMarkdownReadGuidance BuildMarkdownReadGuidance(Carves.Runtime.Domain.Tasks.TaskNode task, Carves.Runtime.Domain.Execution.ExecutionPacket packet)
    {
        var scopedMarkdownRefs = packet.Context.MemoryBundleRefs
            .Concat(task.Scope.Where(IsMarkdownRef))
            .Concat(packet.Scope.Where(IsMarkdownRef))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requiredBeforeEditRefs = packet.Permissions.EditableRoots
            .Where(IsMarkdownRef)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var escalationTriggers = new List<string>
        {
            "task scope includes AGENTS.md or docs/guides governance sources",
            "acceptance or constraints require cleanup, commit hygiene, repository simplification, mixed diff judgment, or sibling boundary routing",
            "task overlay or execution packet readback is stale, missing, or outside current scope",
        };
        if (packet.StopConditions.Any(static condition => string.Equals(condition, "requires_new_card_or_taskgraph", StringComparison.Ordinal)))
        {
            escalationTriggers.Add("execution packet stop condition requires_new_card_or_taskgraph is relevant");
        }

        return new AgentTaskMarkdownReadGuidance
        {
            Summary = scopedMarkdownRefs.Length == 0
                ? "Task overlay has no task-scoped Markdown refs; prefer machine surfaces unless escalation triggers apply."
                : $"Task overlay names {scopedMarkdownRefs.Length} task-scoped Markdown ref(s); read those refs only when task context needs their detail.",
            DefaultReadMode = "task_overlay_first_after_initialization",
            GovernanceBoundary = "task_refs_are_targeted_reads_not_new_truth",
            TaskScopedMarkdownRefs = scopedMarkdownRefs,
            RequiredBeforeEditRefs = requiredBeforeEditRefs,
            ReplacementSurfaces =
            [
                $"inspect runtime-agent-task-overlay {task.TaskId}",
                $"api runtime-agent-task-overlay {task.TaskId}",
                $"inspect execution-packet {task.TaskId}",
                $"api execution-packet {task.TaskId}",
                $"inspect packet-enforcement {task.TaskId}",
            ],
            EscalationTriggers = escalationTriggers.Distinct(StringComparer.Ordinal).ToArray(),
        };
    }

    private static bool IsMarkdownRef(string value)
    {
        return value.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith(".ai/memory/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("docs/guides/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("docs/runtime/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "AGENTS.md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "README.md", StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> GetGitPaths(Func<string, IReadOnlyList<string>>? load)
    {
        if (load is null || !IsGitRepository())
        {
            return [];
        }

        try
        {
            return load(repoRoot)
                .Select(NormalizePath)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    private bool IsGitRepository()
    {
        try
        {
            return gitClient?.IsRepository(repoRoot) == true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private bool PathExists(string path)
    {
        if (IsVirtualPath(path))
        {
            return false;
        }

        var resolved = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(repoRoot, path));
        return File.Exists(resolved) || Directory.Exists(resolved);
    }

    private static string ResolveGitStatus(
        string path,
        bool gitStatusAvailable,
        IReadOnlyList<string> uncommittedPaths,
        IReadOnlyList<string> untrackedPaths)
    {
        if (!gitStatusAvailable)
        {
            return "not_checked";
        }

        var normalized = NormalizePath(path);
        if (untrackedPaths.Any(candidate => PathMatchesScope(candidate, normalized)))
        {
            return "untracked";
        }

        if (uncommittedPaths.Any(candidate => PathMatchesScope(candidate, normalized)))
        {
            return "modified";
        }

        return "clean";
    }

    private static string ResolveBoundaryClass(string path, ExecutionPacketPermissions permissions)
    {
        var normalized = NormalizePath(path);
        if (permissions.TruthRoots.Any(root => PathMatchesScope(normalized, NormalizePath(root))))
        {
            return "truth_root";
        }

        if (permissions.RepoMirrorRoots.Any(root => PathMatchesScope(normalized, NormalizePath(root))))
        {
            return "repo_mirror_root";
        }

        if (permissions.ReadOnlyRoots.Any(root => PathMatchesScope(normalized, NormalizePath(root))))
        {
            return "read_only_root";
        }

        if (permissions.EditableRoots.Any(root => PathMatchesScope(normalized, NormalizePath(root))))
        {
            return "editable_root";
        }

        return "declared_scope";
    }

    private static bool PathMatchesScope(string path, string scope)
    {
        var normalizedPath = NormalizePath(path).TrimEnd('/');
        var normalizedScope = NormalizePath(scope).TrimEnd('/');
        return string.Equals(normalizedPath, normalizedScope, StringComparison.OrdinalIgnoreCase)
               || normalizedPath.StartsWith(normalizedScope + "/", StringComparison.OrdinalIgnoreCase)
               || normalizedScope.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVirtualPath(string path)
    {
        return path.StartsWith("carves://", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path
            .Trim()
            .Trim('`')
            .Replace('\\', '/');
    }

    private static string FormatEvidenceRequirement(AcceptanceContractEvidenceRequirement requirement)
    {
        return string.IsNullOrWhiteSpace(requirement.Description)
            ? requirement.Type
            : $"{requirement.Type}: {requirement.Description}";
    }

    private static string ToSnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }
}
