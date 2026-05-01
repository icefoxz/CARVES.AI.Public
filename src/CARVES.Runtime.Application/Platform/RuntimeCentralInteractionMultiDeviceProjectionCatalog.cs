using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public static class RuntimeCentralInteractionMultiDeviceProjectionCatalog
{
    public static IReadOnlyList<RuntimeProjectionClassBoundarySurface> BuildProjectionClasses()
    {
        return
        [
            new()
            {
                ProjectionClassId = "host_control_node",
                PrimaryForm = "resident_or_cold_host",
                InspectScope = "full_runtime_owned_surface",
                RequestExecutionMode = "direct_host_owned",
                ApprovalMode = "host_owned",
                GovernedMutationMode = "host_routed_truth_mutation",
                OfficialTruthWriteMode = "allowed",
                Summary = "The only projection class that owns official truth mutation.",
            },
            new()
            {
                ProjectionClassId = "full_operator_workstation",
                PrimaryForm = "rich_desktop_shell_or_console",
                InspectScope = "broad_projection_and_drilldown",
                RequestExecutionMode = "host_mediated_request",
                ApprovalMode = "allowed_when_role_authority_permits",
                GovernedMutationMode = "request_only",
                OfficialTruthWriteMode = "blocked_direct_write",
                Summary = "A powerful projection surface that still does not become a second truth root.",
            },
            new()
            {
                ProjectionClassId = "thin_mobile_projection",
                PrimaryForm = "thin_shell_or_phone_client",
                InspectScope = "bounded_status_discussion_approval_and_evidence",
                RequestExecutionMode = "bounded_request_only",
                ApprovalMode = "allowed_when_role_authority_and_proof_requirements_are_met",
                GovernedMutationMode = "blocked_direct_execution",
                OfficialTruthWriteMode = "blocked_direct_write",
                Summary = "A lightweight projection surface that stays host-mediated and proof-aware.",
            },
            new()
            {
                ProjectionClassId = "external_agent_projection",
                PrimaryForm = "ai_facing_client_or_agent_consumer",
                InspectScope = "bounded_inspect_and_proposal_context",
                RequestExecutionMode = "proposal_or_request_only",
                ApprovalMode = "blocked_by_default",
                GovernedMutationMode = "blocked_direct_execution",
                OfficialTruthWriteMode = "blocked_direct_write",
                Summary = "An external agent client may help inspect and propose, but it never owns official state.",
            },
        ];
    }

    public static IReadOnlyList<RuntimeRoleAuthorityBoundarySurface> BuildRoleAuthorities()
    {
        return
        [
            new()
            {
                RoleId = "sponsor",
                TypicalProjectionClasses = ["full_operator_workstation", "thin_mobile_projection"],
                ObserveAuthority = "allowed",
                ProposeAuthority = "allowed",
                ApproveAuthority = "bounded_when_explicitly_delegated",
                ExecuteAuthority = "blocked",
                OfficialTruthWriteAuthority = "blocked",
                Summary = "Sponsors may observe and steer, but they do not directly mutate official Runtime truth.",
            },
            new()
            {
                RoleId = "reviewer",
                TypicalProjectionClasses = ["full_operator_workstation", "thin_mobile_projection"],
                ObserveAuthority = "allowed",
                ProposeAuthority = "allowed",
                ApproveAuthority = "review_verdict_only",
                ExecuteAuthority = "blocked",
                OfficialTruthWriteAuthority = "host_routed_review_outcome_only",
                Summary = "Reviewers may return review outcomes without becoming direct truth owners.",
            },
            new()
            {
                RoleId = "approver",
                TypicalProjectionClasses = ["full_operator_workstation", "thin_mobile_projection"],
                ObserveAuthority = "allowed",
                ProposeAuthority = "limited",
                ApproveAuthority = "allowed",
                ExecuteAuthority = "blocked",
                OfficialTruthWriteAuthority = "host_routed_approval_outcome_only",
                Summary = "Approvers may authorize bounded outcomes while host mediation remains mandatory.",
            },
            new()
            {
                RoleId = "operator",
                TypicalProjectionClasses = ["host_control_node", "full_operator_workstation"],
                ObserveAuthority = "allowed",
                ProposeAuthority = "allowed",
                ApproveAuthority = "allowed_when_policy_permits",
                ExecuteAuthority = "host_mediated_governed_request",
                OfficialTruthWriteAuthority = "host_routed_control_plane_mutation_only",
                Summary = "Operators may drive governed work, but official truth mutation still routes through CARVES Host.",
            },
            new()
            {
                RoleId = "evidence_submitter",
                TypicalProjectionClasses = ["thin_mobile_projection", "full_operator_workstation"],
                ObserveAuthority = "bounded",
                ProposeAuthority = "limited",
                ApproveAuthority = "blocked",
                ExecuteAuthority = "blocked",
                OfficialTruthWriteAuthority = "bounded_evidence_return_only",
                Summary = "Evidence submitters may return proof artifacts without becoming task or review truth owners.",
            },
            new()
            {
                RoleId = "governed_executor",
                TypicalProjectionClasses = ["host_control_node", "external_agent_projection"],
                ObserveAuthority = "bounded_task_packet_only",
                ProposeAuthority = "blocked",
                ApproveAuthority = "blocked",
                ExecuteAuthority = "allowed_under_host_governed_execution",
                OfficialTruthWriteAuthority = "blocked_direct_write",
                Summary = "Governed executors may execute under host control but do not own official truth mutation.",
            },
        ];
    }

    public static IReadOnlyList<RuntimeClientActionEnvelopeSurface> BuildClientActionEnvelopes()
    {
        return
        [
            new()
            {
                EnvelopeId = "status_and_posture_read",
                ClientClassId = "thin_mobile_projection",
                AllowedActions = ["inspect_status", "inspect_posture", "inspect_recommendation_packet"],
                HostMediationRequired = false,
                RequiredProofSource = "none",
                Summary = "Lightweight clients may inspect bounded posture without becoming local truth caches.",
            },
            new()
            {
                EnvelopeId = "discussion_and_clarification",
                ClientClassId = "thin_mobile_projection",
                AllowedActions = ["discuss", "clarify", "acknowledge_recommendation"],
                HostMediationRequired = true,
                RequiredProofSource = "repo_local_proof",
                Summary = "Discussion remains bounded and routed through the same CARVES lane.",
            },
            new()
            {
                EnvelopeId = "approval_return",
                ClientClassId = "thin_mobile_projection",
                AllowedActions = ["approve", "reject", "return_review_verdict"],
                HostMediationRequired = true,
                RequiredProofSource = "operator_run_proof",
                Summary = "Approval and rejection return through host mediation rather than device-local truth mutation.",
            },
            new()
            {
                EnvelopeId = "evidence_return",
                ClientClassId = "thin_mobile_projection",
                AllowedActions = ["attach_evidence_ref", "return_log", "return_artifact_ref"],
                HostMediationRequired = true,
                RequiredProofSource = "operator_run_proof",
                Summary = "Evidence return stays bounded by proof source and host acceptance.",
            },
            new()
            {
                EnvelopeId = "execution_and_recovery_request",
                ClientClassId = "thin_mobile_projection",
                AllowedActions = ["request_governed_run", "request_replan", "request_recovery"],
                HostMediationRequired = true,
                RequiredProofSource = "repo_local_proof",
                Summary = "Lightweight clients may request governed activity without directly dispatching or mutating truth.",
            },
        ];
    }

    public static IReadOnlyList<RuntimeExternalAgentIngressBoundarySurface> BuildExternalAgentIngressContracts()
    {
        return
        [
            new()
            {
                ContractId = "inspect",
                AllowedIngressActions = ["inspect_surface", "inspect_packet", "inspect_constraints"],
                BlockedIngressActions = ["direct_truth_mutation"],
                Summary = "External agents may inspect bounded Runtime truth but may not mutate it directly.",
            },
            new()
            {
                ContractId = "propose",
                AllowedIngressActions = ["draft", "propose_next_action", "propose_plan"],
                BlockedIngressActions = ["direct_task_completion", "direct_review_writeback"],
                Summary = "External agents may propose without becoming acceptance authorities.",
            },
            new()
            {
                ContractId = "request",
                AllowedIngressActions = ["request_governed_execution", "request_review_attention", "request_evidence_collection"],
                BlockedIngressActions = ["direct_dispatch", "direct_approval_mutation"],
                Summary = "External agents may request host-mediated work but cannot directly dispatch or approve it.",
            },
            new()
            {
                ContractId = "return_preparation_material",
                AllowedIngressActions = ["prepare_summary", "prepare_evidence_bundle", "prepare_debrief_packet"],
                BlockedIngressActions = ["direct_merge_acceptance", "direct_registry_mutation"],
                Summary = "Prepared material may help the host lane, but it does not become official truth before host acceptance.",
            },
        ];
    }

    public static IReadOnlyList<RuntimeSessionContinuityLaneBoundarySurface> BuildContinuityLanes()
    {
        return
        [
            new()
            {
                LaneId = "session_continuity",
                Trigger = "resume_same_governed_session_from_another_projection",
                HostBoundary = "same_runtime_truth_root",
                Summary = "Cross-device continuity is allowed only as projection continuity over the same Runtime-owned truth.",
            },
            new()
            {
                LaneId = "approval_return",
                Trigger = "approval_or_rejection_return_from_workstation_or_thin_mobile_projection",
                HostBoundary = "host_routed_approval_acceptance",
                Summary = "Approval returns are accepted by CARVES Host before official truth changes.",
            },
            new()
            {
                LaneId = "evidence_return",
                Trigger = "evidence_or_log_return_from_workstation_or_thin_mobile_projection",
                HostBoundary = "proof_aware_host_acceptance",
                Summary = "Evidence return remains bounded by proof source, actor identity, and host acceptance.",
            },
            new()
            {
                LaneId = "notification_return",
                Trigger = "bounded_notification_prompts_operator_reentry",
                HostBoundary = "same_task_and_review_posture",
                Summary = "Notifications return the user to the same governed session rather than to a second queue or planner.",
            },
        ];
    }
}
