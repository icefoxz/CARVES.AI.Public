namespace Carves.Matrix.Core;

public static partial class MatrixArtifactManifestWriter
{
    private static MatrixArtifactManifestEntry? TryMaterializeEntry(
        string artifactRoot,
        MatrixArtifactManifestEntryInput input,
        DateTimeOffset createdAt,
        MatrixArtifactPrivacyFlags privacy)
    {
        var path = NormalizeRelativePath(input.Path);
        var fullPath = ResolveRelativePath(artifactRoot, path);
        if (!File.Exists(fullPath))
        {
            if (input.Required)
            {
                throw new FileNotFoundException($"Required Matrix artifact was not found: {path}", fullPath);
            }

            return null;
        }

        if (IsReparsePointOrSymbolicLink(fullPath))
        {
            throw new InvalidOperationException($"Matrix artifact must be a regular file, not a symbolic link or reparse point: {path}");
        }

        var fileInfo = new FileInfo(fullPath);
        return new MatrixArtifactManifestEntry(
            input.ArtifactKind,
            path,
            ComputeFileSha256(fullPath),
            fileInfo.Length,
            input.SchemaVersion,
            input.Producer,
            createdAt,
            privacy);
    }
}
