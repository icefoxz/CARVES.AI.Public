using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Failures;

public sealed class FailureClassificationService
{
    public FailureType Classify(ResultEnvelope result)
    {
        if (TryParseFailureType(result.Failure.Type, out var parsed))
        {
            return parsed;
        }

        if (string.Equals(result.Validation.Build, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Result.StopReason, "build_failure", StringComparison.OrdinalIgnoreCase))
        {
            return FailureType.BuildFailure;
        }

        if (string.Equals(result.Validation.Tests, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Result.StopReason, "test_failure", StringComparison.OrdinalIgnoreCase))
        {
            return FailureType.TestRegression;
        }

        return result.Result.StopReason.ToLowerInvariant() switch
        {
            "timeout" => FailureType.Timeout,
            "review_rejected" => FailureType.ReviewRejected,
            "contract_violation" => FailureType.ContractViolation,
            "wrong_file_touched" => FailureType.WrongFileTouched,
            "infinite_patch_loop" => FailureType.InfinitePatchLoop,
            "incomplete_task" => FailureType.IncompleteTask,
            "scope_drift" => FailureType.ScopeDrift,
            "dependency_misread" => FailureType.DependencyMisread,
            "environment_failure" or "blocked" => FailureType.EnvironmentFailure,
            _ => FailureType.Unknown,
        };
    }

    public FailureType Classify(RuntimeFailureRecord failure, WorkerExecutionResult? workerResult = null)
    {
        if (failure.FailureType == RuntimeFailureType.ReviewRejected)
        {
            return FailureType.ReviewRejected;
        }

        if (workerResult is not null)
        {
            var classification = WorkerFailureSemantics.Classify(workerResult);
            if (classification.Lane == WorkerFailureLane.Substrate)
            {
                return FailureType.EnvironmentFailure;
            }

            return workerResult.FailureKind switch
            {
                WorkerFailureKind.Timeout => FailureType.Timeout,
                WorkerFailureKind.BuildFailure => FailureType.BuildFailure,
                WorkerFailureKind.TestFailure => FailureType.TestRegression,
                WorkerFailureKind.ContractFailure => FailureType.ContractViolation,
                WorkerFailureKind.PatchFailure => FailureType.WrongFileTouched,
                WorkerFailureKind.TaskLogicFailed => FailureType.IncompleteTask,
                WorkerFailureKind.EnvironmentBlocked => FailureType.EnvironmentFailure,
                WorkerFailureKind.TransientInfra => FailureType.EnvironmentFailure,
                WorkerFailureKind.InvalidOutput => FailureType.ContractViolation,
                WorkerFailureKind.PolicyDenied => FailureType.EnvironmentFailure,
                WorkerFailureKind.ApprovalRequired => FailureType.EnvironmentFailure,
                _ => FailureType.Unknown,
            };
        }

        return failure.FailureType switch
        {
            RuntimeFailureType.ArtifactPersistenceFailure => FailureType.EnvironmentFailure,
            RuntimeFailureType.ControlPlaneDesync => FailureType.EnvironmentFailure,
            RuntimeFailureType.SchemaMismatch => FailureType.ContractViolation,
            RuntimeFailureType.InvariantViolation => FailureType.ContractViolation,
            RuntimeFailureType.SchedulerDecisionFailure => FailureType.Unknown,
            RuntimeFailureType.WorkerExecutionFailure => FailureType.Unknown,
            _ => FailureType.Unknown,
        };
    }

    public FailureAttribution BuildAttribution(RuntimeFailureRecord failure, WorkerExecutionResult? workerResult = null)
    {
        if (failure.FailureType == RuntimeFailureType.ReviewRejected)
        {
            return new FailureAttribution
            {
                Layer = FailureAttributionLayer.Worker,
                Confidence = 0.8,
                Notes = "Review rejected an execution outcome after validation.",
            };
        }

        if (workerResult is null)
        {
            return new FailureAttribution
            {
                Layer = FailureAttributionLayer.Environment,
                Confidence = 0.7,
                Notes = "Failure was classified from runtime-level exception handling.",
            };
        }

        var classification = WorkerFailureSemantics.Classify(workerResult);
        if (classification.Lane == WorkerFailureLane.Substrate)
        {
            return workerResult.FailureLayer is WorkerFailureLayer.Provider or WorkerFailureLayer.Transport
                ? new FailureAttribution
                {
                    Layer = FailureAttributionLayer.Provider,
                    Confidence = 0.8,
                    Notes = $"Execution substrate failure '{classification.SubstrateCategory ?? classification.ReasonCode}' originated below task semantics.",
                }
                : new FailureAttribution
                {
                    Layer = FailureAttributionLayer.Environment,
                    Confidence = 0.85,
                    Notes = $"Execution substrate failure '{classification.SubstrateCategory ?? classification.ReasonCode}' requires runtime correction rather than semantic replan.",
                };
        }

        return workerResult.FailureKind switch
        {
            WorkerFailureKind.BuildFailure => new FailureAttribution
            {
                Layer = FailureAttributionLayer.Worker,
                Confidence = 0.85,
                Notes = "Execution reached compilation and failed due to an implementation-level build error.",
            },
            WorkerFailureKind.TestFailure => new FailureAttribution
            {
                Layer = FailureAttributionLayer.Worker,
                Confidence = 0.85,
                Notes = "Execution reached verification and failed due to a task-level test regression.",
            },
            WorkerFailureKind.ContractFailure or WorkerFailureKind.PatchFailure => new FailureAttribution
            {
                Layer = FailureAttributionLayer.Worker,
                Confidence = 0.8,
                Notes = "Execution changed code but produced a semantic contract or patch-level mismatch.",
            },
            WorkerFailureKind.Timeout => new FailureAttribution
            {
                Layer = FailureAttributionLayer.Provider,
                Confidence = 0.7,
                Notes = "Execution timed out while waiting on the delegated backend.",
            },
            WorkerFailureKind.TransientInfra => new FailureAttribution
            {
                Layer = FailureAttributionLayer.Provider,
                Confidence = 0.7,
                Notes = "Transient infrastructure or transport failure from the worker backend.",
            },
            WorkerFailureKind.InvalidOutput => new FailureAttribution
            {
                Layer = FailureAttributionLayer.Provider,
                Confidence = 0.65,
                Notes = "Backend output could not be normalized into governed runtime truth.",
            },
            WorkerFailureKind.EnvironmentBlocked or WorkerFailureKind.PolicyDenied or WorkerFailureKind.ApprovalRequired => new FailureAttribution
            {
                Layer = FailureAttributionLayer.Environment,
                Confidence = 0.8,
                Notes = "Execution was blocked by environment or runtime governance.",
            },
            WorkerFailureKind.TaskLogicFailed => new FailureAttribution
            {
                Layer = FailureAttributionLayer.Worker,
                Confidence = 0.75,
                Notes = "Execution completed far enough to indicate an implementation-level failure.",
            },
            _ => new FailureAttribution
            {
                Layer = FailureAttributionLayer.Environment,
                Confidence = 0.5,
                Notes = "Failure classification fell back to the generic runtime boundary.",
            },
        };
    }

    public FailureAttribution BuildAttribution(ResultEnvelope result)
    {
        return new FailureAttribution
        {
            Layer = FailureAttributionLayer.Worker,
            Confidence = string.Equals(result.Status, "blocked", StringComparison.OrdinalIgnoreCase) ? 0.7 : 0.8,
            Notes = "Result envelope failures default to worker attribution in v1.",
        };
    }

    private static bool TryParseFailureType(string? raw, out FailureType failureType)
    {
        if (!string.IsNullOrWhiteSpace(raw) && Enum.TryParse<FailureType>(raw, ignoreCase: true, out failureType))
        {
            return true;
        }

        failureType = FailureType.Unknown;
        return false;
    }
}
