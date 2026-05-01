using System.Text.Json;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeSessionGatewayService
{
    private const string WorkOrderCandidateLevel = "L3_RUN_TO_REVIEW";
    private const string WorkOrderTerminalState = "submitted_to_review";

    private static SessionGatewayIntentEnvelopeSurface BuildIntentEnvelope(
        SessionGatewayMessageRequest request,
        string classifiedIntent,
        RoleGovernanceRuntimePolicy roleGovernancePolicy)
    {
        var actionTokens = ResolveActionTokens(request, classifiedIntent, roleGovernancePolicy);
        var objectFocus = ResolveObjectFocus(request);
        return new SessionGatewayIntentEnvelopeSurface
        {
            PrimaryIntent = classifiedIntent,
            CandidateLevel = classifiedIntent switch
            {
                "privileged_work_order" => PrivilegedCandidateLevel,
                "governed_run" => WorkOrderCandidateLevel,
                "plan" => "L2_READ",
                _ => "L0_CHAT",
            },
            Confidence = ResolveIntentConfidence(classifiedIntent, request),
            ObjectFocus = objectFocus,
            ActionTokens = actionTokens,
            RequiresWorkOrder = string.Equals(classifiedIntent, "governed_run", StringComparison.Ordinal)
                || string.Equals(classifiedIntent, "privileged_work_order", StringComparison.Ordinal),
            MayExecute = false,
        };
    }

    private SessionGatewayWorkOrderDryRunSurface BuildWorkOrderDryRun(
        ActorSessionRecord session,
        SessionGatewayMessageRequest request,
        SessionGatewayIntentEnvelopeSurface intentEnvelope)
    {
        var externalModuleReceiptEvaluation = BuildExternalModuleReceiptEvaluation(request);
        if (string.Equals(intentEnvelope.PrimaryIntent, "privileged_work_order", StringComparison.Ordinal))
        {
            return BuildPrivilegedWorkOrderDryRun(session, request, intentEnvelope, externalModuleReceiptEvaluation);
        }

        if (!intentEnvelope.RequiresWorkOrder)
        {
            return new SessionGatewayWorkOrderDryRunSurface
            {
                ReceiptKind = "not_required",
                AdmissionState = "not_required",
                UserVisibleLevel = intentEnvelope.CandidateLevel,
                TerminalState = "none",
                RequiresWorkOrder = false,
                MayExecute = false,
                LeaseIssued = false,
                BoundObjects = intentEnvelope.ObjectFocus,
                BoundArtifacts = ResolveBoundArtifacts(request, requirePlanHash: false, requireAcceptanceContractHash: false),
                SubmitSemantics = BuildSubmitSemantics(),
                OperationRegistry = BuildOperationRegistry(),
                CapabilityLease = BuildNotRequiredCapabilityLease(),
                ResourceLease = BuildNotRequiredResourceLease(),
                ExternalModuleAdapters = BuildExternalModuleAdapterRegistry(),
                ExternalModuleReceiptEvaluation = externalModuleReceiptEvaluation,
                PrivilegedWorkOrder = BuildNotRequiredPrivilegedWorkOrder(),
                TransactionDryRun = BuildNotRequiredTransactionDryRun(),
                NextRequiredAction = "none",
                Summary = "This turn does not require an Authorized Work Order.",
            };
        }

        var requirePlanHash = RequiresPlanHash(request);
        var requireAcceptanceContractHash = RequiresAcceptanceContractHash(request);
        var boundArtifacts = ResolveBoundArtifacts(request, requirePlanHash, requireAcceptanceContractHash);
        var stopReasons = ResolveWorkOrderStopReasons(intentEnvelope, boundArtifacts);
        if (intentEnvelope.ObjectFocus.Count == 0)
        {
            return new SessionGatewayWorkOrderDryRunSurface
            {
                ReceiptKind = "draft_receipt",
                AdmissionState = "draft",
                UserVisibleLevel = WorkOrderCandidateLevel,
                TerminalState = WorkOrderTerminalState,
                RequiresWorkOrder = true,
                MayExecute = false,
                LeaseIssued = false,
                BoundObjects = intentEnvelope.ObjectFocus,
                BoundArtifacts = boundArtifacts,
                SubmitSemantics = BuildSubmitSemantics(),
                OperationRegistry = BuildOperationRegistry(),
                CapabilityLease = BuildNotRequiredCapabilityLease(),
                ResourceLease = BuildNotRequiredResourceLease(),
                ExternalModuleAdapters = BuildExternalModuleAdapterRegistry(),
                ExternalModuleReceiptEvaluation = externalModuleReceiptEvaluation,
                PrivilegedWorkOrder = BuildNotRequiredPrivilegedWorkOrder(),
                TransactionDryRun = BuildNotRequiredTransactionDryRun(),
                StopReasons = stopReasons,
                NextRequiredAction = "provide_target_object",
                Summary = "The request needs a target card, task graph, or task before Host admission can accept it.",
            };
        }

        if (string.Equals(externalModuleReceiptEvaluation.TransactionDisposition, "stop_transaction", StringComparison.Ordinal))
        {
            return new SessionGatewayWorkOrderDryRunSurface
            {
                ReceiptKind = "blocked_receipt",
                AdmissionState = "blocked",
                UserVisibleLevel = WorkOrderCandidateLevel,
                TerminalState = WorkOrderTerminalState,
                RequiresWorkOrder = true,
                MayExecute = false,
                LeaseIssued = false,
                BoundObjects = intentEnvelope.ObjectFocus,
                BoundArtifacts = boundArtifacts,
                SubmitSemantics = BuildSubmitSemantics(),
                OperationRegistry = BuildOperationRegistry(),
                CapabilityLease = BuildNotRequiredCapabilityLease(),
                ResourceLease = BuildNotRequiredResourceLease(),
                ExternalModuleAdapters = BuildExternalModuleAdapterRegistry(),
                ExternalModuleReceiptEvaluation = externalModuleReceiptEvaluation,
                PrivilegedWorkOrder = BuildNotRequiredPrivilegedWorkOrder(),
                TransactionDryRun = BuildNotRequiredTransactionDryRun(),
                StopReasons = externalModuleReceiptEvaluation.StopReasons,
                NextRequiredAction = "repair_external_module_receipts",
                Summary = "Host admission dry-run rejected the Work Order because the cited external module receipts did not satisfy the verified adapter receipt contract.",
            };
        }

        if (stopReasons.Count != 0)
        {
            return new SessionGatewayWorkOrderDryRunSurface
            {
                ReceiptKind = "blocked_receipt",
                AdmissionState = "blocked",
                UserVisibleLevel = WorkOrderCandidateLevel,
                TerminalState = WorkOrderTerminalState,
                RequiresWorkOrder = true,
                MayExecute = false,
                LeaseIssued = false,
                BoundObjects = intentEnvelope.ObjectFocus,
                BoundArtifacts = boundArtifacts,
                SubmitSemantics = BuildSubmitSemantics(),
                OperationRegistry = BuildOperationRegistry(),
                CapabilityLease = BuildNotRequiredCapabilityLease(),
                ResourceLease = BuildNotRequiredResourceLease(),
                ExternalModuleAdapters = BuildExternalModuleAdapterRegistry(),
                ExternalModuleReceiptEvaluation = externalModuleReceiptEvaluation,
                PrivilegedWorkOrder = BuildNotRequiredPrivilegedWorkOrder(),
                TransactionDryRun = BuildNotRequiredTransactionDryRun(),
                StopReasons = stopReasons,
                NextRequiredAction = "provide_bound_artifact_hashes",
                Summary = "The request is understood, but Host admission dry-run rejected it until required artifact hashes are bound.",
            };
        }

        var workOrderId = $"wodry-{Guid.NewGuid():N}";
        var capabilityLease = IssueCapabilityLeaseDryRun(workOrderId);
        var resourceLease = IssueResourceLeaseDryRun(workOrderId, request, capabilityLease);
        if (!string.Equals(resourceLease.LeaseState, "projected", StringComparison.Ordinal)
            && !string.Equals(resourceLease.LeaseState, "active", StringComparison.Ordinal))
        {
            return new SessionGatewayWorkOrderDryRunSurface
            {
                WorkOrderId = workOrderId,
                ReceiptKind = "blocked_receipt",
                AdmissionState = "blocked",
                UserVisibleLevel = WorkOrderCandidateLevel,
                TerminalState = WorkOrderTerminalState,
                RequiresWorkOrder = true,
                MayExecute = false,
                LeaseIssued = string.Equals(capabilityLease.LeaseState, "issued", StringComparison.Ordinal),
                BoundObjects = intentEnvelope.ObjectFocus,
                BoundArtifacts = boundArtifacts,
                SubmitSemantics = BuildSubmitSemantics(),
                OperationRegistry = BuildOperationRegistry(),
                CapabilityLease = capabilityLease,
                ResourceLease = resourceLease,
                ExternalModuleAdapters = BuildExternalModuleAdapterRegistry(),
                ExternalModuleReceiptEvaluation = externalModuleReceiptEvaluation,
                PrivilegedWorkOrder = BuildNotRequiredPrivilegedWorkOrder(),
                TransactionDryRun = BuildNotRequiredTransactionDryRun(),
                StopReasons = resourceLease.StopReasons.Count == 0
                    ? [ResourceLeaseService.ConflictStopReason]
                    : resourceLease.StopReasons,
                NextRequiredAction = "resolve_resource_lease_conflict",
                Summary = "Host admission dry-run rejected the Work Order because its resource lease conflicts with active work.",
            };
        }

        var transactionDryRun = BuildCanonicalTransactionDryRun(capabilityLease);
        var ledger = TryRecordWorkOrderDryRunLedger(
            workOrderId,
            request,
            intentEnvelope,
            boundArtifacts,
            capabilityLease,
            resourceLease,
            transactionDryRun);
        if (!ledger.CanWriteBack)
        {
            return new SessionGatewayWorkOrderDryRunSurface
            {
                WorkOrderId = workOrderId,
                ReceiptKind = "blocked_receipt",
                AdmissionState = "blocked",
                UserVisibleLevel = WorkOrderCandidateLevel,
                TerminalState = WorkOrderTerminalState,
                RequiresWorkOrder = true,
                MayExecute = false,
                LeaseIssued = false,
                BoundObjects = intentEnvelope.ObjectFocus,
                BoundArtifacts = boundArtifacts,
                SubmitSemantics = BuildSubmitSemantics(),
                OperationRegistry = BuildOperationRegistry(),
                CapabilityLease = capabilityLease,
                ResourceLease = resourceLease,
                ExternalModuleAdapters = BuildExternalModuleAdapterRegistry(),
                ExternalModuleReceiptEvaluation = externalModuleReceiptEvaluation,
                PrivilegedWorkOrder = BuildNotRequiredPrivilegedWorkOrder(),
                TransactionDryRun = transactionDryRun,
                StopReasons = [EffectLedgerService.AuditIncompleteStopReason],
                EffectLedgerPath = ledger.LedgerPath,
                EffectLedgerReplayState = ledger.ReplayState,
                EffectLedgerTerminalState = ledger.TerminalState,
                EffectLedgerStopReasons = ledger.StopReasons,
                NextRequiredAction = "inspect_effect_ledger",
                Summary = "Host admission dry-run rejected the Work Order because the effect ledger could not be written and replayed.",
            };
        }

        return new SessionGatewayWorkOrderDryRunSurface
        {
            WorkOrderId = workOrderId,
            ReceiptKind = "accepted_receipt",
            AdmissionState = "admitted_dry_run",
            UserVisibleLevel = WorkOrderCandidateLevel,
            TerminalState = WorkOrderTerminalState,
            RequiresWorkOrder = true,
            MayExecute = false,
            LeaseIssued = string.Equals(capabilityLease.LeaseState, "issued", StringComparison.Ordinal),
            BoundObjects = intentEnvelope.ObjectFocus,
            BoundArtifacts = boundArtifacts,
            SubmitSemantics = BuildSubmitSemantics(),
            OperationRegistry = BuildOperationRegistry(),
            CapabilityLease = capabilityLease,
            ResourceLease = resourceLease,
            ExternalModuleAdapters = BuildExternalModuleAdapterRegistry(),
            ExternalModuleReceiptEvaluation = externalModuleReceiptEvaluation,
            PrivilegedWorkOrder = BuildNotRequiredPrivilegedWorkOrder(),
            TransactionDryRun = transactionDryRun,
            EffectLedgerPath = ledger.LedgerPath,
            EffectLedgerReplayState = ledger.ReplayState,
            EffectLedgerTerminalState = ledger.TerminalState,
            EffectLedgerStopReasons = ledger.StopReasons,
            NextRequiredAction = string.Equals(externalModuleReceiptEvaluation.TransactionDisposition, "downgrade_transaction", StringComparison.Ordinal)
                ? "review_external_module_receipts"
                : "compile_deterministic_execution_transaction",
            Summary = string.Equals(externalModuleReceiptEvaluation.TransactionDisposition, "downgrade_transaction", StringComparison.Ordinal)
                ? "Host admission dry-run accepted the bounded Work Order shape, but the cited external module receipts require a downgraded decision path before execution can proceed."
                : "Host admission dry-run accepted the bounded Work Order shape, issued scoped capability and resource leases, and verified the typed operation transaction shape; execution remains disabled.",
        };
    }

    private static bool AllowsGatewayOperationBinding(SessionGatewayWorkOrderDryRunSurface workOrderDryRun)
    {
        return !workOrderDryRun.RequiresWorkOrder
            || string.Equals(workOrderDryRun.AdmissionState, "admitted_dry_run", StringComparison.Ordinal);
    }

    private static IReadOnlyList<SessionGatewayObjectFocusSurface> ResolveObjectFocus(SessionGatewayMessageRequest request)
    {
        var focus = new List<SessionGatewayObjectFocusSurface>();
        AddObjectFocus(focus, "explicit_card_id", "card", request.TargetCardId, 1.0);
        AddObjectFocus(focus, "explicit_taskgraph_id", "taskgraph", request.TargetTaskGraphId, 1.0);
        AddObjectFocus(focus, "explicit_task_id", "task", request.TargetTaskId, 1.0);
        return focus;
    }

    private static void AddObjectFocus(
        ICollection<SessionGatewayObjectFocusSurface> focus,
        string source,
        string kind,
        string? id,
        double confidence)
    {
        var normalizedId = NormalizeOptional(id);
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return;
        }

        focus.Add(new SessionGatewayObjectFocusSurface
        {
            Source = source,
            Kind = kind,
            Id = normalizedId,
            Confidence = confidence,
        });
    }

    private static IReadOnlyList<string> ResolveActionTokens(
        SessionGatewayMessageRequest request,
        string classifiedIntent,
        RoleGovernanceRuntimePolicy roleGovernancePolicy)
    {
        var text = NormalizeConversationControlText(request.UserText);
        var normalizedMode = NormalizeRequestedMode(request.RequestedMode);
        if (ShouldTreatRoleAutomationAsDiscussion(request, roleGovernancePolicy, text, normalizedMode))
        {
            return ["role_mode_disabled_discussion", "role_automation_reference"];
        }

        if ((IsAcknowledgementOnly(text) || IsBlanketConsentOnly(text))
            && !HasStructuredActionBinding(request, normalizedMode))
        {
            return ["acknowledgement_only"];
        }

        var tokens = new List<string>();
        AddTokenIf(tokens, string.Equals(classifiedIntent, "governed_run", StringComparison.Ordinal), "governed_run");
        AddTokenIf(tokens, string.Equals(classifiedIntent, "privileged_work_order", StringComparison.Ordinal), "privileged_work_order");
        AddTokenIf(tokens, ContainsAny(text, "approve", "批准"), "approve");
        AddTokenIf(tokens, ContainsAny(text, "execute", "implement", "run ", "run-task", "task run", "执行", "实现", "推进"), "execute");
        AddTokenIf(tokens, ContainsAny(text, "verify", "test", "passes", "passed", "通过", "测试"), "verify");
        AddTokenIf(tokens, ContainsAny(text, "submit", "review", "提交", "审查"), "submit_to_review");
        AddTokenIf(tokens, ContainsAny(text, "complete", "finish", "完成"), "complete_requested");
        AddTokenIf(tokens, ContainsAny(text, "plan", "方案", "计划"), "plan_reference");
        AddTokenIf(tokens, ContainsPrivilegedOperationText(text), "privileged_transition");
        return tokens.Count == 0 ? ["none"] : tokens;
    }

    private static void AddTokenIf(ICollection<string> tokens, bool condition, string token)
    {
        if (condition && !tokens.Contains(token))
        {
            tokens.Add(token);
        }
    }

    private static double ResolveIntentConfidence(string classifiedIntent, SessionGatewayMessageRequest request)
    {
        if (!string.IsNullOrWhiteSpace(NormalizeRequestedMode(request.RequestedMode)))
        {
            return 0.98;
        }

        return classifiedIntent switch
        {
            "governed_run" => 0.90,
            "plan" => 0.86,
            _ => 0.80,
        };
    }

    private static bool RequiresPlanHash(SessionGatewayMessageRequest request)
    {
        var text = request.UserText.Trim().ToLowerInvariant();
        return ContainsAny(
            text,
            "approve this plan",
            "approve the plan",
            "approved plan",
            "批准这个方案",
            "批准这个计划",
            "批准方案",
            "批准计划");
    }

    private static bool RequiresAcceptanceContractHash(SessionGatewayMessageRequest request)
    {
        var text = request.UserText.Trim().ToLowerInvariant();
        return RequiresPlanHash(request)
            || ContainsAny(
                text,
                "submit",
                "when it passes",
                "if it passes",
                "complete",
                "finish",
                "提交",
                "通过后",
                "完成");
    }

    private static IReadOnlyList<SessionGatewayBoundArtifactSurface> ResolveBoundArtifacts(
        SessionGatewayMessageRequest request,
        bool requirePlanHash,
        bool requireAcceptanceContractHash)
    {
        return
        [
            BuildBoundArtifact("plan_hash", request.PlanHash, requirePlanHash),
            BuildBoundArtifact("taskgraph_hash", request.TaskGraphHash, required: false),
            BuildBoundArtifact("acceptance_contract_hash", request.AcceptanceContractHash, requireAcceptanceContractHash),
        ];
    }

    private static SessionGatewayBoundArtifactSurface BuildBoundArtifact(string kind, string? hash, bool required)
    {
        var normalizedHash = NormalizeOptional(hash);
        return new SessionGatewayBoundArtifactSurface
        {
            Kind = kind,
            Hash = normalizedHash,
            Required = required,
            Status = string.IsNullOrWhiteSpace(normalizedHash)
                ? required ? "missing" : "not_required"
                : "bound",
        };
    }

    private static IReadOnlyList<string> ResolveWorkOrderStopReasons(
        SessionGatewayIntentEnvelopeSurface intentEnvelope,
        IReadOnlyList<SessionGatewayBoundArtifactSurface> boundArtifacts)
    {
        var reasons = new List<string>();
        if (intentEnvelope.ObjectFocus.Count == 0)
        {
            reasons.Add("SC-AMBIG-TARGET");
        }

        if (boundArtifacts.Any(static artifact => artifact.Required
            && string.Equals(artifact.Status, "missing", StringComparison.Ordinal)
            && string.Equals(artifact.Kind, "plan_hash", StringComparison.Ordinal)))
        {
            reasons.Add("SC-PLAN-HASH-MISMATCH");
        }

        if (boundArtifacts.Any(static artifact => artifact.Required
            && string.Equals(artifact.Status, "missing", StringComparison.Ordinal)
            && string.Equals(artifact.Kind, "acceptance_contract_hash", StringComparison.Ordinal)))
        {
            reasons.Add("SC-ACCEPTANCE-CONTRACT-MISSING");
        }

        return reasons;
    }

    private static SessionGatewaySubmitSemanticsSurface BuildSubmitSemantics()
    {
        return new SessionGatewaySubmitSemanticsSurface
        {
            SubmitMeans =
            [
                "create_result_commit_in_task_worktree",
                "create_review_submission",
            ],
            DoesNotMean =
            [
                "push",
                "merge",
                "release",
                "apply_to_current_branch",
                "write_review_verdict",
                "write_memory_truth",
                "refresh_authoritative_codegraph",
            ],
        };
    }

    private EffectLedgerReplayResult TryRecordWorkOrderDryRunLedger(
        string workOrderId,
        SessionGatewayMessageRequest request,
        SessionGatewayIntentEnvelopeSurface intentEnvelope,
        IReadOnlyList<SessionGatewayBoundArtifactSurface> boundArtifacts,
        SessionGatewayCapabilityLeaseSurface capabilityLease,
        SessionGatewayResourceLeaseSurface resourceLease,
        SessionGatewayExecutionTransactionDryRunSurface transactionDryRun)
    {
        var ledgerPath = effectLedgerService.GetWorkOrderLedgerPath(workOrderId);
        try
        {
            var utteranceHash = EffectLedgerService.ComputeContentHash(request.UserText);
            var objectBindingHash = ComputeStableHash(intentEnvelope.ObjectFocus);
            var boundArtifactHash = ComputeStableHash(boundArtifacts);
            var eventPrefix = $"EV-{workOrderId}";
            _ = effectLedgerService.AppendEvent(
                ledgerPath,
                new EffectLedgerEventDraft(
                    eventPrefix,
                    "intent_envelope",
                    "session_gateway",
                    ["classify_intent", "resolve_object_focus"],
                    ["classify_intent", "resolve_object_focus"],
                    [],
                    "accepted")
                {
                    WorkOrderId = workOrderId,
                    UtteranceHash = utteranceHash,
                    ObjectBindingHash = objectBindingHash,
                    AdmissionState = "admitted_dry_run",
                    TerminalState = WorkOrderTerminalState,
                    Facts = new Dictionary<string, string?>
                    {
                        ["primary_intent"] = intentEnvelope.PrimaryIntent,
                        ["candidate_level"] = intentEnvelope.CandidateLevel,
                        ["requires_work_order"] = intentEnvelope.RequiresWorkOrder.ToString().ToLowerInvariant(),
                        ["may_execute"] = intentEnvelope.MayExecute.ToString().ToLowerInvariant(),
                    },
                });
            _ = effectLedgerService.AppendEvent(
                ledgerPath,
                new EffectLedgerEventDraft(
                    eventPrefix,
                    "work_order_admission",
                    "host_admission",
                    ["bind_work_order_artifacts", "admit_work_order_dry_run"],
                    ["bind_work_order_artifacts", "admit_work_order_dry_run"],
                    [],
                    "admitted_dry_run")
                {
                    WorkOrderId = workOrderId,
                    UtteranceHash = utteranceHash,
                    ObjectBindingHash = objectBindingHash,
                    AdmissionState = "admitted_dry_run",
                    TerminalState = WorkOrderTerminalState,
                    Facts = new Dictionary<string, string?>
                    {
                        ["bound_artifacts_hash"] = boundArtifactHash,
                        ["bound_object_count"] = intentEnvelope.ObjectFocus.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                });
            _ = effectLedgerService.AppendEvent(
                ledgerPath,
                new EffectLedgerEventDraft(
                    eventPrefix,
                    "capability_lease",
                    "host_admission",
                    ["issue_capability_lease"],
                    ["issue_capability_lease"],
                    [],
                    capabilityLease.LeaseState)
                {
                    WorkOrderId = workOrderId,
                    LeaseId = capabilityLease.LeaseId,
                    UtteranceHash = utteranceHash,
                    ObjectBindingHash = objectBindingHash,
                    AdmissionState = "admitted_dry_run",
                    TerminalState = WorkOrderTerminalState,
                    Facts = new Dictionary<string, string?>
                    {
                        ["lease_state"] = capabilityLease.LeaseState,
                        ["capability_count"] = capabilityLease.CapabilityIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["execution_enabled"] = capabilityLease.ExecutionEnabled.ToString().ToLowerInvariant(),
                    },
                });
            _ = effectLedgerService.AppendEvent(
                ledgerPath,
                new EffectLedgerEventDraft(
                    eventPrefix,
                    "resource_lease",
                    "scheduler",
                    ["issue_resource_lease", "declare_write_set"],
                    ["issue_resource_lease", "declare_write_set"],
                    [],
                    resourceLease.LeaseState)
                {
                    WorkOrderId = workOrderId,
                    LeaseId = capabilityLease.LeaseId,
                    UtteranceHash = utteranceHash,
                    ObjectBindingHash = objectBindingHash,
                    AdmissionState = "admitted_dry_run",
                    TerminalState = WorkOrderTerminalState,
                    Facts = new Dictionary<string, string?>
                    {
                        ["resource_lease_id"] = resourceLease.LeaseId,
                        ["resource_lease_state"] = resourceLease.LeaseState,
                        ["task_ids"] = string.Join(",", resourceLease.DeclaredWriteSet.TaskIds),
                        ["paths"] = string.Join(",", resourceLease.DeclaredWriteSet.Paths),
                        ["modules"] = string.Join(",", resourceLease.DeclaredWriteSet.Modules),
                        ["truth_operations"] = string.Join(",", resourceLease.DeclaredWriteSet.TruthOperations),
                        ["target_branches"] = string.Join(",", resourceLease.DeclaredWriteSet.TargetBranches),
                        ["can_run_in_parallel"] = resourceLease.CanRunInParallel.ToString().ToLowerInvariant(),
                    },
                });
            _ = effectLedgerService.AppendEvent(
                ledgerPath,
                new EffectLedgerEventDraft(
                    eventPrefix,
                    "transaction_verified",
                    "execution_transaction_compiler",
                    ["compile_execution_transaction", "verify_execution_transaction"],
                    ["compile_execution_transaction", "verify_execution_transaction"],
                    [],
                    transactionDryRun.VerificationState)
                {
                    WorkOrderId = workOrderId,
                    TransactionId = transactionDryRun.TransactionId,
                    LeaseId = capabilityLease.LeaseId,
                    UtteranceHash = utteranceHash,
                    ObjectBindingHash = objectBindingHash,
                    AdmissionState = "admitted_dry_run",
                    TransactionHash = transactionDryRun.TransactionHash,
                    TerminalState = WorkOrderTerminalState,
                    TransactionStepIds = transactionDryRun.Steps.Select(static step => step.StepId).ToArray(),
                    Facts = new Dictionary<string, string?>
                    {
                        ["registry_version"] = transactionDryRun.RegistryVersion,
                        ["compiler_version"] = transactionDryRun.CompilerVersion,
                        ["step_count"] = transactionDryRun.Steps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    },
                });
            _ = effectLedgerService.AppendEvent(
                ledgerPath,
                new EffectLedgerEventDraft(
                    eventPrefix,
                    "terminal_state",
                    "host_admission",
                    ["record_terminal_state"],
                    ["record_terminal_state"],
                    [],
                    WorkOrderTerminalState)
                {
                    WorkOrderId = workOrderId,
                    TransactionId = transactionDryRun.TransactionId,
                    LeaseId = capabilityLease.LeaseId,
                    UtteranceHash = utteranceHash,
                    ObjectBindingHash = objectBindingHash,
                    AdmissionState = "admitted_dry_run",
                    TransactionHash = transactionDryRun.TransactionHash,
                    TerminalState = WorkOrderTerminalState,
                });
            _ = effectLedgerService.Seal(
                ledgerPath,
                new EffectLedgerSealDraft(eventPrefix, "host_admission")
                {
                    WorkOrderId = workOrderId,
                    TransactionId = transactionDryRun.TransactionId,
                    LeaseId = capabilityLease.LeaseId,
                    UtteranceHash = utteranceHash,
                    ObjectBindingHash = objectBindingHash,
                    AdmissionState = "admitted_dry_run",
                    TransactionHash = transactionDryRun.TransactionHash,
                    TerminalState = WorkOrderTerminalState,
                    TransactionStepIds = transactionDryRun.Steps.Select(static step => step.StepId).ToArray(),
                    Facts = new Dictionary<string, string?>
                    {
                        ["resource_lease_id"] = resourceLease.LeaseId,
                    },
                });

            return effectLedgerService.ReplayWorkOrder(workOrderId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return EffectLedgerReplayResult.Broken(
                effectLedgerService.ToRepoRelative(ledgerPath),
                "broken",
                [EffectLedgerService.AuditIncompleteStopReason],
                ex.Message);
        }
    }

    private static string ComputeStableHash<T>(T value)
    {
        return EffectLedgerService.ComputeContentHash(JsonSerializer.Serialize(
            value,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false,
            }));
    }
}
