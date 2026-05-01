namespace Carves.Runtime.Domain.Tasks;

public sealed class ReviewDecisionDebt
{
    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> FollowUpActions { get; init; } = Array.Empty<string>();

    public bool RequiresFollowUpReview { get; init; } = true;

    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;
}
