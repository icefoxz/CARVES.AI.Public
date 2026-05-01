namespace Carves.Runtime.Domain.Execution;

public sealed record PatchSummary(
    int FilesChanged,
    int LinesAdded,
    int LinesRemoved,
    bool Estimated,
    IReadOnlyList<string> Paths)
{
    public int TotalLinesChanged => LinesAdded + LinesRemoved;

    public static PatchSummary Empty { get; } = new(0, 0, 0, true, Array.Empty<string>());
}
