using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ResultIngestionService
{
    private static void Validate(string taskId, ResultEnvelope envelope)
    {
        if (!string.Equals(envelope.SchemaVersion, "1.0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Result envelope for '{taskId}' has unsupported schema version '{envelope.SchemaVersion}'.");
        }

        if (!string.Equals(envelope.TaskId, taskId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Result envelope task id mismatch. Expected '{taskId}', received '{envelope.TaskId}'.");
        }

        if (!string.IsNullOrWhiteSpace(envelope.ExecutionRunId)
            && !envelope.ExecutionRunId.StartsWith("RUN-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Result envelope for '{taskId}' has invalid executionRunId '{envelope.ExecutionRunId}'.");
        }

        if (!string.IsNullOrWhiteSpace(envelope.ExecutionEvidencePath)
            && !envelope.ExecutionEvidencePath.EndsWith("evidence.json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Result envelope for '{taskId}' has invalid executionEvidencePath '{envelope.ExecutionEvidencePath}'.");
        }

        if (envelope.CompletedStepCount is < 0)
        {
            throw new InvalidOperationException($"Result envelope for '{taskId}' cannot have negative completedStepCount.");
        }

        if (envelope.TotalStepCount is < 0)
        {
            throw new InvalidOperationException($"Result envelope for '{taskId}' cannot have negative totalStepCount.");
        }

        if (envelope.CompletedStepCount.HasValue
            && envelope.TotalStepCount.HasValue
            && envelope.CompletedStepCount.Value > envelope.TotalStepCount.Value)
        {
            throw new InvalidOperationException($"Result envelope for '{taskId}' cannot report completedStepCount greater than totalStepCount.");
        }

        EnsureValue(taskId, envelope.Status, "status", ["success", "failed", "blocked"]);
        EnsureValue(taskId, envelope.Validation.Build, "validation.build", ["success", "failed", "not_run"]);
        EnsureValue(taskId, envelope.Validation.Tests, "validation.tests", ["success", "failed", "not_run"]);
        if (!string.Equals(envelope.Telemetry.SchemaVersion, "1.0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Result envelope for '{taskId}' has unsupported telemetry schema version '{envelope.Telemetry.SchemaVersion}'.");
        }

        if (envelope.Telemetry.DurationSeconds < 0)
        {
            throw new InvalidOperationException($"Result envelope for '{taskId}' cannot have negative telemetry.durationSeconds.");
        }

        if (!string.IsNullOrWhiteSpace(envelope.Failure.Type)
            && !Enum.TryParse<FailureType>(envelope.Failure.Type, ignoreCase: true, out _))
        {
            throw new InvalidOperationException($"Result envelope for '{taskId}' has unsupported failure.type '{envelope.Failure.Type}'.");
        }
    }

    private static void EnsureValue(string taskId, string value, string field, IReadOnlyCollection<string> allowedValues)
    {
        if (!allowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Result envelope for '{taskId}' has unsupported {field} value '{value}'.");
        }
    }
}
