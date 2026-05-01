using System.Text;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Workers;

public sealed class WorkerFailureInterpreter
{
    public WorkerFailureInterpretation Interpret(WorkerExecutionResult result)
    {
        if (result.Status == WorkerExecutionStatus.ApprovalWait)
        {
            return new WorkerFailureInterpretation
            {
                FailureKind = WorkerFailureKind.ApprovalRequired,
                Retryable = false,
                ReasonCode = "approval_required",
                Summary = result.FailureReason ?? result.Summary,
                RawEvidence = CollectEvidence(result),
            };
        }

        if (result.FailureKind is not WorkerFailureKind.None and not WorkerFailureKind.Unknown)
        {
            var capturedEvidence = CollectEvidence(result);
            var refinedKind = RefineFailureKind(result, capturedEvidence);
            return new WorkerFailureInterpretation
            {
                FailureKind = refinedKind,
                Retryable = IsRetryable(refinedKind),
                ReasonCode = MapReasonCode(refinedKind),
                Summary = result.FailureReason ?? result.Summary,
                RawEvidence = capturedEvidence,
            };
        }

        var evidence = CollectEvidence(result);
        var failureKind = InferFailureKind(result.BackendId, result.ProviderId, evidence);
        return new WorkerFailureInterpretation
        {
            FailureKind = failureKind,
            Retryable = IsRetryable(failureKind),
            ReasonCode = MapReasonCode(failureKind),
            Summary = string.IsNullOrWhiteSpace(result.FailureReason) ? result.Summary : result.FailureReason,
            RawEvidence = evidence,
        };
    }

    public WorkerFailureInterpretation InterpretException(WorkerExecutionRequest request, IWorkerAdapter adapter, Exception exception)
    {
        var evidence = exception.ToString();
        var failureKind = InferFailureKind(adapter.BackendId, adapter.ProviderId, evidence);
        return new WorkerFailureInterpretation
        {
            FailureKind = failureKind,
            Retryable = IsRetryable(failureKind),
            ReasonCode = MapReasonCode(failureKind),
            Summary = exception.Message,
            RawEvidence = evidence,
        };
    }

    public static bool IsRetryable(WorkerFailureKind failureKind)
    {
        return failureKind is WorkerFailureKind.TransientInfra or WorkerFailureKind.Timeout or WorkerFailureKind.InvalidOutput;
    }

    private static WorkerFailureKind RefineFailureKind(WorkerExecutionResult result, string evidence)
    {
        if (result.FailureKind == WorkerFailureKind.InvalidOutput
            && result.FailureLayer == WorkerFailureLayer.Protocol)
        {
            return WorkerFailureKind.InvalidOutput;
        }

        if (result.FailureKind is not (WorkerFailureKind.TaskLogicFailed or WorkerFailureKind.InvalidOutput or WorkerFailureKind.EnvironmentBlocked))
        {
            return result.FailureKind;
        }

        var inferred = InferFailureKind(result.BackendId, result.ProviderId, evidence);
        return inferred == WorkerFailureKind.Unknown ? result.FailureKind : inferred;
    }

    private static WorkerFailureKind InferFailureKind(string? backendId, string? providerId, string evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence))
        {
            return WorkerFailureKind.Unknown;
        }

        if (ContainsAny(evidence, "permission denied", "access is denied", "not authorized", "policy denied"))
        {
            return WorkerFailureKind.PolicyDenied;
        }

        if (ContainsAny(evidence, "approval required", "awaiting operator", "approval wait", "permission approval"))
        {
            return WorkerFailureKind.ApprovalRequired;
        }

        if (ContainsAny(evidence, "attach failed", "attach handshake", "repo attach", "host discovery failed"))
        {
            return WorkerFailureKind.AttachFailure;
        }

        if (ContainsAny(evidence, "timed out", "timeout", "time out"))
        {
            return WorkerFailureKind.Timeout;
        }

        if (ContainsAny(evidence, "build failed", "compile error", "compiler error", "msbuild", "csc : error"))
        {
            return WorkerFailureKind.BuildFailure;
        }

        if (ContainsAny(evidence, "test failed", "assertion", "xunit", "nunit", "mstest"))
        {
            return WorkerFailureKind.TestFailure;
        }

        if (ContainsAny(evidence, "contract violation", "interface mismatch", "schema mismatch", "signature mismatch"))
        {
            return WorkerFailureKind.ContractFailure;
        }

        if (ContainsAny(evidence, "wrong file touched", "patch rejected", "forbidden path", "scope violation"))
        {
            return WorkerFailureKind.PatchFailure;
        }

        if (ContainsAny(evidence, "artifact path", "artifact write", "artifact save", "failed to persist artifact"))
        {
            return WorkerFailureKind.ArtifactFailure;
        }

        if (ContainsAny(evidence, "429", "rate limit", "temporarily unavailable", "connection reset", "socket hang up", "being used by another process", "file is in use"))
        {
            return WorkerFailureKind.TransientInfra;
        }

        if (ContainsAny(evidence, "invalid json", "invalid response", "parse error", "failed to parse", "schema"))
        {
            return WorkerFailureKind.InvalidOutput;
        }

        if (ContainsAny(evidence, "wrapper failed", "cli wrapper", "bootstrap wrapper", "bridge bootstrap"))
        {
            return WorkerFailureKind.WrapperFailure;
        }

        if (ContainsAny(evidence, "launch failed", "failed to start", "process start", "spawn node", "spawn codex"))
        {
            return WorkerFailureKind.LaunchFailure;
        }

        if (ContainsAny(evidence, "bridge script is not configured", "not configured", "not installed", "enoent", "environment"))
        {
            return WorkerFailureKind.EnvironmentBlocked;
        }

        if (string.Equals(backendId, "codex_sdk", StringComparison.OrdinalIgnoreCase)
            && ContainsAny(evidence, "bridge", "codex", "node"))
        {
            return WorkerFailureKind.EnvironmentBlocked;
        }

        if ((string.Equals(providerId, "openai", StringComparison.OrdinalIgnoreCase)
             || string.Equals(providerId, "claude", StringComparison.OrdinalIgnoreCase)
             || string.Equals(providerId, "gemini", StringComparison.OrdinalIgnoreCase))
            && ContainsAny(evidence, "api key", "authentication", "unauthorized"))
        {
            return WorkerFailureKind.EnvironmentBlocked;
        }

        return WorkerFailureKind.TaskLogicFailed;
    }

    private static string MapReasonCode(WorkerFailureKind failureKind)
    {
        return failureKind switch
        {
            WorkerFailureKind.TransientInfra => "transient_infra",
            WorkerFailureKind.EnvironmentBlocked => "environment_blocked",
            WorkerFailureKind.LaunchFailure => "launch_failure",
            WorkerFailureKind.AttachFailure => "attach_failure",
            WorkerFailureKind.WrapperFailure => "wrapper_failure",
            WorkerFailureKind.ArtifactFailure => "artifact_failure",
            WorkerFailureKind.BuildFailure => "build_failure",
            WorkerFailureKind.TestFailure => "test_failure",
            WorkerFailureKind.ContractFailure => "contract_failure",
            WorkerFailureKind.PatchFailure => "patch_failure",
            WorkerFailureKind.PolicyDenied => "policy_denied",
            WorkerFailureKind.TaskLogicFailed => "task_logic_failed",
            WorkerFailureKind.Timeout => "timeout",
            WorkerFailureKind.Cancelled => "cancelled",
            WorkerFailureKind.Aborted => "aborted",
            WorkerFailureKind.InvalidOutput => "invalid_output",
            WorkerFailureKind.ApprovalRequired => "approval_required",
            _ => "unknown_failure",
        };
    }

    private static string CollectEvidence(WorkerExecutionResult result)
    {
        var builder = new StringBuilder();
        void Append(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(value);
            }
        }

        Append(result.FailureReason);
        Append(result.Summary);
        Append(result.ResponsePreview);
        foreach (var @event in result.Events.Take(6))
        {
            Append(@event.RawPayload);
            Append(@event.Summary);
        }

        return builder.ToString();
    }

    private static bool ContainsAny(string value, params string[] patterns)
    {
        return patterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
}
