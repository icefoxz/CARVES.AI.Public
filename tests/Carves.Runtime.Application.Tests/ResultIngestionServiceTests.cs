using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class ResultIngestionServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    [Fact]
    public void Ingest_FailedResult_UpdatesTaskAndEmitsFailureOnce()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-168-001");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteWorkerExecutionArtifact(workspace, artifactRepository, task.TaskId, run.RunId, "dotnet build", "dotnet test");
        var failureReportService = new FailureReportService(
            workspace.RootPath,
            failureRepository,
            new FailureClassificationService(),
            artifactRepository);
        var plannerTriggerService = new PlannerTriggerService(new FailureContextService(failureRepository));
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            failureReportService,
            plannerTriggerService,
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService);

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "failed",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = ["src/CARVES.Runtime.Application/ControlPlane/ResultIngestionService.cs"],
                    LinesChanged = 24,
                },
                Validation = new ResultEnvelopeValidation
                {
                    CommandsRun = ["dotnet build", "dotnet test"],
                    Build = "success",
                    Tests = "failed",
                },
                Result = new ResultEnvelopeOutcome
                {
                    StopReason = "test_failure",
                },
                Failure = new ResultEnvelopeFailure
                {
                    Type = nameof(FailureType.TestRegression),
                    Message = "Regression introduced by worker output.",
                },
                Next = new ResultEnvelopeNextAction
                {
                    Suggested = "Fix the test regression before retrying.",
                },
            });

        var first = service.Ingest(task.TaskId);
        var stored = taskGraphService.GetTask(task.TaskId);
        var failures = failureRepository.LoadAll();

        Assert.False(first.AlreadyApplied);
        Assert.Equal(DomainTaskStatus.Blocked, first.TaskStatus);
        Assert.Equal(DomainTaskStatus.Blocked, stored.Status);
        Assert.Equal(nameof(FailureType.TestRegression), stored.Metadata["last_failure_type"]);
        Assert.Equal("1", stored.Metadata["failure_count"]);
        Assert.Equal(run.RunId, stored.Metadata["execution_run_latest_id"]);
        Assert.Equal("Failed", stored.Metadata["execution_run_latest_status"]);
        Assert.Equal("HealthyProgress", stored.Metadata["execution_pattern_type"]);
        Assert.Equal("false", stored.Metadata["execution_pattern_warning"]);
        Assert.Equal("true", stored.Metadata["planner_replan_required"]);
        Assert.Equal(nameof(Carves.Runtime.Domain.Planning.PlannerReplanTrigger.TaskFailed), stored.Metadata["planner_entry_reason"]);
        Assert.Single(failures);
        Assert.Equal(FailureType.TestRegression, failures[0].Failure.Type);
        Assert.Equal(ExecutionRunStatus.Failed, executionRunService.Get(run.RunId).Status);
        Assert.False(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "run-reports", task.TaskId, $"{run.RunId}.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "planning", "replans", task.TaskId, $"{stored.Metadata["planner_replan_entry_id"]}.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "memory", "execution", task.TaskId, "MEM-T-CARD-168-001-001.json")));

        var second = service.Ingest(task.TaskId);

        Assert.True(second.AlreadyApplied);
        Assert.Single(failureRepository.LoadAll());
    }

    [Fact]
    public void Ingest_SuccessResult_StopsAtReviewBoundaryAndPersistsBoundaryDecision()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-168-002");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteWorkerExecutionArtifact(workspace, artifactRepository, task.TaskId, run.RunId, "dotnet build");
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService);

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = ["src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.Failures.cs"],
                    LinesChanged = 12,
                },
                Validation = new ResultEnvelopeValidation
                {
                    CommandsRun = ["dotnet build"],
                    Build = "success",
                    Tests = "not_run",
                },
                Result = new ResultEnvelopeOutcome
                {
                    StopReason = "acceptance_satisfied",
                },
                Telemetry = new ExecutionTelemetry
                {
                    DurationSeconds = 120,
                    ObservedPaths = ["src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.Failures.cs"],
                    ChangeKinds = [ExecutionChangeKind.SourceCode],
                    BudgetExceeded = false,
                    Summary = "Within declared execution budget.",
                },
            });

        var outcome = service.Ingest(task.TaskId);
        var stored = taskGraphService.GetTask(task.TaskId);

        Assert.Equal(DomainTaskStatus.Review, outcome.TaskStatus);
        Assert.Equal(DomainTaskStatus.Review, stored.Status);
        Assert.Equal("admit_to_review", stored.Metadata["execution_boundary_writeback_decision"]);
        Assert.Equal("0.8", stored.Metadata["execution_boundary_decision_confidence"]);
        Assert.Equal(run.RunId, stored.Metadata["execution_run_latest_id"]);
        Assert.False(stored.Metadata.ContainsKey("execution_run_active_id"));
        Assert.Equal("HealthyProgress", stored.Metadata["execution_pattern_type"]);
        Assert.Equal("false", stored.Metadata["execution_pattern_warning"]);
        Assert.Equal($".ai/artifacts/worker-executions/{run.RunId}/review-submission.json", outcome.ReviewSubmissionPath);
        Assert.Equal(outcome.ReviewSubmissionPath, stored.Metadata["review_submission_sidecar_path"]);
        Assert.Equal($".ai/artifacts/worker-executions/{run.RunId}/effect-ledger.jsonl", outcome.EffectLedgerPath);
        Assert.Equal(outcome.EffectLedgerPath, stored.Metadata["review_submission_effect_ledger_path"]);
        Assert.Equal("submitted_to_review", stored.Metadata["review_submission_terminal_state"]);
        Assert.Equal($".ai/artifacts/worker-executions/{run.RunId}/state-transition-certificate.json", stored.Metadata["review_submission_state_transition_certificate_path"]);
        Assert.Contains("task_status_to_review", stored.Metadata["review_submission_certified_transitions"], StringComparison.Ordinal);
        Assert.Contains("review_submission_recorded", stored.Metadata["review_submission_certified_transitions"], StringComparison.Ordinal);
        Assert.Equal("not_captured_git_client_unavailable", stored.Metadata["review_submission_result_commit_status"]);
        Assert.False(stored.ResultCommit is not null);
        Assert.Empty(failureRepository.LoadAll());
        Assert.Equal(ExecutionRunStatus.Completed, executionRunService.Get(run.RunId).Status);
        var reviewSubmissionPath = Path.Combine(workspace.RootPath, outcome.ReviewSubmissionPath!.Replace('/', Path.DirectorySeparatorChar));
        var effectLedgerPath = Path.Combine(workspace.RootPath, outcome.EffectLedgerPath!.Replace('/', Path.DirectorySeparatorChar));
        var certificatePath = Path.Combine(workspace.RootPath, stored.Metadata["review_submission_state_transition_certificate_path"].Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(reviewSubmissionPath));
        Assert.True(File.Exists(effectLedgerPath));
        Assert.True(File.Exists(certificatePath));
        Assert.Contains("\"review_verdict_written\": false", File.ReadAllText(reviewSubmissionPath), StringComparison.Ordinal);
        Assert.Contains("\"task_completed\": false", File.ReadAllText(reviewSubmissionPath), StringComparison.Ordinal);
        var certificate = File.ReadAllText(certificatePath);
        using var reviewCertificateDocument = JsonDocument.Parse(certificate);
        Assert.Contains("\"operation\": \"task_status_to_review\"", certificate, StringComparison.Ordinal);
        Assert.Contains("\"operation\": \"review_submission_recorded\"", certificate, StringComparison.Ordinal);
        Assert.Contains("\"policy_verdict\": \"allow\"", certificate, StringComparison.Ordinal);
        Assert.Equal("result-ingestion:" + run.RunId, reviewCertificateDocument.RootElement.GetProperty("work_order_id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(reviewCertificateDocument.RootElement.GetProperty("lease_id").GetString()));
        Assert.Equal(
            stored.Metadata["task_truth_transition_certificate_hash"],
            reviewCertificateDocument.RootElement.GetProperty("certificate_hash").GetString());
        var effectLedger = File.ReadAllText(effectLedgerPath);
        Assert.Contains("create_review_submission_sidecar", effectLedger, StringComparison.Ordinal);
        Assert.Contains("task_status_to_review", effectLedger, StringComparison.Ordinal);
        Assert.Contains("state_transition_certificate", effectLedger, StringComparison.Ordinal);
        Assert.Contains("final_seal", effectLedger, StringComparison.Ordinal);
        var replay = new EffectLedgerService(workspace.Paths).ReplayRun(run.RunId);
        Assert.Equal("verified", replay.ReplayState);
        Assert.True(replay.CanWriteBack);
        Assert.True(replay.Sealed);
        Assert.Equal(task.TaskId, replay.TaskId);
        Assert.Equal(run.RunId, replay.RunId);
        Assert.Equal("submitted_to_review", replay.TerminalState);
        Assert.Equal(4, replay.EventCount);
        Assert.Contains("submit_to_review", replay.StepEvents);
        Assert.Contains("state_transition_certificate", replay.StepEvents);
        Assert.Contains(stored.Metadata["review_submission_effect_ledger_event_hash"], effectLedger, StringComparison.Ordinal);
        var reviewLeaseSnapshot = new ResourceLeaseService(workspace.Paths).LoadSnapshot();
        var reviewLease = Assert.Single(reviewLeaseSnapshot.Leases);
        Assert.Equal(ResourceLeaseStatus.Released, reviewLease.Status);
        Assert.Equal("result_ingestion_review", reviewLease.ReleaseReason);
        Assert.Contains("src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.Failures.cs", reviewLease.ActualWriteSet.Paths);
        Assert.Contains($"ai/runtime/run-reports/{task.TaskId}/{run.RunId}.json", reviewLease.ActualWriteSet.Paths);
        Assert.Contains($"ai/runtime/boundary/decisions/{task.TaskId}.json", reviewLease.ActualWriteSet.Paths);
        Assert.Contains($"ai/artifacts/worker-executions/{run.RunId}/review-submission.json", reviewLease.ActualWriteSet.Paths);
        Assert.Contains($"ai/artifacts/worker-executions/{run.RunId}/state-transition-certificate.json", reviewLease.ActualWriteSet.Paths);
        Assert.Contains($"ai/artifacts/worker-executions/{run.RunId}/effect-ledger.jsonl", reviewLease.ActualWriteSet.Paths);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "run-reports", task.TaskId, $"{run.RunId}.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "boundary", "decisions", $"{task.TaskId}.json")));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.pending.json")));
    }

    [Fact]
    public void Ingest_SuccessResultFromGitWorktree_CapturesResultCommitBeforeReviewSubmission()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-168-006");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", task.TaskId);
        Directory.CreateDirectory(worktreePath);
        var changedPath = "src/CARVES.Runtime.Application/ControlPlane/ResultIngestionService.cs";
        WriteWorkerExecutionArtifact(
            workspace,
            artifactRepository,
            task.TaskId,
            run.RunId,
            worktreePath,
            [changedPath],
            "dotnet build",
            "dotnet test");
        var gitClient = new ResultCommitGitClient(worktreePath);
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService,
            runToReviewSubmissionService: new RunToReviewSubmissionService(workspace.Paths, gitClient));

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = [changedPath],
                    LinesChanged = 9,
                },
                Validation = new ResultEnvelopeValidation
                {
                    CommandsRun = ["dotnet build", "dotnet test"],
                    Build = "success",
                    Tests = "success",
                },
                Result = new ResultEnvelopeOutcome
                {
                    StopReason = "acceptance_satisfied",
                },
                Telemetry = new ExecutionTelemetry
                {
                    DurationSeconds = 90,
                    ObservedPaths = [changedPath],
                    ChangeKinds = [ExecutionChangeKind.SourceCode],
                    BudgetExceeded = false,
                    Summary = "Within declared execution budget.",
                },
            });

        var outcome = service.Ingest(task.TaskId);
        var stored = taskGraphService.GetTask(task.TaskId);
        var reviewSubmissionPath = Path.Combine(workspace.RootPath, outcome.ReviewSubmissionPath!.Replace('/', Path.DirectorySeparatorChar));
        var submission = File.ReadAllText(reviewSubmissionPath);

        Assert.Equal(DomainTaskStatus.Review, outcome.TaskStatus);
        Assert.Equal("p4-result-commit", outcome.ResultCommit);
        Assert.Equal("p4-result-commit", stored.ResultCommit);
        Assert.Equal("p4-result-commit", stored.Metadata["review_submission_result_commit"]);
        Assert.Equal("captured", stored.Metadata["review_submission_result_commit_status"]);
        Assert.Equal([changedPath], gitClient.CapturedPaths);
        Assert.Contains("\"result_commit\": \"p4-result-commit\"", submission, StringComparison.Ordinal);
        Assert.Contains("\"result_commit_status\": \"captured\"", submission, StringComparison.Ordinal);
        Assert.Contains("\"review_verdict_written\": false", submission, StringComparison.Ordinal);
        Assert.Equal(ExecutionRunStatus.Completed, executionRunService.Get(run.RunId).Status);
    }

    [Fact]
    public void Ingest_DirectTaskWriteback_IssuesAndVerifiesTaskTruthTransitionCertificate()
    {
        using var workspace = new TemporaryWorkspace();
        const string changedPath = "docs/direct-writeback.md";
        var task = CreateTask(
            "T-CARD-168-DIRECT",
            TaskType.Meta,
            ["docs"],
            CreateDirectWritebackAcceptanceContract("T-CARD-168-DIRECT"));
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteWorkerExecutionArtifact(
            workspace,
            artifactRepository,
            task.TaskId,
            run.RunId,
            worktreePath: null,
            filesWritten: [changedPath],
            "docs check");
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService);

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = [changedPath],
                    LinesChanged = 2,
                },
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
                Telemetry = new ExecutionTelemetry
                {
                    DurationSeconds = 5,
                    ObservedPaths = [changedPath],
                    ChangeKinds = [ExecutionChangeKind.Documentation],
                    BudgetExceeded = false,
                    Summary = "Within declared execution budget.",
                },
            });

        var outcome = service.Ingest(task.TaskId);
        var stored = taskGraphService.GetTask(task.TaskId);

        Assert.Equal(DomainTaskStatus.Completed, outcome.TaskStatus);
        Assert.Equal(DomainTaskStatus.Completed, stored.Status);
        Assert.Equal("admit_to_writeback", stored.Metadata["execution_boundary_writeback_decision"]);
        Assert.Equal($".ai/artifacts/worker-executions/{run.RunId}/state-transition-certificate.json", stored.Metadata["task_truth_transition_certificate_path"]);
        Assert.Contains("task_status_to_completed", stored.Metadata["task_truth_transition_certified_transitions"], StringComparison.Ordinal);
        Assert.Equal("host.result_ingestion.task_truth_transition", stored.Metadata["task_truth_transition_certificate_host_route"]);
        Assert.Equal(stored.Metadata["task_truth_transition_committed_event_hash"], stored.Metadata["task_truth_transition_effect_ledger_event_hash"]);
        Assert.Equal($".ai/artifacts/worker-executions/{run.RunId}/task-truth-writeback-receipt.json", stored.Metadata["task_truth_transition_receipt_path"]);
        Assert.False(stored.Metadata.ContainsKey("task_truth_transition_authorization_certificate_path"));
        Assert.False(stored.Metadata.ContainsKey("task_truth_transition_authorization_certificate_hash"));
        Assert.False(stored.Metadata.ContainsKey("task_truth_transition_authorization_effect_ledger_event_hash"));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.json")));
        var certificatePath = Path.Combine(workspace.RootPath, stored.Metadata["task_truth_transition_certificate_path"].Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(certificatePath));
        var certificate = File.ReadAllText(certificatePath);
        Assert.Contains("\"operation\": \"task_status_to_completed\"", certificate, StringComparison.Ordinal);
        Assert.Contains("\"host_route\": \"host.result_ingestion.task_truth_transition\"", certificate, StringComparison.Ordinal);
        using var certificateDocument = JsonDocument.Parse(certificate);
        Assert.Equal(
            stored.Metadata["task_truth_transition_certificate_hash"],
            certificateDocument.RootElement.GetProperty("certificate_hash").GetString());
        Assert.Equal(
            stored.Metadata["task_truth_transition_committed_event_hash"],
            certificateDocument.RootElement.GetProperty("effect_ledger_event_hash").GetString());
        Assert.Contains(
            certificateDocument.RootElement.GetProperty("required_evidence").EnumerateArray(),
            evidence => string.Equals(
                evidence.GetProperty("kind").GetString(),
                "task_truth_writeback_receipt",
                StringComparison.Ordinal));
        Assert.Equal("result-ingestion:" + run.RunId, certificateDocument.RootElement.GetProperty("work_order_id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(certificateDocument.RootElement.GetProperty("lease_id").GetString()));
        var effectLedger = File.ReadAllText(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "effect-ledger.jsonl"));
        Assert.Contains("task_truth_transition_authorized", effectLedger, StringComparison.Ordinal);
        Assert.Contains("task_truth_transition_committed", effectLedger, StringComparison.Ordinal);
        Assert.Contains("state_transition_certificate", effectLedger, StringComparison.Ordinal);
        var replay = new EffectLedgerService(workspace.Paths).ReplayRun(run.RunId);
        Assert.Equal("verified", replay.ReplayState);
        Assert.True(replay.Sealed);
        Assert.Contains("task_truth_transition_authorized", replay.StepEvents);
        Assert.Contains("task_truth_transition_committed", replay.StepEvents);
        Assert.Contains("state_transition_certificate", replay.StepEvents);
        Assert.Equal(ExecutionRunStatus.Completed, executionRunService.Get(run.RunId).Status);
        var resourceLeaseSnapshot = new ResourceLeaseService(workspace.Paths).LoadSnapshot();
        var resourceLease = Assert.Single(resourceLeaseSnapshot.Leases);
        Assert.Equal(ResourceLeaseStatus.Released, resourceLease.Status);
        Assert.Equal("result_ingestion_completed", resourceLease.ReleaseReason);
        Assert.Contains(changedPath, resourceLease.ActualWriteSet.Paths);
        Assert.Contains($"ai/runtime/run-reports/{task.TaskId}/{run.RunId}.json", resourceLease.ActualWriteSet.Paths);
        Assert.Contains($"ai/runtime/boundary/decisions/{task.TaskId}.json", resourceLease.ActualWriteSet.Paths);
        Assert.Contains($"ai/artifacts/worker-executions/{run.RunId}/state-transition-certificate.json", resourceLease.ActualWriteSet.Paths);
        Assert.Contains($"ai/artifacts/worker-executions/{run.RunId}/effect-ledger.jsonl", resourceLease.ActualWriteSet.Paths);
    }

    [Fact]
    public void Ingest_DirectTaskWritebackWithoutAutoCompleteAcceptance_RoutesToReview()
    {
        using var workspace = new TemporaryWorkspace();
        const string changedPath = "docs/direct-writeback-needs-review.md";
        var task = CreateTask(
            "T-CARD-168-DIRECT-REVIEW",
            TaskType.Meta,
            ["docs"],
            new AcceptanceContract
            {
                ContractId = "AC-T-CARD-168-DIRECT-REVIEW",
                Title = "Direct writeback requires explicit auto-complete permission",
                Status = AcceptanceContractLifecycleStatus.Compiled,
                HumanReview = new AcceptanceContractHumanReviewPolicy
                {
                    Required = false,
                },
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement { Type = "validation_passed" },
                    new AcceptanceContractEvidenceRequirement { Type = "command_log" },
                    new AcceptanceContractEvidenceRequirement { Type = "files_written" },
                ],
                Traceability = new AcceptanceContractTraceability
                {
                    SourceTaskId = "T-CARD-168-DIRECT-REVIEW",
                },
            });
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteWorkerExecutionArtifact(
            workspace,
            artifactRepository,
            task.TaskId,
            run.RunId,
            worktreePath: null,
            filesWritten: [changedPath],
            "docs check");
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService);

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = [changedPath],
                    LinesChanged = 2,
                },
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
                Telemetry = new ExecutionTelemetry
                {
                    DurationSeconds = 5,
                    ObservedPaths = [changedPath],
                    ChangeKinds = [ExecutionChangeKind.Documentation],
                    BudgetExceeded = false,
                    Summary = "Within declared execution budget.",
                },
            });

        var outcome = service.Ingest(task.TaskId);
        var stored = taskGraphService.GetTask(task.TaskId);

        Assert.Equal(DomainTaskStatus.Review, outcome.TaskStatus);
        Assert.Equal(DomainTaskStatus.Review, stored.Status);
        Assert.Equal("admit_to_review", stored.Metadata["execution_boundary_writeback_decision"]);
        Assert.Contains("acceptance_auto_complete_not_allowed", stored.Metadata["execution_boundary_reason_codes"], StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.json")));
        Assert.Equal($".ai/artifacts/worker-executions/{run.RunId}/state-transition-certificate.json", stored.Metadata["task_truth_transition_certificate_path"]);
        Assert.Contains("task_status_to_review", stored.Metadata["task_truth_transition_certified_transitions"], StringComparison.Ordinal);
        Assert.DoesNotContain("task_status_to_completed", stored.Metadata["task_truth_transition_certified_transitions"], StringComparison.Ordinal);
        Assert.Equal(stored.Metadata["task_truth_transition_certificate_hash"], stored.Metadata["review_submission_state_transition_certificate_hash"]);
        Assert.Equal(stored.Metadata["task_truth_transition_committed_event_hash"], stored.Metadata["task_truth_transition_effect_ledger_event_hash"]);
    }

    [Fact]
    public void Ingest_DirectTaskWritebackWithoutStateTransitionCertificate_DoesNotMutateTaskTruth()
    {
        using var workspace = new TemporaryWorkspace();
        const string changedPath = "docs/direct-writeback-without-stc.md";
        var task = CreateTask(
            "T-CARD-168-DIRECT-NO-STC",
            TaskType.Meta,
            ["docs"],
            CreateDirectWritebackAcceptanceContract("T-CARD-168-DIRECT-NO-STC"));
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteWorkerExecutionArtifact(
            workspace,
            artifactRepository,
            task.TaskId,
            run.RunId,
            worktreePath: null,
            filesWritten: [changedPath],
            "docs check");
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService,
            stateTransitionCertificateService: new RejectingStateTransitionCertificateService(workspace.Paths));

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = [changedPath],
                    LinesChanged = 2,
                },
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
                Telemetry = new ExecutionTelemetry
                {
                    DurationSeconds = 5,
                    ObservedPaths = [changedPath],
                    ChangeKinds = [ExecutionChangeKind.Documentation],
                    BudgetExceeded = false,
                    Summary = "Within declared execution budget.",
                },
            });

        var exception = Assert.Throws<InvalidOperationException>(() => service.Ingest(task.TaskId));
        var stored = taskGraphService.GetTask(task.TaskId);

        Assert.Contains("state transition certificate", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DomainTaskStatus.Pending, stored.Status);
        Assert.False(stored.Metadata.ContainsKey("task_truth_transition_certificate_path"));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "state-transition-certificate.json")));
    }

    [Fact]
    public void Ingest_DirectTaskWritebackFailureAfterInitialTruthWrite_PersistsAuthorizationMetadataOnly()
    {
        using var workspace = new TemporaryWorkspace();
        const string changedPath = "docs/direct-writeback-phase3.md";
        var task = CreateTask(
            "T-CARD-168-DIRECT-PHASE3",
            TaskType.Meta,
            ["docs"],
            CreateDirectWritebackAcceptanceContract("T-CARD-168-DIRECT-PHASE3"));
        var repository = new FailOnNthUpsertTaskGraphRepository(new DomainTaskGraph([task]), failOnUpsertCount: 2);
        var taskGraphService = new TaskGraphService(repository, new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteWorkerExecutionArtifact(
            workspace,
            artifactRepository,
            task.TaskId,
            run.RunId,
            worktreePath: null,
            filesWritten: [changedPath],
            "docs check");
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService);

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = [changedPath],
                    LinesChanged = 2,
                },
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
                Telemetry = new ExecutionTelemetry
                {
                    DurationSeconds = 5,
                    ObservedPaths = [changedPath],
                    ChangeKinds = [ExecutionChangeKind.Documentation],
                    BudgetExceeded = false,
                    Summary = "Within declared execution budget.",
                },
            });

        var exception = Assert.Throws<InvalidOperationException>(() => service.Ingest(task.TaskId));

        Assert.Contains("Simulated task graph replace failure", exception.Message, StringComparison.Ordinal);
        var stored = taskGraphService.GetTask(task.TaskId);
        Assert.Equal(DomainTaskStatus.Completed, stored.Status);
        Assert.Equal($".ai/artifacts/worker-executions/{run.RunId}/state-transition-certificate.json", stored.Metadata["task_truth_transition_authorization_certificate_path"]);
        Assert.Equal("host.result_ingestion.task_truth_transition", stored.Metadata["task_truth_transition_authorization_certificate_host_route"]);
        Assert.Contains("task_status_to_completed", stored.Metadata["task_truth_transition_authorization_certified_transitions"], StringComparison.Ordinal);
        Assert.False(stored.Metadata.ContainsKey("task_truth_transition_certificate_path"));
        Assert.False(stored.Metadata.ContainsKey("task_truth_transition_committed_event_hash"));
        Assert.False(stored.Metadata.ContainsKey("task_truth_transition_receipt_path"));
    }

    [Fact]
    public void Ingest_ResultWithActualWriteSetOutsideHostDeclaredScope_StopsBeforeTruthWriteback()
    {
        using var workspace = new TemporaryWorkspace();
        const string changedPath = "docs/out-of-scope.md";
        var task = CreateTask(
            "T-CARD-168-RESOURCE-LEASE-STOP",
            TaskType.Meta,
            ["src/Allowed/"],
            CreateDirectWritebackAcceptanceContract("T-CARD-168-RESOURCE-LEASE-STOP"));
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteWorkerExecutionArtifact(
            workspace,
            artifactRepository,
            task.TaskId,
            run.RunId,
            worktreePath: null,
            filesWritten: [changedPath],
            "docs check");
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService);

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = [changedPath],
                    LinesChanged = 2,
                },
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
                Telemetry = new ExecutionTelemetry
                {
                    DurationSeconds = 5,
                    ObservedPaths = [changedPath],
                    ChangeKinds = [ExecutionChangeKind.Documentation],
                    BudgetExceeded = false,
                    Summary = "Changed path escaped the declared host scope.",
                },
            });

        var outcome = service.Ingest(task.TaskId);
        var stored = taskGraphService.GetTask(task.TaskId);
        var resourceLeaseSnapshot = new ResourceLeaseService(workspace.Paths).LoadSnapshot();
        var resourceLease = Assert.Single(resourceLeaseSnapshot.Leases);

        Assert.True(outcome.BoundaryStopped);
        Assert.Equal(nameof(ExecutionBoundaryStopReason.ScopeViolation), outcome.BoundaryReason);
        Assert.Equal(DomainTaskStatus.Review, outcome.TaskStatus);
        Assert.Equal(DomainTaskStatus.Review, stored.Status);
        Assert.Equal(ResourceLeaseStatus.Stopped, resourceLease.Status);
        Assert.Contains(ResourceLeaseService.ActualWriteSetEscalationStopReason, resourceLease.StopReasons);
        Assert.Contains(resourceLease.ConflictReasons, reason => reason.Contains("docs/out-of-scope.md", StringComparison.Ordinal));
    }

    [Fact]
    public void RunToReviewSubmission_PrepareOnly_WritesPendingSidecarWithoutFinalSubmissionOrCertificate()
    {
        using var workspace = new TemporaryWorkspace();
        const string changedPath = "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.Failures.cs";
        var task = CreateTask("T-CARD-168-REVIEW-PREPARE");
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteWorkerExecutionArtifact(
            workspace,
            artifactRepository,
            task.TaskId,
            run.RunId,
            worktreePath: null,
            filesWritten: [changedPath],
            "dotnet build");
        var envelope = new ResultEnvelope
        {
            TaskId = task.TaskId,
            ExecutionRunId = run.RunId,
            ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
            Status = "success",
            Changes = new ResultEnvelopeChanges
            {
                FilesModified = [changedPath],
                LinesChanged = 3,
            },
            Validation = new ResultEnvelopeValidation
            {
                CommandsRun = ["dotnet build"],
                Build = "success",
                Tests = "not_run",
            },
            Result = new ResultEnvelopeOutcome
            {
                StopReason = "acceptance_satisfied",
            },
        };
        var service = new RunToReviewSubmissionService(workspace.Paths);
        var attempt = service.TryCreate(
            task,
            run,
            envelope,
            artifactRepository.TryLoadWorkerExecutionArtifact(task.TaskId)!,
            new BoundaryDecision
            {
                TaskId = task.TaskId,
                RunId = run.RunId,
                WritebackDecision = BoundaryWritebackDecision.AdmitToReview,
                DecisionConfidence = 0.8,
                ReviewerRequired = true,
                Summary = "review required",
                EvidenceStatus = "complete",
                SafetyStatus = "allow",
                TestStatus = "not_run",
                ReasonCodes = ["review_required"],
            },
            new ExecutionBoundaryArtifactSet("", "", null, null, null));

        Assert.True(attempt.CanProceed);
        Assert.True(attempt.Created);
        Assert.False(attempt.Committed);
        Assert.False(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.pending.json")));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "state-transition-certificate.json")));
    }

    [Fact]
    public void Ingest_ReviewSubmissionLeaseConflict_KeepsPendingSidecarWithoutFinalSubmissionOrCertificate()
    {
        using var workspace = new TemporaryWorkspace();
        const string changedPath = "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.Failures.cs";
        var task = CreateTask("T-CARD-168-REVIEW-LEASE-CONFLICT");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteWorkerExecutionArtifact(
            workspace,
            artifactRepository,
            task.TaskId,
            run.RunId,
            worktreePath: null,
            filesWritten: [changedPath],
            "dotnet build");
        var pendingRelativePath = $"ai/artifacts/worker-executions/{run.RunId}/review-submission.pending.json";
        var resourceLeaseService = new ResourceLeaseService(workspace.Paths);
        var now = DateTimeOffset.UtcNow;
        var blockingLease = resourceLeaseService.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = "WO-P8-REVIEW-LEASE-CONFLICT",
            TaskId = "TASK-P8-REVIEW-LEASE-CONFLICT",
            DeclaredWriteSet = new ResourceWriteSet
            {
                Paths = [pendingRelativePath],
            },
            Now = now,
            ValidUntil = now.AddHours(1),
        });
        Assert.True(blockingLease.Acquired);

        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService);

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = [changedPath],
                    LinesChanged = 3,
                },
                Validation = new ResultEnvelopeValidation
                {
                    CommandsRun = ["dotnet build"],
                    Build = "success",
                    Tests = "not_run",
                },
                Result = new ResultEnvelopeOutcome
                {
                    StopReason = "acceptance_satisfied",
                },
                Telemetry = new ExecutionTelemetry
                {
                    DurationSeconds = 30,
                    ObservedPaths = [changedPath],
                    ChangeKinds = [ExecutionChangeKind.SourceCode],
                    BudgetExceeded = false,
                    Summary = "Prepared review submission before lease gate conflict.",
                },
            });

        var exception = Assert.Throws<InvalidOperationException>(() => service.Ingest(task.TaskId));
        var stored = taskGraphService.GetTask(task.TaskId);
        var snapshot = resourceLeaseService.LoadSnapshot();

        Assert.Contains("Resource lease rejected task truth writeback", exception.Message, StringComparison.Ordinal);
        Assert.Equal(DomainTaskStatus.Pending, stored.Status);
        Assert.False(stored.Metadata.ContainsKey("review_submission_sidecar_path"));
        Assert.False(stored.Metadata.ContainsKey("review_submission_state_transition_certificate_path"));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.pending.json")));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.json")));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "state-transition-certificate.json")));
        Assert.False(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "effect-ledger.jsonl")));
        Assert.Equal(2, snapshot.Leases.Count);
        Assert.Contains(snapshot.Leases, lease => lease.LeaseId == blockingLease.Lease.LeaseId && lease.Status == ResourceLeaseStatus.Active);
        Assert.Contains(snapshot.Leases, lease =>
            string.Equals(lease.WorkOrderId, $"result-ingestion:{run.RunId}", StringComparison.Ordinal)
            && lease.Status == ResourceLeaseStatus.Stopped
            && lease.StopReasons.Contains(ResourceLeaseService.ConflictStopReason, StringComparer.Ordinal)
            && lease.ConflictReasons.Any(reason => reason.Contains(pendingRelativePath, StringComparison.Ordinal)));
    }

    [Fact]
    public void Ingest_DirectTaskWritebackRequiringPostWritebackEvidence_RoutesToReview()
    {
        using var workspace = new TemporaryWorkspace();
        const string changedPath = "docs/direct-writeback-post-writeback-evidence.md";
        var task = CreateTask(
            "T-CARD-168-DIRECT-POST-WRITEBACK",
            TaskType.Meta,
            ["docs"],
            CreateDirectWritebackAcceptanceContract(
                "T-CARD-168-DIRECT-POST-WRITEBACK",
                new AcceptanceContractEvidenceRequirement { Type = "validation_passed" },
                new AcceptanceContractEvidenceRequirement { Type = "result_commit", Description = "Capture detached result commit." }));
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteWorkerExecutionArtifact(
            workspace,
            artifactRepository,
            task.TaskId,
            run.RunId,
            worktreePath: null,
            filesWritten: [changedPath],
            "docs check");
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService);

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = [changedPath],
                    LinesChanged = 2,
                },
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
                Telemetry = new ExecutionTelemetry
                {
                    DurationSeconds = 5,
                    ObservedPaths = [changedPath],
                    ChangeKinds = [ExecutionChangeKind.Documentation],
                    BudgetExceeded = false,
                    Summary = "Within declared execution budget.",
                },
            });

        var outcome = service.Ingest(task.TaskId);
        var stored = taskGraphService.GetTask(task.TaskId);

        Assert.Equal(DomainTaskStatus.Review, outcome.TaskStatus);
        Assert.Equal(DomainTaskStatus.Review, stored.Status);
        Assert.Equal("admit_to_review", stored.Metadata["execution_boundary_writeback_decision"]);
        Assert.Contains("acceptance_evidence_post_writeback_only", stored.Metadata["execution_boundary_reason_codes"], StringComparison.Ordinal);
    }

    [Fact]
    public void Ingest_ResultAttemptingPlannerLifecycleAction_IsRejectedBeforeWriteback()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-302-INGEST-001");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteExecutionPacket(workspace, task.TaskId);
        WriteWorkerExecutionArtifact(workspace, artifactRepository, task.TaskId, run.RunId, "dotnet build");
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService);

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = ["src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.Failures.cs"],
                    LinesChanged = 12,
                },
                Validation = new ResultEnvelopeValidation
                {
                    CommandsRun = ["dotnet build"],
                    Build = "success",
                    Tests = "not_run",
                },
                Next = new ResultEnvelopeNextAction
                {
                    Suggested = "run review-task and sync-state now",
                },
            });

        var outcome = service.Ingest(task.TaskId);
        var stored = taskGraphService.GetTask(task.TaskId);

        Assert.Equal(DomainTaskStatus.Pending, outcome.TaskStatus);
        Assert.Equal(DomainTaskStatus.Pending, stored.Status);
        Assert.Equal("reject_result", stored.Metadata["execution_boundary_writeback_decision"]);
        Assert.Equal("reject", stored.Metadata["execution_packet_enforcement_verdict"]);
        Assert.Equal("true", stored.Metadata["execution_packet_planner_only_action_attempted"]);
        Assert.Equal(ExecutionRunStatus.Failed, executionRunService.Get(run.RunId).Status);
        Assert.True(File.Exists(Path.Combine(workspace.Paths.RuntimeRoot, "packet-enforcement", $"{task.TaskId}.json")));
    }

    [Fact]
    public void Ingest_SuccessResultBeyondBudget_StopsBeforeFailureFlow()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-172-001");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteWorkerExecutionArtifact(workspace, artifactRepository, task.TaskId, run.RunId, "dotnet build");
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService);

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified =
                    [
                        "src/CARVES.Runtime.Application/ControlPlane/A.cs",
                        "src/CARVES.Runtime.Application/ControlPlane/B.cs",
                        "src/CARVES.Runtime.Application/ControlPlane/C.cs",
                        "src/CARVES.Runtime.Application/ControlPlane/D.cs",
                    ],
                    LinesChanged = 240,
                },
                Validation = new ResultEnvelopeValidation
                {
                    CommandsRun = ["dotnet build"],
                    Build = "success",
                    Tests = "not_run",
                },
                Result = new ResultEnvelopeOutcome
                {
                    StopReason = "acceptance_satisfied",
                },
                Telemetry = new ExecutionTelemetry
                {
                    DurationSeconds = 600,
                    ObservedPaths =
                    [
                        "src/CARVES.Runtime.Application/ControlPlane/A.cs",
                        "src/CARVES.Runtime.Application/ControlPlane/B.cs",
                        "src/CARVES.Runtime.Application/ControlPlane/C.cs",
                        "src/CARVES.Runtime.Application/ControlPlane/D.cs",
                    ],
                    ChangeKinds = [ExecutionChangeKind.SourceCode],
                    BudgetExceeded = true,
                    Summary = "Exceeded the declared execution budget.",
                },
            });

        var outcome = service.Ingest(task.TaskId);
        var stored = taskGraphService.GetTask(task.TaskId);

        Assert.Equal(DomainTaskStatus.Review, outcome.TaskStatus);
        Assert.Equal(DomainTaskStatus.Review, stored.Status);
        Assert.True(outcome.BoundaryStopped);
        Assert.Equal(nameof(ExecutionBoundaryStopReason.SizeExceeded), outcome.BoundaryReason);
        Assert.Equal("true", stored.Metadata["boundary_stopped"]);
        Assert.Equal("stop", stored.Metadata["execution_boundary_decision"]);
        Assert.Equal(nameof(ExecutionBoundaryReplanStrategy.SplitTask), stored.Metadata["boundary_replan_strategy"]);
        Assert.Equal($".ai/artifacts/worker-executions/{run.RunId}/state-transition-certificate.json", stored.Metadata["task_truth_transition_certificate_path"]);
        Assert.Contains("task_status_to_review", stored.Metadata["task_truth_transition_certified_transitions"], StringComparison.Ordinal);
        Assert.Equal($"RUN-{task.TaskId}-002", stored.Metadata["execution_run_latest_id"]);
        Assert.Equal($"RUN-{task.TaskId}-002", stored.Metadata["execution_run_active_id"]);
        Assert.Equal("HealthyProgress", stored.Metadata["execution_pattern_type"]);
        Assert.Equal("false", stored.Metadata["execution_pattern_warning"]);
        Assert.Empty(failureRepository.LoadAll());
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, stored.Metadata["task_truth_transition_certificate_path"].Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "boundary", "violations", $"{task.TaskId}.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "boundary", "replans", $"{task.TaskId}.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "run-reports", task.TaskId, $"{run.RunId}.json")));
        Assert.Equal(ExecutionRunStatus.Stopped, executionRunService.Get(run.RunId).Status);
        Assert.Equal(ExecutionRunStatus.Planned, executionRunService.Get($"RUN-{task.TaskId}-002").Status);
    }

    [Fact]
    public void Ingest_ManagedWorkspaceScopeEscape_StopsAndMarksReplan()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-697-RESULT-001");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        WriteWorkerExecutionArtifact(workspace, artifactRepository, task.TaskId, run.RunId, "dotnet build");
        var leaseRepository = new InMemoryManagedWorkspaceLeaseRepository();
        leaseRepository.Save(new ManagedWorkspaceLeaseSnapshot
        {
            Leases =
            [
                new ManagedWorkspaceLease
                {
                    LeaseId = "lease-ingest-001",
                    TaskId = task.TaskId,
                    WorkspacePath = Path.Combine(workspace.RootPath, ".carves-worktrees", task.TaskId),
                    RepoRoot = workspace.RootPath,
                    Status = ManagedWorkspaceLeaseStatus.Active,
                    AllowedWritablePaths = ["src/CARVES.Runtime.Application/ControlPlane/ResultIngestionService.cs"],
                },
            ],
        });
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService,
            executionBoundaryService: new ExecutionBoundaryService(
                new ExecutionBudgetFactory(new ExecutionPathClassifier()),
                new ExecutionPathClassifier(),
                new ManagedWorkspacePathPolicyService(workspace.RootPath, leaseRepository)));

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = ["docs/ScopeEscape.md"],
                    LinesChanged = 10,
                },
                Validation = new ResultEnvelopeValidation
                {
                    CommandsRun = ["dotnet build"],
                    Build = "success",
                    Tests = "not_run",
                },
                Result = new ResultEnvelopeOutcome
                {
                    StopReason = "acceptance_satisfied",
                },
            });

        var outcome = service.Ingest(task.TaskId);
        var stored = taskGraphService.GetTask(task.TaskId);

        Assert.True(outcome.BoundaryStopped);
        Assert.Equal(nameof(ExecutionBoundaryStopReason.ScopeViolation), outcome.BoundaryReason);
        Assert.Equal("scope_escape", stored.Metadata["managed_workspace_path_policy_status"]);
        Assert.Equal("true", stored.Metadata["planner_required"]);
        Assert.Contains("scope escape", stored.Metadata["managed_workspace_path_policy_summary"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scope escape", stored.PlannerReview.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ingest_ThrowsWhenExecutionEvidenceIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-250-001");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService);

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                Status = "success",
                Result = new ResultEnvelopeOutcome
                {
                    StopReason = "acceptance_satisfied",
                },
            });

        var exception = Assert.Throws<InvalidOperationException>(() => service.Ingest(task.TaskId));

        Assert.Contains("without worker execution evidence", exception.Message, StringComparison.OrdinalIgnoreCase);
        var stored = taskGraphService.GetTask(task.TaskId);
        Assert.Equal("reject_result", stored.Metadata["execution_boundary_writeback_decision"]);
        Assert.False(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "boundary", "decisions", $"{task.TaskId}.json")));
    }

    [Fact]
    public void Ingest_SuccessResultMissingPatchArtifact_IsRejectedBeforeWriteback()
    {
        using var workspace = new TemporaryWorkspace();
        var task = CreateTask("T-CARD-679-RESULT-001");
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph([task])), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        WriteWorkerExecutionArtifact(workspace, artifactRepository, task.TaskId, run.RunId, "dotnet build", "dotnet test");
        File.Delete(Path.Combine(workspace.Paths.AiRoot, "artifacts", "worker-executions", run.RunId, "patch.diff"));
        var service = new ResultIngestionService(
            workspace.Paths,
            taskGraphService,
            new FailureReportService(
                workspace.RootPath,
                failureRepository,
                new FailureClassificationService(),
                artifactRepository),
            new PlannerTriggerService(new FailureContextService(failureRepository)),
            new NoOpMarkdownSyncService(),
            () => null,
            artifactRepository,
            executionRunService: executionRunService);

        WriteResultEnvelope(
            workspace,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = run.RunId,
                ExecutionEvidencePath = $".ai/artifacts/worker-executions/{run.RunId}/evidence.json",
                Status = "success",
                Changes = new ResultEnvelopeChanges
                {
                    FilesModified = ["src/CARVES.Runtime.Application/ControlPlane/ResultIngestionService.cs"],
                    LinesChanged = 14,
                },
                Validation = new ResultEnvelopeValidation
                {
                    CommandsRun = ["dotnet build", "dotnet test"],
                    Build = "success",
                    Tests = "success",
                },
                Result = new ResultEnvelopeOutcome
                {
                    StopReason = "acceptance_satisfied",
                },
            });

        var exception = Assert.Throws<InvalidOperationException>(() => service.Ingest(task.TaskId));
        var stored = taskGraphService.GetTask(task.TaskId);

        Assert.Contains("missing the reviewable patch artifact", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DomainTaskStatus.Pending, stored.Status);
        Assert.Equal("reject_result", stored.Metadata["execution_boundary_writeback_decision"]);
        Assert.Contains("patch_artifact_missing", stored.Metadata["execution_boundary_reason_codes"], StringComparison.Ordinal);
        Assert.Equal("reject_result", stored.Metadata["execution_boundary_writeback_decision"]);
        Assert.False(File.Exists(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.json")));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "boundary", "decisions", $"{task.TaskId}.json")));
    }

    private static TaskNode CreateTask(
        string taskId,
        TaskType taskType = TaskType.Execution,
        IReadOnlyList<string>? scope = null,
        AcceptanceContract? acceptanceContract = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new TaskNode
        {
            TaskId = taskId,
            CardId = "CARD-168",
            Title = "Write result envelope",
            Description = "Persist worker result envelope.",
            Status = DomainTaskStatus.Pending,
            TaskType = taskType,
            Priority = "P1",
            Scope = scope ?? ["src/CARVES.Runtime.Application/ControlPlane/"],
            Acceptance = ["result envelope is ingested"],
            AcceptanceContract = acceptanceContract,
            Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal),
        };
    }

    private static AcceptanceContract CreateDirectWritebackAcceptanceContract(
        string taskId,
        params AcceptanceContractEvidenceRequirement[] evidenceRequired)
    {
        return new AcceptanceContract
        {
            ContractId = $"AC-{taskId}",
            Title = $"Direct writeback acceptance for {taskId}",
            Status = AcceptanceContractLifecycleStatus.Compiled,
            AutoCompleteAllowed = true,
            HumanReview = new AcceptanceContractHumanReviewPolicy
            {
                Required = false,
            },
            EvidenceRequired = evidenceRequired.Length == 0
                ? [
                    new AcceptanceContractEvidenceRequirement { Type = "validation_passed" },
                    new AcceptanceContractEvidenceRequirement { Type = "command_log" },
                    new AcceptanceContractEvidenceRequirement { Type = "files_written" },
                ]
                : evidenceRequired,
            Traceability = new AcceptanceContractTraceability
            {
                SourceTaskId = taskId,
            },
        };
    }

    private static void WriteResultEnvelope(TemporaryWorkspace workspace, ResultEnvelope envelope)
    {
        workspace.WriteFile($".ai/execution/{envelope.TaskId}/result.json", JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static void WriteWorkerExecutionArtifact(
        TemporaryWorkspace workspace,
        JsonRuntimeArtifactRepository artifactRepository,
        string taskId,
        string runId,
        params string[] commands)
    {
        WriteWorkerExecutionArtifact(
            workspace,
            artifactRepository,
            taskId,
            runId,
            worktreePath: null,
            filesWritten: ["src/CARVES.Runtime.Application/ControlPlane/ResultIngestionService.cs"],
            commands);
    }

    private static void WriteWorkerExecutionArtifact(
        TemporaryWorkspace workspace,
        JsonRuntimeArtifactRepository artifactRepository,
        string taskId,
        string runId,
        string? worktreePath,
        IReadOnlyList<string> filesWritten,
        params string[] commands)
    {
        var evidencePath = $".ai/artifacts/worker-executions/{runId}/evidence.json";
        var commandLogPath = $".ai/artifacts/worker-executions/{runId}/command.log";
        var buildLogPath = $".ai/artifacts/worker-executions/{runId}/build.log";
        var testLogPath = $".ai/artifacts/worker-executions/{runId}/test.log";
        var patchPath = $".ai/artifacts/worker-executions/{runId}/patch.diff";
        workspace.WriteFile(evidencePath, "{\"taskId\":\"" + taskId + "\"}");
        workspace.WriteFile(commandLogPath, string.Join(Environment.NewLine, commands));
        workspace.WriteFile(buildLogPath, "Build succeeded.");
        workspace.WriteFile(testLogPath, "Test run completed.");
        workspace.WriteFile(patchPath, "diff --git a/src/example.cs b/src/example.cs\n--- a/src/example.cs\n+++ b/src/example.cs\n@@\n+ change");
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = taskId,
            Result = new WorkerExecutionResult
            {
                TaskId = taskId,
                RunId = runId,
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.TestFailure,
                FailureLayer = WorkerFailureLayer.WorkerSemantic,
            },
            Evidence = new ExecutionEvidence
            {
                TaskId = taskId,
                RunId = runId,
                WorkerId = "CodexCliWorkerAdapter",
                EvidenceSource = ExecutionEvidenceSource.Host,
                CommandsExecuted = commands,
                FilesWritten = filesWritten,
                WorktreePath = worktreePath,
                EvidencePath = evidencePath,
                CommandLogRef = commandLogPath,
                BuildOutputRef = buildLogPath,
                TestOutputRef = testLogPath,
                PatchRef = patchPath,
                EvidenceCompleteness = ExecutionEvidenceCompleteness.Complete,
                EvidenceStrength = ExecutionEvidenceStrength.Replayable,
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                EndedAt = DateTimeOffset.UtcNow,
                ExitStatus = 1,
            },
        });
        artifactRepository.SaveSafetyArtifact(new SafetyArtifact
        {
            Decision = SafetyDecision.Allow(taskId),
        });
    }

    private sealed class ResultCommitGitClient : StubGitClient
    {
        private readonly string worktreePath;

        public ResultCommitGitClient(string worktreePath)
        {
            this.worktreePath = worktreePath;
        }

        public IReadOnlyList<string> CapturedPaths { get; private set; } = Array.Empty<string>();

        public override bool IsRepository(string repoRoot)
        {
            return string.Equals(repoRoot, worktreePath, StringComparison.Ordinal);
        }

        public override string? TryCreateScopedSnapshotCommit(string repoRoot, IReadOnlyList<string> paths, string message)
        {
            CapturedPaths = paths.ToArray();
            return string.Equals(repoRoot, worktreePath, StringComparison.Ordinal)
                ? "p4-result-commit"
                : null;
        }
    }

    private sealed class RejectingStateTransitionCertificateService : IStateTransitionCertificateService
    {
        private readonly ControlPlanePaths paths;

        public RejectingStateTransitionCertificateService(ControlPlanePaths paths)
        {
            this.paths = paths;
        }

        public string GetRunCertificatePath(string runId)
        {
            return Path.Combine(paths.WorkerExecutionArtifactsRoot, runId, "state-transition-certificate.json");
        }

        public StateTransitionCertificateIssueResult TryIssue(StateTransitionCertificateIssueRequest request)
        {
            return StateTransitionCertificateIssueResult.Block(
                [StateTransitionCertificateService.MissingCertificateStopReason],
                "State transition certificate deliberately unavailable for regression test.");
        }

        public StateTransitionCertificateIssueResult RebindCommittedEffect(StateTransitionCertificateRebindRequest request)
        {
            return StateTransitionCertificateIssueResult.Block(
                [StateTransitionCertificateService.MissingCertificateStopReason],
                "State transition certificate deliberately unavailable for regression test.");
        }

        public StateTransitionCertificateVerificationResult VerifyRequired(
            string? certificatePath,
            IReadOnlyList<string> requiredOperations)
        {
            return StateTransitionCertificateVerificationResult.Block(
                [StateTransitionCertificateService.MissingCertificateStopReason],
                "State transition certificate deliberately unavailable for regression test.");
        }

        public StateTransitionCertificateVerificationResult VerifyRequired(StateTransitionCertificateVerificationRequest request)
        {
            return StateTransitionCertificateVerificationResult.Block(
                [StateTransitionCertificateService.MissingCertificateStopReason],
                "State transition certificate deliberately unavailable for regression test.");
        }

        public StateTransitionCertificateEvidence BuildEvidence(string kind, string path, bool required = true)
        {
            return new StateTransitionCertificateEvidence
            {
                Kind = kind,
                Path = path.Replace('\\', '/'),
                Hash = "sha256:test",
                Required = required,
            };
        }
    }

    private sealed class FailOnNthUpsertTaskGraphRepository : ITaskGraphRepository
    {
        private readonly int failOnUpsertCount;
        private DomainTaskGraph graph;
        private int upsertCount;

        public FailOnNthUpsertTaskGraphRepository(DomainTaskGraph graph, int failOnUpsertCount)
        {
            this.graph = graph;
            this.failOnUpsertCount = failOnUpsertCount;
        }

        public DomainTaskGraph Load()
        {
            return graph;
        }

        public void Save(DomainTaskGraph graph)
        {
            this.graph = graph;
        }

        public void Upsert(TaskNode task)
        {
            upsertCount += 1;
            if (upsertCount == failOnUpsertCount)
            {
                throw new InvalidOperationException("Simulated task graph replace failure.");
            }

            graph.AddOrReplace(task);
        }

        public void UpsertRange(IEnumerable<TaskNode> tasks)
        {
            foreach (var task in tasks)
            {
                Upsert(task);
            }
        }

        public T WithWriteLock<T>(Func<T> action)
        {
            return action();
        }
    }

    private static void WriteExecutionPacket(TemporaryWorkspace workspace, string taskId)
    {
        var packet = new ExecutionPacket
        {
            PacketId = $"EP-{taskId}-v1",
            TaskRef = new ExecutionPacketTaskRef
            {
                CardId = "CARD-302",
                TaskId = taskId,
                TaskRevision = 1,
            },
            Goal = "Packet enforcement fixture.",
            PlannerIntent = Carves.Runtime.Domain.Planning.PlannerIntent.Execution,
            Permissions = new ExecutionPacketPermissions
            {
                EditableRoots = ["src/"],
                ReadOnlyRoots = ["docs/"],
                TruthRoots = ["carves://truth/tasks", "carves://truth/runtime"],
                RepoMirrorRoots = [".ai/"],
            },
            WorkerAllowedActions = ["read", "edit", "build", "test", "carves.submit_result", "carves.request_replan"],
            PlannerOnlyActions = ["carves.review_task", "carves.sync_state"],
        };

        workspace.WriteFile($".ai/runtime/execution-packets/{taskId}.json", JsonSerializer.Serialize(packet, JsonOptions));
    }
}
