namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static string ResolveNativeArtifactRoot(string? explicitArtifactRoot)
    {
        return string.IsNullOrWhiteSpace(explicitArtifactRoot)
            ? Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "matrix", "native"))
            : Path.GetFullPath(explicitArtifactRoot);
    }

    private static void ResetNativeProofArtifacts(string artifactRoot)
    {
        var projectRoot = Path.Combine(artifactRoot, "project");
        Directory.CreateDirectory(projectRoot);

        var paths = MatrixArtifactManifestWriter.DefaultRequiredArtifacts
            .Select(artifact => artifact.Path)
            .Concat([
                MatrixArtifactManifestWriter.DefaultManifestFileName,
                "matrix-proof-summary.json",
                "project-matrix-output.json",
                "packaged-matrix-output.json",
                "packaged/matrix-packaged-summary.json",
            ]);
        foreach (var path in paths)
        {
            var fullPath = ResolveNativeRelativePath(artifactRoot, path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }

    private static string CreateNativeWorkRepoRoot(string? explicitWorkRoot)
    {
        var parent = string.IsNullOrWhiteSpace(explicitWorkRoot)
            ? Path.Combine(Path.GetTempPath(), "carves-matrix-native-proof")
            : Path.GetFullPath(explicitWorkRoot);
        Directory.CreateDirectory(parent);
        return Path.Combine(parent, "repo-" + Guid.NewGuid().ToString("N"));
    }
}
