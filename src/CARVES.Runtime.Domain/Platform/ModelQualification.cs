using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Domain.Platform;

public sealed class ModelQualificationMatrix
{
    public string SchemaVersion { get; init; } = "model-qualification-matrix.v1";

    public string MatrixId { get; init; } = "current-connected-lanes";

    public string Version { get; init; } = "1";

    public string? Summary { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public int DefaultAttempts { get; init; } = 3;

    public ModelQualificationLane[] Lanes { get; init; } = [];

    public ModelQualificationCase[] Cases { get; init; } = [];
}

public sealed class ModelQualificationLane
{
    public string LaneId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string RequestFamily { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public string ApiKeyEnvironmentVariable { get; init; } = string.Empty;

    public string RoutingProfileId { get; init; } = string.Empty;

    public string? RouteGroup { get; init; }

    public string? ObservedVariance { get; init; }

    public string? Summary { get; init; }
}

public sealed class ModelQualificationCase
{
    public string CaseId { get; init; } = string.Empty;

    public string RoutingIntent { get; init; } = string.Empty;

    public string? ModuleId { get; init; }

    public string Prompt { get; init; } = string.Empty;

    public ModelQualificationExpectedFormat ExpectedFormat { get; init; } = ModelQualificationExpectedFormat.Text;

    public string[] RequiredJsonFields { get; init; } = [];

    public int? Attempts { get; init; }

    public string? Summary { get; init; }
}

public enum ModelQualificationExpectedFormat
{
    Text,
    Json,
}

public sealed class ModelQualificationRunLedger
{
    public string SchemaVersion { get; init; } = "model-qualification-run.v1";

    public string RunId { get; init; } = $"qual-run-{Guid.NewGuid():N}";

    public string MatrixId { get; init; } = string.Empty;

    public string? Summary { get; init; }

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public ModelQualificationResult[] Results { get; init; } = [];
}

public sealed class ModelQualificationResult
{
    public string ResultId { get; init; } = $"qual-result-{Guid.NewGuid():N}";

    public string RunId { get; init; } = string.Empty;

    public string LaneId { get; init; } = string.Empty;

    public string CaseId { get; init; } = string.Empty;

    public int Attempt { get; init; }

    public string ProviderId { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string RequestFamily { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string RoutingIntent { get; init; } = string.Empty;

    public string? ModuleId { get; init; }

    public long? LatencyMs { get; init; }

    public bool Success { get; init; }

    public int? HttpStatus { get; init; }

    public bool FormatValid { get; init; }

    public double QualityScore { get; init; }

    public int? TokensInput { get; init; }

    public int? TokensOutput { get; init; }

    public string? ErrorType { get; init; }

    public WorkerFailureKind FailureKind { get; init; } = WorkerFailureKind.None;

    public string? Notes { get; init; }

    public string? RouteGroup { get; init; }

    public string? ObservedVariance { get; init; }

    public string? RequestId { get; init; }
}

public sealed class ModelQualificationCandidateProfile
{
    public string SchemaVersion { get; init; } = "model-qualification-candidate-profile.v1";

    public string CandidateId { get; init; } = $"routing-candidate-{Guid.NewGuid():N}";

    public string MatrixId { get; init; } = string.Empty;

    public string SourceRunId { get; init; } = string.Empty;

    public string? Summary { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public RuntimeRoutingProfile Profile { get; init; } = new();

    public ModelQualificationIntentSummary[] Intents { get; init; } = [];
}

public sealed class ModelQualificationIntentSummary
{
    public string RoutingIntent { get; init; } = string.Empty;

    public string? ModuleId { get; init; }

    public string PreferredLaneId { get; init; } = string.Empty;

    public string[] FallbackLaneIds { get; init; } = [];

    public string[] RejectLaneIds { get; init; } = [];

    public ModelQualificationLaneScore[] LaneScores { get; init; } = [];
}

public sealed class ModelQualificationLaneScore
{
    public string LaneId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string RequestFamily { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public double SuccessRate { get; init; }

    public double FormatValidityRate { get; init; }

    public double AverageQualityScore { get; init; }

    public double AverageLatencyMs { get; init; }

    public string Decision { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}
