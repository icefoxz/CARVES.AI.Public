namespace Carves.Runtime.Domain.Runtime;

public sealed record PromptSafeProjection
{
    public string Summary { get; init; } = string.Empty;

    public string? DetailRef { get; init; }

    public string? DetailHash { get; init; }

    public string? ExcerptTail { get; init; }

    public int OriginalLength { get; init; }

    public bool Truncated { get; init; }
}
