using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.Platform.SurfaceModels;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class TypedExecutionCoreService
{
    private const string OperationRegistryVersion = "session-gateway-operation-registry@0.98-rc.p1";
    private const string TransactionCompilerVersion = "session-gateway-transaction-compiler@0.98-rc.p3";

    private static readonly IReadOnlyList<SessionGatewayOperationDefinitionSurface> OperationDefinitions =
    [
        Operation(
            "inspect_bound_objects",
            "read.bound_objects",
            ["work_order_objects_bound"],
            ["read_bound_object_projection"],
            ["ledger:bound_object_projection"],
            "stop_on_missing_object",
            "carves.ledger.bound_object_projection.v0.98-rc"),
        Operation(
            "create_or_reuse_task_worktree",
            "execute.create_worktree",
            ["base_commit_available", "worktree_policy_allows"],
            ["create_or_reuse_task_worktree"],
            ["ledger:worktree_record"],
            "stop_on_worktree_error",
            "carves.ledger.worktree_record.v0.98-rc"),
        Operation(
            "build_context_pack",
            "read.context_pack",
            ["read_grant_allows_context"],
            ["build_context_pack"],
            ["ledger:context_pack_hash"],
            "stop_on_context_pack_error",
            "carves.ledger.context_pack.v0.98-rc"),
        Operation(
            "acquire_resource_lease",
            "lease.resource",
            ["resource_lease_available"],
            ["acquire_resource_lease"],
            ["ledger:resource_lease_record"],
            "queue_or_stop_on_conflict",
            "carves.ledger.resource_lease.v0.98-rc"),
        Operation(
            "execute_worker_in_worktree",
            "execute.worker",
            ["worktree_ready", "resource_lease_active"],
            ["run_worker", "write_repo_files_in_worktree"],
            ["run_artifacts"],
            "stop_on_worker_failure",
            "carves.ledger.worker_execution.v0.98-rc"),
        Operation(
            "run_build",
            "execute.build",
            ["patch_exists"],
            ["run_build"],
            ["evidence:build_log"],
            "stop_on_build_failure",
            "carves.ledger.build_result.v0.98-rc"),
        Operation(
            "run_tests",
            "execute.tests",
            ["build_available"],
            ["run_tests"],
            ["evidence:test_result"],
            "stop_on_test_failure",
            "carves.ledger.test_result.v0.98-rc"),
        Operation(
            "run_lint",
            "execute.lint",
            ["patch_exists"],
            ["run_lint"],
            ["evidence:lint_result"],
            "stop_on_lint_failure",
            "carves.ledger.lint_result.v0.98-rc"),
        Operation(
            "run_guard",
            "execute.guard",
            ["patch_exists"],
            ["run_guard"],
            ["evidence:guard_report"],
            "stop_on_guard_block",
            "carves.ledger.guard_report.v0.98-rc"),
        Operation(
            "verify_external_module_receipts",
            "adapter.receipts.verify",
            ["external_module_receipts_available"],
            ["verify_external_module_receipts"],
            ["ledger:external_module_receipt_refs"],
            "stop_or_downgrade_on_untrusted_receipt",
            "carves.ledger.external_module_receipt_refs.v0.98-rc"),
        Operation(
            "evaluate_boundary",
            "evaluate.boundary",
            ["evidence_complete"],
            ["evaluate_scope", "evaluate_patch_budget", "evaluate_truth_operations"],
            ["evidence:boundary_decision"],
            "stop_on_boundary_block",
            "carves.ledger.boundary_decision.v0.98-rc"),
        Operation(
            "create_result_commit_in_task_worktree",
            "git.create_result_commit",
            ["verification_passed", "boundary_allow"],
            ["create_result_commit_in_task_worktree"],
            ["ledger:result_commit_record"],
            "stop_on_commit_failure",
            "carves.ledger.result_commit.v0.98-rc"),
        Operation(
            "create_review_submission_sidecar",
            "artifact.create_review_submission",
            ["result_commit_exists", "evidence_complete"],
            ["create_review_submission_sidecar"],
            ["run_artifact:review_submission_sidecar"],
            "stop_on_submission_failure",
            "carves.ledger.review_submission_sidecar.v0.98-rc"),
        Operation(
            "task_status_to_review_certificate",
            "truth.certificate.task_status_to_review",
            ["review_submission_sidecar_exists", "boundary_allow"],
            ["issue_state_transition_certificate"],
            ["evidence:state_transition_certificate"],
            "stop_on_certificate_failure",
            "carves.ledger.state_transition_certificate.v0.98-rc"),
    ];

    public SessionGatewayOperationRegistrySurface GetOperationRegistry()
    {
        return new SessionGatewayOperationRegistrySurface
        {
            RegistryVersion = OperationRegistryVersion,
            Operations = OperationDefinitions,
        };
    }

    public SessionGatewayExecutionTransactionDryRunSurface CompileCanonicalTransactionDryRun(
        SessionGatewayCapabilityLeaseSurface capabilityLease)
    {
        var steps = OperationDefinitions
            .Select((definition, index) => BuildTransactionStep($"step-{index + 1:00}", definition))
            .ToArray();
        var verified = VerifyTransactionDryRun(steps, capabilityLease.CapabilityIds);
        var transactionHash = ComputeTransactionHash(verified.Steps, verified.RegistryVersion);
        return new SessionGatewayExecutionTransactionDryRunSurface
        {
            TransactionId = $"txdry-{transactionHash[..16]}",
            TransactionHash = transactionHash,
            CompilerVersion = TransactionCompilerVersion,
            RegistryVersion = verified.RegistryVersion,
            VerificationState = verified.VerificationState,
            Steps = verified.Steps,
            VerificationErrors = verified.VerificationErrors,
            VerificationReport = verified.VerificationReport,
        };
    }

    public SessionGatewayExecutionTransactionDryRunSurface BuildNotRequiredTransactionDryRun()
    {
        return new SessionGatewayExecutionTransactionDryRunSurface
        {
            CompilerVersion = TransactionCompilerVersion,
            RegistryVersion = OperationRegistryVersion,
            VerificationState = "not_required",
            VerificationReport = BuildVerificationReport(
                operationCoverage: true,
                capabilityCoverage: true,
                preconditionCoverage: true,
                declaredEffectCoverage: true,
                writeTargetCoverage: true,
                failurePolicyBinding: true,
                ledgerEventBinding: true,
                deterministicHash: true,
                stepCount: 0,
                errorCount: 0),
        };
    }

    public SessionGatewayExecutionTransactionDryRunSurface VerifyTransactionDryRun(
        SessionGatewayTransactionVerificationRequest request)
    {
        return VerifyTransactionDryRun(request.Steps, request.CapabilityIds);
    }

    public SessionGatewayExecutionTransactionDryRunSurface VerifyTransactionDryRun(
        IReadOnlyList<SessionGatewayTransactionStepSurface> steps,
        IReadOnlyList<string> capabilityIds)
    {
        var registry = OperationDefinitions.ToDictionary(static definition => definition.OperationId, StringComparer.Ordinal);
        var capabilities = capabilityIds.ToHashSet(StringComparer.Ordinal);
        var errors = new List<string>();
        var operationCoverage = true;
        var capabilityCoverage = true;
        var preconditionCoverage = true;
        var declaredEffectCoverage = true;
        var writeTargetCoverage = true;
        var failurePolicyBinding = true;
        var ledgerEventBinding = true;

        foreach (var step in steps)
        {
            if (!registry.TryGetValue(step.OperationId, out var definition))
            {
                operationCoverage = false;
                errors.Add($"SC-UNKNOWN-OPERATION:{step.StepId}:{step.OperationId}");
                continue;
            }

            if (!string.Equals(step.OperationVersion, definition.Version, StringComparison.Ordinal))
            {
                errors.Add($"SC-OPERATION-VERSION-MISMATCH:{step.StepId}:{step.OperationId}");
            }

            if (!string.Equals(step.CapabilityRequired, definition.CapabilityRequired, StringComparison.Ordinal)
                || !capabilities.Contains(definition.CapabilityRequired))
            {
                capabilityCoverage = false;
                errors.Add($"SC-CAPABILITY-MISMATCH:{step.StepId}:{definition.CapabilityRequired}");
            }

            if (step.PreconditionResolvers.Count == 0
                || !step.PreconditionResolvers.SequenceEqual(definition.PreconditionResolvers, StringComparer.Ordinal)
                || !string.Equals(step.PreconditionState, "resolved", StringComparison.Ordinal))
            {
                preconditionCoverage = false;
                errors.Add($"SC-PRECONDITION-UNRESOLVED:{step.StepId}:{step.OperationId}");
            }

            foreach (var declaredEffect in step.DeclaredEffects)
            {
                if (!definition.DeclaredEffects.Contains(declaredEffect, StringComparer.Ordinal))
                {
                    declaredEffectCoverage = false;
                    errors.Add($"SC-FREE-TEXT-EFFECT:{step.StepId}:{declaredEffect}");
                }
            }

            foreach (var write in step.WritesDeclared)
            {
                if (!definition.WriteTargets.Contains(write, StringComparer.Ordinal))
                {
                    writeTargetCoverage = false;
                    errors.Add($"SC-UNREGISTERED-WRITE-TARGET:{step.StepId}:{write}");
                }
            }

            if (!string.Equals(step.FailurePolicy, definition.FailurePolicy, StringComparison.Ordinal))
            {
                failurePolicyBinding = false;
                errors.Add($"SC-FAILURE-POLICY-UNBOUND:{step.StepId}:{step.OperationId}");
            }

            if (!string.Equals(step.LedgerEventSchema, definition.LedgerEventSchema, StringComparison.Ordinal))
            {
                ledgerEventBinding = false;
                errors.Add($"SC-LEDGER-EVENT-UNBOUND:{step.StepId}:{step.OperationId}");
            }
        }

        var transactionHash = ComputeTransactionHash(steps, OperationRegistryVersion);
        return new SessionGatewayExecutionTransactionDryRunSurface
        {
            TransactionId = steps.Count == 0 ? null : $"txdry-{transactionHash[..16]}",
            TransactionHash = steps.Count == 0 ? null : transactionHash,
            CompilerVersion = TransactionCompilerVersion,
            RegistryVersion = OperationRegistryVersion,
            VerificationState = errors.Count == 0 ? "verified" : "failed",
            Steps = steps,
            VerificationErrors = errors,
            VerificationReport = BuildVerificationReport(
                operationCoverage,
                capabilityCoverage,
                preconditionCoverage,
                declaredEffectCoverage,
                writeTargetCoverage,
                failurePolicyBinding,
                ledgerEventBinding,
                deterministicHash: true,
                stepCount: steps.Count,
                errorCount: errors.Count),
        };
    }

    private static SessionGatewayTransactionStepSurface BuildTransactionStep(
        string stepId,
        SessionGatewayOperationDefinitionSurface definition)
    {
        return new SessionGatewayTransactionStepSurface
        {
            StepId = stepId,
            OperationId = definition.OperationId,
            OperationVersion = definition.Version,
            CapabilityRequired = definition.CapabilityRequired,
            PreconditionResolvers = definition.PreconditionResolvers,
            PreconditionState = "resolved",
            DeclaredEffects = definition.DeclaredEffects,
            WritesDeclared = definition.WriteTargets,
            FailurePolicy = definition.FailurePolicy,
            LedgerEventSchema = definition.LedgerEventSchema,
        };
    }

    private static string ComputeTransactionHash(
        IReadOnlyList<SessionGatewayTransactionStepSurface> steps,
        string registryVersion)
    {
        var builder = new StringBuilder()
            .Append(TransactionCompilerVersion).Append('\n')
            .Append(registryVersion).Append('\n');
        foreach (var step in steps.OrderBy(static step => step.StepId, StringComparer.Ordinal))
        {
            builder
                .Append(step.StepId).Append('|')
                .Append(step.OperationId).Append('|')
                .Append(step.OperationVersion).Append('|')
                .Append(step.CapabilityRequired).Append('|')
                .Append(string.Join(",", step.PreconditionResolvers)).Append('|')
                .Append(step.PreconditionState).Append('|')
                .Append(string.Join(",", step.DeclaredEffects)).Append('|')
                .Append(string.Join(",", step.WritesDeclared)).Append('|')
                .Append(step.FailurePolicy).Append('|')
                .Append(step.LedgerEventSchema)
                .Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static SessionGatewayTransactionVerificationReportSurface BuildVerificationReport(
        bool operationCoverage,
        bool capabilityCoverage,
        bool preconditionCoverage,
        bool declaredEffectCoverage,
        bool writeTargetCoverage,
        bool failurePolicyBinding,
        bool ledgerEventBinding,
        bool deterministicHash,
        int stepCount,
        int errorCount)
    {
        return new SessionGatewayTransactionVerificationReportSurface
        {
            OperationCoverage = operationCoverage,
            CapabilityCoverage = capabilityCoverage,
            PreconditionCoverage = preconditionCoverage,
            DeclaredEffectCoverage = declaredEffectCoverage,
            WriteTargetCoverage = writeTargetCoverage,
            FailurePolicyBinding = failurePolicyBinding,
            LedgerEventBinding = ledgerEventBinding,
            DeterministicHash = deterministicHash,
            StepCount = stepCount,
            ErrorCount = errorCount,
        };
    }

    private static SessionGatewayOperationDefinitionSurface Operation(
        string operationId,
        string capabilityRequired,
        IReadOnlyList<string> preconditionResolvers,
        IReadOnlyList<string> declaredEffects,
        IReadOnlyList<string> writeTargets,
        string failurePolicy,
        string ledgerEventSchema)
    {
        return new SessionGatewayOperationDefinitionSurface
        {
            OperationId = operationId,
            Version = "v0.98-rc",
            CapabilityRequired = capabilityRequired,
            PreconditionResolvers = preconditionResolvers,
            DeclaredEffects = declaredEffects,
            WriteTargets = writeTargets,
            FailurePolicy = failurePolicy,
            LedgerEventSchema = ledgerEventSchema,
        };
    }
}
