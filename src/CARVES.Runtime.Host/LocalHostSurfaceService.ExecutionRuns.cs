using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    public JsonObject BuildRunInspect(string runId)
    {
        var run = services.ExecutionRunService.Get(runId);
        var task = services.TaskGraphService.GetTask(run.TaskId);
        var runArtifact = services.ArtifactRepository.TryLoadWorkerArtifact(run.TaskId);
        var workerArtifact = services.ArtifactRepository.TryLoadWorkerExecutionArtifact(run.TaskId);
        var resultEnvelope = TryLoadJson<ResultEnvelope>(run.ResultEnvelopePath);
        var boundary = TryLoadJson<ExecutionBoundaryViolation>(run.BoundaryViolationPath);
        var replan = TryLoadJson<ExecutionBoundaryReplanRequest>(run.ReplanArtifactPath);
        var recentFailure = services.FailureContextService.GetTaskFailures(run.TaskId, 1).FirstOrDefault();
        var requestMetadata = runArtifact?.Report.Request.ExecutionRequest?.Metadata;
        return new JsonObject
        {
            ["kind"] = "execution_run",
            ["run_id"] = run.RunId,
            ["task_id"] = run.TaskId,
            ["status"] = run.Status.ToString(),
            ["trigger_reason"] = run.TriggerReason.ToString(),
            ["goal"] = run.Goal,
            ["created_at_utc"] = run.CreatedAtUtc,
            ["started_at_utc"] = run.StartedAtUtc,
            ["ended_at_utc"] = run.EndedAtUtc,
            ["current_step_index"] = run.CurrentStepIndex,
            ["current_step_title"] = run.Steps.Count == 0
                ? "(none)"
                : run.Steps[Math.Clamp(run.CurrentStepIndex, 0, run.Steps.Count - 1)].Title,
            ["result_envelope_path"] = run.ResultEnvelopePath,
            ["boundary_violation_path"] = run.BoundaryViolationPath,
            ["replan_artifact_path"] = run.ReplanArtifactPath,
            ["selected_pack"] = run.SelectedPack is null ? null : BuildSelectedPackNode(run.SelectedPack),
            ["pack_command_admission"] = BuildRuntimePackCommandAdmissionNode(requestMetadata),
            ["request_budget"] = BuildRequestBudgetNode(runArtifact?.Report.Request.ExecutionRequest?.RequestBudget ?? runArtifact?.Report.Session.WorkerRequestBudget),
            ["acceptance_contract"] = BuildAcceptanceContractRouteNode(requestMetadata),
            ["correlated_result"] = resultEnvelope is null
                ? null
                : new JsonObject
                {
                    ["status"] = resultEnvelope.Status,
                    ["stop_reason"] = resultEnvelope.Result.StopReason,
                    ["files_changed"] = resultEnvelope.Telemetry.FilesChanged,
                    ["lines_changed"] = resultEnvelope.Telemetry.LinesChanged,
                },
            ["execution_evidence"] = workerArtifact is null || !string.Equals(workerArtifact.Result.RunId, run.RunId, StringComparison.Ordinal)
                ? null
                : BuildExecutionEvidenceNode(workerArtifact.Evidence),
            ["correlated_failure"] = recentFailure is null
                ? null
                : new JsonObject
                {
                    ["failure_id"] = recentFailure.Id,
                    ["type"] = recentFailure.Failure.Type.ToString(),
                    ["message"] = recentFailure.Failure.Message,
                },
            ["correlated_boundary"] = boundary is null
                ? null
                : new JsonObject
                {
                    ["reason"] = boundary.Reason.ToString(),
                    ["detail"] = boundary.Detail,
                    ["stopped_at_step"] = boundary.StoppedAtStep,
                },
            ["correlated_replan"] = replan is null
                ? null
                : new JsonObject
                {
                    ["strategy"] = replan.Strategy.ToString(),
                    ["reason"] = replan.ViolationReason.ToString(),
                    ["max_files"] = replan.Constraints.MaxFiles,
                    ["allowed_change_kinds"] = ToJsonArray(replan.Constraints.AllowedChangeKinds.Select(item => item.ToString())),
                    ["follow_up_suggestions"] = ToJsonArray(replan.FollowUpSuggestions),
                },
            ["next_action"] = ResolveTaskNextAction(task, task.Dependencies.Where(dependency => !services.TaskGraphService.Load().CompletedTaskIds().Contains(dependency)).ToArray()),
            ["steps"] = new JsonArray(run.Steps.Select(step => new JsonObject
            {
                ["step_id"] = step.StepId,
                ["title"] = step.Title,
                ["kind"] = step.Kind.ToString(),
                ["status"] = step.Status.ToString(),
                ["started_at_utc"] = step.StartedAtUtc,
                ["ended_at_utc"] = step.EndedAtUtc,
                ["notes"] = step.Notes,
            }).ToArray()),
        };
    }

    public JsonObject BuildExecutionTraceInspect(string taskId)
    {
        var task = services.TaskGraphService.GetTask(taskId);
        var runArtifact = services.ArtifactRepository.TryLoadWorkerArtifact(taskId);
        var workerArtifact = services.ArtifactRepository.TryLoadWorkerExecutionArtifact(taskId);
        var providerArtifact = services.ArtifactRepository.TryLoadProviderArtifact(taskId);
        var permissionArtifact = services.ArtifactRepository.TryLoadWorkerPermissionArtifact(taskId);
        var reviewArtifact = services.ArtifactRepository.TryLoadPlannerReviewArtifact(taskId);
        var runs = services.ExecutionRunService.ListRuns(taskId);
        var latestRun = runs.LastOrDefault();
        var trace = runArtifact?.Report.WorkerExecution.CommandTrace
            ?? workerArtifact?.Result.CommandTrace
            ?? Array.Empty<CommandExecutionRecord>();
        var validationTrace = runArtifact?.Report.Validation.CommandResults ?? Array.Empty<CommandExecutionRecord>();
        var patchPaths = runArtifact?.Report.Patch.Paths ?? workerArtifact?.Result.ChangedFiles ?? Array.Empty<string>();
        var executionEvidence = workerArtifact?.Evidence ?? ExecutionEvidence.None;
        var route = BuildWorkerRouteNode(task, runArtifact, workerArtifact, providerArtifact);
        var requestBudget = runArtifact?.Report.Request.ExecutionRequest?.RequestBudget ?? runArtifact?.Report.Session.WorkerRequestBudget;
        var reviewEvidenceGate = BuildReviewEvidenceGateNode(task, reviewArtifact, workerArtifact);
        var reviewClosureBundle = BuildReviewClosureBundleNode(reviewArtifact);
        var requestMetadata = runArtifact?.Report.Request.ExecutionRequest?.Metadata;
        var runtimePackReviewRubric = BuildRuntimePackReviewRubricNode();

        return new JsonObject
        {
            ["kind"] = "execution_trace",
            ["task_id"] = taskId,
            ["task_status"] = task.Status.ToString(),
            ["latest_run_id"] = latestRun?.RunId ?? task.LastWorkerRunId,
            ["pack_command_admission"] = BuildRuntimePackCommandAdmissionNode(requestMetadata),
            ["runtime_pack_review_rubric"] = runtimePackReviewRubric,
            ["worktree_path"] = runArtifact?.Report.WorktreePath,
            ["result_commit"] = runArtifact?.Report.ResultCommit ?? task.ResultCommit,
            ["patch"] = new JsonObject
            {
                ["files_changed"] = runArtifact?.Report.Patch.FilesChanged ?? workerArtifact?.Result.ChangedFiles.Count ?? 0,
                ["paths"] = ToJsonArray(patchPaths),
                ["estimated"] = runArtifact?.Report.Patch.Estimated ?? true,
            },
            ["request_budget"] = BuildRequestBudgetNode(requestBudget),
            ["evidence"] = BuildExecutionEvidenceNode(executionEvidence),
            ["route"] = route,
            ["worker"] = workerArtifact is null
                ? null
                : new JsonObject
                {
                    ["run_id"] = workerArtifact.Result.RunId,
                    ["status"] = workerArtifact.Result.Status.ToString(),
                    ["backend_id"] = workerArtifact.Result.BackendId,
                    ["provider_id"] = workerArtifact.Result.ProviderId,
                    ["protocol_family"] = workerArtifact.Result.ProtocolFamily,
                    ["request_family"] = workerArtifact.Result.RequestFamily,
                    ["prior_thread_id"] = workerArtifact.Result.PriorThreadId,
                    ["thread_id"] = workerArtifact.Result.ThreadId,
                    ["thread_continuity"] = workerArtifact.Result.ThreadContinuity.ToString(),
                    ["summary"] = workerArtifact.Result.Summary,
                    ["failure_kind"] = workerArtifact.Result.FailureKind.ToString(),
                    ["failure_layer"] = workerArtifact.Result.FailureLayer.ToString(),
                    ["retryable"] = workerArtifact.Result.Retryable,
                    ["started_at"] = workerArtifact.Result.StartedAt,
                    ["completed_at"] = workerArtifact.Result.CompletedAt,
                },
            ["provider"] = providerArtifact is null
                ? null
                : new JsonObject
                {
                    ["worker_adapter"] = providerArtifact.Record.WorkerAdapter,
                    ["provider"] = providerArtifact.Record.Provider,
                    ["model"] = providerArtifact.Record.Model,
                    ["configured"] = providerArtifact.Record.Configured,
                    ["request_id"] = providerArtifact.Record.RequestId,
                    ["failure_reason"] = providerArtifact.Record.FailureReason,
                },
            ["permissions"] = new JsonObject
            {
                ["count"] = permissionArtifact?.Requests.Count ?? 0,
                ["pending"] = permissionArtifact?.Requests.Count(item => item.State == WorkerPermissionState.Pending) ?? 0,
            },
            ["review"] = reviewArtifact is null
                ? null
                : new JsonObject
                {
                    ["resulting_status"] = reviewArtifact.ResultingStatus.ToString(),
                    ["decision_status"] = reviewArtifact.DecisionStatus.ToString(),
                    ["decision_debt"] = reviewArtifact.DecisionDebt is null
                        ? null
                        : new JsonObject
                        {
                            ["summary"] = reviewArtifact.DecisionDebt.Summary,
                            ["requires_follow_up_review"] = reviewArtifact.DecisionDebt.RequiresFollowUpReview,
                        },
                    ["transition_reason"] = reviewArtifact.TransitionReason,
                    ["planner_comment"] = reviewArtifact.PlannerComment,
                    ["validation_passed"] = reviewArtifact.ValidationPassed,
                    ["closure_bundle"] = reviewClosureBundle,
                    ["evidence_gate"] = reviewEvidenceGate,
                },
            ["worker_trace"] = new JsonArray(trace.Select(item => new JsonObject
            {
                ["command"] = string.Join(' ', item.Command),
                ["exit_code"] = item.ExitCode,
                ["skipped"] = item.Skipped,
                ["working_directory"] = item.WorkingDirectory,
                ["category"] = item.Category,
                ["stdout"] = item.StandardOutput,
                ["stderr"] = item.StandardError,
                ["captured_at"] = item.CapturedAt,
            }).ToArray()),
            ["validation_trace"] = new JsonArray(validationTrace.Select(item => new JsonObject
            {
                ["command"] = string.Join(' ', item.Command),
                ["exit_code"] = item.ExitCode,
                ["skipped"] = item.Skipped,
                ["working_directory"] = item.WorkingDirectory,
                ["category"] = item.Category,
                ["stdout"] = item.StandardOutput,
                ["stderr"] = item.StandardError,
                ["captured_at"] = item.CapturedAt,
            }).ToArray()),
            ["runs"] = new JsonArray(runs.Select(BuildRunSummaryNode).ToArray()),
        };
    }

    private static JsonObject? BuildWorkerRouteNode(
        TaskNode task,
        TaskRunArtifact? runArtifact,
        WorkerExecutionArtifact? workerArtifact,
        AiExecutionArtifact? providerArtifact)
    {
        var session = runArtifact?.Report.Session;
        var selection = runArtifact?.Report.Request.Selection;
        var requestMetadata = runArtifact?.Report.Request.ExecutionRequest?.Metadata;
        var execution = workerArtifact?.Result;
        var routeSource = session?.WorkerRouteSource ?? selection?.RouteSource;
        var routingIntent = session?.WorkerRoutingIntent ?? selection?.RoutingIntent ?? task.Metadata.GetValueOrDefault("routing_intent");
        var routingModuleId = session?.WorkerRoutingModuleId ?? selection?.RoutingModuleId ?? task.Metadata.GetValueOrDefault("module_id");
        var selectedBackendId = session?.WorkerBackend ?? selection?.SelectedBackendId ?? execution?.BackendId ?? task.LastWorkerBackend;
        var selectedProviderId = session?.WorkerProviderId ?? selection?.SelectedProviderId ?? execution?.ProviderId;
        var selectedModelId = session?.WorkerModelId ?? selection?.SelectedModelId ?? execution?.Model;
        var activeRoutingProfileId = session?.ActiveRoutingProfileId ?? selection?.ActiveRoutingProfileId;
        var selectedRoutingProfileId = session?.WorkerRoutingProfileId ?? selection?.SelectedRoutingProfileId;
        var routingRuleId = session?.WorkerRoutingRuleId ?? selection?.AppliedRoutingRuleId;
        var requestBudget = session?.WorkerRequestBudget ?? runArtifact?.Report.Request.ExecutionRequest?.RequestBudget;
        if (string.IsNullOrWhiteSpace(selectedBackendId)
            && string.IsNullOrWhiteSpace(selectedProviderId)
            && string.IsNullOrWhiteSpace(selectedModelId)
            && string.IsNullOrWhiteSpace(routeSource)
            && string.IsNullOrWhiteSpace(routingIntent)
            && string.IsNullOrWhiteSpace(routingModuleId)
            && string.IsNullOrWhiteSpace(activeRoutingProfileId)
            && string.IsNullOrWhiteSpace(selectedRoutingProfileId)
            && string.IsNullOrWhiteSpace(routingRuleId)
            && requestBudget is null
            && execution is null
            && providerArtifact is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["run_id"] = execution?.RunId ?? session?.WorkerRunId ?? task.LastWorkerRunId,
            ["backend_id"] = selectedBackendId,
            ["provider_id"] = selectedProviderId,
            ["model"] = execution?.Model ?? selectedModelId,
            ["selected_model"] = selectedModelId,
            ["provider_artifact_model"] = providerArtifact?.Record.Model,
            ["active_routing_profile_id"] = activeRoutingProfileId,
            ["selected_routing_profile_id"] = selectedRoutingProfileId,
            ["routing_rule_id"] = routingRuleId,
            ["routing_intent"] = routingIntent,
            ["routing_module_id"] = routingModuleId,
            ["route_source"] = routeSource,
            ["selection_summary"] = session?.WorkerSelectionSummary ?? selection?.Summary,
            ["request_timeout_seconds"] = requestBudget?.TimeoutSeconds,
            ["request_budget_policy_id"] = requestBudget?.PolicyId,
            ["request_budget_summary"] = requestBudget?.Summary,
            ["request_budget_reasons"] = requestBudget is null ? null : ToJsonArray(requestBudget.Reasons),
            ["acceptance_contract"] = BuildAcceptanceContractRouteNode(requestMetadata),
            ["protocol_family"] = execution?.ProtocolFamily ?? providerArtifact?.Record.ProtocolFamily,
            ["request_family"] = execution?.RequestFamily ?? providerArtifact?.Record.RequestFamily,
            ["thread_id"] = execution?.ThreadId,
            ["thread_continuity"] = execution?.ThreadContinuity.ToString(),
            ["status"] = execution?.Status.ToString(),
            ["failure_kind"] = execution?.FailureKind.ToString(),
            ["failure_layer"] = execution?.FailureLayer.ToString(),
        };
    }

    private static JsonObject? BuildAcceptanceContractRouteNode(IReadOnlyDictionary<string, string>? metadata)
    {
        var contractId = metadata?.GetValueOrDefault("acceptance_contract_id");
        var status = metadata?.GetValueOrDefault("acceptance_contract_status");
        var evidenceRequired = SplitDelimitedMetadata(metadata?.GetValueOrDefault("acceptance_contract_evidence_required"));
        var decisions = SplitDelimitedMetadata(metadata?.GetValueOrDefault("acceptance_contract_human_decisions"));
        var humanReviewRequired = ParseNullableBoolean(metadata?.GetValueOrDefault("acceptance_contract_human_review_required"));
        var provisionalAllowed = ParseNullableBoolean(metadata?.GetValueOrDefault("acceptance_contract_provisional_allowed"));

        if (string.IsNullOrWhiteSpace(contractId)
            && string.IsNullOrWhiteSpace(status)
            && evidenceRequired.Length == 0
            && decisions.Length == 0
            && humanReviewRequired is null
            && provisionalAllowed is null)
        {
            return null;
        }

        return new JsonObject
        {
            ["contract_id"] = string.IsNullOrWhiteSpace(contractId) ? null : contractId,
            ["status"] = string.IsNullOrWhiteSpace(status) ? null : status,
            ["human_review_required"] = humanReviewRequired,
            ["provisional_allowed"] = provisionalAllowed,
            ["evidence_required"] = ToJsonArray(evidenceRequired),
            ["decisions"] = ToJsonArray(decisions),
        };
    }

    private static JsonObject? BuildRequestBudgetNode(WorkerRequestBudget? requestBudget)
    {
        if (requestBudget is null || requestBudget.TimeoutSeconds <= 0)
        {
            return null;
        }

        return new JsonObject
        {
            ["policy_id"] = requestBudget.PolicyId,
            ["timeout_seconds"] = requestBudget.TimeoutSeconds,
            ["provider_baseline_seconds"] = requestBudget.ProviderBaselineSeconds,
            ["execution_budget_size"] = requestBudget.ExecutionBudgetSize.ToString(),
            ["confidence_level"] = requestBudget.ConfidenceLevel.ToString(),
            ["max_duration_minutes"] = requestBudget.MaxDurationMinutes,
            ["validation_command_count"] = requestBudget.ValidationCommandCount,
            ["long_running_lane"] = requestBudget.LongRunningLane,
            ["repo_truth_guidance_required"] = requestBudget.RepoTruthGuidanceRequired,
            ["summary"] = requestBudget.Summary,
            ["rationale"] = requestBudget.Rationale,
            ["reasons"] = ToJsonArray(requestBudget.Reasons),
        };
    }

    private static string[] SplitDelimitedMetadata(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool? ParseNullableBoolean(string? value)
    {
        return bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static JsonObject BuildExecutionEvidenceNode(ExecutionEvidence evidence)
    {
        return new JsonObject
        {
            ["source"] = evidence.EvidenceSource.ToString(),
            ["completeness"] = evidence.EvidenceCompleteness.ToString(),
            ["strength"] = evidence.EvidenceStrength.ToString(),
            ["worker_id"] = evidence.WorkerId,
            ["started_at"] = evidence.StartedAt,
            ["ended_at"] = evidence.EndedAt,
            ["declared_scope_files"] = ToJsonArray(evidence.DeclaredScopeFiles),
            ["files_read"] = ToJsonArray(evidence.FilesRead),
            ["files_written"] = ToJsonArray(evidence.FilesWritten),
            ["commands_executed"] = ToJsonArray(evidence.CommandsExecuted),
            ["repo_root"] = evidence.RepoRoot,
            ["worktree_path"] = evidence.WorktreePath,
            ["base_commit"] = evidence.BaseCommit,
            ["requested_thread_id"] = evidence.RequestedThreadId,
            ["thread_id"] = evidence.ThreadId,
            ["thread_continuity"] = evidence.ThreadContinuity.ToString(),
            ["evidence_path"] = evidence.EvidencePath,
            ["command_log_ref"] = evidence.CommandLogRef,
            ["build_output_ref"] = evidence.BuildOutputRef,
            ["test_output_ref"] = evidence.TestOutputRef,
            ["patch_ref"] = evidence.PatchRef,
            ["artifacts"] = ToJsonArray(evidence.Artifacts),
            ["artifact_hashes"] = JsonSerializer.SerializeToNode(evidence.ArtifactHashes, JsonOptions),
            ["command_trace_hash"] = evidence.CommandTraceHash,
            ["patch_hash"] = evidence.PatchHash,
            ["exit_status"] = evidence.ExitStatus,
        };
    }

    private static JsonObject BuildRunSummaryNode(ExecutionRun run)
    {
        return new JsonObject
        {
            ["run_id"] = run.RunId,
            ["status"] = run.Status.ToString(),
            ["trigger_reason"] = run.TriggerReason.ToString(),
            ["current_step_index"] = run.CurrentStepIndex,
            ["current_step_title"] = run.Steps.Count == 0
                ? "(none)"
                : run.Steps[Math.Clamp(run.CurrentStepIndex, 0, run.Steps.Count - 1)].Title,
            ["selected_pack_summary"] = run.SelectedPack is null
                ? null
                : $"{run.SelectedPack.PackId}@{run.SelectedPack.PackVersion} ({run.SelectedPack.Channel})",
            ["result_envelope_path"] = run.ResultEnvelopePath,
            ["boundary_violation_path"] = run.BoundaryViolationPath,
            ["replan_artifact_path"] = run.ReplanArtifactPath,
        };
    }

    private ExecutionRunHistoricalExceptionService CreateExecutionRunHistoricalExceptionService()
    {
        return new ExecutionRunHistoricalExceptionService(
            services.Paths.RepoRoot,
            services.Paths,
            services.TaskGraphService,
            services.ExecutionRunService,
            services.ArtifactRepository);
    }

    private T? TryLoadJson<T>(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return default;
        }

        var resolved = Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(services.Paths.RepoRoot, path));
        if (!File.Exists(resolved))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(resolved), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch
        {
            return default;
        }
    }
}
