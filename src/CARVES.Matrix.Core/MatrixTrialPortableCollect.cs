using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private sealed record PortablePackageCollectPaths(
        string PackageRoot,
        string WorkspaceRoot,
        string ResultsRoot,
        string LocalResultsRoot,
        string SubmitBundleRoot);

    private static PortablePackageCollectPaths? TryResolvePortablePackageCollectPaths(MatrixTrialOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.WorkspaceRoot))
        {
            return null;
        }

        var packageRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var workspaceRoot = Path.Combine(packageRoot, "agent-workspace");
        var authorityRoot = Path.Combine(packageRoot, ".carves-pack");
        if (!Directory.Exists(workspaceRoot) || !Directory.Exists(authorityRoot))
        {
            return null;
        }

        var resultsRoot = Path.Combine(packageRoot, "results");
        return new PortablePackageCollectPaths(
            packageRoot,
            workspaceRoot,
            resultsRoot,
            Path.Combine(resultsRoot, "local"),
            Path.GetFullPath(string.IsNullOrWhiteSpace(options.BundleRoot)
                ? Path.Combine(resultsRoot, "submit-bundle")
                : options.BundleRoot));
    }

    private static void ThrowIfPortableCollectRootMissing(
        MatrixTrialOptions options,
        PortablePackageCollectPaths? paths)
    {
        if (string.IsNullOrWhiteSpace(options.WorkspaceRoot) && paths is null)
        {
            throw new InvalidOperationException(
                "Portable package root not found. Run trial collect from a package root containing agent-workspace/ and .carves-pack/, or pass --workspace <path>.");
        }
    }

    private static void WritePortablePackageLocalResults(
        PortablePackageCollectPaths? paths,
        TrialCommandResult result)
    {
        if (paths is null)
        {
            return;
        }

        Directory.CreateDirectory(paths.LocalResultsRoot);
        File.WriteAllText(
            Path.Combine(paths.LocalResultsRoot, "matrix-agent-trial-collect.json"),
            JsonSerializer.Serialize(result, JsonOptions) + Environment.NewLine);

        if (result.ResultCard is not null)
        {
            File.WriteAllText(
                Path.Combine(paths.LocalResultsRoot, "matrix-agent-trial-result-card.md"),
                result.ResultCard.Markdown);
        }
    }
}
