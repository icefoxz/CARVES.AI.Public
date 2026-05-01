namespace Carves.Runtime.Application.CodeGraph;

public sealed record CodeGraphImpactAnalysis(
    IReadOnlyList<string> RequestedScopes,
    IReadOnlyList<string> ImpactedModules,
    IReadOnlyList<string> ImpactedFiles,
    IReadOnlyList<string> SummaryLines)
{
    public static CodeGraphImpactAnalysis Empty { get; } = new(
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());

    public bool HasMatches =>
        ImpactedModules.Count > 0 ||
        ImpactedFiles.Count > 0;
}
