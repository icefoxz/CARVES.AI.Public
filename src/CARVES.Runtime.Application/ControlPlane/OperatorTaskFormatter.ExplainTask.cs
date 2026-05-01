using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorTaskFormatter
{
    public static OperatorCommandResult ExplainTask(
        TaskNode task,
        RoleGovernanceRuntimePolicy roleGovernancePolicy,
        CodeGraphScopeAnalysis scopeAnalysis,
        CodeGraphImpactAnalysis impactAnalysis,
        Carves.Runtime.Domain.Execution.WorkerExecutionArtifact? workerExecutionArtifact,
        Carves.Runtime.Domain.Execution.WorkerPermissionArtifact? workerPermissionArtifact,
        Carves.Runtime.Domain.Execution.AiExecutionArtifact? providerArtifact,
        PlannerReviewArtifact? reviewArtifact,
        ReviewEvidenceGateProjection reviewEvidenceProjection)
    {
        var lines = new List<string>
        {
            $"Task: {task.TaskId}",
            $"Title: {task.Title}",
            $"Status: {task.Status}",
            $"Task type: {task.TaskType}",
            $"Dispatch eligibility: {task.TaskType.DescribeDispatchEligibility()}",
            $"Safety profile: {task.TaskType.DescribeSafetyProfile()}",
            $"Priority: {task.Priority}",
            $"Source: {task.Source}",
            $"Card: {task.CardId ?? "(none)"}",
            $"Description: {task.Description}",
            $"Dependencies: {(task.Dependencies.Count == 0 ? "(none)" : string.Join(", ", task.Dependencies))}",
            $"Scope: {(task.Scope.Count == 0 ? "(none)" : string.Join(", ", task.Scope))}",
            $"Acceptance: {(task.Acceptance.Count == 0 ? "(none)" : string.Join(" | ", task.Acceptance))}",
            $"Acceptance contract: {task.AcceptanceContract?.ContractId ?? "(none)"}",
            $"Acceptance contract status: {task.AcceptanceContract?.Status.ToString() ?? "(none)"}",
            $"Planner verdict: {task.PlannerReview.Verdict}",
            $"Planner reason: {task.PlannerReview.Reason}",
            $"Review decision: {task.PlannerReview.DecisionStatus}",
        };
        var acceptanceContractGate = AcceptanceContractExecutionGate.Evaluate(task);
        lines.Add("Acceptance contract gate:");
        lines.Add($"- status: {acceptanceContractGate.Status}");
        lines.Add($"- reason code: {acceptanceContractGate.ReasonCode}");
        lines.Add($"- required: {acceptanceContractGate.Required}");
        lines.Add($"- projected: {acceptanceContractGate.Projected}");
        lines.Add($"- blocks execution: {acceptanceContractGate.BlocksExecution}");
        lines.Add($"- summary: {acceptanceContractGate.Summary}");
        lines.Add($"- recommended next action: {acceptanceContractGate.RecommendedNextAction}");

        if (reviewArtifact is not null)
        {
            lines.Add($"Review artifact status: {reviewArtifact.DecisionStatus} -> {reviewArtifact.ResultingStatus}");
        }

        if (reviewArtifact is not null)
        {
            lines.Add("Review evidence:");
            lines.Add($"- status: {reviewEvidenceProjection.Status}");
            lines.Add($"- can final approve: {reviewEvidenceProjection.CanFinalApprove}");
            lines.Add($"- can writeback proceed: {reviewEvidenceProjection.CanWritebackProceed}");
            lines.Add($"- will apply writeback: {reviewEvidenceProjection.WillApplyWriteback}");
            lines.Add($"- will capture result commit: {reviewEvidenceProjection.WillCaptureResultCommit}");
            lines.Add($"- closure status: {reviewEvidenceProjection.ClosureStatus}");
            lines.Add($"- closure writeback allowed: {reviewEvidenceProjection.ClosureWritebackAllowed}");
            lines.Add($"- closure decision: {reviewEvidenceProjection.ClosureDecision}");
            lines.Add($"- closure blockers: {(reviewEvidenceProjection.ClosureBlockers.Count == 0 ? "(none)" : string.Join(" | ", reviewEvidenceProjection.ClosureBlockers))}");
            lines.Add($"- worker completion claim: status={reviewEvidenceProjection.CompletionClaimStatus}; required={reviewEvidenceProjection.CompletionClaimRequired}");
            lines.Add($"- completion claim present fields: {(reviewEvidenceProjection.CompletionClaimPresentFields.Count == 0 ? "(none)" : string.Join(" | ", reviewEvidenceProjection.CompletionClaimPresentFields))}");
            lines.Add($"- completion claim missing fields: {(reviewEvidenceProjection.CompletionClaimMissingFields.Count == 0 ? "(none)" : string.Join(" | ", reviewEvidenceProjection.CompletionClaimMissingFields))}");
            lines.Add($"- completion claim evidence paths: {(reviewEvidenceProjection.CompletionClaimEvidencePaths.Count == 0 ? "(none)" : string.Join(" | ", reviewEvidenceProjection.CompletionClaimEvidencePaths))}");
            lines.Add($"- completion claim next recommendation: {(string.IsNullOrWhiteSpace(reviewEvidenceProjection.CompletionClaimNextRecommendation) ? "(none)" : reviewEvidenceProjection.CompletionClaimNextRecommendation)}");
            lines.Add($"- completion claim summary: {reviewEvidenceProjection.CompletionClaimSummary}");
            lines.Add($"- summary: {reviewEvidenceProjection.Summary}");
            lines.Add($"- required evidence: {(reviewEvidenceProjection.RequiredEvidence.Count == 0 ? "(none)" : string.Join(" | ", reviewEvidenceProjection.RequiredEvidence))}");
            lines.Add($"- missing before writeback: {(reviewEvidenceProjection.MissingBeforeWriteback.Count == 0 ? "(none)" : string.Join(" | ", reviewEvidenceProjection.MissingBeforeWriteback.Select(static gap => gap.DisplayLabel)))}");
            lines.Add($"- missing after writeback: {(reviewEvidenceProjection.MissingAfterWriteback.Count == 0 ? "(none)" : string.Join(" | ", reviewEvidenceProjection.MissingAfterWriteback.Select(static gap => gap.DisplayLabel)))}");
            lines.Add($"- follow-up actions: {(reviewEvidenceProjection.FollowUpActions.Count == 0 ? "(none)" : string.Join(" | ", reviewEvidenceProjection.FollowUpActions))}");
            lines.Add($"- writeback failure: {reviewEvidenceProjection.WritebackFailureMessage ?? "(none)"}");
        }

        if (task.PlannerReview.DecisionDebt is not null)
        {
            lines.Add($"Review debt: {task.PlannerReview.DecisionDebt.Summary}");
        }

        if (task.ProposalSource != TaskProposalSource.None || !string.IsNullOrWhiteSpace(task.ProposalReason))
        {
            lines.Add($"Proposal source: {task.ProposalSource}");
            lines.Add($"Proposal reason: {task.ProposalReason ?? "(none)"}");
            lines.Add($"Proposal confidence: {task.ProposalConfidence?.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) ?? "(none)"}");
            lines.Add($"Proposal priority hint: {task.ProposalPriorityHint ?? "(none)"}");
        }

        var explicitRoleBinding = TaskRoleBindingMetadata.TryRead(task.Metadata);
        var effectiveRoleBinding = TaskRoleBindingMetadata.Resolve(task, roleGovernancePolicy);
        lines.Add("Role binding:");
        lines.Add($"- source: {(explicitRoleBinding is null ? "policy_default" : "explicit_task_truth")}");
        lines.Add($"- producer: {effectiveRoleBinding.Producer}");
        lines.Add($"- executor: {effectiveRoleBinding.Executor}");
        lines.Add($"- reviewer: {effectiveRoleBinding.Reviewer}");
        lines.Add($"- approver: {effectiveRoleBinding.Approver}");
        lines.Add($"- scope steward: {effectiveRoleBinding.ScopeSteward}");
        lines.Add($"- policy owner: {effectiveRoleBinding.PolicyOwner}");
        lines.Add("Role governance:");
        lines.Add($"- controlled mode default: {roleGovernancePolicy.ControlledModeDefault}");
        lines.Add($"- producer cannot self approve: {roleGovernancePolicy.ProducerCannotSelfApprove}");
        lines.Add($"- reviewer cannot approve same task: {roleGovernancePolicy.ReviewerCannotApproveSameTask}");

        if (task.Metadata.TryGetValue("completion_provenance", out var completionProvenance)
            && !string.IsNullOrWhiteSpace(completionProvenance))
        {
            lines.Add("Completion provenance:");
            lines.Add($"- mode: {completionProvenance}");
            lines.Add($"- outcome status: {task.Metadata.GetValueOrDefault("completion_outcome_status") ?? task.Status.ToString()}");
            lines.Add($"- recorded at: {task.Metadata.GetValueOrDefault("completion_recorded_at") ?? "(none)"}");
            lines.Add($"- reason: {task.Metadata.GetValueOrDefault("completion_reason") ?? task.PlannerReview.Reason}");
            lines.Add($"- historical run id: {task.Metadata.GetValueOrDefault("completion_historical_run_id") ?? "(none)"}");
            lines.Add($"- historical run status: {task.Metadata.GetValueOrDefault("completion_historical_run_status") ?? "(none)"}");
            lines.Add($"- historical worker backend: {task.Metadata.GetValueOrDefault("completion_historical_worker_backend") ?? "(none)"}");
            lines.Add($"- historical worker failure kind: {task.Metadata.GetValueOrDefault("completion_historical_worker_failure_kind") ?? "(none)"}");
            lines.Add($"- historical worker summary: {task.Metadata.GetValueOrDefault("completion_historical_worker_summary") ?? "(none)"}");
            lines.Add($"- historical worker detail ref: {task.Metadata.GetValueOrDefault("completion_historical_worker_detail_ref") ?? "(none)"}");
            lines.Add($"- historical provider detail ref: {task.Metadata.GetValueOrDefault("completion_historical_provider_detail_ref") ?? "(none)"}");
        }

        if (!string.IsNullOrWhiteSpace(task.LastWorkerRunId) || !string.IsNullOrWhiteSpace(task.LastWorkerBackend))
        {
            lines.Add("Worker truth:");
            lines.Add($"- run id: {task.LastWorkerRunId ?? "(none)"}");
            lines.Add($"- backend: {task.LastWorkerBackend ?? "(none)"}");
            lines.Add($"- failure kind: {task.LastWorkerFailureKind}");
            lines.Add($"- retryable: {task.LastWorkerRetryable}");
            lines.Add($"- summary: {task.LastWorkerSummary ?? "(none)"}");
            lines.Add($"- worker detail ref: {task.LastWorkerDetailRef ?? "(none)"}");
            lines.Add($"- provider detail ref: {task.LastProviderDetailRef ?? "(none)"}");
            lines.Add($"- recovery action: {task.LastRecoveryAction}");
            lines.Add($"- recovery reason: {task.LastRecoveryReason ?? "(none)"}");
            if (task.Metadata.TryGetValue("execution_substrate_failure", out var substrateFailure)
                && string.Equals(substrateFailure, "true", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add($"- failure lane: {task.Metadata.GetValueOrDefault("execution_failure_lane") ?? "(none)"}");
                lines.Add($"- failure reason code: {task.Metadata.GetValueOrDefault("execution_failure_reason_code") ?? "(none)"}");
                lines.Add($"- replan allowed: {task.Metadata.GetValueOrDefault("execution_replan_allowed") ?? "(none)"}");
                lines.Add($"- substrate category: {task.Metadata.GetValueOrDefault("execution_substrate_category") ?? "(none)"}");
                lines.Add($"- substrate next action: {task.Metadata.GetValueOrDefault("execution_substrate_next_action") ?? "(none)"}");
                lines.Add($"- recommended next action: {task.Metadata.GetValueOrDefault("execution_failure_next_action") ?? "(none)"}");
            }
        }

        if (task.Metadata.Count > 0)
        {
            lines.Add("Metadata:");
            lines.AddRange(task.Metadata.Select(pair => $"- {pair.Key}: {pair.Value}"));
        }

        if (workerExecutionArtifact is not null)
        {
            lines.Add("Worker execution:");
            lines.Add($"- run id: {workerExecutionArtifact.Result.RunId}");
            lines.Add($"- status: {workerExecutionArtifact.Result.Status}");
            lines.Add($"- backend: {workerExecutionArtifact.Result.BackendId}");
            lines.Add($"- protocol: {workerExecutionArtifact.Result.ProtocolFamily ?? "(none)"}/{workerExecutionArtifact.Result.RequestFamily ?? "(none)"}");
            lines.Add($"- profile: {workerExecutionArtifact.Result.ProfileId}");
            lines.Add($"- trusted: {workerExecutionArtifact.Result.TrustedProfile}");
            lines.Add($"- failure kind: {workerExecutionArtifact.Result.FailureKind}");
            lines.Add($"- failure layer: {workerExecutionArtifact.Result.FailureLayer}");
            lines.Add($"- retryable: {workerExecutionArtifact.Result.Retryable}");
            lines.Add($"- thread id: {workerExecutionArtifact.Result.ThreadId ?? "(none)"}");
            lines.Add($"- thread continuity: {workerExecutionArtifact.Result.ThreadContinuity}");
            lines.Add($"- changed files: {(workerExecutionArtifact.Result.ChangedFiles.Count == 0 ? "(none)" : string.Join(", ", workerExecutionArtifact.Result.ChangedFiles))}");
            lines.Add($"- summary: {workerExecutionArtifact.Projection.Summary}");
            lines.Add($"- detail ref: {workerExecutionArtifact.Projection.DetailRef ?? "(none)"}");
            if (!string.IsNullOrWhiteSpace(workerExecutionArtifact.Projection.ExcerptTail))
            {
                lines.Add($"- excerpt tail: {workerExecutionArtifact.Projection.ExcerptTail}");
            }
            if (workerExecutionArtifact.Result.Events.Count > 0)
            {
                lines.Add("Worker trace:");
                lines.AddRange(workerExecutionArtifact.Result.Events.Take(5).Select(item => $"- {item.EventType}: {item.Summary}"));
            }
        }

        if (workerPermissionArtifact is not null)
        {
            lines.Add("Worker permissions:");
            lines.Add($"- requests: {workerPermissionArtifact.Requests.Count}");
            lines.AddRange(workerPermissionArtifact.Requests.Select(request =>
                $"- {request.PermissionRequestId}: state={request.State}; kind={request.Kind}; risk={request.RiskLevel}; recommended={request.RecommendedDecision}; decision={request.FinalDecision?.ToString() ?? "(none)"}; summary={request.Summary}"));
        }

        if (providerArtifact is not null)
        {
            lines.Add("Worker adapter:");
            lines.Add($"- adapter: {providerArtifact.Record.WorkerAdapter}");
            lines.Add($"- reason: {providerArtifact.Record.WorkerAdapterReason}");
            lines.Add($"- provider: {providerArtifact.Record.Provider}");
            lines.Add($"- protocol: {providerArtifact.Record.ProtocolFamily ?? "(none)"}/{providerArtifact.Record.RequestFamily ?? "(none)"}");
            lines.Add($"- model: {providerArtifact.Record.Model}");
            lines.Add($"- configured: {providerArtifact.Record.Configured}");
            lines.Add($"- fallback: {providerArtifact.Record.UsedFallback}");
            lines.Add($"- failure layer: {providerArtifact.Record.FailureLayer}");
            lines.Add($"- request id: {providerArtifact.Record.RequestId ?? "(none)"}");
            lines.Add($"- summary: {providerArtifact.Projection.Summary}");
            lines.Add($"- detail ref: {providerArtifact.Projection.DetailRef ?? "(none)"}");
        }

        if (scopeAnalysis.HasMatches)
        {
            lines.Add("CodeGraph scope:");
            lines.Add($"- modules: {JoinOrNone(scopeAnalysis.Modules)}");
            lines.Add($"- files: {JoinOrNone(scopeAnalysis.Files)}");
            lines.Add($"- dependency modules: {JoinOrNone(scopeAnalysis.DependencyModules)}");
        }

        if (impactAnalysis.HasMatches)
        {
            lines.Add("CodeGraph impact:");
            lines.Add($"- impacted modules: {JoinOrNone(impactAnalysis.ImpactedModules)}");
            lines.Add($"- impacted files: {JoinOrNone(impactAnalysis.ImpactedFiles)}");
        }

        return new OperatorCommandResult(0, lines);
    }
}
