using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class PacketEnforcementService
{
    public PacketEnforcementRecord Persist(string taskId, ResultEnvelope? envelope = null, WorkerExecutionArtifact? workerArtifact = null)
    {
        var record = Evaluate(taskId, envelope, workerArtifact);
        var path = GetRecordPath(taskId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(record, JsonOptions));
        return record;
    }

    public PacketEnforcementSurfaceSnapshot BuildSnapshot(string taskId)
    {
        var task = taskGraphService.GetTask(taskId);
        var path = GetRecordPath(taskId);
        var packetPath = GetPacketPath(taskId);
        var persisted = File.Exists(path);
        var record = persisted
            ? JsonSerializer.Deserialize<PacketEnforcementRecord>(File.ReadAllText(path), JsonOptions) ?? Evaluate(taskId)
            : Evaluate(taskId);

        return new PacketEnforcementSurfaceSnapshot
        {
            TaskId = taskId,
            CardId = task.CardId ?? string.Empty,
            PacketPath = ToRepoRelative(packetPath),
            EnforcementPath = ToRepoRelative(path),
            Persisted = persisted,
            Summary = record.Summary,
            Record = record,
        };
    }

    public string GetRecordPath(string taskId)
    {
        return Path.Combine(paths.RuntimeRoot, "packet-enforcement", $"{taskId}.json");
    }

    private string GetPacketPath(string taskId)
    {
        return Path.Combine(paths.RuntimeRoot, "execution-packets", $"{taskId}.json");
    }

    private string GetResultPath(string taskId)
    {
        return Path.Combine(paths.AiRoot, "execution", taskId, "result.json");
    }

    private ResultEnvelope? TryReadResultEnvelope(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ResultEnvelope>(File.ReadAllText(path), JsonOptions);
    }
}
