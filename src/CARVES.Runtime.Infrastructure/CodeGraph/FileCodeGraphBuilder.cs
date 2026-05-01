using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Infrastructure.CodeGraph;

public sealed partial class FileCodeGraphBuilder : ICodeGraphBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions DetailJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;

    public FileCodeGraphBuilder(string repoRoot, ControlPlanePaths paths, SystemConfig systemConfig)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.systemConfig = systemConfig;
    }

    public CodeGraphBuildResult Build()
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var state = new FileCodeGraphScanState();

        ScanSourceDirectories(state);
        ConnectDependencies(state);

        var moduleNodes = CreateModuleNodes(state);
        var moduleEntries = CreateModuleEntries(state);
        var graph = BuildGraph(generatedAt, state, moduleNodes);
        var index = BuildIndex(generatedAt, state, moduleEntries, moduleNodes);
        WriteOutputs(graph, index);

        var manifestPath = Path.Combine(paths.AiRoot, "codegraph", "manifest.json");
        var indexPath = Path.Combine(paths.AiRoot, "codegraph", "index.json");
        return new CodeGraphBuildResult(graph, index, manifestPath, indexPath);
    }
}
