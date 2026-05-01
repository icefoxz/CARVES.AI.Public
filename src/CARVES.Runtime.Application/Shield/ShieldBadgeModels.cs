namespace Carves.Runtime.Application.Shield;

public sealed record ShieldBadgeResult(
    string SchemaVersion,
    string Status,
    bool SelfCheck,
    bool Certification,
    string? OutputPath,
    ShieldBadgePayload? Badge,
    ShieldEvaluationResult Evaluation,
    IReadOnlyList<ShieldEvaluationError> Errors)
{
    public bool IsOk => string.Equals(Status, ShieldEvaluationStatuses.Ok, StringComparison.Ordinal);
}

public sealed record ShieldBadgePayload(
    string Label,
    string Message,
    string Color,
    string ColorName,
    string MessageTextColor,
    string StandardCompact,
    int? LiteScore,
    string? LiteBand,
    IReadOnlyList<string> CriticalGates,
    string AltText,
    string Markdown,
    string Svg);
