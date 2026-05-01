namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static string? ResolveRuntimeRoot(string? explicitRuntimeRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRuntimeRoot))
        {
            var path = Path.GetFullPath(explicitRuntimeRoot);
            return File.Exists(Path.Combine(path, "CARVES.Runtime.sln")) ? path : null;
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CARVES.Runtime.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string ResolveArtifactRoot(string runtimeRoot, string? explicitArtifactRoot)
    {
        return string.IsNullOrWhiteSpace(explicitArtifactRoot)
            ? Path.GetFullPath(Path.Combine(runtimeRoot, "artifacts", "matrix"))
            : Path.GetFullPath(explicitArtifactRoot);
    }

    private static string ToPublicArtifactRootMarker()
    {
        return MatrixArtifactManifestWriter.PortableArtifactRoot;
    }

    private static string? ToPublicArtifactRootMarker(string artifactRoot, string? candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return null;
        }

        var normalizedCandidate = candidatePath.Replace('\\', '/');
        if (string.Equals(normalizedCandidate, MatrixArtifactManifestWriter.PortableArtifactRoot, StringComparison.Ordinal)
            || string.Equals(normalizedCandidate, MatrixArtifactManifestWriter.RedactedLocalArtifactRoot, StringComparison.Ordinal))
        {
            return normalizedCandidate;
        }

        if (!Path.IsPathRooted(candidatePath)
            && !(candidatePath.Length >= 2 && char.IsLetter(candidatePath[0]) && candidatePath[1] == ':'))
        {
            return string.Equals(normalizedCandidate, "..", StringComparison.Ordinal)
                   || normalizedCandidate.StartsWith("../", StringComparison.Ordinal)
                ? MatrixArtifactManifestWriter.RedactedLocalArtifactRoot
                : normalizedCandidate;
        }

        try
        {
            var fullRoot = Path.GetFullPath(artifactRoot);
            var fullCandidate = Path.GetFullPath(candidatePath);
            var relative = Path.GetRelativePath(fullRoot, fullCandidate).Replace('\\', '/');
            if (string.Equals(relative, ".", StringComparison.Ordinal))
            {
                return MatrixArtifactManifestWriter.PortableArtifactRoot;
            }

            if (!Path.IsPathRooted(relative)
                && !string.Equals(relative, "..", StringComparison.Ordinal)
                && !relative.StartsWith("../", StringComparison.Ordinal))
            {
                return relative;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }

        return MatrixArtifactManifestWriter.RedactedLocalArtifactRoot;
    }

    private static string ResolveMatrixScript(string runtimeRoot, string scriptName)
    {
        var scriptPath = Path.GetFullPath(Path.Combine(runtimeRoot, "scripts", "matrix", scriptName));
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Matrix script was not found: {scriptPath}", scriptPath);
        }

        return scriptPath;
    }
}
