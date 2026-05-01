namespace Carves.Runtime.Application.CodeGraph;

public sealed record CodeGraphScopeAnalysis(
    IReadOnlyList<string> RequestedScopes,
    IReadOnlyList<string> Modules,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Callables,
    IReadOnlyList<string> DependencyModules,
    IReadOnlyList<string> SummaryLines)
{
    public static CodeGraphScopeAnalysis Empty { get; } = new(
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());

    public bool HasMatches =>
        Modules.Count > 0 ||
        Files.Count > 0 ||
        Callables.Count > 0 ||
        DependencyModules.Count > 0;
}
