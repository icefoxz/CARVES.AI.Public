namespace Carves.Runtime.Application.Artifacts;

public sealed class RuntimeArtifactPersistenceException : Exception
{
    public RuntimeArtifactPersistenceException(string artifactKind, string artifactPath, Exception innerException)
        : base($"Failed to persist runtime artifact '{artifactKind}' to '{artifactPath}'.", innerException)
    {
        ArtifactKind = artifactKind;
        ArtifactPath = artifactPath;
    }

    public string ArtifactKind { get; }

    public string ArtifactPath { get; }
}
