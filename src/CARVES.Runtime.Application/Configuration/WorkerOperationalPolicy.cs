namespace Carves.Runtime.Application.Configuration;

public sealed record WorkerOperationalPolicy(
    string Version,
    string? PreferredBackendId,
    string PreferredTrustProfileId,
    WorkerApprovalOperationalPolicy Approval,
    WorkerRecoveryOperationalPolicy Recovery,
    WorkerObservabilityOperationalPolicy Observability)
{
    public static WorkerOperationalPolicy CreateDefault()
    {
        return new WorkerOperationalPolicy(
            Version: "1.0",
            PreferredBackendId: null,
            PreferredTrustProfileId: "workspace_build_test",
            Approval: new WorkerApprovalOperationalPolicy(
                OutsideWorkspaceRequiresReview: true,
                HighRiskRequiresReview: true,
                ManualApprovalModeRequiresReview: true,
                AutoAllowCategories: ["filesystem_write", "process_control"],
                AutoDenyCategories: ["secret_access", "elevated_privilege", "system_configuration"],
                ForceReviewCategories: ["filesystem_delete", "outside_workspace_access", "network_access", "unknown_permission_request"]),
            Recovery: new WorkerRecoveryOperationalPolicy(
                MaxRetryCount: 2,
                TransientInfraBackoffSeconds: 5,
                TimeoutBackoffSeconds: 10,
                InvalidOutputBackoffSeconds: 3,
                EnvironmentRebuildBackoffSeconds: 5,
                SwitchProviderOnEnvironmentBlocked: true,
                SwitchProviderOnUnavailableBackend: true),
            Observability: new WorkerObservabilityOperationalPolicy(
                ProviderDegradedLatencyMs: 1500,
                ApprovalQueuePreviewLimit: 8,
                BlockedQueuePreviewLimit: 8,
                IncidentPreviewLimit: 10,
                GovernanceReportDefaultHours: 24));
    }
}

public sealed record WorkerApprovalOperationalPolicy(
    bool OutsideWorkspaceRequiresReview,
    bool HighRiskRequiresReview,
    bool ManualApprovalModeRequiresReview,
    IReadOnlyList<string> AutoAllowCategories,
    IReadOnlyList<string> AutoDenyCategories,
    IReadOnlyList<string> ForceReviewCategories);

public sealed record WorkerRecoveryOperationalPolicy(
    int MaxRetryCount,
    int TransientInfraBackoffSeconds,
    int TimeoutBackoffSeconds,
    int InvalidOutputBackoffSeconds,
    int EnvironmentRebuildBackoffSeconds,
    bool SwitchProviderOnEnvironmentBlocked,
    bool SwitchProviderOnUnavailableBackend);

public sealed record WorkerObservabilityOperationalPolicy(
    int ProviderDegradedLatencyMs,
    int ApprovalQueuePreviewLimit,
    int BlockedQueuePreviewLimit,
    int IncidentPreviewLimit,
    int GovernanceReportDefaultHours);
