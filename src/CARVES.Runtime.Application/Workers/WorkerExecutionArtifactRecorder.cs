using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Workers;

internal sealed class WorkerExecutionArtifactRecorder
{
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly ExecutionEvidenceRecorder executionEvidenceRecorder;

    public WorkerExecutionArtifactRecorder(
        IRuntimeArtifactRepository artifactRepository,
        ExecutionEvidenceRecorder executionEvidenceRecorder)
    {
        this.artifactRepository = artifactRepository;
        this.executionEvidenceRecorder = executionEvidenceRecorder;
    }

    public void Record(TaskRunReport finalReport, PromptSafeProjection workerProjection)
    {
        var workerExecution = finalReport.WorkerExecution;
        var executionEvidence = executionEvidenceRecorder.Record(finalReport);
        var providerArtifact = BuildProviderArtifact(finalReport.TaskId, workerExecution);

        artifactRepository.SaveWorkerArtifact(new TaskRunArtifact { Report = finalReport });
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = finalReport.TaskId,
            Result = workerExecution,
            Evidence = executionEvidence,
            Projection = workerProjection,
        });
        artifactRepository.SaveWorkerPermissionArtifact(new WorkerPermissionArtifact
        {
            TaskId = finalReport.TaskId,
            RunId = workerExecution.RunId,
            Requests = workerExecution.PermissionRequests,
        });
        artifactRepository.SaveProviderArtifact(providerArtifact);
        artifactRepository.SaveSafetyArtifact(new SafetyArtifact { Decision = finalReport.SafetyDecision });
    }

    private static AiExecutionArtifact BuildProviderArtifact(string taskId, WorkerExecutionResult workerExecution)
    {
        var record = new AiExecutionRecord
        {
            Provider = workerExecution.ProviderId,
            WorkerAdapter = workerExecution.AdapterId,
            WorkerAdapterReason = workerExecution.AdapterReason,
            ProtocolFamily = workerExecution.ProtocolFamily,
            RequestFamily = workerExecution.RequestFamily,
            Model = workerExecution.Model,
            Configured = workerExecution.Configured,
            Succeeded = workerExecution.Succeeded,
            RequestId = workerExecution.RequestId,
            RequestPreview = workerExecution.RequestPreview,
            RequestHash = workerExecution.RequestHash,
            ResponsePreview = workerExecution.ResponsePreview ?? workerExecution.Summary,
            ResponseHash = workerExecution.ResponseHash,
            OutputText = FirstNonEmpty(workerExecution.Rationale, workerExecution.FailureReason, workerExecution.ResponsePreview, workerExecution.Summary),
            FailureReason = workerExecution.FailureReason,
            FailureLayer = workerExecution.FailureLayer,
            InputTokens = workerExecution.InputTokens,
            OutputTokens = workerExecution.OutputTokens,
            CapturedAt = workerExecution.CompletedAt,
        };

        return new AiExecutionArtifact
        {
            TaskId = taskId,
            Record = record,
            Projection = PromptSafeArtifactProjectionFactory.ForProviderArtifact(taskId, record),
        };
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
