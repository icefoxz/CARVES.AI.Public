using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixArtifactManifestWriter
{
    public const string ManifestSchemaVersion = "matrix-artifact-manifest.v0";
    public const string DefaultManifestFileName = "matrix-artifact-manifest.json";
    public const string PortableArtifactRoot = ".";
    public const string RedactedLocalArtifactRoot = "<redacted-local-artifact-root>";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static IReadOnlyList<MatrixArtifactManifestRequirement> DefaultRequiredArtifacts => DefaultRequiredArtifactRequirements;

    public static IReadOnlyList<MatrixArtifactManifestRequirement> DefaultOptionalArtifacts => DefaultOptionalArtifactRequirements;

    public static string WriteDefaultProofManifest(string artifactRoot, DateTimeOffset? createdAt = null)
    {
        var entries = DefaultRequiredArtifactRequirements
            .Select(requirement => ToInput(requirement, required: true))
            .Concat(DefaultOptionalArtifactRequirements.Select(requirement => ToInput(requirement, required: false)))
            .ToArray();

        return WriteManifest(artifactRoot, entries, createdAt);
    }

    public static string WriteManifest(
        string artifactRoot,
        IEnumerable<MatrixArtifactManifestEntryInput> entries,
        DateTimeOffset? createdAt = null,
        string manifestFileName = DefaultManifestFileName)
    {
        if (string.IsNullOrWhiteSpace(artifactRoot))
        {
            throw new ArgumentException("Artifact root is required.", nameof(artifactRoot));
        }

        var fullArtifactRoot = Path.GetFullPath(artifactRoot);
        var manifestCreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        var privacy = MatrixArtifactPrivacyFlags.CreateSummaryOnly();
        var materializedEntries = entries
            .Select(entry => TryMaterializeEntry(fullArtifactRoot, entry, manifestCreatedAt, privacy))
            .Where(entry => entry is not null)
            .Cast<MatrixArtifactManifestEntry>()
            .ToArray();

        var manifest = new MatrixArtifactManifest(
            ManifestSchemaVersion,
            manifestCreatedAt,
            new MatrixArtifactManifestProducer("carves-matrix", "CARVES.Matrix.Core", "local_proof_bundle"),
            PortableArtifactRoot,
            RedactedLocalArtifactRoot,
            privacy,
            materializedEntries);

        var manifestPath = ResolveRelativePath(fullArtifactRoot, manifestFileName);
        var manifestDirectory = Path.GetDirectoryName(manifestPath);
        if (!string.IsNullOrWhiteSpace(manifestDirectory))
        {
            Directory.CreateDirectory(manifestDirectory);
        }

        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
        return manifestPath;
    }
}
