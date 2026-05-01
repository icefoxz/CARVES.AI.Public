using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.ExecutionPolicy;

public sealed class ExecutionPathClassifier
{
    public IReadOnlyList<ExecutionChangeKind> ClassifyMany(IEnumerable<string> paths)
    {
        return paths
            .Select(Classify)
            .Distinct()
            .OrderBy(kind => kind)
            .ToArray();
    }

    public ExecutionChangeKind Classify(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ExecutionChangeKind.SourceCode;
        }

        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith(".ai/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".carves-platform/", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionChangeKind.ControlPlaneState;
        }

        if (normalized.StartsWith("docs/contracts/", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionChangeKind.Contracts;
        }

        if (normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionChangeKind.Documentation;
        }

        if (normalized.StartsWith("tests/", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionChangeKind.Tests;
        }

        if (normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".toml", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".props", StringComparison.OrdinalIgnoreCase))
        {
            return ExecutionChangeKind.Configuration;
        }

        return ExecutionChangeKind.SourceCode;
    }
}
