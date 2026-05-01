using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class PacketEnforcementService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly IReadOnlyList<string> LocalEphemeralActions =
    [
        "read",
        "edit",
        "build",
        "test",
    ];

    private static readonly IReadOnlyList<string> LifecycleWritebackActions =
    [
        "review_task",
        "sync_state",
        "approve_review",
        "reject_review",
        "approve_taskgraph_draft",
        "approve_card",
    ];

    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly AuthoritativeTruthStoreService authoritativeTruthStoreService;

    public PacketEnforcementService(
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        IRuntimeArtifactRepository artifactRepository)
    {
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.artifactRepository = artifactRepository;
        authoritativeTruthStoreService = new AuthoritativeTruthStoreService(paths);
    }

    public ControlPlanePaths Paths => paths;

    public PacketEnforcementRecord Evaluate(string taskId, ResultEnvelope? envelope = null, WorkerExecutionArtifact? workerArtifact = null)
    {
        var task = taskGraphService.GetTask(taskId);
        var (packetPath, resultPath, packetPersisted, packet, resolvedEnvelope, resolvedWorkerArtifact) =
            LoadEvaluationInputs(taskId, envelope, workerArtifact);

        if (packet is null)
        {
            return BuildMissingPacketRecord(task, taskId, packetPath, resultPath, resolvedEnvelope, resolvedWorkerArtifact);
        }

        var (changedFileProvenance, contractIssues, packetContractValid, requestedAction, requestedActionClass, plannerOnlyActionAttempted, lifecycleWritebackAttempted, offPacketFiles, truthWriteFiles) =
            EvaluatePacketScope(packet, resolvedEnvelope, resolvedWorkerArtifact);
        var (verdict, reasonCodes, summary) = DetermineVerdict(
            packetContractValid,
            contractIssues,
            resolvedEnvelope,
            resolvedWorkerArtifact,
            plannerOnlyActionAttempted,
            lifecycleWritebackAttempted,
            requestedAction,
            truthWriteFiles,
            offPacketFiles);

        return BuildPacketRecord(
            task,
            taskId,
            packet,
            packetPersisted,
            packetContractValid,
            resolvedEnvelope,
            resolvedWorkerArtifact,
            requestedAction,
            requestedActionClass,
            plannerOnlyActionAttempted,
            lifecycleWritebackAttempted,
            changedFileProvenance,
            offPacketFiles,
            truthWriteFiles,
            verdict,
            reasonCodes,
            packetPath,
            resultPath,
            summary);
    }
}
