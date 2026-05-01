using Carves.Runtime.Domain.CodeGraph;

namespace Carves.Runtime.Infrastructure.CodeGraph;

public sealed partial class FileCodeGraphBuilder
{
    private sealed class FileCodeGraphScanState
    {
        public Dictionary<string, ModuleAccumulator> ModuleAccumulators { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<CodeGraphNode> FileNodes { get; } = [];

        public List<CodeGraphNode> TypeNodes { get; } = [];

        public List<CodeGraphNode> CallableNodes { get; } = [];

        public List<CodeGraphEdge> Edges { get; } = [];

        public List<FileAccumulator> FileAccumulators { get; } = [];
    }

    private sealed class ModuleAccumulator
    {
        public ModuleAccumulator(string name, string nodeId, string pathPrefix)
        {
            Name = name;
            NodeId = nodeId;
            PathPrefix = pathPrefix;
        }

        public string Name { get; }

        public string NodeId { get; }

        public string PathPrefix { get; }

        public HashSet<string> FileIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> DependencyModules { get; } = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> TopTokens { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FileAccumulator
    {
        public FileAccumulator(string nodeId, string path, string module, string language, string summary, IReadOnlyList<string> tokens, string content)
        {
            NodeId = nodeId;
            Path = path;
            Module = module;
            Language = language;
            Summary = summary;
            Tokens = tokens;
            Content = content;
        }

        public string NodeId { get; }

        public string Path { get; }

        public string Module { get; }

        public string Language { get; }

        public string Summary { get; }

        public IReadOnlyList<string> Tokens { get; }

        public string Content { get; }

        public HashSet<string> TypeIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> CallableIds { get; } = new(StringComparer.Ordinal);

        public HashSet<string> DependencyModules { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
