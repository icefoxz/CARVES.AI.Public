namespace Carves.Runtime.Application.Safety;

public static class SafetyLayerSemantics
{
    public const string PreExecutionBoundaryLayerId = "worker_execution_boundary";
    public const string ChangeObservationLayerId = "change_observation";
    public const string PostExecutionSafetyLayerId = "safety_service";

    public static IReadOnlyList<SafetyLayerSemantic> WorkerExecutionLayers { get; } =
    [
        new SafetyLayerSemantic(
            PreExecutionBoundaryLayerId,
            "pre_execution",
            "blocking_request_gate",
            "before_worker_adapter_launch",
            [
                "trust_profile_allowed",
                "repo_scope_allowed",
                "validation_command_prefix_allowed",
            ],
            [
                "does_not_know_future_patch_paths",
                "does_not_measure_patch_size",
                "does_not_provide_process_filesystem_containment",
            ]),
        new SafetyLayerSemantic(
            ChangeObservationLayerId,
            "post_execution_observation",
            "evidence_collection",
            "after_worker_adapter_returns",
            [
                "reported_changed_files",
                "observed_git_changed_files",
            ],
            [
                "does_not_prevent_worker_writes",
                "does_not_replace_review_or_safety_decision",
            ]),
        new SafetyLayerSemantic(
            PostExecutionSafetyLayerId,
            "post_execution",
            "blocking_report_gate",
            "after_worker_execution_and_validation",
            [
                "file_access_policy",
                "task_scope_policy",
                "patch_size_policy",
                "test_and_retry_policy",
            ],
            [
                "does_not_launch_before_worker_execution",
                "does_not_rollback_filesystem_mutations",
                "does_not_provide_process_filesystem_containment",
            ]),
    ];

    public static string Summary =>
        "pre_execution=worker_execution_boundary; observation=change_observation; post_execution=safety_service";

    public static string FormatEvidence(string layerId)
    {
        var layer = WorkerExecutionLayers.First(item => string.Equals(item.LayerId, layerId, StringComparison.Ordinal));
        return $"safety layer {layer.LayerId}: phase={layer.Phase}; authority={layer.Authority}; timing={layer.EnforcementTiming}";
    }
}

public sealed record SafetyLayerSemantic(
    string LayerId,
    string Phase,
    string Authority,
    string EnforcementTiming,
    IReadOnlyList<string> Enforces,
    IReadOnlyList<string> NonClaims);
