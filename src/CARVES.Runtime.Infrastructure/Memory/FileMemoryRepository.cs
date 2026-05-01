using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Memory;

namespace Carves.Runtime.Infrastructure.Memory;

public sealed class FileMemoryRepository : IMemoryRepository
{
    private readonly ControlPlanePaths paths;

    public FileMemoryRepository(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public IReadOnlyList<MemoryDocument> LoadCategory(string category)
    {
        var root = Path.Combine(paths.AiRoot, "memory", category);
        if (!Directory.Exists(root))
        {
            return Array.Empty<MemoryDocument>();
        }

        return Directory
            .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new MemoryDocument(
                Path.GetRelativePath(paths.RepoRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
                category,
                Path.GetFileNameWithoutExtension(path),
                File.ReadAllText(path)))
            .ToArray();
    }

    public IReadOnlyList<MemoryDocument> LoadRelevantModules(IReadOnlyList<string> moduleNames)
    {
        var modules = LoadCategory("modules");
        if (moduleNames.Count == 0)
        {
            return modules;
        }

        var wanted = moduleNames
            .Select(name => name.ToLowerInvariant().Replace("-", "_", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        return modules
            .Where(document => wanted.Contains(document.Title.ToLowerInvariant().Replace("-", "_", StringComparison.Ordinal)))
            .ToArray();
    }
}
