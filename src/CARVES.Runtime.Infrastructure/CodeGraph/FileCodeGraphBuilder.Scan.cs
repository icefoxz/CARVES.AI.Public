using System.Text.RegularExpressions;
using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Application.CodeGraph;

namespace Carves.Runtime.Infrastructure.CodeGraph;

public sealed partial class FileCodeGraphBuilder
{
    private static readonly Regex TokenPattern = new("[A-Za-z_][A-Za-z0-9_]+", RegexOptions.Compiled);
    private static readonly Regex CSharpUsingPattern = new(@"^\s*using\s+([A-Za-z0-9_.]+)\s*;", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex PythonImportPattern = new(@"^\s*(?:from\s+([A-Za-z0-9_.]+)\s+import|import\s+([A-Za-z0-9_.]+))", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex CSharpTypePattern = new(@"^\s*(?:public|private|internal|protected|sealed|abstract|static|partial|\s)*(class|record|interface|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex CSharpCallablePattern = new(@"^\s*(?:\[.*\]\s*)*(?:public|private|internal|protected|static|virtual|override|sealed|async|extern|partial|\s)+[\w<>\[\],?.]+\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);
    private static readonly Regex PythonTypePattern = new(@"^\s*class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex PythonCallablePattern = new(@"^\s*def\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);

    private void ScanSourceDirectories(FileCodeGraphScanState state)
    {
        foreach (var directory in CodeDirectoryDiscoveryPolicy.ResolveEffectiveDirectories(repoRoot, systemConfig))
        {
            ScanDirectory(state, directory);
        }
    }

    private void ScanDirectory(FileCodeGraphScanState state, string directory)
    {
        var absoluteDirectory = string.Equals(directory, ".", StringComparison.Ordinal)
            ? repoRoot
            : Path.GetFullPath(Path.Combine(repoRoot, directory.Replace('/', Path.DirectorySeparatorChar)));
        if (!Directory.Exists(absoluteDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(absoluteDirectory, "*.*", SearchOption.AllDirectories))
        {
            if (!CodeGraphSourceTruthPolicy.ShouldTrackFile(repoRoot, path, systemConfig))
            {
                continue;
            }

            AddFile(state, path);
        }
    }

    private void AddFile(FileCodeGraphScanState state, string path)
    {
        var relativePath = Path.GetRelativePath(repoRoot, path).Replace(Path.DirectorySeparatorChar, '/');
        var content = File.ReadAllText(path);
        var module = DetermineModule(relativePath);
        var moduleAccumulator = GetOrCreateModule(state.ModuleAccumulators, module, DetermineModulePathPrefix(relativePath, module));
        var language = DetermineLanguage(path);
        var tokens = TokenPattern.Matches(content)
            .Select(match => match.Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Take(200)
            .ToArray();
        var fileNodeId = BuildNodeId("file", relativePath);

        var fileNode = new CodeGraphNode(
            fileNodeId,
            "file",
            Path.GetFileNameWithoutExtension(path),
            relativePath,
            module,
            $"{language} file {relativePath}",
            tokens,
            ParentNodeId: moduleAccumulator.NodeId,
            QualifiedName: relativePath,
            Language: language);
        state.FileNodes.Add(fileNode);
        state.Edges.Add(new CodeGraphEdge(moduleAccumulator.NodeId, fileNodeId, "contains"));

        var fileAccumulator = new FileAccumulator(fileNodeId, relativePath, module, language, fileNode.Summary, tokens, content);
        ParseSymbols(fileAccumulator, moduleAccumulator, state.TypeNodes, state.CallableNodes, state.Edges);
        state.FileAccumulators.Add(fileAccumulator);
    }

    private void ConnectDependencies(FileCodeGraphScanState state)
    {
        var knownModules = state.ModuleAccumulators.Keys.OrderByDescending(name => name.Length).ToArray();
        foreach (var file in state.FileAccumulators)
        {
            foreach (var dependency in DetermineDependencies(file.Content, file.Module, knownModules))
            {
                if (!state.ModuleAccumulators.TryGetValue(dependency, out var targetModule))
                {
                    continue;
                }

                if (file.DependencyModules.Add(dependency))
                {
                    state.Edges.Add(new CodeGraphEdge(file.NodeId, targetModule.NodeId, "depends_on", $"File {file.Path} depends on module {dependency}."));
                }

                if (state.ModuleAccumulators[file.Module].DependencyModules.Add(dependency))
                {
                    state.Edges.Add(new CodeGraphEdge(state.ModuleAccumulators[file.Module].NodeId, targetModule.NodeId, "depends_on", $"Module {file.Module} depends on module {dependency}."));
                }
            }
        }
    }

    private static string DetermineLanguage(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".py" => "python",
            _ => "text",
        };
    }

    private static string DetermineModule(string relativePath)
    {
        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
        {
            return "(root)";
        }

        return string.Equals(parts[0], "src", StringComparison.OrdinalIgnoreCase) && parts.Length > 1
            ? parts[1]
            : parts[0];
    }

    private static string DetermineModulePathPrefix(string relativePath, string module)
    {
        if (string.Equals(module, "(root)", StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var slash = relativePath.IndexOf('/', StringComparison.Ordinal);
        if (slash < 0)
        {
            return relativePath;
        }

        var prefix = $"{relativePath[..slash]}/{module}/";
        return relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? prefix : $"{relativePath[..slash]}/";
    }

    private static ModuleAccumulator GetOrCreateModule(IDictionary<string, ModuleAccumulator> modules, string name, string pathPrefix)
    {
        if (modules.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var created = new ModuleAccumulator(name, BuildNodeId("module", name), pathPrefix);
        modules[name] = created;
        return created;
    }

    private static string BuildNodeId(string kind, string value)
    {
        return $"{kind}:{value.Replace('\\', '/').Trim()}";
    }
}
