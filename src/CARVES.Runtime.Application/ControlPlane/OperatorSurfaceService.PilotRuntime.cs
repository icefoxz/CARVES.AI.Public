using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult ClosePilotLoop(PilotCloseLoopRequest request)
    {
        var service = CreatePilotRuntimeService();
        var record = service.CloseLoop(request);
        var lines = new List<string>
        {
            $"Closed pilot loop for {record.TaskId}.",
            $"Execution run: {record.ExecutionRunId}",
            $"Changed file: {record.ChangedFile}",
            $"Result envelope: {record.ResultEnvelopePath}",
            $"Worker execution artifact: {record.WorkerExecutionArtifactPath}",
            $"Evidence root: {record.EvidenceDirectory}",
            string.Empty,
            "task ingest-result:",
        };

        var ingest = IngestTaskResult(request.TaskId);
        lines.AddRange(ingest.Lines);
        if (ingest.ExitCode != 0)
        {
            return new OperatorCommandResult(1, lines);
        }

        if (!string.IsNullOrWhiteSpace(request.ReviewReason))
        {
            lines.Add(string.Empty);
            lines.Add("review complete:");
            var review = ReviewTask(request.TaskId, "complete", request.ReviewReason);
            lines.AddRange(review.Lines);
            if (review.ExitCode != 0)
            {
                return new OperatorCommandResult(1, lines);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.PilotEvidencePath))
        {
            lines.Add(string.Empty);
            lines.Add("pilot record-evidence:");
            var evidence = RecordPilotEvidence(request.PilotEvidencePath);
            lines.AddRange(evidence.Lines);
            if (evidence.ExitCode != 0)
            {
                return new OperatorCommandResult(1, lines);
            }
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult InspectAttachProof(string taskId)
    {
        taskGraphService.GetTask(taskId);
        var service = CreatePilotRuntimeService();
        var proof = service.CaptureAttachToTaskProof(taskId);
        var lines = new List<string>
        {
            $"Attach-to-task proof for {taskId}:",
            $"Proof: {proof.ProofId}",
            $"Repo: {proof.RepoId ?? "(none)"}",
            $"Repo root: {proof.RepoRoot}",
            $"Runtime manifest: {(proof.RuntimeManifestPresent ? "present" : "missing")}",
            $"Attach handshake: {(proof.AttachHandshakePresent ? "present" : "missing")}",
            $"Card: {proof.CardId ?? "(none)"} [{proof.CardLifecycleState ?? "(unknown)"}]",
            $"Task status: {proof.TaskStatus}",
            $"Dispatch state: {proof.DispatchState}",
            $"Execution run: {proof.ExecutionRunId ?? "(none)"} [{proof.ExecutionRunStatus ?? "(none)"}]",
            $"Result: {proof.ResultStatus ?? "(none)"}",
            $"Review verdict: {proof.ReviewVerdict ?? "(none)"}",
            $"Replan entry: {proof.ReplanEntryId ?? "(none)"}",
            $"Suggested tasks: {(proof.SuggestedTaskIds.Count == 0 ? "(none)" : string.Join(", ", proof.SuggestedTaskIds))}",
            $"Execution memory: {(proof.ExecutionMemoryIds.Count == 0 ? "(none)" : string.Join(", ", proof.ExecutionMemoryIds))}",
            "Evidence paths:",
        };

        if (proof.EvidencePaths.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var path in proof.EvidencePaths)
            {
                lines.Add($"- {path}");
            }
        }

        if (proof.Notes.Count > 0)
        {
            lines.Add("Notes:");
            foreach (var note in proof.Notes)
            {
                lines.Add($"- {note}");
            }
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult RecordPilotEvidence(string jsonPath)
    {
        var service = CreatePilotRuntimeService();
        var record = service.RecordPilotEvidence(jsonPath);
        return OperatorCommandResult.Success(
            $"Recorded pilot evidence {record.EvidenceId}.",
            $"Repo: {record.RepoId ?? "(none)"}",
            $"Task: {record.TaskId ?? "(none)"}",
            $"Proof: {record.AttachProofId ?? "(none)"}");
    }

    public OperatorCommandResult InspectPilotEvidence(string evidenceId)
    {
        var service = CreatePilotRuntimeService();
        var record = service.TryGetPilotEvidence(evidenceId);
        if (record is null)
        {
            return OperatorCommandResult.Failure($"Pilot evidence '{evidenceId}' was not found.");
        }

        var lines = new List<string>
        {
            $"Pilot evidence: {record.EvidenceId}",
            $"Repo: {record.RepoId ?? "(none)"}",
            $"Task: {record.TaskId ?? "(none)"}",
            $"Card: {record.CardId ?? "(none)"}",
            $"Summary: {record.Summary}",
            $"Proof: {record.AttachProofId ?? "(none)"}",
            "Observations:",
        };
        lines.AddRange(record.Observations.Count == 0 ? ["- (none)"] : record.Observations.Select(item => $"- {item}"));
        lines.Add("Friction points:");
        lines.AddRange(record.FrictionPoints.Count == 0 ? ["- (none)"] : record.FrictionPoints.Select(item => $"- {item}"));
        lines.Add("Failed expectations:");
        lines.AddRange(record.FailedExpectations.Count == 0 ? ["- (none)"] : record.FailedExpectations.Select(item => $"- {item}"));
        lines.Add("Follow-ups:");
        if (record.FollowUps.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var followUp in record.FollowUps)
            {
                lines.Add($"- [{followUp.Kind}] {followUp.Title}: {followUp.Reason}");
            }
        }

        if (record.RelatedSuggestedTaskIds.Count > 0)
        {
            lines.Add($"Related suggested tasks: {string.Join(", ", record.RelatedSuggestedTaskIds)}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult ListPilotEvidence(string? repoId = null)
    {
        var service = CreatePilotRuntimeService();
        var records = service.ListPilotEvidence(repoId);
        var lines = new List<string> { $"Pilot evidence records{(string.IsNullOrWhiteSpace(repoId) ? string.Empty : $" for {repoId}")}:" };
        if (records.Count == 0)
        {
            lines.Add("- (none)");
            return new OperatorCommandResult(0, lines);
        }

        foreach (var record in records.Take(20))
        {
            lines.Add($"- {record.EvidenceId}: {record.Summary}");
            lines.Add($"  repo={record.RepoId ?? "(none)"}; task={record.TaskId ?? "(none)"}; proof={record.AttachProofId ?? "(none)"}");
        }

        return new OperatorCommandResult(0, lines);
    }

    private PilotRuntimeService CreatePilotRuntimeService()
    {
        return new PilotRuntimeService(paths, taskGraphService, planningDraftService, executionRunService, artifactRepository);
    }
}
