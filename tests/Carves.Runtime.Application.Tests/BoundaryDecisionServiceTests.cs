using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class BoundaryDecisionServiceTests
{
    [Fact]
    public void Evaluate_RoutesDirectCompletedToReviewWhenAcceptanceEvidenceContractIsEmpty()
    {
        var service = new BoundaryDecisionService();
        var task = CreateTask(new AcceptanceContract
        {
            ContractId = "AC-DIRECT-EMPTY",
            Status = AcceptanceContractLifecycleStatus.Compiled,
            AutoCompleteAllowed = true,
            HumanReview = new AcceptanceContractHumanReviewPolicy { Required = false },
            EvidenceRequired = [],
        });

        var decision = service.Evaluate(
            task,
            CreateSuccessEnvelope(task.TaskId),
            CreateWorkerArtifact(task.TaskId),
            new SafetyArtifact { Decision = SafetyDecision.Allow(task.TaskId) },
            new ResultValidityDecision(true, "valid", "valid"),
            new ExecutionBoundaryAssessment { Decision = ExecutionBoundaryDecision.Allow });

        Assert.Equal(BoundaryWritebackDecision.AdmitToReview, decision.WritebackDecision);
        Assert.Contains("acceptance_evidence_contract_empty", decision.ReasonCodes);
    }

    [Fact]
    public void Evaluate_RoutesDirectCompletedToReviewWhenWritebackIsOnlyAcceptanceEvidence()
    {
        var service = new BoundaryDecisionService();
        var task = CreateTask(new AcceptanceContract
        {
            ContractId = "AC-DIRECT-SELF-PROVING",
            Status = AcceptanceContractLifecycleStatus.Compiled,
            AutoCompleteAllowed = true,
            HumanReview = new AcceptanceContractHumanReviewPolicy { Required = false },
            EvidenceRequired =
            [
                new AcceptanceContractEvidenceRequirement { Type = "writeback" },
            ],
        });

        var decision = service.Evaluate(
            task,
            CreateSuccessEnvelope(task.TaskId),
            CreateWorkerArtifact(task.TaskId),
            new SafetyArtifact { Decision = SafetyDecision.Allow(task.TaskId) },
            new ResultValidityDecision(true, "valid", "valid"),
            new ExecutionBoundaryAssessment { Decision = ExecutionBoundaryDecision.Allow });

        Assert.Equal(BoundaryWritebackDecision.AdmitToReview, decision.WritebackDecision);
        Assert.Contains("acceptance_evidence_post_writeback_only", decision.ReasonCodes);
    }

    [Fact]
    public void Evaluate_RoutesDirectCompletedToReviewWhenAcceptanceRequiresPostWritebackEvidence()
    {
        var service = new BoundaryDecisionService();
        var task = CreateTask(new AcceptanceContract
        {
            ContractId = "AC-DIRECT-POST-WRITEBACK",
            Status = AcceptanceContractLifecycleStatus.Compiled,
            AutoCompleteAllowed = true,
            HumanReview = new AcceptanceContractHumanReviewPolicy { Required = false },
            EvidenceRequired =
            [
                new AcceptanceContractEvidenceRequirement { Type = "validation_passed" },
                new AcceptanceContractEvidenceRequirement { Type = "result_commit", Description = "Capture the detached result commit." },
            ],
        });

        var decision = service.Evaluate(
            task,
            CreateSuccessEnvelope(task.TaskId),
            CreateWorkerArtifact(task.TaskId),
            new SafetyArtifact { Decision = SafetyDecision.Allow(task.TaskId) },
            new ResultValidityDecision(true, "valid", "valid"),
            new ExecutionBoundaryAssessment { Decision = ExecutionBoundaryDecision.Allow });

        Assert.Equal(BoundaryWritebackDecision.AdmitToReview, decision.WritebackDecision);
        Assert.Contains("acceptance_evidence_post_writeback_only", decision.ReasonCodes);
    }

    [Fact]
    public void Evaluate_RoutesDirectCompletedToReviewWhenAcceptanceDoesNotExplicitlyPermitAutoComplete()
    {
        var service = new BoundaryDecisionService();
        var task = CreateTask(new AcceptanceContract
        {
            ContractId = "AC-DIRECT-NO-AUTO-COMPLETE",
            Status = AcceptanceContractLifecycleStatus.Compiled,
            HumanReview = new AcceptanceContractHumanReviewPolicy { Required = false },
            EvidenceRequired =
            [
                new AcceptanceContractEvidenceRequirement { Type = "validation_passed" },
                new AcceptanceContractEvidenceRequirement { Type = "command_log" },
                new AcceptanceContractEvidenceRequirement { Type = "files_written" },
            ],
        });

        var decision = service.Evaluate(
            task,
            CreateSuccessEnvelope(task.TaskId),
            CreateWorkerArtifact(task.TaskId),
            new SafetyArtifact { Decision = SafetyDecision.Allow(task.TaskId) },
            new ResultValidityDecision(true, "valid", "valid"),
            new ExecutionBoundaryAssessment { Decision = ExecutionBoundaryDecision.Allow });

        Assert.Equal(BoundaryWritebackDecision.AdmitToReview, decision.WritebackDecision);
        Assert.Contains("acceptance_auto_complete_not_allowed", decision.ReasonCodes);
    }

    private static TaskNode CreateTask(AcceptanceContract acceptanceContract)
    {
        return new TaskNode
        {
            TaskId = "T-DIRECT-ACCEPTANCE",
            CardId = "CARD-DIRECT",
            Title = "Direct writeback acceptance gate",
            Description = "Validate direct writeback acceptance evidence.",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Meta,
            Priority = "P1",
            Scope = ["docs"],
            Acceptance = ["direct writeback is governed"],
            AcceptanceContract = acceptanceContract,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    private static ResultEnvelope CreateSuccessEnvelope(string taskId)
    {
        return new ResultEnvelope
        {
            TaskId = taskId,
            Status = "success",
            Validation = new ResultEnvelopeValidation
            {
                CommandsRun = ["docs check"],
                Build = "not_run",
                Tests = "not_run",
            },
            Result = new ResultEnvelopeOutcome
            {
                StopReason = "acceptance_satisfied",
            },
        };
    }

    private static WorkerExecutionArtifact CreateWorkerArtifact(string taskId)
    {
        return new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = "run-direct-acceptance",
                Status = WorkerExecutionStatus.Succeeded,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = "run-direct-acceptance",
                CommandsExecuted = ["docs check"],
                FilesWritten = ["docs/direct.md"],
                CommandLogRef = ".ai/artifacts/worker-executions/run-direct-acceptance/command.log",
                EvidenceCompleteness = ExecutionEvidenceCompleteness.Complete,
                EvidenceStrength = ExecutionEvidenceStrength.Replayable,
            },
        };
    }
}
