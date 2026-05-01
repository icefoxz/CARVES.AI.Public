using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult PacketEnforcement(PacketEnforcementSurfaceSnapshot snapshot)
    {
        var record = snapshot.Record;
        var lines = new List<string>
        {
            "Packet enforcement",
            $"Task: {snapshot.TaskId}",
            $"Card: {snapshot.CardId}",
            $"Packet path: {snapshot.PacketPath}",
            $"Enforcement path: {snapshot.EnforcementPath}",
            $"Persisted: {snapshot.Persisted}",
            $"Verdict: {record.Verdict}",
            $"Packet present: {record.PacketPresent}",
            $"Packet contract valid: {record.PacketContractValid}",
            $"Requested action: {record.RequestedAction}",
            $"Requested action class: {record.RequestedActionClass}",
            $"Planner-only action attempted: {record.PlannerOnlyActionAttempted}",
            $"Lifecycle writeback attempted: {record.LifecycleWritebackAttempted}",
            $"Summary: {snapshot.Summary}",
            $"Changed files: {(record.ChangedFiles.Count == 0 ? "(none)" : string.Join(" | ", record.ChangedFiles))}",
            $"Result-envelope changed files: {(record.ResultEnvelopeChangedFiles.Count == 0 ? "(none)" : string.Join(" | ", record.ResultEnvelopeChangedFiles))}",
            $"Worker-reported changed files: {(record.WorkerReportedChangedFiles.Count == 0 ? "(none)" : string.Join(" | ", record.WorkerReportedChangedFiles))}",
            $"Worker-observed changed files: {(record.WorkerObservedChangedFiles.Count == 0 ? "(none)" : string.Join(" | ", record.WorkerObservedChangedFiles))}",
            $"Evidence changed files: {(record.EvidenceChangedFiles.Count == 0 ? "(none)" : string.Join(" | ", record.EvidenceChangedFiles))}",
            $"Off-packet files: {(record.OffPacketFiles.Count == 0 ? "(none)" : string.Join(" | ", record.OffPacketFiles))}",
            $"Truth-write files: {(record.TruthWriteFiles.Count == 0 ? "(none)" : string.Join(" | ", record.TruthWriteFiles))}",
            $"Reason codes: {(record.ReasonCodes.Count == 0 ? "(none)" : string.Join(", ", record.ReasonCodes))}",
            $"Worker allowed actions: {(record.WorkerAllowedActions.Count == 0 ? "(none)" : string.Join(" | ", record.WorkerAllowedActions))}",
            $"Planner-only actions: {(record.PlannerOnlyActions.Count == 0 ? "(none)" : string.Join(" | ", record.PlannerOnlyActions))}",
        };

        return new OperatorCommandResult(0, lines);
    }
}
