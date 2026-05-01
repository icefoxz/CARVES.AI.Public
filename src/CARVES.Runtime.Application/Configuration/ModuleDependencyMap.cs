namespace Carves.Runtime.Application.Configuration;

public sealed record ModuleDependencyMap(IReadOnlyDictionary<string, IReadOnlyList<string>> Entries)
{
    public static ModuleDependencyMap Empty { get; } = new ModuleDependencyMap(
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
}
