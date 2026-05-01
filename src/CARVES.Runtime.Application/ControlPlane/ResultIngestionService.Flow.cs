using System.Text.Json;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ResultIngestionService
{
    public ResultEnvelope Read(string taskId)
    {
        var path = ResultPath(taskId);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Result envelope for '{taskId}' was not found at '{path}'.");
        }

        var envelope = JsonSerializer.Deserialize<ResultEnvelope>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException($"Result envelope for '{taskId}' could not be deserialized.");
        Validate(taskId, envelope);
        return envelope;
    }

    public ResultIngestionOutcome Ingest(string taskId)
    {
        var task = taskGraphService.GetTask(taskId);
        var envelope = Read(taskId);
        var workerArtifact = artifactRepository.TryLoadWorkerExecutionArtifact(taskId);
        var safetyArtifact = artifactRepository.TryLoadSafetyArtifact(taskId);

        var resultPath = ResultPath(taskId);
        var fingerprint = ComputeFingerprint(envelope);
        if (task.Metadata.TryGetValue("last_result_fingerprint", out var existingFingerprint)
            && string.Equals(existingFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return new ResultIngestionOutcome(
                taskId,
                envelope.Status,
                task.Status,
                AlreadyApplied: true,
                FailureId: task.Metadata.TryGetValue("last_failure_id", out var failureId) ? failureId : null);
        }

        var run = executionRunService.EnsureRunForResult(task, envelope);
        var validity = resultValidityPolicy.Evaluate(taskId, envelope, workerArtifact, run.RunId);
        var assessment = executionBoundaryService.Assess(task, envelope);
        var packetEnforcement = packetEnforcementService.Persist(taskId, envelope, workerArtifact);
        var decision = boundaryDecisionService.Evaluate(task, envelope, workerArtifact, safetyArtifact, validity, assessment, packetEnforcement);
        if (!validity.Valid)
        {
            var rejectedArtifacts = executionBoundaryArtifactService.Persist(taskId, assessment, run: run, decision: decision);
            var rejectedTask = ApplyBoundaryMetadata(task, assessment, rejectedArtifacts, decision, packetEnforcement);
            taskGraphService.ReplaceTask(rejectedTask);
            markdownSyncService.Sync(taskGraphService.Load(), session: sessionAccessor());
            throw new InvalidOperationException(validity.Message);
        }

        if (assessment.ShouldStop)
        {
            var violation = executionBoundaryService.CreateViolation(taskId, assessment, run);
            var replan = plannerTriggerService.CreateBoundaryReplan(task, violation, executionBoundaryArtifactService.GetViolationPath(taskId), run);
            var artifacts = executionBoundaryArtifactService.Persist(taskId, assessment, violation, replan, run, decision);
            var stoppedRun = executionRunService.StopRun(run, violation, replan, resultPath, artifacts.ViolationPath, artifacts.ReplanPath);
            var nextRun = executionRunService.CreateReplanRun(task, replan, stoppedRun);
            executionRunReportService.Persist(stoppedRun, envelope, violation: violation, replan: replan);
            var stoppedTask = ApplyBoundaryStop(task, envelope, fingerprint, assessment, artifacts, violation, replan, decision, packetEnforcement);
            stoppedTask = plannerTriggerService.ApplyBoundaryStop(stoppedTask, violation, replan);
            stoppedTask = executionPatternGuardService.Apply(stoppedTask, executionPatternService.Analyze(taskId, executionRunReportService.ListReports(taskId)));
            stoppedTask = plannerEmergenceService.CaptureBoundaryStop(stoppedTask, violation, replan);
            stoppedTask = executionRunService.ApplyTaskMetadata(stoppedTask, nextRun, nextRun.RunId);
            ResourceLeaseRecord? boundaryStopLease = null;
            try
            {
                boundaryStopLease = EnforceResultWriteSetLease(
                    task,
                    stoppedTask,
                    stoppedRun,
                    envelope,
                    workerArtifact,
                    packetEnforcement,
                    RunToReviewSubmissionAttempt.NotApplicable(),
                    artifacts,
                    requireWithinDeclaredScope: false);
                var boundaryStopTransitionGate = EnforceTaskTruthTransitionCertificate(
                    task,
                    stoppedTask,
                    stoppedRun,
                    envelope,
                    resultPath,
                    artifacts,
                    RunToReviewSubmissionAttempt.NotApplicable(),
                    leaseId: boundaryStopLease.LeaseId);
                stoppedTask = boundaryStopTransitionGate.Task;
                boundaryStopLease = ReconcileResultWriteSetLease(
                    boundaryStopLease,
                    task,
                    stoppedTask,
                    stoppedRun,
                    envelope,
                    workerArtifact,
                    packetEnforcement,
                    RunToReviewSubmissionAttempt.NotApplicable(),
                    artifacts,
                    requireWithinDeclaredScope: false);
                stoppedTask = ApplyTaskTruthTransitionAuthorizationMetadata(
                    stoppedTask,
                    boundaryStopTransitionGate.Certificate);
                taskGraphService.ReplaceTask(stoppedTask);
                stoppedTask = RecordTaskTruthTransitionCommitted(
                    task,
                    taskGraphService.GetTask(task.TaskId),
                    stoppedRun,
                    RunToReviewSubmissionAttempt.NotApplicable(),
                    boundaryStopTransitionGate.Certificate);
                taskGraphService.ReplaceTask(stoppedTask);
                markdownSyncService.Sync(taskGraphService.Load(), session: sessionAccessor());
                ReleaseResultWriteSetLease(boundaryStopLease, $"boundary_stop_{stoppedTask.Status.ToString().ToLowerInvariant()}");
                return new ResultIngestionOutcome(
                    taskId,
                    envelope.Status,
                    stoppedTask.Status,
                    AlreadyApplied: false,
                    FailureId: null,
                    BoundaryStopped: true,
                    BoundaryReason: violation.Reason.ToString());
            }
            catch
            {
                ReleaseResultWriteSetLease(boundaryStopLease, "result_ingestion_aborted");
                throw;
            }
        }

        var passArtifacts = executionBoundaryArtifactService.Persist(taskId, assessment, run: run, decision: decision);
        var runToReviewSubmission = runToReviewSubmissionService.TryCreate(
            task,
            run,
            envelope,
            workerArtifact!,
            decision,
            passArtifacts);
        if (!runToReviewSubmission.CanProceed)
        {
            throw new InvalidOperationException(
                runToReviewSubmission.FailureMessage ?? $"Cannot submit {taskId} to review: review submission sidecar could not be written.");
        }

        var mappedTask = decision.WritebackDecision switch
        {
            BoundaryWritebackDecision.AdmitToReview or BoundaryWritebackDecision.RequireHumanReview
                => ApplyBoundaryMetadata(ApplyReviewAdmission(task, envelope, fingerprint, decision), assessment, passArtifacts, decision, packetEnforcement),
            BoundaryWritebackDecision.QuarantineResult
                => ApplyBoundaryMetadata(ApplyQuarantinedResult(task, envelope, fingerprint, decision), assessment, passArtifacts, decision, packetEnforcement),
            BoundaryWritebackDecision.RetryableInfraFailure or BoundaryWritebackDecision.SemanticFailure or BoundaryWritebackDecision.AdmitToWriteback
                => ApplyBoundaryMetadata(ApplyResult(task, envelope, fingerprint), assessment, passArtifacts, decision, packetEnforcement),
            BoundaryWritebackDecision.RejectResult
                => ApplyBoundaryMetadata(task, assessment, passArtifacts, decision, packetEnforcement),
            _ => ApplyBoundaryMetadata(ApplyResult(task, envelope, fingerprint), assessment, passArtifacts, decision, packetEnforcement),
        };
        FailureReport? failure = null;
        if (decision.WritebackDecision is BoundaryWritebackDecision.RetryableInfraFailure or BoundaryWritebackDecision.SemanticFailure)
        {
            failure = failureReportService.EmitResultFailure(mappedTask, envelope, workerArtifact);
        }

        mappedTask = plannerTriggerService.Apply(mappedTask, failure);
        var finalRun = decision.WritebackDecision switch
        {
            BoundaryWritebackDecision.AdmitToWriteback or BoundaryWritebackDecision.AdmitToReview or BoundaryWritebackDecision.RequireHumanReview
                => executionRunService.CompleteRun(run, resultPath),
            _ => executionRunService.FailRun(run, resultPath, decision.Summary),
        };
        executionRunReportService.Persist(finalRun, envelope, failure);
        mappedTask = executionPatternGuardService.Apply(mappedTask, executionPatternService.Analyze(taskId, executionRunReportService.ListReports(taskId)));
        mappedTask = plannerEmergenceService.CaptureResult(mappedTask, envelope, failure);
        mappedTask = executionRunService.ApplyTaskMetadata(mappedTask, finalRun, activeRunId: null);
        ResourceLeaseRecord? resultLease = null;
        try
        {
            resultLease = EnforceResultWriteSetLease(
                task,
                mappedTask,
                finalRun,
                envelope,
                workerArtifact,
                packetEnforcement,
                runToReviewSubmission,
                passArtifacts,
                requireWithinDeclaredScope: mappedTask.Status != task.Status);
            if (decision.WritebackDecision is BoundaryWritebackDecision.AdmitToReview or BoundaryWritebackDecision.RequireHumanReview)
            {
                runToReviewSubmission = runToReviewSubmissionService.CommitPrepared(
                    task,
                    finalRun,
                    runToReviewSubmission,
                    resultLease.LeaseId);
                if (!runToReviewSubmission.CanProceed)
                {
                    throw new InvalidOperationException(
                        runToReviewSubmission.FailureMessage ?? $"Cannot submit {taskId} to review: review submission commit failed.");
                }

                resultLease = ReconcileResultWriteSetLease(
                    resultLease,
                    task,
                    mappedTask,
                    finalRun,
                    envelope,
                    workerArtifact,
                    packetEnforcement,
                    runToReviewSubmission,
                    passArtifacts,
                    requireWithinDeclaredScope: mappedTask.Status != task.Status);
            }

            mappedTask = ApplyRunToReviewSubmissionMetadata(mappedTask, runToReviewSubmission);
            var taskTruthTransitionGate = EnforceTaskTruthTransitionCertificate(
                task,
                mappedTask,
                finalRun,
                envelope,
                resultPath,
                passArtifacts,
                runToReviewSubmission,
                leaseId: resultLease.LeaseId);
            mappedTask = taskTruthTransitionGate.Task;
            resultLease = ReconcileResultWriteSetLease(
                resultLease,
                task,
                mappedTask,
                finalRun,
                envelope,
                workerArtifact,
                packetEnforcement,
                runToReviewSubmission,
                passArtifacts,
                requireWithinDeclaredScope: mappedTask.Status != task.Status);
            mappedTask = ApplyTaskTruthTransitionAuthorizationMetadata(
                mappedTask,
                taskTruthTransitionGate.Certificate);
            taskGraphService.ReplaceTask(mappedTask);
            mappedTask = RecordTaskTruthTransitionCommitted(
                task,
                taskGraphService.GetTask(task.TaskId),
                finalRun,
                runToReviewSubmission,
                taskTruthTransitionGate.Certificate);
            taskGraphService.ReplaceTask(mappedTask);
            markdownSyncService.Sync(taskGraphService.Load(), session: sessionAccessor());
            ReleaseResultWriteSetLease(resultLease, $"result_ingestion_{mappedTask.Status.ToString().ToLowerInvariant()}");
            return new ResultIngestionOutcome(
                taskId,
                envelope.Status,
                mappedTask.Status,
                AlreadyApplied: false,
                FailureId: failure?.Id,
                BoundaryStopped: false,
                BoundaryReason: null,
                ReviewSubmissionPath: runToReviewSubmission.SubmissionPath,
                EffectLedgerPath: runToReviewSubmission.EffectLedgerPath,
                ResultCommit: runToReviewSubmission.ResultCommit);
        }
        catch
        {
            ReleaseResultWriteSetLease(resultLease, "result_ingestion_aborted");
            throw;
        }
    }
}
