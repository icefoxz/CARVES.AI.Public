namespace Carves.Runtime.Application.Shield;

public enum ShieldEvaluationOutput
{
    Lite,
    Standard,
    Combined,
}

public sealed record ShieldEvaluationResult(
    string SchemaVersion,
    string Status,
    string EvaluationPosture,
    string PrivacyPosture,
    bool Certification,
    string? EvidenceSchemaVersion,
    string? EvidenceId,
    int? SampleWindowDays,
    string? ConsumedEvidenceSha256,
    ShieldStandardResult? Standard,
    ShieldLiteResult? Lite,
    IReadOnlyList<ShieldEvaluationError> Errors)
{
    public bool IsOk => string.Equals(Status, ShieldEvaluationStatuses.Ok, StringComparison.Ordinal);
}

public static class ShieldEvaluationStatuses
{
    public const string Ok = "ok";
    public const string InvalidInput = "invalid_input";
    public const string UnsupportedSchema = "unsupported_schema";
    public const string InvalidPrivacyPosture = "invalid_privacy_posture";
}

public sealed record ShieldEvaluationError(
    string Code,
    string Message,
    IReadOnlyList<string> EvidenceRefs);

public sealed record ShieldStandardResult(
    string RubricId,
    string Label,
    IReadOnlyDictionary<string, ShieldStandardDimensionResult> Dimensions,
    int? OverallScore,
    IReadOnlyList<string> CriticalGates);

public sealed record ShieldStandardDimensionResult(
    string Dimension,
    string Level,
    int? NumericLevel,
    string Band,
    IReadOnlyList<string> CriticalGates,
    string Summary);

public sealed record ShieldLiteResult(
    string ModelId,
    int Score,
    string Band,
    bool SelfCheck,
    int? SampleWindowDays,
    IReadOnlyDictionary<string, ShieldDimensionContribution> DimensionContributions,
    IReadOnlyList<string> CriticalGates,
    IReadOnlyList<string> TopRisks,
    IReadOnlyList<string> NextSteps);

public sealed record ShieldDimensionContribution(
    string StandardLevel,
    int Weight,
    int Points);
