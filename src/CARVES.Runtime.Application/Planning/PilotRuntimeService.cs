using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public sealed class PilotRuntimeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly PlanningDraftService planningDraftService;
    private readonly ExecutionRunService executionRunService;
    private readonly IRuntimeArtifactRepository artifactRepository;

    public PilotRuntimeService(
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        PlanningDraftService planningDraftService,
        ExecutionRunService executionRunService,
        IRuntimeArtifactRepository artifactRepository)
    {
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.planningDraftService = planningDraftService;
        this.executionRunService = executionRunService;
        this.artifactRepository = artifactRepository;
    }

    public AttachToTaskProofRecord CaptureAttachToTaskProof(string taskId)
    {
        var task = taskGraphService.GetTask(taskId);
        var plannerEmergence = new PlannerEmergenceService(paths, taskGraphService, executionRunService);
        var latestRun = executionRunService.ListRuns(taskId).OrderBy(run => run.CreatedAtUtc).LastOrDefault();
        var latestMemory = plannerEmergence.ListExecutionMemory(taskId, take: 10);
        var latestReplan = plannerEmergence.TryGetLatestReplan(taskId);
        var suggestions = plannerEmergence.ListSuggestedTasks(taskId);
        var cardDraft = string.IsNullOrWhiteSpace(task.CardId) ? null : planningDraftService.TryGetCardDraft(task.CardId);
        var runtimeManifestPath = Path.Combine(paths.AiRoot, "runtime.json");
        var attachHandshakePath = Path.Combine(paths.RuntimeRoot, "attach-handshake.json");
        var resultPath = Path.Combine(paths.AiRoot, "execution", taskId, "result.json");
        var reviewPath = Path.Combine(paths.ReviewArtifactsRoot, $"{taskId}.json");
        var proofId = $"PROOF-{taskId}";
        var evidencePaths = new List<string>();

        if (File.Exists(runtimeManifestPath))
        {
            evidencePaths.Add(runtimeManifestPath);
        }

        if (File.Exists(attachHandshakePath))
        {
            evidencePaths.Add(attachHandshakePath);
        }

        if (File.Exists(resultPath))
        {
            evidencePaths.Add(resultPath);
        }

        if (File.Exists(reviewPath))
        {
            evidencePaths.Add(reviewPath);
        }

        if (latestRun is not null)
        {
            var runPath = GetRunPath(taskId, latestRun.RunId);
            if (File.Exists(runPath))
            {
                evidencePaths.Add(runPath);
            }
        }

        if (latestReplan is not null)
        {
            var replanPath = Path.Combine(paths.RuntimeRoot, "planning", "replans", taskId, $"{latestReplan.EntryId}.json");
            if (File.Exists(replanPath))
            {
                evidencePaths.Add(replanPath);
            }
        }

        var notes = new List<string>();
        var acceptanceContractGate = AcceptanceContractExecutionGate.Evaluate(task);
        if (!File.Exists(attachHandshakePath))
        {
            notes.Add("No attach handshake artifact is present for this repo.");
        }

        if (latestRun is null)
        {
            notes.Add("No execution run exists for the requested task.");
        }

        if (acceptanceContractGate.BlocksExecution)
        {
            notes.Add(acceptanceContractGate.Summary);
        }

        var (repoId, hostSessionId) = ReadRuntimeManifest(runtimeManifestPath);
        var resultStatus = TryReadResultStatus(resultPath);
        var reviewVerdict = task.PlannerReview.Verdict.ToString();

        var proof = new AttachToTaskProofRecord
        {
            ProofId = proofId,
            TaskId = task.TaskId,
            CardId = task.CardId,
            RepoRoot = paths.RepoRoot,
            RepoId = repoId,
            HostSessionId = hostSessionId,
            RuntimeManifestPresent = File.Exists(runtimeManifestPath),
            AttachHandshakePresent = File.Exists(attachHandshakePath),
            CardLifecycleState = cardDraft?.Status.ToString().ToLowerInvariant() ?? "approved",
            TaskStatus = task.Status.ToString(),
            DispatchState = task.Status switch
            {
                Carves.Runtime.Domain.Tasks.TaskStatus.Pending when !acceptanceContractGate.BlocksExecution => "dispatchable",
                Carves.Runtime.Domain.Tasks.TaskStatus.Pending => "dispatch_blocked",
                _ => "dispatched",
            },
            ExecutionRunId = latestRun?.RunId,
            ExecutionRunStatus = latestRun?.Status.ToString(),
            ResultStatus = resultStatus,
            ReviewVerdict = reviewVerdict,
            ReplanEntryId = latestReplan?.EntryId,
            SuggestedTaskIds = suggestions.Select(item => item.SuggestionId).ToArray(),
            ExecutionMemoryIds = latestMemory.Select(item => item.MemoryId).ToArray(),
            EvidencePaths = evidencePaths.Distinct(StringComparer.Ordinal).ToArray(),
            Notes = notes,
            CapturedAtUtc = DateTimeOffset.UtcNow,
        };

        WriteJson(GetAttachProofPath(task.TaskId), proof);
        return proof;
    }

    public AttachToTaskProofRecord? TryGetAttachToTaskProof(string taskId)
    {
        var path = GetAttachProofPath(taskId);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<AttachToTaskProofRecord>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    public PilotEvidenceRecord RecordPilotEvidence(string jsonPath)
    {
        var submission = JsonSerializer.Deserialize<PilotEvidenceSubmission>(File.ReadAllText(jsonPath), JsonOptions)
            ?? throw new InvalidOperationException($"Pilot evidence payload '{jsonPath}' could not be parsed.");
        var record = CreatePilotEvidenceRecord(submission);
        WriteJson(GetPilotEvidencePath(record.EvidenceId), record);
        return record;
    }

    public PilotProblemIntakeRecord RecordPilotProblemIntake(string jsonPath)
    {
        var submission = JsonSerializer.Deserialize<PilotProblemIntakeSubmission>(File.ReadAllText(jsonPath), JsonOptions)
            ?? throw new InvalidOperationException($"Pilot problem intake payload '{jsonPath}' could not be parsed.");
        if (string.IsNullOrWhiteSpace(submission.Summary))
        {
            throw new InvalidOperationException("Pilot problem intake field 'summary' is required.");
        }

        if (string.IsNullOrWhiteSpace(submission.ProblemKind))
        {
            throw new InvalidOperationException("Pilot problem intake field 'problem_kind' is required.");
        }

        if (!IsAcceptedProblemKind(submission.ProblemKind))
        {
            throw new InvalidOperationException($"Pilot problem intake field 'problem_kind' value '{submission.ProblemKind}' is not accepted.");
        }

        var evidence = CreatePilotEvidenceRecord(BuildProblemEvidenceSubmission(submission));
        WriteJson(GetPilotEvidencePath(evidence.EvidenceId), evidence);

        var problemId = $"PROBLEM-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..36];
        var record = new PilotProblemIntakeRecord
        {
            ProblemId = problemId,
            EvidenceId = evidence.EvidenceId,
            RepoRoot = paths.RepoRoot,
            RepoId = evidence.RepoId ?? submission.RepoId,
            TaskId = submission.TaskId,
            CardId = submission.CardId ?? evidence.CardId,
            CurrentStageId = NormalizeScalar(submission.CurrentStageId),
            NextGovernedCommand = NormalizeScalar(submission.NextGovernedCommand),
            ProblemKind = NormalizeScalar(submission.ProblemKind),
            Severity = string.IsNullOrWhiteSpace(submission.Severity) ? "blocking" : submission.Severity.Trim(),
            Summary = submission.Summary.Trim(),
            BlockedCommand = NormalizeScalar(submission.BlockedCommand),
            CommandExitCode = submission.CommandExitCode,
            CommandOutput = NormalizeScalar(submission.CommandOutput),
            StopTrigger = NormalizeScalar(submission.StopTrigger),
            Observations = Normalize(submission.Observations),
            AffectedPaths = Normalize(submission.AffectedPaths).Select(NormalizePath).OfType<string>().ToArray(),
            RecommendedFollowUp = NormalizeScalar(submission.RecommendedFollowUp),
            Status = "recorded",
            RecordedAtUtc = evidence.RecordedAtUtc,
        };

        WriteJson(GetPilotProblemPath(record.ProblemId), record);
        return record;
    }

    public IReadOnlyList<PilotProblemIntakeRecord> ListPilotProblemIntake(string? repoId = null)
    {
        var root = GetPilotProblemRoot();
        if (!Directory.Exists(root))
        {
            return Array.Empty<PilotProblemIntakeRecord>();
        }

        return Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<PilotProblemIntakeRecord>(File.ReadAllText(path), JsonOptions))
            .OfType<PilotProblemIntakeRecord>()
            .Where(record => string.IsNullOrWhiteSpace(repoId) || string.Equals(record.RepoId, repoId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(record => record.RecordedAtUtc)
            .ThenByDescending(record => record.ProblemId, StringComparer.Ordinal)
            .ToArray();
    }

    public PilotProblemIntakeRecord? TryGetPilotProblemIntake(string problemId)
    {
        var path = GetPilotProblemPath(problemId);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<PilotProblemIntakeRecord>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    public IReadOnlyList<PilotEvidenceRecord> ListPilotEvidence(string? repoId = null)
    {
        var root = GetPilotEvidenceRoot();
        if (!Directory.Exists(root))
        {
            return Array.Empty<PilotEvidenceRecord>();
        }

        return Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<PilotEvidenceRecord>(File.ReadAllText(path), JsonOptions))
            .OfType<PilotEvidenceRecord>()
            .Where(record => string.IsNullOrWhiteSpace(repoId) || string.Equals(record.RepoId, repoId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(record => record.RecordedAtUtc)
            .ThenByDescending(record => record.EvidenceId, StringComparer.Ordinal)
            .ToArray();
    }

    public PilotEvidenceRecord? TryGetPilotEvidence(string evidenceId)
    {
        var path = GetPilotEvidencePath(evidenceId);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<PilotEvidenceRecord>(File.ReadAllText(path), JsonOptions)
            : null;
    }

    public PilotCloseLoopRecord CloseLoop(PilotCloseLoopRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TaskId))
        {
            throw new InvalidOperationException("Pilot close-loop requires a task id.");
        }

        var task = taskGraphService.GetTask(request.TaskId);
        var latestRun = executionRunService.ListRuns(task.TaskId)
            .OrderBy(run => run.CreatedAtUtc)
            .LastOrDefault()
            ?? throw new InvalidOperationException($"Task '{task.TaskId}' does not have an execution run to close.");

        var workerArtifactSeed = artifactRepository.TryLoadWorkerExecutionArtifact(task.TaskId);
        var changedFile = NormalizeChangedFile(request.ChangedFile, task.Scope);
        var summary = string.IsNullOrWhiteSpace(request.Summary)
            ? "CARVES.Runtime recorded a bounded failed proof to close the current pilot loop."
            : request.Summary.Trim();
        var failureMessage = string.IsNullOrWhiteSpace(request.FailureMessage)
            ? "CARVES.Runtime recorded a bounded failed proof for pilot close-loop."
            : request.FailureMessage.Trim();

        var proofPaths = BuildProofPaths(task.TaskId, latestRun.RunId);
        Directory.CreateDirectory(proofPaths.EvidenceDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(proofPaths.ResultEnvelopePath)!);

        var now = DateTimeOffset.UtcNow;
        var startedAt = now.AddSeconds(-30);
        var commands = new[] { "dotnet build", "dotnet test" };
        File.WriteAllText(proofPaths.CommandLogPath, string.Join(Environment.NewLine, commands));
        File.WriteAllText(proofPaths.BuildLogPath, "Build succeeded.");
        File.WriteAllText(proofPaths.TestLogPath, "Test run completed with a bounded pilot failure.");
        File.WriteAllText(
            proofPaths.PatchPath,
            $"diff --git a/{changedFile} b/{changedFile}{Environment.NewLine}--- a/{changedFile}{Environment.NewLine}+++ b/{changedFile}{Environment.NewLine}@@{Environment.NewLine}+// CARVES.Runtime pilot close-loop proof");

        var evidence = new ExecutionEvidence
        {
            RunId = latestRun.RunId,
            TaskId = task.TaskId,
            WorkerId = workerArtifactSeed?.Result.AdapterId ?? "PilotRuntimeService",
            StartedAt = startedAt,
            EndedAt = now,
            EvidenceSource = ExecutionEvidenceSource.Host,
            DeclaredScopeFiles = task.Scope,
            FilesRead = [changedFile],
            FilesWritten = [changedFile],
            CommandsExecuted = commands,
            RepoRoot = paths.RepoRoot,
            WorktreePath = workerArtifactSeed?.Evidence.WorktreePath,
            BaseCommit = workerArtifactSeed?.Evidence.BaseCommit,
            RequestedThreadId = workerArtifactSeed?.Evidence.RequestedThreadId,
            ThreadId = workerArtifactSeed?.Evidence.ThreadId,
            ThreadContinuity = workerArtifactSeed?.Evidence.ThreadContinuity ?? WorkerThreadContinuity.None,
            EvidencePath = ToRepoRelative(proofPaths.EvidencePath),
            BuildOutputRef = ToRepoRelative(proofPaths.BuildLogPath),
            TestOutputRef = ToRepoRelative(proofPaths.TestLogPath),
            CommandLogRef = ToRepoRelative(proofPaths.CommandLogPath),
            PatchRef = ToRepoRelative(proofPaths.PatchPath),
            Artifacts =
            [
                ToRepoRelative(proofPaths.EvidencePath),
                ToRepoRelative(proofPaths.CommandLogPath),
                ToRepoRelative(proofPaths.BuildLogPath),
                ToRepoRelative(proofPaths.TestLogPath),
                ToRepoRelative(proofPaths.PatchPath),
            ],
            ExitStatus = 1,
            EvidenceCompleteness = ExecutionEvidenceCompleteness.Complete,
            EvidenceStrength = ExecutionEvidenceStrength.Replayable,
        };

        WriteJson(proofPaths.EvidencePath, evidence);

        var seedResult = workerArtifactSeed?.Result;
        artifactRepository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            CapturedAt = now,
            TaskId = task.TaskId,
            Result = new WorkerExecutionResult
            {
                RunId = latestRun.RunId,
                TaskId = task.TaskId,
                BackendId = seedResult?.BackendId ?? "codex_cli",
                ProviderId = seedResult?.ProviderId ?? "codex",
                AdapterId = seedResult?.AdapterId ?? "PilotRuntimeService",
                AdapterReason = "pilot_close_loop",
                ProtocolFamily = seedResult?.ProtocolFamily,
                RequestFamily = seedResult?.RequestFamily,
                ProfileId = seedResult?.ProfileId ?? "workspace_build_test",
                TrustedProfile = seedResult?.TrustedProfile ?? false,
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.TestFailure,
                FailureLayer = WorkerFailureLayer.WorkerSemantic,
                Retryable = false,
                Configured = true,
                Model = string.IsNullOrWhiteSpace(seedResult?.Model) ? "pilot-close-loop" : seedResult.Model,
                RequestId = seedResult?.RequestId,
                PriorThreadId = seedResult?.PriorThreadId,
                ThreadId = seedResult?.ThreadId,
                ThreadContinuity = seedResult?.ThreadContinuity ?? WorkerThreadContinuity.None,
                RequestPreview = string.IsNullOrWhiteSpace(seedResult?.RequestPreview) ? task.Description : seedResult.RequestPreview,
                RequestHash = string.IsNullOrWhiteSpace(seedResult?.RequestHash) ? latestRun.RunId.ToLowerInvariant() : seedResult.RequestHash,
                Summary = summary,
                Rationale = seedResult?.Rationale,
                FailureReason = failureMessage,
                ResponsePreview = seedResult?.ResponsePreview,
                ResponseHash = seedResult?.ResponseHash,
                ChangedFiles = [changedFile],
                Events = Array.Empty<WorkerEvent>(),
                PermissionRequests = Array.Empty<WorkerPermissionRequest>(),
                CommandTrace = Array.Empty<CommandExecutionRecord>(),
                InputTokens = seedResult?.InputTokens,
                OutputTokens = seedResult?.OutputTokens,
                ProviderStatusCode = seedResult?.ProviderStatusCode,
                ProviderLatencyMs = seedResult?.ProviderLatencyMs,
                StartedAt = startedAt,
                CompletedAt = now,
            },
            Evidence = evidence,
        });

        var resultEnvelope = new ResultEnvelope
        {
            TaskId = task.TaskId,
            ExecutionRunId = latestRun.RunId,
            ExecutionEvidencePath = evidence.EvidencePath,
            Status = "failed",
            Changes = new ResultEnvelopeChanges
            {
                FilesModified = [changedFile],
                LinesChanged = 1,
            },
            Validation = new ResultEnvelopeValidation
            {
                CommandsRun = commands,
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
                Message = failureMessage,
            },
            Next = new ResultEnvelopeNextAction
            {
                Suggested = "Generate a focused repair follow-up task.",
            },
            Telemetry = new ExecutionTelemetry
            {
                FilesChanged = 1,
                LinesChanged = 1,
                RetryCount = 0,
                FailureCount = 1,
                FailureDensity = 1.0,
                DurationSeconds = 45,
                ObservedPaths = [changedFile],
                ChangeKinds = [ExecutionChangeKind.SourceCode],
                BudgetExceeded = false,
                Summary = "CARVES.Runtime pilot close-loop stayed within the declared execution budget.",
            },
        };

        WriteResultEnvelope(proofPaths.ResultEnvelopePath, resultEnvelope);
        return new PilotCloseLoopRecord
        {
            TaskId = task.TaskId,
            ExecutionRunId = latestRun.RunId,
            ChangedFile = changedFile,
            ResultEnvelopePath = proofPaths.ResultEnvelopePath,
            WorkerExecutionArtifactPath = Path.Combine(paths.WorkerExecutionArtifactsRoot, $"{task.TaskId}.json"),
            EvidenceDirectory = proofPaths.EvidenceDirectory,
            RecordedAtUtc = now,
        };
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private PilotEvidenceRecord CreatePilotEvidenceRecord(PilotEvidenceSubmission submission)
    {
        if (string.IsNullOrWhiteSpace(submission.Summary))
        {
            throw new InvalidOperationException("Pilot evidence field 'summary' is required.");
        }

        var evidenceId = $"PILOT-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..34];
        AttachToTaskProofRecord? proof = null;
        if (!string.IsNullOrWhiteSpace(submission.TaskId))
        {
            proof = TryGetAttachToTaskProof(submission.TaskId) ?? CaptureAttachToTaskProof(submission.TaskId);
        }

        return new PilotEvidenceRecord
        {
            EvidenceId = evidenceId,
            RepoRoot = paths.RepoRoot,
            RepoId = proof?.RepoId ?? submission.RepoId,
            TaskId = submission.TaskId,
            CardId = submission.CardId ?? proof?.CardId,
            Summary = submission.Summary.Trim(),
            Observations = Normalize(submission.Observations),
            FrictionPoints = Normalize(submission.FrictionPoints),
            FailedExpectations = Normalize(submission.FailedExpectations),
            FollowUps = NormalizeFollowUps(submission.FollowUps),
            RelatedSuggestedTaskIds = proof?.SuggestedTaskIds ?? Array.Empty<string>(),
            AttachProofId = proof?.ProofId,
            AttachProofPath = proof is null ? null : GetAttachProofPath(proof.TaskId),
            Status = "recorded",
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static PilotEvidenceSubmission BuildProblemEvidenceSubmission(PilotProblemIntakeSubmission submission)
    {
        var observations = new List<string>();
        observations.AddRange(Normalize(submission.Observations));
        AddIfPresent(observations, "problem_kind", submission.ProblemKind);
        AddIfPresent(observations, "severity", submission.Severity);
        AddIfPresent(observations, "current_stage_id", submission.CurrentStageId);
        AddIfPresent(observations, "next_governed_command", submission.NextGovernedCommand);
        AddIfPresent(observations, "blocked_command", submission.BlockedCommand);
        if (submission.CommandExitCode is not null)
        {
            observations.Add($"command_exit_code={submission.CommandExitCode.Value}");
        }

        AddIfPresent(observations, "command_output", Truncate(submission.CommandOutput, 500));

        var frictionPoints = new List<string>();
        AddIfPresent(frictionPoints, "stop_trigger", submission.StopTrigger);
        foreach (var path in Normalize(submission.AffectedPaths).Select(NormalizePath).OfType<string>())
        {
            frictionPoints.Add($"affected_path={path}");
        }

        var failedExpectations = new List<string>();
        failedExpectations.Add("agent could not continue under the surfaced CARVES next action");
        AddIfPresent(failedExpectations, "expected_next_governed_command", submission.NextGovernedCommand);

        return new PilotEvidenceSubmission
        {
            RepoId = submission.RepoId,
            TaskId = submission.TaskId,
            CardId = submission.CardId,
            Summary = $"Agent problem intake: {submission.Summary!.Trim()}",
            Observations = observations,
            FrictionPoints = frictionPoints,
            FailedExpectations = failedExpectations,
            FollowUps =
            [
                new PilotFollowUpSubmission
                {
                    Kind = "agent_problem_intake",
                    Title = submission.Summary.Trim(),
                    Reason = string.IsNullOrWhiteSpace(submission.RecommendedFollowUp)
                        ? NormalizeScalar(submission.StopTrigger)
                        : submission.RecommendedFollowUp.Trim(),
                },
            ],
        };
    }

    private static void AddIfPresent(List<string> target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target.Add($"{key}={value.Trim()}");
        }
    }

    private static bool IsAcceptedProblemKind(string problemKind)
    {
        return problemKind.Trim() switch
        {
            "command_failed" => true,
            "blocked_posture" => true,
            "protected_truth_root_requested" => true,
            "missing_acceptance_contract" => true,
            "workspace_scope_ambiguous" => true,
            "next_command_ambiguous" => true,
            "runtime_binding_ambiguous" => true,
            "agent_policy_conflict" => true,
            "other" => true,
            _ => false,
        };
    }

    private static string NormalizeScalar(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : string.Concat(trimmed.AsSpan(0, maxLength), "...");
    }

    private static IReadOnlyList<PilotFollowUpRecord> NormalizeFollowUps(IReadOnlyList<PilotFollowUpSubmission>? values)
    {
        return (values ?? Array.Empty<PilotFollowUpSubmission>())
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Reason))
            .Select(item => new PilotFollowUpRecord
            {
                Kind = string.IsNullOrWhiteSpace(item.Kind) ? "follow_up" : item.Kind.Trim(),
                Title = item.Title!.Trim(),
                Reason = item.Reason!.Trim(),
            })
            .ToArray();
    }

    private static string? TryReadResultStatus(string resultPath)
    {
        if (!File.Exists(resultPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ResultEnvelope>(File.ReadAllText(resultPath), JsonOptions)?.Status;
    }

    private static (string? RepoId, string? HostSessionId) ReadRuntimeManifest(string runtimeManifestPath)
    {
        if (!File.Exists(runtimeManifestPath))
        {
            return (null, null);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(runtimeManifestPath));
        var root = document.RootElement;
        var repoId = root.TryGetProperty("repo_id", out var repoIdElement) ? repoIdElement.GetString() : null;
        var hostSessionId = root.TryGetProperty("host_session_id", out var hostSessionElement) ? hostSessionElement.GetString() : null;
        return (repoId, hostSessionId);
    }

    private void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private void WriteResultEnvelope(string path, ResultEnvelope envelope)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(envelope, ResultJsonOptions));
    }

    private string GetRunPath(string taskId, string runId) => Path.Combine(paths.RuntimeRoot, "runs", taskId, $"{runId}.json");

    private string GetAttachProofRoot() => Path.Combine(paths.RuntimeRoot, "proofs", "attach-to-task");

    private string GetAttachProofPath(string taskId) => Path.Combine(GetAttachProofRoot(), $"{taskId}.json");

    private string GetPilotEvidenceRoot() => Path.Combine(paths.RuntimeRoot, "pilot-evidence");

    private string GetPilotEvidencePath(string evidenceId) => Path.Combine(GetPilotEvidenceRoot(), $"{evidenceId}.json");

    private string GetPilotProblemRoot() => Path.Combine(paths.RuntimeRoot, "pilot-problems");

    private string GetPilotProblemPath(string problemId) => Path.Combine(GetPilotProblemRoot(), $"{problemId}.json");

    private ProofPaths BuildProofPaths(string taskId, string runId)
    {
        var evidenceDirectory = Path.Combine(paths.WorkerExecutionArtifactsRoot, runId);
        return new ProofPaths(
            EvidenceDirectory: evidenceDirectory,
            EvidencePath: Path.Combine(evidenceDirectory, "evidence.json"),
            CommandLogPath: Path.Combine(evidenceDirectory, "command.log"),
            BuildLogPath: Path.Combine(evidenceDirectory, "build.log"),
            TestLogPath: Path.Combine(evidenceDirectory, "test.log"),
            PatchPath: Path.Combine(evidenceDirectory, "patch.diff"),
            ResultEnvelopePath: Path.Combine(paths.AiRoot, "execution", taskId, "result.json"));
    }

    private static string NormalizeChangedFile(string? requestedChangedFile, IReadOnlyList<string> scope)
    {
        if (!string.IsNullOrWhiteSpace(requestedChangedFile))
        {
            return NormalizePath(requestedChangedFile)!;
        }

        var scopeEntry = scope.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        if (string.IsNullOrWhiteSpace(scopeEntry))
        {
            return "src/PilotProof.cs";
        }

        var normalized = NormalizePath(scopeEntry)!;
        if (normalized.EndsWith("/", StringComparison.Ordinal))
        {
            return normalized + "PilotProof.cs";
        }

        return Path.HasExtension(normalized)
            ? normalized
            : normalized + "/PilotProof.cs";
    }

    private static string? NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : path.Trim().Trim('`').Replace('\\', '/');
    }

    private string ToRepoRelative(string path)
    {
        return Path.GetRelativePath(paths.RepoRoot, path).Replace('\\', '/');
    }

    private sealed class PilotEvidenceSubmission
    {
        public string? RepoId { get; init; }

        public string? TaskId { get; init; }

        public string? CardId { get; init; }

        public string? Summary { get; init; }

        public IReadOnlyList<string>? Observations { get; init; }

        public IReadOnlyList<string>? FrictionPoints { get; init; }

        public IReadOnlyList<string>? FailedExpectations { get; init; }

        public IReadOnlyList<PilotFollowUpSubmission>? FollowUps { get; init; }
    }

    private sealed class PilotProblemIntakeSubmission
    {
        public string? RepoId { get; init; }

        public string? TaskId { get; init; }

        public string? CardId { get; init; }

        public string? CurrentStageId { get; init; }

        public string? NextGovernedCommand { get; init; }

        public string? ProblemKind { get; init; }

        public string? Severity { get; init; }

        public string? Summary { get; init; }

        public string? BlockedCommand { get; init; }

        public int? CommandExitCode { get; init; }

        public string? CommandOutput { get; init; }

        public string? StopTrigger { get; init; }

        public IReadOnlyList<string>? Observations { get; init; }

        public IReadOnlyList<string>? AffectedPaths { get; init; }

        public string? RecommendedFollowUp { get; init; }
    }

    private sealed class PilotFollowUpSubmission
    {
        public string? Kind { get; init; }

        public string? Title { get; init; }

        public string? Reason { get; init; }
    }

    private sealed record ProofPaths(
        string EvidenceDirectory,
        string EvidencePath,
        string CommandLogPath,
        string BuildLogPath,
        string TestLogPath,
        string PatchPath,
        string ResultEnvelopePath);
}
