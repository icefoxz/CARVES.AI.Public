namespace Carves.Runtime.Domain.Execution;

public static class WorkerFailureSemantics
{
    public static bool IsExecutionSubstrateFailure(WorkerExecutionResult result)
    {
        return Classify(result).Lane == WorkerFailureLane.Substrate
               && result.Status != WorkerExecutionStatus.ApprovalWait;
    }

    public static string? ClassifyExecutionSubstrateFailure(WorkerExecutionResult result)
    {
        return Classify(result).SubstrateCategory;
    }

    public static WorkerFailureClassification Classify(WorkerExecutionResult result)
    {
        if (result.Status is WorkerExecutionStatus.Succeeded or WorkerExecutionStatus.Skipped)
        {
            return new WorkerFailureClassification
            {
                Kind = result.FailureKind,
                Lane = WorkerFailureLane.Unknown,
                Retryable = false,
                ReplanAllowed = false,
                ReasonCode = "no_failure",
                TaskStatusRecommendation = "completed",
                NextAction = "observe downstream tasks",
            };
        }

        if (result.Status == WorkerExecutionStatus.ApprovalWait || result.FailureKind == WorkerFailureKind.ApprovalRequired)
        {
            return new WorkerFailureClassification
            {
                Kind = WorkerFailureKind.ApprovalRequired,
                Lane = WorkerFailureLane.Substrate,
                Retryable = false,
                ReplanAllowed = false,
                ReasonCode = "approval_required",
                SubstrateCategory = "approval_wait",
                TaskStatusRecommendation = "approval_wait",
                NextAction = "resolve permission approval before continuing execution",
            };
        }

        if (result.FailureKind == WorkerFailureKind.PolicyDenied)
        {
            return new WorkerFailureClassification
            {
                Kind = result.FailureKind,
                Lane = WorkerFailureLane.Substrate,
                Retryable = false,
                ReplanAllowed = false,
                ReasonCode = "policy_denied",
                SubstrateCategory = "permission_boundary",
                TaskStatusRecommendation = "blocked",
                NextAction = "review policy boundary and operator approval requirements",
            };
        }

        var substrateCategory = ClassifySubstrateCategory(result);
        if (substrateCategory is not null)
        {
            return new WorkerFailureClassification
            {
                Kind = result.FailureKind,
                Lane = WorkerFailureLane.Substrate,
                Retryable = result.Retryable,
                ReplanAllowed = false,
                ReasonCode = substrateCategory,
                SubstrateCategory = substrateCategory,
                TaskStatusRecommendation = result.Retryable ? "pending" : "blocked",
                NextAction = substrateCategory switch
                {
                    "environment_setup_failed" => "repair delegated worker environment or launch contract",
                    "delegated_worker_launch_failed" => "repair worker launch path or replace the worker backend",
                    "delegated_worker_hung" => "recover the stuck delegated run before retrying",
                    "delegated_worker_invalid_bootstrap_output" => "inspect protocol bootstrap output and runtime wrapper contract",
                    "artifact_path_failure" => "repair artifact path or worktree writeability before retrying",
                    _ => "repair execution substrate before semantic retry",
                },
            };
        }

        var isSemantic = result.FailureKind is WorkerFailureKind.TaskLogicFailed
            or WorkerFailureKind.BuildFailure
            or WorkerFailureKind.TestFailure
            or WorkerFailureKind.ContractFailure
            or WorkerFailureKind.PatchFailure;
        if (isSemantic)
        {
            return new WorkerFailureClassification
            {
                Kind = result.FailureKind,
                Lane = WorkerFailureLane.Semantic,
                Retryable = result.Retryable,
                ReplanAllowed = true,
                ReasonCode = MapSemanticReasonCode(result.FailureKind),
                TaskStatusRecommendation = result.Retryable ? "pending" : "review",
                NextAction = "review semantic failure and decide retry, repair, or replan",
            };
        }

        return new WorkerFailureClassification
        {
            Kind = result.FailureKind,
            Lane = WorkerFailureLane.Unknown,
            Retryable = result.Retryable,
            ReplanAllowed = false,
            ReasonCode = "unknown_failure",
            TaskStatusRecommendation = "blocked",
            NextAction = "inspect evidence and classify before replan or writeback",
        };
    }

    private static string? ClassifySubstrateCategory(WorkerExecutionResult result)
    {
        if (result.FailureKind is WorkerFailureKind.EnvironmentBlocked or WorkerFailureKind.LaunchFailure or WorkerFailureKind.AttachFailure or WorkerFailureKind.WrapperFailure)
        {
            if (ContainsAny(result, "preflight", "dotnet --info", "DOTNET_CLI_HOME", "temp root", "worktree root", "worktree directory"))
            {
                return "environment_setup_failed";
            }

            if (result.FailureKind == WorkerFailureKind.AttachFailure)
            {
                return "attach_failure";
            }

            if (result.FailureKind == WorkerFailureKind.WrapperFailure)
            {
                return "wrapper_failure";
            }

            return "delegated_worker_launch_failed";
        }

        if (result.FailureKind == WorkerFailureKind.ArtifactFailure)
        {
            return "artifact_path_failure";
        }

        if (result.FailureKind == WorkerFailureKind.Timeout
            && result.FailureLayer is WorkerFailureLayer.Environment or WorkerFailureLayer.Transport or WorkerFailureLayer.Provider)
        {
            return "delegated_worker_hung";
        }

        if ((result.FailureKind is WorkerFailureKind.InvalidOutput or WorkerFailureKind.ContractFailure)
            && result.FailureLayer == WorkerFailureLayer.Protocol)
        {
            return "delegated_worker_invalid_bootstrap_output";
        }

        if (result.FailureLayer is WorkerFailureLayer.Transport or WorkerFailureLayer.Provider)
        {
            return "delegated_worker_launch_failed";
        }

        if (result.FailureLayer == WorkerFailureLayer.Environment)
        {
            return "execution_substrate_failure";
        }

        return null;
    }

    private static string MapSemanticReasonCode(WorkerFailureKind kind)
    {
        return kind switch
        {
            WorkerFailureKind.BuildFailure => "build_failure",
            WorkerFailureKind.TestFailure => "test_failure",
            WorkerFailureKind.ContractFailure => "contract_failure",
            WorkerFailureKind.PatchFailure => "patch_failure",
            WorkerFailureKind.TaskLogicFailed => "task_logic_failed",
            _ => "semantic_failure",
        };
    }

    private static bool ContainsAny(WorkerExecutionResult result, params string[] patterns)
    {
        var evidence = string.Join(
            Environment.NewLine,
            new[]
            {
                result.Summary,
                result.FailureReason,
                result.ResponsePreview,
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return patterns.Any(pattern => evidence.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}
