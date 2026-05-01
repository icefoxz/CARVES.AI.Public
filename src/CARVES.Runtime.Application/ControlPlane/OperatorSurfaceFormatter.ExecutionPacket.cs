using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult ExecutionPacket(ExecutionPacketSurfaceSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "Execution packet",
            $"Task: {snapshot.TaskId}",
            $"Card: {snapshot.CardId}",
            $"Planner intent: {snapshot.PlannerIntent}",
            $"Packet id: {snapshot.Packet.PacketId}",
            $"Packet path: {snapshot.PacketPath}",
            $"Persisted: {snapshot.Persisted}",
            $"Recovery authority: {snapshot.RecoveryAuthority}",
            $"Writeback authority: {snapshot.WritebackAuthority}",
            $"Summary: {snapshot.Summary}",
            $"Context order: {(snapshot.Packet.Context.AssemblyOrder.Count == 0 ? "(none)" : string.Join(" -> ", snapshot.Packet.Context.AssemblyOrder))}",
        };

        lines.Add($"Memory refs: {(snapshot.Packet.Context.MemoryBundleRefs.Count == 0 ? "(none)" : string.Join(" | ", snapshot.Packet.Context.MemoryBundleRefs))}");
        lines.Add($"Codegraph queries: {(snapshot.Packet.Context.CodegraphQueries.Count == 0 ? "(none)" : string.Join(" | ", snapshot.Packet.Context.CodegraphQueries))}");
        lines.Add($"Relevant files: {(snapshot.Packet.Context.RelevantFiles.Count == 0 ? "(none)" : string.Join(" | ", snapshot.Packet.Context.RelevantFiles))}");
        lines.Add($"Context pack ref: {snapshot.Packet.Context.ContextPackRef ?? "(none)"}");
        lines.Add($"Context compaction: strategy={snapshot.Packet.Context.Compaction.Strategy}; candidates={snapshot.Packet.Context.Compaction.CandidateFileCount}; relevant={snapshot.Packet.Context.Compaction.RelevantFileCount}; windowed={snapshot.Packet.Context.Compaction.WindowedReadCount}; full={snapshot.Packet.Context.Compaction.FullReadCount}; omitted={snapshot.Packet.Context.Compaction.OmittedFileCount}");
        lines.Add($"Windowed reads: {(snapshot.Packet.Context.WindowedReads.Count == 0 ? "(none)" : string.Join(" | ", snapshot.Packet.Context.WindowedReads.Select(item => $"{item.Path} lines {item.StartLine}-{item.EndLine}/{item.TotalLines} [{item.Reason}]")))}");
        lines.Add($"Patch budget: <= {snapshot.Packet.Budgets.MaxFilesChanged} files | <= {snapshot.Packet.Budgets.MaxLinesChanged} lines | <= {snapshot.Packet.Budgets.MaxShellCommands} shell commands");
        lines.Add($"Worker allowed actions: {(snapshot.Packet.WorkerAllowedActions.Count == 0 ? "(none)" : string.Join(" | ", snapshot.Packet.WorkerAllowedActions))}");
        lines.Add($"Worker execution packet: {(string.IsNullOrWhiteSpace(snapshot.Packet.WorkerExecutionPacket.PacketId) ? "(none)" : snapshot.Packet.WorkerExecutionPacket.PacketId)}");
        lines.Add($"Worker result channel: {(string.IsNullOrWhiteSpace(snapshot.Packet.WorkerExecutionPacket.ResultSubmission.CandidateResultChannel) ? "(none)" : snapshot.Packet.WorkerExecutionPacket.ResultSubmission.CandidateResultChannel)}");
        lines.Add($"Worker packet contract matrix: {(snapshot.Packet.WorkerExecutionPacket.RequiredContractMatrix.Count == 0 ? "(none)" : string.Join(" | ", snapshot.Packet.WorkerExecutionPacket.RequiredContractMatrix))}");
        lines.Add($"Worker packet truth authority: lifecycle={snapshot.Packet.WorkerExecutionPacket.GrantsLifecycleTruthAuthority}; write={snapshot.Packet.WorkerExecutionPacket.GrantsTruthWriteAuthority}; createsQueue={snapshot.Packet.WorkerExecutionPacket.CreatesTaskQueue}");
        lines.Add($"Planner-only actions: {(snapshot.Packet.PlannerOnlyActions.Count == 0 ? "(none)" : string.Join(" | ", snapshot.Packet.PlannerOnlyActions))}");
        lines.Add($"Stop conditions: {(snapshot.Packet.StopConditions.Count == 0 ? "(none)" : string.Join(" | ", snapshot.Packet.StopConditions))}");
        lines.Add($"Required validation: {(snapshot.Packet.RequiredValidation.Count == 0 ? "(none)" : string.Join(" | ", snapshot.Packet.RequiredValidation))}");
        lines.Add($"Stable evidence surfaces: {(snapshot.Packet.StableEvidenceSurfaces.Count == 0 ? "(none)" : string.Join(" | ", snapshot.Packet.StableEvidenceSurfaces))}");

        return new OperatorCommandResult(0, lines);
    }
}
