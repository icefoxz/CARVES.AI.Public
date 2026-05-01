namespace Carves.Runtime.Domain.Tasks;

public sealed class ReviewWritebackRecord
{
    public bool Applied { get; init; }

    public DateTimeOffset? AppliedAt { get; init; }

    public string? SourcePath { get; init; }

    public string? ResultCommit { get; init; }

    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();

    public string Summary { get; init; } = string.Empty;
}
