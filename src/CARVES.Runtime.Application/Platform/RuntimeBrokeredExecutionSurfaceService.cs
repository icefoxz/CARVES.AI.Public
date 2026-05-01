using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeBrokeredExecutionSurfaceService
{
    private const string DocumentPath = "docs/runtime/runtime-mode-e-brokered-execution.md";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string repoRoot;
    private readonly TaskGraphService taskGraphService;
    private readonly ExecutionPacketCompilerService executionPacketCompilerService;
    private readonly PacketEnforcementService packetEnforcementService;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly ReviewEvidenceGateService reviewEvidenceGateService;
    private readonly RuntimeWorkspaceMutationAuditService? mutationAuditService;
    private readonly MemoryPatternWritebackRouteAuthorizationService? memoryPatternWritebackRouteAuthorizationService;

    public RuntimeBrokeredExecutionSurfaceService(
        string repoRoot,
        TaskGraphService taskGraphService,
        ExecutionPacketCompilerService executionPacketCompilerService,
        PacketEnforcementService packetEnforcementService,
        IRuntimeArtifactRepository artifactRepository,
        ReviewEvidenceGateService reviewEvidenceGateService,
        RuntimeWorkspaceMutationAuditService? mutationAuditService = null,
        MemoryPatternWritebackRouteAuthorizationService? memoryPatternWritebackRouteAuthorizationService = null)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.taskGraphService = taskGraphService;
        this.executionPacketCompilerService = executionPacketCompilerService;
        this.packetEnforcementService = packetEnforcementService;
        this.artifactRepository = artifactRepository;
        this.reviewEvidenceGateService = reviewEvidenceGateService;
        this.mutationAuditService = mutationAuditService;
        this.memoryPatternWritebackRouteAuthorizationService = memoryPatternWritebackRouteAuthorizationService;
    }

    public RuntimeBrokeredExecutionSurface Build(string taskId)
    {
        var errors = new List<string>();
        ValidatePath(DocumentPath, "Mode E brokered execution doctrine document", errors);

        var task = taskGraphService.GetTask(taskId);
        var packet = new ExecutionPacketSurfaceService(executionPacketCompilerService).Build(taskId);
        var resultReturnChannel = $".ai/execution/{taskId}/result.json";
        var resultRead = ReadResultEnvelope(taskId, resultReturnChannel);
        var packetEnforcement = resultRead.HasPayloadShapeBlocker
            ? BuildResultReturnBlockedPacketEnforcement(task, packet, resultRead)
            : new PacketEnforcementSurfaceService(packetEnforcementService).Build(taskId);
        var resultReturn = BuildResultReturnSurface(resultReturnChannel, resultRead, packetEnforcement);
        var reviewPreflight = new ModeEReviewPreflightService(
                packetEnforcementService.Paths,
                taskGraphService,
                artifactRepository,
                packetEnforcementService,
                reviewEvidenceGateService,
                memoryPatternWritebackRouteAuthorizationService)
            .Build(taskId, packetEnforcement);
        var mutationAudit = mutationAuditService?.Build(taskId);
        var brokeredChecks = BuildChecks(packet, packetEnforcement, resultReturn, reviewPreflight, mutationAudit, errors);
        var state = ResolveState(packet, packetEnforcement, resultReturn, reviewPreflight, errors, brokeredChecks);
        var summary = BuildSummary(state, packet, packetEnforcement, resultReturn, reviewPreflight);

        return new RuntimeBrokeredExecutionSurface
        {
            TaskId = taskId,
            CardId = task.CardId ?? string.Empty,
            TaskStatus = task.Status.ToString().ToLowerInvariant(),
            BrokeredExecutionState = state,
            ResultReturnChannel = resultReturnChannel,
            ResultReturnPayloadStatus = resultReturn.PayloadStatus,
            ResultReturnOfficialTruthState = resultReturn.OfficialTruthState,
            PacketPath = packet.PacketPath,
            PacketPersisted = packet.Persisted,
            PacketEnforcementPath = packetEnforcement.EnforcementPath,
            PacketEnforcementVerdict = packetEnforcement.Record.Verdict,
            PlannerIntent = packet.PlannerIntent.ToString().ToLowerInvariant(),
            Summary = summary,
            RecommendedNextAction = ResolveRecommendedNextAction(state, task, packetEnforcement, resultReturn, reviewPreflight),
            BrokeredChecks = brokeredChecks,
            InspectCommands =
            [
                $"inspect execution-packet {taskId}",
                $"inspect packet-enforcement {taskId}",
                $"inspect runtime-brokered-execution {taskId}",
                $"inspect runtime-workspace-mutation-audit {taskId}",
            ],
            NonClaims =
            [
                "Mode E brokered execution is task-scoped packet/result mediation, not OS-user or container isolation.",
                "Mode E does not grant a worker direct official truth write authority.",
                "Mode E does not replace Mode C or Mode D for IDE-friendly workspace execution.",
                "Mode E does not claim remote transport, broad ACP packaging, or broad MCP packaging.",
            ],
            Errors = errors,
            IsValid = errors.Count == 0 && !string.Equals(state, "blocked_by_packet_contract", StringComparison.Ordinal),
            ExecutionPacket = packet,
            PacketEnforcement = packetEnforcement,
            ResultReturn = resultReturn,
            ReviewPreflight = reviewPreflight,
            MutationAudit = mutationAudit,
        };
    }

    private static RuntimeBrokeredExecutionCheckSurface[] BuildChecks(
        ExecutionPacketSurfaceSnapshot packet,
        PacketEnforcementSurfaceSnapshot packetEnforcement,
        RuntimeBrokeredResultReturnSurface resultReturn,
        RuntimeBrokeredReviewPreflightSurface reviewPreflight,
        RuntimeWorkspaceMutationAuditSurface? mutationAudit,
        IReadOnlyList<string> errors)
    {
        var packetContractValid = PacketContractLooksValid(packet.Packet);
        var submitResultDeclared = packet.Packet.WorkerAllowedActions.Any(action => ActionMatches(action, "carves.submit_result"));
        var plannerLifecycleOwned = packet.Packet.PlannerOnlyActions.Any(action => ActionMatches(action, "carves.review_task"))
            && packet.Packet.PlannerOnlyActions.Any(action => ActionMatches(action, "carves.sync_state"));
        var truthRootsNotEditable = TruthRootsAreNotWorkerEditable(packet.Packet.Permissions);
        var enforcementAvailable = packet.Persisted
            && packetEnforcement.Record.PacketPresent
            && !string.Equals(packetEnforcement.Record.Verdict, "not_applicable", StringComparison.Ordinal);

        return
        [
            BuildCheck(
                "mode_e_doctrine_anchor",
                errors.Count == 0,
                "Mode E doctrine document is present and can anchor brokered execution claims.",
                "Restore docs/runtime/runtime-mode-e-brokered-execution.md before claiming Mode E support."),
            BuildCheck(
                "execution_packet_available",
                !string.IsNullOrWhiteSpace(packet.Packet.PacketId),
                "Execution packet can be built or read for this task.",
                "Fix task truth so execution-packet can compile a packet."),
            BuildCheck(
                "execution_packet_persisted",
                packet.Persisted,
                "Execution packet is persisted and can be used as the worker contract.",
                "Issue or persist the execution packet before starting brokered execution."),
            BuildCheck(
                "submit_result_channel_declared",
                submitResultDeclared,
                "Worker contract includes carves.submit_result as the terminal return action.",
                "Regenerate or correct the execution packet so workers terminate at carves.submit_result."),
            BuildCheck(
                "planner_owned_lifecycle",
                plannerLifecycleOwned,
                "Review and sync lifecycle actions remain planner-owned.",
                "Keep review_task and sync_state in planner-only actions."),
            BuildCheck(
                "truth_roots_not_worker_editable",
                truthRootsNotEditable,
                "Packet permissions do not make truth roots ordinary worker-editable roots.",
                "Remove truth-root or mirror-root paths from editable roots."),
            BuildCheck(
                "packet_contract_valid",
                packetContractValid,
                "Packet worker and planner action sets are separated and submit_result is declared.",
                "Fix packet actions before accepting brokered execution results."),
            BuildCheck(
                "packet_enforcement_available",
                enforcementAvailable,
                "Packet enforcement can evaluate the current brokered result state.",
                "Persist the packet and inspect packet-enforcement before writeback."),
            BuildCheck(
                "result_return_channel_projected",
                true,
                "Result return channel is projected as .ai/execution/<task-id>/result.json.",
                "Return a result envelope, worker artifact, or replan request through the brokered channel."),
            BuildCheck(
                "result_return_payload_shape",
                !resultReturn.PayloadPresent || resultReturn.PayloadValid,
                "Returned payload is absent or conforms to result-envelope.v1 for the bound task.",
                "Return valid result-envelope.v1 JSON for the bound task before planner review."),
            BuildCheck(
                "result_return_expected_evidence",
                resultReturn.MissingEvidence.Count == 0,
                "Returned material includes the expected result envelope, worker artifact, and packet-enforcement evidence.",
                $"Provide missing result-return evidence: {string.Join(", ", resultReturn.MissingEvidence)}."),
            BuildCheck(
                "returned_material_not_approved_truth",
                string.Equals(resultReturn.OfficialTruthState, "returned_material_not_approved_truth", StringComparison.Ordinal),
                "Returned worker material is projected separately from approved official truth.",
                "Keep worker-returned artifacts out of official truth until planner review and host writeback approve them."),
            BuildCheck(
                "mode_e_review_preflight_packet_scope",
                !reviewPreflight.Applies || string.Equals(reviewPreflight.PacketScopeStatus, "clear", StringComparison.Ordinal),
                "Mode E review preflight reports no packet-scope mismatch.",
                "Resolve packet-scope mismatches before review approval."),
            BuildCheck(
                "mode_e_review_preflight_acceptance_evidence",
                !reviewPreflight.Applies || reviewPreflight.AcceptanceEvidenceStatus is "satisfied" or "no_requirements" or "awaiting_review_artifact",
                "Mode E review preflight reports acceptance evidence is satisfied or not required.",
                "Capture missing acceptance evidence before final review approval."),
            BuildCheck(
                "mode_e_review_preflight_path_policy",
                !reviewPreflight.Applies || string.Equals(reviewPreflight.PathPolicyStatus, "clear", StringComparison.Ordinal),
                "Mode E review preflight reports no protected-path policy violation.",
                "Remove protected-path writes before review approval or host-owned writeback."),
            BuildCheck(
                "mode_e_review_preflight_mutation_audit",
                !reviewPreflight.Applies || reviewPreflight.MutationAuditStatus is "clear" or "no_changed_paths",
                "Mode E review preflight reports no mutation-audit violation.",
                "Resolve mutation-audit blockers before review approval or writeback."),
            BuildCheck(
                "workspace_mutation_audit_surface",
                mutationAudit is null || mutationAudit.CanProceedToWriteback,
                "Workspace mutation audit is absent or reports no writeback-blocking violation.",
                mutationAudit is null
                    ? "Inspect runtime-workspace-mutation-audit <task-id> before final writeback."
                    : mutationAudit.RecommendedNextAction),
        ];
    }

    private static RuntimeBrokeredExecutionCheckSurface BuildCheck(
        string checkId,
        bool satisfied,
        string satisfiedSummary,
        string requiredAction)
    {
        return new RuntimeBrokeredExecutionCheckSurface
        {
            CheckId = checkId,
            State = satisfied ? "satisfied" : "missing",
            Summary = satisfied ? satisfiedSummary : requiredAction,
            RequiredAction = satisfied ? "none" : requiredAction,
        };
    }

    private ResultEnvelopeReadResult ReadResultEnvelope(string taskId, string resultReturnChannel)
    {
        var fullPath = Path.Combine(repoRoot, resultReturnChannel.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            return new ResultEnvelopeReadResult(resultReturnChannel, false, false, null, [], null);
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<ResultEnvelope>(File.ReadAllText(fullPath), JsonOptions);
            if (envelope is null)
            {
                return new ResultEnvelopeReadResult(
                    resultReturnChannel,
                    true,
                    true,
                    null,
                    ["result_envelope_deserialized_null"],
                    "Result return payload deserialized to null.");
            }

            return new ResultEnvelopeReadResult(
                resultReturnChannel,
                true,
                false,
                envelope,
                ValidateResultEnvelopeShape(taskId, envelope),
                null);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return new ResultEnvelopeReadResult(
                resultReturnChannel,
                true,
                true,
                null,
                ["result_envelope_malformed_json"],
                exception.Message);
        }
    }

    private static RuntimeBrokeredResultReturnSurface BuildResultReturnSurface(
        string resultReturnChannel,
        ResultEnvelopeReadResult resultRead,
        PacketEnforcementSurfaceSnapshot packetEnforcement)
    {
        var expectedEvidence = new[]
        {
            "result_envelope",
            "execution_evidence_path",
            "worker_execution_artifact",
            "packet_enforcement_allow",
        };
        var missingEvidence = BuildMissingEvidence(resultRead, packetEnforcement);
        var payloadStatus = ResolveResultReturnPayloadStatus(resultRead, packetEnforcement, missingEvidence);
        return new RuntimeBrokeredResultReturnSurface
        {
            Channel = resultReturnChannel,
            PayloadPresent = resultRead.Present,
            PayloadReadable = resultRead.Present && !resultRead.Malformed,
            PayloadValid = resultRead.Present && !resultRead.Malformed && resultRead.PayloadIssues.Count == 0,
            PayloadMalformed = resultRead.Malformed,
            PayloadStatus = payloadStatus,
            PayloadTaskId = resultRead.Envelope?.TaskId,
            PayloadExecutionRunId = resultRead.Envelope?.ExecutionRunId,
            PayloadResultStatus = resultRead.Envelope?.Status,
            ExpectedEvidence = expectedEvidence,
            MissingEvidence = missingEvidence,
            PayloadIssues = resultRead.PayloadIssues,
            Summary = BuildResultReturnSummary(payloadStatus, resultRead, packetEnforcement, missingEvidence),
        };
    }

    private static PacketEnforcementSurfaceSnapshot BuildResultReturnBlockedPacketEnforcement(
        TaskNode task,
        ExecutionPacketSurfaceSnapshot packet,
        ResultEnvelopeReadResult resultRead)
    {
        var packetPresent = packet.Persisted && !string.IsNullOrWhiteSpace(packet.Packet.PacketId);
        var record = new PacketEnforcementRecord
        {
            TaskId = task.TaskId,
            CardId = task.CardId ?? string.Empty,
            PacketId = packet.Packet.PacketId,
            PlannerIntent = packet.PlannerIntent.ToString().ToLowerInvariant(),
            PacketPresent = packetPresent,
            PacketPersisted = packet.Persisted,
            PacketContractValid = PacketContractLooksValid(packet.Packet),
            ResultPresent = resultRead.Present,
            WorkerArtifactPresent = false,
            SubmitResultAllowed = packet.Packet.WorkerAllowedActions.Any(action => ActionMatches(action, "carves.submit_result")),
            WorkerAllowedActions = packet.Packet.WorkerAllowedActions,
            PlannerOnlyActions = packet.Packet.PlannerOnlyActions,
            EditableRoots = packet.Packet.Permissions.EditableRoots,
            RepoMirrorRoots = packet.Packet.Permissions.RepoMirrorRoots,
            Verdict = "result_return_payload_invalid",
            ReasonCodes = resultRead.PayloadIssues.Count == 0
                ? ["result_envelope_invalid"]
                : resultRead.PayloadIssues,
            EvidencePaths = [packet.PacketPath, resultRead.Channel],
            Summary = "Packet enforcement is held because the brokered result-return payload is missing a valid result-envelope.v1 shape.",
        };

        return new PacketEnforcementSurfaceSnapshot
        {
            TaskId = task.TaskId,
            CardId = task.CardId ?? string.Empty,
            PacketPath = packet.PacketPath,
            EnforcementPath = $".ai/runtime/packet-enforcement/{task.TaskId}.json",
            Persisted = false,
            Summary = record.Summary,
            Record = record,
        };
    }

    private static IReadOnlyList<string> ValidateResultEnvelopeShape(string taskId, ResultEnvelope envelope)
    {
        var issues = new List<string>();
        if (!string.Equals(envelope.SchemaVersion, "1.0", StringComparison.Ordinal))
        {
            issues.Add("result_schema_version_invalid");
        }

        if (!string.Equals(envelope.TaskId, taskId, StringComparison.Ordinal))
        {
            issues.Add("result_task_id_mismatch");
        }

        if (!string.IsNullOrWhiteSpace(envelope.ExecutionRunId)
            && !envelope.ExecutionRunId.StartsWith("RUN-", StringComparison.Ordinal))
        {
            issues.Add("result_execution_run_id_invalid");
        }

        if (!string.IsNullOrWhiteSpace(envelope.ExecutionEvidencePath)
            && !envelope.ExecutionEvidencePath.EndsWith("evidence.json", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("result_execution_evidence_path_invalid");
        }

        if (envelope.CompletedStepCount is < 0)
        {
            issues.Add("result_completed_step_count_negative");
        }

        if (envelope.TotalStepCount is < 0)
        {
            issues.Add("result_total_step_count_negative");
        }

        if (envelope.CompletedStepCount.HasValue
            && envelope.TotalStepCount.HasValue
            && envelope.CompletedStepCount.Value > envelope.TotalStepCount.Value)
        {
            issues.Add("result_completed_step_count_exceeds_total");
        }

        AddUnsupportedValueIssue(issues, envelope.Status, "result_status_invalid", ["success", "failed", "blocked"]);
        AddUnsupportedValueIssue(issues, envelope.Validation.Build, "result_validation_build_invalid", ["success", "failed", "not_run"]);
        AddUnsupportedValueIssue(issues, envelope.Validation.Tests, "result_validation_tests_invalid", ["success", "failed", "not_run"]);

        if (!string.Equals(envelope.Telemetry.SchemaVersion, "1.0", StringComparison.Ordinal))
        {
            issues.Add("result_telemetry_schema_version_invalid");
        }

        if (envelope.Telemetry.DurationSeconds < 0)
        {
            issues.Add("result_telemetry_duration_negative");
        }

        return issues.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> BuildMissingEvidence(
        ResultEnvelopeReadResult resultRead,
        PacketEnforcementSurfaceSnapshot packetEnforcement)
    {
        var missing = new List<string>();
        if (!resultRead.Present)
        {
            missing.Add("result_envelope");
        }

        if (resultRead.Present && (resultRead.Malformed || resultRead.PayloadIssues.Count > 0))
        {
            missing.Add("valid_result_envelope");
        }

        if (resultRead.Envelope is not null && string.IsNullOrWhiteSpace(resultRead.Envelope.ExecutionEvidencePath))
        {
            missing.Add("execution_evidence_path");
        }

        if (!packetEnforcement.Record.WorkerArtifactPresent)
        {
            missing.Add("worker_execution_artifact");
        }

        if (!string.Equals(packetEnforcement.Record.Verdict, "allow", StringComparison.Ordinal))
        {
            missing.Add("packet_enforcement_allow");
        }

        return missing.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string ResolveResultReturnPayloadStatus(
        ResultEnvelopeReadResult resultRead,
        PacketEnforcementSurfaceSnapshot packetEnforcement,
        IReadOnlyList<string> missingEvidence)
    {
        if (!resultRead.Present)
        {
            return packetEnforcement.Record.WorkerArtifactPresent
                ? "worker_artifact_without_result_envelope"
                : "missing";
        }

        if (resultRead.Malformed)
        {
            return "malformed";
        }

        if (resultRead.PayloadIssues.Count > 0)
        {
            return "invalid";
        }

        if (missingEvidence.Any(item => string.Equals(item, "worker_execution_artifact", StringComparison.Ordinal)
                || string.Equals(item, "execution_evidence_path", StringComparison.Ordinal)))
        {
            return "returned_without_worker_artifact";
        }

        return packetEnforcement.Record.Verdict switch
        {
            "allow" => "returned_ready_for_review",
            "reject" or "quarantine" => "returned_blocked_by_packet_enforcement",
            "pending_execution" => "returned_waiting_for_packet_enforcement",
            _ => "returned_ready_for_packet_enforcement",
        };
    }

    private static string BuildResultReturnSummary(
        string payloadStatus,
        ResultEnvelopeReadResult resultRead,
        PacketEnforcementSurfaceSnapshot packetEnforcement,
        IReadOnlyList<string> missingEvidence)
    {
        var issues = resultRead.PayloadIssues.Count == 0
            ? "none"
            : string.Join(", ", resultRead.PayloadIssues);
        var missing = missingEvidence.Count == 0
            ? "none"
            : string.Join(", ", missingEvidence);
        return $"result_return={payloadStatus}; payload_present={resultRead.Present}; packet_enforcement={packetEnforcement.Record.Verdict}; missing_evidence={missing}; payload_issues={issues}; official_truth=not_approved_until_planner_review_and_host_writeback.";
    }

    private static void AddUnsupportedValueIssue(
        List<string> issues,
        string value,
        string issue,
        IReadOnlyCollection<string> allowedValues)
    {
        if (!allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(issue);
        }
    }

    private static string ResolveState(
        ExecutionPacketSurfaceSnapshot packet,
        PacketEnforcementSurfaceSnapshot packetEnforcement,
        RuntimeBrokeredResultReturnSurface resultReturn,
        RuntimeBrokeredReviewPreflightSurface reviewPreflight,
        IReadOnlyList<string> errors,
        IReadOnlyList<RuntimeBrokeredExecutionCheckSurface> checks)
    {
        if (errors.Count > 0)
        {
            return "blocked_by_mode_e_doctrine_gaps";
        }

        if (checks.Any(check => IsPacketContractCheck(check.CheckId)
                && string.Equals(check.State, "missing", StringComparison.Ordinal)))
        {
            return "blocked_by_packet_contract";
        }

        if (!packet.Persisted)
        {
            return "packet_ready_to_issue";
        }

        if (resultReturn.PayloadMalformed || (resultReturn.PayloadPresent && !resultReturn.PayloadValid))
        {
            return "result_blocked_by_result_return_payload";
        }

        if (string.Equals(resultReturn.PayloadStatus, "returned_without_worker_artifact", StringComparison.Ordinal)
            || string.Equals(resultReturn.PayloadStatus, "worker_artifact_without_result_envelope", StringComparison.Ordinal))
        {
            return "result_waiting_for_writeback_evidence";
        }

        return packetEnforcement.Record.Verdict switch
        {
            "pending_execution" => "awaiting_brokered_result",
            "reject" or "quarantine" => "result_blocked_by_packet_enforcement",
            "allow" when reviewPreflight.Applies && !reviewPreflight.CanProceedToReviewApproval => "result_blocked_by_review_preflight",
            "allow" => "result_ready_for_review",
            "not_applicable" => "packet_ready_to_issue",
            _ => "brokered_execution_ready",
        };
    }

    private static string ResolveRecommendedNextAction(
        string state,
        TaskNode task,
        PacketEnforcementSurfaceSnapshot packetEnforcement,
        RuntimeBrokeredResultReturnSurface resultReturn,
        RuntimeBrokeredReviewPreflightSurface reviewPreflight)
    {
        return state switch
        {
            "blocked_by_mode_e_doctrine_gaps" => "Restore the Mode E doctrine anchor before treating brokered execution as supported.",
            "blocked_by_packet_contract" => "Regenerate the execution packet so submit_result, planner-only lifecycle actions, and truth-root boundaries are explicit.",
            "packet_ready_to_issue" => $"Issue or persist the execution packet for {task.TaskId} before brokered execution starts.",
            "awaiting_brokered_result" => $"Wait for .ai/execution/{task.TaskId}/result.json or a worker execution artifact, then inspect packet-enforcement {task.TaskId}.",
            "result_blocked_by_result_return_payload" => $"Correct the brokered result payload at {resultReturn.Channel}; issues={string.Join(", ", resultReturn.PayloadIssues)}.",
            "result_waiting_for_writeback_evidence" => $"Return the missing worker evidence before review approval; missing={string.Join(", ", resultReturn.MissingEvidence)}.",
            "result_blocked_by_review_preflight" => $"Resolve Mode E review preflight blockers before approval: {string.Join(", ", reviewPreflight.Blockers.Select(blocker => blocker.BlockerId))}.",
            "result_ready_for_review" => "Route the allowed result through planner review and host-owned writeback; the worker must stop at submit_result.",
            "result_blocked_by_packet_enforcement" => $"Inspect packet-enforcement {task.TaskId}; current verdict is {packetEnforcement.Record.Verdict}. Replan or correct the returned result.",
            _ => $"Inspect execution-packet {task.TaskId} and packet-enforcement {task.TaskId} before writeback.",
        };
    }

    private static string BuildSummary(
        string state,
        ExecutionPacketSurfaceSnapshot packet,
        PacketEnforcementSurfaceSnapshot packetEnforcement,
        RuntimeBrokeredResultReturnSurface resultReturn,
        RuntimeBrokeredReviewPreflightSurface reviewPreflight)
    {
        return $"Mode E state={state}; packet persisted={packet.Persisted}; result return status={resultReturn.PayloadStatus}; packet enforcement verdict={packetEnforcement.Record.Verdict}; review preflight status={reviewPreflight.Status}; returned worker material is not approved truth; official truth ingress remains planner review and host writeback only.";
    }

    private static bool PacketContractLooksValid(ExecutionPacket packet)
    {
        return packet.WorkerAllowedActions.Any(action => ActionMatches(action, "carves.submit_result"))
            && packet.WorkerAllowedActions.All(workerAction =>
                packet.PlannerOnlyActions.All(plannerAction => !ActionMatches(workerAction, plannerAction)));
    }

    private static bool IsPacketContractCheck(string checkId)
    {
        return checkId is "submit_result_channel_declared" or "planner_owned_lifecycle" or "truth_roots_not_worker_editable" or "packet_contract_valid";
    }

    private static bool TruthRootsAreNotWorkerEditable(ExecutionPacketPermissions permissions)
    {
        foreach (var editableRoot in permissions.EditableRoots.Select(NormalizeRoot))
        {
            if (string.IsNullOrWhiteSpace(editableRoot))
            {
                continue;
            }

            if (editableRoot.StartsWith(".ai/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(editableRoot, ".ai", StringComparison.OrdinalIgnoreCase)
                || editableRoot.StartsWith(".carves-platform/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(editableRoot, ".carves-platform", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (var mirrorRoot in permissions.RepoMirrorRoots.Select(NormalizeRoot))
            {
                if (PathsOverlap(editableRoot, mirrorRoot))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool PathsOverlap(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        return string.Equals(first, second, StringComparison.OrdinalIgnoreCase)
            || first.StartsWith($"{second}/", StringComparison.OrdinalIgnoreCase)
            || second.StartsWith($"{first}/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRoot(string root)
    {
        return root.Replace('\\', '/').Trim().TrimEnd('/');
    }

    private static bool ActionMatches(string value, string candidate)
    {
        return string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase)
            || value.Contains(candidate, StringComparison.OrdinalIgnoreCase)
            || value.Replace('.', '_').Contains(candidate.Replace('.', '_'), StringComparison.OrdinalIgnoreCase);
    }

    private void ValidatePath(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private sealed record ResultEnvelopeReadResult(
        string Channel,
        bool Present,
        bool Malformed,
        ResultEnvelope? Envelope,
        IReadOnlyList<string> PayloadIssues,
        string? ParseError)
    {
        public bool HasPayloadShapeBlocker => Present && (Malformed || PayloadIssues.Count > 0);
    }
}
