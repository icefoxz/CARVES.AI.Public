using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeBetaProgramStatusService
{
    private readonly string repoRoot;
    private readonly Func<RuntimeSessionGatewayInternalBetaGateSurface> internalBetaGateFactory;
    private readonly Func<RuntimeFirstRunOperatorPacketSurface> firstRunPacketFactory;
    private readonly Func<RuntimeSessionGatewayInternalBetaExitContractSurface> internalBetaExitContractFactory;

    public RuntimeBetaProgramStatusService(
        string repoRoot,
        Func<RuntimeSessionGatewayInternalBetaGateSurface> internalBetaGateFactory,
        Func<RuntimeFirstRunOperatorPacketSurface> firstRunPacketFactory,
        Func<RuntimeSessionGatewayInternalBetaExitContractSurface> internalBetaExitContractFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.internalBetaGateFactory = internalBetaGateFactory;
        this.firstRunPacketFactory = firstRunPacketFactory;
        this.internalBetaExitContractFactory = internalBetaExitContractFactory;
    }

    public RuntimeBetaProgramStatusSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        const string programStatusDocPath = "docs/runtime/runtime-beta-program-status.md";
        const string routingMapPath = "docs/runtime/runtime-beta-product-routing-map.md";
        const string redundancySweepDocPath = "docs/runtime/runtime-first-batch-redundancy-sweep.md";

        ValidatePath(programStatusDocPath, "Runtime beta program status doc", errors);
        ValidatePath(routingMapPath, "Runtime beta routing map", errors);
        ValidatePath(redundancySweepDocPath, "Runtime first-batch redundancy sweep", errors);

        var internalBetaGate = internalBetaGateFactory();
        var firstRunPacket = firstRunPacketFactory();
        var internalBetaExitContract = internalBetaExitContractFactory();

        errors.AddRange(internalBetaGate.Errors.Select(error => $"Internal beta gate surface: {error}"));
        warnings.AddRange(internalBetaGate.Warnings.Select(warning => $"Internal beta gate surface: {warning}"));
        errors.AddRange(firstRunPacket.Errors.Select(error => $"First-run operator packet surface: {error}"));
        warnings.AddRange(firstRunPacket.Warnings.Select(warning => $"First-run operator packet surface: {warning}"));
        errors.AddRange(internalBetaExitContract.Errors.Select(error => $"Internal beta exit contract surface: {error}"));
        warnings.AddRange(internalBetaExitContract.Warnings.Select(warning => $"Internal beta exit contract surface: {warning}"));

        var upgradePhase = BuildUpgradePhase(errors, warnings);
        var telemetryPhase = BuildTelemetryPhase(errors, warnings);
        var bootstrapPhase = BuildBootstrapPhase(firstRunPacket, internalBetaExitContract, errors, warnings);
        var meteringPhase = BuildMeteringPhase(errors, warnings);
        var hostedBrokerPhase = BuildHostedBrokerPhase(internalBetaGate, meteringPhase, errors, warnings);

        var phases = new[]
        {
            upgradePhase,
            telemetryPhase,
            bootstrapPhase,
            meteringPhase,
            hostedBrokerPhase,
        };

        var valid = phases.All(phase => string.Equals(phase.OverallPosture, "contract_ready", StringComparison.Ordinal)) && errors.Count == 0;
        if (!valid && warnings.Count == 0)
        {
            warnings.Add("Runtime beta program status remains blocked until every merged phase contract is readable and every upstream Runtime-owned dependency posture is ready.");
        }

        return new RuntimeBetaProgramStatusSurface
        {
            ProgramStatusDocPath = programStatusDocPath,
            RoutingMapPath = routingMapPath,
            RedundancySweepDocPath = redundancySweepDocPath,
            OverallPosture = valid ? "runtime_beta_program_status_ready" : "blocked_by_runtime_beta_program_gaps",
            LiveEntryCommand = RuntimeHostCommandLauncher.Cold("inspect", "runtime-beta-program-status"),
            ConsolidatedSurfaceCount = phases.Length,
            ConsolidatedFromSurfaceIds =
            [
                "runtime-beta-upgrade-readiness",
                "runtime-beta-telemetry-readiness",
                "runtime-beta-bootstrap-readiness",
                "runtime-beta-metering-readiness",
                "runtime-beta-hosted-broker-readiness",
            ],
            RuntimeOwnedProgramBoundaries =
            [
                "runtime remains the only execution kernel and official truth ingress",
                "operator shell growth stays outside CARVES.Runtime",
                "cloud backplane ownership stays outside CARVES.Runtime",
                "the merged beta packet keeps P1 to P5 boundary meaning without keeping five parallel projection families",
            ],
            Phases = phases,
            RecommendedNextAction = valid
                ? "Use runtime-beta-program-status as the single Runtime-owned beta-program query surface; keep any follow-on Runtime cards limited to real execution-kernel seams, not new split readiness packets."
                : "Restore the missing Runtime-owned beta-program inputs before treating the merged status as frozen on the Runtime lane.",
            IsValid = valid,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "This contract is a Runtime-owned merge of beta readiness projections, not a cross-repo beta shipment claim.",
                "This contract does not delete the underlying P1 to P5 supporting docs.",
                "This contract does not make Operator-owned or cloud-owned follow-on work local to Runtime.",
            ],
        };
    }

    private RuntimeBetaProgramPhaseSurface BuildUpgradePhase(List<string> errors, List<string> warnings)
    {
        const string supportingDocPath = "docs/runtime/runtime-beta-upgrade-readiness.md";
        var supportingRefs = new[]
        {
            supportingDocPath,
            "docs/runtime/runtime-kernel-upgrade-qualification.md",
            "docs/runtime/host-restart-session-rehydration-proof.md",
            "docs/runtime/host-session-rehydration.md",
            "docs/runtime/operational-summary-surface.md",
            "docs/runtime/runtime-export-and-data-boundary.md",
            "docs/session-gateway/BUG_REPORT_BUNDLE.md",
            "docs/session-gateway/ALPHA_SETUP.md",
            "docs/session-gateway/ALPHA_QUICKSTART.md",
        };

        ValidatePaths("P1 upgrade", supportingRefs, errors);
        var ready = supportingRefs.All(PathExists);
        if (!ready)
        {
            warnings.Add("P1 upgrade references are incomplete for the merged Runtime beta program status.");
        }

        return new RuntimeBetaProgramPhaseSurface
        {
            PhaseId = "P1",
            PhaseTitle = "upgrade_and_version_delivery",
            SupportingDocPath = supportingDocPath,
            OverallPosture = ready ? "contract_ready" : "blocked",
            CrossRepoState = "operator_and_cloud_follow_on_required",
            SupportingReferencePaths = supportingRefs,
            RuntimeOwnedAreas =
            [
                "local_truth_and_data_root_boundary",
                "restart_and_rehydration_contract",
                "health_check_command_contract",
                "rollback_and_bug_bundle_evidence_contract",
                "explicit_routing_out_of_installer_supervisor_and_release_service",
            ],
            QueryEntryCommands =
            [
                RuntimeHostCommandLauncher.Cold("host", "status"),
                RuntimeHostCommandLauncher.Cold("api", "operational-summary"),
                RuntimeHostCommandLauncher.Cold("status", "--summary"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-kernel-upgrade-qualification"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-beta-program-status"),
            ],
            OperatorOwnedFollowOn =
            [
                "installer",
                "supervisor_or_updater_process",
                "restart_after_upgrade_user_flow",
                "local_version_and_rollback_visibility",
                "first_run_product_shell_over_runtime",
            ],
            CloudOwnedFollowOn =
            [
                "release_manifest_truth",
                "signed_package_distribution",
                "installation_and_channel_targeting",
                "release_service_administration",
            ],
            BlockedClaims =
            [
                "runtime already ships the long_term installer shell",
                "runtime already ships a supervisor or updater process",
                "runtime already owns release manifests or package distribution",
                "local restart and rollback proofs are equivalent to shipped external beta delivery",
            ],
        };
    }

    private RuntimeBetaProgramPhaseSurface BuildTelemetryPhase(List<string> errors, List<string> warnings)
    {
        const string supportingDocPath = "docs/runtime/runtime-beta-telemetry-readiness.md";
        var supportingRefs = new[]
        {
            supportingDocPath,
            "docs/contracts/execution-telemetry.schema.json",
            "docs/contracts/boundary-telemetry.schema.json",
            "docs/contracts/operator-os-events.schema.json",
            "docs/contracts/telemetry-queue-batch.schema.json",
            "docs/runtime/delegation-telemetry.md",
            "docs/runtime/operator-os-event-stream.md",
            "docs/runtime/runtime-export-and-data-boundary.md",
            "docs/session-gateway/BUG_REPORT_BUNDLE.md",
        };

        ValidatePaths("P2 telemetry", supportingRefs, errors);
        var ready = supportingRefs.All(PathExists);
        if (!ready)
        {
            warnings.Add("P2 telemetry references are incomplete for the merged Runtime beta program status.");
        }

        return new RuntimeBetaProgramPhaseSurface
        {
            PhaseId = "P2",
            PhaseTitle = "telemetry_and_diagnostic_return",
            SupportingDocPath = supportingDocPath,
            OverallPosture = ready ? "contract_ready" : "blocked",
            CrossRepoState = "operator_and_cloud_follow_on_required",
            SupportingReferencePaths = supportingRefs,
            RuntimeOwnedAreas =
            [
                "telemetry_event_family_and_schema_boundary",
                "default_redaction_and_export_boundary",
                "local_durable_telemetry_queue_envelope_contract",
                "bug_bundle_evidence_and_opt_in_diagnostic_boundary",
                "explicit_routing_out_of_operator_settings_and_cloud_ingestion_retention_support",
            ],
            QueryEntryCommands =
            [
                RuntimeHostCommandLauncher.Cold("api", "operational-summary"),
                RuntimeHostCommandLauncher.Cold("api", "os-events"),
                RuntimeHostCommandLauncher.Cold("worker", "health", "--no-refresh"),
                RuntimeHostCommandLauncher.Cold("status", "--summary"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-beta-program-status"),
            ],
            OperatorOwnedFollowOn =
            [
                "telemetry_settings_ui",
                "pending_upload_visibility",
                "bug_bundle_submission_ux",
                "local_diagnostics_viewer",
            ],
            CloudOwnedFollowOn =
            [
                "telemetry_ingestion_api",
                "retry_and_aggregation_service",
                "retention_and_warehousing",
                "bug_bundle_intake_pipeline",
                "support_triage_tooling",
            ],
            BlockedClaims =
            [
                "runtime already ships the telemetry settings experience",
                "runtime already ships a cloud ingestion queue worker",
                "runtime already owns retention and support truth",
                "freezing a local queue envelope is equivalent to shipped hosted telemetry delivery",
            ],
        };
    }

    private RuntimeBetaProgramPhaseSurface BuildBootstrapPhase(
        RuntimeFirstRunOperatorPacketSurface firstRunPacket,
        RuntimeSessionGatewayInternalBetaExitContractSurface internalBetaExitContract,
        List<string> errors,
        List<string> warnings)
    {
        const string supportingDocPath = "docs/runtime/runtime-beta-bootstrap-readiness.md";
        var supportingRefs = new[]
        {
            supportingDocPath,
            "docs/runtime/runtime-bootstrap-onboarding-implementation-workmap.md",
            "docs/runtime/runtime-trusted-bootstrap-truth-schema.md",
            "docs/runtime/runtime-agent-bootstrap-packet.md",
            "docs/runtime/runtime-first-run-operator-packet.md",
            "docs/runtime/runtime-onboarding-acceleration-contract.md",
            "docs/session-gateway/internal-beta-exit-contract.md",
        };

        ValidatePaths("P3 bootstrap", supportingRefs, errors);
        var ready =
            supportingRefs.All(PathExists)
            && string.Equals(firstRunPacket.OverallPosture, "first_run_packet_ready", StringComparison.Ordinal)
            && string.Equals(internalBetaExitContract.OverallPosture, "internal_beta_exit_contract_ready", StringComparison.Ordinal);

        if (!string.Equals(firstRunPacket.OverallPosture, "first_run_packet_ready", StringComparison.Ordinal))
        {
            warnings.Add($"P3 bootstrap depends on first-run operator packet posture {firstRunPacket.OverallPosture}.");
        }

        if (!string.Equals(internalBetaExitContract.OverallPosture, "internal_beta_exit_contract_ready", StringComparison.Ordinal))
        {
            warnings.Add($"P3 bootstrap depends on internal beta exit contract posture {internalBetaExitContract.OverallPosture}.");
        }

        return new RuntimeBetaProgramPhaseSurface
        {
            PhaseId = "P3",
            PhaseTitle = "bootstrap_and_first_run_acceleration",
            SupportingDocPath = supportingDocPath,
            OverallPosture = ready ? "contract_ready" : "blocked",
            CrossRepoState = "operator_and_cloud_follow_on_required",
            SupportingReferencePaths = supportingRefs,
            RuntimeOwnedAreas =
            [
                "trusted_bootstrap_truth_schema",
                "runtime_agent_bootstrap_packet_and_receipt_seams",
                "first_run_operator_packet_bridge",
                "bounded_application_of_bootstrap_guidance_into_runtime_owned_truth",
                "explicit_routing_out_of_operator_onboarding_shell_and_cloud_bootstrap_service",
            ],
            QueryEntryCommands =
            [
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-agent-bootstrap-packet"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-agent-bootstrap-receipt"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-first-run-operator-packet"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-internal-beta-exit-contract"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-beta-program-status"),
            ],
            OperatorOwnedFollowOn =
            [
                "first_run_onboarding_flow",
                "project_attach_wizard",
                "provider_selection_and_setup_ui",
                "discuss_plan_governedrun_explanation_surface",
            ],
            CloudOwnedFollowOn =
            [
                "bootstrap_service",
                "hosted_bootstrap_agent_packet_generation",
                "installation_scoped_onboarding_checklist_generation",
            ],
            BlockedClaims =
            [
                "the Runtime slice of P3 means carves init is shipped",
                "the Runtime slice of P3 means onboard is shipped",
                "onboarding shell and provider setup UX belong inside CARVES.Runtime",
                "completing this contract means a cloud bootstrap service may write repo or .ai truth directly",
            ],
        };
    }

    private RuntimeBetaProgramPhaseSurface BuildMeteringPhase(List<string> errors, List<string> warnings)
    {
        const string supportingDocPath = "docs/runtime/runtime-beta-metering-readiness.md";
        var supportingRefs = new[]
        {
            supportingDocPath,
            "docs/runtime/pricing-packaging-spec-v1.md",
            "docs/runtime/carves-credits-backend-minimal-architecture.md",
            "docs/runtime/runtime-export-and-data-boundary.md",
            "docs/runtime/open-core-cloud-boundary.md",
            "docs/runtime/provider-quota-routing.md",
            "docs/runtime/worker-selection-operator-surface.md",
            "docs/contracts/usage-ledger-cache.schema.json",
        };

        ValidatePaths("P4 metering", supportingRefs, errors);
        var ready = supportingRefs.All(PathExists);
        if (!ready)
        {
            warnings.Add("P4 metering references are incomplete for the merged Runtime beta program status.");
        }

        return new RuntimeBetaProgramPhaseSurface
        {
            PhaseId = "P4",
            PhaseTitle = "metering_credits_and_hosted_cost_accounting",
            SupportingDocPath = supportingDocPath,
            OverallPosture = ready ? "contract_ready" : "blocked",
            CrossRepoState = "operator_and_cloud_follow_on_required",
            SupportingReferencePaths = supportingRefs,
            RuntimeOwnedAreas =
            [
                "hosted_versus_byok_attribution_hooks",
                "route_trace_and_lane_visibility_inputs",
                "local_usage_ledger_cache_projection",
                "reserve_and_settle_receipt_projection_for_runtime_owned_runs",
                "explicit_routing_out_of_operator_balance_ui_and_cloud_billing_authority",
            ],
            QueryEntryCommands =
            [
                RuntimeHostCommandLauncher.Cold("api", "routing-profile"),
                RuntimeHostCommandLauncher.Cold("api", "provider-quota"),
                RuntimeHostCommandLauncher.Cold("api", "worker-selection", "<repo-id>"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-beta-program-status"),
            ],
            OperatorOwnedFollowOn =
            [
                "credits_balance_display",
                "hosted_mode_usage_visibility",
                "lane_cost_fallback_explanation_ui",
                "low_balance_and_downgrade_prompts",
            ],
            CloudOwnedFollowOn =
            [
                "append_only_credits_ledger",
                "reserve_and_settle_logic",
                "entitlements_and_grants",
                "billing_reconciliation",
                "provider_cost_attribution",
            ],
            BlockedClaims =
            [
                "the Runtime slice of P4 means balances now live in CARVES.Runtime",
                "the Runtime slice of P4 means payment processing belongs inside CARVES.Runtime",
                "the Runtime slice of P4 means cloud reserve-settle logic is already implemented locally",
                "route visibility and local cache projection are equivalent to shipped billing truth",
            ],
        };
    }

    private RuntimeBetaProgramPhaseSurface BuildHostedBrokerPhase(
        RuntimeSessionGatewayInternalBetaGateSurface internalBetaGate,
        RuntimeBetaProgramPhaseSurface meteringPhase,
        List<string> errors,
        List<string> warnings)
    {
        const string supportingDocPath = "docs/runtime/runtime-beta-hosted-broker-readiness.md";
        var supportingRefs = new[]
        {
            supportingDocPath,
            "docs/session-gateway/gate-policy.md",
            "docs/session-gateway/session-api.md",
            "docs/runtime/pricing-packaging-spec-v1.md",
            "docs/runtime/carves-credits-backend-minimal-architecture.md",
            "docs/runtime/open-core-cloud-boundary.md",
            "docs/runtime/runtime-export-and-data-boundary.md",
            "docs/runtime/provider-quota-routing.md",
            "docs/runtime/worker-selection-operator-surface.md",
            "docs/contracts/hosted-action-receipt.schema.json",
        };

        ValidatePaths("P5 hosted broker", supportingRefs, errors);
        var ready =
            supportingRefs.All(PathExists)
            && string.Equals(internalBetaGate.OverallPosture, "internal_beta_gated_ready", StringComparison.Ordinal)
            && string.Equals(internalBetaGate.BrokerMode, "strict_broker", StringComparison.Ordinal)
            && string.Equals(meteringPhase.OverallPosture, "contract_ready", StringComparison.Ordinal);

        if (!string.Equals(internalBetaGate.OverallPosture, "internal_beta_gated_ready", StringComparison.Ordinal))
        {
            warnings.Add($"P5 hosted broker depends on internal beta gate posture {internalBetaGate.OverallPosture}.");
        }

        if (!string.Equals(internalBetaGate.BrokerMode, "strict_broker", StringComparison.Ordinal))
        {
            warnings.Add($"P5 hosted broker depends on broker mode {internalBetaGate.BrokerMode}.");
        }

        if (!string.Equals(meteringPhase.OverallPosture, "contract_ready", StringComparison.Ordinal))
        {
            warnings.Add("P5 hosted broker depends on a ready P4 metering contract inside the merged Runtime beta program status.");
        }

        return new RuntimeBetaProgramPhaseSurface
        {
            PhaseId = "P5",
            PhaseTitle = "hosted_agent_broker",
            SupportingDocPath = supportingDocPath,
            OverallPosture = ready ? "contract_ready" : "blocked",
            CrossRepoState = "operator_and_cloud_follow_on_required",
            SupportingReferencePaths = supportingRefs,
            RuntimeOwnedAreas =
            [
                "strict_broker_boundary",
                "hosted_lane_ingress_contracts",
                "proof_approval_and_receipt_rules_for_hosted_actions",
                "local_acceptance_and_projection_of_hosted_outcomes",
                "explicit_routing_out_of_operator_hosted_controls_and_cloud_broker_authority",
            ],
            QueryEntryCommands =
            [
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-session-gateway-internal-beta-gate"),
                RuntimeHostCommandLauncher.Cold("api", "routing-profile"),
                RuntimeHostCommandLauncher.Cold("api", "provider-quota"),
                RuntimeHostCommandLauncher.Cold("api", "worker-selection", "<repo-id>"),
                RuntimeHostCommandLauncher.Cold("inspect", "runtime-beta-program-status"),
            ],
            OperatorOwnedFollowOn =
            [
                "hosted_mode_opt_in_ux",
                "hosted_run_diagnostics",
                "provider_lane_explanation_surface",
                "fallback_and_retry_explanation_surface",
            ],
            CloudOwnedFollowOn =
            [
                "hosted_agent_broker",
                "provider_gateway_and_routing",
                "hosted_job_dispatch",
                "budget_and_quota_enforcement",
                "broker_side_observability",
            ],
            BlockedClaims =
            [
                "the Runtime slice of P5 means the hosted agent broker now lives in CARVES.Runtime",
                "the Runtime slice of P5 means provider gateway hosted dispatch or quota enforcement are implemented locally",
                "completing this contract means cloud may directly accept repo mutations without Runtime-owned acceptance",
                "strict broker ingress and hosted receipts authorize a second execution scheduler",
            ],
        };
    }

    private void ValidatePaths(string phaseLabel, IEnumerable<string> repoRelativePaths, List<string> errors)
    {
        foreach (var repoRelativePath in repoRelativePaths)
        {
            ValidatePath(repoRelativePath, $"{phaseLabel} support", errors);
        }
    }

    private void ValidatePath(string repoRelativePath, string label, List<string> errors)
    {
        if (!PathExists(repoRelativePath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private bool PathExists(string repoRelativePath)
    {
        var fullPath = Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(fullPath);
    }
}
