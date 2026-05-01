using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Safety;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Workers;

internal sealed class WorkerExecutionPipeline
{
    private readonly WorkerAdapterRegistry workerAdapterRegistry;
    private readonly WorkerExecutionBoundaryService boundaryService;
    private readonly WorkerPermissionOrchestrationService permissionOrchestrationService;
    private readonly WorkerFailureClassifier failureClassifier;

    public WorkerExecutionPipeline(
        WorkerAdapterRegistry workerAdapterRegistry,
        WorkerExecutionBoundaryService boundaryService,
        WorkerPermissionOrchestrationService permissionOrchestrationService,
        WorkerFailureClassifier failureClassifier)
    {
        this.workerAdapterRegistry = workerAdapterRegistry;
        this.boundaryService = boundaryService;
        this.permissionOrchestrationService = permissionOrchestrationService;
        this.failureClassifier = failureClassifier;
    }

    public WorkerExecutionResult Execute(WorkerRequest request, ICollection<string> evidence)
    {
        var result = ResolveWorkerExecution(request, evidence);
        result = EnforceRemoteApiMaterializationContract(request, result, evidence);
        return permissionOrchestrationService.Evaluate(request, result);
    }

    public WorkerExecutionResult NormalizeAfterValidation(WorkerExecutionResult result, WorkerValidationOutcome validationOutcome)
    {
        return failureClassifier.Normalize(result, validationOutcome);
    }

    private WorkerExecutionResult ResolveWorkerExecution(WorkerRequest request, ICollection<string> evidence)
    {
        var selectedBackendId = request.Selection?.SelectedBackendId ?? request.ExecutionRequest?.BackendHint;
        var workerAdapter = string.IsNullOrWhiteSpace(selectedBackendId)
            ? workerAdapterRegistry.ActiveAdapter
            : workerAdapterRegistry.Resolve(selectedBackendId);

        if (!request.Task.CanExecuteInWorker)
        {
            var skipped = WorkerExecutionResult.Skipped(
                request.Task.TaskId,
                workerAdapter.BackendId,
                workerAdapter.ProviderId,
                workerAdapter.AdapterId,
                request.ExecutionRequest?.Profile ?? WorkerExecutionProfile.UntrustedDefault,
                $"Task type '{request.Task.TaskType}' is governed outside the worker execution pipeline.",
                RequestPreview(request.ExecutionRequest),
                RequestHash(request.ExecutionRequest));
            evidence.Add($"worker skipped: {skipped.Summary}");
            return skipped;
        }

        if (request.ExecutionRequest is null)
        {
            return WorkerExecutionResult.Skipped(
                request.Task.TaskId,
                "none",
                "none",
                workerAdapter.AdapterId,
                WorkerExecutionProfile.UntrustedDefault,
                "No worker execution request was attached to the worker request.",
                string.Empty,
                string.Empty);
        }

        if (request.Selection is not null && !request.Selection.Allowed)
        {
            evidence.Add($"worker selection blocked: {request.Selection.Summary}");
            return WorkerExecutionResult.Blocked(
                request.Task.TaskId,
                request.Selection.SelectedBackendId ?? selectedBackendId ?? "none",
                request.Selection.SelectedProviderId ?? "none",
                request.Selection.SelectedAdapterId ?? workerAdapter.AdapterId,
                request.Selection.Profile ?? request.ExecutionRequest.Profile,
                WorkerFailureKind.EnvironmentBlocked,
                request.Selection.Summary,
                RequestPreview(request.ExecutionRequest),
                RequestHash(request.ExecutionRequest));
        }

        evidence.Add(SafetyLayerSemantics.FormatEvidence(SafetyLayerSemantics.PreExecutionBoundaryLayerId));
        var boundaryDecision = boundaryService.Evaluate(request.ExecutionRequest);
        evidence.Add($"worker boundary: {boundaryDecision.Reason}");
        if (!boundaryDecision.Allowed)
        {
            return WorkerExecutionResult.Blocked(
                request.Task.TaskId,
                workerAdapter.BackendId,
                workerAdapter.ProviderId,
                workerAdapter.AdapterId,
                request.ExecutionRequest.Profile,
                WorkerFailureKind.PolicyDenied,
                boundaryDecision.Reason,
                RequestPreview(request.ExecutionRequest),
                RequestHash(request.ExecutionRequest));
        }

        if (request.Session.DryRun)
        {
            var skipped = WorkerExecutionResult.Skipped(
                request.Task.TaskId,
                workerAdapter.BackendId,
                workerAdapter.ProviderId,
                workerAdapter.AdapterId,
                request.ExecutionRequest.Profile,
                "Dry-run mode skipped worker execution.",
                RequestPreview(request.ExecutionRequest),
                RequestHash(request.ExecutionRequest));
            evidence.Add($"worker skipped: {skipped.Summary}");
            return skipped;
        }

        WorkerExecutionResult result;
        try
        {
            result = workerAdapter.Execute(request.ExecutionRequest);
        }
        catch (Exception exception)
        {
            result = failureClassifier.FromException(request.ExecutionRequest, workerAdapter, exception);
        }

        result = WorkerCompletionClaimExtractor.Attach(request, result);
        evidence.Add(SafetyLayerSemantics.FormatEvidence(SafetyLayerSemantics.ChangeObservationLayerId));
        evidence.Add($"worker adapter reason: {workerAdapter.SelectionReason}");
        RecordRequestTelemetry(request, result);
        if (!string.IsNullOrWhiteSpace(result.Summary))
        {
            evidence.Add($"worker summary: {result.Summary}");
        }

        if (result.CompletionClaim.Required)
        {
            evidence.Add($"worker completion claim: status={result.CompletionClaim.Status}; present={FormatClaimFields(result.CompletionClaim.PresentFields)}; missing={FormatClaimFields(result.CompletionClaim.MissingFields)}");
        }

        if (!result.Succeeded && !string.IsNullOrWhiteSpace(result.FailureReason))
        {
            evidence.Add($"worker failure: {result.FailureReason}");
        }

        return result;
    }

    private static void RecordRequestTelemetry(WorkerRequest request, WorkerExecutionResult result)
    {
        if (request.ExecutionRequest?.RequestEnvelopeDraft is null)
        {
            return;
        }

        var paths = ControlPlanePaths.FromRepoRoot(request.Session.RepoRoot);
        var attributionService = new LlmRequestEnvelopeAttributionService(paths);
        var originalDraft = request.ExecutionRequest.RequestEnvelopeDraft;
        var draft = originalDraft with
        {
            RunId = string.IsNullOrWhiteSpace(result.RunId) ? originalDraft.RunId : result.RunId,
            TaskId = request.Task.TaskId,
            Model = string.IsNullOrWhiteSpace(result.Model) ? request.ExecutionRequest.ModelOverride ?? string.Empty : result.Model,
            Provider = string.IsNullOrWhiteSpace(result.ProviderId) ? originalDraft.Provider : result.ProviderId,
            ProviderApiVersion = string.IsNullOrWhiteSpace(result.RequestFamily) ? originalDraft.ProviderApiVersion : result.RequestFamily,
        };
        var usage = RuntimeTokenCapTruthResolver.Apply(
            new Carves.Runtime.Domain.AI.LlmRequestEnvelopeUsage
            {
                TokenAccountingSource = result.InputTokens.HasValue || result.OutputTokens.HasValue ? "provider_actual" : "local_estimate",
                ProviderReportedInputTokens = result.InputTokens,
                ProviderReportedUncachedInputTokens = result.InputTokens,
                ProviderReportedOutputTokens = result.OutputTokens,
                KnownProviderOverheadClass = result.InputTokens.HasValue ? "provider_serialization_delta" : null,
            },
            RuntimeTokenCapTruthResolver.FromMetadata(request.ExecutionRequest.Metadata));
        attributionService.Record(draft, usage);

        var contextPackPath = request.ExecutionRequest.Metadata.TryGetValue("context_pack_path", out var metadataPath)
            ? metadataPath
            : null;
        if (string.IsNullOrWhiteSpace(contextPackPath))
        {
            return;
        }

        var routeGraph = new RuntimeSurfaceRouteGraphService(paths);
        routeGraph.RecordRouteEdge(new Carves.Runtime.Domain.AI.RuntimeConsumerRouteEdgeRecord
        {
            SurfaceId = contextPackPath,
            Consumer = $"worker:{result.ProviderId}:{result.RequestFamily ?? "unknown"}",
            DeclaredRouteKind = "direct_to_llm",
            ObservedRouteKind = "direct_to_llm",
            ObservedCount = 1,
            SampleCount = 1,
            FrequencyWindow = "7d",
            RetrievalHitCount = 0,
            LlmReinjectionCount = 1,
            AverageFanout = 1,
            EvidenceSource = request.ExecutionRequest.RequestId,
            LastSeen = DateTimeOffset.UtcNow,
        });
    }

    private static WorkerExecutionResult EnforceRemoteApiMaterializationContract(
        WorkerRequest request,
        WorkerExecutionResult result,
        ICollection<string> evidence)
    {
        if (!ShouldRejectNarrativeOnlyRemoteApiSuccess(request, result))
        {
            return result;
        }

        const string summary = "Remote API worker returned narrative output but did not materialize changed files or submit_result evidence for a patch-capable execution run.";
        evidence.Add($"worker materialization contract failed: {summary}");
        return result with
        {
            Status = WorkerExecutionStatus.Failed,
            FailureKind = WorkerFailureKind.InvalidOutput,
            FailureLayer = WorkerFailureLayer.Protocol,
            Retryable = false,
            Summary = summary,
            FailureReason = summary,
        };
    }

    private static bool ShouldRejectNarrativeOnlyRemoteApiSuccess(WorkerRequest request, WorkerExecutionResult result)
    {
        if (request.Session.DryRun
            || !result.Succeeded
            || result.ChangedFiles.Count > 0
            || !IsRemoteApiResult(result))
        {
            return false;
        }

        if (AllowsNarrativeOnlyRemoteApiSuccess(request))
        {
            return false;
        }

        return true;
    }

    private static bool AllowsNarrativeOnlyRemoteApiSuccess(WorkerRequest request)
    {
        var routingIntent = request.ExecutionRequest?.RoutingIntent
            ?? request.Selection?.RoutingIntent
            ?? request.Session.WorkerRoutingIntent;
        if (routingIntent is not null
            && routingIntent is "reasoning_summary" or "failure_summary" or "review_summary" or "structured_output")
        {
            return true;
        }

        return request.Task.Scope.Count > 0 && request.Task.Scope.All(IsControlPlaneAssessmentScope);
    }

    private static bool IsRemoteApiResult(WorkerExecutionResult result)
    {
        if (result.CommandTrace.Any(item => string.Equals(item.Category, "remote_api", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return result.ProtocolFamily is "openai_compatible" or "anthropic_native" or "gemini_native";
    }

    private static bool IsControlPlaneAssessmentScope(string scopePath)
    {
        var normalized = scopePath
            .Trim()
            .Trim('`')
            .Replace('\\', '/');
        return normalized.StartsWith(".ai/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(normalized, ".ai", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith("carves://truth/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RequestPreview(WorkerExecutionRequest? request)
    {
        if (request is null)
        {
            return string.Empty;
        }

        return request.Input[..Math.Min(160, request.Input.Length)];
    }

    private static string RequestHash(WorkerExecutionRequest? request)
    {
        if (request is null)
        {
            return string.Empty;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Input))).ToLowerInvariant();
    }

    private static string FormatClaimFields(IReadOnlyList<string> fields)
    {
        return fields.Count == 0 ? "(none)" : string.Join("|", fields);
    }
}
