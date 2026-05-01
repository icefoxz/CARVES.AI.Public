using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Host;

internal static class RuntimeTokenBaselineCommandSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public static OperatorCommandResult Run(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: runtime token-baseline recollect-worker --task-ids <csv> [--cohort-id <id>] [--result-date <yyyy-MM-dd>] | runtime token-baseline recompute (--cohort-file <path> | --cohort-id <id> --window-start <utc> --window-end <utc> --request-kinds <csv>) [--token-accounting-source-policy <policy>] [--context-window-view <id>] [--billable-cost-view <id>] [--result-date <yyyy-MM-dd>] | runtime token-baseline phase10-target-decision --result-date <yyyy-MM-dd> | runtime token-baseline wrapper-policy-inventory --result-date <yyyy-MM-dd> | runtime token-baseline wrapper-policy-invariant-manifest --result-date <yyyy-MM-dd> | runtime token-baseline wrapper-offline-validator --result-date <yyyy-MM-dd> | runtime token-baseline wrapper-candidate --result-date <yyyy-MM-dd> | runtime token-baseline manual-review-resolution --result-date <yyyy-MM-dd> | runtime token-baseline request-kind-slice-proof --result-date <yyyy-MM-dd> | runtime token-baseline rollback-plan-freeze --result-date <yyyy-MM-dd> | runtime token-baseline non-inferiority-cohort-freeze --result-date <yyyy-MM-dd> | runtime token-baseline active-canary-readiness-review --result-date <yyyy-MM-dd> | runtime token-baseline active-canary-approval-review --result-date <yyyy-MM-dd> | runtime token-baseline active-canary-execution-approval --result-date <yyyy-MM-dd> | runtime token-baseline active-canary-result --result-date <yyyy-MM-dd> | runtime token-baseline active-canary-result-review --result-date <yyyy-MM-dd> | runtime token-baseline post-canary-gate --result-date <yyyy-MM-dd> | runtime token-baseline main-path-replacement-review --result-date <yyyy-MM-dd> | runtime token-baseline replacement-scope-freeze --result-date <yyyy-MM-dd> | runtime token-baseline post-rollout-evidence-collection --result-date <yyyy-MM-dd> | runtime token-baseline post-rollout-audit-gate --result-date <yyyy-MM-dd>");
        }

        return arguments[0] switch
        {
            "recollect-worker" => RunRecollectWorker(services, arguments.Skip(1).ToArray()),
            "recompute" => RunRecompute(services, arguments.Skip(1).ToArray()),
            "phase10-target-decision" => RunPhase10TargetDecision(services, arguments.Skip(1).ToArray()),
            "wrapper-policy-inventory" => RunWrapperPolicyInventory(services, arguments.Skip(1).ToArray()),
            "wrapper-policy-invariant-manifest" => RunWrapperPolicyInvariantManifest(services, arguments.Skip(1).ToArray()),
            "wrapper-offline-validator" => RunWrapperOfflineValidator(services, arguments.Skip(1).ToArray()),
            "wrapper-candidate" => RunWrapperCandidate(services, arguments.Skip(1).ToArray()),
            "manual-review-resolution" => RunManualReviewResolution(services, arguments.Skip(1).ToArray()),
            "request-kind-slice-proof" => RunRequestKindSliceProof(services, arguments.Skip(1).ToArray()),
            "rollback-plan-freeze" => RunRollbackPlanFreeze(services, arguments.Skip(1).ToArray()),
            "non-inferiority-cohort-freeze" => RunNonInferiorityCohortFreeze(services, arguments.Skip(1).ToArray()),
            "active-canary-readiness-review" => RunActiveCanaryReadinessReview(services, arguments.Skip(1).ToArray()),
            "active-canary-approval-review" => RunActiveCanaryApprovalReview(services, arguments.Skip(1).ToArray()),
            "active-canary-execution-approval" => RunActiveCanaryExecutionApproval(services, arguments.Skip(1).ToArray()),
            "active-canary-result" => RunActiveCanaryResult(services, arguments.Skip(1).ToArray()),
            "active-canary-result-review" => RunActiveCanaryResultReview(services, arguments.Skip(1).ToArray()),
            "post-canary-gate" => RunPostCanaryGate(services, arguments.Skip(1).ToArray()),
            "main-path-replacement-review" => RunMainPathReplacementReview(services, arguments.Skip(1).ToArray()),
            "replacement-scope-freeze" => RunReplacementScopeFreeze(services, arguments.Skip(1).ToArray()),
            "post-rollout-evidence-collection" => RunPostRolloutEvidenceCollection(services, arguments.Skip(1).ToArray()),
            "post-rollout-audit-gate" => RunPostRolloutAuditGate(services, arguments.Skip(1).ToArray()),
            _ => OperatorCommandResult.Failure("Usage: runtime token-baseline recollect-worker --task-ids <csv> [--cohort-id <id>] [--result-date <yyyy-MM-dd>] | runtime token-baseline recompute (--cohort-file <path> | --cohort-id <id> --window-start <utc> --window-end <utc> --request-kinds <csv>) [--token-accounting-source-policy <policy>] [--context-window-view <id>] [--billable-cost-view <id>] [--result-date <yyyy-MM-dd>] | runtime token-baseline phase10-target-decision --result-date <yyyy-MM-dd> | runtime token-baseline wrapper-policy-inventory --result-date <yyyy-MM-dd> | runtime token-baseline wrapper-policy-invariant-manifest --result-date <yyyy-MM-dd> | runtime token-baseline wrapper-offline-validator --result-date <yyyy-MM-dd> | runtime token-baseline wrapper-candidate --result-date <yyyy-MM-dd> | runtime token-baseline manual-review-resolution --result-date <yyyy-MM-dd> | runtime token-baseline request-kind-slice-proof --result-date <yyyy-MM-dd> | runtime token-baseline rollback-plan-freeze --result-date <yyyy-MM-dd> | runtime token-baseline non-inferiority-cohort-freeze --result-date <yyyy-MM-dd> | runtime token-baseline active-canary-readiness-review --result-date <yyyy-MM-dd> | runtime token-baseline active-canary-approval-review --result-date <yyyy-MM-dd> | runtime token-baseline active-canary-execution-approval --result-date <yyyy-MM-dd> | runtime token-baseline active-canary-result --result-date <yyyy-MM-dd> | runtime token-baseline active-canary-result-review --result-date <yyyy-MM-dd> | runtime token-baseline post-canary-gate --result-date <yyyy-MM-dd> | runtime token-baseline main-path-replacement-review --result-date <yyyy-MM-dd> | runtime token-baseline replacement-scope-freeze --result-date <yyyy-MM-dd> | runtime token-baseline post-rollout-evidence-collection --result-date <yyyy-MM-dd> | runtime token-baseline post-rollout-audit-gate --result-date <yyyy-MM-dd>"),
        };
    }

    private static OperatorCommandResult RunRecollectWorker(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var taskIdsValue = ResolveOption(arguments, "--task-ids");
            if (string.IsNullOrWhiteSpace(taskIdsValue))
            {
                throw new InvalidOperationException("Usage: runtime token-baseline recollect-worker --task-ids <csv> [--cohort-id <id>] [--result-date <yyyy-MM-dd>]");
            }

            var taskIds = taskIdsValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (taskIds.Length == 0)
            {
                throw new InvalidOperationException("Usage: runtime token-baseline recollect-worker --task-ids <csv> [--cohort-id <id>] [--result-date <yyyy-MM-dd>]");
            }

            var resultDate = ResolveResultDate(arguments) ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var cohortId = ResolveOption(arguments, "--cohort-id")
                           ?? $"phase_0a_worker_recollect_{resultDate:yyyyMMdd}";
            var workerConfig = services.AiProviderConfig.ResolveForRole("worker");
            var service = new RuntimeTokenBaselineWorkerRecollectService(
                services.Paths,
                services.SystemConfig.RepoName,
                workerConfig,
                services.GitClient,
                new JsonTaskGraphRepository(services.Paths),
                new WorkerAiRequestFactory(
                    workerConfig.MaxOutputTokens,
                    workerConfig.RequestTimeoutSeconds,
                    workerConfig.Model,
                    workerConfig.ReasoningEffort),
                new LlmRequestEnvelopeAttributionService(services.Paths),
                new RuntimeSurfaceRouteGraphService(services.Paths));
            var result = service.Persist(taskIds, cohortId, resultDate);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunRecompute(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var cohort = ResolveCohort(services.Paths, arguments);
            var resultDate = ResolveResultDate(arguments) ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var formatterService = new RuntimeTokenBaselineEvidenceResultFormatterService(
                services.Paths,
                new RuntimeTokenBaselineAggregatorService(services.Paths),
                new RuntimeTokenOutcomeBinderService(
                    new LlmRequestEnvelopeAttributionService(services.Paths),
                    new JsonTaskGraphRepository(services.Paths),
                    new ExecutionRunReportService(services.Paths)));
            var service = new RuntimeTokenBaselineRecomputeService(
                services.Paths,
                formatterService,
                new RuntimeTokenBaselineReadinessGateService(services.Paths),
                new RuntimeTokenBaselineTrustLineService(services.Paths));
            var result = service.Persist(cohort, resultDate);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunPhase10TargetDecision(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline phase10-target-decision --result-date <yyyy-MM-dd>");
            var evidenceJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-0a",
                $"attribution-baseline-evidence-result-{resultDate:yyyy-MM-dd}.json");
            var trustJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-0a",
                $"trust-line-result-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(evidenceJsonPath))
            {
                throw new InvalidOperationException($"Phase 1.0 target decision requires evidence result '{evidenceJsonPath}'.");
            }

            if (!File.Exists(trustJsonPath))
            {
                throw new InvalidOperationException($"Phase 1.0 target decision requires trust line result '{trustJsonPath}'.");
            }

            var evidenceResult = JsonSerializer.Deserialize<RuntimeTokenBaselineEvidenceResult>(File.ReadAllText(evidenceJsonPath), JsonOptions)
                                 ?? throw new InvalidOperationException($"Evidence result '{evidenceJsonPath}' could not be deserialized.");
            var trustLineResult = JsonSerializer.Deserialize<RuntimeTokenBaselineTrustLineResult>(File.ReadAllText(trustJsonPath), JsonOptions)
                                  ?? throw new InvalidOperationException($"Trust line result '{trustJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenPhase10TargetDecisionService(services.Paths);
            var result = service.Persist(evidenceResult, trustLineResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunWrapperPolicyInventory(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline wrapper-policy-inventory --result-date <yyyy-MM-dd>");
            var evidenceJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-0a",
                $"attribution-baseline-evidence-result-{resultDate:yyyy-MM-dd}.json");
            var trustJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-0a",
                $"trust-line-result-{resultDate:yyyy-MM-dd}.json");
            var phase10JsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"target-decision-result-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(evidenceJsonPath))
            {
                throw new InvalidOperationException($"Wrapper policy inventory requires evidence result '{evidenceJsonPath}'.");
            }

            if (!File.Exists(trustJsonPath))
            {
                throw new InvalidOperationException($"Wrapper policy inventory requires trust line result '{trustJsonPath}'.");
            }

            if (!File.Exists(phase10JsonPath))
            {
                throw new InvalidOperationException($"Wrapper policy inventory requires Phase 1.0 target decision result '{phase10JsonPath}'.");
            }

            var evidenceResult = JsonSerializer.Deserialize<RuntimeTokenBaselineEvidenceResult>(File.ReadAllText(evidenceJsonPath), JsonOptions)
                                 ?? throw new InvalidOperationException($"Evidence result '{evidenceJsonPath}' could not be deserialized.");
            var trustLineResult = JsonSerializer.Deserialize<RuntimeTokenBaselineTrustLineResult>(File.ReadAllText(trustJsonPath), JsonOptions)
                                  ?? throw new InvalidOperationException($"Trust line result '{trustJsonPath}' could not be deserialized.");
            var phase10Result = JsonSerializer.Deserialize<RuntimeTokenPhase10TargetDecisionResult>(File.ReadAllText(phase10JsonPath), JsonOptions)
                                ?? throw new InvalidOperationException($"Phase 1.0 target decision result '{phase10JsonPath}' could not be deserialized.");
            var service = new RuntimeTokenWrapperPolicyInventoryService(services.Paths);
            var result = service.Persist(evidenceResult, trustLineResult, phase10Result);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunWrapperPolicyInvariantManifest(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline wrapper-policy-invariant-manifest --result-date <yyyy-MM-dd>");
            var inventoryJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-policy-inventory-result-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(inventoryJsonPath))
            {
                throw new InvalidOperationException($"Wrapper policy invariant manifest requires wrapper inventory result '{inventoryJsonPath}'.");
            }

            var inventoryResult = JsonSerializer.Deserialize<RuntimeTokenWrapperPolicyInventoryResult>(File.ReadAllText(inventoryJsonPath), JsonOptions)
                                  ?? throw new InvalidOperationException($"Wrapper policy inventory result '{inventoryJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenWrapperPolicyInvariantManifestService(services.Paths);
            var result = service.Persist(inventoryResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunWrapperOfflineValidator(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline wrapper-offline-validator --result-date <yyyy-MM-dd>");
            var manifestJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-policy-invariant-manifest-{resultDate:yyyy-MM-dd}.json");
            var inventoryJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-policy-inventory-result-{resultDate:yyyy-MM-dd}.json");
            var workerRecollectJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-0a",
                $"worker-recollect-result-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(manifestJsonPath))
            {
                throw new InvalidOperationException($"Wrapper offline validator requires wrapper invariant manifest '{manifestJsonPath}'.");
            }

            if (!File.Exists(inventoryJsonPath))
            {
                throw new InvalidOperationException($"Wrapper offline validator requires wrapper inventory result '{inventoryJsonPath}'.");
            }

            if (!File.Exists(workerRecollectJsonPath))
            {
                throw new InvalidOperationException($"Wrapper offline validator requires worker recollect result '{workerRecollectJsonPath}'.");
            }

            var manifestResult = JsonSerializer.Deserialize<RuntimeTokenWrapperPolicyInvariantManifestResult>(File.ReadAllText(manifestJsonPath), JsonOptions)
                                 ?? throw new InvalidOperationException($"Wrapper invariant manifest '{manifestJsonPath}' could not be deserialized.");
            var inventoryResult = JsonSerializer.Deserialize<RuntimeTokenWrapperPolicyInventoryResult>(File.ReadAllText(inventoryJsonPath), JsonOptions)
                                  ?? throw new InvalidOperationException($"Wrapper inventory result '{inventoryJsonPath}' could not be deserialized.");
            var workerRecollectResult = JsonSerializer.Deserialize<RuntimeTokenBaselineWorkerRecollectResult>(File.ReadAllText(workerRecollectJsonPath), JsonOptions)
                                        ?? throw new InvalidOperationException($"Worker recollect result '{workerRecollectJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenWrapperOfflineValidatorService(services.Paths);
            var result = service.Persist(manifestResult, inventoryResult, workerRecollectResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunWrapperCandidate(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline wrapper-candidate --result-date <yyyy-MM-dd>");
            var manifestJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-policy-invariant-manifest-{resultDate:yyyy-MM-dd}.json");
            var validatorJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-offline-validator-result-{resultDate:yyyy-MM-dd}.json");
            var workerRecollectJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-0a",
                $"worker-recollect-result-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(manifestJsonPath))
            {
                throw new InvalidOperationException($"Wrapper candidate requires wrapper invariant manifest '{manifestJsonPath}'.");
            }

            if (!File.Exists(validatorJsonPath))
            {
                throw new InvalidOperationException($"Wrapper candidate requires wrapper offline validator result '{validatorJsonPath}'.");
            }

            if (!File.Exists(workerRecollectJsonPath))
            {
                throw new InvalidOperationException($"Wrapper candidate requires worker recollect result '{workerRecollectJsonPath}'.");
            }

            var manifestResult = JsonSerializer.Deserialize<RuntimeTokenWrapperPolicyInvariantManifestResult>(File.ReadAllText(manifestJsonPath), JsonOptions)
                                 ?? throw new InvalidOperationException($"Wrapper invariant manifest '{manifestJsonPath}' could not be deserialized.");
            var validatorResult = JsonSerializer.Deserialize<RuntimeTokenWrapperOfflineValidatorResult>(File.ReadAllText(validatorJsonPath), JsonOptions)
                                  ?? throw new InvalidOperationException($"Wrapper offline validator result '{validatorJsonPath}' could not be deserialized.");
            var workerRecollectResult = JsonSerializer.Deserialize<RuntimeTokenBaselineWorkerRecollectResult>(File.ReadAllText(workerRecollectJsonPath), JsonOptions)
                                        ?? throw new InvalidOperationException($"Worker recollect result '{workerRecollectJsonPath}' could not be deserialized.");
            var workerConfig = services.AiProviderConfig.ResolveForRole("worker");
            var service = new RuntimeTokenWrapperCandidateService(
                services.Paths,
                services.SystemConfig.RepoName,
                workerConfig,
                services.GitClient,
                new JsonTaskGraphRepository(services.Paths),
                new WorkerAiRequestFactory(
                    workerConfig.MaxOutputTokens,
                    workerConfig.RequestTimeoutSeconds,
                    workerConfig.Model,
                    workerConfig.ReasoningEffort));
            var result = service.Persist(manifestResult, validatorResult, workerRecollectResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunActiveCanaryReadinessReview(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline active-canary-readiness-review --result-date <yyyy-MM-dd>");
            var candidateJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-candidate-result-{resultDate:yyyy-MM-dd}.json");
            var reviewBundleJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"enter-active-canary-review-bundle-{resultDate:yyyy-MM-dd}.json");
            var manifestJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-policy-invariant-manifest-{resultDate:yyyy-MM-dd}.json");
            var manualReviewResolutionJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"manual-review-resolution-{resultDate:yyyy-MM-dd}.json");
            var requestKindSliceProofJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"wrapper-request-kind-slice-proof-{resultDate:yyyy-MM-dd}.json");
            var rollbackPlanJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"wrapper-canary-rollback-plan-{resultDate:yyyy-MM-dd}.json");
            var nonInferiorityCohortJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"non-inferiority-cohort-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(candidateJsonPath))
            {
                throw new InvalidOperationException($"Active canary readiness review requires wrapper candidate result '{candidateJsonPath}'.");
            }

            if (!File.Exists(reviewBundleJsonPath))
            {
                throw new InvalidOperationException($"Active canary readiness review requires enter-active-canary review bundle '{reviewBundleJsonPath}'.");
            }

            if (!File.Exists(manifestJsonPath))
            {
                throw new InvalidOperationException($"Active canary readiness review requires wrapper invariant manifest '{manifestJsonPath}'.");
            }

            var candidateResult = JsonSerializer.Deserialize<RuntimeTokenWrapperCandidateResult>(File.ReadAllText(candidateJsonPath), JsonOptions)
                                  ?? throw new InvalidOperationException($"Wrapper candidate result '{candidateJsonPath}' could not be deserialized.");
            var reviewBundle = JsonSerializer.Deserialize<RuntimeTokenWrapperEnterActiveCanaryReviewBundle>(File.ReadAllText(reviewBundleJsonPath), JsonOptions)
                               ?? throw new InvalidOperationException($"Enter-active-canary review bundle '{reviewBundleJsonPath}' could not be deserialized.");
            var manifestResult = JsonSerializer.Deserialize<RuntimeTokenWrapperPolicyInvariantManifestResult>(File.ReadAllText(manifestJsonPath), JsonOptions)
                                 ?? throw new InvalidOperationException($"Wrapper invariant manifest '{manifestJsonPath}' could not be deserialized.");
            RuntimeTokenPhase2ManualReviewResolutionResult? manualReviewResolutionResult = null;
            if (File.Exists(manualReviewResolutionJsonPath))
            {
                manualReviewResolutionResult = JsonSerializer.Deserialize<RuntimeTokenPhase2ManualReviewResolutionResult>(File.ReadAllText(manualReviewResolutionJsonPath), JsonOptions)
                                             ?? throw new InvalidOperationException($"Manual review resolution '{manualReviewResolutionJsonPath}' could not be deserialized.");
            }
            RuntimeTokenPhase2RequestKindSliceProofResult? requestKindSliceProofResult = null;
            if (File.Exists(requestKindSliceProofJsonPath))
            {
                requestKindSliceProofResult = JsonSerializer.Deserialize<RuntimeTokenPhase2RequestKindSliceProofResult>(File.ReadAllText(requestKindSliceProofJsonPath), JsonOptions)
                                             ?? throw new InvalidOperationException($"Request-kind slice proof '{requestKindSliceProofJsonPath}' could not be deserialized.");
            }
            RuntimeTokenPhase2RollbackPlanFreezeResult? rollbackPlanFreezeResult = null;
            if (File.Exists(rollbackPlanJsonPath))
            {
                rollbackPlanFreezeResult = JsonSerializer.Deserialize<RuntimeTokenPhase2RollbackPlanFreezeResult>(File.ReadAllText(rollbackPlanJsonPath), JsonOptions)
                                           ?? throw new InvalidOperationException($"Rollback plan '{rollbackPlanJsonPath}' could not be deserialized.");
            }
            RuntimeTokenPhase2NonInferiorityCohortFreezeResult? nonInferiorityCohortFreezeResult = null;
            if (File.Exists(nonInferiorityCohortJsonPath))
            {
                nonInferiorityCohortFreezeResult = JsonSerializer.Deserialize<RuntimeTokenPhase2NonInferiorityCohortFreezeResult>(File.ReadAllText(nonInferiorityCohortJsonPath), JsonOptions)
                                                  ?? throw new InvalidOperationException($"Non-inferiority cohort '{nonInferiorityCohortJsonPath}' could not be deserialized.");
            }
            var service = new RuntimeTokenPhase2ActiveCanaryReadinessReviewService(services.Paths);
            var result = service.Persist(candidateResult, reviewBundle, manifestResult, manualReviewResolutionResult, requestKindSliceProofResult, rollbackPlanFreezeResult, nonInferiorityCohortFreezeResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunManualReviewResolution(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline manual-review-resolution --result-date <yyyy-MM-dd>");
            var candidateJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-candidate-result-{resultDate:yyyy-MM-dd}.json");
            var reviewBundleJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"enter-active-canary-review-bundle-{resultDate:yyyy-MM-dd}.json");
            var manifestJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-policy-invariant-manifest-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(candidateJsonPath))
            {
                throw new InvalidOperationException($"Manual review resolution requires wrapper candidate result '{candidateJsonPath}'.");
            }

            if (!File.Exists(reviewBundleJsonPath))
            {
                throw new InvalidOperationException($"Manual review resolution requires enter-active-canary review bundle '{reviewBundleJsonPath}'.");
            }

            if (!File.Exists(manifestJsonPath))
            {
                throw new InvalidOperationException($"Manual review resolution requires wrapper invariant manifest '{manifestJsonPath}'.");
            }

            var candidateResult = JsonSerializer.Deserialize<RuntimeTokenWrapperCandidateResult>(File.ReadAllText(candidateJsonPath), JsonOptions)
                                  ?? throw new InvalidOperationException($"Wrapper candidate result '{candidateJsonPath}' could not be deserialized.");
            var reviewBundle = JsonSerializer.Deserialize<RuntimeTokenWrapperEnterActiveCanaryReviewBundle>(File.ReadAllText(reviewBundleJsonPath), JsonOptions)
                               ?? throw new InvalidOperationException($"Enter-active-canary review bundle '{reviewBundleJsonPath}' could not be deserialized.");
            var manifestResult = JsonSerializer.Deserialize<RuntimeTokenWrapperPolicyInvariantManifestResult>(File.ReadAllText(manifestJsonPath), JsonOptions)
                                 ?? throw new InvalidOperationException($"Wrapper invariant manifest '{manifestJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenPhase2ManualReviewResolutionService(services.Paths);
            var result = service.Persist(candidateResult, reviewBundle, manifestResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunRequestKindSliceProof(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline request-kind-slice-proof --result-date <yyyy-MM-dd>");
            var candidateJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-candidate-result-{resultDate:yyyy-MM-dd}.json");
            var manifestJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-policy-invariant-manifest-{resultDate:yyyy-MM-dd}.json");
            var manualReviewResolutionJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"manual-review-resolution-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(candidateJsonPath))
            {
                throw new InvalidOperationException($"Request-kind slice proof requires wrapper candidate result '{candidateJsonPath}'.");
            }

            if (!File.Exists(manifestJsonPath))
            {
                throw new InvalidOperationException($"Request-kind slice proof requires wrapper invariant manifest '{manifestJsonPath}'.");
            }

            if (!File.Exists(manualReviewResolutionJsonPath))
            {
                throw new InvalidOperationException($"Request-kind slice proof requires manual review resolution '{manualReviewResolutionJsonPath}'.");
            }

            var candidateResult = JsonSerializer.Deserialize<RuntimeTokenWrapperCandidateResult>(File.ReadAllText(candidateJsonPath), JsonOptions)
                                  ?? throw new InvalidOperationException($"Wrapper candidate result '{candidateJsonPath}' could not be deserialized.");
            var manifestResult = JsonSerializer.Deserialize<RuntimeTokenWrapperPolicyInvariantManifestResult>(File.ReadAllText(manifestJsonPath), JsonOptions)
                                 ?? throw new InvalidOperationException($"Wrapper invariant manifest '{manifestJsonPath}' could not be deserialized.");
            var manualReviewResolutionResult = JsonSerializer.Deserialize<RuntimeTokenPhase2ManualReviewResolutionResult>(File.ReadAllText(manualReviewResolutionJsonPath), JsonOptions)
                                               ?? throw new InvalidOperationException($"Manual review resolution '{manualReviewResolutionJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenPhase2RequestKindSliceProofService(services.Paths);
            var result = service.Persist(candidateResult, manifestResult, manualReviewResolutionResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunRollbackPlanFreeze(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline rollback-plan-freeze --result-date <yyyy-MM-dd>");
            var candidateJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-candidate-result-{resultDate:yyyy-MM-dd}.json");
            var requestKindSliceProofJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"wrapper-request-kind-slice-proof-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(candidateJsonPath))
            {
                throw new InvalidOperationException($"Rollback plan freeze requires wrapper candidate result '{candidateJsonPath}'.");
            }

            if (!File.Exists(requestKindSliceProofJsonPath))
            {
                throw new InvalidOperationException($"Rollback plan freeze requires request-kind slice proof '{requestKindSliceProofJsonPath}'.");
            }

            var candidateResult = JsonSerializer.Deserialize<RuntimeTokenWrapperCandidateResult>(File.ReadAllText(candidateJsonPath), JsonOptions)
                                  ?? throw new InvalidOperationException($"Wrapper candidate result '{candidateJsonPath}' could not be deserialized.");
            var requestKindSliceProofResult = JsonSerializer.Deserialize<RuntimeTokenPhase2RequestKindSliceProofResult>(File.ReadAllText(requestKindSliceProofJsonPath), JsonOptions)
                                             ?? throw new InvalidOperationException($"Request-kind slice proof '{requestKindSliceProofJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenPhase2RollbackPlanFreezeService(services.Paths);
            var result = service.Persist(candidateResult, requestKindSliceProofResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunNonInferiorityCohortFreeze(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline non-inferiority-cohort-freeze --result-date <yyyy-MM-dd>");
            var candidateJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-candidate-result-{resultDate:yyyy-MM-dd}.json");
            var requestKindSliceProofJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"wrapper-request-kind-slice-proof-{resultDate:yyyy-MM-dd}.json");
            var rollbackPlanJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"wrapper-canary-rollback-plan-{resultDate:yyyy-MM-dd}.json");
            var workerRecollectJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-0a",
                $"worker-recollect-result-{resultDate:yyyy-MM-dd}.json");
            var trustJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-0a",
                $"trust-line-result-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(candidateJsonPath))
            {
                throw new InvalidOperationException($"Non-inferiority cohort freeze requires wrapper candidate result '{candidateJsonPath}'.");
            }

            if (!File.Exists(requestKindSliceProofJsonPath))
            {
                throw new InvalidOperationException($"Non-inferiority cohort freeze requires request-kind slice proof '{requestKindSliceProofJsonPath}'.");
            }

            if (!File.Exists(rollbackPlanJsonPath))
            {
                throw new InvalidOperationException($"Non-inferiority cohort freeze requires rollback plan '{rollbackPlanJsonPath}'.");
            }

            if (!File.Exists(workerRecollectJsonPath))
            {
                throw new InvalidOperationException($"Non-inferiority cohort freeze requires worker recollect result '{workerRecollectJsonPath}'.");
            }

            if (!File.Exists(trustJsonPath))
            {
                throw new InvalidOperationException($"Non-inferiority cohort freeze requires trust line result '{trustJsonPath}'.");
            }

            var candidateResult = JsonSerializer.Deserialize<RuntimeTokenWrapperCandidateResult>(File.ReadAllText(candidateJsonPath), JsonOptions)
                                  ?? throw new InvalidOperationException($"Wrapper candidate result '{candidateJsonPath}' could not be deserialized.");
            var requestKindSliceProofResult = JsonSerializer.Deserialize<RuntimeTokenPhase2RequestKindSliceProofResult>(File.ReadAllText(requestKindSliceProofJsonPath), JsonOptions)
                                             ?? throw new InvalidOperationException($"Request-kind slice proof '{requestKindSliceProofJsonPath}' could not be deserialized.");
            var rollbackPlanFreezeResult = JsonSerializer.Deserialize<RuntimeTokenPhase2RollbackPlanFreezeResult>(File.ReadAllText(rollbackPlanJsonPath), JsonOptions)
                                           ?? throw new InvalidOperationException($"Rollback plan '{rollbackPlanJsonPath}' could not be deserialized.");
            var workerRecollectResult = JsonSerializer.Deserialize<RuntimeTokenBaselineWorkerRecollectResult>(File.ReadAllText(workerRecollectJsonPath), JsonOptions)
                                        ?? throw new InvalidOperationException($"Worker recollect result '{workerRecollectJsonPath}' could not be deserialized.");
            var trustLineResult = JsonSerializer.Deserialize<RuntimeTokenBaselineTrustLineResult>(File.ReadAllText(trustJsonPath), JsonOptions)
                                  ?? throw new InvalidOperationException($"Trust line result '{trustJsonPath}' could not be deserialized.");
            var attributionService = new LlmRequestEnvelopeAttributionService(services.Paths);
            var attributionLookup = attributionService.ListAll()
                .ToDictionary(item => item.AttributionId, StringComparer.Ordinal);
            var attributionRecords = workerRecollectResult.AttributionIds
                .Select(attributionId => attributionLookup.TryGetValue(attributionId, out var record)
                    ? record
                    : throw new InvalidOperationException($"Non-inferiority cohort freeze could not find attribution '{attributionId}'."))
                .ToArray();
            var service = new RuntimeTokenPhase2NonInferiorityCohortFreezeService(services.Paths);
            var result = service.Persist(candidateResult, requestKindSliceProofResult, rollbackPlanFreezeResult, workerRecollectResult, trustLineResult, attributionRecords);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunActiveCanaryApprovalReview(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline active-canary-approval-review --result-date <yyyy-MM-dd>");
            var readinessReviewJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"active-canary-readiness-review-{resultDate:yyyy-MM-dd}.json");
            var candidateJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"wrapper-candidate-result-{resultDate:yyyy-MM-dd}.json");
            var reviewBundleJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-1",
                $"enter-active-canary-review-bundle-{resultDate:yyyy-MM-dd}.json");
            var rollbackPlanJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"wrapper-canary-rollback-plan-{resultDate:yyyy-MM-dd}.json");
            var nonInferiorityCohortJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"non-inferiority-cohort-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(readinessReviewJsonPath))
            {
                throw new InvalidOperationException($"Active canary approval review requires readiness review '{readinessReviewJsonPath}'.");
            }

            if (!File.Exists(candidateJsonPath))
            {
                throw new InvalidOperationException($"Active canary approval review requires wrapper candidate result '{candidateJsonPath}'.");
            }

            if (!File.Exists(reviewBundleJsonPath))
            {
                throw new InvalidOperationException($"Active canary approval review requires enter-active-canary review bundle '{reviewBundleJsonPath}'.");
            }

            if (!File.Exists(rollbackPlanJsonPath))
            {
                throw new InvalidOperationException($"Active canary approval review requires rollback plan '{rollbackPlanJsonPath}'.");
            }

            if (!File.Exists(nonInferiorityCohortJsonPath))
            {
                throw new InvalidOperationException($"Active canary approval review requires non-inferiority cohort '{nonInferiorityCohortJsonPath}'.");
            }

            var readinessReviewResult = JsonSerializer.Deserialize<RuntimeTokenPhase2ActiveCanaryReadinessReviewResult>(File.ReadAllText(readinessReviewJsonPath), JsonOptions)
                                        ?? throw new InvalidOperationException($"Active canary readiness review '{readinessReviewJsonPath}' could not be deserialized.");
            var candidateResult = JsonSerializer.Deserialize<RuntimeTokenWrapperCandidateResult>(File.ReadAllText(candidateJsonPath), JsonOptions)
                                  ?? throw new InvalidOperationException($"Wrapper candidate result '{candidateJsonPath}' could not be deserialized.");
            var reviewBundle = JsonSerializer.Deserialize<RuntimeTokenWrapperEnterActiveCanaryReviewBundle>(File.ReadAllText(reviewBundleJsonPath), JsonOptions)
                               ?? throw new InvalidOperationException($"Enter-active-canary review bundle '{reviewBundleJsonPath}' could not be deserialized.");
            var rollbackPlanFreezeResult = JsonSerializer.Deserialize<RuntimeTokenPhase2RollbackPlanFreezeResult>(File.ReadAllText(rollbackPlanJsonPath), JsonOptions)
                                           ?? throw new InvalidOperationException($"Rollback plan '{rollbackPlanJsonPath}' could not be deserialized.");
            var nonInferiorityCohortFreezeResult = JsonSerializer.Deserialize<RuntimeTokenPhase2NonInferiorityCohortFreezeResult>(File.ReadAllText(nonInferiorityCohortJsonPath), JsonOptions)
                                                  ?? throw new InvalidOperationException($"Non-inferiority cohort '{nonInferiorityCohortJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenPhase2ActiveCanaryApprovalReviewService(services.Paths);
            var result = service.Persist(readinessReviewResult, candidateResult, reviewBundle, rollbackPlanFreezeResult, nonInferiorityCohortFreezeResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunActiveCanaryExecutionApproval(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline active-canary-execution-approval --result-date <yyyy-MM-dd>");
            var approvalReviewJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"active-canary-approval-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(approvalReviewJsonPath))
            {
                throw new InvalidOperationException($"Active canary execution approval requires implementation approval review '{approvalReviewJsonPath}'.");
            }

            var approvalReviewResult = JsonSerializer.Deserialize<RuntimeTokenPhase2ActiveCanaryApprovalReviewResult>(File.ReadAllText(approvalReviewJsonPath), JsonOptions)
                                       ?? throw new InvalidOperationException($"Implementation approval review '{approvalReviewJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenPhase2ActiveCanaryExecutionApprovalService(
                services.Paths,
                new RuntimeTokenWorkerWrapperCanaryService());
            var result = service.Persist(approvalReviewResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunActiveCanaryResult(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline active-canary-result --result-date <yyyy-MM-dd>");
            var executionApprovalJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"active-canary-execution-approval-{resultDate:yyyy-MM-dd}.json");
            var baselineEvidenceJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-0a",
                $"attribution-baseline-evidence-result-{resultDate:yyyy-MM-dd}.json");
            var nonInferiorityCohortJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"non-inferiority-cohort-{resultDate:yyyy-MM-dd}.json");
            var workerRecollectJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-0a",
                $"worker-recollect-result-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(executionApprovalJsonPath))
            {
                throw new InvalidOperationException($"Active canary result requires execution approval '{executionApprovalJsonPath}'.");
            }

            if (!File.Exists(baselineEvidenceJsonPath))
            {
                throw new InvalidOperationException($"Active canary result requires baseline evidence result '{baselineEvidenceJsonPath}'.");
            }

            if (!File.Exists(nonInferiorityCohortJsonPath))
            {
                throw new InvalidOperationException($"Active canary result requires non-inferiority cohort '{nonInferiorityCohortJsonPath}'.");
            }

            if (!File.Exists(workerRecollectJsonPath))
            {
                throw new InvalidOperationException($"Active canary result requires worker recollect result '{workerRecollectJsonPath}'.");
            }

            var executionApprovalResult = JsonSerializer.Deserialize<RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult>(File.ReadAllText(executionApprovalJsonPath), JsonOptions)
                                          ?? throw new InvalidOperationException($"Active canary execution approval '{executionApprovalJsonPath}' could not be deserialized.");
            var baselineEvidenceResult = JsonSerializer.Deserialize<RuntimeTokenBaselineEvidenceResult>(File.ReadAllText(baselineEvidenceJsonPath), JsonOptions)
                                       ?? throw new InvalidOperationException($"Baseline evidence result '{baselineEvidenceJsonPath}' could not be deserialized.");
            var nonInferiorityCohortFreezeResult = JsonSerializer.Deserialize<RuntimeTokenPhase2NonInferiorityCohortFreezeResult>(File.ReadAllText(nonInferiorityCohortJsonPath), JsonOptions)
                                                  ?? throw new InvalidOperationException($"Non-inferiority cohort '{nonInferiorityCohortJsonPath}' could not be deserialized.");
            var workerRecollectResult = JsonSerializer.Deserialize<RuntimeTokenBaselineWorkerRecollectResult>(File.ReadAllText(workerRecollectJsonPath), JsonOptions)
                                        ?? throw new InvalidOperationException($"Worker recollect result '{workerRecollectJsonPath}' could not be deserialized.");
            var workerConfig = services.AiProviderConfig.ResolveForRole("worker");
            var service = new RuntimeTokenPhase2ActiveCanaryResultService(
                services.Paths,
                services.SystemConfig.RepoName,
                workerConfig,
                services.GitClient,
                new JsonTaskGraphRepository(services.Paths));
            var result = service.Persist(executionApprovalResult, baselineEvidenceResult, nonInferiorityCohortFreezeResult, workerRecollectResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunActiveCanaryResultReview(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline active-canary-result-review --result-date <yyyy-MM-dd>");
            var executionApprovalJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"active-canary-execution-approval-{resultDate:yyyy-MM-dd}.json");
            var canaryResultJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"active-canary-result-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(executionApprovalJsonPath))
            {
                throw new InvalidOperationException($"Active canary result review requires execution approval '{executionApprovalJsonPath}'.");
            }

            if (!File.Exists(canaryResultJsonPath))
            {
                throw new InvalidOperationException($"Active canary result review requires canary result '{canaryResultJsonPath}'.");
            }

            var executionApprovalResult = JsonSerializer.Deserialize<RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult>(File.ReadAllText(executionApprovalJsonPath), JsonOptions)
                                          ?? throw new InvalidOperationException($"Active canary execution approval '{executionApprovalJsonPath}' could not be deserialized.");
            var canaryResult = JsonSerializer.Deserialize<RuntimeTokenPhase2ActiveCanaryResult>(File.ReadAllText(canaryResultJsonPath), JsonOptions)
                               ?? throw new InvalidOperationException($"Active canary result '{canaryResultJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenPhase2ActiveCanaryResultReviewService(services.Paths);
            var result = service.Persist(executionApprovalResult, canaryResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunPostCanaryGate(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline post-canary-gate --result-date <yyyy-MM-dd>");
            var canaryResultReviewJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"active-canary-result-review-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(canaryResultReviewJsonPath))
            {
                throw new InvalidOperationException($"Post-canary gate requires canary result review '{canaryResultReviewJsonPath}'.");
            }

            var canaryResultReviewResult = JsonSerializer.Deserialize<RuntimeTokenPhase2ActiveCanaryResultReviewResult>(File.ReadAllText(canaryResultReviewJsonPath), JsonOptions)
                                          ?? throw new InvalidOperationException($"Canary result review '{canaryResultReviewJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenPhase2PostCanaryGateService(services.Paths);
            var result = service.Persist(canaryResultReviewResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunMainPathReplacementReview(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline main-path-replacement-review --result-date <yyyy-MM-dd>");
            var executionApprovalJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"active-canary-execution-approval-{resultDate:yyyy-MM-dd}.json");
            var canaryResultJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"active-canary-result-{resultDate:yyyy-MM-dd}.json");
            var canaryResultReviewJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"active-canary-result-review-{resultDate:yyyy-MM-dd}.json");
            var postCanaryGateJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-2",
                $"post-canary-gate-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(executionApprovalJsonPath))
            {
                throw new InvalidOperationException($"Main-path replacement review requires execution approval '{executionApprovalJsonPath}'.");
            }

            if (!File.Exists(canaryResultJsonPath))
            {
                throw new InvalidOperationException($"Main-path replacement review requires active canary result '{canaryResultJsonPath}'.");
            }

            if (!File.Exists(canaryResultReviewJsonPath))
            {
                throw new InvalidOperationException($"Main-path replacement review requires active canary result review '{canaryResultReviewJsonPath}'.");
            }

            if (!File.Exists(postCanaryGateJsonPath))
            {
                throw new InvalidOperationException($"Main-path replacement review requires post-canary gate '{postCanaryGateJsonPath}'.");
            }

            var executionApprovalResult = JsonSerializer.Deserialize<RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult>(File.ReadAllText(executionApprovalJsonPath), JsonOptions)
                                          ?? throw new InvalidOperationException($"Execution approval '{executionApprovalJsonPath}' could not be deserialized.");
            var canaryResult = JsonSerializer.Deserialize<RuntimeTokenPhase2ActiveCanaryResult>(File.ReadAllText(canaryResultJsonPath), JsonOptions)
                               ?? throw new InvalidOperationException($"Active canary result '{canaryResultJsonPath}' could not be deserialized.");
            var canaryResultReviewResult = JsonSerializer.Deserialize<RuntimeTokenPhase2ActiveCanaryResultReviewResult>(File.ReadAllText(canaryResultReviewJsonPath), JsonOptions)
                                           ?? throw new InvalidOperationException($"Active canary result review '{canaryResultReviewJsonPath}' could not be deserialized.");
            var postCanaryGateResult = JsonSerializer.Deserialize<RuntimeTokenPhase2PostCanaryGateResult>(File.ReadAllText(postCanaryGateJsonPath), JsonOptions)
                                       ?? throw new InvalidOperationException($"Post-canary gate '{postCanaryGateJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenPhase3MainPathReplacementReviewService(
                services.Paths,
                new RuntimeTokenWorkerWrapperCanaryService());
            var result = service.Persist(executionApprovalResult, canaryResult, canaryResultReviewResult, postCanaryGateResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunReplacementScopeFreeze(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline replacement-scope-freeze --result-date <yyyy-MM-dd>");
            var reviewJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-3",
                $"main-path-replacement-review-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(reviewJsonPath))
            {
                throw new InvalidOperationException($"Replacement scope freeze requires main-path replacement review '{reviewJsonPath}'.");
            }

            var reviewResult = JsonSerializer.Deserialize<RuntimeTokenPhase3MainPathReplacementReviewResult>(File.ReadAllText(reviewJsonPath), JsonOptions)
                               ?? throw new InvalidOperationException($"Main-path replacement review '{reviewJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenPhase3ReplacementScopeFreezeService(services.Paths);
            var result = service.Persist(reviewResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunPostRolloutAuditGate(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline post-rollout-audit-gate --result-date <yyyy-MM-dd>");
            var reviewJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-3",
                $"main-path-replacement-review-{resultDate:yyyy-MM-dd}.json");
            var scopeFreezeJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-3",
                $"replacement-scope-freeze-{resultDate:yyyy-MM-dd}.json");
            var postRolloutEvidenceJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-3",
                $"post-rollout-evidence-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(reviewJsonPath))
            {
                throw new InvalidOperationException($"Post-rollout audit gate requires main-path replacement review '{reviewJsonPath}'.");
            }

            if (!File.Exists(scopeFreezeJsonPath))
            {
                throw new InvalidOperationException($"Post-rollout audit gate requires replacement scope freeze '{scopeFreezeJsonPath}'.");
            }

            if (!File.Exists(postRolloutEvidenceJsonPath))
            {
                throw new InvalidOperationException($"Post-rollout audit gate requires post-rollout evidence '{postRolloutEvidenceJsonPath}'.");
            }

            var reviewResult = JsonSerializer.Deserialize<RuntimeTokenPhase3MainPathReplacementReviewResult>(File.ReadAllText(reviewJsonPath), JsonOptions)
                               ?? throw new InvalidOperationException($"Main-path replacement review '{reviewJsonPath}' could not be deserialized.");
            var scopeFreezeResult = JsonSerializer.Deserialize<RuntimeTokenPhase3ReplacementScopeFreezeResult>(File.ReadAllText(scopeFreezeJsonPath), JsonOptions)
                                    ?? throw new InvalidOperationException($"Replacement scope freeze '{scopeFreezeJsonPath}' could not be deserialized.");
            var postRolloutEvidenceResult = JsonSerializer.Deserialize<RuntimeTokenPhase3PostRolloutEvidenceResult>(File.ReadAllText(postRolloutEvidenceJsonPath), JsonOptions)
                                            ?? throw new InvalidOperationException($"Post-rollout evidence '{postRolloutEvidenceJsonPath}' could not be deserialized.");
            var service = new RuntimeTokenPhase3PostRolloutAuditGateService(services.Paths);
            var result = service.Persist(reviewResult, scopeFreezeResult, postRolloutEvidenceResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static OperatorCommandResult RunPostRolloutEvidenceCollection(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        try
        {
            var resultDate = ResolveResultDate(arguments)
                             ?? throw new InvalidOperationException("Usage: runtime token-baseline post-rollout-evidence-collection --result-date <yyyy-MM-dd>");
            var reviewJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-3",
                $"main-path-replacement-review-{resultDate:yyyy-MM-dd}.json");
            var scopeFreezeJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-3",
                $"replacement-scope-freeze-{resultDate:yyyy-MM-dd}.json");
            var workerRecollectJsonPath = Path.Combine(
                services.Paths.AiRoot,
                "runtime",
                "token-optimization",
                "phase-0a",
                $"worker-recollect-result-{resultDate:yyyy-MM-dd}.json");
            if (!File.Exists(reviewJsonPath))
            {
                throw new InvalidOperationException($"Post-rollout evidence collection requires main-path replacement review '{reviewJsonPath}'.");
            }

            if (!File.Exists(scopeFreezeJsonPath))
            {
                throw new InvalidOperationException($"Post-rollout evidence collection requires replacement scope freeze '{scopeFreezeJsonPath}'.");
            }

            if (!File.Exists(workerRecollectJsonPath))
            {
                throw new InvalidOperationException($"Post-rollout evidence collection requires worker recollect result '{workerRecollectJsonPath}'.");
            }

            var reviewResult = JsonSerializer.Deserialize<RuntimeTokenPhase3MainPathReplacementReviewResult>(File.ReadAllText(reviewJsonPath), JsonOptions)
                               ?? throw new InvalidOperationException($"Main-path replacement review '{reviewJsonPath}' could not be deserialized.");
            var scopeFreezeResult = JsonSerializer.Deserialize<RuntimeTokenPhase3ReplacementScopeFreezeResult>(File.ReadAllText(scopeFreezeJsonPath), JsonOptions)
                                    ?? throw new InvalidOperationException($"Replacement scope freeze '{scopeFreezeJsonPath}' could not be deserialized.");
            var workerRecollectResult = JsonSerializer.Deserialize<RuntimeTokenBaselineWorkerRecollectResult>(File.ReadAllText(workerRecollectJsonPath), JsonOptions)
                                        ?? throw new InvalidOperationException($"Worker recollect result '{workerRecollectJsonPath}' could not be deserialized.");
            var workerConfig = services.AiProviderConfig.ResolveForRole("worker");
            var service = new RuntimeTokenPhase3PostRolloutEvidenceCollectionService(
                services.Paths,
                services.SystemConfig.RepoName,
                workerConfig,
                services.GitClient,
                new JsonTaskGraphRepository(services.Paths));
            var result = service.Persist(reviewResult, scopeFreezeResult, workerRecollectResult);
            return OperatorCommandResult.Success(JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (InvalidOperationException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
        catch (FormatException error)
        {
            return OperatorCommandResult.Failure(error.Message);
        }
    }

    private static RuntimeTokenBaselineCohortFreeze ResolveCohort(ControlPlanePaths paths, IReadOnlyList<string> arguments)
    {
        var cohortFile = ResolveOption(arguments, "--cohort-file");
        if (!string.IsNullOrWhiteSpace(cohortFile))
        {
            var path = Path.IsPathRooted(cohortFile)
                ? Path.GetFullPath(cohortFile)
                : Path.GetFullPath(Path.Combine(paths.RepoRoot, cohortFile));
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Frozen cohort file '{path}' does not exist.");
            }

            return JsonSerializer.Deserialize<RuntimeTokenBaselineCohortFreeze>(File.ReadAllText(path), JsonOptions)
                   ?? throw new InvalidOperationException($"Frozen cohort file '{path}' could not be deserialized.");
        }

        var cohortId = ResolveOption(arguments, "--cohort-id");
        var windowStart = ResolveOption(arguments, "--window-start");
        var windowEnd = ResolveOption(arguments, "--window-end");
        var requestKinds = ResolveOption(arguments, "--request-kinds");
        if (string.IsNullOrWhiteSpace(cohortId)
            || string.IsNullOrWhiteSpace(windowStart)
            || string.IsNullOrWhiteSpace(windowEnd)
            || string.IsNullOrWhiteSpace(requestKinds))
        {
            throw new InvalidOperationException("Usage: runtime token-baseline recompute (--cohort-file <path> | --cohort-id <id> --window-start <utc> --window-end <utc> --request-kinds <csv>) [--token-accounting-source-policy <policy>] [--context-window-view <id>] [--billable-cost-view <id>] [--result-date <yyyy-MM-dd>]");
        }

        return new RuntimeTokenBaselineCohortFreeze
        {
            CohortId = cohortId,
            WindowStartUtc = ParseUtc(windowStart, "--window-start"),
            WindowEndUtc = ParseUtc(windowEnd, "--window-end"),
            RequestKinds = requestKinds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            TokenAccountingSourcePolicy = ResolveOption(arguments, "--token-accounting-source-policy") ?? "provider_actual_preferred_with_reconciliation",
            ContextWindowView = ResolveOption(arguments, "--context-window-view") ?? "context_window_input_tokens_total",
            BillableCostView = ResolveOption(arguments, "--billable-cost-view") ?? "billable_input_tokens_uncached",
        };
    }

    private static DateTimeOffset ParseUtc(string value, string option)
    {
        if (!DateTimeOffset.TryParse(value, out var parsed))
        {
            throw new FormatException($"Usage error: option '{option}' requires an ISO-8601 UTC timestamp.");
        }

        return parsed.ToUniversalTime();
    }

    private static DateOnly? ResolveResultDate(IReadOnlyList<string> arguments)
    {
        var resultDate = ResolveOption(arguments, "--result-date");
        if (string.IsNullOrWhiteSpace(resultDate))
        {
            return null;
        }

        if (!DateOnly.TryParse(resultDate, out var parsed))
        {
            throw new FormatException("Usage error: option '--result-date' requires a yyyy-MM-dd value.");
        }

        return parsed;
    }

    private static string? ResolveOption(IReadOnlyList<string> arguments, string option)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= arguments.Count)
            {
                throw new InvalidOperationException($"Usage error: option '{option}' requires a value.");
            }

            return arguments[index + 1];
        }

        return null;
    }
}
