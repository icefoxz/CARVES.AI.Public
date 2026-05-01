using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class PacketEnforcementService
{
    private (string PacketPath, string ResultPath, bool PacketPersisted, ExecutionPacket? Packet, ResultEnvelope? ResolvedEnvelope, WorkerExecutionArtifact? ResolvedWorkerArtifact)
        LoadEvaluationInputs(string taskId, ResultEnvelope? envelope, WorkerExecutionArtifact? workerArtifact)
    {
        var packetPath = GetPacketPath(taskId);
        var authoritativePacketPath = authoritativeTruthStoreService.GetExecutionPacketPath(taskId);
        var packetPersisted = File.Exists(authoritativePacketPath) || File.Exists(packetPath);
        var packetPayload = authoritativeTruthStoreService.ReadAuthoritativeFirst(authoritativePacketPath, packetPath);
        var packet = packetPersisted && !string.IsNullOrWhiteSpace(packetPayload)
            ? JsonSerializer.Deserialize<ExecutionPacket>(packetPayload, JsonOptions)
            : null;
        var resultPath = GetResultPath(taskId);
        var resolvedEnvelope = envelope ?? TryReadResultEnvelope(resultPath);
        var resolvedWorkerArtifact = workerArtifact ?? artifactRepository.TryLoadWorkerExecutionArtifact(taskId);
        return (packetPath, resultPath, packetPersisted, packet, resolvedEnvelope, resolvedWorkerArtifact);
    }

    private static PacketEnforcementRecord BuildMissingPacketRecord(
        TaskNode task,
        string taskId,
        string packetPath,
        string resultPath,
        ResultEnvelope? resolvedEnvelope,
        WorkerExecutionArtifact? resolvedWorkerArtifact)
    {
        var changedFileProvenance = BuildChangedFileProvenance(resolvedEnvelope, resolvedWorkerArtifact);
        return new PacketEnforcementRecord
        {
            TaskId = taskId,
            CardId = task.CardId ?? string.Empty,
            PacketPresent = false,
            PacketPersisted = false,
            PacketContractValid = false,
            ResultPresent = resolvedEnvelope is not null,
            WorkerArtifactPresent = resolvedWorkerArtifact is not null,
            Verdict = "not_applicable",
            ReasonCodes = ["packet_missing"],
            EvidencePaths = BuildEvidencePaths(packetPath, resultPath, resolvedWorkerArtifact),
            ResultEnvelopeChangedFiles = changedFileProvenance.ResultEnvelopeFiles,
            WorkerReportedChangedFiles = changedFileProvenance.WorkerReportedFiles,
            WorkerObservedChangedFiles = changedFileProvenance.WorkerObservedFiles,
            EvidenceChangedFiles = changedFileProvenance.EvidenceFiles,
            ChangedFiles = changedFileProvenance.EffectiveFiles,
            Summary = "Packet enforcement is not yet applicable because no persisted execution packet exists for this task.",
        };
    }

    private static (ChangedFileProvenance ChangedFileProvenance, IReadOnlyList<string> ContractIssues, bool PacketContractValid, string RequestedAction, string RequestedActionClass, bool PlannerOnlyActionAttempted, bool LifecycleWritebackAttempted, string[] OffPacketFiles, string[] TruthWriteFiles)
        EvaluatePacketScope(
            ExecutionPacket packet,
            ResultEnvelope? resolvedEnvelope,
            WorkerExecutionArtifact? resolvedWorkerArtifact)
    {
        var changedFileProvenance = BuildChangedFileProvenance(resolvedEnvelope, resolvedWorkerArtifact);
        var contractIssues = ValidatePacketContract(packet);
        var packetContractValid = contractIssues.Count == 0;
        var requestedAction = ResolveRequestedAction(resolvedEnvelope?.Next?.Suggested, packet);
        var requestedActionClass = ResolveRequestedActionClass(requestedAction, packet);
        var plannerOnlyActionAttempted = string.Equals(requestedActionClass, "planner_only", StringComparison.Ordinal);
        var lifecycleWritebackAttempted = plannerOnlyActionAttempted
            && LifecycleWritebackActions.Any(action => ActionMatches(requestedAction, action));
        var offPacketFiles = changedFileProvenance.EffectiveFiles
            .Where(file => !IsUnderRoots(file, packet.Permissions.EditableRoots))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var truthWriteFiles = changedFileProvenance.EffectiveFiles
            .Where(file => IsTruthWritePath(file, packet.Permissions.RepoMirrorRoots))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return (
            changedFileProvenance,
            contractIssues,
            packetContractValid,
            requestedAction,
            requestedActionClass,
            plannerOnlyActionAttempted,
            lifecycleWritebackAttempted,
            offPacketFiles,
            truthWriteFiles);
    }

    private static (string Verdict, List<string> ReasonCodes, string Summary) DetermineVerdict(
        bool packetContractValid,
        IReadOnlyList<string> contractIssues,
        ResultEnvelope? resolvedEnvelope,
        WorkerExecutionArtifact? resolvedWorkerArtifact,
        bool plannerOnlyActionAttempted,
        bool lifecycleWritebackAttempted,
        string requestedAction,
        string[] truthWriteFiles,
        string[] offPacketFiles)
    {
        var reasonCodes = new List<string>();

        if (!packetContractValid)
        {
            reasonCodes.AddRange(contractIssues);
            return ("reject", reasonCodes, "Packet enforcement rejected writeback because the persisted execution packet contract is malformed.");
        }

        if (resolvedEnvelope is null && resolvedWorkerArtifact is null)
        {
            reasonCodes.Add("pending_execution");
            return ("pending_execution", reasonCodes, "Packet enforcement is ready but no result envelope or worker execution artifact exists yet.");
        }

        if (plannerOnlyActionAttempted || lifecycleWritebackAttempted)
        {
            reasonCodes.Add("planner_only_action_attempted");
            if (lifecycleWritebackAttempted)
            {
                reasonCodes.Add("lifecycle_writeback_attempted");
            }

            return ("reject", reasonCodes, $"Packet enforcement rejected worker follow-up '{requestedAction}' because lifecycle truth remains planner-owned.");
        }

        if (truthWriteFiles.Length > 0 || offPacketFiles.Length > 0)
        {
            if (offPacketFiles.Length > 0)
            {
                reasonCodes.Add("off_packet_edit_detected");
            }

            if (truthWriteFiles.Length > 0)
            {
                reasonCodes.Add("truth_write_attempt_detected");
            }

            return ("quarantine", reasonCodes, "Packet enforcement quarantined the result because changed files exceeded packet scope or attempted truth-root writes.");
        }

        reasonCodes.Add("packet_scope_respected");
        return ("allow", reasonCodes, "Packet enforcement allowed the result because the worker remained inside packet scope and terminated at submit_result.");
    }

    private static PacketEnforcementRecord BuildPacketRecord(
        TaskNode task,
        string taskId,
        ExecutionPacket packet,
        bool packetPersisted,
        bool packetContractValid,
        ResultEnvelope? resolvedEnvelope,
        WorkerExecutionArtifact? resolvedWorkerArtifact,
        string requestedAction,
        string requestedActionClass,
        bool plannerOnlyActionAttempted,
        bool lifecycleWritebackAttempted,
        ChangedFileProvenance changedFileProvenance,
        string[] offPacketFiles,
        string[] truthWriteFiles,
        string verdict,
        List<string> reasonCodes,
        string packetPath,
        string resultPath,
        string summary)
    {
        return new PacketEnforcementRecord
        {
            TaskId = taskId,
            CardId = task.CardId ?? string.Empty,
            PacketId = packet.PacketId,
            PlannerIntent = JsonNamingPolicy.SnakeCaseLower.ConvertName(packet.PlannerIntent.ToString()),
            PacketPresent = true,
            PacketPersisted = packetPersisted,
            PacketContractValid = packetContractValid,
            ResultPresent = resolvedEnvelope is not null,
            WorkerArtifactPresent = resolvedWorkerArtifact is not null,
            SubmitResultAllowed = packet.WorkerAllowedActions.Any(action => ActionMatches(action, "carves.submit_result")),
            RequestedAction = requestedAction,
            RequestedActionClass = requestedActionClass,
            PlannerOnlyActionAttempted = plannerOnlyActionAttempted,
            LifecycleWritebackAttempted = lifecycleWritebackAttempted,
            WorkerAllowedActions = packet.WorkerAllowedActions,
            PlannerOnlyActions = packet.PlannerOnlyActions,
            EditableRoots = packet.Permissions.EditableRoots,
            RepoMirrorRoots = packet.Permissions.RepoMirrorRoots,
            ResultEnvelopeChangedFiles = changedFileProvenance.ResultEnvelopeFiles,
            WorkerReportedChangedFiles = changedFileProvenance.WorkerReportedFiles,
            WorkerObservedChangedFiles = changedFileProvenance.WorkerObservedFiles,
            EvidenceChangedFiles = changedFileProvenance.EvidenceFiles,
            ChangedFiles = changedFileProvenance.EffectiveFiles,
            OffPacketFiles = offPacketFiles,
            TruthWriteFiles = truthWriteFiles,
            Verdict = verdict,
            ReasonCodes = reasonCodes,
            EvidencePaths = BuildEvidencePaths(packetPath, resultPath, resolvedWorkerArtifact),
            Summary = summary,
        };
    }
}
