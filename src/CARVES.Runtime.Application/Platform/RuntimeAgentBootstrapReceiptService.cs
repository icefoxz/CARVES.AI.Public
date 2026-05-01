using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentBootstrapReceiptService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService? taskGraphService;

    public RuntimeAgentBootstrapReceiptService(string repoRoot, ControlPlanePaths paths, TaskGraphService? taskGraphService = null)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.taskGraphService = taskGraphService;
    }

    public RuntimeAgentBootstrapReceiptSurface Build(string? priorReceiptPath = null)
    {
        var policy = RuntimeAgentGovernanceSupport.LoadPolicy(repoRoot, paths);
        var packetSurface = new RuntimeAgentBootstrapPacketService(repoRoot, paths, taskGraphService).Build();
        var packet = packetSurface.Packet;
        var comparedReceiptPath = NormalizeComparedPath(priorReceiptPath);
        var invalidationReasons = new List<string>();
        var comparisonStatus = "not_provided";
        var resumeDecision = "cold_init_required";
        var resumeReason = "Prior receipt not provided; perform cold init or compare a prior governed receipt.";

        if (!string.IsNullOrWhiteSpace(priorReceiptPath))
        {
            var resolvedPath = ResolvePath(priorReceiptPath);
            if (!File.Exists(resolvedPath))
            {
                comparisonStatus = "missing";
                invalidationReasons.Add("prior receipt path does not exist");
                resumeReason = "Prior receipt path was provided but no readable receipt exists there.";
            }
            else
            {
                try
                {
                    var prior = JsonSerializer.Deserialize<RuntimeAgentBootstrapReceiptSurface>(File.ReadAllText(resolvedPath));
                    if (prior is null)
                    {
                        comparisonStatus = "parse_failed";
                        invalidationReasons.Add("prior receipt could not be parsed");
                        resumeReason = "Prior receipt could not be parsed; fall back to cold init.";
                    }
                    else
                    {
                        comparisonStatus = "compared";
                        invalidationReasons.AddRange(Compare(prior.Receipt, packet));
                        if (invalidationReasons.Count == 0)
                        {
                            resumeDecision = "warm_resume_eligible";
                            comparisonStatus = "matched";
                            resumeReason = "Prior governed receipt matches current bootstrap posture.";
                        }
                        else
                        {
                            comparisonStatus = "mismatched";
                            resumeReason = $"Warm resume invalidated: {invalidationReasons[0]}.";
                        }
                    }
                }
                catch (JsonException)
                {
                    comparisonStatus = "parse_failed";
                    invalidationReasons.Add("prior receipt is not valid runtime-agent-bootstrap-receipt json");
                    resumeReason = "Prior receipt is unreadable as a governed receipt; fall back to cold init.";
                }
            }
        }

        if (invalidationReasons.Count == 0 && resumeDecision == "cold_init_required")
        {
            invalidationReasons.Add("prior receipt not provided");
        }

        var workingContext = BuildWorkingContext(packet);
        var resumeGuidance = BuildResumeGuidance(resumeDecision, workingContext);

        var validationInspectCommands = new List<string>
        {
            "inspect runtime-agent-bootstrap-packet",
            "inspect runtime-agent-bootstrap-receipt [<receipt-json-path>]",
        };
        if (!string.Equals(packet.RepoPosture.CurrentTaskId, "N/A", StringComparison.Ordinal))
        {
            validationInspectCommands.Add($"inspect runtime-agent-task-overlay {packet.RepoPosture.CurrentTaskId}");
        }

        return new RuntimeAgentBootstrapReceiptSurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformAgentGovernanceKernelFile),
            Receipt = new AgentBootstrapReceipt
            {
                PolicyVersion = policy.PolicyVersion,
                PacketSurfaceId = policy.BootstrapPacketContract.SurfaceId,
                TaskOverlaySurfaceId = policy.TaskOverlayContract.SurfaceId,
                RepoRoot = repoRoot,
                CurrentTaskId = packet.RepoPosture.CurrentTaskId,
                CurrentCardMemoryRefs = packet.CurrentCardMemoryRefs,
                RepoPosture = packet.RepoPosture,
                HostSnapshot = packet.HostSnapshot,
                PostureBasis = packet.PostureBasis,
                VerifiedAt = DateTimeOffset.UtcNow,
                ResumeDecision = resumeDecision,
                ResumeReason = resumeReason,
                ComparisonStatus = comparisonStatus,
                ComparedReceiptPath = comparedReceiptPath,
                RequiredReceiptFields = policy.WarmResumeContract.RequiredReceiptFields,
                ValidationInspectCommands = validationInspectCommands.ToArray(),
                WarmResumeChecks = policy.WarmResumeContract.WarmResumeChecks,
                InvalidationReasons = invalidationReasons.ToArray(),
                ColdInitTriggers = policy.WarmResumeContract.ColdInitTriggers,
                HotPathContext = packet.HotPathContext,
                WorkingContext = workingContext,
                ResumeGuidance = resumeGuidance,
            },
        };
    }

    private AgentBootstrapWorkingContext BuildWorkingContext(AgentBootstrapPacket packet)
    {
        var unavailableChecks = new List<string>();
        var recentExecutions = BuildRecentExecutions(unavailableChecks);
        var recommendedNextCommands = packet.HotPathContext.BoundedNextCommands
            .Concat(packet.HotPathContext.TaskOverlayCommands)
            .Append("inspect runtime-agent-model-profile-routing")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var escalationTriggers = packet.HotPathContext.FullGovernanceReadTriggers
            .Concat(packet.HotPathContext.MarkdownReadPolicy.EscalationTriggers)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new AgentBootstrapWorkingContext
        {
            Summary = packet.HotPathContext.ActiveTasks.Length == 0 && recentExecutions.Length == 0
                ? "Short working context is available, but no active task or recent worker execution was found."
                : $"Short working context includes {packet.HotPathContext.ActiveTasks.Length} active task(s) and {recentExecutions.Length} recent worker execution summary item(s).",
            DefaultEntryMode = "receipt_working_context_then_task_overlay",
            GovernanceBoundary = "working_context_reduces_post_initialization_reads_only",
            ActiveTasks = packet.HotPathContext.ActiveTasks,
            RecentExecutions = recentExecutions,
            SafetyPosture = "Use task overlay safety_context, execution packet permissions, and packet-enforcement readback for task-specific write boundaries.",
            BackendPosture = "Use inspect runtime-agent-model-profile-routing for current provider/backend qualification.",
            RecommendedNextCommands = recommendedNextCommands,
            EscalationTriggers = escalationTriggers,
            UnavailableChecks = unavailableChecks.ToArray(),
        };
    }

    private AgentRecentExecutionSummary[] BuildRecentExecutions(ICollection<string> unavailableChecks)
    {
        if (taskGraphService is null)
        {
            unavailableChecks.Add("task_graph_service unavailable; recent worker executions not projected");
            return [];
        }

        try
        {
            return taskGraphService.Load()
                .ListTasks()
                .Where(static task => !string.IsNullOrWhiteSpace(task.LastWorkerRunId))
                .OrderByDescending(static task => task.UpdatedAt)
                .ThenBy(static task => task.TaskId, StringComparer.Ordinal)
                .Take(3)
                .Select(ToRecentExecutionSummary)
                .ToArray();
        }
        catch (IOException)
        {
            unavailableChecks.Add("task graph read failed; recent worker executions not projected");
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            unavailableChecks.Add("task graph read unauthorized; recent worker executions not projected");
            return [];
        }
        catch (InvalidOperationException)
        {
            unavailableChecks.Add("task graph read invalid; recent worker executions not projected");
            return [];
        }
    }

    private static AgentRecentExecutionSummary ToRecentExecutionSummary(TaskNode task)
    {
        return new AgentRecentExecutionSummary
        {
            TaskId = task.TaskId,
            RunId = task.LastWorkerRunId ?? "N/A",
            Backend = task.LastWorkerBackend ?? "N/A",
            Status = ToSnakeCase(task.Status),
            FailureKind = ToSnakeCase(task.LastWorkerFailureKind),
            Retryable = task.LastWorkerRetryable,
            Summary = task.LastWorkerSummary ?? "N/A",
            DetailRef = task.LastWorkerDetailRef ?? "N/A",
            UpdatedAt = task.UpdatedAt,
        };
    }

    private static AgentBootstrapResumeGuidance BuildResumeGuidance(string resumeDecision, AgentBootstrapWorkingContext workingContext)
    {
        var warmResumeEligible = string.Equals(resumeDecision, "warm_resume_eligible", StringComparison.Ordinal);
        return new AgentBootstrapResumeGuidance
        {
            Guidance = warmResumeEligible
                ? "Prior governed receipt matched current posture. After required initialization is already satisfied, prefer working_context and task overlay surfaces before broad Markdown rereads."
                : "Cold initialization is required or warm resume was not proven. Emit the initialization report and follow mandatory repo entry sources before relying on short-context projections.",
            MachineSurfaceFirst = warmResumeEligible,
            SkipActions = warmResumeEligible
                ?
                [
                    "broad .ai/memory/architecture reread when no escalation trigger applies",
                    "broad docs/runtime reread when no escalation trigger applies",
                    "manual reconstruction of active task context already present in working_context",
                ]
                : [],
            RequiredActions = warmResumeEligible
                ?
                [
                    "preserve CARVES.AI initialization report requirement for new sessions",
                    "use working_context.recommended_next_commands for bounded next reads",
                    "inspect runtime-agent-task-overlay for any task before editing",
                    "escalate to mandatory governance reads when an escalation trigger applies",
                ]
                :
                [
                    "emit CARVES.AI initialization report before real repo work",
                    "read required initial sources from markdown_read_policy.required_initial_sources",
                    "create or compare a governed bootstrap receipt before warm-resume claims",
                ],
            EscalationTriggers = workingContext.EscalationTriggers,
        };
    }

    private string[] Compare(AgentBootstrapReceipt prior, AgentBootstrapPacket currentPacket)
    {
        var mismatches = new List<string>();
        if (prior.PolicyVersion != RuntimeAgentGovernanceSupport.LoadPolicy(repoRoot, paths).PolicyVersion)
        {
            mismatches.Add("policy_version changed");
        }

        if (!string.Equals(prior.PacketSurfaceId, "runtime-agent-bootstrap-packet", StringComparison.Ordinal))
        {
            mismatches.Add("packet_surface_id is not runtime-agent-bootstrap-packet");
        }

        if (!string.Equals(prior.TaskOverlaySurfaceId, "runtime-agent-task-overlay", StringComparison.Ordinal))
        {
            mismatches.Add("task_overlay_surface_id is not runtime-agent-task-overlay");
        }

        if (!string.Equals(prior.RepoRoot, repoRoot, StringComparison.OrdinalIgnoreCase))
        {
            mismatches.Add("repo_root changed");
        }

        if (!string.Equals(prior.CurrentTaskId, currentPacket.RepoPosture.CurrentTaskId, StringComparison.Ordinal))
        {
            mismatches.Add("current_task_id changed");
        }

        if (!prior.CurrentCardMemoryRefs.SequenceEqual(currentPacket.CurrentCardMemoryRefs, StringComparer.Ordinal))
        {
            mismatches.Add("current_card_memory_refs changed");
        }

        if (!string.Equals(prior.PostureBasis, currentPacket.PostureBasis, StringComparison.Ordinal))
        {
            mismatches.Add("posture_basis changed");
        }

        if (!PostureEquals(prior.RepoPosture, currentPacket.RepoPosture))
        {
            mismatches.Add("repo_posture changed");
        }

        if (!HostSnapshotEquals(prior.HostSnapshot, currentPacket.HostSnapshot))
        {
            mismatches.Add("host_snapshot changed");
        }

        return mismatches.ToArray();
    }

    private static bool PostureEquals(AgentBootstrapRepoPosture left, AgentBootstrapRepoPosture right)
    {
        return string.Equals(left.SessionStatus, right.SessionStatus, StringComparison.Ordinal)
               && string.Equals(left.LoopMode, right.LoopMode, StringComparison.Ordinal)
               && string.Equals(left.CurrentActionability, right.CurrentActionability, StringComparison.Ordinal)
               && string.Equals(left.PlannerState, right.PlannerState, StringComparison.Ordinal)
               && string.Equals(left.CurrentTaskId, right.CurrentTaskId, StringComparison.Ordinal);
    }

    private static bool HostSnapshotEquals(AgentBootstrapHostSnapshot left, AgentBootstrapHostSnapshot right)
    {
        return string.Equals(left.State, right.State, StringComparison.Ordinal)
               && string.Equals(left.SessionStatus, right.SessionStatus, StringComparison.Ordinal)
               && string.Equals(left.HostControlState, right.HostControlState, StringComparison.Ordinal);
    }

    private string NormalizeComparedPath(string? priorReceiptPath)
    {
        if (string.IsNullOrWhiteSpace(priorReceiptPath))
        {
            return "N/A";
        }

        var resolved = ResolvePath(priorReceiptPath);
        return resolved.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase)
            ? RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, resolved)
            : resolved;
    }

    private string ResolvePath(string candidate)
    {
        return Path.IsPathRooted(candidate)
            ? Path.GetFullPath(candidate)
            : Path.GetFullPath(Path.Combine(repoRoot, candidate));
    }

    private static string ToSnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }
}
