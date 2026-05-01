using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class ExecutionHardeningSurfaceService
{
    private static readonly string[] GovernanceMustCallActions =
    [
        "create-card-draft",
        "approve-card",
        "create-taskgraph-draft",
        "approve-taskgraph-draft",
        "task run",
        "review-task",
        "approve-review",
        "sync-state",
        "audit sustainability",
        "compact-history",
    ];

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly ExecutionPacketCompilerService executionPacketCompilerService;
    private readonly PacketEnforcementService packetEnforcementService;
    private readonly IControlPlaneLockService controlPlaneLockService;

    public ExecutionHardeningSurfaceService(
        string repoRoot,
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        IRuntimeArtifactRepository artifactRepository,
        ExecutionPacketCompilerService executionPacketCompilerService,
        PacketEnforcementService packetEnforcementService,
        IControlPlaneLockService controlPlaneLockService)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.artifactRepository = artifactRepository;
        this.executionPacketCompilerService = executionPacketCompilerService;
        this.packetEnforcementService = packetEnforcementService;
        this.controlPlaneLockService = controlPlaneLockService;
    }

    public ExecutionHardeningSurfaceSnapshot Build(string taskId)
    {
        var task = taskGraphService.GetTask(taskId);
        var governance = BuildGovernanceBootstrap();
        var toolSurface = new CodexToolSurfaceService().Build();
        var packet = new ExecutionPacketSurfaceService(executionPacketCompilerService).Build(taskId);
        var packetEnforcement = new PacketEnforcementSurfaceService(packetEnforcementService).Build(taskId);
        var authoritativeTruth = new AuthoritativeTruthStoreSurfaceService(paths, controlPlaneLockService).Build();
        var reviewArtifact = artifactRepository.TryLoadPlannerReviewArtifact(taskId);
        var reviewStatus = reviewArtifact is null
            ? "not_reviewed"
            : $"{reviewArtifact.DecisionStatus.ToString().ToLowerInvariant()}->{reviewArtifact.ResultingStatus.ToString().ToLowerInvariant()}";

        var relevantTools = toolSurface.Tools
            .Where(tool => tool.ToolId is "get_task" or "get_execution_packet" or "submit_result" or "review_task" or "sync_state")
            .ToArray();
        var executionPacketFamily = authoritativeTruth.Families.FirstOrDefault(family => family.FamilyId == "execution_packets");
        var negativePaths = BuildNegativePaths(packet, packetEnforcement, executionPacketFamily);

        var summary = BuildSummary(governance, packet, packetEnforcement, authoritativeTruth, reviewStatus);

        return new ExecutionHardeningSurfaceSnapshot
        {
            TaskId = taskId,
            CardId = task.CardId ?? string.Empty,
            ScenarioId = taskId,
            TaskStatus = task.Status.ToString().ToLowerInvariant(),
            Summary = summary,
            Governance = governance,
            RelevantTools = relevantTools,
            ExecutionPacket = packet,
            PacketEnforcement = packetEnforcement,
            AuthoritativeTruth = authoritativeTruth,
            ReviewStatus = reviewStatus,
            NegativePaths = negativePaths,
            InspectCommands =
            [
                "inspect codex-tool-surface",
                $"inspect execution-packet {taskId}",
                $"inspect packet-enforcement {taskId}",
                "inspect authoritative-truth-store",
            ],
        };
    }

    private CodexGovernanceBootstrapSnapshot BuildGovernanceBootstrap()
    {
        var assets = new[]
        {
            BuildGovernanceAsset("agents", "AGENTS.md", "Repo must-call and execution boundary guidance."),
            BuildGovernanceAsset("config", ".codex/config.toml", "Repository-local Codex config and sandbox posture."),
            BuildGovernanceAsset("control_plane_rules", ".codex/rules/carves-control-plane.md", "Control-plane must-call rule set."),
            BuildGovernanceAsset("execution_boundary_rules", ".codex/rules/carves-execution-boundary.md", "Execution boundary and stop-condition rule set."),
            BuildGovernanceAsset("skill", ".codex/skills/carves-runtime/SKILL.md", "Repo-managed bootstrap skill for Codex workflows."),
        };

        var bootstrapReady = assets.All(asset => asset.Exists);
        var summary = bootstrapReady
            ? "Codex governance bootstrap assets are present and can anchor the end-to-end scenario."
            : "Codex governance bootstrap is incomplete; one or more required assets are missing.";

        return new CodexGovernanceBootstrapSnapshot
        {
            BootstrapReady = bootstrapReady,
            Summary = summary,
            Assets = assets,
            MustCallActions = GovernanceMustCallActions,
        };
    }

    private CodexGovernanceAssetStatus BuildGovernanceAsset(string assetId, string relativePath, string summary)
    {
        var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return new CodexGovernanceAssetStatus
        {
            AssetId = assetId,
            RelativePath = relativePath.Replace('\\', '/'),
            Exists = File.Exists(fullPath),
            Summary = summary,
        };
    }

    private static ExecutionHardeningNegativePath[] BuildNegativePaths(
        ExecutionPacketSurfaceSnapshot packet,
        PacketEnforcementSurfaceSnapshot packetEnforcement,
        AuthoritativeTruthFamilyBinding? executionPacketFamily)
    {
        var offPacketActive = packetEnforcement.Record.ReasonCodes.Any(code =>
            string.Equals(code, "off_packet_edit_detected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "truth_write_attempt_detected", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "planner_only_action_attempted", StringComparison.OrdinalIgnoreCase));

        var mirrorDriftActive = executionPacketFamily?.MirrorDriftDetected == true;
        var mirrorSyncContentionActive = string.Equals(
            executionPacketFamily?.MirrorSync.Outcome,
            "contention",
            StringComparison.OrdinalIgnoreCase);

        return
        [
            new ExecutionHardeningNegativePath
            {
                PathId = "off_packet_mutation",
                ExplicitVerdictSupported = true,
                CurrentStatus = offPacketActive ? packetEnforcement.Record.Verdict : "supported",
                Summary = "Packet enforcement exposes off-packet edits and truth-write attempts as explicit reject or quarantine verdicts.",
                EvidenceCommands =
                [
                    $"inspect packet-enforcement {packet.TaskId}",
                    $"api packet-enforcement {packet.TaskId}",
                ],
                ReasonCodes =
                [
                    "off_packet_edit_detected",
                    "truth_write_attempt_detected",
                    "planner_only_action_attempted",
                    "lifecycle_writeback_attempted",
                ],
            },
            new ExecutionHardeningNegativePath
            {
                PathId = "missing_validation",
                ExplicitVerdictSupported = true,
                CurrentStatus = packet.Packet.RequiredValidation.Count == 0 ? "not_required" : "supported",
                Summary = "Result validity and boundary gates reject missing admissible evidence or missing build/test artifacts before writeback.",
                EvidenceCommands =
                [
                    "inspect execution-contract-surface",
                    $"inspect execution-packet {packet.TaskId}",
                    $"task inspect {packet.TaskId}",
                ],
                ReasonCodes =
                [
                    "no_evidence",
                    "evidence_not_admissible",
                    "build_log_missing",
                    "test_log_missing",
                    "patch_artifact_missing",
                ],
            },
            new ExecutionHardeningNegativePath
            {
                PathId = "mirror_drift",
                ExplicitVerdictSupported = true,
                CurrentStatus = mirrorSyncContentionActive
                    ? "contention"
                    : mirrorDriftActive
                        ? executionPacketFamily!.MirrorState
                        : executionPacketFamily?.MirrorState ?? "unknown",
                Summary = "Authoritative truth now reports repo-mirror drift and bounded mirror-sync contention explicitly instead of allowing mirror state to silently win.",
                EvidenceCommands =
                [
                    "inspect authoritative-truth-store",
                    "api authoritative-truth-store",
                ],
                ReasonCodes =
                [
                    "contention",
                    "drifted",
                    "mirror_only",
                    "authoritative_only",
                ],
            },
        ];
    }

    private static string BuildSummary(
        CodexGovernanceBootstrapSnapshot governance,
        ExecutionPacketSurfaceSnapshot packet,
        PacketEnforcementSurfaceSnapshot packetEnforcement,
        AuthoritativeTruthStoreSurface authoritativeTruth,
        string reviewStatus)
    {
        var driftedFamilies = authoritativeTruth.Families.Where(family => family.MirrorDriftDetected).Select(family => family.FamilyId).ToArray();
        var driftSummary = driftedFamilies.Length == 0
            ? "no mirror drift detected"
            : $"mirror drift detected for {string.Join(", ", driftedFamilies)}";

        return $"Governance bootstrap {(governance.BootstrapReady ? "ready" : "incomplete")}; packet persisted={packet.Persisted}; packet enforcement verdict={packetEnforcement.Record.Verdict}; review={reviewStatus}; {driftSummary}.";
    }
}
