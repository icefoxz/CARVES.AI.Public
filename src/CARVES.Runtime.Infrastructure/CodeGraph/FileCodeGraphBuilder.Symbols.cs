using System.Text.RegularExpressions;
using Carves.Runtime.Domain.CodeGraph;

namespace Carves.Runtime.Infrastructure.CodeGraph;

public sealed partial class FileCodeGraphBuilder
{
    private static void ParseSymbols(
        FileAccumulator file,
        ModuleAccumulator module,
        ICollection<CodeGraphNode> typeNodes,
        ICollection<CodeGraphNode> callableNodes,
        ICollection<CodeGraphEdge> edges)
    {
        var lines = file.Content.ReplaceLineEndings("\n").Split('\n');
        string? currentTypeId = null;
        string? currentTypeQualifiedName = null;

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var typeMatch = file.Language switch
            {
                "csharp" => CSharpTypePattern.Match(line),
                "python" => PythonTypePattern.Match(line),
                _ => Match.Empty,
            };

            if (typeMatch.Success)
            {
                RegisterType(typeMatch.Groups["name"].Value, file, module, typeNodes, edges, index + 1, out currentTypeId, out currentTypeQualifiedName);
                continue;
            }

            var callableMatch = file.Language switch
            {
                "csharp" => CSharpCallablePattern.Match(line),
                "python" => PythonCallablePattern.Match(line),
                _ => Match.Empty,
            };

            if (!callableMatch.Success)
            {
                continue;
            }

            RegisterCallable(callableMatch.Groups["name"].Value, file, module, callableNodes, edges, currentTypeId, currentTypeQualifiedName, index + 1);
        }

        module.FileIds.Add(file.NodeId);
        foreach (var token in file.Tokens.Take(25))
        {
            module.TopTokens.Add(token);
        }
    }

    private static void RegisterType(
        string typeName,
        FileAccumulator file,
        ModuleAccumulator module,
        ICollection<CodeGraphNode> typeNodes,
        ICollection<CodeGraphEdge> edges,
        int lineNumber,
        out string currentTypeId,
        out string currentTypeQualifiedName)
    {
        currentTypeId = BuildNodeId("type", $"{file.Path}:{typeName}:{lineNumber}");
        currentTypeQualifiedName = $"{file.Module}.{typeName}";
        file.TypeIds.Add(currentTypeId);
        module.TopTokens.Add(typeName);
        typeNodes.Add(new CodeGraphNode(
            currentTypeId,
            "type",
            typeName,
            file.Path,
            file.Module,
            $"Type {typeName} in {file.Path}",
            [typeName.ToLowerInvariant()],
            ParentNodeId: file.NodeId,
            QualifiedName: currentTypeQualifiedName,
            Language: file.Language,
            LineStart: lineNumber,
            LineEnd: lineNumber));
        edges.Add(new CodeGraphEdge(file.NodeId, currentTypeId, "contains"));
    }

    private static void RegisterCallable(
        string callableName,
        FileAccumulator file,
        ModuleAccumulator module,
        ICollection<CodeGraphNode> callableNodes,
        ICollection<CodeGraphEdge> edges,
        string? currentTypeId,
        string? currentTypeQualifiedName,
        int lineNumber)
    {
        if (IsControlKeyword(callableName))
        {
            return;
        }

        var callableNodeId = BuildNodeId("callable", $"{file.Path}:{callableName}:{lineNumber}");
        var qualifiedName = currentTypeQualifiedName is not null
            ? $"{currentTypeQualifiedName}.{callableName}"
            : $"{file.Module}.{Path.GetFileNameWithoutExtension(file.Path)}.{callableName}";
        file.CallableIds.Add(callableNodeId);
        module.TopTokens.Add(callableName);
        callableNodes.Add(new CodeGraphNode(
            callableNodeId,
            "callable",
            callableName,
            file.Path,
            file.Module,
            $"Callable {qualifiedName}",
            [callableName.ToLowerInvariant()],
            ParentNodeId: currentTypeId ?? file.NodeId,
            QualifiedName: qualifiedName,
            Language: file.Language,
            LineStart: lineNumber,
            LineEnd: lineNumber));
        edges.Add(new CodeGraphEdge(currentTypeId ?? file.NodeId, callableNodeId, "contains"));
    }

    private static IEnumerable<string> DetermineDependencies(string content, string currentModule, IReadOnlyList<string> knownModules)
    {
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddDependenciesFromMatches(CSharpUsingPattern.Matches(content), knownModules, currentModule, dependencies, match => match.Groups[1].Value);
        AddDependenciesFromMatches(PythonImportPattern.Matches(content), knownModules, currentModule, dependencies, match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        return dependencies.OrderBy(name => name, StringComparer.OrdinalIgnoreCase);
    }

    private static void AddDependenciesFromMatches(
        MatchCollection matches,
        IReadOnlyList<string> knownModules,
        string currentModule,
        ISet<string> dependencies,
        Func<Match, string> selector)
    {
        foreach (Match match in matches)
        {
            var module = ResolveModule(selector(match), knownModules);
            if (module is not null && !string.Equals(module, currentModule, StringComparison.OrdinalIgnoreCase))
            {
                dependencies.Add(module);
            }
        }
    }

    private static string? ResolveModule(string candidate, IReadOnlyList<string> knownModules)
    {
        foreach (var module in knownModules)
        {
            if (candidate.StartsWith(module, StringComparison.OrdinalIgnoreCase))
            {
                return module;
            }
        }

        return null;
    }

    private static bool IsControlKeyword(string name)
    {
        return name is "if" or "for" or "while" or "switch" or "catch" or "foreach" or "using" or "lock";
    }
}
